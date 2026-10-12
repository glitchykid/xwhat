using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using XWhat.Core;

namespace XWhat.App.Services;

internal sealed class PresentMonCapture(SettingsStore store) : IAsyncDisposable
{
    private Process? _process;
    private Task? _reader;
    private Task? _errors;
    private int _target;
    private string _session = "";
    private volatile FrameWindow _frames = new();
    private string _status = "Выберите игру или переключитесь в неё";
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly double _qpcOrigin = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    private double _retryAfter;
    public FrameSnapshot Read(double seconds, double threshold) => _frames.Read(_clock.Elapsed.TotalSeconds, seconds, threshold);
    public string Status => Volatile.Read(ref _status);
    public string TargetName { get; private set; } = "Ожидание игры";

    public async Task EnsureTargetAsync(string processName)
    {
        var target = ResolveTarget(processName);
        if (target == _target && _process is { HasExited: false }) return;
        if (target == _target && target == 0) return;
        if (target == _target && _clock.Elapsed.TotalSeconds < _retryAfter) return;
        await StopAsync();
        _frames = new();
        _target = target;
        _retryAfter = _clock.Elapsed.TotalSeconds + 5;
        if (target == 0) { _status = "Игра не найдена"; TargetName = "Ожидание игры"; return; }
        var path = Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon.exe");
        if (!File.Exists(path)) { _status = "PresentMon.exe не найден в папке tools"; return; }
        _session = $"XWhat-{Environment.ProcessId}";
        var start = HiddenStart(path);
        foreach (var argument in new[] { "--process_id", target.ToString(CultureInfo.InvariantCulture), "--session_name", _session,
            "--output_stdout", "--qpc_time_ms", "--no_console_stats", "--no_track_gpu", "--no_track_input", "--terminate_on_proc_exit" })
            start.ArgumentList.Add(argument);
        try
        {
            using var game = Process.GetProcessById(target);
            TargetName = game.ProcessName;
            _process = Process.Start(start);
            if (_process == null) return;
            _status = $"PresentMon: {TargetName}";
            var process = _process;
            var window = _frames;
            _reader = Task.Run(async () =>
            {
                var csv = new PresentMonCsv();
                string? primaryChain = null;
                var lastPrimary = 0d;
                double? epoch = null;
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    if (line.TrimStart('\uFEFF').StartsWith("Application,", StringComparison.Ordinal)) { csv.SetHeader(line); continue; }
                    if (!csv.TryRead(line, out var frame))
                    {
                        if (line.StartsWith("error", StringComparison.OrdinalIgnoreCase) || line.StartsWith("warning", StringComparison.OrdinalIgnoreCase))
                            store.Log($"PresentMon: {line}");
                        continue;
                    }
                    if (frame.ProcessId != target) continue;
                    var now = _clock.Elapsed.TotalSeconds;
                    // Keep swap chains separate; an inactive primary can yield to a replacement chain.
                    if (primaryChain != null && primaryChain != frame.SwapChain && now - lastPrimary <= 1) continue;
                    if (primaryChain != frame.SwapChain) { primaryChain = frame.SwapChain; window = new(); _frames = window; epoch = null; }
                    lastPrimary = now;
                    if (frame.CaptureTimeMs.HasValue && !frame.IsQpcTimestamp) epoch ??= now - frame.CaptureTimeMs.Value / 1000;
                    var time = frame.CaptureTimeMs.HasValue ? (frame.IsQpcTimestamp ? frame.CaptureTimeMs.Value / 1000 - _qpcOrigin : epoch!.Value + frame.CaptureTimeMs.Value / 1000) : now;
                    window.Add(time, frame.Milliseconds, 33.3);
                }
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    _status = "PresentMon недоступен: проверьте права (подробности в журнале)";
            });
            _errors = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync() is { } line)
                    if (!string.IsNullOrWhiteSpace(line)) store.Log($"PresentMon: {line}");
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        { _status = "Не удалось начать захват кадров"; store.Log($"Capture: {ex.Message}"); }
    }

    private int ResolveTarget(string processName)
    {
        if (!string.IsNullOrWhiteSpace(processName))
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
            try { return processes.FirstOrDefault(p => p.Id != Environment.ProcessId)?.Id ?? 0; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        GetWindowThreadProcessId(GetForegroundWindow(), out var id);
        if (id == Environment.ProcessId) return _target;
        try
        {
            using var process = Process.GetProcessById((int)id);
            if (process.ProcessName is "explorer" or "dwm" or "ShellExperienceHost" or "StartMenuExperienceHost" or "SearchHost") return _target;
            return (int)id;
        }
        catch (ArgumentException) { return 0; }
    }

    private async Task StopAsync()
    {
        if (_process == null) return;
        try
        {
            if (!_process.HasExited)
            {
                var stop = HiddenStart(_process.StartInfo.FileName);
                stop.ArgumentList.Add("--session_name"); stop.ArgumentList.Add(_session);
                stop.ArgumentList.Add("--terminate_existing_session");
                using var terminator = Process.Start(stop);
                if (terminator != null)
                {
                    await Task.WhenAny(terminator.WaitForExitAsync(), Task.Delay(1500));
                    if (!terminator.HasExited) terminator.Kill();
                }
                await Task.WhenAny(_process.WaitForExitAsync(), Task.Delay(1500));
                if (!_process.HasExited) _process.Kill();
            }
            if (_reader != null) await _reader;
            if (_errors != null) await _errors;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { store.Log($"Capture stop: {ex.Message}"); }
        finally { _process.Dispose(); _process = null; _reader = _errors = null; }
    }

    private static ProcessStartInfo HiddenStart(string path) => new(path)
    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    public async ValueTask DisposeAsync() => await StopAsync();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}

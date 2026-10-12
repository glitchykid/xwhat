using System.Diagnostics;
using XWhat.Core;

namespace XWhat.App.Services;

internal sealed class MonitorEngine(SettingsStore store, MonitorSettings settings, bool demo) : IAsyncDisposable
{
    private MonitorSettings _settings = settings;
    private readonly CancellationTokenSource _stop = new();
    private readonly PresentMonCapture _capture = new(store);
    private readonly FrameWindow _demoFrames = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Task? _worker;
    private HardwareSample? _latest;
    private string _failure = "";
    public SampleHistory[] Histories { get; } = Enumerable.Range(0, 5).Select(_ => new SampleHistory()).ToArray();
    public HardwareSample? Latest => Volatile.Read(ref _latest);
    public DeviceChoice[] Gpus { get; private set; } = [];
    public DeviceChoice[] Disks { get; private set; } = [];
    public string CaptureStatus => _failure.Length > 0 ? _failure : demo ? "ДЕМО · данные искусственные" : _capture.Status;
    public string TargetName => demo ? "DEMO / NIGHT CITY" : _capture.TargetName;
    public void Configure(MonitorSettings value) => Volatile.Write(ref _settings, value.Normalize());
    public FrameSnapshot Frames() => demo ? _demoFrames.Read(_clock.Elapsed.TotalSeconds, _settings.HistorySeconds, _settings.StutterThresholdMs) : _capture.Read(_settings.HistorySeconds, _settings.StutterThresholdMs);

    public void Start() => _worker = Task.Run(async () =>
    {
        using var hardware = demo ? null : new HardwareMonitor(store);
        var lastTargetCheck = -5d;
        var lastDemoTime = 0d;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var currentSettings = Volatile.Read(ref _settings);
                var now = _clock.Elapsed.TotalSeconds;
                HardwareSample sample;
                if (demo)
                {
                    for (; lastDemoTime < now; lastDemoTime += 1.0 / 144)
                        _demoFrames.Add(lastDemoTime, ((int)(lastDemoTime * 144) % 257 == 0) ? 48 : 6.7 + Math.Sin(lastDemoTime * 17) * 1.1, 33.3);
                    sample = new(now, new(56 + 15 * Math.Sin(now), 64), new(94 + 3 * Math.Sin(now * 2), 72), new(62, null, 19.8, 32),
                        new(78, null, 9.4, 12), new(24 + 20 * Math.Sin(now * 1.5), 43), 112, 8.2, 1.7, "AMD Ryzen 7 7800X3D", "NVIDIA GeForce RTX 4070", "NVMe SSD");
                    Gpus = [new("demo", sample.GpuName)]; Disks = [new("demo", sample.DiskName)];
                }
                else
                {
                    if (now - lastTargetCheck >= 1) { await _capture.EnsureTargetAsync(currentSettings.ProcessName); lastTargetCheck = now; }
                    sample = hardware!.Read(now, currentSettings);
                    Gpus = hardware.Gpus; Disks = hardware.Disks;
                }
                var metrics = new[] { sample.Cpu, sample.Gpu, sample.Ram, sample.Vram, sample.Disk };
                for (var i = 0; i < metrics.Length; i++) Histories[i].Add(now, metrics[i].Load);
                Volatile.Write(ref _latest, sample);
                await Task.Delay(currentSettings.SampleIntervalMs, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { _failure = "Мониторинг остановлен; подробности в журнале"; store.Log($"Monitoring failed: {ex}"); }
    });

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (_worker != null) await _worker;
        await _capture.DisposeAsync();
        _stop.Dispose();
    }
}

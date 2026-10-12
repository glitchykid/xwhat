using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XWhat.App.Controls;
using XWhat.App.Services;
using XWhat.Core;
using Forms = System.Windows.Forms;

namespace XWhat.App;

public partial class App : Application
{
    private readonly SettingsStore _store = new();
    private MonitorSettings _settings = new();
    private MonitorEngine? _engine;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private Forms.NotifyIcon? _tray;
    private readonly RtssOutput _rtss = new();
    private DispatcherTimer? _timer;
    private Mutex? _instance;
    private bool _demo, _quitting;
    private double _lastRtssTime = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(true, @"Local\XWhat.SingleInstance", out var first);
        if (!first) { MessageBox.Show("X What уже запущен. Откройте настройки через значок в трее.", "X What"); Shutdown(); return; }
        _demo = e.Args.Contains("--demo");
        _settings = _store.Load();
        if (_demo) _settings = _settings with { Mode = OverlayMode.Desktop };
        _engine = new(_store, _settings, _demo);
        _overlay = new();
        _overlay.ToggleRequested += ToggleOverlay;
        _overlay.Apply(_settings);
        _overlay.Show();
        if (!_settings.ShowOverlay || _settings.Mode == OverlayMode.Rtss) _overlay.Hide();
        CreateTray();
        _engine.Start();
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        var previewArg = e.Args.FirstOrDefault(a => a.StartsWith("--render-preview=", StringComparison.Ordinal));
        if (previewArg != null) _ = RenderPreviewAsync(previewArg["--render-preview=".Length..]);
        else if (e.Args.Contains("--smoke-test")) _ = SmokeAsync();
        else if (_settings.Mode == OverlayMode.Rtss || _store.LoadWarning.Length > 0) ShowSettings();
        DispatcherUnhandledException += (_, args) => { _store.Log(args.Exception.ToString()); args.Handled = true; _ = QuitAsync(); };
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Настройки…", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add("Показать / скрыть · Ctrl+Shift+F10", null, (_, _) => Dispatcher.Invoke(ToggleOverlay));
        menu.Items.Add("Папка настроек и журнала", null, (_, _) =>
        {
            Directory.CreateDirectory(_store.DirectoryPath);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { _store.DirectoryPath }, UseShellExecute = true });
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, async (_, _) => await Dispatcher.InvokeAsync(QuitAsync).Task.Unwrap());
        _tray = new Forms.NotifyIcon { Icon = (System.Drawing.Icon)System.Drawing.SystemIcons.Information.Clone(),
            Text = "X What · настройки", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
    }

    private void ShowSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.Show(); _settingsWindow.Activate(); return; }
        var missing = _engine?.Latest;
        var status = $"{_engine?.CaptureStatus}\n{_rtss.Status}\nCPU: {missing?.CpuName ?? "обнаружение"}\nGPU: {missing?.GpuName ?? "обнаружение"}\n{_store.LoadWarning}";
        _settingsWindow = new(_settings, _engine?.Gpus ?? [], _engine?.Disks ?? [], status, ApplySettings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void ApplySettings(MonitorSettings settings)
    {
        _store.Save(settings);
        var oldMode = _settings.Mode;
        _settings = settings;
        _engine?.Configure(settings);
        _overlay?.Apply(settings);
        if (oldMode == OverlayMode.Rtss && settings.Mode != oldMode) _rtss.Clear();
        if (!_settings.ShowOverlay) _rtss.Clear();
        Refresh();
    }
    private void ToggleOverlay() => ApplySettings(_settings with { ShowOverlay = !_settings.ShowOverlay });

    private void Refresh()
    {
        if (_engine == null || _overlay == null) return;
        var hardware = _engine.Latest;
        var frames = _engine.Frames();
        var histories = _engine.Histories.Select(h => h.Read(hardware?.Time ?? 0, _settings.HistorySeconds)).ToArray();
        _overlay.Hud.Update(hardware, frames, histories, _settings, _engine.TargetName, _demo);
        if (_settings.ShowOverlay && _settings.Mode == OverlayMode.Desktop) { if (!_overlay.IsVisible) _overlay.Show(); }
        else if (_overlay.IsVisible) _overlay.Hide();
        if (_settings.ShowOverlay && _settings.Mode == OverlayMode.Rtss && hardware != null && hardware.Time != _lastRtssTime)
        {
            UpdateRtss(hardware, frames, histories);
            _lastRtssTime = hardware.Time;
        }
        if (_settingsWindow != null)
        {
            _settingsWindow.UpdateDevices(_engine.Gpus, _engine.Disks);
            _settingsWindow.StatusText.Text = $"{_engine.CaptureStatus}\n{(_settings.Mode == OverlayMode.Rtss ? _rtss.Status : "Оверлей Windows")}\nCPU: {hardware?.CpuName ?? "обнаружение"}\nGPU: {hardware?.GpuName ?? "обнаружение"}\n{hardware?.Status}";
        }
        if (_tray != null) _tray.Text = ("X What · " + _engine.TargetName)[..Math.Min(63, ("X What · " + _engine.TargetName).Length)];
    }

    private void UpdateRtss(HardwareSample hardware, FrameSnapshot frames, SamplePoint[][] histories)
    {
        static string N(double? v, string f = "0") => HudCanvas.Number(v, f).Replace("—", "--");
        var metrics = new[] { hardware.Cpu, hardware.Gpu, hardware.Ram, hardware.Vram, hardware.Disk };
        var colors = new[] { "6FE7CF", "86B8FF", "C7A0FF", "FFD27C", "FF728B" };
        var names = new[] { "CPU", "GPU", "RAM", "VRAM", "DISK" };
        var graphs = new List<RtssGraph>();
        var lines = new List<string>
        {
            "<C=6FE7CF>X / WHAT<C>",
            $"<C=FFFFFF>{N(frames.Fps)} FPS<C>   1% {N(frames.OnePercentLow)}   {N(frames.LatestMs, "0.0")} ms"
        };
        if (_settings.ShowGraphs)
        {
            lines.Add($"FRAME {_settings.HistorySeconds}s  MAX {N(frames.MaximumMs, "0.0")} ms  SPIKES {frames.StutterCount}");
            lines.Add("<C=6FE7CF>{graph0}<C>");
            graphs.Add(new(Buckets(frames.Samples, frames.Samples.LastOrDefault().Time, _settings.HistorySeconds, 296), 0,
                (float)Math.Max(50, (frames.MaximumMs ?? 0) * 1.15), 296, 45));
        }
        for (var i = 0; i < 5; i++)
        {
            var metric = metrics[i];
            var detail = i is 2 or 3 ? $"{N(metric.UsedGiB, "0.0")}/{N(metric.TotalGiB, "0.#")} GiB" + (metric.Temperature.HasValue ? $" {N(metric.Temperature)} C" : "") : $"{N(metric.Load)}%  {N(metric.Temperature)} C";
            var graphTag = _settings.ShowGraphs ? " {graph" + graphs.Count.ToString(CultureInfo.InvariantCulture) + "}" : "";
            lines.Add($"<C={colors[i]}>{names[i],-4}<C>  {detail} <C={colors[i]}>{graphTag}<C>");
            if (_settings.ShowGraphs) graphs.Add(new(Buckets(histories[i], hardware.Time, _settings.HistorySeconds, 93), 0, 100, 93, 14));
        }
        lines.Add($"R {N(hardware.DiskReadMiBs, "0.#")} W {N(hardware.DiskWriteMiBs, "0.#")} MiB/s  I/O {N(hardware.DiskLatencyMs, "0.0")} ms");
        _rtss.Update(string.Join('\n', lines), graphs.ToArray());
    }

    private static float[] Buckets(SamplePoint[] samples, double now, double seconds, int width)
        => GraphEnvelope.Create(samples, now, seconds, width).Maximum.Select(value => double.IsFinite(value) ? (float)value : 0).ToArray();

    private async Task RenderPreviewAsync(string path)
    {
        await Task.Delay(11000);
        Refresh();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        SavePng(_overlay!.Hud, path);
        var settings = new SettingsWindow(_settings, _engine!.Gpus, _engine.Disks, "DEMO · 144 FPS\nRTSS: ожидание\nCPU: AMD Ryzen 7 7800X3D\nGPU: NVIDIA GeForce RTX 4070", _ => { });
        settings.Show();
        settings.UpdateLayout();
        SavePng((FrameworkElement)settings.Content, Path.ChangeExtension(path, ".settings.png"));
        settings.Close();
        await QuitAsync();
    }
    private static void SavePng(FrameworkElement element, string path)
    {
        element.Measure(new Size(element.ActualWidth > 0 ? element.ActualWidth : 320, element.ActualHeight > 0 ? element.ActualHeight : 322));
        element.Arrange(new Rect(new Size(element.DesiredSize.Width, element.DesiredSize.Height)));
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private async Task SmokeAsync()
    {
        await Task.Delay(5000);
        var sample = _engine?.Latest;
        var directory = Path.Combine(AppContext.BaseDirectory, "smoke-results");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "telemetry.json"), System.Text.Json.JsonSerializer.Serialize(new
        { Sample = sample, CaptureStatus = _engine?.CaptureStatus, Frames = _engine?.Frames(), RtssStatus = _rtss.Status, Hotkey = _overlay?.HotkeyAvailable }));
        await QuitAsync();
    }
    private async Task QuitAsync()
    {
        if (_quitting) return;
        _quitting = true; _timer?.Stop();
        _tray?.Dispose(); _tray = null;
        _rtss.Dispose();
        if (_engine != null) await _engine.DisposeAsync();
        _settingsWindow?.Close(); _overlay?.Close(); _instance?.Dispose();
        Shutdown();
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
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
    private TrayMenu? _trayMenu;
    private System.Drawing.Icon? _trayIcon;
    private readonly RtssOutput _rtss = new();
    private readonly ForegroundDisplay _display = new();
    private readonly AutostartService _startup = new(Path.Combine(AppContext.BaseDirectory, "XWhat.exe"));
    private RtssHost? _rtssHost;
    private readonly bool _frameAccess = InstalledLaunch.FrameAccess;
    private bool _prerequisiteNotified;
    private DispatcherTimer? _timer;
    private Mutex? _instance;
    private bool _demo, _quitting;
    private double _lastRtssTime = -1;
    private OverlayMode _activeMode = OverlayMode.Desktop;
    private ScreenBounds? _lastArea;
    private bool _autostartLaunch;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--install-tasks") || e.Args.Contains("--uninstall-tasks"))
        {
            try
            {
                if (e.Args.Contains("--install-tasks")) Shutdown(InstalledLaunch.Configure());
                else { InstalledLaunch.Remove(); Shutdown(); }
            }
            catch (Exception ex) { _store.Log($"Installation: {ex}"); Shutdown(1); }
            return;
        }
        if (!e.Args.Contains("--task-launch") && !e.Args.Contains("--demo") && !e.Args.Contains("--smoke-test") && InstalledLaunch.TryLaunch(_store))
        { Shutdown(); return; }
        _autostartLaunch = e.Args.Contains("--autostart") || e.Args.Contains("--task-launch");
        _instance = new Mutex(true, @"Local\XWhat.SingleInstance", out var first);
        if (!first) { if (!_autostartLaunch) MessageBox.Show("X What уже запущен. Откройте настройки через значок в трее.", "X What"); Shutdown(); return; }
        _demo = e.Args.Contains("--demo");
        _settings = _store.Load() with { StartWithWindows = _startup.IsEnabled };
        _rtssHost = new(_store);
        if (!_demo && _settings.Mode != OverlayMode.Desktop) _rtssHost.Start();
        if (_demo) _settings = _settings with { Mode = OverlayMode.Desktop };
        _engine = new(_store, _settings, _demo);
        _overlay = new();
        _overlay.ToggleRequested += ToggleOverlay;
        _overlay.Apply(_settings);
        new WindowInteropHelper(_overlay).EnsureHandle();
        CreateTray();
        _engine.Start();
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        var previewArg = e.Args.FirstOrDefault(a => a.StartsWith("--render-preview=", StringComparison.Ordinal));
        if (previewArg != null) _ = RenderPreviewAsync(previewArg["--render-preview=".Length..]);
        else if (e.Args.Contains("--smoke-test")) _ = SmokeAsync();
        else if (!_autostartLaunch && _store.LoadWarning.Length > 0) ShowSettings();
        DispatcherUnhandledException += (_, args) => { _store.Log(args.Exception.ToString()); args.Handled = true; _ = QuitAsync(); };
    }

    private void CreateTray()
    {
        _trayMenu = new TrayMenu(() => Dispatcher.Invoke(ShowSettings), () => Dispatcher.Invoke(ToggleOverlay), () =>
        {
            Directory.CreateDirectory(_store.DirectoryPath);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { _store.DirectoryPath }, UseShellExecute = true });
        }, async () => await Dispatcher.InvokeAsync(QuitAsync).Task.Unwrap());
        _trayMenu.Opening += (_, _) => _trayMenu.SetOverlayVisible(_settings.ShowOverlay);
        _trayIcon = TrayIcon.Create();
        _tray = new Forms.NotifyIcon { Icon = _trayIcon,
            Text = "X What · настройки", ContextMenuStrip = _trayMenu, Visible = true };
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
        UpdateReadiness(_display.Read());
    }

    private void ApplySettings(MonitorSettings settings) => SaveAndApply(settings, configureStartup: true);
    private void SaveAndApply(MonitorSettings settings, bool configureStartup)
    {
        _store.Save(settings);
        try { if (configureStartup) _startup.SetEnabled(settings.StartWithWindows); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            try { _store.Save(_settings); }
            catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException) { _store.Log($"Settings rollback: {rollbackError.Message}"); }
            throw;
        }
        _settings = settings;
        _lastArea = null;
        // Settings must reach RTSS even when no new hardware sample has arrived yet.
        _lastRtssTime = -1;
        _engine?.Configure(settings);
        if (_settings.Mode != OverlayMode.Desktop) _rtssHost?.Start();
        _overlay?.Apply(settings);
        if (!_settings.ShowOverlay) _rtss.Clear();
        Refresh();
    }
    private void ToggleOverlay() => SaveAndApply(_settings with { ShowOverlay = !_settings.ShowOverlay }, configureStartup: false);

    private void Refresh()
    {
        if (_engine == null || _overlay == null) return;
        var hardware = _engine.Latest;
        var frames = _engine.Frames();
        var histories = _engine.Histories.Select(h => h.Read(hardware?.Time ?? 0, _settings.HistorySeconds)).ToArray();
        var display = _display.Read();
        var mode = OverlayRouting.Resolve(_settings.Mode, display.IsFullscreen, _settings.Mode == OverlayMode.Auto && display.IsFullscreen && _rtss.IsAvailable);
        if (_activeMode != mode)
        {
            if (_activeMode == OverlayMode.Rtss) _rtss.Clear();
            _activeMode = mode; _lastRtssTime = -1; _lastArea = null;
        }
        var rows = HudRows.Build(hardware, frames, histories);
        _overlay.Hud.Update(rows, _settings, hardware?.Time ?? 0, _demo);
        if (_settings.ShowOverlay && mode == OverlayMode.Desktop)
        {
            if (!_overlay.IsVisible) _overlay.Show();
            if (_settings.ClickThrough || _lastArea != display.Area) _overlay.Position(display.Area);
            _lastArea = display.Area;
        }
        else if (_overlay.IsVisible) _overlay.Hide();
        if (_settings.ShowOverlay && mode == OverlayMode.Rtss && hardware != null && hardware.Time != _lastRtssTime)
        {
            UpdateRtss(rows, hardware.Time);
            _lastRtssTime = hardware.Time;
        }
        if (_settingsWindow != null)
        {
            _settingsWindow.UpdateDevices(_engine.Gpus, _engine.Disks);
            var route = mode == OverlayMode.Rtss ? _rtss.Status : display.IsFullscreen && _settings.Mode == OverlayMode.Auto ? "Полный экран · окно Windows; для эксклюзивного режима запустите RTSS" : "Оверлей Windows";
            _settingsWindow.StatusText.Text = $"{_engine.CaptureStatus}\n{route}\nЭкран: {(display.ExclusiveD3d ? "D3D exclusive" : display.IsFullscreen ? "полноэкранное окно" : "оконный")}\nCPU: {hardware?.CpuName ?? "обнаружение"}\nGPU: {hardware?.GpuName ?? "обнаружение"}\n{hardware?.Status}\n{_store.LoadWarning}";
        }
        if (_tray != null) _tray.Text = ("X What · " + _engine.TargetName)[..Math.Min(63, ("X What · " + _engine.TargetName).Length)];
        UpdateReadiness(display);
    }

    private void UpdateReadiness(DisplayState display)
    {
        if (_demo) return;
        var ready = MonitoringReadiness.Check(_settings.Mode, display.IsFullscreen, _rtssHost?.IsInstalled == true, _rtss.IsAvailable, _frameAccess);
        var message = ready.MissingRtss ? "Нужен RTSS · запустите установщик X What" :
            ready.WaitingRtss ? "RTSS не подключён · проверьте диагностику" : ready.MissingFrameAccess ? "Нет доступа к FPS · запустите установщик X What" : "";
        if (_settingsWindow != null)
        {
            _settingsWindow.SetReadiness(ready, message);
            if (_rtssHost?.StartError.Length > 0) _settingsWindow.StatusText.Text += "\n" + _rtssHost.StartError;
        }
        if (!_prerequisiteNotified && message.Length > 0 && _tray != null)
        {
            _prerequisiteNotified = true;
            _tray.ShowBalloonTip(7000, "X What · требуется настройка", message + ". Откройте настройки из трея.", Forms.ToolTipIcon.Warning);
        }
    }

    private void UpdateRtss(HudRow[] rows, double time)
    {
        _rtss.Update(HudRows.RtssText(rows, _settings.ShowGraphs, _demo, _settings.Scale), HudRows.RtssGraphs(rows, _settings, time));
    }

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
        _trayMenu!.CreateControl();
        _trayMenu.PerformLayout();
        using (var menuImage = new System.Drawing.Bitmap(_trayMenu.Width, _trayMenu.Height))
        {
            _trayMenu.DrawToBitmap(menuImage, new System.Drawing.Rectangle(System.Drawing.Point.Empty, menuImage.Size));
            menuImage.Save(Path.ChangeExtension(path, ".tray.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        await QuitAsync();
    }
    private static void SavePng(FrameworkElement element, string path)
    {
        var size = new Size(element.ActualWidth > 0 ? element.ActualWidth : HudRows.Width, element.ActualHeight > 0 ? element.ActualHeight : HudRows.Height);
        element.Measure(size);
        element.Arrange(new Rect(size));
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
        { Sample = sample, CaptureStatus = _engine?.CaptureStatus, Frames = _engine?.Frames(), RtssStatus = _rtss.Status, Hotkey = _overlay?.HotkeyAvailable,
            Display = _display.Read(), ActiveOutput = _activeMode.ToString(), AutostartLaunch = _autostartLaunch, SettingsVisible = _settingsWindow?.IsVisible == true,
            FrameAccess = _frameAccess, RtssInstalled = _rtssHost?.IsInstalled == true }));
        await QuitAsync();
    }
    private async Task QuitAsync()
    {
        if (_quitting) return;
        _quitting = true; _timer?.Stop();
        _tray?.Dispose(); _tray = null;
        _trayMenu?.Dispose(); _trayMenu = null;
        _trayIcon?.Dispose(); _trayIcon = null;
        _rtss.Dispose();
        if (_engine != null) await _engine.DisposeAsync();
        _settingsWindow?.Close(); _overlay?.Close(); _instance?.Dispose();
        Shutdown();
    }
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using XWhat.App.Controls;
using XWhat.Core;

namespace XWhat.App;

internal sealed class OverlayWindow : Window
{
    public HudCanvas Hud { get; } = new();
    private MonitorSettings _settings = new();
    public event Action? ToggleRequested;
    public OverlayWindow()
    {
        Title = "X What — Overlay"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        ShowActivated = false; SizeToContent = SizeToContent.WidthAndHeight; Content = Hud;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(Hook);
            if (!RegisterHotKey(handle, 1, 0x4000 | 0x2 | 0x4, 0x79))
                HotkeyAvailable = false;
            Apply(_settings);
        };
        Closed += (_, _) => UnregisterHotKey(new WindowInteropHelper(this).Handle, 1);
        MouseLeftButtonDown += (_, e) => { if (!_settings.ClickThrough && e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }
    public bool HotkeyAvailable { get; private set; } = true;
    public void Apply(MonitorSettings settings)
    {
        _settings = settings;
        Hud.LayoutTransform = new ScaleTransform(settings.Scale, settings.Scale);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != 0)
        {
            var style = GetWindowLongPtr(handle, -20).ToInt64() | 0x80 | 0x08000000;
            style = settings.ClickThrough ? style | 0x20 : style & ~0x20L;
            SetWindowLongPtr(handle, -20, (nint)style);
        }
        Position();
    }
    public void Position()
    {
        var area = SystemParameters.WorkArea;
        var width = 320 * _settings.Scale;
        var height = (_settings.ShowGraphs ? 322 : 252) * _settings.Scale;
        Left = _settings.Corner is OverlayCorner.TopRight or OverlayCorner.BottomRight ? area.Right - width - 14 : area.Left + 14;
        Top = _settings.Corner is OverlayCorner.BottomLeft or OverlayCorner.BottomRight ? area.Bottom - height - 14 : area.Top + 14;
    }
    private nint Hook(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam == 1) { ToggleRequested?.Invoke(); handled = true; }
        if (message == 0x0021) { handled = true; return 3; } // MA_NOACTIVATE: never steal game focus.
        return 0;
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(nint window, int id);
}

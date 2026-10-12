using System.Runtime.InteropServices;
using XWhat.Core;
using Forms = System.Windows.Forms;

namespace XWhat.App.Services;

internal sealed record DisplayState(ScreenBounds Area, bool IsFullscreen, bool ExclusiveD3d);

internal sealed class ForegroundDisplay
{
    private DisplayState _last;
    public ForegroundDisplay()
    {
        var screen = Forms.Screen.PrimaryScreen!.WorkingArea;
        _last = new(new(screen.Left, screen.Top, screen.Right, screen.Bottom), false, false);
    }
    public DisplayState Read()
    {
        var window = GetForegroundWindow();
        if (window == 0) return _last;
        GetWindowThreadProcessId(window, out var processId);
        // Opening settings must not move the HUD away from the game's monitor or change its renderer.
        if (processId == Environment.ProcessId) return _last;
        var monitor = MonitorFromWindow(window, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info)) return _last;
        var d3d = SHQueryUserNotificationState(out var notificationState) == 0 && notificationState == 3;
        var validRect = DwmGetWindowAttribute(window, 9, out var rectangle, Marshal.SizeOf<NativeRect>()) == 0 || GetWindowRect(window, out rectangle);
        var caption = (GetWindowLongPtr(window, -16).ToInt64() & 0x00C00000) != 0;
        var fullscreen = validRect && !IsIconic(window) && FullscreenDetection.IsFullscreen(rectangle.Bounds, info.Monitor.Bounds, caption, d3d);
        _last = new(fullscreen ? info.Monitor.Bounds : info.Work.Bounds, fullscreen, d3d);
        return _last;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly ScreenBounds Bounds => new(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out NativeRect rectangle, int size);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
}

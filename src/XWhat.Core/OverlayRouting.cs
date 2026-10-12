namespace XWhat.Core;

public readonly record struct ScreenBounds(int Left, int Top, int Right, int Bottom);
public readonly record struct ScreenPosition(int X, int Y);

public static class FullscreenDetection
{
    /// <summary>Physical screen coordinates. Borderless coverage is a fullscreen hint; the shell D3D signal denotes exclusive fullscreen.</summary>
    public static bool IsFullscreen(ScreenBounds window, ScreenBounds monitor, bool hasCaption, bool exclusiveD3d)
        => exclusiveD3d || (!hasCaption && monitor.Right > monitor.Left && monitor.Bottom > monitor.Top &&
            Math.Abs((long)window.Left - monitor.Left) <= 2 && Math.Abs((long)window.Top - monitor.Top) <= 2 &&
            Math.Abs((long)window.Right - monitor.Right) <= 2 && Math.Abs((long)window.Bottom - monitor.Bottom) <= 2);
}

public static class OverlayRouting
{
    public static OverlayMode Resolve(OverlayMode requested, bool fullscreen, bool rtssAvailable)
        => requested == OverlayMode.Auto ? fullscreen && rtssAvailable ? OverlayMode.Rtss : OverlayMode.Desktop : requested;
}

public static class OverlayPlacement
{
    /// <summary>Returns physical pixels on the target monitor; DPI scales the inset and the logical HUD dimensions.</summary>
    public static ScreenPosition Calculate(ScreenBounds area, OverlayCorner corner, double width, double height, double scale, uint dpi)
    {
        var factor = scale * (dpi == 0 ? 96 : dpi) / 96;
        var inset = (int)Math.Round(4 * (dpi == 0 ? 96 : dpi) / 96d);
        var x = corner is OverlayCorner.TopRight or OverlayCorner.BottomRight ? area.Right - (int)Math.Ceiling(width * factor) - inset : area.Left + inset;
        var y = corner is OverlayCorner.BottomLeft or OverlayCorner.BottomRight ? area.Bottom - (int)Math.Ceiling(height * factor) - inset : area.Top + inset;
        return new(x, y);
    }
}

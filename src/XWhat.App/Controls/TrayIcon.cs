using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace XWhat.App.Controls;

internal static partial class TrayIcon
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Point[] points = [new(3, 22), new(10, 22), new(14, 8), new(18, 26), new(22, 15), new(29, 15)];
            using var outline = new Pen(Color.FromArgb(17, 22, 27), 5) { LineJoin = LineJoin.Round };
            using var line = new Pen(Color.FromArgb(220, 229, 235), 2) { LineJoin = LineJoin.Round };
            graphics.DrawLines(outline, points);
            graphics.DrawLines(line, points);
        }
        var handle = bitmap.GetHicon();
        // FromHandle does not own the native handle; clone before releasing it.
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint handle);
}

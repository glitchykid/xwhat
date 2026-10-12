using System.Globalization;
using System.Windows;
using System.Windows.Media;
using XWhat.Core;

namespace XWhat.App.Controls;

/// <summary>One drawing surface: peak preserving per-pixel min/max graphs, no per-frame UI element trees.</summary>
internal sealed class HudCanvas : FrameworkElement
{
    private static readonly Brush White = Frozen("#F5F9FF"), Muted = Frozen("#ADBACE"), Mint = Frozen("#6FE7CF"),
        Amber = Frozen("#FFD27C"), Pink = Frozen("#FF728B"), Blue = Frozen("#86B8FF"), Purple = Frozen("#C7A0FF");
    private static readonly Brush[] Colors = [Mint, Blue, Purple, Amber, Pink];
    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Typeface Mono = new("Consolas");
    private readonly Pen _outline = new(Frozen("#E6000000"), 2.2) { LineJoin = PenLineJoin.Round };
    private HardwareSample? _hardware;
    private FrameSnapshot _frames = FrameSnapshot.Empty;
    private SamplePoint[][] _histories = [[], [], [], [], []];
    private MonitorSettings _settings = new();
    private string _target = "Ожидание игры";
    private bool _demo;

    public HudCanvas() { Width = 320; Height = 322; _outline.Freeze(); }
    public void Update(HardwareSample? hardware, FrameSnapshot frames, SamplePoint[][] histories, MonitorSettings settings, string target, bool demo)
    {
        (_hardware, _frames, _histories, _settings, _target, _demo) = (hardware, frames, histories, settings, target, demo);
        Height = settings.ShowGraphs ? 322 : 252;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var fill = new SolidColorBrush(Color.FromArgb((byte)(_settings.BackgroundOpacity * 255), 8, 13, 21));
        dc.DrawRoundedRectangle(fill, new Pen(Frozen("#405D738C"), 1), new Rect(0.5, 0.5, 319, Height - 1), 12, 12);
        dc.DrawRoundedRectangle(Mint, null, new Rect(12, 13, 3, 14), 1.5, 1.5);
        Text(dc, "X / WHAT", 22, 10, 13, White, true);
        Text(dc, _demo ? "DEMO" : "LIVE", 269, 12, 10, _demo ? Amber : Mint, true);
        Text(dc, Truncate(_target, 39), 12, 33, 10, Muted);
        Text(dc, Number(_frames.Fps, "0"), 11, 48, 35, White, true);
        Text(dc, "FPS", 88, 66, 10, Mint, true);
        Text(dc, "1% LOW", 130, 51, 9, Muted, true);
        Text(dc, Number(_frames.OnePercentLow, "0"), 130, 67, 17, White, true);
        Text(dc, "FRAME / ms", 228, 51, 9, Muted, true);
        Text(dc, Number(_frames.LatestMs, "0.0"), 228, 67, 17, White, true);
        var rowTop = 117d;
        if (_settings.ShowGraphs)
        {
            Text(dc, "FRAME TIME", 12, 99, 9, Muted, true);
            Text(dc, $"{_settings.HistorySeconds}s   MAX {Number(_frames.MaximumMs, "0.0")} ms", 154, 99, 9, Muted, true);
            var frameEnd = _frames.Samples.LastOrDefault().Time;
            var maximum = Math.Max(50, (_frames.MaximumMs ?? 0) * 1.15);
            Graph(dc, _frames.Samples, new Rect(12, 116, 296, 49), frameEnd, _settings.HistorySeconds, maximum, Mint, _settings.StutterThresholdMs);
            Text(dc, $"ПИКИ  {_frames.StutterCount}", 12, 170, 9, _frames.StutterCount > 0 ? Pink : Muted, true);
            Text(dc, $"> {_settings.StutterThresholdMs:0} ms и 2× медиана", 127, 170, 9, Muted);
            rowTop = 187;
        }
        var metrics = _hardware == null ? new Metric[] { new(), new(), new(), new(), new() } :
            [_hardware.Cpu, _hardware.Gpu, _hardware.Ram, _hardware.Vram, _hardware.Disk];
        var labels = new[] { "CPU", "GPU", "RAM", "VRAM", "DISK" };
        for (var i = 0; i < 5; i++)
        {
            var y = rowTop + i * 22;
            Text(dc, labels[i], 12, y, 11, Colors[i], true);
            if (i is 2 or 3)
            {
                Text(dc, $"{Number(metrics[i].UsedGiB, "0.0")} / {Number(metrics[i].TotalGiB, "0.#")} GiB", 59, y, 11, White, true);
                if (metrics[i].Temperature.HasValue) Text(dc, Number(metrics[i].Temperature, "0") + "°", 177, y, 10, Muted, true);
            }
            else
            {
                Text(dc, Number(metrics[i].Load, "0") + "%", 59, y, 12, White, true);
                Text(dc, Number(metrics[i].Temperature, "0") + "°C", 121, y, 11, metrics[i].Temperature >= 85 ? Pink : Muted, true);
            }
            if (_settings.ShowGraphs)
                Graph(dc, _histories[i], new Rect(214, y + 2, 93, 14), _hardware?.Time ?? 0, _settings.HistorySeconds, 100, Colors[i]);
            else Text(dc, Number(metrics[i].Load, "0") + "%", 265, y, 10, Colors[i], true);
        }
        var bottom = Height - 18;
        Text(dc, $"R {Number(_hardware?.DiskReadMiBs, "0.#")}  W {Number(_hardware?.DiskWriteMiBs, "0.#")} MiB/s", 12, bottom, 9, Muted, true);
        Text(dc, $"I/O {Number(_hardware?.DiskLatencyMs, "0.0")} ms", 221, bottom, 9, Muted, true);
    }

    private void Text(DrawingContext dc, string value, double x, double y, double size, Brush brush, bool mono = false)
    {
        var formatted = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            mono ? Mono : Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var geometry = formatted.BuildGeometry(new Point(x, y));
        dc.DrawGeometry(brush, _outline, geometry);
        dc.DrawGeometry(brush, null, geometry);
    }

    private static void Graph(DrawingContext dc, SamplePoint[] samples, Rect area, double now, double seconds,
        double maximum, Brush color, double? threshold = null)
    {
        dc.PushClip(new RectangleGeometry(area));
        var grid = new Pen(Frozen("#405D738C"), 0.5);
        dc.DrawLine(grid, new Point(area.X, area.Bottom), new Point(area.Right, area.Bottom));
        dc.DrawLine(grid, new Point(area.X, area.Y + area.Height / 2), new Point(area.Right, area.Y + area.Height / 2));
        if (threshold.HasValue)
        {
            var y = area.Bottom - threshold.Value / maximum * area.Height;
            dc.DrawLine(new Pen(Frozen("#80775A40"), 0.7) { DashStyle = DashStyles.Dash }, new Point(area.X, y), new Point(area.Right, y));
        }
        var pixels = Math.Max(1, (int)area.Width);
        var envelope = GraphEnvelope.Create(samples, now, seconds, pixels);
        var minima = envelope.Minimum;
        var maxima = envelope.Maximum;
        var pen = new Pen(color, 1.2);
        var alert = new Pen(Pink, 1.5);
        Point? previous = null;
        for (var i = 0; i < pixels; i++)
        {
            if (!double.IsFinite(minima[i])) continue;
            var low = new Point(area.X + i, area.Bottom - minima[i] / maximum * area.Height);
            var high = new Point(area.X + i, area.Bottom - maxima[i] / maximum * area.Height);
            var currentPen = threshold.HasValue && maxima[i] > threshold.Value ? alert : pen;
            if (previous.HasValue) dc.DrawLine(pen, previous.Value, low);
            dc.DrawLine(currentPen, low, high);
            previous = low;
        }
        dc.Pop();
    }

    public static string Number(double? number, string format) => number.HasValue && double.IsFinite(number.Value) ? number.Value.ToString(format, CultureInfo.InvariantCulture) : "—";
    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
    private static Brush Frozen(string color) { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; brush.Freeze(); return brush; }
}

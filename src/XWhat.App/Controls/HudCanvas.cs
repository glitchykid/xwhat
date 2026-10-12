using System.Globalization;
using System.Windows;
using System.Windows.Media;
using XWhat.Core;

namespace XWhat.App.Controls;

/// <summary>Technical data only. A single drawing surface keeps the tiny transparent HUD inexpensive.</summary>
internal sealed class HudCanvas : FrameworkElement
{
    private static readonly Brush White = Frozen("#F0F4F8"), Muted = Frozen("#B9C5D1"), Cyan = Frozen("#78B8CA"), Alert = Frozen("#F5A16C");
    private static readonly Typeface Mono = new("Consolas");
    private static readonly Pen TextOutline = FrozenPen(Frozen("#EE000000"), 1.8);
    private static readonly Pen GraphOutline = FrozenPen(Frozen("#CC000000"), 2.8);
    private static readonly Pen GraphPen = FrozenPen(Cyan, 1);
    private static readonly Pen AlertPen = FrozenPen(Alert, 1.2);
    private HudRow[] _rows = HudRows.Build(null, FrameSnapshot.Empty, [[], [], [], [], []]);
    private MonitorSettings _settings = new();
    private double _time;
    private bool _demo;

    public HudCanvas() { Width = HudRows.Width; Height = HudRows.Height; }
    public void Update(HudRow[] rows, MonitorSettings settings, double time, bool demo)
    {
        (_rows, _settings, _time, _demo) = (rows, settings, time, demo);
        Width = settings.ShowGraphs ? HudRows.Width : HudRows.GraphLeft;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_settings.BackgroundOpacity > 0)
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(_settings.BackgroundOpacity * 255), 0, 0, 0)), null, new Rect(0, 0, Width, Height));
        for (var i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i];
            var y = i * HudRows.RowHeight + 2;
            Text(dc, row.Label, 3, y, 11, Muted);
            Text(dc, row.Values, 35, y, i == 7 ? 9.5 : 11, row.Alert ? Alert : White);
            if (_settings.ShowGraphs && i < 7)
            {
                var now = i < 2 ? row.Graph.LastOrDefault().Time : _time;
                Graph(dc, row.Graph, new Rect(HudRows.GraphLeft, y, HudRows.GraphWidth, 14), now,
                    _settings.HistorySeconds, row.Maximum, row.FrameTime ? _settings.StutterThresholdMs : null);
            }
        }
        if (_demo) Text(dc, "DEMO", HudRows.GraphLeft, 7 * HudRows.RowHeight + 2, 9, Alert);
    }
    private void Text(DrawingContext dc, string value, double x, double y, double size, Brush brush)
    {
        var formatted = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Mono, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var geometry = formatted.BuildGeometry(new Point(x, y));
        dc.DrawGeometry(brush, TextOutline, geometry);
        dc.DrawGeometry(brush, null, geometry);
    }
    private static void Graph(DrawingContext dc, SamplePoint[] samples, Rect area, double now, double seconds, double maximum, double? threshold)
    {
        dc.PushClip(new RectangleGeometry(new Rect(area.X - 2, area.Y - 2, area.Width + 4, area.Height + 4)));
        var envelope = GraphEnvelope.Create(samples, now, seconds, (int)area.Width);
        Point? previous = null;
        for (var i = 0; i < envelope.Minimum.Length; i++)
        {
            var minimum = envelope.Minimum[i];
            var max = envelope.Maximum[i];
            if (!double.IsFinite(minimum)) continue;
            var low = new Point(area.X + i, area.Bottom - Math.Clamp(minimum / maximum, 0, 1) * area.Height);
            var high = new Point(area.X + i, area.Bottom - Math.Clamp(max / maximum, 0, 1) * area.Height);
            var pen = threshold.HasValue && max > threshold.Value ? AlertPen : GraphPen;
            if (previous.HasValue)
            {
                dc.DrawLine(GraphOutline, previous.Value, low);
                dc.DrawLine(GraphPen, previous.Value, low);
            }
            dc.DrawLine(GraphOutline, low, high);
            dc.DrawLine(pen, low, high);
            previous = low;
        }
        dc.Pop();
    }
    private static Pen FrozenPen(Brush brush, double thickness) { var pen = new Pen(brush, thickness) { LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static Brush Frozen(string color) { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; brush.Freeze(); return brush; }
}

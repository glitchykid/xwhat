using System.Globalization;
using System.Text;

namespace XWhat.Core;

public sealed record HudRow(string Label, string Values, SamplePoint[] Graph, double Maximum, bool FrameTime = false, bool Alert = false);

/// <summary>The two renderers share numeric formatting, row order and chart data. No branding or game title in the HUD.</summary>
public static class HudRows
{
    public const int Width = 316;
    public const int Height = 144;
    public const int RowHeight = 18;
    public const int GraphLeft = 192;
    public const int GraphWidth = 120;

    public static HudRow[] Build(HardwareSample? hardware, FrameSnapshot frames, SamplePoint[][] histories)
    {
        var fps = frames.Samples.Select(s => new SamplePoint(s.Time, 1000 / s.Value)).ToArray();
        var rows = new List<HudRow>
        {
            new("FPS", $"{Number(frames.Fps)} 1% {Number(frames.OnePercentLow)}", fps, Math.Max(60, fps.Select(s => s.Value).DefaultIfEmpty(60).Max() * 1.1)),
            new("FT", $"{Number(frames.LatestMs, "0.0")}ms P{Number(frames.MaximumMs)} S{frames.StutterCount}", frames.Samples, Math.Max(50, (frames.MaximumMs ?? 0) * 1.15), true)
        };
        var metrics = hardware == null ? new Metric[] { new(), new(), new(), new(), new() } : [hardware.Cpu, hardware.Gpu, hardware.Ram, hardware.Vram, hardware.Disk];
        var labels = new[] { "CPU", "GPU", "RAM", "VRAM", "DISK" };
        for (var i = 0; i < 5; i++)
        {
            var metric = metrics[i];
            var values = i is 2 or 3 ? $"{Number(metric.UsedGiB, "0.0")}/{Number(metric.TotalGiB, "0.#")}G" +
                (metric.Temperature.HasValue ? $" {Number(metric.Temperature)}C" : "") : $"{Number(metric.Load)}% {Number(metric.Temperature)}C";
            rows.Add(new(labels[i], values, histories[i], 100, Alert: metric.Temperature >= 85));
        }
        rows.Add(new("IO", $"R{Number(hardware?.DiskReadMiBs, "0.#")} W{Number(hardware?.DiskWriteMiBs, "0.#")}M {Number(hardware?.DiskLatencyMs, "0.0")}ms", [], 100));
        return rows.ToArray();
    }

    public static string RtssText(HudRow[] rows, bool showGraphs, bool demo = false)
    {
        var text = new StringBuilder("<FNT=Consolas,-9,400,1>");
        var graph = 0;
        for (var i = 0; i < rows.Length; i++)
        {
            if (i > 0) text.Append('\n');
            var row = rows[i];
            text.Append(CultureInfo.InvariantCulture, $"<C=B9C5D1>{row.Label,-4}<C><C={(row.Alert ? "F5A16C" : "F0F4F8")}>{row.Values.Replace("—", "--"),-23}<C>");
            if (showGraphs && i < 7) text.Append(CultureInfo.InvariantCulture, $"<C=78B8CA>{{graph{graph++}}}<C>");
            if (demo && i == rows.Length - 1) text.Append(" DEMO");
        }
        return text.ToString();
    }
    public static string Number(double? value, string format = "0") => value.HasValue && double.IsFinite(value.Value) ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "—";
}

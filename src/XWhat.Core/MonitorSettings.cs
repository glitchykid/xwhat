namespace XWhat.Core;

public enum OverlayMode { Desktop, Rtss }
public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>Persisted user preferences. Normalize before applying data read from disk.</summary>
public sealed record MonitorSettings
{
    public OverlayMode Mode { get; init; } = OverlayMode.Rtss;
    public OverlayCorner Corner { get; init; }
    public double Scale { get; init; } = 1;
    public double BackgroundOpacity { get; init; } = 0.58;
    public int SampleIntervalMs { get; init; } = 500;
    public int HistorySeconds { get; init; } = 10;
    public double StutterThresholdMs { get; init; } = 33.3;
    public bool ShowOverlay { get; init; } = true;
    public bool ShowGraphs { get; init; } = true;
    public bool ClickThrough { get; init; } = true;
    public string ProcessName { get; init; } = "";
    public string GpuId { get; init; } = "";
    public string DiskId { get; init; } = "";

    public MonitorSettings Normalize() => this with
    {
        Mode = Enum.IsDefined(Mode) ? Mode : OverlayMode.Rtss,
        Corner = Enum.IsDefined(Corner) ? Corner : OverlayCorner.TopLeft,
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, 0.75, 2) : 1,
        BackgroundOpacity = double.IsFinite(BackgroundOpacity) ? Math.Clamp(BackgroundOpacity, 0, 0.9) : 0.58,
        SampleIntervalMs = Math.Clamp(SampleIntervalMs, 250, 2000),
        HistorySeconds = Math.Clamp(HistorySeconds, 5, 60),
        StutterThresholdMs = double.IsFinite(StutterThresholdMs) ? Math.Clamp(StutterThresholdMs, 16, 200) : 33.3,
        ProcessName = Path.GetFileName(ProcessName?.Trim() ?? ""),
        GpuId = GpuId ?? "",
        DiskId = DiskId ?? ""
    };
}

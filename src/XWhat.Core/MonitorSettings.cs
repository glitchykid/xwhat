using System.Text.Json;

namespace XWhat.Core;

public enum OverlayMode { Desktop, Rtss, Auto }
public enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>Persisted user preferences. Normalize before applying data read from disk.</summary>
public sealed record MonitorSettings
{
    public int SchemaVersion { get; init; } = 2;
    public bool StartWithWindows { get; init; }
    public OverlayMode Mode { get; init; } = OverlayMode.Auto;
    public OverlayCorner Corner { get; init; }
    public double Scale { get; init; } = 1;
    public double BackgroundOpacity { get; init; }
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
        SchemaVersion = 2,
        Mode = Enum.IsDefined(Mode) ? Mode : OverlayMode.Auto,
        Corner = Enum.IsDefined(Corner) ? Corner : OverlayCorner.TopLeft,
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, 0.75, 2) : 1,
        BackgroundOpacity = double.IsFinite(BackgroundOpacity) ? Math.Clamp(BackgroundOpacity, 0, 0.9) : 0,
        SampleIntervalMs = Math.Clamp(SampleIntervalMs, 250, 2000),
        HistorySeconds = Math.Clamp(HistorySeconds, 5, 60),
        StutterThresholdMs = double.IsFinite(StutterThresholdMs) ? Math.Clamp(StutterThresholdMs, 16, 200) : 33.3,
        ProcessName = Path.GetFileName(ProcessName?.Trim() ?? ""),
        GpuId = GpuId ?? "",
        DiskId = DiskId ?? ""
    };

    public static MonitorSettings FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Настройки должны быть объектом JSON.");
        var settings = JsonSerializer.Deserialize<MonitorSettings>(json) ?? new();
        // The old preset was a decorated RTSS-only panel; migrate it to the new technical default.
        if (!document.RootElement.TryGetProperty(nameof(SchemaVersion), out _))
            settings = settings with { Mode = OverlayMode.Auto, BackgroundOpacity = 0 };
        return settings.Normalize();
    }
}

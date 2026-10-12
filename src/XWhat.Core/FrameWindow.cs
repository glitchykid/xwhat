namespace XWhat.Core;

public sealed record FrameSnapshot(double? Fps, double? OnePercentLow, double? LatestMs, double? MaximumMs,
    int StutterCount, SamplePoint[] Samples)
{
    public static FrameSnapshot Empty { get; } = new(null, null, null, null, 0, []);
}

/// <summary>Stores individual Present intervals, in ms. Statistics cover the chosen rolling window.
/// A stutter exceeds both the user threshold and twice the window median. This is a heuristic, not a cause diagnosis.</summary>
public sealed class FrameWindow
{
    private readonly SampleHistory _history = new(65536);
    public void Add(double time, double milliseconds, double threshold)
    {
        if (double.IsFinite(time) && double.IsFinite(milliseconds) && milliseconds is > 0 and <= 10000)
            _history.Add(time, milliseconds);
    }

    public FrameSnapshot Read(double now, double seconds, double threshold)
    {
        var samples = _history.Read(now, seconds);
        if (samples.Length == 0 || now - samples[^1].Time > 2) return FrameSnapshot.Empty;
        var durations = samples.Select(s => s.Value).Order().ToArray();
        var lowCount = Math.Max(1, (int)Math.Ceiling(durations.Length * 0.01));
        var lowAverage = durations.AsSpan(durations.Length - lowCount).ToArray().Average();
        var stutterLimit = Math.Max(threshold, durations[durations.Length / 2] * 2);
        return new(1000 / durations.Average(), 1000 / lowAverage, samples[^1].Value, durations[^1],
            durations.Count(ms => ms > stutterLimit), samples);
    }
}

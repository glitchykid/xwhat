namespace XWhat.Core;

/// <summary>Time aligned pixel buckets. Min/max keep individual frame spikes visible; NaN marks an empty bucket.</summary>
public sealed record GraphEnvelope(double[] Minimum, double[] Maximum)
{
    public static GraphEnvelope Create(SamplePoint[] samples, double now, double seconds, int width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(seconds, 0);
        var minima = Enumerable.Repeat(double.NaN, width).ToArray();
        var maxima = Enumerable.Repeat(double.NaN, width).ToArray();
        foreach (var sample in samples)
        {
            if (!double.IsFinite(sample.Value) || sample.Time < now - seconds || sample.Time > now) continue;
            var index = Math.Clamp((int)((sample.Time - now + seconds) / seconds * (width - 1)), 0, width - 1);
            minima[index] = double.IsNaN(minima[index]) ? sample.Value : Math.Min(minima[index], sample.Value);
            maxima[index] = double.IsNaN(maxima[index]) ? sample.Value : Math.Max(maxima[index], sample.Value);
        }
        return new(minima, maxima);
    }
}

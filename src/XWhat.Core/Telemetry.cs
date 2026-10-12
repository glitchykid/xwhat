namespace XWhat.Core;

/// <summary>Time is monotonic seconds; missing or inaccessible readings are null, never fake zero.</summary>
public readonly record struct SamplePoint(double Time, double Value);
public sealed record DeviceChoice(string Id, string Name);
public sealed record Metric(double? Load = null, double? Temperature = null, double? UsedGiB = null, double? TotalGiB = null)
{
    /// <summary>Zero/non-finite temperatures from inaccessible sensors are unavailable, never 0°C readings.</summary>
    public static double? ValidTemperature(double? value) => value is > 0 and < 150 && double.IsFinite(value.Value) ? value : null;
}
public sealed record HardwareSample(double Time, Metric Cpu, Metric Gpu, Metric Ram, Metric Vram, Metric Disk,
    double? DiskReadMiBs = null, double? DiskWriteMiBs = null, double? DiskLatencyMs = null,
    string CpuName = "", string GpuName = "", string DiskName = "", string Status = "");

/// <summary>Fixed capacity, thread safe history. Reads return oldest to newest within an inclusive time window.</summary>
public sealed class SampleHistory
{
    private readonly SamplePoint[] _samples;
    private readonly Lock _gate = new();
    private int _next;
    private int _count;

    public SampleHistory(int capacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _samples = new SamplePoint[capacity];
    }

    public void Add(double time, double? value)
    {
        lock (_gate)
        {
            _samples[_next] = new(time, value ?? double.NaN);
            _next = (_next + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }
    }

    public SamplePoint[] Read(double now, double seconds)
    {
        lock (_gate)
        {
            var result = new List<SamplePoint>(_count);
            for (var i = 0; i < _count; i++)
            {
                var sample = _samples[(_next - _count + i + _samples.Length) % _samples.Length];
                if (sample.Time >= now - seconds && sample.Time <= now) result.Add(sample);
            }
            return result.ToArray();
        }
    }
}

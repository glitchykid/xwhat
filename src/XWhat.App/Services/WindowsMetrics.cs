using System.Runtime.InteropServices;

namespace XWhat.App.Services;

internal sealed class WindowsMetrics : IDisposable
{
    private ulong _idle, _kernel, _user;
    private bool _primed;
    private nint _query;
    private readonly nint _read, _write, _latency, _busy;
    public WindowsMetrics()
    {
        if (PdhOpenQuery(null, 0, out _query) != 0) return;
        _read = Add(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec");
        _write = Add(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec");
        _latency = Add(@"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer");
        _busy = Add(@"\PhysicalDisk(_Total)\% Idle Time");
        PdhCollectQueryData(_query);
    }

    public double? CpuLoad()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        var total = (kernel - _kernel) + (user - _user);
        var idleDelta = idle - _idle;
        var value = _primed && total > 0 ? Math.Clamp(100.0 * (total - Math.Min(total, idleDelta)) / total, 0, 100) : (double?)null;
        (_idle, _kernel, _user, _primed) = (idle, kernel, user, true);
        return value;
    }

    public (double? Used, double? Total) Memory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status)) return (null, null);
        const double gib = 1024d * 1024 * 1024;
        return ((status.TotalPhysical - status.AvailablePhysical) / gib, status.TotalPhysical / gib);
    }

    public (double? Read, double? Write, double? Latency, double? Busy) Disk()
    {
        if (_query == 0 || PdhCollectQueryData(_query) != 0) return (null, null, null, null);
        var idle = Value(_busy);
        return (Value(_read) / (1024 * 1024), Value(_write) / (1024 * 1024), Value(_latency) * 1000,
            idle.HasValue ? Math.Clamp(100 - idle.Value, 0, 100) : null);
    }
    private nint Add(string path) => PdhAddEnglishCounter(_query, path, 0, out var counter) == 0 ? counter : 0;
    private static double? Value(nint counter) => counter != 0 && PdhGetFormattedCounterValue(counter, 0x200, out _, out var value) == 0 &&
        value.Status is 0 or 1 && double.IsFinite(value.Value) ? value.Value : null;
    public void Dispose() { if (_query != 0) { PdhCloseQuery(_query); _query = 0; } }

    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)] private struct CounterValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double Value;
    }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhOpenQueryW")] private static extern uint PdhOpenQuery(string? source, nuint data, out nint query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")] private static extern uint PdhAddEnglishCounter(nint query, string path, nuint data, out nint counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(nint query);
    [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(nint query);
}

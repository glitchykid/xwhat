using LibreHardwareMonitor.Hardware;
using XWhat.Core;

namespace XWhat.App.Services;

/// <summary>Single background thread owns Computer and all sensor access. No sensor/control values are written.</summary>
internal sealed class HardwareMonitor : IDisposable
{
    private readonly Computer _computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsStorageEnabled = true };
    private readonly WindowsMetrics _windows = new();
    private readonly SettingsStore _store;
    private string _status = "";
    public DeviceChoice[] Gpus { get; private set; } = [];
    public DeviceChoice[] Disks { get; private set; } = [];

    public HardwareMonitor(SettingsStore store)
    {
        _store = store;
        try { _computer.Open(); }
        catch (Exception ex) { _status = "Часть датчиков недоступна"; _store.Log($"Sensor init: {ex.Message}"); }
    }

    public HardwareSample Read(double time, MonitorSettings settings)
    {
        var hardware = _computer.Hardware;
        foreach (var device in hardware)
        {
            try { Update(device); }
            catch (Exception ex) { _status = "Часть датчиков недоступна"; _store.Log($"Sensor {device.Name}: {ex.Message}"); }
        }
        var gpus = hardware.Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel).ToArray();
        var disks = hardware.Where(h => h.HardwareType == HardwareType.Storage).ToArray();
        Gpus = gpus.Select(h => new DeviceChoice(h.Identifier.ToString(), h.Name)).ToArray();
        Disks = disks.Select(h => new DeviceChoice(h.Identifier.ToString(), h.Name)).ToArray();
        var cpu = hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        var gpu = gpus.FirstOrDefault(h => h.Identifier.ToString() == settings.GpuId) ?? gpus.OrderByDescending(h =>
            Find(h, SensorType.SmallData, "GPU Memory Total") ?? Find(h, SensorType.Data, "GPU Memory Total") * 1024 ?? 0).FirstOrDefault();
        var disk = disks.FirstOrDefault(h => h.Identifier.ToString() == settings.DiskId);
        var memory = _windows.Memory();
        var io = _windows.Disk();
        var vramUsed = Find(gpu, SensorType.SmallData, "GPU Memory Used") / 1024 ?? Find(gpu, SensorType.Data, "GPU Memory Used");
        var vramTotal = Find(gpu, SensorType.SmallData, "GPU Memory Total") / 1024 ?? Find(gpu, SensorType.Data, "GPU Memory Total");
        var gpuLoad = Find(gpu, SensorType.Load, "GPU Core", "D3D 3D");
        var cpuTemperature = Find(cpu, SensorType.Temperature, "CPU Package", "Core (Tctl/Tdie)", "Core (Tdie)") ?? MaxTemperature(cpu);
        var diskLoad = Find(disk, SensorType.Load, "Total Activity", "Activity");
        var diskRead = Find(disk, SensorType.Throughput, "Read Rate") / (1024 * 1024);
        var diskWrite = Find(disk, SensorType.Throughput, "Write Rate") / (1024 * 1024);
        return new(time, new(_windows.CpuLoad(), cpuTemperature), new(gpuLoad, Find(gpu, SensorType.Temperature, "GPU Core") ?? MaxTemperature(gpu)),
            new(Percent(memory.Used, memory.Total), MaxTemperature(hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory)), memory.Used, memory.Total),
            new(Percent(vramUsed, vramTotal), Find(gpu, SensorType.Temperature, "GPU Memory Junction", "GPU Memory"), vramUsed, vramTotal),
            new(diskLoad ?? (settings.DiskId.Length == 0 ? io.Busy : null), disk != null ? MaxTemperature(disk) : disks.Select(MaxTemperature).Max()),
            diskRead ?? (settings.DiskId.Length == 0 ? io.Read : null), diskWrite ?? (settings.DiskId.Length == 0 ? io.Write : null),
            settings.DiskId.Length == 0 ? io.Latency : null, cpu?.Name ?? "CPU", gpu?.Name ?? "GPU", disk?.Name ?? "Все диски", _status);
    }

    private static void Update(IHardware hardware) { hardware.Update(); foreach (var child in hardware.SubHardware) Update(child); }
    private static IEnumerable<ISensor> Sensors(IHardware? hardware) => hardware == null ? [] : hardware.Sensors.Concat(hardware.SubHardware.SelectMany(Sensors));
    private static double? Find(IHardware? hardware, SensorType type, params string[] names)
    {
        var sensors = Sensors(hardware).Where(s => s.SensorType == type && s.Value.HasValue && float.IsFinite(s.Value.Value) &&
            (type != SensorType.Temperature || Metric.ValidTemperature(s.Value) != null)).ToArray();
        foreach (var name in names)
        {
            var sensor = sensors.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (sensor?.Value is { } value) return value;
        }
        return null;
    }
    private static double? MaxTemperature(IHardware? hardware) => Sensors(hardware).Where(s => s.SensorType == SensorType.Temperature && s.Value is > 0 and < 150).Select(s => (double?)s.Value).Max();
    private static double? Percent(double? used, double? total) => total is > 0 && used.HasValue ? Math.Clamp(used.Value / total.Value * 100, 0, 100) : null;
    public void Dispose() { _computer.Close(); _windows.Dispose(); }
}

using System.Globalization;
using System.Text;

namespace XWhat.Core;

public readonly record struct PresentFrame(int ProcessId, string SwapChain, double Milliseconds, double? CaptureTimeMs, bool IsQpcTimestamp = false);

/// <summary>Reads PresentMon 2.x/2.6 CSV by header names, using invariant units and RFC4180 quoted fields.</summary>
public sealed class PresentMonCsv
{
    private int _pid = -1, _chain = -1, _duration = -1, _time = -1;
    private bool _qpc;

    public bool SetHeader(string line)
    {
        var names = Split(line.TrimStart('\uFEFF'));
        _pid = names.IndexOf("ProcessID");
        _chain = names.IndexOf("SwapChainAddress");
        _duration = names.IndexOf("MsBetweenPresents");
        if (_duration < 0) _duration = names.IndexOf("msBetweenPresents");
        if (_duration < 0) _duration = names.IndexOf("CPUFrameTime");
        _time = names.IndexOf("CPUStartQPCTime");
        _qpc = _time >= 0;
        if (_time < 0) _time = names.IndexOf("CPUStartTime");
        return _pid >= 0 && _chain >= 0 && _duration >= 0;
    }

    public bool TryRead(string line, out PresentFrame frame)
    {
        frame = default;
        if (_pid < 0 || _chain < 0 || _duration < 0) return false;
        var cells = Split(line);
        if (cells.Count <= Math.Max(_pid, Math.Max(_chain, _duration)) ||
            !int.TryParse(cells[_pid], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ||
            !double.TryParse(cells[_duration], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) ||
            !double.IsFinite(duration) || duration is <= 0 or > 10000) return false;
        double? time = _time >= 0 && cells.Count > _time && double.TryParse(cells[_time], NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed) ? parsed : null;
        frame = new(pid, cells[_chain], duration, time, _qpc);
        return true;
    }

    private static List<string> Split(string line)
    {
        var cells = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { cells.Add(value.ToString()); value.Clear(); }
            else value.Append(ch);
        }
        cells.Add(value.ToString());
        return cells;
    }
}

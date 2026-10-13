using System.Buffers.Binary;
using System.Text;

namespace XWhat.Core;

public sealed record RtssGraph(float[] Values, float Minimum, float Maximum, int Width, int Height);

/// <summary>RTSS SDK shared memory v2.14+. Caller must hold bit 0 of dwBusy while writing.
/// Uses server supplied offsets/strides, never fixed array positions. Slot 0 belongs to RTSS.</summary>
public static class RtssProtocol
{
    public const int EntryBytes = 4608 + 262144;

    public static bool TryWrite(Span<byte> memory, string owner, string text, RtssGraph[] graphs, out string error)
    {
        error = "RTSS: несовместимая общая память";
        if (!Validate(memory, out var offset, out var stride, out var count)) return false;
        if (owner.Length is 0 or > 255 || !owner.All(char.IsAscii)) return false;
        var needed = graphs.Sum(g => 36L + g.Values.Length * 4L);
        if (needed > 262144 || graphs.Any(g => !float.IsFinite(g.Minimum) || !float.IsFinite(g.Maximum) || g.Maximum <= g.Minimum)) return false;
        var rendered = new StringBuilder(text);
        var graphOffset = 0;
        for (var i = 0; i < graphs.Length; i++)
        {
            // RTSS hypertext interprets object addresses as hexadecimal byte offsets.
            rendered.Replace($"{{graph{i}}}", $"<OBJ={graphOffset:X8}>");
            graphOffset += 36 + graphs[i].Values.Length * 4;
        }
        if (Encoding.ASCII.GetByteCount(rendered.ToString()) > 4095) return false;
        var slot = -1;
        for (var i = 1; i < count; i++)
            if (ReadOwner(memory.Slice(offset + i * stride + 256, 256)) == owner) { slot = i; break; }
        if (slot < 0)
            for (var i = 1; i < count; i++)
                if (memory[offset + i * stride + 256] == 0) { slot = i; break; }
        if (slot < 0) { error = "RTSS: все слоты OSD заняты"; return false; }
        var entry = memory.Slice(offset + slot * stride, stride);
        graphOffset = 0;
        for (var i = 0; i < graphs.Length; i++)
        {
            var graph = graphs[i];
            var size = 36 + graph.Values.Length * 4;
            var target = entry.Slice(4608 + graphOffset, size);
            WriteInt(target, 0, 0x47523030); // SDK multi-character signature 'GR00'.
            WriteInt(target, 4, size);
            WriteInt(target, 8, graph.Width);
            WriteInt(target, 12, graph.Height);
            WriteInt(target, 16, 0);
            WriteInt(target, 20, 0);
            BinaryPrimitives.WriteSingleLittleEndian(target[24..], graph.Minimum);
            BinaryPrimitives.WriteSingleLittleEndian(target[28..], graph.Maximum);
            WriteInt(target, 32, graph.Values.Length);
            for (var j = 0; j < graph.Values.Length; j++)
                BinaryPrimitives.WriteSingleLittleEndian(target[(36 + j * 4)..], float.IsFinite(graph.Values[j]) ? graph.Values[j] : 0);
            graphOffset += size;
        }
        WriteText(entry.Slice(256, 256), owner);
        WriteText(entry.Slice(512, 4096), rendered.ToString());
        BinaryPrimitives.WriteUInt32LittleEndian(memory[32..], unchecked(BinaryPrimitives.ReadUInt32LittleEndian(memory[32..]) + 1));
        error = "RTSS подключён";
        return true;
    }

    public static void Clear(Span<byte> memory, string owner)
    {
        if (!Validate(memory, out var offset, out var stride, out var count)) return;
        for (var i = 1; i < count; i++)
        {
            var entry = memory.Slice(offset + i * stride, stride);
            if (ReadOwner(entry.Slice(256, 256)) == owner) entry.Clear();
        }
        BinaryPrimitives.WriteUInt32LittleEndian(memory[32..], unchecked(BinaryPrimitives.ReadUInt32LittleEndian(memory[32..]) + 1));
    }

    private static bool Validate(ReadOnlySpan<byte> memory, out int offset, out int stride, out int count)
    {
        offset = stride = count = 0;
        if (memory.Length < 40 || BinaryPrimitives.ReadUInt32LittleEndian(memory) != 0x52545353 ||
            BinaryPrimitives.ReadUInt32LittleEndian(memory[4..]) < 0x0002000e) return false;
        var rawStride = BinaryPrimitives.ReadUInt32LittleEndian(memory[20..]);
        var rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(memory[24..]);
        var rawCount = BinaryPrimitives.ReadUInt32LittleEndian(memory[28..]);
        if (rawStride < EntryBytes || rawOffset < 40 || rawCount is < 2 or > 1024 ||
            rawOffset + (ulong)rawStride * rawCount > (ulong)memory.Length) return false;
        offset = (int)rawOffset; stride = (int)rawStride; count = (int)rawCount;
        return true;
    }

    private static string ReadOwner(ReadOnlySpan<byte> field)
    {
        var end = field.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? field : field[..end]);
    }
    private static void WriteText(Span<byte> field, string value)
    {
        field.Clear();
        Encoding.ASCII.GetBytes(value.AsSpan(), field);
    }
    private static void WriteInt(Span<byte> target, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(target[offset..], value);
}

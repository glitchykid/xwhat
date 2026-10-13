using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace XWhat.Core.Tests;

public sealed class RtssAddressTests
{
    [Fact]
    public void EmbeddedObjectReferencesUseSdkHexadecimalByteOffsets()
    {
        const int offset = 64;
        var memory = new byte[offset + 2 * RtssProtocol.EntryBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(memory, 0x52545353);
        BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(4), 0x0002000e);
        BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(20), RtssProtocol.EntryBytes);
        BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(24), offset);
        BinaryPrimitives.WriteInt32LittleEndian(memory.AsSpan(28), 2);
        RtssGraph[] graphs = [new([1, 2], 0, 100, 120, 14), new([3, 4], 0, 100, 120, 14)];
        Assert.True(RtssProtocol.TryWrite(memory, "XWhat.test", "{graph0}\n{graph1}", graphs, out _));
        var text = Encoding.ASCII.GetString(memory, offset + RtssProtocol.EntryBytes + 512, 4096).TrimEnd('\0');
        Assert.Equal("<OBJ=00000000>\n<OBJ=0000002C>", text);
    }
}

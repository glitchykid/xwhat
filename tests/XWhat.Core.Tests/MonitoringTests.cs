using System.Buffers.Binary;
using XWhat.Core;
using Xunit;

namespace XWhat.Core.Tests;

public sealed class MonitoringTests
{
    [Fact]
    public void HistoryIsBoundedAndChronological()
    {
        var history = new SampleHistory(3);
        for (var i = 0; i < 5; i++) history.Add(i, i * 10);
        Assert.Equal(new double[] { 20, 30, 40 }, history.Read(4, 10).Select(p => p.Value));
        Assert.Equal(new double[] { 30, 40 }, history.Read(4, 1).Select(p => p.Value));
    }

    [Fact]
    public void FrameStatisticsUseFrameDurationsAndRetainStutterPeaks()
    {
        var frames = new FrameWindow();
        for (var i = 0; i < 100; i++) frames.Add(i / 100.0, 10, 33);
        frames.Add(1, 100, 33);
        var snapshot = frames.Read(1, 10, 33);
        Assert.Equal(1000 / (1100.0 / 101), snapshot.Fps!.Value, 5);
        Assert.Equal(100, snapshot.MaximumMs);
        Assert.Equal(1, snapshot.StutterCount);
        Assert.True(snapshot.OnePercentLow < snapshot.Fps);
        Assert.Equal(101, snapshot.Samples.Length);
    }

    [Fact]
    public void FrameStatisticsRejectInvalidValuesAndExpireWhenGameStops()
    {
        var frames = new FrameWindow();
        frames.Add(0, double.NaN, 33);
        frames.Add(0, -5, 33);
        frames.Add(0, 0, 33);
        Assert.Null(frames.Read(0, 10, 33).Fps);
        frames.Add(1, 16, 33);
        Assert.Null(frames.Read(4, 10, 33).Fps);
    }

    [Fact]
    public void CsvParserHandlesQuotedProcessNamesAndV26Columns()
    {
        var csv = new PresentMonCsv();
        Assert.True(csv.SetHeader("Application,ProcessID,SwapChainAddress,CPUStartTime,MsBetweenPresents,MsInPresentAPI"));
        Assert.True(csv.TryRead("\"game, edition.exe\",42,0x123,1000.25,16.667,0.1", out var frame));
        Assert.Equal(42, frame.ProcessId);
        Assert.Equal("0x123", frame.SwapChain);
        Assert.Equal(16.667, frame.Milliseconds);
        Assert.False(csv.TryRead("game.exe,42,0x123,1010,NA,0", out _));
        Assert.False(csv.TryRead("game.exe,42,0x123,1010,NaN,0", out _));
    }

    [Fact]
    public void CsvParserSupportsV2CpuFrameTimeFallback()
    {
        var csv = new PresentMonCsv();
        Assert.True(csv.SetHeader("Application,ProcessID,SwapChainAddress,CPUFrameTime"));
        Assert.True(csv.TryRead("game.exe,2,0xA,12.5", out var frame));
        Assert.Equal(12.5, frame.Milliseconds);
    }

    [Fact]
    public void CsvParserPreservesQpcTimestampsForBatchedFrameDelivery()
    {
        var csv = new PresentMonCsv();
        Assert.True(csv.SetHeader("Application,ProcessID,SwapChainAddress,CPUStartQPCTime,MsBetweenPresents"));
        Assert.True(csv.TryRead("game.exe,2,0xA,1234567.125,12.5", out var frame));
        Assert.True(frame.IsQpcTimestamp);
        Assert.Equal(1234567.125, frame.CaptureTimeMs);
    }

    [Fact]
    public void SettingsRepairCorruptRanges()
    {
        var settings = new MonitorSettings { Scale = double.NaN, BackgroundOpacity = 2, SampleIntervalMs = 1, HistorySeconds = 999, StutterThresholdMs = -2 };
        var valid = settings.Normalize();
        Assert.Equal(1, valid.Scale);
        Assert.Equal(0.9, valid.BackgroundOpacity);
        Assert.Equal(250, valid.SampleIntervalMs);
        Assert.Equal(60, valid.HistorySeconds);
        Assert.Equal(16, valid.StutterThresholdMs);
    }

    [Fact]
    public void RtssProtocolClaimsOnlyFreeSlotAndPreservesOtherClients()
    {
        var memory = CreateRtssMemory();
        memory[64 + RtssProtocol.EntryBytes + 256] = (byte)'A';
        var before = memory.AsSpan(64 + RtssProtocol.EntryBytes, RtssProtocol.EntryBytes).ToArray();
        Assert.True(RtssProtocol.TryWrite(memory, "XWhat.test", "CPU 50%", [new RtssGraph([10, 100, 12], 0, 120, 260, 40)], out _));
        Assert.Equal(before, memory.AsSpan(64 + RtssProtocol.EntryBytes, RtssProtocol.EntryBytes).ToArray());
        var slot = 64 + 2 * RtssProtocol.EntryBytes;
        Assert.Equal((byte)'X', memory[slot + 256]);
        Assert.Equal(0x47523030u, BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan(slot + 4608)));
        Assert.Equal(100f, BinaryPrimitives.ReadSingleLittleEndian(memory.AsSpan(slot + 4608 + 40)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan(32)));
        RtssProtocol.Clear(memory, "XWhat.test");
        Assert.Equal(0, memory[slot + 256]);
    }

    [Fact]
    public void RtssProtocolRejectsInvalidOffsetsWithoutWriting()
    {
        var memory = CreateRtssMemory();
        BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(24), uint.MaxValue);
        var before = memory.ToArray();
        Assert.False(RtssProtocol.TryWrite(memory, "XWhat.test", "hello", [], out _));
        Assert.Equal(before, memory);
    }

    [Fact]
    public void GraphDownsamplingPreservesBothShortSpikesAndMissingData()
    {
        var buckets = GraphEnvelope.Create([new(0, 10), new(0.001, 120), new(0.002, 12), new(0.5, double.NaN)], 1, 1, 10);
        Assert.Equal(120, buckets.Maximum[0]);
        Assert.Equal(10, buckets.Minimum[0]);
        Assert.True(double.IsNaN(buckets.Maximum[4]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    [InlineData(double.NaN)]
    public void UnavailableTemperatureSentinelsAreNeverDisplayedAsMeasurements(double value)
    {
        Assert.Null(Metric.ValidTemperature(value));
        Assert.Equal(54, Metric.ValidTemperature(54));
    }

    [Fact]
    public void RtssProtocolMustNotModifyMemoryIfTextIsTooLong()
    {
        var memory = CreateRtssMemory();
        var before = memory.ToArray();
        Assert.False(RtssProtocol.TryWrite(memory, "XWhat.test", new string('X', 4096), [new RtssGraph([12], 0, 100, 20, 10)], out _));
        Assert.Equal(before, memory);
    }

    private static byte[] CreateRtssMemory()
    {
        var memory = new byte[64 + 4 * RtssProtocol.EntryBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(memory, 0x52545353);
        BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(4), 0x0002000f);
        BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(20), RtssProtocol.EntryBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(24), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(28), 4);
        return memory;
    }
}

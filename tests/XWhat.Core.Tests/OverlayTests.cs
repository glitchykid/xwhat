using XWhat.Core;
using Xunit;

namespace XWhat.Core.Tests;

public sealed class OverlayTests
{
    [Theory]
    [InlineData(true, true, OverlayMode.Rtss)]
    [InlineData(true, false, OverlayMode.Desktop)]
    [InlineData(false, true, OverlayMode.Desktop)]
    public void AutomaticOutputUsesRtssOnlyForFullscreenAndAvailableServer(bool fullscreen, bool available, OverlayMode expected)
        => Assert.Equal(expected, OverlayRouting.Resolve(OverlayMode.Auto, fullscreen, available));

    [Theory]
    [InlineData(OverlayMode.Desktop)]
    [InlineData(OverlayMode.Rtss)]
    public void ManualOutputOverridesAutomaticDetection(OverlayMode mode)
        => Assert.Equal(mode, OverlayRouting.Resolve(mode, true, false));

    [Fact]
    public void FullscreenDetectionHandlesNegativeMonitorCoordinatesAndTitledMaximizedWindows()
    {
        var monitor = new ScreenBounds(-2560, 0, 0, 1440);
        Assert.True(FullscreenDetection.IsFullscreen(new(-2560, 0, 0, 1440), monitor, false, false));
        Assert.True(FullscreenDetection.IsFullscreen(new(-2561, -1, 1, 1441), monitor, false, false));
        Assert.False(FullscreenDetection.IsFullscreen(new(-2560, 0, 0, 1440), monitor, true, false));
        Assert.False(FullscreenDetection.IsFullscreen(new(-2500, 30, -100, 1400), monitor, false, false));
        Assert.True(FullscreenDetection.IsFullscreen(new(-2500, 30, -100, 1400), monitor, false, true));
    }

    [Fact]
    public void CornerPlacementUsesMonitorOriginDpiAndSmallInset()
    {
        var position = OverlayPlacement.Calculate(new(-2560, 0, 0, 1440), OverlayCorner.BottomRight, 316, 144, 1, 144);
        Assert.Equal(-480, position.X);
        Assert.Equal(1218, position.Y);
    }

    [Fact]
    public void LegacySettingsUpgradeToAutomaticTransparentHudWithoutLosingDeviceSelection()
    {
        var settings = MonitorSettings.FromJson("{\"Mode\":1,\"BackgroundOpacity\":0.58,\"GpuId\":\"/gpu/1\",\"HistorySeconds\":30}");
        Assert.Equal(OverlayMode.Auto, settings.Mode);
        Assert.Equal(0, settings.BackgroundOpacity);
        Assert.Equal("/gpu/1", settings.GpuId);
        Assert.Equal(30, settings.HistorySeconds);
        Assert.False(settings.StartWithWindows);
    }

    [Fact]
    public void CurrentSettingsKeepExplicitOutputAndAutostartChoice()
    {
        var settings = MonitorSettings.FromJson("{\"SchemaVersion\":2,\"Mode\":1,\"BackgroundOpacity\":0.4,\"StartWithWindows\":true}");
        Assert.Equal(OverlayMode.Rtss, settings.Mode);
        Assert.Equal(0.4, settings.BackgroundOpacity);
        Assert.True(settings.StartWithWindows);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void NonObjectSettingsFailWithRecoverableJsonError(string json)
        => Assert.Throws<System.Text.Json.JsonException>(() => MonitorSettings.FromJson(json));

    [Fact]
    public void StartupCommandQuotesExecutablePathsAndRequestsSilentLaunch()
    {
        var path = Path.GetFullPath(Path.Combine("folder with spaces", "XWhat.exe"));
        Assert.Equal($"\"{path}\" --autostart", StartupCommand.Build(path));
        Assert.Throws<ArgumentException>(() => StartupCommand.Build("XWhat.exe"));
        Assert.Throws<ArgumentException>(() => StartupCommand.Build(path + "\" --other-program"));
        Assert.Throws<ArgumentException>(() => StartupCommand.Build(Path.GetPathRoot(path)! + new string('a', 260) + ".exe"));
    }

    [Fact]
    public void HudContainsOnlyTechnicalRowsWithFrameAndLoadGraphs()
    {
        var frames = new FrameSnapshot(144, 110, 7, 48, 2, [new(0, 7), new(1, 48)]);
        var hardware = new HardwareSample(1, new(55, 65), new(90, 71), new(50, null, 16, 32), new(60, null, 7.2, 12), new(12, 40), 112, 8, 1.7);
        var rows = HudRows.Build(hardware, frames, [[], [], [], [], []]);
        Assert.Equal(new[] { "FPS", "FT", "CPU", "GPU", "RAM", "VRAM", "DISK", "IO" }, rows.Select(r => r.Label));
        Assert.Contains("144", rows[0].Values);
        Assert.Contains("1% 110", rows[0].Values);
        Assert.Equal(48, rows[1].Graph[1].Value);
        Assert.Equal("55% 65C", rows[2].Values);
        Assert.DoesNotContain(rows, r => r.Values.Contains("WHAT", StringComparison.Ordinal));
        var rtss = HudRows.RtssText(rows, true);
        Assert.Contains("55% 65C", rtss);
        Assert.DoesNotContain("WHAT", rtss);
        Assert.Equal(7, rtss.Split("{graph").Length - 1);
    }
}

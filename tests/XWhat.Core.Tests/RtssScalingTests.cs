using System.Globalization;
using Xunit;

namespace XWhat.Core.Tests;

public sealed class RtssScalingTests
{
    [Theory]
    [InlineData(0.75, 75, 90, 11)]
    [InlineData(0.8, 80, 96, 11)]
    [InlineData(1, 100, 120, 14)]
    [InlineData(1.25, 125, 150, 18)]
    [InlineData(1.5, 150, 180, 21)]
    [InlineData(1.75, 175, 210, 25)]
    [InlineData(2, 200, 240, 28)]
    public void SelectedScaleChangesTextAndEveryGraphWhileRetainingFramePeaks(double scale, int percent, int width, int height)
    {
        var settings = new MonitorSettings { Mode = OverlayMode.Rtss, Scale = scale };
        var frames = new FrameSnapshot(120, 90, 8, 80, 1, [new(0, 8), new(0.001, 80), new(1, 8)]);
        var rows = HudRows.Build(null, frames, [[], [], [], [], []]);
        var text = HudRows.RtssText(rows, settings.ShowGraphs, scale: settings.Scale);
        Assert.StartsWith($"<FNT=Consolas,-9,400,1><S={percent}>", text);
        Assert.EndsWith("<S>", text);
        var graphs = HudRows.RtssGraphs(rows, settings, 1);
        Assert.Equal(7, graphs.Length);
        Assert.All(graphs, graph =>
        {
            Assert.Equal(width, graph.Width);
            Assert.Equal(height, graph.Height);
            Assert.Equal(width, graph.Values.Length);
        });
        Assert.Equal(80, graphs[1].Values.Max());
    }

    [Fact]
    public void TextOnlyOutputStillScalesAndResetsSizeWithInvariantTagFormatting()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var settings = new MonitorSettings { Mode = OverlayMode.Rtss, Scale = 1.25, ShowGraphs = false };
            HudRow[] rows = [new("CPU", "50% 65C", [], 100)];
            var text = HudRows.RtssText(rows, settings.ShowGraphs, scale: settings.Scale);
            Assert.StartsWith("<FNT=Consolas,-9,400,1><S=125>", text);
            Assert.Contains("50% 65C", text);
            Assert.DoesNotContain("{graph", text);
            Assert.EndsWith("<S>", text);
            Assert.Empty(HudRows.RtssGraphs(rows, settings, 0));
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }
}

using Xunit;

namespace XWhat.Core.Tests;

public sealed class ReadinessTests
{
    [Fact]
    public void FullscreenRequiresRtssAndFrameAccessBeforeItIsReady()
    {
        var missing = MonitoringReadiness.Check(OverlayMode.Auto, true, false, false, false);
        Assert.True(missing.MissingRtss);
        Assert.True(missing.MissingFrameAccess);
        Assert.False(missing.Ready);
        var launching = MonitoringReadiness.Check(OverlayMode.Rtss, false, true, false, true);
        Assert.False(launching.MissingRtss);
        Assert.True(launching.WaitingRtss);
        Assert.False(launching.Ready);
        Assert.True(MonitoringReadiness.Check(OverlayMode.Auto, true, true, true, true).Ready);
        Assert.True(MonitoringReadiness.Check(OverlayMode.Desktop, false, false, false, true).Ready);
    }
}

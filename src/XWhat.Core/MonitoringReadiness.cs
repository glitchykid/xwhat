namespace XWhat.Core;

/// <summary>Prerequisites only; readiness does not assert that a game accepts the RTSS hook.</summary>
public sealed record MonitoringReadiness(bool MissingRtss, bool WaitingRtss, bool MissingFrameAccess)
{
    public bool Ready => !MissingRtss && !WaitingRtss && !MissingFrameAccess;
    public static MonitoringReadiness Check(OverlayMode mode, bool fullscreen, bool rtssInstalled, bool rtssAvailable, bool frameAccess)
    {
        var needsRtss = mode == OverlayMode.Rtss || mode == OverlayMode.Auto && fullscreen;
        return new(needsRtss && !rtssInstalled && !rtssAvailable,
            needsRtss && rtssInstalled && !rtssAvailable, !frameAccess);
    }
}

# Verification

Automated: `scripts/Build.ps1` exercises history bounds, frame statistics/expiry, CSV quoting/versioned headers, settings normalization, peak-preserving graph reduction, RTSS signature/bounds/slot isolation/graph serialization and rejected-write atomicity.

`XWhat.exe --demo --render-preview=C:\absolute\overlay.png` renders the real WPF HUD and settings surface to PNG, then exits. Demo readings are artificial and clearly marked. `XWhat.exe --smoke-test` samples real hardware for five seconds, writes `smoke-results/telemetry.json` beside the executable and exits. A real smoke check confirms acquisition and lifecycle, but cannot establish correctness against reference hardware meters.

Before publishing a GitHub release:

1. Run the portable package on Windows x64 with ordinary rights and verify CPU/RAM; missing sensors must show `—`.
2. Check supported CPU temperatures with signed PawnIO and required privileges; compare with LibreHardwareMonitor on the same machine.
3. Select the intended discrete GPU on a system with two adapters; verify VRAM units and load against the vendor/Windows monitor.
4. Select individual disks and verify aggregate vs selected I/O semantics. No latency from another disk should appear as selected-disk latency.
5. Start a DirectX game in borderless mode. Confirm PresentMon FPS and frame times, a visible peak during a deliberate workload stall, and empty FPS after stopping the game.
6. Launch RTSS and use an exclusive-fullscreen game. Verify text, all six embedded graphs, contrast, scale, OSD position and coexistence with another RTSS OSD client. Some games block overlays; honor their supported configuration.
7. Switch game processes and swap chains; old samples must be cleared. Check Ctrl+Shift+F10, tray settings, click-through, transparent background, and all corners at different DPI scales.
8. Exit X What. Its PresentMon child/session and its RTSS slot must be gone. Reopen, verify saved settings, then restore a deliberately malformed JSON and verify graceful fallback.
9. Measure CPU/GPU overhead and working set over a long game session. Fixed history capacity prevents telemetry history growth; measured overhead depends on sensor hardware and drivers.

The current automated environment does not include RTSS or a running exclusive-fullscreen game. That path has protocol tests but still needs the above real-game verification.

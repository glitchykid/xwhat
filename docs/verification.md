# Verification

Automated: `scripts/Build.ps1` exercises history bounds, frame statistics/expiry, CSV quoting/versioned headers, settings normalization/migration, peak-preserving graph reduction, RTSS signature/bounds/slot isolation/graph serialization and rejected-write atomicity. It also checks fullscreen geometry, output selection, negative monitor origins and DPI placement, shared technical HUD rows, safe startup commands and actual startup registration/removal in isolated registry keys.

Windows UI tests also exercise the styled editable process field, invalid/valid frame threshold, tray actions and accessible overlay state, and menu item bounds to prevent a clipped Exit action.

Installation tests inspect the fixed executable/argument/principal and read/execute-only user ACL, reject unsafe paths and altered actions, and invoke Task Scheduler's TASK_VALIDATE_ONLY mode without registering a task. Prerequisite routing and RTSS hexadecimal object addresses are regression tested. These tests never grant group membership or install a driver.

RTSS scale tests check text percentage tags, all seven graph dimensions and peak retention at 75%, 80%, 100%, 125%, 150%, 175% and 200%, text-only output, size reset and culture-independent formatting. The settings UI test saves a 75% scale in RTSS mode.

`XWhat.exe --demo --render-preview=C:\absolute\overlay.png` renders the real WPF HUD, settings surface (`overlay.settings.png`) and native tray menu (`overlay.tray.png`), then exits. Demo readings are artificial and clearly marked. `XWhat.exe --smoke-test` samples real hardware for five seconds, writes `smoke-results/telemetry.json` beside the executable and exits. A real smoke check confirms acquisition and lifecycle, but cannot establish correctness against reference hardware meters.

Before publishing a GitHub release:

1. Run the portable package on Windows x64 with ordinary rights and verify CPU/RAM; missing sensors must show `—`.
2. Check supported CPU temperatures with signed PawnIO and required privileges; compare with LibreHardwareMonitor on the same machine.
3. Select the intended discrete GPU on a system with two adapters; verify VRAM units and load against the vendor/Windows monitor.
4. Select individual disks and verify aggregate vs selected I/O semantics. No latency from another disk should appear as selected-disk latency.
5. Start a DirectX game in borderless mode. Confirm PresentMon FPS and frame times, a visible peak during a deliberate workload stall, and empty FPS after stopping the game.
6. Launch RTSS in Raster 3D mode and use an exclusive-fullscreen game. Verify text, all seven embedded graphs, contrast, OSD position and coexistence with another RTSS OSD client. Apply X What's size at 75%, 100%, 125% and 200%; both columns and every graph must change size together without restarting the game. Repeat with graphs disabled and after restarting X What. Position/background settings stay in RTSS. Some games block overlays; honor their supported configuration.
7. Switch game processes and swap chains; old samples must be cleared. Check Ctrl+Shift+F10, tray settings, click-through, transparent background, and all corners at different DPI scales.
8. Exit X What. Its PresentMon child/session and its RTSS slot must be gone. Reopen, verify saved settings, then restore a deliberately malformed JSON and verify graceful fallback.
9. Measure CPU/GPU overhead and working set over a long game session. Fixed history capacity prevents telemetry history growth; measured overhead depends on sensor hardware and drivers.
10. Enable startup in a permanent portable folder, sign out and back in, and confirm a tray icon without an open settings window. Disable startup and confirm removal of only the XWhat Run entry. When moving folders, apply settings to refresh the registered executable path.
11. In Auto, switch between a titled window, borderless fullscreen, exclusive D3D, another monitor and our settings window. RTSS should be selected only for fullscreen when available; stopping RTSS should fall back to Windows output with a settings hint. Compare mixed-DPI corners and manually dragged placement.
12. Open the tray menu and settings at 100%, 150% and 200% DPI. Check all actions, process editing/list selection, dropdown keyboard navigation, sliders, Enter to apply, Escape to close, and expanded diagnostics scrolling. Confirm no clipped controls or large empty image gutter in the menu.
13. Run Setup.exe on a clean Windows x64 machine with recommended components. Confirm verified official downloads, signed PawnIO installation, RTSS startup and a working per-user launch task. Test denied/cancelled UAC, no internet/hash mismatch, existing RTSS/PawnIO, alternate administrator credentials and a standard account after token refresh. Uninstall must remove the X What task while retaining shared components.
14. In Dragon Age: Inquisition, launch X What, restart the game in fullscreen, and confirm RTSS text plus all seven graphs. Compare the shader/OSD state with another RTSS client. A successful installation or shared-memory write alone does not prove an in-game visible overlay.

Automated protocol tests do not verify the injected in-game renderer. The RTSS path still needs the above real-game verification; a passing shared-memory or scale test alone is not evidence of a visible in-game overlay.

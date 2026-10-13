# Changelog

## 0.3.0 — 2026-10-13

- Windows x64 Setup.exe installs X What and automatically downloads missing RTSS 7.3.7 and signed PawnIO 2.2.0 from their official distribution endpoints, with pinned SHA-256 verification.
- Installer configures frame capture access and a protected, on-demand Windows task for sensor/ETW privileges. The installed application uses it without a repeated UAC prompt; no password is stored.
- RTSS starts with X What. Existing third-party installations and their profiles are retained.
- Missing RTSS or frame access is visible in settings and a one-time tray notification, with an installer link.
- Fix RTSS embedded graph addresses to use hexadecimal offsets as required by the vendor SDK; use a compact Consolas font through RTSS 7.3.7's font tag.
- Additional tests cover prerequisites, task action/ACL restrictions, native task XML validation without registration, and multiple graph references.
- Releases include Setup.exe, portable ZIP and SHA-256 for both.

## 0.2.1 — 2026-10-12

- Minimal technical tray menu with short actions, current overlay state and a monochrome telemetry icon.
- Compact 370 × 390 settings panel: dark square controls, Consolas, 23-pixel rows, paired sampling/history fields and live scale/opacity values.
- Help and sensor diagnostics collapse into one section; all existing settings remain available.
- UI regression checks cover editable process input, threshold validation, tray actions/state and visibility of every menu item.
- Preview export preserves the real settings dimensions and includes the native tray menu.

## 0.2.0 — 2026-10-12

- Transparent technical HUD, 316 × 144: numeric rows on the left and frame/load graphs on the right; no panel border, logo, game title or large FPS heading.
- Automatic fullscreen detection and routing to RTSS when available; Windows fallback and monitor-aware DPI positioning.
- Optional user-level Windows autostart with silent startup, quoted executable paths and isolated registry integration tests.
- Existing preferences migrate to automatic output and transparent background while retaining selected devices and sampling settings.
- Public GitHub releases with downloadable portable ZIP and SHA-256; 0.x releases are marked prerelease.
- Recover gracefully from non-object settings JSON.

## 0.1.0 — 2026-10-12

- CPU, GPU, RAM, VRAM and disk monitoring with available temperatures.
- PresentMon 2.6.0 FPS, frame time, rolling 1% low and stutter detection.
- Compact transparent WPF HUD and RTSS shared-memory output with embedded graphs.
- Tray settings, global visibility hotkey, selectable devices/process and bounded histories.
- xUnit tests, reproducible build scripts, portable self-contained Windows x64 release and initial GitHub workflow.

The RTSS in-game display needs manual validation in a supported game before a public stable release.

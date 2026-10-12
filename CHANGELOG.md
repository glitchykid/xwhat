# Changelog

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

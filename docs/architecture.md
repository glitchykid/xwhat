# Architecture and contracts

`XWhat.Core` has no Windows or UI dependency. It holds normalized settings, bounded monotonic sample histories, frame statistics, the CSV decoder, min/max graph envelopes, and RTSS wire serialization. `XWhat.App` hosts Windows acquisition, tray lifecycle, settings persistence and the two display backends. There is no web view, network service, database, or DI container.

Hardware acquisition runs on one background task. Only that task accesses LibreHardwareMonitor and PDH. Settings and the latest immutable hardware sample are exchanged atomically. The WPF dispatcher refreshes at 4 Hz; hardware polling is user-selectable from 250 to 2000 ms. All histories have fixed capacities (256 hardware samples per metric, 65536 frame samples), independent of run duration. The default hardware rate is 2 Hz.

PresentMon is a bundled, hash-verified external console process. Arguments use `ProcessStartInfo.ArgumentList`; shell execution is disabled. The application owns one named ETW session and terminates only that session. Stdout/stderr are continuously drained. A process switch resets frame history; swap chains are not mixed. The first active swap chain is used; after one second of inactivity a replacement chain may take over. Games simultaneously presenting through several swap chains may require additional target selection in a future release.

Time contracts: hardware samples use monotonic seconds. Frame duration uses PresentMon `MsBetweenPresents` (milliseconds), with `CPUFrameTime` compatibility fallback. Capture requests `--qpc_time_ms`; absolute `CPUStartQPCTime` is mapped onto the Stopwatch epoch, so buffered stdout does not distort frame spacing. Relative CPUStartTime is a compatibility fallback. Unknown data uses nullable values and NaN only inside chart histories. UI always displays unavailable data as `—`.

Frame statistics use the chosen rolling window. FPS = 1000 / mean frame time. 1% low = 1000 / mean of the slowest ceil(N × 0.01) samples. A stutter exceeds max(user threshold, 2 × median). After two seconds without frames, readings expire. The stutter count is a rolling count, not a lifetime count.

RTSS protocol accepts SDK shared-memory version 2.14+ with validated signature, offsets, array bounds, and entry stride. Slot zero is reserved; X What claims only a vacant slot or one matching its unique owner. The native adapter atomically holds dwBusy bit zero during every modification and releases it in finally. A graph contains the public SDK GR00 header and little-endian float data in the extended slot buffer. Downsampling uses per-column maxima for RTSS and min/max envelopes for WPF. RTSS appearance is controlled by RTSS.

Settings writes use a temporary file and replacement within the same directory. JSON is normalized before application; malformed files fall back to defaults. The app is single instance per user session. On exit, it removes its RTSS slot, stops its own PresentMon session, closes sensors, unregisters its hotkey and disposes the tray icon. A size-bounded diagnostic log records failures, never frame-by-frame telemetry.

Sensor reads do not modify clocks, fan control or voltages. The executable runs asInvoker; driver installation and elevation are user decisions. Package versions and the SDK are pinned, and GitHub release creation uses draft status.

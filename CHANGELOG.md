# Changelog

## 0.2.1 - 2026-08-17

- Fixed away-session durations longer than 24 hours wrapping back to `00:00:00` in saved reports.
- Added an automated release check that requires the Git tag, application version, and changelog section to match.
- Added regression coverage for multi-day report formatting.

## 0.2.0 - 2026-07-23

- Added explicit ready, lock-confirmation, monitoring, and report-generation states.
- Added a 15-second lock confirmation timeout that clears sleep prevention when locking is not confirmed.
- Replaced the thread execution-state flag with a reasoned Windows Power Request and deterministic cleanup.
- Added periodic process sampling while Windows is locked.
- Added detection for processes that start and finish entirely during an away session.
- Added persistent text report history under `%LocalAppData%\LockRun\Reports`.
- Added Japanese and English UI switching based on the Windows language or a saved preference.
- Added an in-app settings window for process names, sample interval, and report retention.
- Moved user overrides to `%LocalAppData%\LockRun\appsettings.json`.
- Grouped duplicate process names in the live view while retaining per-PID report tracking.
- Added graceful power-request cleanup during Windows shutdown and app exit.
- Added a modernized main-window layout and a clear primary action.
- Added MSTest coverage for settings normalization, transient process detection, PID reuse, sampling errors, and power-request cleanup.
- Added automated tag-based GitHub Releases with SHA-256 files.
- Added signed GitHub build-provenance attestations for release archives.
- Added a real application screenshot and simplified download-first documentation.

## 0.1.0 - Lock & Run

- Renamed the app from AI Away Guard to Lock & Run before public release.
- Added initial WPF tray app prototype.
- Added single-instance guard with the named Mutex `Local\LockRun.SingleInstance`.
- Improved second launch behavior through `Local\LockRun.ShowMainWindow` to show the existing Lock & Run window instead of opening another tray instance.
- Added Away Mode Start action.
- Added best-effort sleep prevention through Windows `SetThreadExecutionState`.
- Added real Windows lock through `LockWorkStation`.
- Added configurable monitored process list in `appsettings.json`.
- Added process list display and return report.
- Added default monitored process names for common AI, editor, shell, and build workflows.
- Added before/after monitored process difference reporting for still-running, ended, and newly matching processes.
- Added CPU time delta reporting for monitored processes.
- Changed top CPU time delta display to decimal seconds.
- Added Lock & Run's own memory and CPU delta reporting.
- Added Claude and ClaudeCowork to the default monitored process candidates.
- Added GitHub Actions build workflow.
- Added GitHub Actions artifact packaging as `LockRun-v0.1.0-win-x64`.
- Added CI artifact sanity checks for `LockRun.exe` and `appsettings.json`.
- Hardened cleanup so sleep prevention is disabled after lock failures, unlock handling, and normal app exit.

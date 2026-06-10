# Changelog

## 0.1.0

- Added initial WPF tray app prototype.
- Added single-instance guard with a named Mutex.
- Added Away Mode Start action.
- Added best-effort sleep prevention through Windows `SetThreadExecutionState`.
- Added real Windows lock through `LockWorkStation`.
- Added configurable monitored process list in `appsettings.json`.
- Added process list display and return report.
- Added default monitored process names for common AI, editor, shell, and build workflows.
- Added before/after monitored process difference reporting for still-running, ended, and newly matching processes.
- Added CPU time delta reporting for monitored processes.
- Changed top CPU time delta display to decimal seconds.
- Added AI Away Guard's own memory and CPU delta reporting.
- Added Claude and ClaudeCowork to the default monitored process candidates.
- Added GitHub Actions build workflow.
- Added GitHub Actions artifact packaging as `AIAwayGuard-v0.1.0-win-x64`.
- Added CI artifact sanity checks for `AIAwayGuard.exe` and `appsettings.json`.
- Hardened cleanup so sleep prevention is disabled after lock failures, unlock handling, and normal app exit.

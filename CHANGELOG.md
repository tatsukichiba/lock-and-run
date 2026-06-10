# Changelog

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

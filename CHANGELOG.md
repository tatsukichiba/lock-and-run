# Changelog

## 0.1.0

- Added initial WPF tray app prototype.
- Added Away Mode Start action.
- Added best-effort sleep prevention through Windows `SetThreadExecutionState`.
- Added real Windows lock through `LockWorkStation`.
- Added configurable monitored process list in `appsettings.json`.
- Added process list display and return report.
- Hardened cleanup so sleep prevention is disabled after lock failures, unlock handling, and normal app exit.

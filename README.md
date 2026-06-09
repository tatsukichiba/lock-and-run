# AI Away Guard

AI Away Guard is a small open-source Windows 11 tray app for people who leave long-running local AI and build jobs running while they step away.

It turns on best-effort Windows sleep prevention, starts a lightweight process snapshot, and then calls the real Windows `LockWorkStation` API.

## What it does

- Runs as a Windows tray app.
- Provides an **Away Mode Start** action.
- Enables sleep prevention with the Windows `SetThreadExecutionState` API.
- Locks the workstation with the Windows `LockWorkStation` API.
- Shows currently running monitored processes.
- Reads monitored process names from `appsettings.json`.
- Shows a simple report after you unlock Windows and return.
- Estimates background activity from process survival and CPU time differences, not from direct AI task progress.

## What it does not do

- This app is not a fake lock screen.
- It does not implement a custom password screen.
- It uses the standard Windows lock screen.
- Security depends on Windows authentication.
- It is not a recommendation to leave your PC unattended in public places.
- Sleep prevention is best effort and can still be overridden by Windows policy, battery settings, forced shutdowns, updates, or hardware power events.
- CPU time reporting is also best effort; inaccessible or exited processes are shown without CPU time comparison.
- AI Away Guard disables sleep prevention after the session unlock event or when the app exits normally.
- For important work, verify the behavior yourself on your own machine.

## Default monitored process names

- `codex`
- `code`
- `cursor`
- `ollama`
- `python`
- `node`
- `git`
- `powershell`
- `cmd`
- `wsl`
- `Unity`

## Configure monitored processes

Edit `src/AIAwayGuard/appsettings.json`:

```json
{
  "monitoredProcessNames": [
    "codex",
    "ollama",
    "python",
    "node"
  ]
}
```

The file is copied next to the built application. You can also edit the deployed `appsettings.json` after publishing.

## Build

Requirements:

- Windows 11
- .NET 8 SDK or newer

```powershell
dotnet build
```

## Run

```powershell
dotnet run --project src/AIAwayGuard/AIAwayGuard.csproj
```

Use **Away Mode Start** from the main window or tray menu. Windows should lock immediately. When you unlock and return, AI Away Guard disables sleep prevention and displays a short report.

## License

MIT

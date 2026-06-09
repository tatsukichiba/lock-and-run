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
- Estimates background activity from process survival and CPU time differences. It does not guarantee AI processing progress itself.
- Compares returning processes by PID and, when available, process start time to reduce PID reuse false positives.
- Reports AI Away Guard's own memory and CPU time deltas, which can help spot app memory growth while away mode is active.

## What it does not do

- This app is not a fake lock screen.
- It does not implement a custom password screen.
- It uses the standard Windows lock screen.
- Security depends on Windows authentication.
- It is not a recommendation to leave your PC unattended in public places.
- Sleep prevention is best effort and can still be overridden by Windows policy, battery settings, forced shutdowns, updates, or hardware power events.
- CPU time reporting is also best effort; inaccessible or exited processes are shown without CPU time comparison.
- The report estimates activity from process survival and CPU time differences; it does not guarantee AI processing progress itself.
- App memory reporting is a lightweight WorkingSet64 snapshot and can be used as an early signal, not as a full memory profiler.
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

## Download GitHub Actions artifact

PR and branch builds publish a Windows x64 executable zip as a GitHub Actions artifact.

1. Open the latest `.NET Build` workflow run in GitHub Actions.
2. Download the `AIAwayGuard-v0.1.0-win-x64` artifact.
3. Extract `AIAwayGuard-v0.1.0-win-x64.zip`.
4. Run `AIAwayGuard.exe` from the extracted folder.

The artifact structure is validated in CI by extracting the zip, checking for `AIAwayGuard.exe` and `appsettings.json`, confirming the executable is non-empty, and parsing `appsettings.json` as JSON. Actual Windows Lock/Unlock behavior must still be verified on a Windows 11 machine.

## Publish locally

```powershell
dotnet publish src/AIAwayGuard/AIAwayGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/AIAwayGuard-v0.1.0-win-x64
Compress-Archive -Path publish/AIAwayGuard-v0.1.0-win-x64/* -DestinationPath AIAwayGuard-v0.1.0-win-x64.zip -Force
```

## Run

```powershell
dotnet run --project src/AIAwayGuard/AIAwayGuard.csproj
```

Use **Away Mode Start** from the main window or tray menu. Windows should lock immediately. When you unlock and return, AI Away Guard disables sleep prevention and displays a short report.

## License

MIT

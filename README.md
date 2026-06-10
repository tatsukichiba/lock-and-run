# AI Away Guard

AI Away Guard is a small open-source Windows 11 tray app for people who leave long-running local AI and build jobs running while they step away.

It turns on best-effort Windows sleep prevention, starts a lightweight process snapshot, and then calls the real Windows `LockWorkStation` API.

## Main value

- Windows session lock through the standard Windows lock screen.
- Best-effort sleep prevention while away mode is active.
- After-unlock activity report for monitored processes and AI Away Guard itself.

Compared with pressing `Win + L`, AI Away Guard's main added value is best-effort sleep prevention plus the after-unlock away report. It does not replace or strengthen Windows authentication.

## What it does

- Runs as a Windows tray app.
- Prevents multiple simultaneous app instances.
- Provides an **Away Mode Start** action.
- Enables sleep prevention with the Windows `SetThreadExecutionState` API.
- Locks the workstation with the Windows `LockWorkStation` API.
- Shows currently running monitored processes.
- Reads monitored process names from `appsettings.json`.
- Shows a simple report after you unlock Windows and return.
- Estimates background activity from process survival, CPU time deltas, and AI Away Guard's own memory/CPU deltas. These signals do not guarantee that any AI processing finished successfully.
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
- The report estimates activity from process survival, CPU time deltas, and app memory/CPU deltas; it does not guarantee AI processing completion or correctness.
- App memory reporting is a lightweight WorkingSet64 snapshot and can be used as an activity or growth signal, not as a full memory profiler.
- AI Away Guard disables sleep prevention after the session unlock event or when the app exits normally.
- The executable is unsigned, so Windows SmartScreen may show a warning before launch.
- CI verifies build and packaging only; real lock, unlock, and sleep-prevention behavior must still be checked on an actual Windows 11 machine.
- For important work, verify the behavior yourself on your own machine.

## Default monitored process names

- `codex`
- `code`
- `cursor`
- `Claude`
- `ClaudeCowork`
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
    "Claude",
    "ClaudeCowork",
    "ollama",
    "python",
    "node"
  ]
}
```

The file is copied next to the built application. You can also edit the deployed `appsettings.json` after publishing.

If a process does not appear in AI Away Guard, check the actual Windows process name with PowerShell and add that `ProcessName` value to `appsettings.json`:

```powershell
Get-Process | Sort-Object ProcessName | Select-Object ProcessName, Id, MainWindowTitle
```

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
3. If GitHub downloads an artifact wrapper zip, extract it first.
4. Extract `AIAwayGuard-v0.1.0-win-x64.zip`.
5. Run `AIAwayGuard.exe` from the extracted folder. Keep `appsettings.json` next to the executable.

The artifact structure is validated in CI by extracting the zip, checking for `AIAwayGuard.exe` and `appsettings.json`, confirming the executable is non-empty, and parsing `appsettings.json` as JSON. Actual Windows Lock/Unlock behavior must still be verified on a real Windows 11 machine.

## Publish locally

```powershell
dotnet publish src/AIAwayGuard/AIAwayGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/AIAwayGuard-v0.1.0-win-x64
Compress-Archive -Path publish/AIAwayGuard-v0.1.0-win-x64/* -DestinationPath AIAwayGuard-v0.1.0-win-x64.zip -Force
```

To test the local publish output, either run `publish/AIAwayGuard-v0.1.0-win-x64/AIAwayGuard.exe` directly or extract `AIAwayGuard-v0.1.0-win-x64.zip` and run `AIAwayGuard.exe` from the extracted folder.

## Run

```powershell
dotnet run --project src/AIAwayGuard/AIAwayGuard.csproj
```

Use **Away Mode Start** from the main window or tray menu. AI Away Guard snapshots the monitored processes and its own resource usage, enables best-effort sleep prevention, and then calls the real Windows `LockWorkStation` API. When you unlock and return, AI Away Guard disables sleep prevention and displays a short activity-estimation report.

AI Away Guard uses a named Mutex to prevent multiple simultaneous instances. If it is already running, a second launch requests the existing instance to show and activate its main window, then exits without creating another tray icon.

## Roadmap

- English / Japanese language switching
- Startup registration
- Persistent report log
- Advanced process filters by executable path / window title
- File/log growth checks
- Exit code / process completion tracking
- Optional GPU activity reporting

## License

MIT

namespace AIAwayGuard.Models;

public sealed record MonitoredProcessInfo(
    string Name,
    int ProcessId,
    DateTimeOffset? StartedAt,
    string MainWindowTitle);

namespace AIAwayGuard.Models;

public sealed record MonitoredProcessInfo(
    string Name,
    int ProcessId,
    DateTimeOffset? StartedAt,
    TimeSpan? TotalProcessorTime,
    string MainWindowTitle);

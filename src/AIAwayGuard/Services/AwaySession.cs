using AIAwayGuard.Models;

namespace AIAwayGuard.Services;

public sealed class AwaySession(DateTimeOffset startedAt, IReadOnlyList<MonitoredProcessInfo> startProcesses)
{
    public DateTimeOffset StartedAt { get; } = startedAt;

    public IReadOnlyList<MonitoredProcessInfo> StartProcesses { get; } = startProcesses;
}

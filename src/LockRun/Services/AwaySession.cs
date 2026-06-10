using LockRun.Models;

namespace LockRun.Services;

public sealed class AwaySession(
    DateTimeOffset startedAt,
    IReadOnlyList<MonitoredProcessInfo> startProcesses,
    AppResourceSnapshot appResourceAtStart)
{
    public DateTimeOffset StartedAt { get; } = startedAt;

    public IReadOnlyList<MonitoredProcessInfo> StartProcesses { get; } = startProcesses;

    public AppResourceSnapshot AppResourceAtStart { get; } = appResourceAtStart;
}

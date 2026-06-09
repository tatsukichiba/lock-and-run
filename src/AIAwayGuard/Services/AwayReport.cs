using AIAwayGuard.Models;
using System.Text;

namespace AIAwayGuard.Services;

public sealed class AwayReport
{
    private AwayReport(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        IReadOnlyList<MonitoredProcessInfo> startProcesses,
        IReadOnlyList<MonitoredProcessInfo> endProcesses)
    {
        StartedAt = startedAt;
        EndedAt = endedAt;
        StartProcesses = startProcesses;
        EndProcesses = endProcesses;
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public TimeSpan Duration => EndedAt - StartedAt;

    public IReadOnlyList<MonitoredProcessInfo> StartProcesses { get; }

    public IReadOnlyList<MonitoredProcessInfo> EndProcesses { get; }

    public int StillRunningCount => StartProcesses.Count(start =>
        FindMatchingEndProcess(start) is not null);

    public int EndedCount => StartProcesses.Count - StillRunningCount;

    public int NewCount => EndProcesses.Count(end =>
        StartProcesses.All(start => !IsSameProcessInstance(start, end)));

    public int CpuTimeIncreasedCount => StartProcesses.Count(start =>
    {
        return TryGetCpuTimeDelta(start, out var delta) && delta > TimeSpan.Zero;
    });

    public IReadOnlyList<CpuTimeDelta> TopCpuTimeDeltas => StartProcesses
        .Select(start =>
        {
            return TryGetCpuTimeDelta(start, out var delta) && delta > TimeSpan.Zero
                ? new CpuTimeDelta(start.Name, start.ProcessId, delta)
                : null;
        })
        .OfType<CpuTimeDelta>()
        .OrderByDescending(delta => delta.Delta)
        .Take(5)
        .ToList();

    public static AwayReport Create(AwaySession session, IReadOnlyList<MonitoredProcessInfo> endProcesses)
    {
        return new AwayReport(session.StartedAt, DateTimeOffset.Now, session.StartProcesses, endProcesses);
    }

    public string ToSummary()
    {
        var summary = new StringBuilder($"""
            Away session report
            Started: {StartedAt:yyyy-MM-dd HH:mm:ss}
            Returned: {EndedAt:yyyy-MM-dd HH:mm:ss}
            Duration: {Duration:hh\:mm\:ss}

            Monitored processes at start: {StartProcesses.Count}
            Still running: {StillRunningCount}
            Ended during away: {EndedCount}
            New matching processes: {NewCount}
            CPU time increased: {CpuTimeIncreasedCount}
            """);

        var topCpuTimeDeltas = TopCpuTimeDeltas;
        if (topCpuTimeDeltas.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine();
            summary.AppendLine("Top CPU time increases:");
            foreach (var delta in topCpuTimeDeltas)
            {
                summary.AppendLine($"- {delta.Name} (PID {delta.ProcessId}): +{delta.Delta:hh\\:mm\\:ss}");
            }
        }

        return summary.ToString();
    }

    private MonitoredProcessInfo? FindMatchingEndProcess(MonitoredProcessInfo start)
    {
        return EndProcesses.FirstOrDefault(end => IsSameProcessInstance(start, end));
    }

    private static bool IsSameProcessInstance(MonitoredProcessInfo start, MonitoredProcessInfo end)
    {
        if (start.ProcessId != end.ProcessId)
        {
            return false;
        }

        if (start.StartedAt is not null &&
            end.StartedAt is not null &&
            start.StartedAt != end.StartedAt)
        {
            return false;
        }

        return true;
    }

    private bool TryGetCpuTimeDelta(MonitoredProcessInfo start, out TimeSpan delta)
    {
        delta = TimeSpan.Zero;

        if (start.TotalProcessorTime is null)
        {
            return false;
        }

        var end = FindMatchingEndProcess(start);
        if (end?.TotalProcessorTime is null)
        {
            return false;
        }

        delta = end.TotalProcessorTime.Value - start.TotalProcessorTime.Value;
        return true;
    }

    public sealed record CpuTimeDelta(string Name, int ProcessId, TimeSpan Delta);
}

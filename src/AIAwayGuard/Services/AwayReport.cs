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

    public int StillRunningCount => StartProcesses.Count(process =>
        EndProcesses.Any(current => current.ProcessId == process.ProcessId));

    public int EndedCount => StartProcesses.Count - StillRunningCount;

    public int NewCount => EndProcesses.Count(process =>
        StartProcesses.All(started => started.ProcessId != process.ProcessId));

    public int CpuTimeIncreasedCount => StartProcesses.Count(start =>
    {
        if (start.TotalProcessorTime is null)
        {
            return false;
        }

        var end = EndProcesses.FirstOrDefault(current => current.ProcessId == start.ProcessId);
        return end?.TotalProcessorTime is not null &&
            end.TotalProcessorTime > start.TotalProcessorTime;
    });

    public IReadOnlyList<CpuTimeDelta> TopCpuTimeDeltas => StartProcesses
        .Select(start =>
        {
            if (start.TotalProcessorTime is null)
            {
                return null;
            }

            var end = EndProcesses.FirstOrDefault(current => current.ProcessId == start.ProcessId);
            if (end?.TotalProcessorTime is null)
            {
                return null;
            }

            var delta = end.TotalProcessorTime.Value - start.TotalProcessorTime.Value;
            return delta > TimeSpan.Zero
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

    public sealed record CpuTimeDelta(string Name, int ProcessId, TimeSpan Delta);
}

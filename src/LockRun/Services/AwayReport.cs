using LockRun.Models;
using System.Globalization;
using System.Text;

namespace LockRun.Services;

public sealed class AwayReport
{
    private AwayReport(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        IReadOnlyList<MonitoredProcessInfo> startProcesses,
        IReadOnlyList<MonitoredProcessInfo> endProcesses,
        AppResourceSnapshot appResourceAtStart,
        AppResourceSnapshot appResourceAtReturn)
    {
        StartedAt = startedAt;
        EndedAt = endedAt;
        StartProcesses = startProcesses;
        EndProcesses = endProcesses;
        AppResourceAtStart = appResourceAtStart;
        AppResourceAtReturn = appResourceAtReturn;
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public TimeSpan Duration => EndedAt - StartedAt;

    public IReadOnlyList<MonitoredProcessInfo> StartProcesses { get; }

    public IReadOnlyList<MonitoredProcessInfo> EndProcesses { get; }

    public AppResourceSnapshot AppResourceAtStart { get; }

    public AppResourceSnapshot AppResourceAtReturn { get; }

    public long AppMemoryDeltaBytes => AppResourceAtReturn.WorkingSetBytes - AppResourceAtStart.WorkingSetBytes;

    public TimeSpan AppCpuTimeDelta => AppResourceAtReturn.TotalProcessorTime - AppResourceAtStart.TotalProcessorTime;

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

    public static AwayReport Create(
        AwaySession session,
        IReadOnlyList<MonitoredProcessInfo> endProcesses,
        AppResourceSnapshot appResourceAtReturn)
    {
        return new AwayReport(
            session.StartedAt,
            DateTimeOffset.Now,
            session.StartProcesses,
            endProcesses,
            session.AppResourceAtStart,
            appResourceAtReturn);
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

            App memory at start: {ToMegabytes(AppResourceAtStart.WorkingSetBytes):F1} MB
            App memory at return: {ToMegabytes(AppResourceAtReturn.WorkingSetBytes):F1} MB
            App memory delta: {ToMegabytes(AppMemoryDeltaBytes):+0.0;-0.0;0.0} MB
            App CPU time delta: {AppCpuTimeDelta.TotalSeconds:F1} seconds
            """);

        var topCpuTimeDeltas = TopCpuTimeDeltas;
        if (topCpuTimeDeltas.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine();
            summary.AppendLine("Top CPU time increases:");
            foreach (var delta in topCpuTimeDeltas)
            {
                summary.AppendLine($"- {delta.Name} (PID {delta.ProcessId}): {FormatCpuTimeDelta(delta.Delta)}");
            }
        }

        return summary.ToString();
    }

    private static string FormatCpuTimeDelta(TimeSpan delta)
    {
        return $"+{delta.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)} sec";
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

    private static double ToMegabytes(long bytes)
    {
        return bytes / 1024d / 1024d;
    }

    public sealed record CpuTimeDelta(string Name, int ProcessId, TimeSpan Delta);
}

using System.Globalization;
using System.Text;
using LockRun.Models;

namespace LockRun.Services;

public sealed class AwayReport
{
    private AwayReport(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        DateTimeOffset? lockConfirmedAt,
        IReadOnlyList<MonitoredProcessInfo> startProcesses,
        IReadOnlyList<MonitoredProcessInfo> endProcesses,
        IReadOnlyList<AwaySample> samples,
        int samplingErrorCount,
        AppResourceSnapshot appResourceAtStart,
        AppResourceSnapshot appResourceAtReturn)
    {
        StartedAt = startedAt;
        EndedAt = endedAt;
        LockConfirmedAt = lockConfirmedAt;
        StartProcesses = [.. startProcesses];
        EndProcesses = [.. endProcesses];
        Samples = [.. samples];
        SamplingErrorCount = samplingErrorCount;
        AppResourceAtStart = appResourceAtStart;
        AppResourceAtReturn = appResourceAtReturn;
        ProcessActivities = BuildProcessActivities(Samples, StartProcesses, EndProcesses);
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    public DateTimeOffset? LockConfirmedAt { get; }

    public TimeSpan Duration => EndedAt - StartedAt;

    public IReadOnlyList<MonitoredProcessInfo> StartProcesses { get; }

    public IReadOnlyList<MonitoredProcessInfo> EndProcesses { get; }

    public IReadOnlyList<AwaySample> Samples { get; }

    public int SamplingErrorCount { get; }

    public AppResourceSnapshot AppResourceAtStart { get; }

    public AppResourceSnapshot AppResourceAtReturn { get; }

    public IReadOnlyList<ProcessActivity> ProcessActivities { get; }

    public long AppMemoryDeltaBytes =>
        AppResourceAtReturn.WorkingSetBytes - AppResourceAtStart.WorkingSetBytes;

    public TimeSpan AppCpuTimeDelta =>
        AppResourceAtReturn.TotalProcessorTime - AppResourceAtStart.TotalProcessorTime;

    public int StillRunningCount => StartProcesses.Count(start =>
        EndProcesses.Any(end => IsSameProcessInstance(start, end)));

    public int EndedCount => StartProcesses.Count - StillRunningCount;

    public int NewCount => ProcessActivities.Count(static activity =>
        !activity.WasRunningAtStart);

    public int EndedDuringAwayCount => ProcessActivities.Count(static activity =>
        !activity.WasRunningAtEnd);

    public int ObservedProcessCount => ProcessActivities.Count;

    public int CpuTimeIncreasedCount => ProcessActivities.Count(static activity =>
        activity.CpuTimeDelta > TimeSpan.Zero);

    public IReadOnlyList<ProcessActivity> TopCpuTimeDeltas => ProcessActivities
        .Where(static activity => activity.CpuTimeDelta > TimeSpan.Zero)
        .OrderByDescending(static activity => activity.CpuTimeDelta)
        .Take(5)
        .ToList();

    public static AwayReport Create(
        AwaySession session,
        IReadOnlyList<MonitoredProcessInfo> endProcesses,
        AppResourceSnapshot appResourceAtReturn,
        DateTimeOffset? endedAt = null)
    {
        var actualEndedAt = endedAt ?? DateTimeOffset.Now;
        var samples = session.Samples.ToList();
        samples.Add(new AwaySample(actualEndedAt, [.. endProcesses]));

        return new AwayReport(
            session.StartedAt,
            actualEndedAt,
            session.LockConfirmedAt,
            session.StartProcesses,
            endProcesses,
            samples,
            session.SamplingErrorCount,
            session.AppResourceAtStart,
            appResourceAtReturn);
    }

    public string ToSummary(bool japanese)
    {
        return japanese ? ToJapaneseSummary() : ToEnglishSummary();
    }

    private string ToJapaneseSummary()
    {
        var summary = new StringBuilder($"""
            離席セッションレポート
            開始: {StartedAt:yyyy-MM-dd HH:mm:ss}
            ロック確認: {FormatLockConfirmedAt("未確認")}
            復帰: {EndedAt:yyyy-MM-dd HH:mm:ss}
            経過時間: {FormatDuration(Duration, true)}
            サンプル数: {Samples.Count}

            開始時の監視対象: {StartProcesses.Count}
            離席中に観測: {ObservedProcessCount}
            開始時から継続: {StillRunningCount}
            開始後に終了: {EndedCount}
            離席中に新規検出: {NewCount}
            復帰時には終了済み: {EndedDuringAwayCount}
            CPU時間の増加あり: {CpuTimeIncreasedCount}
            サンプリングエラー: {SamplingErrorCount}

            アプリメモリ（開始）: {ToMegabytes(AppResourceAtStart.WorkingSetBytes):F1} MB
            アプリメモリ（復帰）: {ToMegabytes(AppResourceAtReturn.WorkingSetBytes):F1} MB
            アプリメモリ差分: {ToMegabytes(AppMemoryDeltaBytes):+0.0;-0.0;0.0} MB
            アプリCPU時間差分: {AppCpuTimeDelta.TotalSeconds:F1} 秒
            """);

        AppendActivityDetails(summary, true);
        return summary.ToString();
    }

    private string ToEnglishSummary()
    {
        var summary = new StringBuilder($"""
            Away session report
            Started: {StartedAt:yyyy-MM-dd HH:mm:ss}
            Lock confirmed: {FormatLockConfirmedAt("Not confirmed")}
            Returned: {EndedAt:yyyy-MM-dd HH:mm:ss}
            Duration: {FormatDuration(Duration, false)}
            Samples: {Samples.Count}

            Monitored at start: {StartProcesses.Count}
            Observed while away: {ObservedProcessCount}
            Still running from start: {StillRunningCount}
            Ended after start: {EndedCount}
            Newly observed while away: {NewCount}
            No longer running at return: {EndedDuringAwayCount}
            CPU time increased: {CpuTimeIncreasedCount}
            Sampling errors: {SamplingErrorCount}

            App memory at start: {ToMegabytes(AppResourceAtStart.WorkingSetBytes):F1} MB
            App memory at return: {ToMegabytes(AppResourceAtReturn.WorkingSetBytes):F1} MB
            App memory delta: {ToMegabytes(AppMemoryDeltaBytes):+0.0;-0.0;0.0} MB
            App CPU time delta: {AppCpuTimeDelta.TotalSeconds:F1} seconds
            """);

        AppendActivityDetails(summary, false);
        return summary.ToString();
    }

    private void AppendActivityDetails(StringBuilder summary, bool japanese)
    {
        var topCpuTimeDeltas = TopCpuTimeDeltas;
        if (topCpuTimeDeltas.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine();
            summary.AppendLine(japanese ? "CPU時間増加 上位:" : "Top CPU time increases:");
            foreach (var activity in topCpuTimeDeltas)
            {
                summary.AppendLine(
                    $"- {activity.Name} (PID {activity.ProcessId}): {FormatCpuTimeDelta(activity.CpuTimeDelta!.Value)}");
            }
        }

        var changes = ProcessActivities
            .Where(static activity =>
                !activity.WasRunningAtStart || !activity.WasRunningAtEnd)
            .OrderBy(static activity => activity.FirstSeenAt)
            .ThenBy(static activity => activity.Name, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (changes.Count == 0)
        {
            return;
        }

        summary.AppendLine();
        summary.AppendLine(japanese ? "検出した変化:" : "Observed changes:");
        foreach (var activity in changes)
        {
            var state = GetActivityState(activity, japanese);
            summary.AppendLine(
                $"- {activity.Name} (PID {activity.ProcessId}): {state}, " +
                $"{activity.FirstSeenAt:HH:mm:ss} - {activity.LastSeenAt:HH:mm:ss}");
        }
    }

    private string FormatLockConfirmedAt(string unavailableText)
    {
        return LockConfirmedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? unavailableText;
    }

    private static string GetActivityState(ProcessActivity activity, bool japanese)
    {
        if (!activity.WasRunningAtStart && activity.WasRunningAtEnd)
        {
            return japanese ? "離席中に開始・復帰時も稼働" : "started while away and still running";
        }

        if (!activity.WasRunningAtStart)
        {
            return japanese ? "離席中に開始・終了" : "started and ended while away";
        }

        return japanese ? "離席中に終了" : "ended while away";
    }

    private static IReadOnlyList<ProcessActivity> BuildProcessActivities(
        IReadOnlyList<AwaySample> samples,
        IReadOnlyList<MonitoredProcessInfo> startProcesses,
        IReadOnlyList<MonitoredProcessInfo> endProcesses)
    {
        var builders = new Dictionary<ProcessIdentity, ProcessActivityBuilder>();

        foreach (var sample in samples)
        {
            foreach (var process in sample.Processes)
            {
                var identity = ProcessIdentity.From(process);
                if (!builders.TryGetValue(identity, out var builder))
                {
                    builder = new ProcessActivityBuilder(process, sample.CapturedAt);
                    builders.Add(identity, builder);
                }

                builder.Observe(process, sample.CapturedAt);
            }
        }

        return builders.Values
            .Select(builder => builder.Build(
                startProcesses.Any(start => IsSameProcessInstance(start, builder.Process)),
                endProcesses.Any(end => IsSameProcessInstance(end, builder.Process))))
            .OrderBy(static activity => activity.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static activity => activity.ProcessId)
            .ToList();
    }

    private static bool IsSameProcessInstance(
        MonitoredProcessInfo first,
        MonitoredProcessInfo second)
    {
        if (first.ProcessId != second.ProcessId ||
            !string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (first.StartedAt is not null &&
            second.StartedAt is not null &&
            first.StartedAt != second.StartedAt)
        {
            return false;
        }

        return true;
    }

    private static string FormatCpuTimeDelta(TimeSpan delta)
    {
        return $"+{delta.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)} sec";
    }

    private static string FormatDuration(TimeSpan duration, bool japanese)
    {
        var sign = duration < TimeSpan.Zero ? "-" : string.Empty;
        var absoluteDuration = duration.Duration();
        var clock = $"{absoluteDuration.Hours:00}:{absoluteDuration.Minutes:00}:{absoluteDuration.Seconds:00}";

        if (absoluteDuration.Days == 0)
        {
            return sign + clock;
        }

        var dayLabel = japanese ? "日" : "d";
        return $"{sign}{absoluteDuration.Days}{dayLabel} {clock}";
    }

    private static double ToMegabytes(long bytes)
    {
        return bytes / 1024d / 1024d;
    }

    private sealed record ProcessIdentity(
        string NormalizedName,
        int ProcessId,
        long? StartTimeUtcTicks)
    {
        public static ProcessIdentity From(MonitoredProcessInfo process)
        {
            return new ProcessIdentity(
                process.Name.ToUpperInvariant(),
                process.ProcessId,
                process.StartedAt?.UtcDateTime.Ticks);
        }
    }

    private sealed class ProcessActivityBuilder
    {
        private TimeSpan? _firstCpuTime;
        private TimeSpan? _lastCpuTime;

        public ProcessActivityBuilder(MonitoredProcessInfo process, DateTimeOffset observedAt)
        {
            Process = process;
            FirstSeenAt = observedAt;
            LastSeenAt = observedAt;
        }

        public MonitoredProcessInfo Process { get; }

        public DateTimeOffset FirstSeenAt { get; private set; }

        public DateTimeOffset LastSeenAt { get; private set; }

        public void Observe(MonitoredProcessInfo process, DateTimeOffset observedAt)
        {
            if (observedAt < FirstSeenAt)
            {
                FirstSeenAt = observedAt;
            }

            if (observedAt > LastSeenAt)
            {
                LastSeenAt = observedAt;
            }

            if (process.TotalProcessorTime is not null)
            {
                _firstCpuTime ??= process.TotalProcessorTime;
                _lastCpuTime = process.TotalProcessorTime;
            }
        }

        public ProcessActivity Build(bool wasRunningAtStart, bool wasRunningAtEnd)
        {
            TimeSpan? cpuTimeDelta = _firstCpuTime is not null && _lastCpuTime is not null
                ? _lastCpuTime.Value - _firstCpuTime.Value
                : null;

            return new ProcessActivity(
                Process.Name,
                Process.ProcessId,
                FirstSeenAt,
                LastSeenAt,
                wasRunningAtStart,
                wasRunningAtEnd,
                cpuTimeDelta);
        }
    }

    public sealed record ProcessActivity(
        string Name,
        int ProcessId,
        DateTimeOffset FirstSeenAt,
        DateTimeOffset LastSeenAt,
        bool WasRunningAtStart,
        bool WasRunningAtEnd,
        TimeSpan? CpuTimeDelta);
}

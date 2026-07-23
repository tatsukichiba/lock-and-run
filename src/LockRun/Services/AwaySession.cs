using LockRun.Models;

namespace LockRun.Services;

public sealed class AwaySession
{
    private readonly List<AwaySample> _samples = [];

    public AwaySession(
        DateTimeOffset startedAt,
        IReadOnlyList<MonitoredProcessInfo> startProcesses,
        AppResourceSnapshot appResourceAtStart)
    {
        StartedAt = startedAt;
        StartProcesses = [.. startProcesses];
        AppResourceAtStart = appResourceAtStart;
        AddSample(startedAt, startProcesses);
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? LockConfirmedAt { get; private set; }

    public IReadOnlyList<MonitoredProcessInfo> StartProcesses { get; }

    public AppResourceSnapshot AppResourceAtStart { get; }

    public IReadOnlyList<AwaySample> Samples => _samples;

    public int SamplingErrorCount { get; private set; }

    public void MarkLockConfirmed(DateTimeOffset confirmedAt)
    {
        LockConfirmedAt ??= confirmedAt;
    }

    public void AddSample(
        DateTimeOffset capturedAt,
        IReadOnlyList<MonitoredProcessInfo> processes)
    {
        _samples.Add(new AwaySample(capturedAt, [.. processes]));
    }

    public void RegisterSamplingError()
    {
        SamplingErrorCount++;
    }
}

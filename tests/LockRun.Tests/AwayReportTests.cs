using LockRun.Models;
using LockRun.Services;

namespace LockRun.Tests;

[TestClass]
public sealed class AwayReportTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 23, 10, 0, 0, TimeSpan.FromHours(9));

    [TestMethod]
    public void Create_ReportsProcessesThatStartedAndEndedBetweenUnlocks()
    {
        var codeAtStart = Process("code", 10, StartedAt.AddMinutes(-5), 10);
        var pythonAtStart = Process("python", 20, StartedAt.AddMinutes(-3), 5);
        var session = new AwaySession(
            StartedAt,
            [codeAtStart, pythonAtStart],
            new AppResourceSnapshot(100 * 1024 * 1024, TimeSpan.FromSeconds(1)));
        session.MarkLockConfirmed(StartedAt.AddSeconds(1));
        session.AddSample(
            StartedAt.AddSeconds(5),
            [Process("code", 10, codeAtStart.StartedAt, 12), Process("node", 30, StartedAt.AddSeconds(2), 1)]);
        session.AddSample(
            StartedAt.AddSeconds(10),
            [Process("code", 10, codeAtStart.StartedAt, 13)]);

        var report = AwayReport.Create(
            session,
            [Process("code", 10, codeAtStart.StartedAt, 14), Process("git", 40, StartedAt.AddSeconds(12), 0.5)],
            new AppResourceSnapshot(110 * 1024 * 1024, TimeSpan.FromSeconds(2)),
            StartedAt.AddSeconds(15));

        Assert.HasCount(4, report.Samples);
        Assert.AreEqual(4, report.ObservedProcessCount);
        Assert.AreEqual(1, report.StillRunningCount);
        Assert.AreEqual(1, report.EndedCount);
        Assert.AreEqual(2, report.NewCount);
        Assert.AreEqual(2, report.EndedDuringAwayCount);
        Assert.AreEqual(1, report.CpuTimeIncreasedCount);
        Assert.AreEqual(TimeSpan.FromSeconds(4), report.TopCpuTimeDeltas.Single().CpuTimeDelta);
        StringAssert.Contains(report.ToSummary(true), "離席中に開始・終了");
    }

    [TestMethod]
    public void Create_DoesNotTreatPidReuseAsTheSameProcess()
    {
        var originalStartTime = StartedAt.AddMinutes(-10);
        var replacementStartTime = StartedAt.AddSeconds(5);
        var session = new AwaySession(
            StartedAt,
            [Process("python", 99, originalStartTime, 3)],
            new AppResourceSnapshot(1, TimeSpan.Zero));
        session.MarkLockConfirmed(StartedAt.AddSeconds(1));

        var report = AwayReport.Create(
            session,
            [Process("python", 99, replacementStartTime, 1)],
            new AppResourceSnapshot(1, TimeSpan.Zero),
            StartedAt.AddSeconds(10));

        Assert.AreEqual(0, report.StillRunningCount);
        Assert.AreEqual(1, report.EndedCount);
        Assert.AreEqual(1, report.NewCount);
        Assert.AreEqual(2, report.ObservedProcessCount);
    }

    [TestMethod]
    public void Create_IncludesSamplingErrorsInSummary()
    {
        var session = new AwaySession(
            StartedAt,
            [],
            new AppResourceSnapshot(1, TimeSpan.Zero));
        session.MarkLockConfirmed(StartedAt.AddSeconds(1));
        session.RegisterSamplingError();

        var report = AwayReport.Create(
            session,
            [],
            new AppResourceSnapshot(1, TimeSpan.Zero),
            StartedAt.AddSeconds(5));

        Assert.AreEqual(1, report.SamplingErrorCount);
        StringAssert.Contains(report.ToSummary(false), "Sampling errors: 1");
    }

    private static MonitoredProcessInfo Process(
        string name,
        int processId,
        DateTimeOffset? startedAt,
        double cpuSeconds)
    {
        return new MonitoredProcessInfo(
            name,
            processId,
            startedAt,
            TimeSpan.FromSeconds(cpuSeconds),
            string.Empty);
    }
}

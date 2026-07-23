using LockRun.Models;
using LockRun.Services;

namespace LockRun.Tests;

[TestClass]
public sealed class SettingsLoaderTests
{
    [TestMethod]
    public void Normalize_CleansNamesAndClampsNumericSettings()
    {
        var settings = SettingsLoader.Normalize(new AppSettings
        {
            MonitoredProcessNames = [" codex ", "CODEX", "", "node"],
            Language = "unsupported",
            SampleIntervalSeconds = 1,
            ReportHistoryLimit = 1000
        });

        CollectionAssert.AreEqual(new[] { "codex", "node" }, settings.MonitoredProcessNames);
        Assert.AreEqual("auto", settings.Language);
        Assert.AreEqual(2, settings.SampleIntervalSeconds);
        Assert.AreEqual(100, settings.ReportHistoryLimit);
    }

    [TestMethod]
    public void Normalize_UsesDefaultsWhenSettingsAreMissing()
    {
        var settings = SettingsLoader.Normalize(null);

        CollectionAssert.Contains(settings.MonitoredProcessNames, "codex");
        CollectionAssert.Contains(settings.MonitoredProcessNames, "claude");
        Assert.AreEqual(5, settings.SampleIntervalSeconds);
        Assert.AreEqual(30, settings.ReportHistoryLimit);
    }
}

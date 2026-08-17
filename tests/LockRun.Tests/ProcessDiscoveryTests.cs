using LockRun.Services;

namespace LockRun.Tests;

[TestClass]
public sealed class ProcessDiscoveryTests
{
    [TestMethod]
    public void BuildCandidates_GroupsNamesAndSummarizesWindows()
    {
        var candidates = ProcessDiscovery.BuildCandidates(
        [
            new DiscoveredProcess("Unity.exe", 1, "Project A"),
            new DiscoveredProcess("unity", 2, "Project A"),
            new DiscoveredProcess("Unity", 3, "Project B"),
            new DiscoveredProcess("claude", 4, string.Empty),
            new DiscoveredProcess("", 5, "Ignored")
        ]);

        Assert.HasCount(2, candidates);
        Assert.AreEqual("claude", candidates[0].Name);
        Assert.AreEqual(1, candidates[0].InstanceCount);
        Assert.AreEqual("Unity", candidates[1].Name);
        Assert.AreEqual(3, candidates[1].InstanceCount);
        Assert.AreEqual("Project A / Project B", candidates[1].MainWindowTitle);
    }

    [TestMethod]
    public void NormalizeProcessName_RemovesExeSuffixOnly()
    {
        Assert.AreEqual("Unity", ProcessDiscovery.NormalizeProcessName("Unity.exe"));
        Assert.AreEqual("claude", ProcessDiscovery.NormalizeProcessName("claude"));
    }
}

using LockRun.Services;

namespace LockRun.Tests;

[TestClass]
public sealed class ReportStoreTests
{
    [TestMethod]
    public void DeleteHistory_DeletesOnlyAwayReportFiles()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "LockRun.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);

        try
        {
            var firstReport = Path.Combine(testDirectory, "away-20260817-120000-000.txt");
            var secondReport = Path.Combine(testDirectory, "away-20260817-130000-000.txt");
            var unrelatedFile = Path.Combine(testDirectory, "notes.txt");
            File.WriteAllText(firstReport, "first");
            File.WriteAllText(secondReport, "second");
            File.WriteAllText(unrelatedFile, "keep");

            var deletedCount = ReportStore.DeleteHistory(testDirectory);

            Assert.AreEqual(2, deletedCount);
            Assert.IsFalse(File.Exists(firstReport));
            Assert.IsFalse(File.Exists(secondReport));
            Assert.IsTrue(File.Exists(unrelatedFile));
        }
        finally
        {
            Directory.Delete(testDirectory, true);
        }
    }

    [TestMethod]
    public void DeleteHistory_ReturnsZeroWhenDirectoryDoesNotExist()
    {
        var missingDirectory = Path.Combine(
            Path.GetTempPath(),
            "LockRun.Tests",
            Guid.NewGuid().ToString("N"));

        var deletedCount = ReportStore.DeleteHistory(missingDirectory);

        Assert.AreEqual(0, deletedCount);
    }
}

using LockRun.Services;

namespace LockRun.Tests;

[TestClass]
public sealed class WindowsPowerGuardTests
{
    [TestMethod]
    public void EnableAndDisable_TogglesWindowsPowerRequest()
    {
        using var powerGuard = new WindowsPowerGuard();

        powerGuard.Enable();
        Assert.IsTrue(powerGuard.IsEnabled);

        powerGuard.Disable();
        Assert.IsFalse(powerGuard.IsEnabled);
    }
}

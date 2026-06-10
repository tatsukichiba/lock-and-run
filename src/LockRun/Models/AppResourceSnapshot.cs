namespace LockRun.Models;

public sealed record AppResourceSnapshot(
    long WorkingSetBytes,
    TimeSpan TotalProcessorTime)
{
    public static AppResourceSnapshot CaptureCurrentProcess()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return new AppResourceSnapshot(
            process.WorkingSet64,
            process.TotalProcessorTime);
    }
}

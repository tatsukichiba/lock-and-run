namespace LockRun.Models;

public sealed record RunningProcessCandidate(
    string Name,
    int InstanceCount,
    string MainWindowTitle);

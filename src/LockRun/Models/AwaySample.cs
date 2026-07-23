namespace LockRun.Models;

public sealed record AwaySample(
    DateTimeOffset CapturedAt,
    IReadOnlyList<MonitoredProcessInfo> Processes);

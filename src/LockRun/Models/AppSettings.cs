namespace LockRun.Models;

public sealed class AppSettings
{
    public List<string> MonitoredProcessNames { get; set; } = [];

    public string Language { get; set; } = "auto";

    public int SampleIntervalSeconds { get; set; } = 5;

    public int ReportHistoryLimit { get; set; } = 30;
}

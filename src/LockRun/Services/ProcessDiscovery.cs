using System.Diagnostics;
using LockRun.Models;

namespace LockRun.Services;

public static class ProcessDiscovery
{
    public static IReadOnlyList<RunningProcessCandidate> Discover()
    {
        var processes = new List<DiscoveredProcess>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!TryReadProcess(process, out var discoveredProcess) ||
                    discoveredProcess.ProcessId == Environment.ProcessId)
                {
                    continue;
                }

                processes.Add(discoveredProcess);
            }
        }

        return BuildCandidates(processes);
    }

    internal static IReadOnlyList<RunningProcessCandidate> BuildCandidates(
        IEnumerable<DiscoveredProcess> processes)
    {
        return processes
            .Where(static process => !string.IsNullOrWhiteSpace(process.Name))
            .Select(static process => process with
            {
                Name = NormalizeProcessName(process.Name.Trim())
            })
            .GroupBy(static process => process.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new RunningProcessCandidate(
                group.Key,
                group.Count(),
                string.Join(" / ", group
                    .Select(static process => process.MainWindowTitle)
                    .Where(static title => !string.IsNullOrWhiteSpace(title))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(2))))
            .OrderBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string NormalizeProcessName(string name)
    {
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }

    private static bool TryReadProcess(
        Process process,
        out DiscoveredProcess discoveredProcess)
    {
        try
        {
            discoveredProcess = new DiscoveredProcess(
                process.ProcessName,
                process.Id,
                process.MainWindowTitle ?? string.Empty);
            return true;
        }
        catch
        {
            discoveredProcess = new DiscoveredProcess(string.Empty, 0, string.Empty);
            return false;
        }
    }
}

internal sealed record DiscoveredProcess(
    string Name,
    int ProcessId,
    string MainWindowTitle);

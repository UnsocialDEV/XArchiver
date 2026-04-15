using System.Diagnostics;
using System.Management;

namespace XArchiver.Services;

public sealed class ScraperBrowserProcessController : IScraperBrowserProcessController
{
    private readonly IScraperSessionStore _scraperSessionStore;

    public ScraperBrowserProcessController(IScraperSessionStore scraperSessionStore)
    {
        _scraperSessionStore = scraperSessionStore;
    }

    public Task<int> TerminateSessionProcessesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ScraperBrowserSessionInfo? sessionInfo = _scraperSessionStore.GetSessionInfo();
        if (sessionInfo is null)
        {
            return Task.FromResult(0);
        }

        HashSet<int> processIds = new();
        if (sessionInfo.LastLaunchedBrowserProcessId is int knownProcessId and > 0)
        {
            processIds.Add(knownProcessId);
        }

        foreach (int processId in FindSessionProcessIds(sessionInfo))
        {
            processIds.Add(processId);
        }

        int terminatedCount = 0;
        foreach (int processId in processIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using Process process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                terminatedCount++;
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        return Task.FromResult(terminatedCount);
    }

    private static IEnumerable<int> FindSessionProcessIds(ScraperBrowserSessionInfo sessionInfo)
    {
        string normalizedUserDataDirectory = sessionInfo.UserDataDirectory.Replace("\\", "\\\\", StringComparison.Ordinal);
        using ManagementObjectSearcher searcher = new(
            "SELECT ProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name='chrome.exe' OR Name='msedge.exe' OR Name='brave.exe' OR Name='vivaldi.exe' OR Name='chromium.exe' OR Name='opera.exe'");

        foreach (ManagementObject processObject in searcher.Get())
        {
            string executablePath = processObject["ExecutablePath"] as string ?? string.Empty;
            string commandLine = processObject["CommandLine"] as string ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(sessionInfo.BrowserExecutablePath) &&
                !string.Equals(executablePath, sessionInfo.BrowserExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool matchesUserDataDirectory = commandLine.Contains(sessionInfo.UserDataDirectory, StringComparison.OrdinalIgnoreCase) ||
                                            commandLine.Contains(normalizedUserDataDirectory, StringComparison.OrdinalIgnoreCase);
            bool matchesDebugPort = sessionInfo.RemoteDebuggingPort > 0 &&
                                    commandLine.Contains(
                                        $"--remote-debugging-port={sessionInfo.RemoteDebuggingPort}",
                                        StringComparison.OrdinalIgnoreCase);

            if (!matchesUserDataDirectory && !matchesDebugPort)
            {
                continue;
            }

            if (processObject["ProcessId"] is uint processId)
            {
                yield return unchecked((int)processId);
            }
        }
    }
}

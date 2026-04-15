using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Win32;

namespace XArchiver.Services;

internal sealed class ScraperBrowserSessionLauncher : IScraperBrowserSessionLauncher
{
    private const string EdgeExecutableName = "msedge.exe";
    private readonly IScraperBrowserProcessController _scraperBrowserProcessController;
    private readonly IScraperSessionStore _scraperSessionStore;

    public ScraperBrowserSessionLauncher(
        IScraperSessionStore scraperSessionStore,
        IScraperBrowserProcessController scraperBrowserProcessController)
    {
        _scraperSessionStore = scraperSessionStore;
        _scraperBrowserProcessController = scraperBrowserProcessController;
    }

    public ScraperBrowserSessionInfo? GetCurrentSession()
    {
        return _scraperSessionStore.GetSessionInfo();
    }

    public Task<ScraperBrowserSessionInfo> OpenLoginBrowserAsync(ScraperBrowserLaunchOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        BrowserLaunchTarget launchTarget = ResolveLaunchTarget();
        ScraperBrowserSessionInfo? existingSession = _scraperSessionStore.GetSessionInfo();
        int remoteDebuggingPort = existingSession?.RemoteDebuggingPort is int existingPort and > 0
            ? existingPort
            : GetAvailablePort();
        string userDataDirectory = _scraperSessionStore.GetUserDataDirectory();

        Directory.CreateDirectory(Path.Combine(userDataDirectory, "Default"));

        ScraperBrowserSessionInfo sessionInfo = new()
        {
            BrowserDisplayName = launchTarget.DisplayName,
            BrowserExecutablePath = launchTarget.ExecutablePath,
            BrowserKind = launchTarget.BrowserKind,
            IsInitialized = true,
            IsValidated = false,
            LastValidatedUtc = null,
            RemoteDebuggingPort = remoteDebuggingPort,
            UserDataDirectory = userDataDirectory,
        };

        _scraperSessionStore.SaveSessionInfo(sessionInfo);

        Process? launchedProcess = Process.Start(
            new ProcessStartInfo
            {
                Arguments = BuildArguments(userDataDirectory, remoteDebuggingPort, options.TargetUrl),
                FileName = launchTarget.ExecutablePath,
                UseShellExecute = true,
            });

        if (launchedProcess is not null)
        {
            _scraperSessionStore.SaveSessionInfo(
                sessionInfo with
                {
                    LastLaunchedBrowserProcessId = launchedProcess.Id,
                });
        }

        return Task.FromResult(sessionInfo);
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _scraperSessionStore.ResetSession();
        return Task.CompletedTask;
    }

    public Task<int> TerminateSessionProcessesAsync(CancellationToken cancellationToken)
    {
        return _scraperBrowserProcessController.TerminateSessionProcessesAsync(cancellationToken);
    }

    private static string BuildArguments(string userDataDirectory, int remoteDebuggingPort, string targetUrl)
    {
        return string.Join(
            ' ',
            [
                "--new-window",
                $"--user-data-dir=\"{userDataDirectory}\"",
                "--profile-directory=Default",
                $"--remote-debugging-port={remoteDebuggingPort}",
                "--no-first-run",
                $"\"{targetUrl}\"",
            ]);
    }

    private static int GetAvailablePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static bool IsChromiumExecutablePath(string executablePath)
    {
        string fileName = Path.GetFileName(executablePath);
        if (string.Equals(fileName, EdgeExecutableName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "chrome.exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "brave.exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "vivaldi.exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "chromium.exe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedPath = executablePath.Replace('\\', '/');
        return normalizedPath.Contains("/Google/Chrome/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/Microsoft/Edge/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/BraveSoftware/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/Vivaldi/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/Chromium/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/Opera/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ParseExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string trimmedCommand = command.Trim();
        if (trimmedCommand.StartsWith('"'))
        {
            int closingQuoteIndex = trimmedCommand.IndexOf('"', 1);
            return closingQuoteIndex > 1
                ? trimmedCommand[1..closingQuoteIndex]
                : null;
        }

        int executableIndex = trimmedCommand.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return executableIndex > 0
            ? trimmedCommand[..(executableIndex + 4)]
            : null;
    }

    private static string? ReadOpenCommand(string progId)
    {
        using RegistryKey? commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
        return commandKey?.GetValue(null) as string;
    }

    private static string? ReadUserChoiceProgId()
    {
        using RegistryKey? userChoiceKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        return userChoiceKey?.GetValue("ProgId") as string;
    }

    private static string? ResolveDefaultBrowserExecutablePath()
    {
        string? progId = ReadUserChoiceProgId();
        if (string.IsNullOrWhiteSpace(progId))
        {
            return null;
        }

        string? command = ReadOpenCommand(progId);
        string? executablePath = ParseExecutablePath(command ?? string.Empty);
        return string.IsNullOrWhiteSpace(executablePath) ? null : executablePath;
    }

    private static string ResolveEdgeExecutablePath()
    {
        string programFilesX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string candidate = Path.Combine(programFilesX86Path, "Microsoft", "Edge", "Application", EdgeExecutableName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        candidate = Path.Combine(programFilesPath, "Microsoft", "Edge", "Application", EdgeExecutableName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return EdgeExecutableName;
    }

    private static BrowserLaunchTarget ResolveLaunchTarget()
    {
        string? defaultExecutablePath = ResolveDefaultBrowserExecutablePath();
        if (!string.IsNullOrWhiteSpace(defaultExecutablePath) && IsChromiumExecutablePath(defaultExecutablePath))
        {
            return new BrowserLaunchTarget(
                ScraperBrowserKind.Chromium,
                "Default Chromium",
                defaultExecutablePath);
        }

        return new BrowserLaunchTarget(
            ScraperBrowserKind.Edge,
            "Edge fallback",
            ResolveEdgeExecutablePath());
    }

    private sealed record BrowserLaunchTarget(
        ScraperBrowserKind BrowserKind,
        string DisplayName,
        string ExecutablePath);
}

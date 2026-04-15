namespace XArchiver.Services;

internal sealed class ScraperSessionLockCleaner : IScraperSessionLockCleaner
{
    private static readonly string[] LockFileNames =
    [
        "SingletonCookie",
        "SingletonLock",
        "SingletonSocket",
        "SingletonStartupLock",
    ];

    public bool Clean(string userDataDirectory)
    {
        if (string.IsNullOrWhiteSpace(userDataDirectory) || !Directory.Exists(userDataDirectory))
        {
            return false;
        }

        bool cleanedAny = false;
        foreach (string lockFileName in LockFileNames)
        {
            string lockPath = Path.Combine(userDataDirectory, lockFileName);
            if (!File.Exists(lockPath))
            {
                continue;
            }

            File.Delete(lockPath);
            cleanedAny = true;
        }

        return cleanedAny;
    }
}

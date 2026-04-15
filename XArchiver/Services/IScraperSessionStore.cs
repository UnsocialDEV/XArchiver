namespace XArchiver.Services;

public interface IScraperSessionStore
{
    string GetUserDataDirectory();

    ScraperBrowserSessionInfo? GetSessionInfo();

    void ResetSession();

    void SaveSessionInfo(ScraperBrowserSessionInfo sessionInfo);
}

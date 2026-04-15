namespace XArchiver.Services;

internal interface IScraperSessionLockCleaner
{
    bool Clean(string userDataDirectory);
}

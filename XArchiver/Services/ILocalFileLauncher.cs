namespace XArchiver.Services;

public interface ILocalFileLauncher
{
    Task OpenAsync(string path);
}

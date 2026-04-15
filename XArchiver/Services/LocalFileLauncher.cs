using Windows.Storage;
using Windows.System;

namespace XArchiver.Services;

public sealed class LocalFileLauncher : ILocalFileLauncher
{
    public async Task OpenAsync(string path)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(path);
        await Launcher.LaunchFileAsync(file);
    }
}

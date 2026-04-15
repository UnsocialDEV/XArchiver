using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace XArchiver.Services;

public sealed class VideoThumbnailCache : IVideoThumbnailCache
{
    private const uint ThumbnailSize = 480;
    private readonly string _cacheDirectory;

    public VideoThumbnailCache(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    public async Task<string?> GetThumbnailPathAsync(string mediaPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
        {
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);

        string cachePath = GetCachePath(mediaPath);
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        try
        {
            StorageFile videoFile = await StorageFile.GetFileFromPathAsync(mediaPath);
            using StorageItemThumbnail? thumbnail = await videoFile.GetThumbnailAsync(
                ThumbnailMode.VideosView,
                ThumbnailSize,
                ThumbnailOptions.UseCurrentScale);

            if (thumbnail is null || thumbnail.Size == 0)
            {
                return null;
            }

            await using Stream inputStream = thumbnail.AsStreamForRead();
            await using FileStream outputStream = File.Create(cachePath);
            await inputStream.CopyToAsync(outputStream, cancellationToken);
            await outputStream.FlushAsync(cancellationToken);
            return cachePath;
        }
        catch
        {
            return null;
        }
    }

    private string GetCachePath(string mediaPath)
    {
        FileInfo fileInfo = new(mediaPath);
        string fingerprint = $"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
        byte[] hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprint));
        string hash = Convert.ToHexString(hashBytes);
        return Path.Combine(_cacheDirectory, $"{hash}.jpg");
    }
}

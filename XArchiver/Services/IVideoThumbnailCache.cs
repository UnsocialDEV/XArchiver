namespace XArchiver.Services;

public interface IVideoThumbnailCache
{
    Task<string?> GetThumbnailPathAsync(string mediaPath, CancellationToken cancellationToken);
}

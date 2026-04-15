namespace XArchiver.Core.Interfaces;

public interface IMediaDownloader
{
    Task DownloadAsync(Uri sourceUri, string destinationPath, CancellationToken cancellationToken);
}

using XArchiver.Core.Interfaces;

namespace XArchiver.Core.Services;

public sealed class MediaDownloader : IMediaDownloader
{
    private readonly HttpClient _httpClient;

    public MediaDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task DownloadAsync(Uri sourceUri, string destinationPath, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(sourceUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream destinationStream = File.Create(destinationPath);
        await using Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
    }
}

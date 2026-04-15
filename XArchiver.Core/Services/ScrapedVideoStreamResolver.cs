using System.Globalization;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ScrapedVideoStreamResolver : IScrapedVideoStreamResolver
{
    private readonly HttpClient _httpClient;

    public ScrapedVideoStreamResolver(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ScrapedVideoStreamResolution> ResolveAsync(IReadOnlyList<string> candidateUrls, CancellationToken cancellationToken)
    {
        List<string> normalizedCandidates = candidateUrls
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .Where(candidate => !candidate.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? directMp4 = normalizedCandidates.FirstOrDefault(IsDirectMp4Url);
        if (!string.IsNullOrWhiteSpace(directMp4))
        {
            return new ScrapedVideoStreamResolution
            {
                ResolutionKind = "DirectMp4",
                ResolvedUrl = directMp4,
                WasResolved = true,
            };
        }

        foreach (string manifestUrl in normalizedCandidates.Where(IsManifestUrl))
        {
            string? resolvedUrl = await TryResolveFromManifestAsync(new Uri(manifestUrl), depth: 0, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(resolvedUrl))
            {
                return new ScrapedVideoStreamResolution
                {
                    ManifestUrl = manifestUrl,
                    ResolutionKind = "HlsToMp4",
                    ResolvedUrl = resolvedUrl,
                    WasResolved = true,
                };
            }
        }

        return new ScrapedVideoStreamResolution
        {
            FailureReason = normalizedCandidates.Count == 0
                ? "No downloadable video asset URLs were discovered."
                : "No downloadable MP4 video asset could be resolved from the discovered sources.",
            ResolutionKind = "Failed",
            WasResolved = false,
        };
    }

    private static bool IsDirectMp4Url(string candidateUrl)
    {
        return candidateUrl.Contains("video.twimg.com", StringComparison.OrdinalIgnoreCase) &&
               candidateUrl.Contains(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManifestUrl(string candidateUrl)
    {
        return candidateUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> TryResolveFromManifestAsync(Uri manifestUri, int depth, CancellationToken cancellationToken)
    {
        if (depth > 2)
        {
            return null;
        }

        string manifestText;
        try
        {
            manifestText = await _httpClient.GetStringAsync(manifestUri, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        List<PlaylistEntry> entries = ParseEntries(manifestUri, manifestText);
        string? directMp4 = entries
            .Where(entry => entry.IsMp4)
            .OrderByDescending(entry => entry.Bandwidth)
            .Select(entry => entry.Url)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(directMp4))
        {
            return directMp4;
        }

        foreach (PlaylistEntry nestedManifest in entries.Where(entry => entry.IsManifest).OrderByDescending(entry => entry.Bandwidth))
        {
            string? nestedResolvedUrl = await TryResolveFromManifestAsync(new Uri(nestedManifest.Url), depth + 1, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(nestedResolvedUrl))
            {
                return nestedResolvedUrl;
            }
        }

        return null;
    }

    private static List<PlaylistEntry> ParseEntries(Uri manifestUri, string manifestText)
    {
        List<PlaylistEntry> entries = [];
        int? pendingBandwidth = null;

        foreach (string rawLine in manifestText.Split('\n'))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
            {
                pendingBandwidth = ParseBandwidth(line);
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            Uri resolvedUri = Uri.TryCreate(manifestUri, line, out Uri? candidateUri)
                ? candidateUri
                : new Uri(line, UriKind.Absolute);

            string absoluteUrl = resolvedUri.AbsoluteUri;
            entries.Add(
                new PlaylistEntry
                {
                    Bandwidth = pendingBandwidth ?? 0,
                    IsManifest = IsManifestUrl(absoluteUrl),
                    IsMp4 = IsDirectMp4Url(absoluteUrl),
                    Url = absoluteUrl,
                });

            pendingBandwidth = null;
        }

        return entries;
    }

    private static int ParseBandwidth(string line)
    {
        const string bandwidthPrefix = "BANDWIDTH=";
        int startIndex = line.IndexOf(bandwidthPrefix, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return 0;
        }

        int valueStart = startIndex + bandwidthPrefix.Length;
        int valueEnd = line.IndexOf(',', valueStart);
        string value = valueEnd >= 0
            ? line[valueStart..valueEnd]
            : line[valueStart..];

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bandwidth)
            ? bandwidth
            : 0;
    }

    private sealed class PlaylistEntry
    {
        public int Bandwidth { get; init; }

        public bool IsManifest { get; init; }

        public bool IsMp4 { get; init; }

        public string Url { get; init; } = string.Empty;
    }
}

using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace XArchiver.Services;

internal sealed partial class SensitiveMediaDetector : ISensitiveMediaDetector
{
    private static readonly string[] WarningMarkers =
    [
        "Content warning",
        "Adult Content",
        "sensitive content",
        "sensitive material",
        "This media may contain",
    ];

    public async Task<IReadOnlyList<SensitiveMediaCandidate>> DetectAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] articleHtml = await page.EvaluateAsync<string[]>(
            """
            () => {
              const articles = Array.from(document.querySelectorAll("article[data-testid='tweet'], article[role='article']"));
              return articles.map(article => article.outerHTML);
            }
            """)
            .ConfigureAwait(false);

        List<SensitiveMediaCandidate> candidates = [];
        HashSet<string> seenPostIds = new(StringComparer.Ordinal);
        foreach (string articleHtmlItem in articleHtml)
        {
            string warningMarker = WarningMarkers.FirstOrDefault(
                marker => articleHtmlItem.Contains(marker, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(warningMarker))
            {
                continue;
            }

            Match match = StatusUrlPattern().Match(articleHtmlItem);
            if (!match.Success)
            {
                continue;
            }

            string postId = match.Groups["postId"].Value;
            if (!seenPostIds.Add(postId))
            {
                continue;
            }

            string username = match.Groups["username"].Value;
            candidates.Add(
                new SensitiveMediaCandidate
                {
                    PostId = postId,
                    PostUrl = $"https://x.com/{username}/status/{postId}",
                    WarningMarker = warningMarker,
                });
        }

        return candidates;
    }

    [GeneratedRegex(@"/(?<username>[^/\?\#]+)/status/(?<postId>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StatusUrlPattern();
}

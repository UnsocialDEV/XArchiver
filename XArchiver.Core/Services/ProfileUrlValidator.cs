using System.Text.RegularExpressions;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ProfileUrlValidator : IProfileUrlValidator
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "twitter.com",
        "www.twitter.com",
        "x.com",
        "www.x.com",
    };

    private static readonly Regex InvalidStatusPattern = new("/status/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public ProfileUrlValidationResult? Validate(string profileUrl)
    {
        string sanitizedProfileUrl = profileUrl.Trim().Trim('\'', '"');
        if (!Uri.TryCreate(sanitizedProfileUrl, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        if (!AllowedHosts.Contains(uri.Host))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return null;
        }

        if (segments[0].Equals("i", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (InvalidStatusPattern.IsMatch(uri.AbsolutePath))
        {
            return null;
        }

        string username = segments[0];
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        string normalizedPath = segments.Length > 1
            ? $"/{segments[0]}/{segments[1]}"
            : $"/{segments[0]}";

        string normalizedUrl = $"{uri.Scheme}://{uri.Host}{normalizedPath}";
        return new ProfileUrlValidationResult
        {
            NormalizedUrl = normalizedUrl,
            Username = username,
        };
    }
}

namespace XArchiver.Core.Models;

public sealed class ScrapedVideoStreamResolution
{
    public string FailureReason { get; set; } = string.Empty;

    public string ManifestUrl { get; set; } = string.Empty;

    public string ResolutionKind { get; set; } = string.Empty;

    public string ResolvedUrl { get; set; } = string.Empty;

    public bool WasResolved { get; set; }
}

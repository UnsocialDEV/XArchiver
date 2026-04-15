namespace XArchiver.Core.Models;

public sealed record ScraperDiagnosticsEvent
{
    public string? ArtifactPath { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Selector { get; init; }

    public ScraperDiagnosticsSeverity Severity { get; init; } = ScraperDiagnosticsSeverity.Information;

    public string StageText { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? Url { get; init; }
}

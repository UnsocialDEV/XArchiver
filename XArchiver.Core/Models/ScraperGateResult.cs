namespace XArchiver.Core.Models;

public sealed record ScraperGateResult
{
    public ScraperGateDisposition Disposition { get; init; } = ScraperGateDisposition.NotPresent;

    public string Message { get; init; } = string.Empty;

    public string? Selector { get; init; }
}

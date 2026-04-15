namespace XArchiver.Core.Models;

public sealed class ProfileUrlValidationResult
{
    public string NormalizedUrl { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;
}

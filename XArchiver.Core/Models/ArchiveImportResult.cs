namespace XArchiver.Core.Models;

public sealed class ArchiveImportResult
{
    public int ImportedCount { get; init; }

    public int UpdatedCount { get; init; }

    public int SkippedInvalidCount { get; init; }

    public int SkippedDuplicateCount { get; init; }

    public IReadOnlyList<ArchiveProfile> ImportedProfiles { get; init; } = [];
}

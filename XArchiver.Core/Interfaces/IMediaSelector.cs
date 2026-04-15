using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IMediaSelector
{
    IReadOnlyList<ArchivedMediaRecord> SelectMedia(string postId, IReadOnlyList<XMediaDefinition> mediaDefinitions);
}

using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IArchiveMetadataBuilder
{
    ArchivedPostMetadataDocument Build(ArchivedPostRecord post);
}

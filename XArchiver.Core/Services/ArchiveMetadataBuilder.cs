using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ArchiveMetadataBuilder : IArchiveMetadataBuilder
{
    public ArchivedPostMetadataDocument Build(ArchivedPostRecord post)
    {
        return new ArchivedPostMetadataDocument
        {
            SchemaVersion = ArchivedPostRecord.ExtendedMetadataSchemaVersion,
            Post = post,
        };
    }
}

using XArchiver.Core.Models;

namespace XArchiver.Core.Utilities;

public static class PostTypeClassifier
{
    public static ArchivePostType Classify(bool isReply, IReadOnlyList<string> referenceTypes)
    {
        if (referenceTypes.Any(referenceType => string.Equals(referenceType, "retweeted", StringComparison.OrdinalIgnoreCase)))
        {
            return ArchivePostType.Repost;
        }

        if (referenceTypes.Any(referenceType => string.Equals(referenceType, "quoted", StringComparison.OrdinalIgnoreCase)))
        {
            return ArchivePostType.Quote;
        }

        if (isReply)
        {
            return ArchivePostType.Reply;
        }

        return ArchivePostType.Original;
    }
}

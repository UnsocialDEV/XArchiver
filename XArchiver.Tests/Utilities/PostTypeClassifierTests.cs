using XArchiver.Core.Models;
using XArchiver.Core.Utilities;

namespace XArchiver.Tests.Utilities;

[TestClass]
public sealed class PostTypeClassifierTests
{
    [TestMethod]
    public void ClassifyWhenQuotedReferenceExistsReturnsQuote()
    {
        ArchivePostType result = PostTypeClassifier.Classify(false, ["quoted"]);

        Assert.AreEqual(ArchivePostType.Quote, result);
    }

    [TestMethod]
    public void ClassifyWhenReplyFlagIsTrueReturnsReply()
    {
        ArchivePostType result = PostTypeClassifier.Classify(true, []);

        Assert.AreEqual(ArchivePostType.Reply, result);
    }

    [TestMethod]
    public void ClassifyWhenRetweetReferenceExistsReturnsRepost()
    {
        ArchivePostType result = PostTypeClassifier.Classify(false, ["retweeted"]);

        Assert.AreEqual(ArchivePostType.Repost, result);
    }

    [TestMethod]
    public void ClassifyWhenNoSignalsExistReturnsOriginal()
    {
        ArchivePostType result = PostTypeClassifier.Classify(false, []);

        Assert.AreEqual(ArchivePostType.Original, result);
    }
}

using XArchiver.Core.Utilities;

namespace XArchiver.Tests.Utilities;

[TestClass]
public sealed class ArchiveRangeComposerTests
{
    [TestMethod]
    public void TryComposeUtcRangeWhenDisabledReturnsNoRange()
    {
        bool isValid = ArchiveRangeComposer.TryComposeUtcRange(
            useArchiveRange: false,
            DateTimeOffset.Now,
            TimeSpan.Zero,
            DateTimeOffset.Now,
            TimeSpan.Zero,
            out DateTimeOffset? archiveStartUtc,
            out DateTimeOffset? archiveEndUtc,
            out string? validationError);

        Assert.IsTrue(isValid);
        Assert.IsNull(archiveStartUtc);
        Assert.IsNull(archiveEndUtc);
        Assert.IsNull(validationError);
    }

    [TestMethod]
    public void TryComposeUtcRangeWhenStartIsNotEarlierThanStopReturnsValidationError()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset today = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

        bool isValid = ArchiveRangeComposer.TryComposeUtcRange(
            useArchiveRange: true,
            today,
            TimeSpan.FromHours(18),
            today,
            TimeSpan.FromHours(18),
            out _,
            out _,
            out string? validationError);

        Assert.IsFalse(isValid);
        Assert.AreEqual("Choose an archive range where the start time is earlier than the stop time.", validationError);
    }
}

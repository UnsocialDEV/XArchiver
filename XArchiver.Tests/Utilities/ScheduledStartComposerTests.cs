using XArchiver.Core.Utilities;

namespace XArchiver.Tests.Utilities;

[TestClass]
public sealed class ScheduledStartComposerTests
{
    [TestMethod]
    public void TryComposeUtcScheduledStartWhenFutureDateSelectedReturnsUtcValue()
    {
        DateTimeOffset scheduledLocal = DateTimeOffset.Now.AddMinutes(20);
        DateTimeOffset scheduledDate = new(
            scheduledLocal.Year,
            scheduledLocal.Month,
            scheduledLocal.Day,
            0,
            0,
            0,
            scheduledLocal.Offset);

        bool isValid = ScheduledStartComposer.TryComposeUtcScheduledStart(
            useScheduledStart: true,
            scheduledDate,
            scheduledLocal.TimeOfDay,
            out DateTimeOffset? scheduledStartUtc,
            out string? validationError);

        Assert.IsTrue(isValid);
        Assert.IsTrue(scheduledStartUtc.HasValue);
        Assert.IsTrue(scheduledStartUtc > DateTimeOffset.UtcNow);
        Assert.IsNull(validationError);
    }
}

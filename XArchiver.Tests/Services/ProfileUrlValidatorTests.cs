using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class ProfileUrlValidatorTests
{
    [TestMethod]
    public void ValidateWhenUrlIsAProfileReturnsNormalizedResult()
    {
        ProfileUrlValidator validator = new();

        ProfileUrlValidationResult? result = validator.Validate("https://x.com/OpenAI/with_replies");

        Assert.IsNotNull(result);
        Assert.AreEqual("OpenAI", result.Username);
        Assert.AreEqual("https://x.com/OpenAI/with_replies", result.NormalizedUrl);
    }

    [TestMethod]
    public void ValidateWhenUrlIsStatusLinkReturnsNull()
    {
        ProfileUrlValidator validator = new();

        ProfileUrlValidationResult? result = validator.Validate("https://twitter.com/OpenAI/status/1234567890");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ValidateWhenHostIsNotSupportedReturnsNull()
    {
        ProfileUrlValidator validator = new();

        ProfileUrlValidationResult? result = validator.Validate("https://example.com/OpenAI");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ValidateWhenUrlHasWrappedQuotesTrimsThem()
    {
        ProfileUrlValidator validator = new();

        ProfileUrlValidationResult? result = validator.Validate("'https://x.com/ditzymaru\"'");

        Assert.IsNotNull(result);
        Assert.AreEqual("ditzymaru", result.Username);
        Assert.AreEqual("https://x.com/ditzymaru", result.NormalizedUrl);
    }
}

using XArchiver.Core.Models;
using XArchiver.Core.Services;

namespace XArchiver.Tests.Services;

[TestClass]
public sealed class SyncCostEstimatorTests
{
    [TestMethod]
    public void EstimateForInitialSelectiveSyncReturnsHigherScanScenario()
    {
        SyncCostEstimator estimator = new();
        ArchiveProfile profile = new()
        {
            IncludeOriginalPosts = true,
            IncludeQuotes = false,
            IncludeReplies = false,
            IncludeReposts = false,
            MaxPostsPerSync = 5,
        };

        SyncCostEstimate result = estimator.Estimate(profile, 5.02m);

        Assert.AreEqual(5, result.BaselineEstimatedPostReads);
        Assert.AreEqual(45, result.HighScanEstimatedPostReads);
        Assert.AreEqual(0.03m, result.BaselineEstimatedCost);
        Assert.AreEqual(0.23m, result.HighScanEstimatedCost);
    }

    [TestMethod]
    public void EstimateForIncrementalBroadSyncKeepsLowerMultiplier()
    {
        SyncCostEstimator estimator = new();
        ArchiveProfile profile = new()
        {
            IncludeOriginalPosts = true,
            IncludeQuotes = true,
            IncludeReplies = true,
            IncludeReposts = true,
            LastSinceId = "123",
            MaxPostsPerSync = 20,
        };

        SyncCostEstimate result = estimator.Estimate(profile, 5.02m);

        Assert.AreEqual(20, result.BaselineEstimatedPostReads);
        Assert.AreEqual(46, result.HighScanEstimatedPostReads);
        Assert.AreEqual(0.10m, result.BaselineEstimatedCost);
        Assert.AreEqual(0.23m, result.HighScanEstimatedCost);
    }
}

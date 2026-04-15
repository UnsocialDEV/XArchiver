using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class SyncCostEstimator : ISyncCostEstimator
{
    public SyncCostEstimate Estimate(ArchiveProfile profile, decimal costPerThousandPostReads)
    {
        int requestedPosts = Math.Max(1, profile.MaxPostsPerSync);
        decimal sanitizedRate = Math.Max(0m, costPerThousandPostReads);
        decimal postReadUnitCost = sanitizedRate / 1000m;

        int baselineEstimatedPostReads = requestedPosts;
        int highScanEstimatedPostReads = Math.Max(
            baselineEstimatedPostReads,
            (int)Math.Ceiling(requestedPosts * GetScanMultiplier(profile)));

        return new SyncCostEstimate
        {
            AssumedRatePerThousandPostReads = decimal.Round(sanitizedRate, 2, MidpointRounding.AwayFromZero),
            BaselineEstimatedCost = RoundCurrency(baselineEstimatedPostReads * postReadUnitCost),
            BaselineEstimatedPostReads = baselineEstimatedPostReads,
            HighScanEstimatedCost = RoundCurrency(highScanEstimatedPostReads * postReadUnitCost),
            HighScanEstimatedPostReads = highScanEstimatedPostReads,
        };
    }

    private static decimal GetScanMultiplier(ArchiveProfile profile)
    {
        int selectedPostTypeCount = GetSelectedPostTypeCount(profile);
        decimal selectivityMultiplier = selectedPostTypeCount switch
        {
            4 => 2m,
            3 => 2.5m,
            2 => 3.5m,
            1 => 6m,
            _ => 1m,
        };

        decimal syncModeMultiplier = string.IsNullOrWhiteSpace(profile.LastSinceId) ? 1.5m : 1.15m;
        return selectivityMultiplier * syncModeMultiplier;
    }

    private static int GetSelectedPostTypeCount(ArchiveProfile profile)
    {
        int selectedCount = 0;

        if (profile.IncludeOriginalPosts)
        {
            selectedCount++;
        }

        if (profile.IncludeReplies)
        {
            selectedCount++;
        }

        if (profile.IncludeQuotes)
        {
            selectedCount++;
        }

        if (profile.IncludeReposts)
        {
            selectedCount++;
        }

        return selectedCount;
    }

    private static decimal RoundCurrency(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

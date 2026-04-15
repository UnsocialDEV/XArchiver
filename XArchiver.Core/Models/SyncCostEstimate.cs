namespace XArchiver.Core.Models;

public sealed class SyncCostEstimate
{
    public decimal AssumedRatePerThousandPostReads { get; init; }

    public int BaselineEstimatedPostReads { get; init; }

    public decimal BaselineEstimatedCost { get; init; }

    public int HighScanEstimatedPostReads { get; init; }

    public decimal HighScanEstimatedCost { get; init; }
}

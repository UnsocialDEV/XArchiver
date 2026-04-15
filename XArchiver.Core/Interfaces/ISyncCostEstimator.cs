using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface ISyncCostEstimator
{
    SyncCostEstimate Estimate(ArchiveProfile profile, decimal costPerThousandPostReads);
}

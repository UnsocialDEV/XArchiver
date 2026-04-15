using XArchiver.Core.Models;

namespace XArchiver.Services;

internal interface IScraperExecutionPolicyProvider
{
    ScraperExecutionPolicy GetPolicy(ScraperExecutionMode mode);
}

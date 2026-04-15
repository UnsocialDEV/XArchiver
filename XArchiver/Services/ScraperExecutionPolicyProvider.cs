using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed class ScraperExecutionPolicyProvider : IScraperExecutionPolicyProvider
{
    public ScraperExecutionPolicy GetPolicy(ScraperExecutionMode mode)
    {
        return mode switch
        {
            ScraperExecutionMode.Conservative => new ScraperExecutionPolicy
            {
                AuthenticationFailureThreshold = 2,
                BlockedGateThreshold = 2,
                GateCooldownMaximumMilliseconds = 2400,
                GateCooldownMinimumMilliseconds = 1500,
                MaximumNoNewPostCycles = 2,
                Mode = ScraperExecutionMode.Conservative,
                RouteRecoveryCooldownMaximumMilliseconds = 1800,
                RouteRecoveryCooldownMinimumMilliseconds = 1200,
                RouteRecoveryThreshold = 5,
                ScrollDelayMaximumMilliseconds = 5000,
                ScrollDelayMinimumMilliseconds = 3200,
                SensitiveDetailPageThreshold = 6,
                SensitiveRevealCooldownMaximumMilliseconds = 1700,
                SensitiveRevealCooldownMinimumMilliseconds = 900,
                SensitiveRevealFailureThreshold = 3,
                VideoDetailCooldownMaximumMilliseconds = 2400,
                VideoDetailCooldownMinimumMilliseconds = 1400,
                VideoDetailPageThreshold = 12,
                VideoResolutionFailureThreshold = 4,
            },
            _ => new ScraperExecutionPolicy
            {
                AuthenticationFailureThreshold = 5,
                BlockedGateThreshold = 5,
                GateCooldownMaximumMilliseconds = 1000,
                GateCooldownMinimumMilliseconds = 1000,
                MaximumNoNewPostCycles = 3,
                Mode = ScraperExecutionMode.Normal,
                RouteRecoveryCooldownMaximumMilliseconds = 1000,
                RouteRecoveryCooldownMinimumMilliseconds = 1000,
                RouteRecoveryThreshold = 12,
                ScrollDelayMaximumMilliseconds = 1500,
                ScrollDelayMinimumMilliseconds = 1500,
                SensitiveDetailPageThreshold = 100,
                SensitiveRevealCooldownMaximumMilliseconds = 750,
                SensitiveRevealCooldownMinimumMilliseconds = 750,
                SensitiveRevealFailureThreshold = 20,
                VideoDetailCooldownMaximumMilliseconds = 1200,
                VideoDetailCooldownMinimumMilliseconds = 1200,
                VideoDetailPageThreshold = 200,
                VideoResolutionFailureThreshold = 20,
            },
        };
    }
}

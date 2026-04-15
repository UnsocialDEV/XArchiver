using XArchiver.Core.Models;

namespace XArchiver.Services;

internal sealed record ScraperExecutionPolicy
{
    public int AuthenticationFailureThreshold { get; init; }

    public int BlockedGateThreshold { get; init; }

    public int GateCooldownMaximumMilliseconds { get; init; }

    public int GateCooldownMinimumMilliseconds { get; init; }

    public int MaximumNoNewPostCycles { get; init; }

    public ScraperExecutionMode Mode { get; init; }

    public int RouteRecoveryCooldownMaximumMilliseconds { get; init; }

    public int RouteRecoveryCooldownMinimumMilliseconds { get; init; }

    public int RouteRecoveryThreshold { get; init; }

    public int ScrollDelayMaximumMilliseconds { get; init; }

    public int ScrollDelayMinimumMilliseconds { get; init; }

    public int SensitiveDetailPageThreshold { get; init; }

    public int SensitiveRevealCooldownMaximumMilliseconds { get; init; }

    public int SensitiveRevealCooldownMinimumMilliseconds { get; init; }

    public int SensitiveRevealFailureThreshold { get; init; }

    public int VideoDetailCooldownMaximumMilliseconds { get; init; }

    public int VideoDetailCooldownMinimumMilliseconds { get; init; }

    public int VideoDetailPageThreshold { get; init; }

    public int VideoResolutionFailureThreshold { get; init; }
}

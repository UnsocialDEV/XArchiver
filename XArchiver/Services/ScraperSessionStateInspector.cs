using Microsoft.Playwright;

namespace XArchiver.Services;

internal sealed class ScraperSessionStateInspector : IScraperSessionStateInspector
{
    private const string PrimaryColumnSelector = "[data-testid='primaryColumn']";
    private const string StatusLinkSelector = "[data-testid='primaryColumn'] a[href*='/status/']";
    private const string TweetArticleSelector = "article[data-testid='tweet'], article[role='article']";

    public async Task<ScraperSessionPageState> InspectAsync(IPage page)
    {
        bool hasTweetArticles = await page.Locator(TweetArticleSelector).CountAsync().ConfigureAwait(false) > 0;
        bool hasStatusLinks = await page.Locator(StatusLinkSelector).CountAsync().ConfigureAwait(false) > 0;
        bool hasPrimaryColumn = await page.Locator(PrimaryColumnSelector).CountAsync().ConfigureAwait(false) > 0;
        bool hasGuestAuthPrompt = await HasGuestAuthPromptAsync(page).ConfigureAwait(false);
        bool hasSensitiveProfileInterstitial = await HasSensitiveProfileInterstitialAsync(page).ConfigureAwait(false);
        bool hasTimelineContent = hasTweetArticles || hasStatusLinks;
        bool isLoginFlow = page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
                           page.Url.Contains("/i/flow", StringComparison.OrdinalIgnoreCase);

        bool requiresAuthentication = isLoginFlow ||
                                      hasGuestAuthPrompt ||
                                      (!hasPrimaryColumn && !hasTimelineContent);

        return new ScraperSessionPageState
        {
            HasGuestAuthPrompt = hasGuestAuthPrompt,
            HasPrimaryColumn = hasPrimaryColumn,
            HasSensitiveProfileInterstitial = hasSensitiveProfileInterstitial,
            HasTimelineContent = hasTimelineContent,
            Reason = BuildReason(isLoginFlow, hasGuestAuthPrompt, hasSensitiveProfileInterstitial, hasPrimaryColumn, hasTimelineContent),
            RequiresAuthentication = requiresAuthentication,
        };
    }

    private static string BuildReason(
        bool isLoginFlow,
        bool hasGuestAuthPrompt,
        bool hasSensitiveProfileInterstitial,
        bool hasPrimaryColumn,
        bool hasTimelineContent)
    {
        if (isLoginFlow)
        {
            return "X redirected the scraper into the login flow. Reopen the dedicated X login browser, sign in again, close it, and validate the scraper session before scraping.";
        }

        if (hasGuestAuthPrompt && hasSensitiveProfileInterstitial)
        {
            return "The scraper opened the profile in guest mode instead of the dedicated signed-in X session. Close the dedicated login browser, validate the session again, and retry the scrape.";
        }

        if (hasGuestAuthPrompt)
        {
            return "The dedicated scraper session is not signed in to X. Reopen the dedicated X login browser, sign in again, close it, and validate the scraper session before scraping.";
        }

        if (!hasPrimaryColumn && !hasTimelineContent)
        {
            return "X did not load a usable timeline page for the scraper session.";
        }

        return string.Empty;
    }

    private static async Task<bool> HasGuestAuthPromptAsync(IPage page)
    {
        bool hasLogInButton = await HasNamedControlAsync(page, "Log in").ConfigureAwait(false);
        bool hasSignUpButton = await HasNamedControlAsync(page, "Sign up").ConfigureAwait(false);
        if (hasLogInButton && hasSignUpButton)
        {
            return true;
        }

        return await page.GetByText("Don’t miss what’s happening").CountAsync().ConfigureAwait(false) > 0 ||
               await page.GetByText("Don't miss what's happening").CountAsync().ConfigureAwait(false) > 0;
    }

    private static async Task<bool> HasNamedControlAsync(IPage page, string name)
    {
        if (await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name }).CountAsync().ConfigureAwait(false) > 0)
        {
            return true;
        }

        return await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = name }).CountAsync().ConfigureAwait(false) > 0;
    }

    private static async Task<bool> HasSensitiveProfileInterstitialAsync(IPage page)
    {
        if (await HasNamedControlAsync(page, "Yes, view profile").ConfigureAwait(false))
        {
            return true;
        }

        return await page.GetByText("may include potentially sensitive content").CountAsync().ConfigureAwait(false) > 0;
    }
}

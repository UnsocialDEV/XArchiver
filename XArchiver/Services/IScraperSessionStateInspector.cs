using Microsoft.Playwright;

namespace XArchiver.Services;

internal interface IScraperSessionStateInspector
{
    Task<ScraperSessionPageState> InspectAsync(IPage page);
}

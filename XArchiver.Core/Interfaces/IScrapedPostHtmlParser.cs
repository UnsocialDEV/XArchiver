using XArchiver.Core.Models;

namespace XArchiver.Core.Interfaces;

public interface IScrapedPostHtmlParser
{
    ScrapedPostRecord? Parse(string articleHtml, string fallbackUsername);
}

using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class ScraperPreviewPostItemViewModel : ObservableObject
{
    public ScraperPreviewPostItemViewModel(ScrapedPostRecord post)
    {
        Post = post;
    }

    public string CreatedAtText => Post.CreatedAtUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);

    public string MediaSummaryText => Post.Media.Count == 0
        ? "No media"
        : $"{Post.Media.Count} media item(s)";

    public ScrapedPostRecord Post { get; }

    public string Username => Post.Username;

    public string PreviewText => string.IsNullOrWhiteSpace(Post.Text) ? "(No text extracted)" : Post.Text;
}

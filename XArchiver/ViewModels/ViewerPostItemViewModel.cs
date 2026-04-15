using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class ViewerPostItemViewModel : ObservableObject
{
    public ViewerPostItemViewModel(ArchivedPostRecord post)
    {
        Post = post;
    }

    public string CreatedAtText => Post.CreatedAtUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);

    public string MediaBadgeText => Post.Media.Count == 0 ? "Text only" : $"{Post.Media.Count} media";

    public ArchivedPostRecord Post { get; }

    public string PostTypeText => Post.PostType.ToString();

    public string PreviewText => Post.Text;
}

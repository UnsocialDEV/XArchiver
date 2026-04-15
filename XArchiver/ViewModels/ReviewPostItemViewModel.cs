using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Models;

namespace XArchiver.ViewModels;

public sealed class ReviewPostItemViewModel : ObservableObject
{
    private bool _isAlreadyArchived;
    private bool _isSelected;

    public ReviewPostItemViewModel(PreviewPostRecord post)
    {
        Post = post;
        _isAlreadyArchived = post.IsAlreadyArchived;
        _isSelected = post.IsSelected;
    }

    public event EventHandler? SelectionStateChanged;

    public string CreatedAtText => Post.CreatedAtUtc.ToLocalTime().ToString("f", System.Globalization.CultureInfo.CurrentCulture);

    public bool CanSelect => !IsAlreadyArchived;

    public string ArchivedBadgeText => IsAlreadyArchived ? "Archived" : string.Empty;

    public bool HasMedia => Post.Media.Count > 0;

    public bool IsAlreadyArchived
    {
        get => _isAlreadyArchived;
        set
        {
            if (SetProperty(ref _isAlreadyArchived, value))
            {
                Post.IsAlreadyArchived = value;
                if (value && IsSelected)
                {
                    IsSelected = false;
                }

                OnPropertyChanged(nameof(ArchivedBadgeText));
                OnPropertyChanged(nameof(CanSelect));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            bool nextValue = value && !IsAlreadyArchived;
            if (SetProperty(ref _isSelected, nextValue))
            {
                Post.IsSelected = nextValue;
                SelectionStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string MediaSummaryText => Post.Media.Count == 0 ? string.Empty : $"{Post.Media.Count} media";

    public PreviewPostRecord Post { get; }

    public string PostTypeText => Post.PostType.ToString();
}

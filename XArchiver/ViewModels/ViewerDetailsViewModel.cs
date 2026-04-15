using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class ViewerDetailsViewModel : ObservableObject
{
    private readonly IArchiveMetadataRepository _archiveMetadataRepository;
    private readonly ILocalFileLauncher _localFileLauncher;
    private double _mediaPreviewMaxHeight = 360;
    private readonly IResourceService _resourceService;
    private string _metadataJsonText = string.Empty;
    private string _metadataStatusText = string.Empty;
    private ArchivedPostRecord? _selectedPost;

    public ViewerDetailsViewModel(
        IArchiveMetadataRepository archiveMetadataRepository,
        ILocalFileLauncher localFileLauncher,
        IResourceService resourceService)
    {
        _archiveMetadataRepository = archiveMetadataRepository;
        _localFileLauncher = localFileLauncher;
        _resourceService = resourceService;
    }

    public bool CanOpenMetadataFile => SelectedPost is not null && !string.IsNullOrWhiteSpace(SelectedPost.MetadataRelativePath) && File.Exists(SelectedPost.MetadataRelativePath);

    public bool HasSelectedPost => SelectedPost is not null;

    public ObservableCollection<ViewerMediaItemViewModel> MediaItems { get; } = [];

    public double MediaPreviewMaxHeight
    {
        get => _mediaPreviewMaxHeight;
        set => SetProperty(ref _mediaPreviewMaxHeight, value);
    }

    public string MetadataJsonText
    {
        get => _metadataJsonText;
        set => SetProperty(ref _metadataJsonText, value);
    }

    public string MetadataStatusText
    {
        get => _metadataStatusText;
        set => SetProperty(ref _metadataStatusText, value);
    }

    public ObservableCollection<DetailFieldViewModel> OverviewFields { get; } = [];

    public ArchivedPostRecord? SelectedPost
    {
        get => _selectedPost;
        private set
        {
            if (SetProperty(ref _selectedPost, value))
            {
                OnPropertyChanged(nameof(CanOpenMetadataFile));
                OnPropertyChanged(nameof(HasSelectedPost));
            }
        }
    }

    public async Task LoadAsync(ArchivedPostRecord? post)
    {
        Clear();

        if (post is null)
        {
            return;
        }

        SelectedPost = post;
        ArchivedPostMetadataReadResult metadataResult = await _archiveMetadataRepository.LoadAsync(post.MetadataRelativePath, CancellationToken.None);
        ArchivedPostRecord detailSource = metadataResult.Document?.Post ?? post;

        PopulateOverview(detailSource, metadataResult);
        PopulateMedia(detailSource);
        MetadataJsonText = metadataResult.RawJson;
        MetadataStatusText = GetMetadataStatusText(metadataResult);
    }

    public Task OpenMetadataFileAsync()
    {
        return !CanOpenMetadataFile || SelectedPost is null
            ? Task.CompletedTask
            : _localFileLauncher.OpenAsync(SelectedPost.MetadataRelativePath!);
    }

    public void UpdateMediaPreviewMaxHeight(double availableHeight)
    {
        double targetHeight = Math.Clamp(availableHeight * 0.38, 240, 520);
        MediaPreviewMaxHeight = targetHeight;
    }

    private void Clear()
    {
        MediaItems.Clear();
        MetadataJsonText = string.Empty;
        MetadataStatusText = string.Empty;
        OverviewFields.Clear();
        SelectedPost = null;
    }

    private string GetMetadataStatusText(ArchivedPostMetadataReadResult metadataResult)
    {
        if (!metadataResult.HasMetadataFile)
        {
            return _resourceService.GetString("ViewerMetadataUnavailable");
        }

        return metadataResult.IsExtendedMetadata
            ? _resourceService.Format("ViewerMetadataExtendedFormat", metadataResult.Document?.SchemaVersion ?? 0)
            : _resourceService.GetString("ViewerMetadataLegacy");
    }

    private void PopulateMedia(ArchivedPostRecord post)
    {
        foreach (ArchivedMediaRecord media in post.Media)
        {
            ArchivedMediaDetailRecord? matchingDetail = post.MediaDetails.FirstOrDefault(detail => string.Equals(detail.MediaKey, media.MediaKey, StringComparison.Ordinal));
            MediaItems.Add(new ViewerMediaItemViewModel(media, matchingDetail));
        }
    }

    private void PopulateOverview(ArchivedPostRecord post, ArchivedPostMetadataReadResult metadataResult)
    {
        AddField("ViewerOverviewPostId", post.PostId);
        AddField("ViewerOverviewUsername", post.Username);
        AddField("ViewerOverviewUserId", post.UserId);
        AddField("ViewerOverviewPostType", _resourceService.GetString($"PostType{post.PostType}"));
        AddField("ViewerOverviewCreatedLocal", post.CreatedAtUtc.ToLocalTime().ToString("f", System.Globalization.CultureInfo.CurrentCulture));
        AddField("ViewerOverviewCreatedUtc", post.CreatedAtUtc.ToUniversalTime().ToString("u", System.Globalization.CultureInfo.CurrentCulture));
        AddField("ViewerOverviewArchivedAt", post.ArchivedAtUtc?.ToLocalTime().ToString("f", System.Globalization.CultureInfo.CurrentCulture) ?? _resourceService.GetString("ViewerValueUnavailable"));
        AddField("ViewerOverviewConversationId", post.ConversationId ?? _resourceService.GetString("ViewerValueUnavailable"));
        AddField("ViewerOverviewReplyTarget", post.InReplyToUserId ?? _resourceService.GetString("ViewerValueUnavailable"));
        AddField("ViewerOverviewReferences", FormatReferences(post.ReferencedPosts));
        AddField("ViewerOverviewMetrics", _resourceService.Format("MetricsSummaryFormat", post.LikeCount, post.ReplyCount, post.RepostCount, post.QuoteCount));
        AddField("ViewerOverviewTextPath", post.TextRelativePath ?? _resourceService.GetString("ViewerValueUnavailable"));
        AddField("ViewerOverviewMetadataPath", metadataResult.MetadataFilePath == string.Empty ? _resourceService.GetString("ViewerValueUnavailable") : metadataResult.MetadataFilePath);
        AddField("ViewerOverviewText", post.Text);
        AddField("ViewerOverviewRawPayload", string.IsNullOrWhiteSpace(post.RawPayloadJson) ? _resourceService.GetString("ViewerValueUnavailable") : _resourceService.GetString("ViewerRawPayloadAvailable"));
    }

    private void AddField(string labelResourceKey, string value)
    {
        OverviewFields.Add(
            new DetailFieldViewModel
            {
                Label = _resourceService.GetString(labelResourceKey),
                Value = value,
            });
    }

    private string FormatReferences(List<ArchivedReferencedPostRecord> references)
    {
        return references.Count == 0
            ? _resourceService.GetString("ViewerValueUnavailable")
            : string.Join(Environment.NewLine, references.Select(reference => $"{reference.ReferenceType}: {reference.ReferencedPostId}"));
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Services;
using XArchiver.Services;
using XArchiver.ViewModels;

namespace XArchiver;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public static T GetService<T>()
        where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        GetService<IWindowContext>().SetWindow(_window);
        StartScheduler();
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        string appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XArchiver");

        ServiceCollection services = new();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IWindowContext, WindowContext>();
        services.AddSingleton<IResourceService, ResourceService>();
        services.AddSingleton<IVideoPreviewSegmentPlanner, VideoPreviewSegmentPlanner>();
        services.AddSingleton<IVideoThumbnailCache>(
            _ => new VideoThumbnailCache(Path.Combine(appDataRoot, "video-thumbnails")));
        services.AddSingleton<IScraperBrowserProcessController, ScraperBrowserProcessController>();
        services.AddSingleton<IScraperExecutionPolicyProvider, ScraperExecutionPolicyProvider>();
        services.AddSingleton<IAppSettingsRepository>(
            _ => new AppSettingsRepository(Path.Combine(appDataRoot, "appsettings.json")));
        services.AddSingleton<IReviewCostFormatter, ReviewCostFormatter>();
        services.AddSingleton<ISyncConfirmationFormatter, SyncConfirmationFormatter>();
        services.AddSingleton<IScraperSessionStore>(
            _ => new ScraperSessionStore(Path.Combine(appDataRoot, "scraper-session")));
        services.AddSingleton<IScraperBrowserSessionLauncher, ScraperBrowserSessionLauncher>();
        services.AddSingleton<IScraperSessionLockCleaner, ScraperSessionLockCleaner>();
        services.AddSingleton<IScraperSessionStateInspector, ScraperSessionStateInspector>();
        services.AddSingleton<IXCredentialStore, CredentialStore>();
        services.AddSingleton<IFolderAccessService, FolderAccessService>();
        services.AddSingleton<ILocalFileLauncher, LocalFileLauncher>();
        services.AddSingleton<IArchiveProfileRepository>(
            _ => new ArchiveProfileRepository(Path.Combine(appDataRoot, "profiles.json")));
        services.AddSingleton<IScheduledArchiveRunRepository>(
            _ => new ScheduledArchiveRunRepository(Path.Combine(appDataRoot, "scheduled-runs.json")));
        services.AddSingleton<IArchiveInspectionService, ArchiveInspectionService>();
        services.AddSingleton<IArchiveImportService, ArchiveImportService>();
        services.AddSingleton<IArchiveIndexRepository, ArchiveIndexRepository>();
        services.AddSingleton<IArchiveMetadataRepository, ArchiveMetadataRepository>();
        services.AddSingleton<IArchiveMetadataBuilder, ArchiveMetadataBuilder>();
        services.AddSingleton<IMediaSelector, MediaSelector>();
        services.AddSingleton<IProfileUrlValidator, ProfileUrlValidator>();
        services.AddSingleton<IApiSyncRequestFactory, ApiSyncRequestFactory>();
        services.AddSingleton<IWebArchiveRequestFactory, WebArchiveRequestFactory>();
        services.AddSingleton<IScrapedPostHtmlParser, ScrapedPostHtmlParser>();
        services.AddSingleton<IScraperGateHandler, ScraperGateHandler>();
        services.AddSingleton<IScraperRouteGuard, ScraperRouteGuard>();
        services.AddSingleton<IVideoAssetUrlCollector, VideoAssetUrlCollector>();
        services.AddSingleton<IScrapedVideoResolver, ScrapedVideoResolver>();
        services.AddSingleton<ISensitiveMediaDetector, SensitiveMediaDetector>();
        services.AddSingleton<ISensitiveMediaRevealCoordinator, SensitiveMediaRevealCoordinator>();
        services.AddSingleton<IScraperRunManager>(
            provider => new ScraperRunManager(
                provider.GetRequiredService<IWebArchiveService>(),
                provider.GetRequiredService<IScraperBrowserSessionLauncher>(),
                Path.Combine(appDataRoot, "scraper-diagnostics")));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IArchiveRunScheduler, ArchiveRunScheduler>();
        services.AddSingleton<ISyncCostEstimator, SyncCostEstimator>();
        services.AddSingleton<ScrapedPostArchiveMapper>();
        services.AddSingleton<IArchiveSyncService, ArchiveSyncService>();
        services.AddSingleton<IProfileWebScraper, PlaywrightProfileWebScraper>();
        services.AddSingleton<ISyncRunner, SyncRunner>();
        services.AddSingleton<ISyncSessionManager, SyncSessionManager>();
        services.AddTransient<IArchiveFileWriter, ArchiveFileWriter>();
        services.AddTransient<IPostReviewService, PostReviewService>();
        services.AddTransient<IManualArchiveService, ManualArchiveService>();
        services.AddSingleton<IWebArchiveService, WebArchiveService>();
        services.AddHttpClient<IXApiClient, XApiClient>(client => client.BaseAddress = new Uri("https://api.x.com/2/"));
        services.AddHttpClient<IMediaDownloader, MediaDownloader>();
        services.AddHttpClient<IScrapedVideoStreamResolver, ScrapedVideoStreamResolver>();
        services.AddTransient<HomePageViewModel>();
        services.AddTransient<ArchiveProfileEditorViewModel>();
        services.AddTransient<ArchiveRunTimingViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MediaOverlayViewModel>();
        services.AddTransient<ProfileReviewWorkspaceViewModel>();
        services.AddTransient<ProfilesPageViewModel>();
        services.AddTransient<ReviewPageViewModel>();
        services.AddTransient<ScraperPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<SyncsPageViewModel>();
        services.AddTransient<ViewerDetailsViewModel>();
        services.AddTransient<ViewerPageViewModel>();
        return services.BuildServiceProvider();
    }

    private static async void StartScheduler()
    {
        try
        {
            await GetService<IArchiveRunScheduler>().InitializeAsync();
        }
        catch
        {
            // Keep app startup resilient. Scheduler errors surface in page views when opened.
        }
    }
}

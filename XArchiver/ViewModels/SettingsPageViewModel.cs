using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Core.Interfaces;
using XArchiver.Models;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class SettingsPageViewModel : ObservableObject
{
    private const double MaximumEstimatedRate = 1000;
    private const double MinimumEstimatedRate = 0.01;

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IXCredentialStore _credentialStore;
    private readonly IResourceService _resourceService;
    private string _credentialStatusText = string.Empty;
    private double _estimatedCostPerThousandPostReads = 5.02;
    private bool _isBusy;
    private bool _isInitialized;
    private string _statusMessage = string.Empty;

    public SettingsPageViewModel(
        IAppSettingsRepository appSettingsRepository,
        IXCredentialStore credentialStore,
        IResourceService resourceService)
    {
        _appSettingsRepository = appSettingsRepository;
        _credentialStore = credentialStore;
        _resourceService = resourceService;
    }

    public string CredentialStatusText
    {
        get => _credentialStatusText;
        set => SetProperty(ref _credentialStatusText, value);
    }

    public double EstimatedCostPerThousandPostReads
    {
        get => _estimatedCostPerThousandPostReads;
        set => SetProperty(ref _estimatedCostPerThousandPostReads, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public async Task DeleteCredentialAsync()
    {
        IsBusy = true;
        try
        {
            await _credentialStore.DeleteCredentialAsync(CancellationToken.None);
            await RefreshCredentialStatusAsync();
            StatusMessage = _resourceService.GetString("StatusTokenRemoved");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            await RefreshCredentialStatusAsync();
            return;
        }

        AppSettings settings = await _appSettingsRepository.GetAsync(CancellationToken.None);
        EstimatedCostPerThousandPostReads = Convert.ToDouble(settings.EstimatedCostPerThousandPostReads, System.Globalization.CultureInfo.InvariantCulture);
        await RefreshCredentialStatusAsync();
        _isInitialized = true;
    }

    public async Task RefreshCredentialStatusAsync()
    {
        bool hasCredential = await _credentialStore.HasCredentialAsync(CancellationToken.None);
        CredentialStatusText = _resourceService.GetString(hasCredential ? "StatusCredentialAvailable" : "StatusCredentialMissing");
    }

    public async Task SaveCredentialAsync(string credential)
    {
        IsBusy = true;
        try
        {
            await _credentialStore.SaveCredentialAsync(credential, CancellationToken.None);
            await RefreshCredentialStatusAsync();
            StatusMessage = _resourceService.GetString("StatusTokenSaved");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveEstimatedRateAsync()
    {
        if (EstimatedCostPerThousandPostReads < MinimumEstimatedRate || EstimatedCostPerThousandPostReads > MaximumEstimatedRate)
        {
            StatusMessage = _resourceService.Format("StatusEstimatedRateValidationFormat", MinimumEstimatedRate, MaximumEstimatedRate);
            return;
        }

        IsBusy = true;
        try
        {
            AppSettings settings = new()
            {
                EstimatedCostPerThousandPostReads = Convert.ToDecimal(EstimatedCostPerThousandPostReads, System.Globalization.CultureInfo.InvariantCulture),
            };

            await _appSettingsRepository.SaveAsync(settings, CancellationToken.None);
            StatusMessage = _resourceService.GetString("StatusSettingsSaved");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

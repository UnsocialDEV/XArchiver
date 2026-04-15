using Windows.Storage.Pickers;
using XArchiver.Core.Interfaces;

namespace XArchiver.Services;

internal sealed class FolderAccessService : IFolderAccessService
{
    private readonly IWindowContext _windowContext;

    public FolderAccessService(IWindowContext windowContext)
    {
        _windowContext = windowContext;
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowContext.GetWindowHandle());
        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}

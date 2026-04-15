using CommunityToolkit.Mvvm.ComponentModel;
using XArchiver.Services;

namespace XArchiver.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IResourceService resourceService)
    {
        AppTitle = resourceService.GetString("AppTitle");
    }

    public string AppTitle { get; }
}

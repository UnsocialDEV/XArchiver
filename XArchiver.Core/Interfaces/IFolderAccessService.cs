namespace XArchiver.Core.Interfaces;

public interface IFolderAccessService
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);
}

namespace XArchiver.Core.Interfaces;

public interface IXCredentialStore
{
    Task<bool> HasCredentialAsync(CancellationToken cancellationToken);

    Task SaveCredentialAsync(string credential, CancellationToken cancellationToken);

    Task<string?> GetCredentialAsync(CancellationToken cancellationToken);

    Task DeleteCredentialAsync(CancellationToken cancellationToken);
}

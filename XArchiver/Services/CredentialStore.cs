using Windows.Security.Credentials;
using XArchiver.Core.Interfaces;

namespace XArchiver.Services;

internal sealed class CredentialStore : IXCredentialStore
{
    private const string ResourceName = "XArchiver.XApiBearerToken";
    private const string UserName = "XArchiver";

    private readonly PasswordVault _passwordVault = new();

    public Task DeleteCredentialAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PasswordCredential? credential = TryGetCredential();
        if (credential is not null)
        {
            _passwordVault.Remove(credential);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetCredentialAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PasswordCredential? credential = TryGetCredential();
        if (credential is null)
        {
            return Task.FromResult<string?>(null);
        }

        credential.RetrievePassword();
        return Task.FromResult<string?>(credential.Password);
    }

    public Task<bool> HasCredentialAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryGetCredential() is not null);
    }

    public Task SaveCredentialAsync(string credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PasswordCredential? existingCredential = TryGetCredential();
        if (existingCredential is not null)
        {
            _passwordVault.Remove(existingCredential);
        }

        _passwordVault.Add(new PasswordCredential(ResourceName, UserName, credential));
        return Task.CompletedTask;
    }

    private PasswordCredential? TryGetCredential()
    {
        try
        {
            return _passwordVault.Retrieve(ResourceName, UserName);
        }
        catch
        {
            return null;
        }
    }
}

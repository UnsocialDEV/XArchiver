namespace XArchiver.Services;

public interface IScraperBrowserProcessController
{
    Task<int> TerminateSessionProcessesAsync(CancellationToken cancellationToken);
}

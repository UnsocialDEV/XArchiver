using System.Text.Json;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;

namespace XArchiver.Core.Services;

public sealed class ScheduledArchiveRunRepository : IScheduledArchiveRunRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _storagePath;

    public ScheduledArchiveRunRepository(string storagePath)
    {
        _storagePath = storagePath;
    }

    public async Task DeleteAsync(Guid runId, CancellationToken cancellationToken)
    {
        List<ScheduledArchiveRunRecord> runs = (await GetAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(run => run.RunId != runId)
            .ToList();

        await SaveRunsAsync(runs, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScheduledArchiveRunRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(_storagePath);
        List<ScheduledArchiveRunRecord>? runs = await JsonSerializer.DeserializeAsync<List<ScheduledArchiveRunRecord>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return runs ?? [];
    }

    public async Task SaveAsync(ScheduledArchiveRunRecord run, CancellationToken cancellationToken)
    {
        List<ScheduledArchiveRunRecord> runs = (await GetAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
        int existingIndex = runs.FindIndex(existingRun => existingRun.RunId == run.RunId);
        if (existingIndex >= 0)
        {
            runs[existingIndex] = run;
        }
        else
        {
            runs.Add(run);
        }

        await SaveRunsAsync(runs, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveRunsAsync(List<ScheduledArchiveRunRecord> runs, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
        await using FileStream stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, runs, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}

using CareerForge.Core;

namespace CareerForge.Infrastructure;

public sealed record WatchlistScanOutcome(string BoardToken, JobScan Scan);
public sealed record WatchlistBatch(IReadOnlyList<WatchlistScanOutcome> Results)
{
    public bool HasFailures => Results.Any(r => r.Scan.Status != "Succeeded");
    public int Added => Results.Sum(r => r.Scan.Added);
}

/// <summary>
/// Sequential bounded scanning of explicitly enabled sources.
/// Each source failure is captured independently so remaining sources still run.
/// </summary>
public sealed class WatchlistScanCoordinator(
    Func<string, bool, CancellationToken, Task<JobScan>> scan,
    TimeSpan? interBoardDelay = null)
{
    private readonly TimeSpan _delay = interBoardDelay ?? TimeSpan.FromSeconds(1);

    public async Task<WatchlistBatch> RunAsync(EmployerWatchlist watchlist,
        CancellationToken cancellationToken = default)
    {
        EmployerWatchlistEditor.Validate(watchlist);
        var enabled = watchlist.Sources.Where(source => source.Enabled).ToList();
        if (enabled.Count == 0)
            throw new InvalidOperationException("No enabled sources. Add one with 'sources add <token>'.");

        var outcomes = new List<WatchlistScanOutcome>();
        for (var i = 0; i < enabled.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = enabled[i];
            JobScan result;
            try
            {
                result = await scan(source.BoardToken, source.IncludeDescriptions, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Unexpected failures still appear in the batch report, rather than being silently ignored.
                result = new JobScan
                {
                    BoardToken = source.BoardToken, Status = "Failed",
                    StartedAtUtc = DateTime.UtcNow, FinishedAtUtc = DateTime.UtcNow,
                    Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message
                };
            }
            outcomes.Add(new WatchlistScanOutcome(source.BoardToken, result));

            if (i < enabled.Count - 1 && _delay > TimeSpan.Zero)
                await Task.Delay(_delay, cancellationToken);
        }

        return new WatchlistBatch(outcomes);
    }
}

using CareerForge.Core;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Infrastructure;

public sealed record MarketScanBatch(IReadOnlyList<JobScan> Sources)
{
    public bool HasFailures => Sources.Any(x => x.Status != "Succeeded");
    public int Added => Sources.Sum(x => x.Added);
    public int Found => Sources.Sum(x => x.Found);
}

/// <summary>Searches reviewed cross-employer indexes, using profile terms, never preselected company IDs.</summary>
public sealed class MarketScanService(
    IDbContextFactory<CareerForgeDbContext> factory,
    IEnumerable<IMarketJobFeed> feeds, TimeProvider clock)
{
    public async Task<MarketScanBatch> ScanAsync(
        CareerProfile profile, CancellationToken cancellationToken = default)
    {
        if (!profile.AllowRemote)
            throw new InvalidOperationException(
                "Current market sources contain remote-only jobs. Enable remote in the private profile " +
                "or add a reviewed nonremote market source before running this search.");
        var terms = MarketSearchTerms.FromProfile(profile);
        var results = new List<JobScan>();

        foreach (var feed in feeds.OrderBy(x => x.Provider, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scan = new JobScan
            {
                Provider = feed.Provider,
                BoardToken = "market",
                StartedAtUtc = clock.GetUtcNow().UtcDateTime
            };
            var matches = new Dictionary<string, MarketPosting>(StringComparer.Ordinal);
            var failures = new List<string>();

            foreach (var term in terms)
            {
                try
                {
                    var jobs = await feed.SearchAsync(term, cancellationToken);
                    scan.Found += jobs.Count;
                    foreach (var item in jobs)
                    {
                        if (item.Provider != feed.Provider ||
                            string.IsNullOrWhiteSpace(item.ExternalId) ||
                            item.ExternalId.Length > 100 ||
                            MarketHttpJson.HttpsUrl(item.JobUrl).Length == 0 ||
                            string.IsNullOrWhiteSpace(item.Title))
                        {
                            scan.Skipped++;
                            continue;
                        }
                        if (!matches.TryAdd(item.ExternalId, item))
                        {
                            matches[item.ExternalId] = item;
                            scan.Skipped++;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
                    InvalidDataException or System.Text.Json.JsonException or IOException)
                {
                    failures.Add($"Search '{term}': {ex.Message}");
                    // A rate-limited or otherwise failed provider should not be hammered with more requests.
                    break;
                }
                // Conservative small gap between keyword requests; no parallel provider polling.
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await JobDatabaseSchema.EnsureReadyAsync(db, cancellationToken);
            var existing = (await db.Jobs
                .Where(j => j.Provider == feed.Provider && j.BoardToken == "market")
                .ToListAsync(cancellationToken))
                .ToDictionary(j => j.ExternalId, StringComparer.Ordinal);
            var now = clock.GetUtcNow().UtcDateTime;

            foreach (var item in matches.Values)
            {
                // Himalayas requires expired positions to be removed rather than shown as open.
                if (item.ExpiresAtUtc is { } expiry && expiry.UtcDateTime <= now)
                {
                    scan.Skipped++;
                    continue;
                }
                var title = MarketHttpJson.NormalizedText(item.Title);
                var location = MarketHttpJson.NormalizedText(item.Location, 200);
                var company = MarketHttpJson.NormalizedText(item.Company, 200);
                var url = MarketHttpJson.HttpsUrl(item.JobUrl);
                if (title.Length == 0 || url.Length == 0)
                {
                    scan.Skipped++;
                    continue;
                }
                if (existing.TryGetValue(item.ExternalId, out var current))
                {
                    current.Title = title;
                    current.Location = location;
                    current.Company = company;
                    current.JobUrl = url;
                    current.SourceUpdatedAtUtc = item.SourceUpdatedAtUtc?.UtcDateTime;
                    current.ExpiresAtUtc = item.ExpiresAtUtc?.UtcDateTime;
                    current.LastSeenAtUtc = now;
                    scan.Updated++;
                }
                else
                {
                    db.Jobs.Add(new JobPosting
                    {
                        Provider = feed.Provider,
                        BoardToken = "market",
                        ExternalId = item.ExternalId,
                        Company = company,
                        Title = title,
                        Location = location,
                        JobUrl = url,
                        SourceUpdatedAtUtc = item.SourceUpdatedAtUtc?.UtcDateTime,
                        ExpiresAtUtc = item.ExpiresAtUtc?.UtcDateTime,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now
                    });
                    scan.Added++;
                }
            }
            // Prune previously stored expired Himalayas records on each scan. Do not infer
            // closure merely because a posting was absent from a partial search response.
            if (feed.Provider == "himalayas")
            {
                var expiredRows = db.Jobs.Local.Where(j => j.Provider == "himalayas" &&
                    j.BoardToken == "market" && j.ExpiresAtUtc is { } expiry &&
                    expiry <= now).ToArray();
                db.Jobs.RemoveRange(expiredRows);
            }
            scan.Status = failures.Count == 0 ? "Succeeded" : "PartialFailure";
            scan.Error = failures.Count == 0 ? null :
                string.Join("; ", failures)[..Math.Min(1000, string.Join("; ", failures).Length)];
            scan.FinishedAtUtc = clock.GetUtcNow().UtcDateTime;
            db.Scans.Add(scan);
            await db.SaveChangesAsync(cancellationToken);
            results.Add(scan);
        }
        return new MarketScanBatch(results);
    }
}

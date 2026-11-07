using CareerForge.Core;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Infrastructure;

public sealed class JobScanService(
    IDbContextFactory<CareerForgeDbContext> databaseFactory,
    IJobFeedClient feedClient,
    TimeProvider clock)
{
    public async Task<JobScan> ScanBoardAsync(
        string boardToken, bool includeDescriptions = false, CancellationToken cancellationToken = default)
    {
        if (!JobNormalizer.IsValidBoardToken(boardToken))
            throw new ArgumentException("Invalid Greenhouse board token.", nameof(boardToken));

        // One worker process, sequential boards. Distributed locking is deferred.
        await using var db = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await JobDatabaseSchema.EnsureReadyAsync(db, cancellationToken);
        var scan = new JobScan { BoardToken = boardToken, StartedAtUtc = clock.GetUtcNow().UtcDateTime };
        try
        {
            var jobs = includeDescriptions
                ? await (feedClient as IDescribedJobFeedClient
                    ?? throw new InvalidOperationException("Provider lacks a reviewed description interface."))
                    .GetJobsWithDescriptionsAsync(boardToken, cancellationToken)
                : await feedClient.GetJobsAsync(boardToken, cancellationToken);
            scan.Found = jobs.Count;
            var unique = new Dictionary<string, NormalizedJob>(StringComparer.Ordinal);
            var descriptionTexts = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var job in jobs)
            {
                if (!JobNormalizer.TryNormalize(job, out var normalized) || normalized is null)
                {
                    scan.Skipped++;
                    continue;
                }
                if (includeDescriptions)
                    descriptionTexts[normalized.ExternalId] = JobDescriptionText.Extract(job.DescriptionHtml);

                // If a source repeats an id in one response, keep its final entry.
                if (!unique.TryAdd(normalized.ExternalId, normalized))
                {
                    unique[normalized.ExternalId] = normalized;
                    scan.Skipped++;
                }
            }

            var existing = (await db.Jobs
                .Where(x => x.Provider == "greenhouse" && x.BoardToken == boardToken)
                .ToListAsync(cancellationToken)).ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
            var now = clock.GetUtcNow().UtcDateTime;
            // Only load description rows if this scan explicitly opted in.
            var storedDescriptions = new Dictionary<Guid, JobDescription>();
            if (includeDescriptions)
            {
                var ids = existing.Values.Select(x => x.Id).ToArray();
                storedDescriptions = (await db.JobDescriptions.Where(x => ids.Contains(x.JobId))
                    .ToListAsync(cancellationToken)).ToDictionary(x => x.JobId);
            }

            foreach (var candidate in unique.Values)
            {
                JobPosting posting;
                if (existing.TryGetValue(candidate.ExternalId, out var current))
                {
                    current.Title = candidate.Title;
                    current.Location = candidate.Location;
                    current.JobUrl = candidate.JobUrl;
                    current.SourceUpdatedAtUtc = candidate.SourceUpdatedAtUtc?.UtcDateTime;
                    current.LastSeenAtUtc = now;
                    scan.Updated++;
                    posting = current;
                }
                else
                {
                    posting = new JobPosting
                    {
                        BoardToken = boardToken,
                        ExternalId = candidate.ExternalId,
                        Title = candidate.Title,
                        Location = candidate.Location,
                        JobUrl = candidate.JobUrl,
                        SourceUpdatedAtUtc = candidate.SourceUpdatedAtUtc?.UtcDateTime,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now
                    };
                    db.Jobs.Add(posting);
                    scan.Added++;
                }

                if (includeDescriptions)
                {
                    var text = descriptionTexts[candidate.ExternalId];
                    if (text is null)
                    {
                        if (storedDescriptions.TryGetValue(posting.Id, out var prior))
                            db.JobDescriptions.Remove(prior);
                    }
                    else
                    {
                        var hash = JobDescriptionText.Sha256(text);
                        if (!storedDescriptions.TryGetValue(posting.Id, out var saved))
                        {
                            db.JobDescriptions.Add(new JobDescription
                            {
                                JobId = posting.Id, Text = text, Sha256 = hash,
                                SourceUrl = candidate.JobUrl, RetrievedAtUtc = now
                            });
                        }
                        else if (saved.Sha256 != hash || saved.SourceUrl != candidate.JobUrl)
                        {
                            saved.Text = text;
                            saved.Sha256 = hash;
                            saved.SourceUrl = candidate.JobUrl;
                            saved.RetrievedAtUtc = now;
                        }
                    }
                }
            }
            scan.Status = "Succeeded";
            // No inferred removals: omission from one snapshot is not proof of closure.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            scan.Status = "Failed";
            scan.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            scan.Added = 0;
            scan.Updated = 0;
            // Do not accidentally save partial job mutations after a failed feed scan.
            db.ChangeTracker.Clear();
        }

        scan.FinishedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.Scans.Add(scan);
        await db.SaveChangesAsync(cancellationToken);
        return scan;
    }
}

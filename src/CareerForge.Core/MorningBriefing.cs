namespace CareerForge.Core;

/// <summary>Evidence from observation timestamps, not a guarantee that positions remain open.</summary>
public sealed record BriefingJob(
    Guid Id, string BoardToken, string Title, string Location, string JobUrl,
    DateTime FirstSeenAtUtc, DateTime LastSeenAtUtc,
    string Triage, IReadOnlyList<string> ReasonCodes);

public sealed record BriefingSource(
    string BoardToken, string DisplayName, bool Enabled, bool IncludeDescriptions,
    string LastStatus, DateTime? LastAttemptUtc, int? LastAdded, string? LastError);

public sealed record MorningBriefing(
    DateTime GeneratedAtUtc, int WindowDays, bool ProfileConfigured,
    int RecentCount, int CandidateCount, int ReviewCount, int ExcludedCount,
    int UnclassifiedCount, int OmittedCount, IReadOnlyList<BriefingJob> Jobs,
    IReadOnlyList<BriefingSource> Sources);

public static class MorningBriefingBuilder
{
    public static MorningBriefing Build(
        IReadOnlyList<JobPosting> jobs, IReadOnlyList<JobScan> scans,
        EmployerWatchlist watchlist, CareerProfile? profile,
        DateTime utcNow, int days = 7, int maxShown = 40)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(scans);
        EmployerWatchlistEditor.Validate(watchlist);
        if (days is < 1 or > 30 || maxShown is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(days), "Window must be 1-30 days and maxShown 1-200.");
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Report clock must be UTC.", nameof(utcNow));

        var activeTokens = watchlist.Sources.Where(s => s.Enabled)
            .Select(s => s.BoardToken).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cutoff = utcNow.AddDays(-days);

        // Only boards the user currently watches; old/disabled sources are not silently included.
        var recent = jobs
            .Where(j => activeTokens.Contains(j.BoardToken) &&
                j.FirstSeenAtUtc >= cutoff && j.FirstSeenAtUtc <= utcNow)
            .Select(j =>
            {
                var triage = profile is null ? null : DeterministicTriage.Evaluate(j, profile);
                return new BriefingJob(j.Id, j.BoardToken, j.Title, j.Location, j.JobUrl,
                    j.FirstSeenAtUtc, j.LastSeenAtUtc,
                    triage?.Verdict.ToString() ?? "Unclassified", triage?.ReasonCodes ?? []);
            })
            .ToList();

        var sorted = recent
            .Where(j => j.Triage != nameof(TriageVerdict.Excluded))
            .OrderBy(j => j.Triage == nameof(TriageVerdict.Candidate) ? 0 :
                j.Triage == nameof(TriageVerdict.Review) ? 1 : 2)
            .ThenByDescending(j => j.FirstSeenAtUtc)
            .ThenBy(j => j.BoardToken, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sourceHealth = watchlist.Sources
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(source =>
            {
                var latest = scans
                    .Where(scan => scan.BoardToken.Equals(source.BoardToken, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(scan => scan.StartedAtUtc)
                    .FirstOrDefault();
                return new BriefingSource(source.BoardToken, source.DisplayName,
                    source.Enabled, source.IncludeDescriptions,
                    !source.Enabled ? "Disabled" : latest?.Status ?? "NeverScanned",
                    latest?.StartedAtUtc, latest?.Added, latest?.Error);
            }).ToArray();

        return new MorningBriefing(
            utcNow, days, profile is not null,
            recent.Count,
            recent.Count(j => j.Triage == nameof(TriageVerdict.Candidate)),
            recent.Count(j => j.Triage == nameof(TriageVerdict.Review)),
            recent.Count(j => j.Triage == nameof(TriageVerdict.Excluded)),
            recent.Count(j => j.Triage == "Unclassified"),
            Math.Max(0, sorted.Count - maxShown),
            sorted.Take(maxShown).ToArray(), sourceHealth);
    }
}

using System.Text;
using CareerForge.Core;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Infrastructure;

/// <summary>Generates deterministic, local-only reports without calling any LLM.</summary>
public sealed class MorningBriefingService(
    IDbContextFactory<CareerForgeDbContext> databaseFactory,
    EmployerWatchlistStore watchlistStore,
    PrivateProfileStore profileStore,
    TimeProvider clock)
{
    public async Task<MorningBriefing> BuildAsync(int days = 7, CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(days));
        var watchlist = await watchlistStore.LoadAsync(cancellationToken);
        var profile = await profileStore.LoadAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-days);

        await using var db = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await JobDatabaseSchema.EnsureReadyAsync(db, cancellationToken);
        var jobs = await db.Jobs.AsNoTracking()
            .Where(j => j.FirstSeenAtUtc >= cutoff && j.FirstSeenAtUtc <= now)
            .OrderByDescending(j => j.FirstSeenAtUtc)
            .ToListAsync(cancellationToken);
        var scans = await db.Scans.AsNoTracking()
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync(cancellationToken);
        return MorningBriefingBuilder.Build(jobs, scans, watchlist, profile, now, days);
    }

    public static string ToPlainText(MorningBriefing briefing)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CAREERFORGE — MORNING BRIEFING");
        sb.AppendLine($"Generated (UTC): {briefing.GeneratedAtUtc:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Newly first observed in past {briefing.WindowDays} day(s): {briefing.RecentCount}");
        sb.AppendLine($"Candidate: {briefing.CandidateCount}; Review: {briefing.ReviewCount}; " +
            $"Excluded: {briefing.ExcludedCount}; Unclassified: {briefing.UnclassifiedCount}");
        sb.AppendLine("Observation does not guarantee that a job remains open.");
        if (!briefing.ProfileConfigured)
            sb.AppendLine("No private career profile: roles cannot yet be triaged.");
        sb.AppendLine();
        sb.AppendLine("SOURCE HEALTH");
        foreach (var source in briefing.Sources)
        {
            var state = source.LastStatus;
            var when = source.LastAttemptUtc is null ? "never" : source.LastAttemptUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");
            sb.AppendLine($"- {Safe(source.DisplayName)} [{source.BoardToken}]: {state}, last attempt {when}, added {source.LastAdded?.ToString() ?? "-"}");
            if (source.LastError is not null) sb.AppendLine("  ERROR: " + Safe(source.LastError));
        }
        if (briefing.Sources.Count == 0) sb.AppendLine("No employer sources configured.");
        sb.AppendLine();
        sb.AppendLine("NEWLY DISCOVERED ROLES (candidate / review first)");
        foreach (var job in briefing.Jobs)
        {
            sb.AppendLine($"- [{job.Triage}] {Safe(job.Title)} — {Safe(job.Location)} ({job.BoardToken})");
            sb.AppendLine($"  First seen UTC: {job.FirstSeenAtUtc:yyyy-MM-dd HH:mm}; last observed UTC: {job.LastSeenAtUtc:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"  Source: {job.JobUrl}");
            if (job.ReasonCodes.Count != 0) sb.AppendLine($"  Signals: {string.Join(", ", job.ReasonCodes)}");
        }
        if (briefing.Jobs.Count == 0) sb.AppendLine("No newly observed nonexcluded roles in this time window.");
        if (briefing.OmittedCount > 0) sb.AppendLine($"{briefing.OmittedCount} additional roles omitted from the display cap.");
        return sb.ToString();
    }

    public static async Task<string> SaveLatestAsync(MorningBriefing report, string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        var path = outputPath ?? Path.Combine(LocalDataPaths.DirectoryPath, "morning-briefing.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, ToPlainText(report), cancellationToken);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return path;
    }

    private static string Safe(string value) =>
        new(value.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
}

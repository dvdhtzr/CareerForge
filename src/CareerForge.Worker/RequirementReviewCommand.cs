using CareerForge.Core;
using CareerForge.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class RequirementReviewCommand
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var model = "qwen3.5:9b";
        var limit = 3;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--model" && i + 1 < args.Length)
                model = args[++i];
            else if (args[i] == "--limit" && i + 1 < args.Length &&
                int.TryParse(args[++i], out var parsed) && parsed is >= 1 and <= 20)
                limit = parsed;
            else
            {
                Console.Error.WriteLine("Usage: requirements [--model qwen3.5:9b] [--limit 1..20]");
                return 2;
            }
        }

        var profile = await new PrivateProfileStore().LoadAsync();
        if (profile is null)
        {
            Console.Error.WriteLine("No private profile. Run the worker with: profile init");
            return 2;
        }

        await using var db = await services.GetRequiredService<IDbContextFactory<CareerForgeDbContext>>()
            .CreateDbContextAsync();
        await JobDatabaseSchema.EnsureReadyAsync(db);

        var postings = await db.Jobs.AsNoTracking()
            .Where(j => db.JobDescriptions.Any(d => d.JobId == j.Id))
            .OrderByDescending(j => j.LastSeenAtUtc).Take(500).ToListAsync();
        var selected = postings
            .Select(j => (Job: j, Triage: DeterministicTriage.Evaluate(j, profile)))
            .Where(x => x.Triage.Verdict != TriageVerdict.Excluded)
            .OrderBy(x => x.Triage.Verdict == TriageVerdict.Candidate ? 0 : 1)
            .Take(limit).ToArray();
        if (selected.Length == 0)
        {
            Console.Error.WriteLine("No descriptions available. Run: scan --with-descriptions <board-token>");
            return 2;
        }

        var ids = selected.Select(x => x.Job.Id).ToArray();
        var descriptions = (await db.JobDescriptions.AsNoTracking()
            .Where(d => ids.Contains(d.JobId)).ToListAsync())
            .ToDictionary(d => d.JobId);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        var evaluator = new OllamaRequirementEvaluator(http);
        var store = new LocalRequirementStore();
        var failed = 0;
        foreach (var (job, triage) in selected)
        {
            var description = descriptions[job.Id];
            try
            {
                var extracted = await evaluator.EvaluateAsync(job, description, profile, model);
                await store.SaveAsync(new RequirementEvaluation(
                    job.Id, profile.Fingerprint(), description.Sha256, model,
                    DateTime.UtcNow, extracted.Claims));

                var supported = extracted.Claims.Count(x => x.ProfileSkill is not null);
                Console.WriteLine($"[{triage.Verdict}] {job.Title} ({job.Location})");
                Console.WriteLine($"  {supported} of {extracted.Claims.Count} quoted requirements mention listed profile skills.");
                Console.WriteLine("  Other requirements are unverified, not proven gaps.");
                foreach (var claim in extracted.Claims)
                    Console.WriteLine($"  - \"{claim.Quote}\" [{claim.ProfileSkill ?? "not verified"}]");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
                InvalidDataException or System.Text.Json.JsonException or ArgumentException or IOException)
            {
                failed++;
                Console.Error.WriteLine($"FAILED {job.Title}: {ex.Message}");
            }
        }
        return failed == 0 ? 0 : 1;
    }
}

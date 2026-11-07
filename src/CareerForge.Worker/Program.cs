using CareerForge.Core;
using CareerForge.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();
builder.Services.AddCareerForgeInfrastructure(builder.Configuration);
using var host = builder.Build();

if (args.Length > 0 && args[0] == "profile")
{
    if (args.Length > 1 && args[1] == "init")
        return await CreateProfileAsync();
    if (args.Length > 1 && args[1] == "show")
        return await ShowProfileAsync();
    Console.Error.WriteLine("Usage: profile init | profile show");
    return 2;
}

if (args.Length > 0 && args[0] == "evaluate")
    return await EvaluateAsync(args[1..]);

if (args.Length > 0 && args[0] == "requirements")
    return await RequirementReviewCommand.RunAsync(args[1..], host.Services);

if (args.Length > 0 && args[0] == "sources")
    return await WatchlistCommands.RunAsync(args[1..]);

if (args.Length > 0 && args[0] == "daily")
    return await DailyCommands.DailyAsync(host.Services, args[1..]);

if (args.Length > 0 && args[0] == "report")
    return await DailyCommands.ReportAsync(host.Services, args[1..]);

if (args.Length >= 2 && args[0] == "scan" && args[1] == "watchlist")
    return await DailyCommands.ScanWatchlistAsync(host.Services, args[2..]);

if (args.Length >= 2 && args[0] == "scan" && args[1] == "market")
    return await MarketCommands.ScanAsync(host.Services, args[2..]);

bool withDescriptions = args.Length > 1 &&
    args[0] == "scan" && args[1] == "--with-descriptions";
string[] boards = args.Length == 0 ? ["greenhouse"] :
    args[0] == "scan" ? args[(withDescriptions ? 2 : 1)..] : args;
if (boards.Length == 0 || boards.Any(board => !JobNormalizer.IsValidBoardToken(board)))
{
    Console.Error.WriteLine("Usage: scan market | scan watchlist | scan [--with-descriptions] <board-token> [...] | daily | report | sources ...");
    return 2;
}

var scanner = host.Services.GetRequiredService<JobScanService>();
var failures = 0;
foreach (var board in boards.Distinct(StringComparer.OrdinalIgnoreCase))
{
    var result = await scanner.ScanBoardAsync(board, includeDescriptions: withDescriptions);
    Console.WriteLine($"{board}: {result.Status} | found={result.Found} " +
        $"added={result.Added} refreshed={result.Updated} skipped={result.Skipped}");
    if (result.Status != "Succeeded")
    {
        failures++;
        Console.Error.WriteLine($"  Error: {result.Error}");
    }
}
return failures == 0 ? 0 : 1;

static async Task<int> CreateProfileAsync()
{
    var store = new PrivateProfileStore();
    Console.WriteLine("CareerForge private career profile — stored outside this Git repository.");
    Console.WriteLine($"Location: {LocalDataPaths.ProfilePath}");
    if (store.Exists)
    {
        Console.Write("A local profile already exists. Replace it? (yes/no): ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            return 0;
    }

    var titles = AskTerms("Target job titles, comma separated (required)");
    if (titles.Count == 0)
    {
        Console.Error.WriteLine("At least one target job title is required.");
        return 2;
    }

    var skills = AskTerms("Skill keywords, comma separated (optional)");
    var locations = AskTerms("Preferred location terms, comma separated (optional)");
    var exclusions = AskTerms("Job title terms to explicitly exclude (optional)");
    Console.Write("Allow remote jobs? (yes/no, default yes): ");
    var remoteInput = Console.ReadLine()?.Trim();
    var remote = !string.Equals(remoteInput, "no", StringComparison.OrdinalIgnoreCase);

    try
    {
        await store.SaveAsync(new CareerProfile
        {
            TargetTitles = titles,
            SkillKeywords = skills,
            PreferredLocations = locations,
            ExcludedTitleTerms = exclusions,
            AllowRemote = remote
        });
        Console.WriteLine("Private profile saved successfully. It is not committed to Git.");
        return 0;
    }
    catch (Exception ex) when (ex is IOException or ArgumentException)
    {
        Console.Error.WriteLine($"Profile not saved: {ex.Message}");
        return 1;
    }
}

static List<string> AskTerms(string prompt)
{
    Console.Write($"{prompt}: ");
    return (Console.ReadLine() ?? "")
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static async Task<int> ShowProfileAsync()
{
    var store = new PrivateProfileStore();
    var profile = await store.LoadAsync();
    if (profile is null)
    {
        Console.Error.WriteLine("No private profile. Run: dotnet run --project src/CareerForge.Worker -- profile init");
        return 2;
    }
    Console.WriteLine($"Target titles: {string.Join(", ", profile.TargetTitles)}");
    Console.WriteLine($"Skills: {string.Join(", ", profile.SkillKeywords)}");
    Console.WriteLine($"Preferred locations: {string.Join(", ", profile.PreferredLocations)}");
    Console.WriteLine($"Allow remote: {profile.AllowRemote}");
    Console.WriteLine($"Excluded title terms: {string.Join(", ", profile.ExcludedTitleTerms)}");
    return 0;
}

async Task<int> EvaluateAsync(string[] options)
{
    var model = "qwen3.5:9b";
    var limit = 5;
    for (var i = 0; i < options.Length; i++)
    {
        if (options[i] == "--model" && i + 1 < options.Length)
            model = options[++i];
        else if (options[i] == "--limit" && i + 1 < options.Length &&
            int.TryParse(options[++i], out var parsed) && parsed is >= 1 and <= 25)
            limit = parsed;
        else
        {
            Console.Error.WriteLine("Usage: evaluate [--model qwen3.5:9b] [--limit 1..25]");
            return 2;
        }
    }

    var profile = await new PrivateProfileStore().LoadAsync();
    if (profile is null)
    {
        Console.Error.WriteLine("Create a private career profile first: profile init");
        return 2;
    }

    await using var db = await host.Services
        .GetRequiredService<IDbContextFactory<CareerForgeDbContext>>().CreateDbContextAsync();
    await JobDatabaseSchema.EnsureReadyAsync(db);
    var jobs = await db.Jobs.AsNoTracking().OrderByDescending(j => j.LastSeenAtUtc)
        .Take(500).ToListAsync();
    var candidates = jobs
        .Select(job => (Job: job, Triage: DeterministicTriage.Evaluate(job, profile)))
        .Where(result => result.Triage.Verdict != TriageVerdict.Excluded)
        .OrderBy(result => result.Triage.Verdict == TriageVerdict.Candidate ? 0 : 1)
        .Take(limit).ToArray();

    if (candidates.Length == 0)
    {
        Console.WriteLine("No jobs to evaluate. Run a Greenhouse scan first.");
        return 0;
    }

    Console.WriteLine($"Evaluating {candidates.Length} posting(s) with local Ollama model {model}.");
    Console.WriteLine("WARNING: Only job title/location are available; these are NOT verified qualification matches.");

    using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    var evaluator = new OllamaTitleEvaluator(client);
    var results = new LocalAssessmentStore();
    var errors = 0;

    foreach (var (job, triage) in candidates)
    {
        try
        {
            var verdict = await evaluator.EvaluateAsync(job, profile, model);
            var assessment = new LocalModelAssessment(
                job.Id, profile.Fingerprint(), model, verdict.Fit, verdict.Rationale,
                verdict.Uncertainty, DateTime.UtcNow);
            await results.SaveAsync(assessment);
            Console.WriteLine($"[{triage.Verdict}] [{verdict.Fit}] {job.Title} — {job.Location}");
            Console.WriteLine($"  {verdict.Rationale}");
            Console.WriteLine($"  Uncertainty: {verdict.Uncertainty}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or InvalidDataException or System.Text.Json.JsonException or ArgumentException)
        {
            errors++;
            Console.Error.WriteLine($"FAILED: {job.Title}: {ex.Message}");
        }
    }
    return errors == 0 ? 0 : 1;
}

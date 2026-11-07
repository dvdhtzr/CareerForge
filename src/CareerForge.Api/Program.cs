using CareerForge.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCareerForgeInfrastructure(builder.Configuration);
builder.Services.AddCors(options => options.AddPolicy("local-web", policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .WithMethods("GET")
    .AllowAnyHeader()));

var app = builder.Build();
app.UseCors("local-web");

await using (var db = await app.Services.GetRequiredService<IDbContextFactory<CareerForgeDbContext>>()
    .CreateDbContextAsync())
{
    await JobDatabaseSchema.EnsureReadyAsync(db);
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "CareerForge" }));
app.MapGet("/api/jobs", async (IDbContextFactory<CareerForgeDbContext> factory,
    string? query, int? limit, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var now = DateTime.UtcNow;
    var jobs = db.Jobs.AsNoTracking()
        .Where(job => job.ExpiresAtUtc == null || job.ExpiresAtUtc > now);
    if (!string.IsNullOrWhiteSpace(query))
    {
        var term = query.Trim();
        if (term.Length > 100) return Results.BadRequest("Search query exceeds 100 characters.");
        jobs = jobs.Where(job => job.Title.Contains(term) || job.Location.Contains(term)
            || job.BoardToken.Contains(term));
    }
    var rows = await jobs.OrderByDescending(x => x.LastSeenAtUtc)
        .Take(Math.Clamp(limit ?? 200, 1, 500))
        .Select(job => new
        {
            job.Id, job.Provider, job.BoardToken, job.ExternalId, job.Title,
            job.Location, job.Company, job.JobUrl, job.SourceUpdatedAtUtc,
            job.FirstSeenAtUtc, job.LastSeenAtUtc,
            HasDescription = db.JobDescriptions.Any(d => d.JobId == job.Id)
        })
        .ToListAsync(cancellationToken);
    return Results.Ok(rows);
});
app.MapGet("/api/scans", async (IDbContextFactory<CareerForgeDbContext> factory,
    CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    return Results.Ok(await db.Scans.AsNoTracking()
        .OrderByDescending(x => x.StartedAtUtc).Take(30).ToListAsync(cancellationToken));
});

// Read-only API in the first release. Scanning is intentionally a separate local CLI.
// Do not expose this server publicly: the local database is not an authenticated service.
app.MapGet("/api/profile/status", () => Results.Ok(new
{
    configured = new PrivateProfileStore().Exists
}));

app.MapGet("/api/evaluations", async Task<IResult> (CancellationToken cancellationToken) =>
{
    var profile = await new PrivateProfileStore().LoadAsync(cancellationToken);
    if (profile is null) return Results.Ok(Array.Empty<object>());
    var fingerprint = profile.Fingerprint();
    var results = await new LocalAssessmentStore().ReadAsync(cancellationToken);
    var current = results.Where(x => x.ProfileFingerprint == fingerprint)
        .OrderByDescending(x => x.EvaluatedAtUtc).Take(500)
        .Select(x => new
        {
            x.JobId, x.Model, x.Fit, x.Rationale, x.Uncertainty,
            x.EvidenceLevel, x.EvaluatedAtUtc
        }).ToArray();
    return Results.Ok(current);
});

// Return only short, validated requirement quotes. Never expose the full stored text.
app.MapGet("/api/requirement-reviews", async Task<IResult> (
    IDbContextFactory<CareerForgeDbContext> factory, CancellationToken cancellationToken) =>
{
    var profile = await new PrivateProfileStore().LoadAsync(cancellationToken);
    if (profile is null) return Results.Ok(Array.Empty<object>());
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var currentHashes = (await db.JobDescriptions.AsNoTracking()
        .Select(d => new { d.JobId, d.Sha256 }).ToListAsync(cancellationToken))
        .ToDictionary(d => d.JobId, d => d.Sha256);
    var results = await new LocalRequirementStore().ReadAsync(cancellationToken);
    var current = results
        .Where(r => r.ProfileFingerprint == profile.Fingerprint() &&
            currentHashes.TryGetValue(r.JobId, out var hash) && hash == r.DescriptionHash)
        .OrderByDescending(r => r.EvaluatedAtUtc)
        .GroupBy(r => r.JobId)
        .Select(g => g.First())
        .Take(500)
        .Select(r => new
        {
            r.JobId, r.Model, r.EvidenceLevel, r.EvaluatedAtUtc,
            Claims = r.Claims.Select(c => new { c.Quote, c.ProfileSkill }).ToArray(),
            ListedSkillCount = r.Claims.Count(c => c.ProfileSkill is not null)
        }).ToArray();
    return Results.Ok(current);
});

app.MapGet("/api/sources", async Task<IResult> (EmployerWatchlistStore store, CancellationToken ct) =>
{
    var watchlist = await store.LoadAsync(ct);
    return Results.Ok(watchlist.Sources);
});

app.MapGet("/api/briefing", async Task<IResult> (
    MorningBriefingService service, int? days, CancellationToken ct) =>
{
    if (days is < 1 or > 30) return Results.BadRequest("days must be between 1 and 30.");
    return Results.Ok(await service.BuildAsync(days ?? 7, ct));
});

// All endpoints are read-only and intended for loopback use only.
app.Run();

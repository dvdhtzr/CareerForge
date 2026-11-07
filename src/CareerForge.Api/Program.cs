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
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "CareerForge" }));
app.MapGet("/api/jobs", async (IDbContextFactory<CareerForgeDbContext> factory,
    string? query, int? limit, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var jobs = db.Jobs.AsNoTracking();
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
            job.Location, job.JobUrl, job.SourceUpdatedAtUtc,
            job.FirstSeenAtUtc, job.LastSeenAtUtc
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
app.Run();

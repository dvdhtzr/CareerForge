using CareerForge.Core;
using CareerForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Tests;

public sealed class MarketDiscoveryTests
{
    private static CareerProfile Profile() => new()
    {
        TargetTitles = ["Senior .NET Engineer"],
        SkillKeywords = [".NET"],
        PreferredLocations = ["New York"],
        AllowRemote = true
    };

    [Fact]
    public async Task Existing_database_gets_company_column_without_losing_data()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CareerForgeDbContext>().UseSqlite(connection).Options;
        await using var db = new CareerForgeDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var original = new JobPosting
        {
            BoardToken = "oldboard", ExternalId = "567",
            Title = "Old role", Location = "New York", JobUrl = "https://example.org/old",
            FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
        };
        db.Jobs.Add(original);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Jobs\" DROP COLUMN \"Company\"");
        db.ChangeTracker.Clear();

        await JobDatabaseSchema.EnsureReadyAsync(db);
        await JobDatabaseSchema.EnsureReadyAsync(db);
        var restored = await db.Jobs.SingleAsync();
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal("Old role", restored.Title);
        Assert.Equal("", restored.Company);
    }

    [Fact]
    public async Task Market_scan_deduplicates_terms_keeps_provider_and_company_and_reports_failures()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CareerForgeDbContext>().UseSqlite(connection).Options;
        var factory = new Factory(options);
        var source1 = new StubFeed("himalayas", false);
        var source2 = new StubFeed("jobicy", true);
        var scanner = new MarketScanService(factory, [source1, source2], TimeProvider.System);

        var first = await scanner.ScanAsync(Profile());
        Assert.True(first.HasFailures);
        Assert.Equal(1, first.Added);
        Assert.Equal(1, first.Sources.Single(s => s.Provider == "himalayas").Added);
        Assert.Equal("PartialFailure", first.Sources.Single(s => s.Provider == "jobicy").Status);

        var second = await scanner.ScanAsync(Profile());
        Assert.Equal(0, second.Added);
        await using var db = factory.CreateDbContext();
        var stored = Assert.Single(await db.Jobs.ToListAsync());
        Assert.Equal("himalayas", stored.Provider);
        Assert.Equal("market", stored.BoardToken);
        Assert.Equal("Acme Engineering", stored.Company);
        Assert.Equal(4, await db.Scans.CountAsync());
    }

    [Fact]
    public void Market_jobs_show_in_briefing_without_an_employer_watchlist()
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var marketJob = new JobPosting
        {
            Provider = "himalayas", BoardToken = "market", Company = "Acme",
            ExternalId = "hash", Title = "Senior .NET Engineer", Location = "Remote — United States",
            JobUrl = "https://himalayas.app/apply/abc",
            FirstSeenAtUtc = now.AddHours(-2), LastSeenAtUtc = now
        };
        var briefing = MorningBriefingBuilder.Build([marketJob], [],
            new EmployerWatchlist(), Profile(), now);
        Assert.Equal(1, briefing.RecentCount);
        Assert.Equal(1, briefing.CandidateCount);
        Assert.Equal("Acme", Assert.Single(briefing.Jobs).Company);
        Assert.Contains(briefing.Sources, source => source.Provider == "jobicy");
        Assert.Contains(briefing.Sources, source => source.Provider == "himalayas");
    }

    [Fact]
    public async Task Remote_disabled_does_not_silently_search_remote_only_feeds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CareerForgeDbContext>().UseSqlite(connection).Options;
        var profile = Profile();
        profile.AllowRemote = false;
        var service = new MarketScanService(new Factory(options), [new StubFeed("himalayas", false)],
            TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ScanAsync(profile));
    }

    [Fact]
    public async Task Expired_himalayas_jobs_are_not_imported_and_stored_expired_jobs_are_pruned()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CareerForgeDbContext>().UseSqlite(connection).Options;
        var factory = new Factory(options);
        await using (var db = factory.CreateDbContext())
        {
            await JobDatabaseSchema.EnsureReadyAsync(db);
            db.Jobs.Add(new JobPosting
            {
                Provider = "himalayas", BoardToken = "market",
                ExternalId = "previous", Title = "Expired developer",
                Location = "Remote — US eligible", JobUrl = "https://himalayas.app/apply/old",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
                FirstSeenAtUtc = DateTime.UtcNow.AddDays(-2),
                LastSeenAtUtc = DateTime.UtcNow.AddDays(-2)
            });
            await db.SaveChangesAsync();
        }

        var scanner = new MarketScanService(factory, [new ExpiredHimalayasFeed()], TimeProvider.System);
        var result = await scanner.ScanAsync(Profile());
        Assert.False(result.HasFailures);
        Assert.Equal(0, result.Added);
        await using var check = factory.CreateDbContext();
        Assert.Empty(await check.Jobs.ToListAsync());
    }

    private sealed class ExpiredHimalayasFeed : IMarketJobFeed
    {
        public string Provider => "himalayas";
        public Task<IReadOnlyList<MarketPosting>> SearchAsync(
            string term, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MarketPosting>>(
            [
                new("himalayas", "newexpired", "Example", "Engineer",
                    "Remote — US", "https://himalayas.app/apply/new",
                    DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-1))
            ]);
    }

    private sealed class StubFeed(string provider, bool fail) : IMarketJobFeed
    {
        public string Provider => provider;
        public Task<IReadOnlyList<MarketPosting>> SearchAsync(
            string term, CancellationToken cancellationToken = default)
        {
            if (fail) throw new HttpRequestException("429 shared allowance exhausted");
            IReadOnlyList<MarketPosting> postings = [new(provider,
                MarketSearchTerms.NormalizeIdentity("external-123"), "Acme Engineering",
                "Senior .NET Engineer", "Remote — United States",
                "https://himalayas.app/apply/abc", null)];
            return Task.FromResult(postings);
        }
    }

    private sealed class Factory(DbContextOptions<CareerForgeDbContext> options) :
        IDbContextFactory<CareerForgeDbContext>
    {
        public CareerForgeDbContext CreateDbContext() => new(options);
    }
}

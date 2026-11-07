using CareerForge.Core;
using CareerForge.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Tests;

public sealed class JobScanServiceTests
{
    [Fact]
    public async Task Repeated_scan_updates_existing_instead_of_creating_duplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CareerForgeDbContext>()
            .UseSqlite(connection).Options;
        var factory = new TestDbFactory(options);
        var service = new JobScanService(factory, new StubFeed(false), TimeProvider.System);

        var first = await service.ScanBoardAsync("example");
        var second = await service.ScanBoardAsync("example");
        Assert.Equal("Succeeded", first.Status);
        Assert.Equal(1, first.Added);
        Assert.Equal(0, second.Added);
        Assert.Equal(1, second.Updated);

        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.Jobs.CountAsync());
        Assert.Equal(2, await db.Scans.CountAsync());
        Assert.Equal("Senior .NET Engineer", (await db.Jobs.SingleAsync()).Title);
    }

    [Fact]
    public async Task Failed_scan_does_not_mutate_jobs_and_is_recorded()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CareerForgeDbContext>().UseSqlite(connection).Options;
        var factory = new TestDbFactory(options);
        var scan = await new JobScanService(factory, new StubFeed(true), TimeProvider.System)
            .ScanBoardAsync("example");
        Assert.Equal("Failed", scan.Status);
        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.Jobs.ToListAsync());
        Assert.Single(await db.Scans.ToListAsync());
    }

    private sealed class StubFeed(bool fail) : IJobFeedClient
    {
        public Task<IReadOnlyList<CollectedJob>> GetJobsAsync(string boardToken,
            CancellationToken cancellationToken = default)
        {
            if (fail) throw new HttpRequestException("Provider unavailable");
            IReadOnlyList<CollectedJob> jobs = [new("123", "Senior .NET Engineer", "NYC",
                "https://job-boards.greenhouse.io/example/jobs/123", null)];
            return Task.FromResult(jobs);
        }
    }

    private sealed class TestDbFactory(DbContextOptions<CareerForgeDbContext> options)
        : IDbContextFactory<CareerForgeDbContext>
    {
        public CareerForgeDbContext CreateDbContext() => new(options);
    }
}

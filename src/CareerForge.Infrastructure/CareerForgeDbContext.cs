using CareerForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareerForge.Infrastructure;

public sealed class CareerForgeDbContext(DbContextOptions<CareerForgeDbContext> options) : DbContext(options)
{
    public DbSet<JobPosting> Jobs => Set<JobPosting>();
    public DbSet<JobScan> Scans => Set<JobScan>();
    public DbSet<JobDescription> JobDescriptions => Set<JobDescription>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var utc = new ValueConverter<DateTime, long>(
            value => value.Ticks,
            ticks => new DateTime(ticks, DateTimeKind.Utc));

        var jobs = builder.Entity<JobPosting>();
        jobs.HasKey(x => x.Id);
        jobs.HasIndex(x => new { x.Provider, x.BoardToken, x.ExternalId }).IsUnique();
        jobs.HasIndex(x => x.LastSeenAtUtc);
        jobs.Property(x => x.FirstSeenAtUtc).HasConversion(utc);
        jobs.Property(x => x.LastSeenAtUtc).HasConversion(utc);
        jobs.Property(x => x.ExpiresAtUtc).HasConversion(utc);
        jobs.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        jobs.Property(x => x.BoardToken).HasMaxLength(80).IsRequired();
        jobs.Property(x => x.Company).HasMaxLength(200).IsRequired();
        jobs.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
        jobs.Property(x => x.Title).HasMaxLength(300).IsRequired();
        jobs.Property(x => x.Location).HasMaxLength(200).IsRequired();
        jobs.Property(x => x.JobUrl).HasMaxLength(2048).IsRequired();

        var description = builder.Entity<JobDescription>();
        description.HasKey(x => x.JobId);
        description.Property(x => x.Text).IsRequired();
        description.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        description.Property(x => x.SourceUrl).HasMaxLength(2048).IsRequired();
        description.Property(x => x.RetrievedAtUtc).HasConversion(utc);

        var scans = builder.Entity<JobScan>();
        scans.HasKey(x => x.Id);
        scans.HasIndex(x => x.StartedAtUtc);
        scans.Property(x => x.StartedAtUtc).HasConversion(utc);
        scans.Property(x => x.FinishedAtUtc).HasConversion(utc);
        scans.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        scans.Property(x => x.BoardToken).HasMaxLength(80).IsRequired();
        scans.Property(x => x.Status).HasMaxLength(20).IsRequired();
        scans.Property(x => x.Error).HasMaxLength(1000);
    }
}

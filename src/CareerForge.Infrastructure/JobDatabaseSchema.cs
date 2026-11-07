using Microsoft.EntityFrameworkCore;

namespace CareerForge.Infrastructure;

/// <summary>
/// Additive v3 SQLite upgrade for existing PR1/PR2 databases created with EnsureCreated.
/// No existing Jobs, Scans, or user data is dropped or rewritten.
/// </summary>
public static class JobDatabaseSchema
{
    public static async Task EnsureReadyAsync(CareerForgeDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (!db.Database.IsSqlite()) return;

        // CREATE TABLE IF NOT EXISTS handles older SQLite databases where EnsureCreated
        // does not create newly introduced EF entity tables.
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "JobDescriptions" (
                "JobId" TEXT NOT NULL CONSTRAINT "PK_JobDescriptions" PRIMARY KEY,
                "Text" TEXT NOT NULL,
                "Sha256" TEXT NOT NULL,
                "SourceUrl" TEXT NOT NULL,
                "RetrievedAtUtc" INTEGER NOT NULL
            )
            """, cancellationToken);
    }
}

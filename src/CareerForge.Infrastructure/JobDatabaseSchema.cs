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

        // PR1-PR4 created Jobs without Company. Add the column without recreating tables
        // and without dropping previously collected local data.
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        bool hasCompany = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('Jobs')";
            await using var rows = await command.ExecuteReaderAsync(cancellationToken);
            while (await rows.ReadAsync(cancellationToken))
            {
                if (string.Equals(rows.GetString(1), "Company", StringComparison.OrdinalIgnoreCase))
                {
                    hasCompany = true;
                    break;
                }
            }
        }
        if (!hasCompany)
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Jobs\" ADD COLUMN \"Company\" TEXT NOT NULL DEFAULT ''",
                cancellationToken);

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

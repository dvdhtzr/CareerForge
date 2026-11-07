using CareerForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

internal static class MarketCommands
{
    public static async Task<int> ScanAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: scan market");
            return 2;
        }

        var profile = await services.GetRequiredService<PrivateProfileStore>().LoadAsync();
        if (profile is null)
        {
            Console.Error.WriteLine("No local career profile. Run: profile init");
            return 2;
        }

        try
        {
            var batch = await services.GetRequiredService<MarketScanService>().ScanAsync(profile);
            foreach (var scan in batch.Sources)
            {
                Console.WriteLine($"{scan.Provider}: {scan.Status} | retrieved={scan.Found} " +
                    $"added={scan.Added} refreshed={scan.Updated} skipped={scan.Skipped}");
                if (scan.Error is not null) Console.Error.WriteLine("  " + scan.Error);
            }
            Console.WriteLine($"MARKET DISCOVERY: {batch.Added} newly observed roles across " +
                $"{batch.Sources.Count} public source(s). Not exhaustive coverage.");
            return batch.HasFailures ? 1 : 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException
            or IOException or Microsoft.Data.Sqlite.SqliteException or HttpRequestException)
        {
            Console.Error.WriteLine("Market scan failed: " + ex.Message);
            return 1;
        }
    }
}

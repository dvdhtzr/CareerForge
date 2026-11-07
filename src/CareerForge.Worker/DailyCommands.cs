using CareerForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

internal static class DailyCommands
{
    public static async Task<int> ScanWatchlistAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 0)
            return Usage();
        var result = await ScanInternalAsync(services);
        return result ? 0 : 1;
    }

    public static async Task<int> ReportAsync(IServiceProvider services, string[] args)
    {
        var days = ParseDays(args);
        if (days is null) return Usage();
        try
        {
            var report = await services.GetRequiredService<MorningBriefingService>().BuildAsync(days.Value);
            var path = await MorningBriefingService.SaveLatestAsync(report);
            Console.WriteLine(MorningBriefingService.ToPlainText(report));
            Console.WriteLine($"Saved locally: {path}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or Microsoft.Data.Sqlite.SqliteException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine("Report failed: " + ex.Message);
            return 1;
        }
    }

    public static async Task<int> DailyAsync(IServiceProvider services, string[] args)
    {
        var days = ParseDays(args);
        if (days is null) return Usage();
        // Profile-based market search is the default. No employer watchlist required.
        var marketResult = await MarketCommands.ScanAsync(services, []);
        // Optional explicit employer watchlist extends coverage; never blocks market scanning.
        var watchlist = await services.GetRequiredService<EmployerWatchlistStore>().LoadAsync();
        var watchlistResult = watchlist.Sources.Any(source => source.Enabled)
            ? await ScanInternalAsync(services) : true;
        // Always generate a report, including when one of the feeds fails.
        var reportCode = await ReportAsync(services, args);
        return marketResult == 0 && watchlistResult && reportCode == 0 ? 0 : 1;
    }

    private static int? ParseDays(string[] args)
    {
        if (args.Length == 0) return 7;
        return args.Length == 2 && args[0] == "--days" &&
            int.TryParse(args[1], out var value) && value is >= 1 and <= 30 ? value : null;
    }

    private static async Task<bool> ScanInternalAsync(IServiceProvider services)
    {
        try
        {
            var watchlist = await services.GetRequiredService<EmployerWatchlistStore>().LoadAsync();
            var scanner = services.GetRequiredService<JobScanService>();
            var coordinator = new WatchlistScanCoordinator(
                (board, descriptions, ct) => scanner.ScanBoardAsync(board, descriptions, ct));
            var batch = await coordinator.RunAsync(watchlist);
            foreach (var result in batch.Results)
            {
                var scan = result.Scan;
                Console.WriteLine($"{result.BoardToken}: {scan.Status} | found={scan.Found} " +
                    $"added={scan.Added} refreshed={scan.Updated} skipped={scan.Skipped}");
                if (scan.Status != "Succeeded") Console.Error.WriteLine("  Error: " + scan.Error);
            }
            Console.WriteLine($"Watchlist scan: {batch.Results.Count} boards, {batch.Added} new postings.");
            return !batch.HasFailures;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException or
            System.Text.Json.JsonException)
        {
            Console.Error.WriteLine("Watchlist scan failed: " + ex.Message);
            return false;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("""
            Usage:
              scan watchlist
              report [--days 1..30]
              daily [--days 1..30]
            Configure sources locally using the 'sources' command first.
            """);
        return 2;
    }
}

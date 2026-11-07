using CareerForge.Core;
using CareerForge.Infrastructure;

internal static class WatchlistCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0) return Usage();

        var store = new EmployerWatchlistStore();
        try
        {
            var watchlist = await store.LoadAsync();
            switch (args[0].ToLowerInvariant())
            {
                case "list" when args.Length == 1:
                    if (watchlist.Sources.Count == 0)
                        Console.WriteLine("No employer boards configured. Use: sources add <board-token> [company-name]");
                    foreach (var source in watchlist.Sources)
                        Console.WriteLine($"{source.BoardToken} | {source.DisplayName} | " +
                            $"{(source.Enabled ? "enabled" : "disabled")} | " +
                            $"descriptions={(source.IncludeDescriptions ? "opted-in" : "off")}");
                    return 0;

                case "add" when args.Length is 2 or 3:
                    watchlist = EmployerWatchlistEditor.Add(watchlist, args[1],
                        args.Length == 3 ? args[2] : null);
                    break;

                case "remove" when args.Length == 2:
                    watchlist = EmployerWatchlistEditor.Remove(watchlist, args[1]);
                    break;

                case "enable" when args.Length == 2:
                    watchlist = EmployerWatchlistEditor.SetEnabled(watchlist, args[1], true);
                    break;

                case "disable" when args.Length == 2:
                    watchlist = EmployerWatchlistEditor.SetEnabled(watchlist, args[1], false);
                    break;

                case "descriptions" when args.Length == 3 &&
                    (args[2].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                     args[2].Equals("off", StringComparison.OrdinalIgnoreCase)):
                    var include = args[2].Equals("on", StringComparison.OrdinalIgnoreCase);
                    if (include)
                        Console.WriteLine("Opt-in reminder: review each employer's applicable source/usage terms before processing descriptions.");
                    watchlist = EmployerWatchlistEditor.SetDescriptions(watchlist, args[1], include);
                    break;

                default:
                    return Usage();
            }

            await store.SaveAsync(watchlist);
            Console.WriteLine($"Employer watchlist updated locally: {LocalDataPaths.DirectoryPath}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or
            IOException or UnauthorizedAccessException or InvalidOperationException or
            System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"Watchlist not updated: {ex.Message}");
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("""
            Usage:
              sources list
              sources add <board-token> [company-name]
              sources remove <board-token>
              sources enable <board-token>
              sources disable <board-token>
              sources descriptions <board-token> on|off
            All changes stay in local user data, never the Git repository.
            """);
        return 2;
    }
}

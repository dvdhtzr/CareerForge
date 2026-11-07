namespace CareerForge.Core;

/// <summary>User-selected employer boards. The watchlist is private and is never seeded from source control.</summary>
public sealed class EmployerWatchlist
{
    public List<EmployerSource> Sources { get; set; } = [];
}

public sealed record EmployerSource(
    string BoardToken,
    string DisplayName,
    bool Enabled = true,
    bool IncludeDescriptions = false);

public static class EmployerWatchlistEditor
{
    public const int MaxBoards = 75;

    public static void Validate(EmployerWatchlist watchlist)
    {
        ArgumentNullException.ThrowIfNull(watchlist);
        if (watchlist.Sources is null || watchlist.Sources.Count > MaxBoards)
            throw new InvalidDataException($"An employer watchlist must contain at most {MaxBoards} boards.");

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in watchlist.Sources)
        {
            if (source is null || !JobNormalizer.IsValidBoardToken(source.BoardToken) ||
                string.IsNullOrWhiteSpace(source.DisplayName) ||
                source.DisplayName.Length > 80 ||
                source.DisplayName.Any(char.IsControl) ||
                !tokens.Add(source.BoardToken))
                throw new InvalidDataException("Watchlist contains an invalid or duplicate Greenhouse board.");
        }
    }

    public static EmployerWatchlist Add(EmployerWatchlist original, string boardToken, string? displayName)
    {
        Validate(original);
        if (!JobNormalizer.IsValidBoardToken(boardToken))
            throw new ArgumentException("Invalid Greenhouse board token.", nameof(boardToken));
        var normalized = boardToken.ToLowerInvariant();
        if (original.Sources.Any(s => s.BoardToken.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Board '{normalized}' is already on the watchlist.");
        var label = string.IsNullOrWhiteSpace(displayName) ? normalized : displayName.Trim();
        var result = new EmployerWatchlist
        {
            Sources = [.. original.Sources, new EmployerSource(normalized, label)]
        };
        Validate(result);
        return result;
    }

    public static EmployerWatchlist Remove(EmployerWatchlist original, string boardToken)
    {
        Validate(original);
        RequireExisting(original, boardToken);
        return new EmployerWatchlist
        {
            Sources = original.Sources
                .Where(s => !s.BoardToken.Equals(boardToken, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
    }

    public static EmployerWatchlist SetEnabled(EmployerWatchlist original, string boardToken, bool enabled) =>
        Change(original, boardToken, source => source with { Enabled = enabled });

    public static EmployerWatchlist SetDescriptions(EmployerWatchlist original, string boardToken, bool include) =>
        Change(original, boardToken, source => source with { IncludeDescriptions = include });

    private static EmployerWatchlist Change(EmployerWatchlist original, string boardToken, Func<EmployerSource, EmployerSource> change)
    {
        Validate(original);
        RequireExisting(original, boardToken);
        return new EmployerWatchlist
        {
            Sources = original.Sources.Select(source =>
                source.BoardToken.Equals(boardToken, StringComparison.OrdinalIgnoreCase)
                    ? change(source) : source).ToList()
        };
    }

    private static void RequireExisting(EmployerWatchlist original, string boardToken)
    {
        if (string.IsNullOrWhiteSpace(boardToken) ||
            !original.Sources.Any(s => s.BoardToken.Equals(boardToken, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Board '{boardToken}' is not on the watchlist.");
    }
}

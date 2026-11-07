using System.Security.Cryptography;
using System.Text;

namespace CareerForge.Core;

/// <summary>Opportunity from a reviewed public job index. No employer token is needed.</summary>
public sealed record MarketPosting(
    string Provider, string ExternalId, string Company, string Title,
    string Location, string JobUrl, DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc = null);

public interface IMarketJobFeed
{
    string Provider { get; }
    Task<IReadOnlyList<MarketPosting>> SearchAsync(string term, CancellationToken cancellationToken = default);
}

/// <summary>Bounded searches derived exclusively from the user's already-entered career profile.</summary>
public static class MarketSearchTerms
{
    public static IReadOnlyList<string> FromProfile(CareerProfile profile, int maxTerms = 4)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (maxTerms is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(maxTerms));
        var terms = profile.TargetTitles.Concat(profile.SkillKeywords)
            .Select(term => term.Trim())
            .Where(term => term.Length is >= 2 and <= 50)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxTerms)
            .ToArray();
        if (terms.Length == 0)
            throw new InvalidDataException("Profile lacks usable job title or skill search terms.");
        return terms;
    }

    public static string NormalizeIdentity(string value)
    {
        var id = value.Trim();
        if (id.Length == 0) return "";
        // Never truncate IDs: hashing preserves distinct identities under the DB length cap.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
    }
}

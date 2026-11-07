using System.Text.RegularExpressions;

namespace CareerForge.Core;

/// <summary>
/// Deterministic evidence gate. Model claims must quote the collected description verbatim
/// apart from whitespace differences, and must not invent profile skills.
/// </summary>
public static partial class RequirementEvidenceValidator
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static bool ContainsQuote(string description, string quote)
    {
        if (string.IsNullOrWhiteSpace(quote) || quote.Length is < 12 or > 400)
            return false;

        var plain = Whitespace().Replace(description, " ").Trim();
        var candidate = Whitespace().Replace(quote, " ").Trim();
        return plain.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<RequirementClaim> Validate(
        string description, IReadOnlyList<string> profileSkills, IReadOnlyList<RequirementClaim> claims)
    {
        if (claims.Count is < 1 or > 8)
            throw new InvalidDataException("Expected 1-8 quoted requirements.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in claims)
        {
            if (!ContainsQuote(description, claim.Quote))
                throw new InvalidDataException("A generated requirement quote was not found in the source description.");

            var normalizedQuote = Whitespace().Replace(claim.Quote, " ").Trim();
            if (!seen.Add(normalizedQuote))
                throw new InvalidDataException("Duplicate source quote in generated requirements.");

            if (claim.ProfileSkill is not null)
            {
                if (!profileSkills.Contains(claim.ProfileSkill, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException("A claimed skill was not present in the user profile.");

                if (!normalizedQuote.Contains(claim.ProfileSkill, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A claimed skill did not appear in its cited requirement.");
            }
        }
        return claims;
    }
}

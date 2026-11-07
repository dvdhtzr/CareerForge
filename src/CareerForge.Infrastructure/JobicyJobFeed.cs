using System.Text.Json;
using CareerForge.Core;

namespace CareerForge.Infrastructure;

/// <summary>Public Jobicy feed for remote US-eligible jobs. Always use the public source URL.</summary>
public sealed class JobicyJobFeed(HttpClient httpClient) : IMarketJobFeed
{
    public string Provider => "jobicy";

    public async Task<IReadOnlyList<MarketPosting>> SearchAsync(
        string term, CancellationToken cancellationToken = default)
    {
        // Public Jobicy tag filter requires 3-50 characters.
        if (string.IsNullOrWhiteSpace(term) || term.Length > 50)
            throw new ArgumentException("Invalid search term.", nameof(term));
        if (term.Trim().Length < 3) return [];

        var found = new Dictionary<string, MarketPosting>(StringComparer.Ordinal);
        string? cursor = null;
        for (var page = 0; page < 2; page++)
        {
            var url = "https://jobicy.com/api/jobs?count=100&geo=usa&tag=" +
                Uri.EscapeDataString(term) +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
            using var doc = await MarketHttpJson.GetAsync(httpClient, url, cancellationToken);
            if (doc.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
                throw new InvalidDataException("Jobicy reported unsuccessful search.");
            if (!doc.RootElement.TryGetProperty("jobs", out var jobs) ||
                jobs.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Jobicy API returned no jobs array.");
            foreach (var item in jobs.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("id", out var rawId)) continue;
                var identity = rawId.ValueKind == JsonValueKind.Number ? rawId.GetRawText() :
                    rawId.ValueKind == JsonValueKind.String ? rawId.GetString() : null;
                var id = string.IsNullOrWhiteSpace(identity) ? "" :
                    MarketSearchTerms.NormalizeIdentity(identity);
                var title = MarketHttpJson.NormalizedText(MarketHttpJson.String(item, "jobTitle"));
                var company = MarketHttpJson.NormalizedText(MarketHttpJson.String(item, "companyName"), 200);
                var location = MarketHttpJson.NormalizedText(MarketHttpJson.String(item, "jobGeo"), 200);
                var link = MarketHttpJson.HttpsUrl(MarketHttpJson.String(item, "url"));
                if (id.Length == 0 || title.Length == 0 || link.Length == 0) continue;
                found[id] = new MarketPosting(Provider, id, company, title,
                    location.Length == 0 ? "Remote — eligibility unspecified" : "Remote — " + location,
                    link, MarketHttpJson.Date(item, "pubDate"));
            }
            cursor = MarketHttpJson.String(doc.RootElement, "nextCursor");
            if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 1024) break;
        }
        return found.Values.ToArray();
    }
}

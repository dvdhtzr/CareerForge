using System.Text.Json;
using CareerForge.Core;

namespace CareerForge.Infrastructure;

/// <summary>Keyword search across Himalayas' US-eligible remote postings, not individual employers.</summary>
public sealed class HimalayasJobFeed(HttpClient httpClient) : IMarketJobFeed
{
    public string Provider => "himalayas";

    public async Task<IReadOnlyList<MarketPosting>> SearchAsync(
        string term, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length > 50)
            throw new ArgumentException("Invalid search term.", nameof(term));

        var found = new Dictionary<string, MarketPosting>(StringComparer.Ordinal);
        // Small bounded search to respect the no-key shared public allowance.
        for (var page = 1; page <= 2; page++)
        {
            var url = "https://himalayas.app/jobs/api/search?q=" +
                Uri.EscapeDataString(term) + "&country=US&sort=recent&page=" + page;
            using var doc = await MarketHttpJson.GetAsync(httpClient, url, cancellationToken);
            if (!doc.RootElement.TryGetProperty("jobs", out var jobs) ||
                jobs.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Himalayas API returned no jobs array.");

            foreach (var item in jobs.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var guid = MarketHttpJson.String(item, "guid");
                var id = string.IsNullOrWhiteSpace(guid) ? "" : MarketSearchTerms.NormalizeIdentity(guid);
                var company = MarketHttpJson.NormalizedText(MarketHttpJson.String(item, "companyName"), 200);
                var title = MarketHttpJson.NormalizedText(MarketHttpJson.String(item, "title"));
                var link = MarketHttpJson.HttpsUrl(MarketHttpJson.String(item, "applicationLink"));
                if (id.Length == 0 || title.Length == 0 || link.Length == 0) continue;

                var location = "Remote — US eligible";
                if (item.TryGetProperty("locationRestrictions", out var restrictions) &&
                    restrictions.ValueKind == JsonValueKind.Array)
                {
                    var regions = restrictions.EnumerateArray()
                        .Where(r => r.ValueKind == JsonValueKind.String)
                        .Select(r => MarketHttpJson.NormalizedText(r.GetString(), 50))
                        .Where(r => r.Length > 0).Take(8).ToArray();
                    location = regions.Length == 0 ? "Remote — worldwide" :
                        "Remote — " + string.Join(", ", regions);
                }

                found[id] = new MarketPosting(Provider, id, company, title, location, link,
                    MarketHttpJson.Date(item, "pubDate"));
            }
            if (jobs.GetArrayLength() < 20) break;
        }
        return found.Values.ToArray();
    }
}

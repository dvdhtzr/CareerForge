using System.Net;
using System.Text.Json;

namespace CareerForge.Infrastructure;

/// <summary>Fixed-host callers only. Parse bounded JSON; never follow provider redirects.</summary>
internal static class MarketHttpJson
{
    public static async Task<JsonDocument> GetAsync(HttpClient client, string url, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        const int maxBytes = 4_000_000;
        if (response.Content.Headers.ContentLength > maxBytes)
            throw new InvalidDataException("Market source response exceeds 4MB.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidDataException("Market source response exceeds 4MB.");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
    }

    public static string? String(JsonElement element, string field)
    {
        if (!element.TryGetProperty(field, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    public static DateTimeOffset? Date(JsonElement element, string field)
    {
        if (!element.TryGetProperty(field, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unix))
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(unix); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), out var parsed))
            return parsed.ToUniversalTime();
        return null;
    }

    public static string NormalizedText(string? value, int length = 300)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = WebUtility.HtmlDecode(value).Trim();
        var normalized = string.Join(' ', trimmed.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length > length ? normalized[..length] : normalized;
    }

    public static string HttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048 ||
            !string.Equals(url, url.Trim(), StringComparison.Ordinal) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps || parsed.UserInfo.Length > 0)
            return "";
        // Provider attribution requirements: keep the original applicationLink byte-for-byte.
        return url;
    }
}

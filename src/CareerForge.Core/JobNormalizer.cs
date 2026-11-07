using System.Net;
using System.Text.RegularExpressions;

namespace CareerForge.Core;

public static partial class JobNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,80}$")]
    private static partial Regex BoardPattern();

    public static bool IsValidBoardToken(string? boardToken) =>
        boardToken is not null && BoardPattern().IsMatch(boardToken);

    public static bool TryNormalize(CollectedJob source, out NormalizedJob? normalized)
    {
        normalized = null;
        var id = Clean(source.ExternalId, 100);
        var title = Clean(WebUtility.HtmlDecode(source.Title), 300);
        var location = Clean(WebUtility.HtmlDecode(source.Location ?? "Remote / unspecified"), 200);
        var url = (source.JobUrl ?? "").Trim();
        if (id.Length == 0 || title.Length == 0 || url.Length > 2048 ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0)
            return false;

        normalized = new NormalizedJob(id, title, location, uri.AbsoluteUri, source.SourceUpdatedAtUtc);
        return true;
    }

    private static string Clean(string? value, int maxLength)
    {
        var result = Whitespace().Replace(value ?? "", " ").Trim();
        return result.Length > maxLength ? result[..maxLength] : result;
    }
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CareerForge.Infrastructure;

/// <summary>Converts Greenhouse's HTML-entity-escaped description into bounded plain text.</summary>
public static partial class JobDescriptionText
{
    [GeneratedRegex(@"[^\S\r\n]+")]
    private static partial Regex InlineWhitespace();

    public static string? Extract(string? encodedHtml)
    {
        if (string.IsNullOrWhiteSpace(encodedHtml)) return null;
        if (encodedHtml.Length > 200_000)
            throw new InvalidDataException("Source description exceeded 200,000 characters.");

        var html = encodedHtml;
        // Greenhouse sometimes returns twice-escaped HTML entities.
        for (var i = 0; i < 2; i++)
            html = WebUtility.HtmlDecode(html);

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var prohibited = document.DocumentNode.SelectNodes(
            "//script|//style|//noscript|//iframe|//form|//svg|//comment()");
        if (prohibited is not null)
            foreach (var node in prohibited) node.Remove();

        var breaks = document.DocumentNode.SelectNodes(
            "//br|//p|//div|//li|//ul|//ol|//h1|//h2|//h3|//h4|//table|//tr|//section");
        if (breaks is not null)
            foreach (var node in breaks)
                node.ParentNode?.InsertBefore(document.CreateTextNode("\n"), node);

        var text = WebUtility.HtmlDecode(document.DocumentNode.InnerText)
            .Replace('\u00a0', ' ').Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n')
            .Select(line => InlineWhitespace().Replace(line.Trim(), " "))
            .Where(line => line.Length > 0);
        text = string.Join("\n", lines).Trim();
        if (text.Length > 40_000)
            throw new InvalidDataException("Plain description exceeded 40,000 characters.");
        return text.Length == 0 ? null : text;
    }

    public static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

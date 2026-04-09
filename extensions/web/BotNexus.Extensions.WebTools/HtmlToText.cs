using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace BotNexus.Extensions.WebTools;

/// <summary>
/// Converts HTML to readable text using regex-based parsing.
/// Strips unwanted elements, converts formatting to markdown, and normalizes whitespace.
/// </summary>
internal static partial class HtmlToText
{
    [GeneratedRegex(@"<script[^>]*?>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"<style[^>]*?>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleTagRegex();

    [GeneratedRegex(@"<(nav|footer|header)[^>]*?>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NavigationTagRegex();

    [GeneratedRegex(@"<a\s+[^>]*?href=[""']([^""']+)[""'][^>]*?>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkTagRegex();

    [GeneratedRegex(@"<(br|p|div|li|h[1-6])[^>]*?/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.None)]
    private static partial Regex AllTagsRegex();

    [GeneratedRegex(@"\n\s*\n\s*\n+", RegexOptions.None)]
    private static partial Regex MultipleNewlinesRegex();

    [GeneratedRegex(@"[ \t]+", RegexOptions.None)]
    private static partial Regex MultipleSpacesRegex();

    /// <summary>
    /// Converts HTML to readable text with markdown-style links.
    /// </summary>
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = html;

        // Remove script, style, nav, header, footer blocks
        text = ScriptTagRegex().Replace(text, string.Empty);
        text = StyleTagRegex().Replace(text, string.Empty);
        text = NavigationTagRegex().Replace(text, string.Empty);

        // Convert links to markdown format: <a href="url">text</a> → [text](url)
        text = LinkTagRegex().Replace(text, match =>
        {
            var url = match.Groups[1].Value;
            var linkText = match.Groups[2].Value;
            // Strip any nested tags from link text
            linkText = AllTagsRegex().Replace(linkText, string.Empty).Trim();
            return string.IsNullOrWhiteSpace(linkText) ? url : $"[{linkText}]({url})";
        });

        // Convert block-level elements to newlines
        text = BlockTagRegex().Replace(text, "\n");

        // Strip all remaining HTML tags
        text = AllTagsRegex().Replace(text, string.Empty);

        // Decode HTML entities
        text = HttpUtility.HtmlDecode(text);

        // Normalize whitespace
        text = MultipleSpacesRegex().Replace(text, " "); // collapse spaces
        text = MultipleNewlinesRegex().Replace(text, "\n\n"); // max 2 newlines

        // Split into lines, trim each, and rejoin
        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line));

        return string.Join('\n', lines);
    }
}

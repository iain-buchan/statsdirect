using StatsDirect.UI.HtmlHelp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StatsDirect.UI.Learning;

/// <summary>Bounded text and links from the packaged StatsDirect Help index for worksheet questions.</summary>
internal static class WorksheetAIHelp
{
    internal const string SearchTool = "statsdirect_search_help";
    internal const string ReadTopicTool = "statsdirect_read_help_topic";
    private const string IndexPrefix = "window.StatsDirectSearch=";
    private const int MaxFileBytes = 2_000_000;
    private const int MaxTopicPageCharacters = 10_000;
    private const string PublicHelp = "https://www.statsdirect.com/help";
    private static readonly Regex Words = new(@"[\p{L}\p{N}]{2,}", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> CommonWords = new(
        ["about", "and", "are", "can", "could", "does", "from", "have", "how", "into", "its", "please", "should", "that", "the", "this", "what", "when", "which", "with", "would", "your"],
        StringComparer.Ordinal);

    // A short starting point based on the Statistical Method Selection Help.
    internal static JsonObject MethodSelectionGuide() => new()
    {
        ["title"] = "Statistical Method Selection",
        ["url"] = "https://www.statsdirect.com/help/methods/method_selection.htm",
        ["text"] = """
            Start with the goal: compare independent groups, compare paired measurements, or examine an association on the same people or units. Establish the measurement scale and study design. When size, distribution or other conditions affect the choice, use what the user supplied and what the approved selection clearly establishes. Ask for the remaining details only.
            """,
    };

    private static string TopicText(string html, Uri page)
    {
        const RegexOptions ignoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var article = Regex.Match(html, @"<article\b[^>]*>(.*?)</article\s*>", ignoreCase | RegexOptions.Singleline);
        string text = Regex.Replace(article.Groups[1].Value, @"<!--.*?-->|<(footer|script|style|svg)\b[^>]*>.*?</\1\s*>",
            "", ignoreCase | RegexOptions.Singleline);
        string[] parts = Regex.Split(text, @"<pre\b[^>]*>(.*?)</pre\s*>", ignoreCase | RegexOptions.Singleline);
        return string.Join("\n\n", parts.Select((part, index) => index % 2 == 0
            ? ProseText(part, page)
            : WebUtility.HtmlDecode(Regex.Replace(part, "<[^>]+>", "")).Trim())
            .Where(part => part.Length > 0));
    }

    private static string ProseText(string text, Uri page)
    {
        const RegexOptions ignoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        // Retain lists, tables and internal Help link targets in plain text.
        text = Regex.Replace(text, @"\s+", " ");
        text = Regex.Replace(text, """<a\b[^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*>(.*?)</a\s*>""", link =>
            Uri.TryCreate(page, WebUtility.HtmlDecode(link.Groups[1].Value), out Uri target)
                && target.GetLeftPart(UriPartial.Authority) == page.GetLeftPart(UriPartial.Authority)
                && target.AbsolutePath.StartsWith("/help/", StringComparison.Ordinal)
                ? link.Groups[2].Value + " (" + target.AbsoluteUri + ")"
                : link.Groups[2].Value, ignoreCase);
        text = Regex.Replace(text, @"<li\b[^>]*>", "\n- ", ignoreCase);
        text = Regex.Replace(text, @"</t[dh]\s*>", " | ", ignoreCase);
        text = Regex.Replace(text, @"</?(?:br|p|li|h[1-6]|tr|div|pre)\b[^>]*>", "\n", ignoreCase);
        text = WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", ""));
        return string.Join("\n", text.Split('\n').Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Any(c => c is not (' ' or '|'))));
    }

    internal static JsonArray Search(string directory, string query)
    {
        var topics = new JsonArray();
        try
        {
            using var parsed = ReadIndex(directory);
            JsonElement documents = parsed.RootElement.GetProperty("documents");
            JsonElement terms = parsed.RootElement.GetProperty("terms");
            int count = documents.GetArrayLength();
            var scores = new Dictionary<int, double>();
            foreach (string word in Words.Matches(query.ToLowerInvariant()).Select(m => m.Value).Where(w => !CommonWords.Contains(w)).Distinct().Take(24))
            {
                if (!terms.TryGetProperty(word, out JsonElement postings)) continue;
                double scale = 1 + Math.Log((count + 1.0) / (postings.GetArrayLength() + 1));
                foreach (JsonElement posting in postings.EnumerateArray())
                {
                    int doc = posting[0].GetInt32();
                    if (doc >= 0 && doc < count)
                        scores[doc] = scores.GetValueOrDefault(doc) + posting[1].GetInt32() * scale;
                }
            }

            var catalog = new HelpCatalog(directory);
            foreach (int doc in scores.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Select(pair => pair.Key))
            {
                if (topics.Count >= 3) break;
                AddTopic(topics, catalog, documents[doc]);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or System.Xml.XmlException)
        {
            // A damaged or absent help bundle must not prevent asking a question.
        }
        return topics;
    }

    internal static JsonObject ReadTopic(string directory, string topic, int offset = 0)
    {
        if (!IsTopicId(topic) || offset < 0)
            throw new ArgumentException("Choose a topic ID returned by StatsDirect Help search and a valid offset.");

        try
        {
            using var parsed = ReadIndex(directory);
            JsonElement document = parsed.RootElement.GetProperty("documents").EnumerateArray()
                .FirstOrDefault(entry => entry.GetProperty("url").GetString() == topic);
            if (document.ValueKind == JsonValueKind.Undefined)
                throw new ArgumentException("Choose a topic ID returned by StatsDirect Help search.");

            var page = new Uri(PublicHelp + "/" + string.Join("/", topic.Split('/').Select(Uri.EscapeDataString)));
            string text = TopicText(ReadPackagedFile(directory, topic), page);
            if (text.Length == 0)
                throw new InvalidDataException("The Help topic has no readable article.");
            if (offset >= text.Length || char.IsLowSurrogate(text[offset]))
                throw new ArgumentException("Use the next offset returned for this Help topic.");

            int end = offset + Math.Min(text.Length - offset, MaxTopicPageCharacters);
            if (end < text.Length)
            {
                int boundary = text.LastIndexOf('\n', end - 1, MaxTopicPageCharacters / 2);
                if (boundary < 0) boundary = text.LastIndexOf(' ', end - 1, MaxTopicPageCharacters / 2);
                if (boundary >= 0) end = boundary + 1;
                else if (char.IsHighSurrogate(text[end - 1])) end--;
            }
            return new JsonObject
            {
                ["topic"] = topic,
                ["title"] = document.GetProperty("title").GetString().Limit(160),
                ["url"] = page.AbsoluteUri,
                ["text"] = text[offset..end],
                ["offset"] = offset,
                ["nextOffset"] = end == text.Length ? null : JsonValue.Create(end),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidOperationException("The packaged StatsDirect Help topic is unavailable.");
        }
    }

    private static bool IsTopicId(string topic) =>
        topic is { Length: > 0 and <= 300 } && topic.IndexOfAny(['\\', ':', '#', '?', '%', '\0']) < 0
        && topic.Split('/').All(part => part.Length > 0 && part is not ("." or ".."))
        && Path.GetExtension(topic) is ".htm" or ".html";

    private static JsonDocument ReadIndex(string directory)
    {
        string index = ReadPackagedFile(directory, "search-index.js").Trim();
        if (!index.StartsWith(IndexPrefix, StringComparison.Ordinal) || !index.EndsWith(';'))
            throw new InvalidDataException("The packaged Help index is unreadable.");
        return JsonDocument.Parse(index[IndexPrefix.Length..^1]);
    }

    private static string ReadPackagedFile(string directory, string relative)
    {
        string path = Path.GetFullPath(directory);
        foreach (string segment in relative.Split('/'))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Packaged Help cannot follow filesystem redirects.");
            path = Path.Combine(path, segment);
        }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Packaged Help cannot follow filesystem redirects.");
        return ReadBounded(path);
    }

    private static string ReadBounded(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > MaxFileBytes) throw new InvalidDataException("Packaged help file is too large.");
        using var reader = new StreamReader(file);
        return reader.ReadToEnd();
    }

    private static void AddTopic(JsonArray topics, HelpCatalog catalog, JsonElement document)
    {
        // The requested keywords are matched to packaged IDs. Only the installed index chooses page paths.
        string topic = document.GetProperty("url").GetString();
        if (!IsTopicId(topic)) return;
        var page = catalog.Resolve(topic);
        if (page == null) return;
        string text = document.GetProperty("excerpt").GetString();
        try
        {
            string html = ReadPackagedFile(catalog.Directory, topic);
            int article = html.IndexOf("<article", StringComparison.OrdinalIgnoreCase);
            int end = article < 0 ? -1 : html.IndexOf("</article>", article, StringComparison.OrdinalIgnoreCase);
            if (end > article)
            {
                string content = Regex.Replace(html[article..end], @"<(script|style|svg)\b[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                text = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(content, "<[^>]+>", " ")), @"\s+", " ").Trim();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { /* The index excerpt remains available. */ }
        topics.Add(new JsonObject
        {
            ["title"] = document.GetProperty("title").GetString().Limit(160),
            ["topic"] = topic,
            ["url"] = PublicHelp + page.AbsolutePath + page.Fragment,
            ["text"] = text.Limit(2400),
        });
    }
}

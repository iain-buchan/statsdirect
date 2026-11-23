using StatsDirect.UI.HtmlHelp;
using StatsDirect.Utilities;
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
    private const string IndexPrefix = "window.StatsDirectSearch=";
    private const string WorksheetTopic = "basics/worksheet.htm";
    private const int MaxFileBytes = 2_000_000;
    private static readonly Regex Words = new(@"[\p{L}\p{N}]{2,}", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> CommonWords = new(
        ["about", "and", "are", "can", "could", "does", "from", "have", "how", "into", "its", "please", "should", "that", "the", "this", "what", "when", "which", "with", "would", "your"],
        StringComparer.Ordinal);

    internal static JsonArray Search(string directory, string query, bool includeWorksheet = false)
    {
        var topics = new JsonArray();
        try
        {
            string index = ReadBounded(Path.Combine(directory, "search-index.js")).Trim();
            if (!index.StartsWith(IndexPrefix, StringComparison.Ordinal) || !index.EndsWith(';')) return topics;

            using var parsed = JsonDocument.Parse(index[IndexPrefix.Length..^1]);
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
            if (includeWorksheet)
            {
                JsonElement intro = documents.EnumerateArray().FirstOrDefault(d => d.GetProperty("url").GetString() == WorksheetTopic);
                if (intro.ValueKind != JsonValueKind.Undefined) AddTopic(topics, catalog, intro);
            }
            foreach (int doc in scores.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Select(pair => pair.Key))
            {
                if (topics.Count >= 3) break;
                if (includeWorksheet && documents[doc].GetProperty("url").GetString() == WorksheetTopic) continue;
                AddTopic(topics, catalog, documents[doc]);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or System.Xml.XmlException)
        {
            // A damaged or absent help bundle must not prevent asking a question.
        }
        return topics;
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
        var page = catalog.Resolve(document.GetProperty("url").GetString());
        if (page == null) return;
        string text = document.GetProperty("excerpt").GetString();
        try
        {
            string html = ReadBounded(Path.Combine(catalog.Directory, Uri.UnescapeDataString(page.AbsolutePath).TrimStart('/')));
            int article = html.IndexOf("<article", StringComparison.OrdinalIgnoreCase);
            int end = article < 0 ? -1 : html.IndexOf("</article>", article, StringComparison.OrdinalIgnoreCase);
            if (end > article)
            {
                string content = Regex.Replace(html[article..end], @"<(script|style|svg)\b[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                text = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(content, "<[^>]+>", " ")), @"\s+", " ").Trim();
            }
        }
        catch (IOException) { /* Use the index excerpt when the full topic is unavailable. */ }
        topics.Add(new JsonObject
        {
            ["title"] = document.GetProperty("title").GetString().Limit(160),
            ["url"] = "https://www.statsdirect.com/help" + page.AbsolutePath + page.Fragment,
            ["text"] = text.Limit(2400),
        });
    }
}

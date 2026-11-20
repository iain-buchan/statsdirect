using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatsDirect.UI.WebReports;

/// <summary>
/// The completed fenced code blocks of a reply, read by the program itself so that Open in R and Copy only use code found in its
/// own saved copy of the reply.  This mirrors the shared reference reader, Learning/Shared/Learning/code-blocks.mjs, rule for rule;
/// tests/CodeBlocks runs the shared examples (code-block-examples.json) through it, as the Mac host does through its own reader.
/// </summary>
internal static class CompletedCodeBlocks
{
    /// <summary>Whether the block's language is R; the code as the page shows it (leading tabs as spaces), which is what a click
    /// sends; and the code as the reply has it (leading tabs kept), which is what the program opens or copies.</summary>
    internal readonly record struct Block(bool R, string Shown, string Text);

    /// <summary>Said when a clicked block cannot be matched to the saved reply. The Mac says the same sentence.</summary>
    internal const string Unmatched = "StatsDirect could not match that code block to the saved reply. Select the code in the conversation and copy it instead.";

    // The page's Markdown reader expands a line's leading tabs first: leading spaces, then a run of tabs, four spaces each.
    private static readonly Regex LeadingTabs = new(@"^( *)(\t+)", RegexOptions.Compiled);
    private static readonly Regex QuoteMarker = new(@"^ *>[ \t]?", RegexOptions.Compiled);
    // Any spaces, or a list marker and the spaces after it, then three or more backticks or tildes, then the label.
    private static readonly Regex Opening = new(@"^( *(?:(?:[-*+]|[0-9]{1,9}[.)]) {1,4})?)(`{3,}|~{3,})(.*)$", RegexOptions.Compiled);
    private static readonly Regex ClosingTail = new(@"^[`~]* *$", RegexOptions.Compiled);

    /// <summary>The code of each completed block as the reply has it: R blocks only, or every block.</summary>
    internal static string[] Read(string markdown, bool rOnly = true) =>
        ReadBlocks(markdown).Where(b => !rOnly || b.R).Select(b => b.Text).ToArray();

    /// <summary>The completed fenced blocks of a reply, in order. A block with no closing fence is not returned.</summary>
    internal static List<Block> ReadBlocks(string markdown)
    {
        string[] original = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string[] lines = Array.ConvertAll(original, line => LeadingTabs.Replace(line, m => m.Groups[1].Value + new string(' ', 4 * m.Groups[2].Length), 1));
        var blocks = new List<Block>();
        for (int i = 0; i < lines.Length; i++)
        {
            // A fence may sit behind block-quote markers and at any indent; a backtick fence's label holds no backtick.
            var opening = Opening.Match(Unquote(lines[i], int.MaxValue, out int depth));
            if (!opening.Success || opening.Groups[2].Value[0] == '`' && opening.Groups[3].Value.Contains('`')) continue;
            int indent = opening.Groups[1].Length;
            string fence = opening.Groups[2].Value, pad = new string(' ', indent);
            string[] label = opening.Groups[3].Value.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string language = label.Length > 0 ? label[0] : "";
            bool r = language.Equals("r", StringComparison.OrdinalIgnoreCase) || language.Equals("rscript", StringComparison.OrdinalIgnoreCase);
            var shown = new List<string>(); var text = new List<string>();
            bool closed = false;
            while (++i < lines.Length)
            {
                // A line inside the block loses as many quote markers as its opener had, then the opener's indent, but only
                // when it begins with at least that many spaces; a line indented less is left as it is.
                string rest = Unquote(lines[i], depth, out _);
                if (Closes(rest, fence, indent)) { closed = true; break; }
                string code = rest.StartsWith(pad, StringComparison.Ordinal) ? rest[indent..] : rest;
                int removed = lines[i].Length - code.Length;
                shown.Add(code);
                // The reply's own characters when the removed prefix is literally there; otherwise the line as shown.
                text.Add(original[i].StartsWith(lines[i][..removed], StringComparison.Ordinal) ? original[i][removed..] : code);
            }
            if (closed) blocks.Add(new Block(r, string.Join("\n", shown), string.Join("\n", text)));
        }
        return blocks;
    }

    /// <summary>
    /// Which block a click means: the index among the R blocks (or among all blocks) of the block at the page's position when
    /// its shown text is the text the page sent, otherwise of the first block with that text, otherwise -1.
    /// </summary>
    internal static int Find(IReadOnlyList<Block> blocks, int position, string shown, bool rOnly = true)
    {
        var list = rOnly ? blocks.Where(b => b.R).ToList() : blocks.ToList();
        if (position >= 0 && position < list.Count && list[position].Shown == shown) return position;
        return list.FindIndex(b => b.Shown == shown);
    }

    // Removes up to `limit` block-quote markers (spaces, ">", one optional space or tab) from the start of a line.
    private static string Unquote(string line, int limit, out int depth)
    {
        depth = 0;
        while (depth < limit)
        {
            var marker = QuoteMarker.Match(line);
            if (!marker.Success) break;
            line = line[marker.Length..]; depth++;
        }
        return line;
    }

    // A closing fence: spaces (at most three more than the opener's indent), the opener's own fence, any further backticks or
    // tildes, then only spaces.
    private static bool Closes(string line, string fence, int indent)
    {
        int spaces = 0;
        while (spaces < line.Length && line[spaces] == ' ') spaces++;
        return spaces <= indent + 3 && line.AsSpan(spaces).StartsWith(fence, StringComparison.Ordinal) && ClosingTail.IsMatch(line[(spaces + fence.Length)..]);
    }
}

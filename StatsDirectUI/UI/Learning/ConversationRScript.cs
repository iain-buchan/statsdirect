using StatsDirect.R;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StatsDirect.UI.Learning;

/// <summary>Replays the local source snapshots and R blocks up to a chosen reply.
/// Nothing is evaluated here, and the local snapshots are not conversation text sent to AI.</summary>
internal static class ConversationRScript
{
    internal static string[] Blocks(string markdown)
    {
        var blocks = new List<string>();
        string[] lines = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var opening = Regex.Match(lines[i], @"^\s*(`{3,}|~{3,})\s*([\w+#.-]*)\s*$");
            if (!opening.Success) continue;
            char fence = opening.Groups[1].Value[0];
            bool r = opening.Groups[2].Value.Equals("r", StringComparison.OrdinalIgnoreCase) || opening.Groups[2].Value.Equals("rscript", StringComparison.OrdinalIgnoreCase);
            var code = new List<string>();
            while (++i < lines.Length && !Regex.IsMatch(lines[i], @"^\s*" + fence + @"{3,}\s*$")) code.Add(lines[i]);
            // An unfinished streamed block is not ready to run.
            if (r && i < lines.Length) blocks.Add(string.Join("\n", code));
        }
        return blocks.ToArray();
    }

    internal static string Build(JsonArray messages, int messageIndex, int blockIndex = -1)
    {
        if (messageIndex < 0 || messageIndex >= messages.Count || messages[messageIndex].S("role") is not ("assistant" or "user"))
            throw new InvalidOperationException("Wait for the reply to finish before opening its R code.");
        var selected = Blocks(messages[messageIndex].S("text"));
        if (selected.Length == 0 || blockIndex < -1 || blockIndex >= selected.Length)
            throw new InvalidOperationException("This reply contains no completed R code block.");
        var result = new StringBuilder("# AI suggestion — review before running. Nothing has been run automatically.\n# Source snapshots and earlier R blocks are included in conversation order.\n# Review earlier examples too: running this script will run all included code.\n");
        string previousSource = "";
        void Append(string label, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if ((long)result.Length + code.Length > 2_000_000) throw new InvalidOperationException("The conversation is too large to open as one R script. Start a new conversation with the relevant source script.");
            result.Append("\n# ").Append(label).Append('\n').Append(code).Append('\n');
        }
        for (int i = 0; i <= messageIndex; i++)
        {
            var message = messages[i];
            string source = message.S("rSource");
            if (source.Length > 0 && source != previousSource) { Append("Source script snapshot shared for this question", source); previousSource = source; }
            Append("Worksheet values read for this reply (fixed snapshot)", message.S("rData"));
            if (message.S("role") is not ("assistant" or "user")) continue;
            var blocks = Blocks(message.S("text"));
            int count = i == messageIndex && blockIndex >= 0 ? blockIndex + 1 : blocks.Length;
            for (int b = 0; b < count; b++) Append(i == messageIndex ? "Selected reply — R block " + (b + 1) : "Earlier conversation — R block " + (b + 1), blocks[b]);
        }
        return result.ToString();
    }

    internal static string Dataset(JsonObject data)
    {
        string Literal(JsonNode value) => value == null ? "NA" : RScriptWriter.Literal(JsonSerializer.SerializeToElement(value));
        string key = RScriptWriter.Quote(data.S("datasetId"));
        var columns = data.A("columns");
        // Explicit column indices avoid collisions/guessed aliases and preserve blank rows.
        return "if (!exists(\".statsdirect_data\", inherits = FALSE)) .statsdirect_data <- list()\n" +
            ".statsdirect_data[[" + key + "]] <- setNames(list(\n" +
            string.Join(",\n", columns.Select(c => "  c(" + string.Join(", ", c.A("values").Select(Literal)) + ")")) +
            "\n), c(" + string.Join(", ", columns.Select(c => RScriptWriter.Quote("column_" + c.I("index")))) + "))\n";
    }
}

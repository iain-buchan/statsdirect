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
    internal static string[] Blocks(string markdown, bool rOnly = true) => WebReports.CompletedCodeBlocks.Read(markdown, rOnly);

    internal static string Build(JsonArray messages, int messageIndex, int blockIndex = -1, bool currentReplyOnly = false)
    {
        var selected = SelectedBlocks(messages, messageIndex, blockIndex);
        var result = new StringBuilder("# AI suggestion — review before running. Nothing has run automatically.\n");
        result.Append(currentReplyOnly
            ? "# Includes the fixed worksheet values read for this reply.\n"
            : "# Source snapshots and earlier R blocks are included in conversation order.\n# Review earlier examples too: running this script will run all included code.\n");
        string previousSource = "";
        void Append(string label, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if ((long)result.Length + code.Length > 2_000_000) throw new InvalidOperationException("The conversation is too large to open as one R script. Start a new conversation with the relevant source script.");
            result.Append("\n# ").Append(label).Append('\n').Append(code).Append('\n');
        }
        for (int i = currentReplyOnly ? messageIndex : 0; i <= messageIndex; i++)
        {
            var message = messages[i];
            string source = message.S("rSource");
            if (source.Length > 0 && source != previousSource) { Append("Source script snapshot shared for this question", source); previousSource = source; }
            Append("Worksheet values read for this reply (fixed snapshot)", message.S("rData"));
            if (message.S("role") is not ("assistant" or "user")) continue;
            var blocks = i == messageIndex ? selected : Blocks(message.S("text"));
            int count = i == messageIndex && blockIndex >= 0 ? blockIndex + 1 : blocks.Length;
            for (int b = 0; b < count; b++) Append(i == messageIndex ? "Selected reply — R block " + (b + 1) : "Earlier conversation — R block " + (b + 1), blocks[b]);
        }
        return result.ToString();
    }

    internal static string BuildForCurrentSession(JsonArray messages, int messageIndex, int blockIndex = -1)
    {
        var blocks = SelectedBlocks(messages, messageIndex, blockIndex);
        var result = new StringBuilder("# AI suggestion\n");
        void Append(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if ((long)result.Length + code.Length > 2_000_000)
                throw new InvalidOperationException("The R code and worksheet snapshots are too large to run together. Start a new conversation or copy the code you need.");
            result.Append('\n').Append(code).Append('\n');
        }

        // Restore only the app-generated worksheet bindings. The existing R session
        // already owns its variables and may have run a different version of the script.
        for (int i = 0; i <= messageIndex; i++) Append(messages[i].S("rData"));
        if (blockIndex >= 0) Append(blocks[blockIndex]);
        else foreach (string block in blocks) Append(block);
        return result.ToString();
    }

    private static string[] SelectedBlocks(JsonArray messages, int messageIndex, int blockIndex)
    {
        if (messageIndex < 0 || messageIndex >= messages.Count || messages[messageIndex].S("role") is not ("assistant" or "user"))
            throw new InvalidOperationException("Wait for the reply to finish before opening its R code.");
        var selected = Blocks(messages[messageIndex].S("text"));
        if (selected.Length == 0 || blockIndex < -1 || blockIndex >= selected.Length)
            throw new InvalidOperationException("This reply contains no completed R code block.");
        return selected;
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
            "\n), c(" + string.Join(", ", columns.Select(c => RScriptWriter.Quote("column_" + c.I("index")))) + "))\n" +
            "attr(.statsdirect_data[[" + key + "]], \"statsdirect\") <- list(documentId = " + RScriptWriter.Quote(data.S("documentId")) +
            ", workbook = " + RScriptWriter.Quote(data.S("workbook")) + ", sheet = " + RScriptWriter.Quote(data.S("sheet")) +
            ", range = " + RScriptWriter.Quote(data.S("range")) + ")\n";
    }
}

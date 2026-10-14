using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace StatsDirect.UI.Learning;

internal sealed partial class LearningService
{
    // Local-only, compatible with the Mac portfolio's replay metadata. Never sent
    // to the web page, tutor conversation or assessment export.
    private JsonObject rContexts = new();
    private string replayPortfolioId;
    private JsonObject closedSnapshot;

    private void LoadRContexts(JsonNode state)
    {
        var contexts = state?["rContexts"];
        if (contexts != null && (contexts is not JsonObject rows || rows.Any(p => p.Value is not JsonObject row ||
            new[] { "userMessageID", "rSource", "rData" }.Any(key => row[key] is not JsonValue v || !v.TryGetValue<string>(out _)))))
            throw new InvalidDataException("The saved R context is incompatible. The learning record has not been overwritten.");
        rContexts = contexts?.DeepClone() as JsonObject ?? new();
        replayPortfolioId = state.S("id");
    }

    private void RememberRContext(string id, string userMessageId, QuestionContext context)
    {
        if (State == null || !State.A("conversation").Any(c => c.S("id") == userMessageId && c.S("role") == "user")) return;
        rContexts[id] = new JsonObject { ["userMessageID"] = userMessageId, ["rSource"] = context.RSource, ["rData"] = context.RData };
        SaveCurrent();
    }

    internal string LearningRScript(string messageId, string lessonId, int blockIndex)
    {
        var messages = new JsonArray();
        foreach (var row in State.A("conversation").Where(c => c.S("lessonId") == lessonId || c["lessonId"] == null && lessonId == "epidemiology"))
        {
            string id = row.S("id");
            var source = rContexts.FirstOrDefault(p => p.Value.S("userMessageID") == id).Value;
            messages.Add(new JsonObject { ["id"] = id, ["role"] = row.S("role"), ["text"] = row.S("text"), ["rSource"] = source.S("rSource"), ["rData"] = rContexts[id].S("rData") });
        }
        int index = Enumerable.Range(0, messages.Count).FirstOrDefault(i => messages[i].S("id") == messageId, -1);
        return ConversationRScript.Build(messages, index, blockIndex);
    }

    internal void LearningClosed(LearningView view)
    {
        if (view != View) return;
        CancelQuestion();
        var pending = view.ReplyEvent;
        if (State == null || pending == null) return;
        var value = pending["value"];
        string id = value.S("id");
        bool complete = pending.S("action") == "reply";
        string text = complete ? value.S("text") : pending.S("partial");
        if (text.Length == 0 || State.A("conversation").Any(c => c.S("id") == id)) return;
        var saved = (JsonObject)State.DeepClone();
        var entry = new JsonObject { ["id"] = id, ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["role"] = "assistant",
            ["text"] = text + (complete ? "" : "\n\n[Reply stopped before completion.]"), ["source"] = "StatsDirect AI tutor", ["lessonId"] = pending.S("lessonId") };
        foreach (string key in new[] { "model", "responseID", "promptVersion", "courseSources", "workspaceSources" })
            if (value?[key] != null) entry[key] = value[key].DeepClone();
        saved.A("conversation").Add(entry);
        try { SaveState(saved); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { State = saved; closedSnapshot = saved; } // Keep received text available for reopening even if disk writes fail.
    }
}

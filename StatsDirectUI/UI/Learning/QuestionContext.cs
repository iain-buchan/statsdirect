using StatsDirect.Templates;
using StatsDirect.UI.HtmlHelp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StatsDirect.UI.Learning;

/// <summary>One explicit grant, tied to existing documents and worksheet revisions. Only shipped, whitelisted analyses may run.</summary>
internal sealed class QuestionContext
{
    private readonly StatsDirectForm[] documents;
    private readonly Dictionary<string, int> versions;
    private readonly Dictionary<string, JsonNode> scripts = new();
    private readonly Dictionary<string, JsonObject> reports = new();
    private readonly AISharingConsent sharing;
    private readonly int sharingRevision;
    private readonly CancellationToken token;
    private readonly bool scriptOnly;
    private readonly Dictionary<string, (frmSpreadsheetGear Grid, JsonObject Data)> datasets = new();
    private readonly Dictionary<string, Task<JsonNode>> calculations = new();
    // Full local source is kept for Open R code, independently of the bounded AI excerpt.
    internal string RSource { get; }
    internal string RData => string.Join("\n", datasets.Values.Select(d => ConversationRScript.Dataset(d.Data)));
    internal QuestionContext(IEnumerable<StatsDirectForm> documents, bool scriptOnly, CancellationToken token, AISharingConsent sharing = null)
    {
        this.documents = documents.ToArray(); this.token = token; this.scriptOnly = scriptOnly;
        this.sharing = sharing; sharingRevision = sharing?.Revision ?? 0;
        RSource = string.Join("\n\n", this.documents.OfType<frmScript>().Where(s => s.LearningLanguage == "R").Select(s => s.LearningScript));
        versions = this.documents.OfType<frmSpreadsheetGear>().ToDictionary(g => g.Id(), g => g.LearningVersion);
        foreach (var script in this.documents.OfType<frmScript>()) scripts[script.Id()] = new JsonObject { ["documentId"] = script.Id(), ["title"] = script.Text, ["kind"] = LearningService.Kind(script), ["language"] = script.LearningLanguage, ["script"] = script.LearningScript.Limit(12000), ["output"] = script.LearningOutput.Length > 8000 ? script.LearningOutput[^8000..] : script.LearningOutput, ["truncated"] = script.LearningScript.Length > 12000 || script.LearningOutput.Length > 8000, ["running"] = script.LearningRunning, ["snapshot"] = true };
    }
    internal void Check()
    {
        token.ThrowIfCancellationRequested();
        if (sharing != null && sharing.Revision != sharingRevision) throw new OperationCanceledException("The selected data document changed. Confirm sharing again before sending.");
        if (ChatGPTTutor.Disabled) throw new InvalidOperationException(ChatGPTTutor.PolicyMessage);
        foreach (var doc in documents)
        {
            if (doc.IsDisposed || doc.MdiParent == null) throw new InvalidOperationException("A shared document closed. Send again to choose the current context.");
            if (doc is frmSpreadsheetGear grid && grid.LearningVersion != versions[doc.Id()]) throw new InvalidOperationException("The worksheet changed after confirmation. Review it and confirm sharing for a new question.");
        }
    }
    internal JsonObject Overview()
    {
        Check();
        return new JsonObject { ["documents"] = new JsonArray(documents.Select(d => (JsonNode)new JsonObject { ["documentId"] = d.Id(), ["title"] = d.Text, ["kind"] = LearningService.Kind(d), ["worksheet"] = d is frmSpreadsheetGear grid ? grid.LearningInfo() : null }).ToArray()), ["sharing"] = documents.Length == 0 ? "Question and conversation only. No open document metadata or content is shared." : "Only these documents are approved for this question. Script/output are fixed snapshots. Worksheet reads are bounded and reject changes." };
    }
    internal JsonObject InitialContext(int limit = 28000)
    {
        var result = Overview(); result["scripts"] = new JsonArray(scripts.Values.Select(s => s.DeepClone()).ToArray());
        if (reports.Count > 0) result["currentReport"] = reports.Values.First().DeepClone();
        if (result.ToJsonString().Length > limit)
        {
            result.Remove("scripts"); result.Remove("currentReport"); result["readDetailsWithTools"] = true;
        }
        return result;
    }
    internal async Task CaptureReportsAsync(string resultId = null)
    {
        Check();
        foreach (var report in documents.OfType<frmReportWebView>())
        { reports[report.Id()] = await ReportAIContext.SnapshotAsync(report, resultId); Check(); }
    }
    private static JsonObject Tool(string name, string description, JsonObject properties, params string[] required) => new() { ["type"] = "function", ["name"] = name, ["description"] = description, ["inputSchema"] = new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray(required.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()), ["additionalProperties"] = false } };
    private static JsonObject StringType => new() { ["type"] = "string" };
    private static JsonObject IntegerType => new() { ["type"] = "integer", ["minimum"] = 0 };
    internal JsonArray Tools
    {
        get
        {
            var tools = new JsonArray(Tool("statsdirect_workspace", "List only documents approved for this question.", new()), Tool("statsdirect_method_help", "Read bundled method help using a StatsDirect operation ID, never a path or URL.", new() { ["operation"] = StringType }, "operation"));
            if (documents.OfType<frmSpreadsheetGear>().Any()) tools.Add(Tool("statsdirect_read_data", "Read literal worksheet rows, preserving blanks and text. Row numbers are 1-based; columns/sheetIndex 0-based. At most 32 columns and 4,000 cells. Never silently sampled. Exclude heading rows explicitly when calculating.", new() { ["documentId"] = StringType, ["sheetIndex"] = IntegerType, ["firstRow"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }, ["lastRow"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }, ["columns"] = new JsonObject { ["type"] = "array", ["items"] = IntegerType, ["minItems"] = 1, ["maxItems"] = 32 } }, "documentId"));
            if (documents.Any(d => d is frmScript or frmReportWebView)) tools.Add(Tool("statsdirect_read_document", "Read approved script/output snapshot or report. Does not run or edit code.", new() { ["documentId"] = StringType }, "documentId"));
            if (!scriptOnly && documents.OfType<frmSpreadsheetGear>().Any()) tools.Add(Tool("statsdirect_open_analysis", "Open a documented analysis in StatsDirect for the learner to select options and run. This does not itself calculate results.", new() { ["operation"] = StringType, ["documentId"] = StringType }, "operation", "documentId"));
            if (!scriptOnly && documents.OfType<frmSpreadsheetGear>().Any()) tools.Add(Tool("statsdirect_run_analysis", "Run the unchanged StatsDirect engine on a datasetId from an approved read in this question. Adds the real output and Continue in R record to the active report. Exclude headings; preserve missing rows. For contingency tables ask which study design applies and supply studyType explicitly. Uses normal default options; open the analysis form for other options. Never supply values or code.", new() { ["datasetId"] = StringType, ["operation"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(TutorCalculation.Allowed.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()) }, ["agreement"] = new JsonObject { ["type"] = "boolean" }, ["studyType"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("casecontrol", "cohort", "neither") } }, "datasetId", "operation"));
            return tools;
        }
    }
    internal async Task<JsonNode> CallAsync(string name, JsonObject arguments)
    {
        Check();
        var spec = Tools.FirstOrDefault(t => t.S("name") == name);
        if (spec == null) throw new InvalidOperationException("This action is not available for this question.");
        var schema = spec["inputSchema"];
        if (arguments.Any(p => ((JsonObject)schema["properties"])[p.Key] is not JsonObject type || !ValidArgument(p.Value, type)) || schema.A("required").Any(p => arguments[p.GetValue<string>()] == null)) throw new ArgumentException("Invalid tool arguments.");
        if (name == "statsdirect_workspace") return Overview();
        if (name == "statsdirect_method_help") return MethodHelp(arguments.S("operation"));
        if (name == "statsdirect_run_analysis")
        {
            if (!datasets.TryGetValue(arguments.S("datasetId"), out var dataset)) throw new InvalidOperationException("Read an approved range for this question first; datasetId is not valid here.");
            string key = arguments.S("datasetId") + ":" + arguments.S("operation") + ":" + arguments.B("agreement") + ":" + arguments.S("studyType");
            if (!calculations.TryGetValue(key, out var task))
            {
                task = CalculateAsync(dataset.Grid, dataset.Data, arguments);
                calculations.Add(key, task); // Include in-flight and failed calls: never append a duplicate after a retry.
            }
            var result = await task; Check(); return result.DeepClone();
        }
        var doc = documents.FirstOrDefault(d => d.Id() == arguments.S("documentId")) ?? throw new InvalidOperationException("That document was not approved for this question.");
        if (name == "statsdirect_read_data" && doc is frmSpreadsheetGear grid)
        {
            var data = grid.LearningRead(arguments); Check();
            string id = Guid.NewGuid().ToString("N"); data["datasetId"] = id;
            datasets[id] = (grid, (JsonObject)data.DeepClone());
            // The R editor uses the same literal values, never a later read of the worksheet.
            data["rCode"] = ConversationRScript.Dataset(data);
            return data;
        }
        if (name == "statsdirect_read_document")
        {
            if (scripts.TryGetValue(doc.Id(), out var snapshot)) return snapshot.DeepClone();
            if (reports.TryGetValue(doc.Id(), out var report)) return report.DeepClone();
        }
        if (name == "statsdirect_open_analysis" && !scriptOnly && doc is frmSpreadsheetGear)
        { ((frmMain)doc.MdiParent).OpenLearningAnalysis(arguments.S("operation"), doc); return new JsonObject { ["state"] = "awaiting_user_input", ["message"] = "The method is opening. No calculation has been completed by this tool." }; }
        throw new InvalidOperationException("This document does not support that action.");
    }
    private async Task<JsonNode> CalculateAsync(frmSpreadsheetGear grid, JsonObject data, JsonObject arguments)
    {
        Check(); var main = (frmMain)grid.MdiParent;
        void CheckDataset()
        {
            Check();
            var current = grid.LearningRead(new JsonObject { ["sheetIndex"] = data.I("sheetIndex"), ["firstRow"] = data.I("firstRow"), ["lastRow"] = data.I("lastRow"), ["columns"] = new JsonArray(data.A("columns").Select(c => (JsonNode)JsonValue.Create(c.I("index"))).ToArray()) });
            var original = data.DeepClone().AsObject(); original.Remove("datasetId");
            if (!JsonNode.DeepEquals(original, current)) throw new InvalidOperationException("The selected data changed after it was read. Review sharing again before calculating.");
        }
        CheckDataset();
        if (main.InOperation || main.LearningCalculationRunning) throw new InvalidOperationException("Finish or stop the current analysis before running another.");
        var calculation = new TutorCalculation(arguments.S("operation"), data, ((IGrid)grid).WorkbookPath, arguments.B("agreement"), arguments.S("studyType"), SdApplication.SoleInstance, token);
        main.LearningCalculationRunning = true;
        try
        {
            await Task.Run(calculation.Run, token); CheckDataset();
            // Keep the engine HTML intact: its Creole heading already supplies the title.
            var report = main.SelectedReportWindow as frmReportWebView ?? throw new InvalidOperationException("Open an HTML report to receive the result.");
            string item = await report.AppendLearningResultAsync(calculation.Html, arguments.S("operation"), calculation.Record, CheckDataset);
            var record = JsonNode.Parse(calculation.Record);
            string text = WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(calculation.Html, @"<svg\b[^>]*>.*?</svg>", "[Chart]", RegexOptions.Singleline | RegexOptions.IgnoreCase), "<[^>]+>", " "));
            return new JsonObject { ["state"] = "completed", ["operation"] = arguments.S("operation"), ["datasetId"] = arguments.S("datasetId"), ["reportId"] = report.Id(), ["itemId"] = item, ["text"] = text.Limit(24000), ["values"] = record?["values"]?.DeepClone(), ["warnings"] = calculation.Warnings, ["hasSVG"] = calculation.Html.Contains("<svg"), ["continueInR"] = true };
        }
        finally { main.LearningCalculationRunning = false; }
    }
    internal static async Task<string> ReportTextAsync(frmReportWebView report)
    {
        await report.LearningReport.Ready;
        return JsonNode.Parse(await report.LearningReport.Browser.CoreWebView2.ExecuteScriptAsync("document.getElementById('results').innerText"))?.GetValue<string>() ?? "";
    }
    private static bool ValidArgument(JsonNode value, JsonObject spec)
    {
        if (spec["enum"] is JsonArray choices && !choices.Any(c => JsonNode.DeepEquals(c, value))) return false;
        return spec.S("type") switch
        {
            "string" => value is JsonValue s && s.TryGetValue<string>(out var text) && text.Length <= 200,
            "boolean" => value is JsonValue b && b.TryGetValue<bool>(out _),
            "integer" => value is JsonValue n && n.TryGetValue<int>(out int number) && number >= spec.I("minimum"),
            "array" => value is JsonArray a && a.Count >= spec.I("minItems") && a.Count <= spec.I("maxItems", int.MaxValue) && a.All(v => ValidArgument(v, (JsonObject)spec["items"])),
            _ => false
        };
    }
    internal static JsonObject MethodHelp(string operation)
    {
        if (!TemplateFactory.Operations.TryGetValue(operation, out var definition)) throw new ArgumentException("Choose a documented StatsDirect operation.");
        string topic = definition.HelpContext?.Url ?? definition.HelpContext?.ChmId.ToString();
        var catalog = new HelpCatalog(Configuration.SDConfiguration.HelpDirectory); var uri = catalog.Resolve(topic);
        if (uri == null) throw new IOException("This method's help is unavailable.");
        string html = File.ReadAllText(Path.Combine(catalog.Directory, Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/')));
        string text = WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, @"<(script|style|svg)\b[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase), "<[^>]+>", " "));
        return new JsonObject { ["operation"] = operation, ["text"] = text.Limit(18000), ["boundedExcerpt"] = true };
    }
}

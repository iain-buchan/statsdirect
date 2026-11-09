using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.UI.WebReports;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    // Saved runs whose values were too many to keep with the result, read back from the worksheet they point to as Continue in R and
    // the notebook export read them: the actual grid's ReadColumns through FrameReader. Each run is the template processor's, with
    // its frames acquired from the same worksheet by the production selection code, and R must give the native figures.
    static async Task TestSavedRunRereads(frmMain main)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        double Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        string Vector(IEnumerable<double> values) => "c(" + string.Join(", ", values.Select(v => v.ToString("R", inv))) + ")";

        // A multi-column category frame whose first column's range is the longer: the reread sized its buffer from the last column.
        await WithGrid(main, "Saved frequency, longer range first", async grid =>
        {
            Fill(grid, 0, "Response one", Enumerable.Range(0, 3000).Select(k => (object)(double)(1 + k * 7 % 5)));
            Fill(grid, 1, "Response two", Enumerable.Range(0, 2600).Select(k => (object)(double)(1 + k * 3 % 4)));
            var (run, output) = RunOnGrid(grid, "Frequency", new() { ["data"] = "A1:A3001,B1:B2601", ["sortBy"] = "value", ["sortOrder"] = "asc" });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "data");
            Check(!record.TryGetProperty("data", out _) && input.GetProperty("kind").GetString() == "frame"
                && input.GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 3001 && input.GetProperty("columns")[1].GetProperty("rows")[1].GetInt32() == 2601,
                "frequency of 5,600 categories is saved pointer-only, the longer range first");
            FrameValues reread = FrameReader.Read(FramePointer.From(input));
            Check(reread.Unavailable == null && reread.Columns.Count == 2 && reread.Columns.All(c => !c.Changed)
                && reread.Columns[0].Values.Length == 3000 && reread.Columns[1].Values.Length == 2600,
                "the actual reread of a category frame with its longer range first gives every value with its recorded hash (" + Read(reread) + ")");
            var first = ((IList<ParameterBag>)output.ParameterBag["*variable"].AsObject)[0];
            var bins = ((IList<ParameterBag>)first["*bin"].AsObject).ToList();
            RScript script = RScriptWriter.Write(record, "Frequency", FrameReader.Read);
            Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), "the frequency replay reads its pointer-only data without a change note");
            string expected = "stopifnot(n == " + Number(first, "n").ToString("R", inv) + ", identical(names(counts), c(" + string.Join(", ", bins.Select(b => "\"" + b["x"].AsObject + "\"")) + ")), " +
                "identical(as.vector(counts), " + Vector(bins.Select(b => Number(b, "fx"))).Replace("c(", "as.integer(c(") + ")))\ncat('SAVED_FREQUENCY_OK\\n')";
            await RunConversationR(script.Text, "saved-frequency-longer-first", expected, "SAVED_FREQUENCY_OK");
            Notebook(record, "Frequency", "the frequency notebook export");

            // the longer range last, which was always read right, reads as before
            var (lastRun, _) = RunOnGrid(grid, "Frequency", new() { ["data"] = "B1:B2601,A1:A3001", ["sortBy"] = "value", ["sortOrder"] = "asc" });
            FrameValues last = FrameReader.Read(FramePointer.From(SavedInput(JsonDocument.Parse(lastRun.Record).RootElement.Clone(), "data")));
            Check(last.Unavailable == null && last.Columns.All(c => !c.Changed) && last.Columns.Select(c => c.Values.Length).SequenceEqual(new[] { 2600, 3000 })
                && last.Columns[0].Values.SequenceEqual(reread.Columns[1].Values) && last.Columns[1].Values.SequenceEqual(reread.Columns[0].Values),
                "the same columns with the longer range last reread the same values (" + Read(last) + ")");
        });

        // The same with two raters: equal counts of ratings, but the first range runs three blank rows past the last rating.
        await WithGrid(main, "Saved kappa, unequal ranges", async grid =>
        {
            Fill(grid, 0, "First rater", Enumerable.Range(0, 2597).Select(k => (object)(double)(1 + k * 7 % 3)));
            Fill(grid, 1, "Second rater", Enumerable.Range(0, 2597).Select(k => (object)(double)(1 + (k * 7 + (k % 4 == 0 ? 1 : 0)) % 3)));
            Fill(grid, 3, "Notes", Enumerable.Range(0, 2604).Select(k => (object)"seen"));   // the sheet's used rows run past both selections
            var (run, output) = RunOnGrid(grid, "Kappa", new() { ["ci"] = 0.95, ["responses"] = "A1:A2601,B1:B2598", ["method"] = "1" });
            JsonElement record = JsonDocument.Parse(run.Record).RootElement.Clone();
            JsonElement input = SavedInput(record, "responses");
            Check(!record.TryGetProperty("data", out _) && input.GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 2601,
                "kappa of 2,597 paired ratings is saved pointer-only with the first rater's longer range");
            FrameValues reread = FrameReader.Read(FramePointer.From(input));
            Check(reread.Unavailable == null && reread.Columns.All(c => !c.Changed && c.Values.Length == 2597),
                "the actual reread of the raters' unequal ranges gives 2,597 ratings each with their recorded hashes (" + Read(reread) + ")");
            RScript script = RScriptWriter.Write(record, "Kappa", FrameReader.Read);
            Check(script.HasRecipe && !script.DataUnavailable, "the kappa replay reads its pointer-only data");
            ParameterBag k = output.ParameterBag;
            await RunConversationR(script.Text, "saved-kappa-unequal-ranges",
                "stopifnot(n == 2597, isTRUE(all.equal(unname(c(kappa, se0, se, kappaw, se0w, sew, (po - spe) / (1 - spe))), " +
                Vector(new[] { "kappa", "se", "seci", "kappaw", "sekw", "sekwci", "spi" }.Select(key => Number(k, key))) + ", tolerance = 1e-10, check.attributes = FALSE)))\ncat('SAVED_KAPPA_OK\\n')",
                "SAVED_KAPPA_OK");
            Notebook(record, "Kappa", "the kappa notebook export");
        });
    }

    static async Task WithGrid(frmMain main, string name, Func<frmSpreadsheetGear, Task> test)
    {
        var grid = (frmSpreadsheetGear)main.CreateGrid(name);
        try { await test(grid); }
        finally { CloseTestDocument(grid); }
    }

    static void Fill(frmSpreadsheetGear grid, int column, string title, IEnumerable<object> values)
    {
        object[] cells = new object[] { title }.Concat(values).ToArray();
        object[,] block = new object[cells.Length, 1];
        for (int i = 0; i < cells.Length; i++) block[i, 0] = cells[i];
        ((IGrid)grid).SetValues(0, column, block);
    }

    static void Select(frmSpreadsheetGear grid, string address)
    {
        var view = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
        view.GetLock();
        try { view.ActiveWorksheet.Range[address].Select(); }
        finally { view.ReleaseLock(); }
    }

    /// <summary>What a reread gave, for the label of a check: why it could not be read, or each column's count and whether it changed.</summary>
    static string Read(FrameValues values) => values.Unavailable ?? string.Join(", ", values.Columns.Select(c => c.Values.Length + (c.Changed ? " changed" : "")));

    static JsonElement SavedInput(JsonElement record, string name) => record.GetProperty("inputs").EnumerateArray().First(i => i.GetProperty("name").GetString() == name);

    static (OperationRun run, StepOutput output) RunOnGrid(frmSpreadsheetGear grid, string operation, Dictionary<string, object> answers)
    {
        var host = new SavedRunHost(grid, answers);
        ITemplateProcessor processor = new TemplateProcessor(host);
        StepOutput output = processor.Execute(TemplateFactory.Operations[operation], new ParameterBag());
        if (host.LastRun?.Record == null) throw new Exception(operation + " left no record of its run");
        return (host.LastRun, output);
    }

    static string Notebook(JsonElement record, string title, string label)
    {
        string json = ReportNotebook.Write(NotebookSnapshot(record, title), title, FrameReader.Read, "test");
        string code = string.Join("\n", JsonDocument.Parse(json).RootElement.GetProperty("cells").EnumerateArray()
            .Where(c => c.GetProperty("cell_type").GetString() == "code").Select(c => c.GetProperty("source").GetString()));
        Check(code.Contains("local({"), label + " accepts the reread values");
        return code;
    }

    static JsonElement NotebookSnapshot(JsonElement record, string title) => JsonSerializer.SerializeToElement(new JsonObject
    {
        ["version"] = 1, ["engineVersion"] = "test", ["reportHtml"] = "<p>" + title + "</p>",
        ["entries"] = new JsonArray(new JsonObject
        {
            ["id"] = "saved-run", ["title"] = title, ["operation"] = record.GetProperty("operation").GetString(), ["html"] = "<p>" + title + "</p>",
            ["cells"] = new JsonArray(new JsonObject { ["source"] = title }), ["record"] = JsonNode.Parse(record.GetRawText())
        })
    });

    /// <summary>The host of a run on the worksheet: a frame prompt is answered by selecting its ranges on the grid and acquiring them with
    /// the program's own selection code, every other prompt from the answers given; the run the report step hands it is kept.</summary>
    sealed class SavedRunHost : ITemplateHost, IProgressBar
    {
        readonly frmSpreadsheetGear grid;
        readonly Dictionary<string, object> answers;
        internal OperationRun LastRun { get; private set; }
        internal SavedRunHost(frmSpreadsheetGear grid, Dictionary<string, object> answers) { this.grid = grid; this.answers = answers; }
        public Operation Operation { get; set; }
        public IDictionary<string, ParameterBag> SessionParametersPerOperation { get; } = new Dictionary<string, ParameterBag>();
        public ParameterBag SessionParametersAcrossOperations => new();
        public SDPreferences Preferences => SdApplication.SoleInstance.Preferences;
        public ParameterBag Amend(IFillable options, ParameterBag context) => context;
        public bool CanCombine(Parameter parameter) => false;
        public void Error(string message, string caption) => throw new Exception("the operation refused: " + message);
        public void Warning(string message, string caption) { }
        public bool GetBoolean(string prompt, string title, bool initialValue, out bool cancelled) { cancelled = false; return initialValue; }
        public ParameterBag FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context) => throw new NotImplementedException();
        public ParameterBag FillParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context, bool shouldCombine)
        {
            if (parameter.HasAcquireIfTrue && !parameter.AcquireIfTrue(processor, context)) return new ParameterBag();
            if (!answers.TryGetValue(parameter.Name, out object value)) throw new Exception($"the operation asked for {parameter.Name}, which these checks do not answer");
            if (parameter is FrameParameter frame && value is string address)
            {
                Select(grid, address);
                return new GridSelectionProcessor(grid).FillFrameParameter(parameter, processor, context);
            }
            return new ParameterBag(parameter.Name, FilledParameterFactory.Input(value));
        }
        public void PrepareParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context) { }
        public IScriptEngine GetScriptEngine(string language) => ScriptEngine.CanHandle(language) ? new ScriptEngine() : null;
        public void OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation, RelativePosition defaultPosition) { }
        public object OutputReport(IRenderable renderable, Operation operation, object preferredOutputLocation, object run) { LastRun = run as OperationRun; return null; }
        public string pval(double p) => p.ToString("R", CultureInfo.InvariantCulture);
        public string pval_half(double p) => p.ToString("R", CultureInfo.InvariantCulture);
        public string RoundU(double amount) => amount.ToString("R", CultureInfo.InvariantCulture);
        public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
        public void Finish() { }
        public bool Update(double fractionComplete) => false;
        public void Dispose() { }
    }
}

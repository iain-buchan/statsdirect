using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace StatsDirect.UI.Learning;

/// <summary>Answers shipped operation prompts from one approved, immutable range. The model cannot supply values or code.</summary>
internal sealed class TutorCalculation : ITemplateHost, IProgressBar
{
    internal static readonly string[] Allowed = ["TPaired", "UnivariateSummary", "Chi2by2", "ExactFisher", "Wilcoxon", "TUnpaired", "Spearman", "Agreement", "Normality", "QuickSummary"];
    private readonly JsonObject dataset;
    private readonly string workbook;
    private readonly bool agreement;
    private readonly string studyType;
    private readonly CancellationToken token;
    private readonly ITemplateHost defaults;
    private readonly OperationRun captured;
    private readonly StringBuilder html = new();
    private readonly List<string> warnings = new();
    internal string Html => html.ToString();
    internal string Record { get; private set; }
    internal JsonArray Warnings => new(warnings.Select(s => (JsonNode)JsonValue.Create(s)).ToArray());

    internal TutorCalculation(string operation, JsonObject dataset, string workbook, bool agreement, string studyType, ITemplateHost defaults, CancellationToken token)
    {
        if (!Allowed.Contains(operation) || !TemplateFactory.Operations.TryGetValue(operation, out var definition)) throw new ArgumentException("Use a supported method, or open the analysis form to choose its options.");
        if (!dataset.B("complete") || dataset.B("hasErrors") || dataset.B("hasStaleFormulas")) throw new ArgumentException("Read a complete range without cell errors or stale formulas before calculating.");
        if (operation is "Chi2by2" or "ExactFisher")
        {
            if (studyType is not ("casecontrol" or "cohort" or "neither")) throw new ArgumentException("Ask the learner which study design applies; supply studyType explicitly (casecontrol, cohort or neither).");
        }
        else if (studyType.Length > 0) throw new ArgumentException("Study design is only an option for a contingency table.");
        if (agreement && operation != "TPaired") throw new ArgumentException("The agreement option applies only to TPaired.");
        this.dataset = (JsonObject)dataset.DeepClone(); this.workbook = workbook; this.agreement = agreement; this.studyType = studyType; this.defaults = defaults; this.token = token;
        captured = new OperationRun(definition);
    }
    internal void Run()
    {
        token.ThrowIfCancellationRequested();
        var result = ((ITemplateProcessor)new TemplateProcessor(this)).Execute(captured.Operation, new ParameterBag());
        token.ThrowIfCancellationRequested();
        if (result?.ParameterBag == null || html.Length == 0) throw new InvalidOperationException("The analysis did not produce a complete result. Use its analysis form to review the data and options.");
        // QuickSummary presents a text result through Amend rather than a report step.
        Record ??= RunRecord.Write(captured, result.ParameterBag);
    }
    private DataFrame Frame(FrameParameter parameter, ITemplateProcessor processor, ParameterBag context)
    {
        var columns = dataset.A("columns"); int rows = dataset.I("rowCount");
        if (columns.Count < parameter.MinimumColumns(processor, context) || columns.Count > parameter.MaximumColumns(processor, context) || rows < 1 || parameter.HasLength && rows != parameter.Length(processor, context)) throw new ArgumentException("The range dimensions do not match this method. Read the required columns and rows explicitly.");
        var frame = new DataFrame();
        foreach (var column in columns)
        {
            var values = column.A("values");
            if (values.Count != rows) throw new ArgumentException("The range is incomplete; row alignment cannot be preserved.");
            double[] numbers = values.Select(Number).ToArray();
            // Match normal worksheet acquisition: paired methods retain missing rows; only
            // methods requesting NumericSkipMissing remove them, independently per column.
            if (parameter.DataAcquisitionMode == DataAcquisitionMode.NumericSkipMissing) numbers = numbers.Where(n => n != Constant.MISSING).ToArray();
            frame.Variables.Add(new DoubleVariable(numbers, column.S("title")) { Origin = new WorksheetOrigin(workbook, dataset.S("sheet"), column.I("index"), dataset.I("firstRow") - 1, rows, parameter.DataAcquisitionMode, false, false, 1) });
        }
        return frame;
    }
    private static double Number(JsonNode value)
    {
        if (value == null || value is JsonValue text && text.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s)) return Constant.MISSING;
        if (value is JsonValue number && number.TryGetValue<double>(out double n) && double.IsFinite(n)) return n;
        throw new ArgumentException("The range contains text or nonnumeric values. Exclude heading rows and select numeric data explicitly; blanks are kept as missing values.");
    }
    public Operation Operation { get; set; }
    public SDPreferences Preferences => defaults.Preferences;
    public IDictionary<string, ParameterBag> SessionParametersPerOperation { get; } = new Dictionary<string, ParameterBag>();
    public ParameterBag SessionParametersAcrossOperations { get; } = new();
    public bool CanCombine(Parameter parameter) => false;
    public void PrepareParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context) => token.ThrowIfCancellationRequested();
    public ParameterBag FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context) => throw new InvalidOperationException("Choose this method's options in the analysis form.");
    public ParameterBag FillParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context, bool shouldCombine)
    {
        token.ThrowIfCancellationRequested();
        if (parameter.HasAcquireIfTrue && !parameter.AcquireIfTrue(processor, context)) return new();
        var result = new ParameterBag(); object value;
        switch (parameter)
        {
            case FrameParameter frame: value = Frame(frame, processor, context); break;
            case Double2By2Parameter table:
                var columns = dataset.A("columns");
                if (columns.Count != 2 || dataset.I("rowCount") != 2 || columns.Any(c => c.A("values").Count != 2)) throw new ArgumentException("Read exactly two rows and two columns of counts, excluding headings.");
                var counts = new[] { Number(columns[0].A("values")[0]), Number(columns[1].A("values")[0]), Number(columns[0].A("values")[1]), Number(columns[1].A("values")[1]) };
                if (counts.Any(n => !double.IsFinite(n) || n == Constant.MISSING || n < 0 || n != Math.Truncate(n)) || counts.Sum() <= 0) throw new ArgumentException("A contingency table requires nonnegative whole counts, no missing cells, and a positive total.");
                string[] names = [table.TopLeftName, table.TopRightName, table.BottomLeftName, table.BottomRightName];
                for (int i = 0; i < names.Length; i++) result.AddInput(names[i], counts[i]);
                captured.Note(parameter, parameter.Prompt(processor, context, parameter.Name)); return result;
            case ConfidenceIntervalParameter ci:
                value = ci.CanDefault && Preferences.CanDefaultConfidenceInterval ? Preferences.DefaultConfidenceInterval : ci.DefaultValue(processor, context) ?? .95; break;
            case BooleanParameter boolean: value = parameter.Name == "doAgreement" ? agreement : boolean.DefaultValue(processor, context) ?? false; break;
            case OptionParameter option: value = parameter.Name == "study_type" ? studyType : option.DefaultValue(processor, context) ?? throw new InvalidOperationException("Choose the method's options in the analysis form."); break;
            case OptionsParameter options:
                foreach (var choice in options.Options) result.AddInput(choice.Name, choice.Selected);
                return result;
            default: throw new InvalidOperationException("This prompt requires the analysis form: " + parameter.Name);
        }
        var filled = FilledParameterFactory.Input(value); result.Add(parameter.Name, filled);
        captured.Note(parameter, parameter.Prompt(processor, context, parameter.Name), value: filled); return result;
    }
    public ParameterBag Amend(IFillable options, ParameterBag context)
    {
        token.ThrowIfCancellationRequested();
        if (options is not SummaryStatisticsOptions summary) throw new InvalidOperationException("These options require the analysis form.");
        html.Append("<pre>").Append(WebUtility.HtmlEncode(summary.Text)).Append("</pre>");
        Record = RunRecord.Write(captured, context); return context;
    }
    public object OutputReport(IRenderable renderable, Operation operation, object preferredOutputLocation, object run)
    {
        token.ThrowIfCancellationRequested();
        html.Append(new HtmlRenderer(this).Render(renderable));
        Record = (run as OperationRun)?.Record; return null;
    }
    public void OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation, RelativePosition defaultPosition) => throw new InvalidOperationException("The learning assistant cannot write to worksheets.");
    public IScriptEngine GetScriptEngine(string language) => ScriptEngine.CanHandle(language) ? new ScriptEngine() : null;
    public bool GetBoolean(string prompt, string title, bool initialValue, out bool cancelled) { token.ThrowIfCancellationRequested(); cancelled = false; return initialValue; }
    public void Error(string message, string caption) => throw new InvalidOperationException(message);
    public void Warning(string message, string caption) => warnings.Add(message);
    public string pval(double p) => defaults.pval(p);
    public string pval_half(double p) => defaults.pval_half(p);
    public string RoundU(double amount) => defaults.RoundU(amount);
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) { token.ThrowIfCancellationRequested(); return this; }
    public bool Update(double fractionComplete) => token.IsCancellationRequested;
    public void Finish() { }
    public void Dispose() { }
}

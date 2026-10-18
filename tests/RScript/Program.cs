// Continue in R: checks of the record a run leaves with its report item, of the R script written from the record, and of the R
// recipes shared with the Mac version.  An operation is run through the template processor with a host that answers its prompts,
// as the program does, and the record is read from the run the report step hands to the host.  The scripts are run in the R that
// is installed, and their figures compared with the engine's.
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Xml.Serialization;
using StatsDirect.Data;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI;

internal static partial class Program
{
    private static int checks;
    internal static bool MetaExact;   // whether the host's preferences ask the meta-analyses for their exact methods
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static void Check(bool ok, string description) { if (!ok) throw new Exception("FAIL " + description); checks++; Console.WriteLine("PASS " + description); }
    /// <summary>The folders the checks made under the temporary folder, statsdirect-*, removed when every check passed; kept for a look when one failed.</summary>
    private static void CleanTemporaryFolders(DateTime since)
    {
        foreach (string folder in Directory.EnumerateDirectories(Path.GetTempPath(), "statsdirect-*"))
            try { if (Directory.GetCreationTime(folder) >= since) Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
    }

    private static string Metadata(string key) => typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value;

    [STAThread]
    static int Main()
    {
        DateTime started = DateTime.Now;
        try
        {
            _ = typeof(StatsDirect.R.RScriptWriter).Assembly.GetType("StatsDirect.UI.SdApplication").GetProperty("SoleInstance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);   // the application object, whose making registers the engine's functions, as the program does at its start
            // one group of checks alone, while it is being written: STATSDIRECT_RSCRIPT_ONLY=options runs the options checks and stops
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "window") { CheckScriptWindowNumbers(); Console.WriteLine($"PASS {checks} script window checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "options") { CheckOptions(); Console.WriteLine($"PASS {checks} Continue in R checks (the options alone)"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "options2") { CheckOptions2(); Console.WriteLine($"PASS {checks} Continue in R checks (the second batch of options alone)"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "options3") { CheckOptions3(); Console.WriteLine($"PASS {checks} Continue in R checks (the confidence levels alone)"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "box") { CheckBoxWhisker(); CheckTukeyHinges(); Console.WriteLine($"PASS {checks} box and whisker Continue in R checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "tukey") { CheckTukeyHinges(); Console.WriteLine($"PASS {checks} Tukey hinges checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "axis-labels") { CheckNativeAxisLabels(); Console.WriteLine($"PASS {checks} native axis label checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "general-simplicity") { CheckRecipeGeneralSimplicity(); Console.WriteLine($"PASS {checks} general recipe simplification checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "advanced-simplicity") { CheckRecipeAdvancedSimplicity(); Console.WriteLine($"PASS {checks} advanced recipe simplification checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "charts-simplicity") { CheckChartRecipeSimplicity(); Console.WriteLine($"PASS {checks} chart recipe simplification checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "survival-method-simplicity") { CheckRecipeSurvivalMethodSimplicity(); Console.WriteLine($"PASS {checks} survival/method recipe checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "recipes") { CheckRecipes(); CheckRecipesAgainstHelp(); Console.WriteLine($"PASS {checks} R recipe checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "recipe-bindings") { CheckRecipes(); Console.WriteLine($"PASS {checks} recipe binding checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "acquisition") { CheckAcquisition(); Console.WriteLine($"PASS {checks} acquisition checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "charts-a") { CheckChartRecipesA(); Console.WriteLine($"PASS {checks} chart recipe A checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "charts-b") { CheckChartRecipesB(); Console.WriteLine($"PASS {checks} chart recipe B checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "charts-c") { CheckChartRecipesC(); Console.WriteLine($"PASS {checks} chart recipe C checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "charts-d") { CheckChartReplayD(); Console.WriteLine($"PASS {checks} chart replay D checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "probit-replay") { CheckProbitReplay(); Console.WriteLine($"PASS {checks} dose-response replay checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "probit-extreme") { CheckProbitExtremeGroup(); Console.WriteLine($"PASS {checks} dose-response extreme-group checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "housekeeping") { CheckChartHousekeeping(); Console.WriteLine($"PASS {checks} chart housekeeping checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "analysis-replay") { CheckAnalysisReplay(); Console.WriteLine($"PASS {checks} analysis replay checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "routed-replay") { CheckRoutedReplay(); Console.WriteLine($"PASS {checks} routed replay checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "records") { CheckRecord(); CheckThreshold(); CheckHash(); CheckScript(); CheckTable(); CheckFollowOn(); CheckRealRuns(); CheckRegressionFollowOn(); CheckTwoLevel(); CheckColumnLocator(); Console.WriteLine($"PASS {checks} saved record checks"); return 0; }
            CheckRecord();
            CheckNativeAxisLabels();
            CheckRecipeGeneralSimplicity();
            CheckRecipeAdvancedSimplicity();
            CheckChartRecipeSimplicity();
            CheckRecipeSurvivalMethodSimplicity();
            CheckAcquisition();
            CheckChartRecipesA();
            CheckChartRecipesB();
            CheckChartRecipesC();
            CheckChartReplayD();
            CheckProbitReplay();
            CheckProbitExtremeGroup();
            CheckChartHousekeeping();
            CheckAnalysisReplay();
            CheckRoutedReplay();
            CheckThreshold();
            CheckHash();
            CheckRecipes();
            CheckScript();
            CheckTable();
            CheckRecipesAgainstHelp();
            CheckStop();
            CheckLoessChart();
            CheckMethodComparisonCharts();
            CheckInverseRecipes();
            CheckFollowOn();
            CheckRealRuns();
            CheckOptions();
            CheckOptions2();
            CheckOptions3();
            CheckScriptWindowNumbers();
            CheckRegressionFollowOn();
            CheckTwoLevel();
            CheckColumnLocator();
            CheckBoxWhisker();
            CheckTukeyHinges();
            CleanTemporaryFolders(started);
            Console.WriteLine($"PASS {checks} Continue in R checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message.StartsWith("FAIL") ? ex.Message : "FAIL " + ex);
            return 1;
        }
    }

    /// <summary>An operation's definition, read as the program reads it.</summary>
    private static Operation Load(string name)
    {
        string file = Path.Combine(Metadata("Operations"), name + ".xml");
        using TextReader reader = File.OpenText(file);
        Operation operation = (Operation)new XmlSerializer(typeof(Operation)).Deserialize(reader);
        operation.FixAfterLoading();
        return operation;
    }

    /// <summary>The run of an operation with the given answers: the record it left at its last report step, and its outputs.</summary>
    private static (OperationRun run, StepOutput output) Run(string name, Dictionary<string, object> answers, ParameterBag context = null, Action<IFillable> amend = null)
    {
        Host host = new(answers, amend);
        ITemplateProcessor processor = new TemplateProcessor(host);
        StepOutput output = processor.Execute(Load(name), context ?? new ParameterBag());   // a context: the earlier run a follow-on operation continues
        return (host.LastRun, output);
    }

    private static JsonElement Record(OperationRun run)
    {
        if (run?.Record == null) throw new Exception("FAIL the run left no record");
        return JsonDocument.Parse(run.Record).RootElement.Clone();
    }

    private static DoubleVariable Column(string title, double[] values, WorksheetOrigin origin = null) => new(values, title) { Origin = origin };

    /// <summary>A frame of columns read from a sheet of a workbook, as the grid gives them: each column with its origin.</summary>
    private static DataFrame SheetFrame(string workbook, string sheet, int firstColumn, int topRow, params (string title, double[] values)[] columns)
    {
        DataFrame frame = new();
        int c = firstColumn;
        foreach ((string title, double[] values) in columns)
            frame.Variables.Add(Column(title, values, new WorksheetOrigin(workbook, sheet, c++, topRow, values.Length + 1, StatsDirect.Utilities.DataAcquisitionMode.NumericReplaceMissing, true, false, 1)));
        return frame;
    }
}

/// <summary>The host of a run in these checks: answers the prompts from what it was given, keeps the run the report step hands it, and shows nothing.</summary>
internal sealed class Host : ITemplateHost, IProgressBar
{
    private readonly Dictionary<string, object> answers;
    private readonly Action<IFillable> amend;
    public OperationRun LastRun { get; private set; }
    public Host(Dictionary<string, object> answers, Action<IFillable> amend = null) { this.answers = answers; this.amend = amend; }

    public Operation Operation { get; set; }
    public IDictionary<string, ParameterBag> SessionParametersPerOperation { get; } = new Dictionary<string, ParameterBag>();
    public ParameterBag SessionParametersAcrossOperations => new();
    public SDPreferences Preferences { get; } = System.Reflection.DispatchProxy.Create<SDPreferences, PreferencesProxy>();
    public ParameterBag Amend(IFillable options, ParameterBag context) { amend?.Invoke(options); return context; }
    public bool CanCombine(Parameter parameter) => false;
    public void Error(string message, string caption) => throw new Exception("the operation refused: " + message);
    public void Warning(string message, string caption) { }
    public bool GetBoolean(string prompt, string title, bool initialValue, out bool cancelled) { cancelled = false; return initialValue; }
    public ParameterBag FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context) => throw new NotImplementedException();
    public ParameterBag FillParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context, bool shouldCombine)
    {
        if (parameter.HasAcquireIfTrue && !parameter.AcquireIfTrue(processor, context)) return new ParameterBag();   // not acquired, as the dialog leaves such a parameter
        if (parameter is OptionsParameter unnamed && string.IsNullOrEmpty(parameter.Name))
        {
            answers.TryGetValue("options", out object selection);
            var unnamedChoices = selection as Dictionary<string, bool>;
            ParameterBag ticks = new();
            foreach (OptionsOption option in unnamed.Options)
                ticks.AddInput(option.Name, unnamedChoices != null && unnamedChoices.TryGetValue(option.Name, out bool enabled) ? enabled : option.Selected);
            return ticks;
        }
        if (!answers.TryGetValue(parameter.Name, out object value)) throw new Exception($"the operation asked for {parameter.Name}, which these checks do not answer");
        if (value is Queue<object> passes) value = passes.Count > 0 ? passes.Dequeue() : throw new Exception($"the operation asked for {parameter.Name} more times than these checks answer");   // a prompt asked in each pass of a loop
        if (value == null) { ParameterBag skipped = new(); skipped[parameter.Name] = null; return skipped; }   // an optional parameter skipped: the dialog notes it as a null entry
        if (parameter is OptionsParameter options && value is Dictionary<string, bool> chosen)
        {
            // as the program's dialog fills a set of options: one boolean input per option, under the option's name
            ParameterBag ticks = new();
            foreach (OptionsOption option in options.Options)
                ticks.AddInput(option.Name, chosen.TryGetValue(option.Name, out bool on) && on);
            return ticks;
        }
        if (parameter is Double2By2Parameter table && value is double[] counts)
        {
            // as the program's dialog fills a 2 by 2 table: four named counts, nothing under the table's own name
            ParameterBag cells = new();
            cells.AddInput(table.TopLeftName, counts[0]);
            cells.AddInput(table.TopRightName, counts[1]);
            cells.AddInput(table.BottomLeftName, counts[2]);
            cells.AddInput(table.BottomRightName, counts[3]);
            return cells;
        }
        if (parameter is FrameParameter frameParameter && !string.IsNullOrEmpty(frameParameter.AppendToFrame) && value is DataFrame part)
        {
            // as the grid appends a frame asked in each pass of a loop to the accumulated frame the operation names (the survival plot's groups)
            ParameterBag both = new(parameter.Name, FilledParameterFactory.Input(part));
            DataFrame whole = context != null && context.ContainsKey(frameParameter.AppendToFrame) ? context[frameParameter.AppendToFrame].AsDataFrame : new DataFrame();
            foreach (IVariable v in part.Variables) whole.Variables.Add(v);
            both.AddInput(frameParameter.AppendToFrame, whole);
            return both;
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

/// <summary>The preferences: six decimal places, and the default of everything else.</summary>
public class PreferencesProxy : System.Reflection.DispatchProxy
{
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        switch (method.Name)
        {
            case "get_PDecimalPlaces":
            case "get_DisplayDecimalPlaces": return 6;
            case "get_DefaultConfidenceInterval": return 0.95;
            case "get_MetaExact": return Program.MetaExact;   // the exact methods of the meta-analyses, for the checks that want them
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

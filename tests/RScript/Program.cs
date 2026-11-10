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
    internal static double MetaCC;   // the continuity correction the preferences give the meta-analyses' tables with an empty cell (0, a half, unless a check sets it)
    internal static bool DelayContinuityCorrection;   // whether the preferences delay that correction in the pooling
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static void Check(bool ok, string description) { if (!ok) throw new Exception("FAIL " + description); checks++; Console.WriteLine("PASS " + description); }
    private static readonly string TemporaryRoot = CreateTemporaryRoot();
    private static string CreateTemporaryRoot()
    {
        // R and CreateProcess still have working-directory length limits on
        // Windows. Keep the private parent short even under sandbox Temp paths.
        string root = Path.Combine(Path.GetTempPath(), "sd-r-" + Guid.NewGuid().ToString("N")[..16]);
        Directory.CreateDirectory(root);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (Environment.ExitCode != 0) return;
            try { RemoveOwnedTemporaryFolder(root); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };
        return root;
    }
    private static void RemoveOwnedTemporaryFolder(string folder)
    {
        string path = Path.GetFullPath(folder);
        if (!path.Equals(TemporaryRoot, StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith(TemporaryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test cleanup cannot delete a folder outside this run's temporary root.");
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
    private static string Metadata(string key) => typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value;

    [STAThread]
    static int Main()
    {
        try
        {
            CheckTemporaryCleanupIsolation();
            _ = typeof(StatsDirect.R.RScriptWriter).Assembly.GetType("StatsDirect.UI.SdApplication").GetProperty("SoleInstance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);   // the application object, whose making registers the engine's functions, as the program does at its start
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "posthoc-weighted")
            {
                StatsDirect.R.RRecipes.Folder = Metadata("Recipes");
                CheckPosthocWeighted537(Path.Combine(TemporaryRoot, "weighted-refit"));
                CheckDuplicatePredictorReplay(Path.Combine(TemporaryRoot, "duplicate-predictors"));
                Console.WriteLine($"PASS {checks} weighted refit checks");
                return 0;
            }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "models539")
            {
                CheckModels539(Path.Combine(TemporaryRoot, "models-539"));
                Console.WriteLine($"PASS {checks} 5.3.9 model checks");
                return 0;
            }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") is string recipes539 && recipes539.StartsWith("recipes539", StringComparison.Ordinal))
            {
                CheckRecipes539(Path.Combine(TemporaryRoot, "recipes-539"), recipes539 == "recipes539" ? null : recipes539["recipes539-".Length..]);
                Console.WriteLine($"PASS {checks} 5.3.9 recipe checks");
                return 0;
            }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") is string recipes5310 && recipes5310.StartsWith("recipes5310", StringComparison.Ordinal))
            {
                CheckRecipes5310(Path.Combine(TemporaryRoot, "recipes-5310"), recipes5310 == "recipes5310" ? null : recipes5310["recipes5310-".Length..]);
                Console.WriteLine($"PASS {checks} 5.3.10 recipe checks");
                return 0;
            }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "posthoc-meta")
            {
                CheckPosthocMeta537(Path.Combine(TemporaryRoot, "posthoc-meta"));
                Console.WriteLine($"PASS {checks} post-release meta-analysis checks");
                return 0;
            }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") is string meta539 && (meta539 == "meta-539" || meta539.StartsWith("meta-539-", StringComparison.Ordinal)))
            {
                string part = meta539 == "meta-539" ? null : meta539["meta-539-".Length..];
                if (part is not (null or "forest" or "bias" or "summary" or "empty" or "limits"))
                    throw new ArgumentException("Unknown 5.3.9 meta-analysis test group: " + meta539);
                CheckMetaReview539(Path.Combine(TemporaryRoot, "meta-539"), part);
                Console.WriteLine($"PASS {checks} 5.3.9 meta-analysis checks");
                return 0;
            }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "posthoc-missing")
            {
                CheckMetaMissingStudies(Path.Combine(TemporaryRoot, "meta-missing"));
                Console.WriteLine($"PASS {checks} incomplete-study checks");
                return 0;
            }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") is string deferred && deferred.StartsWith("deferred", StringComparison.Ordinal))
            {
                if (deferred is not ("deferred538" or "deferred-capture" or "deferred-matched" or "deferred-models" or "deferred-independent" or "deferred-meta"))
                    throw new ArgumentException("Unknown deferred test group: " + deferred);
                StatsDirect.R.RRecipes.Folder = Metadata("Recipes");
                string folder = Path.Combine(TemporaryRoot, "deferred-538"); Directory.CreateDirectory(folder);
                if (deferred is "deferred538" or "deferred-capture") CheckDeferredRecord538(folder);
                if (deferred is "deferred538" or "deferred-matched") CheckDeferredMatched538(folder);
                if (deferred is "deferred538" or "deferred-models") CheckDeferredModels538(folder);
                if (deferred is "deferred538" or "deferred-independent") CheckDeferredIndependent538(folder);
                if (deferred is "deferred538" or "deferred-meta") CheckDeferredMeta538(folder);
                Console.WriteLine($"PASS {checks} deferred 5.3.8 checks"); return 0;
            }
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
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "review-acquisition") { StatsDirect.R.RRecipes.Folder = Metadata("Recipes"); CheckDummyPredictorReplay(Path.Combine(TemporaryRoot, "statsdirect-dummy-review-" + Guid.NewGuid().ToString("N"))); Console.WriteLine($"PASS {checks} dummy predictor acquisition checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "paired-missing") { StatsDirect.R.RRecipes.Folder = Metadata("Recipes"); CheckPairedMissingReplay(Path.Combine(TemporaryRoot, "statsdirect-paired-missing-" + Guid.NewGuid().ToString("N"))); Console.WriteLine($"PASS {checks} paired missing-data replay checks"); return 0; }
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
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "follow-on-plots") { CheckFollowOnPlots(); Console.WriteLine($"PASS {checks} follow-on plot checks"); return 0; }
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "acquisition-modes") { StatsDirect.R.RRecipes.Folder = Metadata("Recipes"); string folder = Path.Combine(TemporaryRoot, "statsdirect-acquisition-modes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); CheckAcquisitionModeReplay(folder); Console.WriteLine($"PASS {checks} acquisition-mode replay checks"); return 0; }
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
            CheckFollowOnPlots();
            CheckRealRuns();
            CheckOptions();
            CheckOptions2();
            CheckOptions3();
            if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") != "headless") CheckScriptWindowNumbers();
            CheckRegressionFollowOn();
            CheckTwoLevel();
            CheckColumnLocator();
            CheckBoxWhisker();
            CheckTukeyHinges();
            string coverage = Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT_ONLY") == "headless" ? "headless Continue in R" : "Continue in R";
            Console.WriteLine($"PASS {checks} {coverage} checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message.StartsWith("FAIL") ? ex.Message : "FAIL " + ex);
            Console.WriteLine("Retained test artifacts: " + TemporaryRoot);
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

    /// <summary>The run of an operation whose answers also go through each parameter's validators, as the picker puts them, with
    /// what the validators refuse: the run itself goes on, so that the record of a selection the picker would refuse can be checked.</summary>
    private static (OperationRun run, StepOutput output, IReadOnlyList<string> refusals) RunAtPicker(string name, Dictionary<string, object> answers)
    {
        Host host = new(answers) { Validates = true };
        ITemplateProcessor processor = new TemplateProcessor(host);
        StepOutput output = processor.Execute(Load(name), new ParameterBag());
        return (host.LastRun, output, host.Refusals);
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
    /// <summary>Whether each filled parameter is put through its validators as the program's picker puts it.</summary>
    public bool Validates { get; init; }
    /// <summary>The validators that refused a filled parameter: its name and the validator's.</summary>
    public List<string> Refusals { get; } = new();
    private static readonly MethodInfo validate = typeof(StatsDirect.R.RScriptWriter).Assembly
        .GetType("StatsDirect.TemplateProcessing.ValidationProcessor").GetMethod("Validate", BindingFlags.Static | BindingFlags.Public);

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
        ParameterBag filled = Fill(processor, parameter, context);
        if (Validates && parameter.Validators != null && filled != null && filled.ContainsKey(parameter.Name) && filled[parameter.Name] != null)
            foreach (Validator validator in parameter.Validators)
                if (!validator.HasTestIfTrueExpression || (bool)processor.Evaluate(validator.TestIfTrueExpression, filled))
                {
                    var result = (StatsDirect.TemplateProcessing.ValidationResult)validate.Invoke(null, new object[] { this, validator.ValidatorName, parameter, filled, parameter.ValidationFailMessage });
                    if (result.Validity != StatsDirect.TemplateProcessing.Validity.Valid) Refusals.Add(parameter.Name + ": " + validator.ValidatorName);
                }
        return filled;
    }
    private ParameterBag Fill(ITemplateProcessor processor, Parameter parameter, ParameterBag context)
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
        if (value is Func<ITemplateProcessor, Parameter, ParameterBag, ITemplateHost, ParameterBag> acquire)
            return acquire(processor, parameter, context, this);
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
            case "get_MetaCC": return Program.MetaCC;
            case "get_DelayContinuityCorrection": return Program.DelayContinuityCorrection;
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

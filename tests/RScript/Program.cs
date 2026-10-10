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
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;
    private static void Check(bool ok, string description) { if (!ok) throw new Exception("FAIL " + description); checks++; Console.WriteLine("PASS " + description); }
    private static string Metadata(string key) => typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value;

    [STAThread]
    static int Main()
    {
        try
        {
            BuiltinRegistry.SoleInstance.AddAll(StatsDirect.Builtins.Registry.GetFunctionRegistry());   // the engine's functions, as the program registers them at its start
            CheckRecord();
            CheckThreshold();
            CheckHash();
            CheckRecipes();
            CheckScript();
            CheckTable();
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
    private static (OperationRun run, StepOutput output) Run(string name, Dictionary<string, object> answers)
    {
        Host host = new(answers);
        ITemplateProcessor processor = new TemplateProcessor(host);
        StepOutput output = processor.Execute(Load(name), new ParameterBag());
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
    public OperationRun LastRun { get; private set; }
    public Host(Dictionary<string, object> answers) { this.answers = answers; }

    public Operation Operation { get; set; }
    public IDictionary<string, ParameterBag> SessionParametersPerOperation { get; } = new Dictionary<string, ParameterBag>();
    public ParameterBag SessionParametersAcrossOperations => new();
    public SDPreferences Preferences { get; } = System.Reflection.DispatchProxy.Create<SDPreferences, PreferencesProxy>();
    public ParameterBag Amend(IFillable options, ParameterBag context) => context;
    public bool CanCombine(Parameter parameter) => false;
    public void Error(string message, string caption) => throw new Exception("the operation refused: " + message);
    public void Warning(string message, string caption) { }
    public bool GetBoolean(string prompt, string title, bool initialValue, out bool cancelled) { cancelled = false; return initialValue; }
    public ParameterBag FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context) => throw new NotImplementedException();
    public ParameterBag FillParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context, bool shouldCombine)
    {
        if (!answers.TryGetValue(parameter.Name, out object value)) throw new Exception($"the operation asked for {parameter.Name}, which these checks do not answer");
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
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

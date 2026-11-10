using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.Utilities;

// Exercise the production validator dispatch with the actual authored frames.
// Retained missing rows are allowed only where the operation opts into joint omission.
internal static class ValidationPolicyChecks
{
    static int checks, failures;
    static readonly MethodInfo validate = typeof(StatsDirect.Builtins.Regress).Assembly
        .GetType("StatsDirect.TemplateProcessing.ValidationProcessor")
        .GetMethod("ValidateGeneric", BindingFlags.NonPublic | BindingFlags.Static);
    static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) failures++;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  validation policy: {message}");
    }
    static string Root()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, "StatsDirectUI", "Assets", "Operations"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find operation definitions for validation checks.");
    }
    internal static string Operations => Path.Combine(Root(), "StatsDirectUI", "Assets", "Operations");
    static T Parameter<T>(string operation, string name, string element) where T : Parameter
    {
        XNamespace ns = "http://www.statsdirect.com/schemas/Operation.xsd";
        var document = XDocument.Load(Path.Combine(Root(), "StatsDirectUI", "Assets", "Operations", operation + ".xml"));
        var source = document.Descendants(ns + element).Single(item => (string)item.Element(ns + "name") == name);
        var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(element) { Namespace = ns.NamespaceName });
        using var reader = source.CreateReader();
        return (T)serializer.Deserialize(reader);
    }
    static Validity Validate(Parameter parameter, string rule, params double[] values)
    {
        var bag = new ParameterBag();
        bag.AddInput(parameter.Name, new DataFrame(new DoubleVariable(values, "validation fixture")));
        return ((ValidationResult)validate.Invoke(null, new object[] { rule, parameter, bag, null })).Validity;
    }
    static ValidationResult Validate(Parameter parameter, string rule, DataFrame frame)
    {
        var bag = new ParameterBag();
        bag.AddInput(parameter.Name, frame);
        return (ValidationResult)validate.Invoke(null, new object[] { rule, parameter, bag, parameter.ValidationFailMessage });
    }
    /// <summary>One column of cells, its title first, as the program acquires it in the parameter's own mode.</summary>
    static DataFrame Acquired(FrameParameter parameter, params object[] cells)
    {
        var sheet = new MatchedCells(new[] { cells });
        return MatchedSelectionChecks.Read(sheet.Selection(new[] { (0, 0, cells.Length) }), parameter.SelectionMode, 0, 1);
    }
    public static int Run()
    {
        checks = failures = 0;
        try
        {
            var data = Parameter<FrameParameter>("Gini", "data", "frame");
            var weights = Parameter<FrameParameter>("Gini", "weights", "frame");
            var cases = Parameter<FrameParameter>("ConditionalLogisticRegression", "case-control", "frame");
            var counts = Parameter<SpecialParameter>("Chi2byNWithoutTrend", "data", "special");
            Check(data.PreserveRowAlignment && data.Validators.Any(v => v.ValidatorName == "NonNegative"), "Gini data deserializes its aligned nonnegative policy");
            Check(weights.PreserveRowAlignment && weights.Validators.Any(v => v.ValidatorName == "Integer"), "Gini weights deserialize their aligned integer policy");
            Check(cases.PreserveRowAlignment && cases.Validators.Any(v => v.ValidatorName == "Boolean"), "conditional case/control deserializes its aligned Boolean policy");

            double m = Constant.MISSING;
            double[] padded = Enumerable.Repeat(m, 200).ToArray();
            padded[0] = 0; padded[2] = 1;
            Check(Validate(cases, "Boolean", padded) == Validity.Valid, "conditional case/control accepts interior missing values and a long unused tail");
            Check(Validate(cases, "Boolean", 0, m, 2) == Validity.Invalid, "conditional case/control still refuses an observed non-binary value");
            Check(Validate(data, "NonNegative", 0, m, 12, m) == Validity.Valid, "Gini data permits joint omission of missing rows");
            Check(Validate(weights, "Integer", 1, m, 2, m) == Validity.Valid && Validate(weights, "NonNegative", 1, m, 2, m) == Validity.Valid,
                "Gini frequency weights permit missing rows through both authored value checks");
            Check(Validate(weights, "Integer", 1, m, 1.5) == Validity.Invalid, "Gini weights still refuse an observed fractional frequency");
            Check(Validate(data, "NonNegative", 1, m, -1) == Validity.Invalid, "Gini still refuses observed negative data");

            foreach (var (rule, valid, invalid) in new[] {
                ("Boolean", 1d, 2d), ("Integer", 2d, 1.5d), ("NonNegative", 0d, -1d),
                ("Positive", 1d, 0d), ("ZeroToOneInclusive", 0d, 1.1d), ("ZeroToOneExclusive", .5d, 0d) })
            {
                Check(Validate(data, rule, valid, m) == Validity.Valid, rule + " ignores only the missing sentinel in an aligned frame");
                Check(Validate(data, rule, valid, m, invalid) == Validity.Invalid, rule + " still validates every observed value");
                if (rule != "Integer")
                    Check(Validate(counts, rule, valid, m) == Validity.Invalid, rule + " keeps the legacy count-table missing-value refusal");
            }
            var legacy = Parameter<FrameParameter>("Gini", "data", "frame");
            legacy.PreserveRowAlignment = false;
            Check(Validate(legacy, "NonNegative", 1, m) == Validity.Invalid && Validate(legacy, "Boolean", 1, m) == Validity.Invalid,
                "ordinary frame parameters retain their previous missing-data policy");
            foreach (Parameter parameter in new Parameter[] { data, weights, cases, counts })
                Check(Validate(parameter, "NoMissingData", 1, m) == Validity.Invalid, "explicit NoMissingData remains binding for " + parameter.GetType().Name + "/" + parameter.Name);
            Check(Validate(data, "PositiveRows", 1, m) == Validity.Invalid, "row-total validation is unchanged by the aligned value policy");

            // A blank identifier or category, or a typed missing marker, is a missing value of a classifier frame. Explicit NoMissingData
            // refuses it with its message, as it refuses a missing number, before the analysis meets the missing code.
            foreach (var (operation, name) in new[] { ("TimeSeriesSummary", "subjectIds"), ("TimeSeriesSummary", "groups"), ("ContractFrequencies", "categories") })
            {
                var parameter = Parameter<FrameParameter>(operation, name, "frame");
                Check(parameter.SelectionMode == DataAcquisitionMode.CategoryCombineAllColumns && parameter.Validators.Any(v => v.ValidatorName == "NoMissingData"),
                    operation + " " + name + " keeps the selected category rows for NoMissingData");
                foreach (var (label, cells) in new[] {
                    ("a blank cell", new object[] { "Subject", "p1", "p2", null, "p4", "p2" }),
                    ("a typed * marker", new object[] { "Subject", "p1", "*", "p3", "p4", "p2" }),
                    ("a final blank cell", new object[] { "Subject", "p1", "p2", "p3", "p4", null }),
                    ("a final typed * marker", new object[] { "Subject", "p1", "p2", "p3", "p4", "*" }),
                    ("every value present", new object[] { "Subject", "p1", "p2", "p3", "p4", "p2" }) })
                {
                    DataFrame frame = Acquired(parameter, cells);
                    ValidationResult result = Validate(parameter, "NoMissingData", frame);
                    bool missing = label != "every value present";
                    Check(frame.Variables.Single() is ClassifierVariable classifier && classifier.Length == 5
                        && classifier.Data.Count(id => id == Constant.MISSING) == (missing ? 1 : 0)
                        && result.Validity == (missing ? Validity.Invalid : Validity.Valid)
                        && (!missing || result.FailedValidationMessage == "Data with missing values cannot be used here"),
                        $"{operation} {name} with {label} is {(missing ? "refused by NoMissingData" : "accepted")}: {result.Validity} {result.FailedValidationMessage}");
                }
            }
            // The same through the template processor, each parameter filled and validated as the program fills it: a blank identifier or
            // category stops the run with the message the user is shown (the analysis failed on it with an exception), while complete
            // data give the analysis as before.
            foreach (bool blank in new[] { true, false })
            {
                DataFrame subjects = Acquired(Parameter<FrameParameter>("TimeSeriesSummary", "subjectIds", "frame"), "Subject", "p1", "p2", blank ? null : "p1", "p2");
                var (summary, summaryRefusal) = ValidatingHost.Run("TimeSeriesSummary", new()
                {
                    ["times"] = new DataFrame(new DoubleVariable(new double[] { 0, 0, 1, 1 }, "Time")), ["observations"] = new DataFrame(new DoubleVariable(new double[] { 1, 2, 3, 4 }, "Level")),
                    ["subjectIds"] = subjects, ["groups"] = null, ["ci"] = 0.95, ["doExactP"] = false
                });
                var group = summary == null ? null : ((IEnumerable<ParameterBag>)summary["*group"].AsObject).Single();
                var times = group == null ? null : ((IEnumerable<ParameterBag>)group["*time"].AsObject).ToList();
                Check(blank ? summary == null && summaryRefusal == "Data with missing values cannot be used here"
                            : summaryRefusal == null && times.Select(t => t["mean"].AsDouble).SequenceEqual(new[] { 1.5, 3.5 }),
                    "time series summary with " + (blank ? "a blank subject identifier stops at its validation message" : "complete identifiers gives its means by time") + ": " + (summaryRefusal ?? "completed"));
                DataFrame categories = Acquired(Parameter<FrameParameter>("ContractFrequencies", "categories", "frame"), "Category", "red", "blue", blank ? null : "red", "blue");
                var (contracted, contractRefusal) = ValidatingHost.Run("ContractFrequencies", new() { ["type"] = "categories", ["categories"] = categories });
                var labels = contracted?["output"].AsDataFrame.Variables.First() as StringVariable;
                var frequencies = contracted?["output"].AsDataFrame.Variables.Last() as DoubleVariable;
                Check(blank ? contracted == null && contractRefusal == "Data with missing values cannot be used here"
                            : contractRefusal == null && labels.Data.SequenceEqual(new[] { "red", "blue" }) && frequencies.Data.SequenceEqual(new double[] { 2, 2 }),
                    "contraction of frequencies with " + (blank ? "a blank category stops at its validation message" : "complete categories counts each category") + ": " + (contractRefusal ?? "completed"));
            }
            // The groups of a time series summary are a classifier of the same kind, behind a prompt that may be skipped. A blank group, or
            // one typed as a missing marker, is the missing code, which the analysis took as the index of a group (Index was out of range):
            // the prompt refuses it, as the subject identifiers' does. Complete groups, and a run with its groups skipped, are as before.
            var groupsPrompt = Parameter<FrameParameter>("TimeSeriesSummary", "groups", "frame");
            Check(groupsPrompt.SelectionMode == DataAcquisitionMode.CategoryCombineAllColumns && groupsPrompt.CancelSkipsParameter == "Skip"
                && groupsPrompt.Validators?.Any(v => v.ValidatorName == "NoMissingData") == true,
                "TimeSeriesSummary groups deserializes its category mode, its skip and its NoMissingData");
            Check(new[] { null, new ParameterBag() }.All(skipped => ((ValidationResult)validate.Invoke(null, new object[] { "NoMissingData", groupsPrompt, skipped, null })).Validity == Validity.Valid),
                "TimeSeriesSummary groups skipped leaves NoMissingData nothing to refuse");
            DataFrame Groups(object third) => Acquired(groupsPrompt, "Group", "a", "b", third, "b");
            foreach (var (label, groups, expected) in new (string, DataFrame, string)[] {
                ("a blank group", Groups(null), null), ("a group typed as *", Groups("*"), null), ("a group typed as * (missing)", Groups("* (missing)"), null),
                ("complete groups", Groups("a"), "a: AUC 2, means 1,3; b: AUC 3, means 2,4"), ("its groups skipped", null, "all: AUC 2.5, means 1.5,3.5") })
            {
                ParameterBag summary = null;
                string refusal;
                try
                {
                    (summary, refusal) = ValidatingHost.Run("TimeSeriesSummary", new()
                    {
                        ["times"] = new DataFrame(new DoubleVariable(new double[] { 0, 0, 1, 1 }, "Time")), ["observations"] = new DataFrame(new DoubleVariable(new double[] { 1, 2, 3, 4 }, "Level")),
                        ["subjectIds"] = Acquired(Parameter<FrameParameter>("TimeSeriesSummary", "subjectIds", "frame"), "Subject", "p1", "p2", "p1", "p2"), ["groups"] = groups, ["ci"] = 0.95, ["doExactP"] = false
                    });
                }
                catch (Exception ex) { refusal = "no validation message, but " + (ex.InnerException ?? ex).GetType().Name; }
                var invariant = System.Globalization.CultureInfo.InvariantCulture;
                string summarised = summary == null ? null : string.Join("; ", ((IEnumerable<ParameterBag>)summary["*group"].AsObject).Select(g =>
                    g["groupName"].AsObject + ": AUC " + g["aucMean"].AsDouble.ToString("R", invariant) + ", means " + string.Join(",", ((IEnumerable<ParameterBag>)g["*time"].AsObject).Select(t => t["mean"].AsDouble.ToString("R", invariant)))));
                int missing = groups == null ? 0 : ((ClassifierVariable)groups.Variables.Single()).Data.Count(id => id == Constant.MISSING);
                Check(expected == null ? missing == 1 && summary == null && refusal == "Data with missing values cannot be used here" : missing == 0 && refusal == null && summarised == expected,
                    "time series summary with " + label + (expected == null ? " stops at its validation message" : " is summarised as before") + ": " + (refusal ?? summarised));
            }

            double[] distinctWithTail = new[] { 1d, 2d, 3d, 4d }.Concat(Enumerable.Repeat(m, 196)).ToArray();
            Check(Validate(data, "CheckForNonDummiedCategories", distinctWithTail) == Validity.Valid,
                "an unused tail cannot turn distinct observed numbers into a categorical warning");
            Check(Validate(legacy, "CheckForNonDummiedCategories", distinctWithTail) == Validity.NeedMoreInformation,
                "the legacy predictor-category threshold is preserved");
            Check(Validate(data, "CheckForNonDummiedCategories", 1, 2, 3, 1, 2, 3, m) == Validity.NeedMoreInformation,
                "genuinely repeated observed categories still prompt for coding");
        }
        catch (Exception ex) { Check(false, ex.ToString()); }
        Console.WriteLine($"{checks} validation policy checks; {failures} failures");
        return failures;
    }
}

/// <summary>A host that fills each parameter as SdApplication.FillParameter does, without its dialogs: the answer given, then every
/// validator of the parameter through the production dispatch. A refusal ends the run with the message the program would show.</summary>
public class ValidatingHost : DispatchProxy
{
    sealed class NoProgress : IProgressBar
    {
        public bool Update(double fractionComplete) => false;
        public void Finish() { }
        public void Dispose() { }
    }
    public class Preferences : DispatchProxy
    {
        protected override object Invoke(MethodInfo method, object[] arguments)
        {
            if (method.Name is "get_PDecimalPlaces" or "get_DisplayDecimalPlaces") return 6;
            if (method.Name == "get_DefaultConfidenceInterval") return 0.95;
            return method.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    sealed class Refused : Exception { internal Refused(string message) : base(message) { } }
    static readonly MethodInfo dispatch = typeof(StatsDirect.Builtins.Regress).Assembly.GetType("StatsDirect.TemplateProcessing.ValidationProcessor").GetMethod("Validate", BindingFlags.Public | BindingFlags.Static);
    readonly SDPreferences preferences = Create<SDPreferences, Preferences>();
    readonly Dictionary<string, ParameterBag> session = new();
    Dictionary<string, object> answers;
    string refusal;

    /// <summary>The operation's run with these answers: its outputs, or null and the validation message that stopped it.</summary>
    internal static (ParameterBag outputs, string refusal) Run(string operation, Dictionary<string, object> answers)
    {
        ITemplateHost host = Create<ITemplateHost, ValidatingHost>();
        var self = (ValidatingHost)(object)host;
        self.answers = answers;
        using TextReader reader = File.OpenText(Path.Combine(ValidationPolicyChecks.Operations, operation + ".xml"));
        var definition = (Operation)new XmlSerializer(typeof(Operation)).Deserialize(reader);
        definition.FixAfterLoading();
        try { return (((ITemplateProcessor)new TemplateProcessor(host)).Execute(definition, new ParameterBag()).ParameterBag, null); }
        catch (Exception) when (self.refusal != null) { return (null, self.refusal); }
    }

    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        switch (method.Name)
        {
            case "FillParameter":
                {
                    var (processor, parameter, context) = ((ITemplateProcessor)arguments[0], (Parameter)arguments[1], (ParameterBag)arguments[2]);
                    if (parameter.HasAcquireIfTrue && !parameter.AcquireIfTrue(processor, context)) return new ParameterBag();
                    if (!answers.TryGetValue(parameter.Name, out object value)) throw new InvalidOperationException("Unanswered parameter " + parameter.Name);
                    // an answer of null is the prompt skipped: a prompt that may be skipped has its validators run on the empty answer, as the program runs them
                    ParameterBag filled = value == null ? null : new(parameter.Name, FilledParameterFactory.Input(value));
                    if (filled != null || parameter.CancelSkipsParameter != null)
                        foreach (Validator validator in parameter.Validators ?? Enumerable.Empty<Validator>())
                        {
                            var result = (ValidationResult)dispatch.Invoke(null, new object[] { this, validator.ValidatorName, parameter, filled, parameter.ValidationFailMessage });
                            if (result.Validity != Validity.Valid) throw new Refused(refusal = result.FailedValidationMessage ?? result.Validity.ToString());
                        }
                    if (filled == null) { ParameterBag skipped = new(); skipped[parameter.Name] = null; return skipped; }
                    return filled;
                }
            case "StartProgress": return new NoProgress();
            case "GetScriptEngine": return ScriptEngine.CanHandle((string)arguments[0]) ? new ScriptEngine() : null;
            case "get_Preferences": return preferences;
            case "get_SessionParametersPerOperation": return session;
            case "get_SessionParametersAcrossOperations": return new ParameterBag();
            case "Amend": return arguments[1];
            case "GetBoolean": arguments[3] = false; return arguments[2];
            case "RoundU" or "pval" or "pval_half": return Convert.ToDouble(arguments[0], System.Globalization.CultureInfo.InvariantCulture).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            case "Error": throw new InvalidOperationException("Unexpected refusal: " + arguments[0]);
        }
        return method.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

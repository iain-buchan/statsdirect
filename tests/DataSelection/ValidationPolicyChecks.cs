using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;

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

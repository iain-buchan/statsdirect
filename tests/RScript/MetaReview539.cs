using System.Globalization;
using System.Text;
using StatsDirect.Data;
using StatsDirect.Charting;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // StatsDirect 5.3.9, meta-analysis: the native forest plots number unlabelled studies as the report's tables and the R
    // script do. Every run goes through the real cell acquisition and the real template run.
    // One part can be checked alone: "forest".
    private static void CheckMetaReview539(string folder, string part = null)
    {
        RRecipes.Folder = Metadata("Recipes");
        if (part is null or "forest") CheckMeta539ForestLabels();
    }

    // A frame of one numeric column acquired from cells, as the worksheet gives it.
    private static DataFrame Meta539Frame(RecordCells cells, int column, int rows) => cells.Acquire(column, 0, rows, DataAcquisitionMode.NumericReplaceMissing);

    private static IList<ParameterBag> Meta539Rows(ParameterBag bag, string block) =>
        bag.TryGetValue(block, out FilledParameter value) && value?.AsObject is IList<ParameterBag> rows ? rows : new List<ParameterBag>();

    // The native forest plots draw the kept studies with the labels the report's tables and the R replay give them: with no
    // labels, their places among the kept studies, not their worksheet rows
    private static void CheckMeta539ForestLabels()
    {
        double[] exposed = { 100, 150, 110, 80, 200, 120 }, events = { 20, 35, 23, 10, 60, 50 };
        double[] control = { 100, 140, 125, 90, 210, 115 }, controlEvents = { 12, 20, 18, 12, 30, 15 };
        var cases = new (string operation, (string name, double[] values)[] columns, Dictionary<string, object> settings, string labels, string format)[]
        {
            ("RelativeRiskMeta", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}"),
            ("RiskDifference", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}"),
            ("PetoMeta", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}"),
            ("Mantel", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}"),
            ("MetaIncidenceRateRatio", new[] { ("pt1", exposed), ("a", events), ("pt2", control), ("b", controlEvents) }, new(), "strata", "stratum {0}"),
            ("MetaIncidenceRateDifference", new[] { ("pt1", exposed), ("a", events), ("pt2", control), ("b", controlEvents) }, new(), "strata", "stratum {0}"),
            ("Effect", new[] { ("en", new double[] { 34, 50, 39, 24, 41, 28 }), ("cn", new double[] { 39, 44, 35, 29, 48, 36 }), ("em", new double[] { 11, 8, 9, 10, 14, 13 }),
                ("es", new[] { 2.1, 2.5, 2.8, 3.4, 2.2, 2.6 }), ("cm", new double[] { 8, 10, 9, 8, 11, 8 }), ("cs", new[] { 1.8, 2, 3.1, 3.2, 2.9, 2.4 }) },
                new() { ["type"] = "d", ["g"] = null }, "strata", "stratum {0}"),
            ("MetaSummary", new[] { ("y", new[] { .05, -.02, .10, .03, .08, -.04 }), ("se_y", new[] { .03, .04, .05, .02, .06, .03 }) },
                new() { ["type"] = "riskDifference", ["use_ci"] = "false" }, "studies", "study {0}"),
            ("ProportionMeta", new[] { ("sn", exposed), ("sr", events) }, new() { ["method"] = "arcsine" }, "strata", "stratum {0}")
        };
        foreach (var (operation, columns, settings, labels, format) in cases)
        foreach (bool labelled in new[] { false, true })
        {
            // the third row is blank in every column, so the analysis keeps five studies; with labels, the fifth row's is blank
            object[][] raw = columns.Select(column => column.values.Select((v, i) => i == 2 ? null : (object)v).ToArray()).ToArray();
            object[] names = { "Study names", "Alpha", "Beta", "Gamma", "Delta", null, "Zeta" };   // a text column's first cell is its title
            var cells = new RecordCells(labelled ? raw.Append(names).ToArray() : raw);
            Dictionary<string, object> answers = new(settings) { ["gamma"] = .95, [labels] = labelled ? cells.Acquire(columns.Length, 0, names.Length, DataAcquisitionMode.Text) : null };
            for (int i = 0; i < columns.Length; i++) answers[columns[i].name] = Meta539Frame(cells, i, 6);
            var (_, native) = Run(operation, answers);
            string[] expected = labelled ? new[] { "Alpha", "Beta", "Delta", string.Format(inv, format, 4), "Zeta" }
                : Enumerable.Range(1, 5).Select(i => string.Format(inv, format, i)).ToArray();
            List<IList<string>> titles = new();
            foreach (ParameterBag chart in Meta539Rows(native.ParameterBag, "*chart"))
                if (chart.TryGetValue("chart", out FilledParameter value) && value.AsObject is ChartDefinition definition)
                {
                    if (definition.ChartOptions is ForestishOptions forest) titles.Add(forest.Titles);
                    else if (definition.ChartOptions is CorrelationOptions correlation) titles.Add(correlation.Titles);
                }
            string label = operation + (labelled ? " with a blank label" : " without labels");
            Check(titles.Count >= 1 && titles.All(t => t.Skip(1).Take(5).SequenceEqual(expected)),
                label + ": the native forest plots label the kept studies " + string.Join(", ", expected) + " after a blank row"
                + (titles.Count > 0 ? "" : " (no forest plot)") + string.Concat(titles.Where(t => !t.Skip(1).Take(5).SequenceEqual(expected)).Take(1).Select(t => " [drawn: " + string.Join(", ", t.Skip(1).Take(5)) + "]")));
        }
    }
}

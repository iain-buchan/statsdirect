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
    // script do; the R replays of tables with empty cells apply the native exclusion rules and continuity correction; the
    // bias indicators are given only with more than three studies, at the bias tests' level; the summary meta-analysis of a
    // difference is labelled and plotted as a difference; and two studies have the test-based I-squared interval when Q > k.
    // The replays also hold at the limits: a pooled proportion of 0 or 1, and summary limits selected the wrong way round.
    // Every run goes through the real cell acquisition, the real template run and its saved record, and R.
    // One part can be checked alone: "forest", "bias", "summary", "empty" or "limits".
    private static void CheckMetaReview539(string folder, string part = null)
    {
        RRecipes.Folder = Metadata("Recipes");
        bool exact = MetaExact, delay = DelayContinuityCorrection;
        double correction = MetaCC;
        try
        {
            if (part is null or "forest") CheckMeta539ForestLabels();
            if (part is null or "bias") CheckMeta539BiasIndicators(Path.Combine(folder, "bias"));
            if (part is null or "summary") CheckMeta539SummaryDifferences(Path.Combine(folder, "summary-differences"));
            if (part is null or "empty") CheckMeta539EmptyCells(Path.Combine(folder, "empty-cells"));
            if (part is null or "limits") CheckMeta539Limits(Path.Combine(folder, "limits"));
        }
        finally { MetaExact = exact; MetaCC = correction; DelayContinuityCorrection = delay; }
    }

    // A frame of one numeric column acquired from cells, as the worksheet gives it.
    private static DataFrame Meta539Frame(RecordCells cells, int column, int rows) => cells.Acquire(column, 0, rows, DataAcquisitionMode.NumericReplaceMissing);

    private static double Meta539Number(ParameterBag bag, string key) =>
        bag.TryGetValue(key, out FilledParameter value) && value?.AsObject is double number ? number : Constant.MISSING;

    private static IList<ParameterBag> Meta539Rows(ParameterBag bag, string block) =>
        bag.TryGetValue(block, out FilledParameter value) && value?.AsObject is IList<ParameterBag> rows ? rows : new List<ParameterBag>();

    // a figure of the report wherever it is, in the bag or in a block of it (the effect size analysis pools in a block)
    private static double Meta539Find(ParameterBag bag, string key)
    {
        if (bag.TryGetValue(key, out FilledParameter value) && value?.AsObject is double number) return number;
        foreach (var pair in bag.Pairs)
            if (pair.Value?.AsObject is IList<ParameterBag> rows)
                foreach (ParameterBag row in rows)
                    if (row.ContainsKey(key)) return Meta539Number(row, key);
        return Constant.MISSING;
    }

    private static string Meta539Value(double value) => value == Constant.MISSING || double.IsNaN(value) ? "NA"
        : double.IsPositiveInfinity(value) ? "Inf" : double.IsNegativeInfinity(value) ? "-Inf" : value.ToString("R", inv);

    private static string Meta539Vector(IEnumerable<double> values) => "c(" + string.Join(", ", values.Select(Meta539Value)) + ")";

    // R's comparison of a replayed figure with the native one: the same missing and infinite figures, and the finite ones
    // within a relative 2e-7 (the exact and score limits are found by different searches)
    private const string Meta539Same = """
        same <- function(x, y, tol = 2e-7) {
          x <- as.numeric(x); y <- as.numeric(y)
          length(x) == length(y) && all(is.na(x) == is.na(y)) &&
            all(ifelse(is.na(y), TRUE, ifelse(is.infinite(y), !is.na(x) & x == y, abs(x - y) <= tol * abs(y) + 1e-9)))
        }
        """;

    private static string Meta539Compare(string what, string expression, IEnumerable<double> expected, double tolerance = 2e-7) =>
        "if (!same(c(" + expression + "), " + Meta539Vector(expected) + ", " + tolerance.ToString("R", inv) + ")) stop('" + what + " differs: ', paste(format(c(" +
        expression + "), digits = 12), collapse = ' '), ' against " + string.Join(" ", expected.Select(Meta539Value)) + "')\n";

    // Runs one batch of replays in one R process, each in its own environment, and lists those that did not reach their mark
    private static List<string> Meta539Batch(string folder, IList<(string label, string code)> replays)
    {
        StringBuilder batch = new("pdf(NULL)\n" + Meta539Same + "\n");
        foreach (var (label, code) in replays)
            batch.Append("local(tryCatch({\n").Append(code).Append("\ncat('META_539_OK ").Append(label).Append("\\n')\n")
                .Append("}, error = function(e) cat('META_539_ERROR ").Append(label).Append(": ', conditionMessage(e), '\\n', sep = '')))\n");
        string output = DeferredR538(batch.ToString(), folder, 600).Replace("\r\n", "\n");
        List<string> failures = new();
        foreach (var (label, _) in replays)
            if (output.Contains("META_539_OK " + label + "\n")) Check(true, label + ": the replay completes with the native figures");
            else failures.Add(output.Split('\n').FirstOrDefault(l => l.StartsWith("META_539_ERROR " + label + ":", StringComparison.Ordinal)) ?? label + ": no result");
        return failures;
    }

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

    // The bias indicators of all nine meta-analysis replays, as the report has them: none with two or three studies (the replay
    // used to stop at Egger's P, or print what the report withholds), and Egger's and Harbord's intervals at the bias tests' level
    // 1 - 2 (1 - confidence), not always 90%. The heterogeneity figures are compared too, with two studies' test-based I-squared
    // interval where Q > k.
    private static void CheckMeta539BiasIndicators(string folder)
    {
        double[][] two = { new double[] { 30, 120, 15, 110 }, new double[] { 12, 90, 20, 95 } };
        double[][] three = { new double[] { 10, 50, 12, 50 }, new double[] { 25, 60, 10, 60 }, new double[] { 14, 70, 15, 70 } };
        double[][] six = { new double[] { 20, 100, 12, 100 }, new double[] { 35, 150, 20, 140 }, new double[] { 10, 80, 12, 90 },
            new double[] { 60, 200, 30, 210 }, new double[] { 50, 120, 15, 115 }, new double[] { 23, 110, 18, 125 } };
        // effect sizes, summary statistics, correlations and proportions: two studies that differ, three, and six
        int[][] subsets = { new[] { 0, 4 }, new[] { 0, 1, 2 }, new[] { 0, 1, 2, 3, 4, 5 } };
        double[] en = { 27, 16, 25, 62, 9, 63 }, em = { 18.5, 60, 10.72, 9.2, 3.75, 60 }, es = { 14.9, 29.2, 6.46, 6.16, 2.55, 13.3 };
        double[] cn = { 17, 15, 66, 66, 22, 40 }, cm = { 5.4, 55, 6.92, 6.92, 1.05, 46.3 }, cs = { 17.3, 29.2, 6.83, 6.83, 2.12, 18.6 };
        double[] y = { 1.5, .8, 2, 1.15, 2.6, 1.3 }, se = { .12, .18, .21, .15, .25, .2 };
        double[] r = { .3, .52, .1, .45, .21, .6 }, n = { 50, 80, 40, 120, 60, 90 };
        double[] total = { 311, 65, 67, 285, 73, 116 }, responders = { 214, 58, 59, 182, 65, 66 };
        var analyses = new (string operation, string begg, bool harbord, string block)[]
        {
            ("RelativeRiskMeta", "kt", true, "*egger"), ("RiskDifference", "begg", false, "*egger"), ("PetoMeta", "begg", true, "*egger"),
            ("Mantel", "begg", true, "*egger"), ("MantelHaenszelScreen", "begg", true, "*egger"), ("Effect", "begg", false, "*egger"),
            ("MetaSummary", "kt", false, "*bias"), ("MetaCorrelation", "bm", false, "*bias"), ("ProportionMeta", "begg", true, "*egger")
        };
        List<string> failures = new();
        foreach (var (operation, beggName, harbord, block) in analyses)
        {
            List<(string, string)> replays = new();
            bool twoStudyInterval = false;
            for (int set = 0; set < 3; set++)
            foreach (double confidence in new[] { .8, .95, .99 })
            foreach (bool exact in new[] { false, true })
            {
                MetaExact = exact;
                string label = operation + "-" + (set == 0 ? 2 : set == 1 ? 3 : 6) + "-studies-" + (exact ? "exact" : "approximate") + "-" + confidence.ToString(inv);
                // two studies that differ
                int[] take = set > 0 ? subsets[set] : operation == "Effect" ? new[] { 3, 4 } : operation == "MetaCorrelation" ? new[] { 2, 5 } : subsets[0];
                double[][] tables = set == 0 ? two : set == 1 ? three : six;
                Dictionary<string, object> answers = new() { ["gamma"] = confidence };
                DataFrame Columns(params double[][] columns)
                {
                    var cells = new RecordCells(columns.Select(c => c.Cast<object>().ToArray()).ToArray());
                    DataFrame frame = new();
                    for (int i = 0; i < columns.Length; i++) frame.Variables.Add(Meta539Frame(cells, i, columns[i].Length).Variables[0]);
                    return frame;
                }
                DataFrame One(double[] values) => Columns(values);
                double[] Pick(double[] values) => take.Select(i => values[i]).ToArray();
                switch (operation)
                {
                    case "MantelHaenszelScreen":
                        answers = new() { ["cco"] = confidence, ["try_exact"] = exact, ["plot_forest"] = false,
                            ["data"] = Columns(tables.SelectMany(t => new[] { t[0], t[1] - t[0] }).ToArray(), tables.SelectMany(t => new[] { t[2], t[3] - t[2] }).ToArray()) };
                        break;
                    case "Effect":
                        answers["type"] = "d"; answers["strata"] = null; answers["g"] = null;
                        answers["en"] = One(Pick(en)); answers["em"] = One(Pick(em)); answers["es"] = One(Pick(es));
                        answers["cn"] = One(Pick(cn)); answers["cm"] = One(Pick(cm)); answers["cs"] = One(Pick(cs));
                        break;
                    case "MetaSummary":
                        answers["type"] = "oddsRatio"; answers["use_ci"] = "false"; answers["studies"] = null;
                        answers["y"] = One(Pick(y)); answers["se_y"] = One(Pick(se));
                        break;
                    case "MetaCorrelation":
                        answers["studies"] = null; answers["r"] = One(Pick(r)); answers["n"] = One(Pick(n));
                        break;
                    case "ProportionMeta":
                        answers["method"] = "arcsine"; answers["strata"] = null; answers["sn"] = One(Pick(total)); answers["sr"] = One(Pick(responders));
                        break;
                    default:
                        answers["strata"] = null;
                        answers["sr"] = One(tables.Select(t => t[0]).ToArray()); answers["sn"] = One(tables.Select(t => t[1]).ToArray());
                        answers["xr"] = One(tables.Select(t => t[2]).ToArray()); answers["xn"] = One(tables.Select(t => t[3]).ToArray());
                        break;
                }
                var (run, native) = Run(operation, answers);
                ParameterBag bag = native.ParameterBag;
                RScript script = RScriptWriter.Write(Record(run), null, null);
                Check(script.HasRecipe && !script.DataUnavailable, label + " replays its recorded studies");
                StringBuilder checks = new();
                checks.Append(Meta539Compare("I-squared", "i2", new[] { Meta539Find(bag, "isq") }))
                      .Append(Meta539Compare("I-squared interval", "i2_ci", new[] { Meta539Find(bag, "llisq"), Meta539Find(bag, "ulisq") }, 1e-6));
                if (set == 0 && !exact)
                {
                    if (Meta539Find(bag, "qc") > 2 == (Meta539Find(bag, "llisq") != Constant.MISSING))
                        Check(true, label + ": two studies have the test-based I-squared interval exactly when Q > k");
                    else failures.Add(label + ": the native report gives Q = " + Meta539Find(bag, "qc").ToString("R", inv) + " and the interval " +
                        Meta539Value(Meta539Find(bag, "llisq")) + " to " + Meta539Value(Meta539Find(bag, "ulisq")));
                    twoStudyInterval |= Meta539Find(bag, "llisq") != Constant.MISSING;
                }
                // the printed lines, as a reader sees them: the sentence of the report in place of the indicators, or Egger's and
                // Harbord's limits at the report's level
                void Printed(string line, ParameterBag figures, string level) =>
                    checks.Append("line <- grep('^").Append(line).Append(": bias = ', printed, value = TRUE)\n")
                          .Append("expected <- paste0('(', ").Append(Meta539Value(Math.Round(Meta539Number(figures, level), 2))).Append(", '% CI = ', six(")
                          .Append(Meta539Value(Meta539Number(figures, "cl"))).Append("), ' to ', six(").Append(Meta539Value(Meta539Number(figures, "cu"))).Append("), ')')\n")
                          .Append("if (length(line) != 1 || !grepl(expected, line, fixed = TRUE)) stop('the printed ").Append(line)
                          .Append(" line differs: ', paste(line, collapse = ' | '), ' against ', expected)\n");
                if (Meta539Rows(bag, "*fewStrata").Count == 1)
                    checks.Append("if (exists('egger_figures', inherits = FALSE) || exists('").Append(beggName)
                          .Append("', inherits = FALSE) || any(grepl('^Egger: ', printed))) stop('the replay gives bias indicators that the report withholds')\n")
                          .Append("if (!any(printed == 'Bias indicators need more than three studies with an estimate')) stop('the replay does not say why there are no bias indicators')\n");
                else
                {
                    ParameterBag egger = Meta539Rows(bag, block)[0];
                    checks.Append(Meta539Compare("Begg-Mazumdar", beggName + "$estimate, " + beggName + "$p.value", new[] { "tau", "p2" }.Select(key => Meta539Number(egger, key))));
                    Printed("Egger", egger, "pc_egger");
                    checks.Append(Meta539Compare("Egger", "egger_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(egger, key))))
                          .Append(Meta539Compare("bias test level", "100 * bias_level", new[] { Meta539Number(egger, "pc_egger") }));
                    if (harbord)
                    {
                        ParameterBag h = Meta539Rows(bag, "*harbord")[0];
                        Printed(operation == "ProportionMeta" ? "Harbord" : "Harbord-Egger", h, "pc_harbord");
                        checks.Append(Meta539Compare("Harbord", "harbord_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(h, key))))
                              .Append(Meta539Compare("Harbord level", "100 * bias_level", new[] { Meta539Number(h, "pc_harbord") }));
                    }
                }
                Check(set == 2 == (Meta539Rows(bag, "*fewStrata").Count == 0), label + ": the native report gives bias indicators only with more than three studies");
                replays.Add((label, "printed <- capture.output({\n" + script.Text + "\n})\ncat(printed, sep = '\\n')\n" + checks));
            }
            if (twoStudyInterval) Check(true, operation + ": the two studies, which differ (Q > k), have the test-based I-squared interval in the report and the replay");
            else failures.Add(operation + ": the two studies, which differ (Q > k), have no test-based I-squared interval");
            failures.AddRange(Meta539Batch(Path.Combine(folder, operation), replays));
        }
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " bias indicator replays disagree with the native analysis");
    }

    // A summary meta-analysis of a difference (a risk difference, or another difference) is pooled as it is: the replay labels it
    // as the report does, draws its forest plots on a linear scale about 0 instead of stopping at a logarithmic axis, and gives
    // the native figures, from standard errors or from confidence limits
    private static void CheckMeta539SummaryDifferences(string folder)
    {
        double[] y = { .05, -.02, .10, .03, .08, -.04 }, se = { .03, .04, .05, .02, .06, .03 };
        List<(string, string)> replays = new();
        foreach (string type in new[] { "riskDifference", "otherDifference" })
        foreach (bool limits in new[] { false, true })
        foreach (bool exact in new[] { false, true })
        foreach (double confidence in new[] { .9, .95 })
        {
            MetaExact = exact;
            string label = "MetaSummary-" + type + (limits ? "-limits-" : "-se-") + (exact ? "exact" : "approximate") + "-" + confidence.ToString(inv);
            double[] lower = y.Select((v, i) => v - 1.6 * se[i]).ToArray(), upper = y.Select((v, i) => v + 1.6 * se[i]).ToArray();
            var cells = new RecordCells(new[] { y, limits ? lower : se, upper }.Select(c => c.Cast<object>().ToArray()).ToArray());
            Dictionary<string, object> answers = new() { ["gamma"] = confidence, ["type"] = type, ["use_ci"] = limits ? "true" : "false", ["studies"] = null, ["y"] = Meta539Frame(cells, 0, 6) };
            if (limits) { answers["ll_y"] = Meta539Frame(cells, 1, 6); answers["ul_y"] = Meta539Frame(cells, 2, 6); }
            else answers["se_y"] = Meta539Frame(cells, 1, 6);
            var (run, native) = Run("MetaSummary", answers);
            ParameterBag bag = native.ParameterBag;
            RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
            Check(script.HasRecipe && !script.DataUnavailable, label + " replays its acquired summaries");
            string stat = Convert.ToString(bag["stat"].AsObject, inv);
            string checks = Meta539Compare("fixed effects", "back(pooled), back(pooled + c(-1, 1) * z * se_pooled), z_fixed", new[] { "rmh", "from_fixed", "to_fixed", "z" }.Select(key => Meta539Number(bag, key)))
                + Meta539Compare("random effects", "back(pooled_dl), back(pooled_dl + c(-1, 1) * z * se_dl), z_dl", new[] { "dsrr", "dsll", "dsul", "dz" }.Select(key => Meta539Number(bag, key)))
                + Meta539Compare("heterogeneity", "q, tau2, i2", new[] { "qc", "tausq", "isq" }.Select(key => Meta539Number(bag, key)))
                + Meta539Compare("study limits", "lower, upper", Meta539Rows(bag, "*studies").Select(row => Meta539Number(row, "from")).Concat(Meta539Rows(bag, "*studies").Select(row => Meta539Number(row, "to"))))
                + Meta539Compare("Egger", "egger_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(Meta539Rows(bag, "*bias")[0], key)))
                + "if (ratio || stat != " + RScriptWriter.Vector(new object[] { stat }) + ") stop('the summary statistic is not ', " + RScriptWriter.Vector(new object[] { stat }) + ")\n"
                + "printed <- capture.output(forest(w, pooled, se_pooled, 'check'))\n"
                + "if (!identical(par('xlog'), FALSE)) stop('a difference is drawn on a logarithmic axis')\n";
            replays.Add((label, script.Text + "\n" + checks));
            Check(script.Text.Contains("type <- \"" + type + "\""), label + " records the type of the summary statistic");
        }
        List<string> failures = Meta539Batch(folder, replays);
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " summary difference replays disagree with the native analysis");
    }

    // Tables with empty cells, from the sparse and boundary sets of the meta-analysis benchmarks (tests/MetaAnalysis) and the
    // study with every subject of one group an event and none of the other (10/10 v 0/10), put through the four analyses of
    // fourfold tables and the typed-in Mantel-Haenszel test under the calculation options (exact methods on or off; the
    // treatment arm, 0.5 or 0.01 correction; delayed or not). The R replay must complete and give the native figures, the
    // excluded studies and the corrections.
    private static void CheckMeta539EmptyCells(string folder)
    {
        // events and total of the first group, events and total of the second, one row for each study
        var sets = new (string name, double[][] rows)[]
        {
            ("one.sided", new[] { new double[] { 10, 10, 0, 10 }, new double[] { 12, 50, 8, 50 }, new double[] { 20, 60, 15, 60 }, new double[] { 9, 40, 10, 40 } }),
            ("one.sided.control", new[] { new double[] { 0, 10, 10, 10 }, new double[] { 12, 50, 8, 50 }, new double[] { 20, 60, 15, 60 }, new double[] { 9, 40, 10, 40 } }),
            ("single.zero", new[] { new double[] { 0, 50, 4, 50 }, new double[] { 5, 60, 9, 60 }, new double[] { 8, 70, 10, 70 }, new double[] { 12, 80, 15, 80 } }),
            ("sparse01", new[] { new double[] { 0, 10, 0, 9 }, new double[] { 1, 29, 2, 11 }, new double[] { 0, 6, 1, 5 } }),
            ("sparse02", new[] { new double[] { 2, 19, 0, 8 }, new double[] { 1, 28, 3, 29 }, new double[] { 0, 17, 2, 11 }, new double[] { 0, 5, 1, 6 },
                new double[] { 0, 12, 0, 9 }, new double[] { 0, 10, 0, 5 }, new double[] { 0, 5, 1, 7 }, new double[] { 3, 23, 0, 6 } }),
            ("sparse05", new[] { new double[] { 0, 8, 3, 34 }, new double[] { 1, 14, 2, 8 }, new double[] { 0, 14, 0, 32 }, new double[] { 3, 31, 0, 15 },
                new double[] { 0, 10, 0, 8 }, new double[] { 1, 15, 3, 34 }, new double[] { 0, 17, 2, 8 }, new double[] { 0, 18, 0, 15 } }),
            ("sparse12", new[] { new double[] { 0, 7, 0, 33 }, new double[] { 0, 33, 0, 5 }, new double[] { 1, 16, 0, 8 }, new double[] { 0, 9, 1, 11 }, new double[] { 1, 7, 0, 11 } }),
            ("none.second", new[] { new double[] { 14, 23, 0, 26 }, new double[] { 3, 14, 0, 12 }, new double[] { 11, 29, 0, 30 }, new double[] { 9, 38, 0, 61 },
                new double[] { 10, 37, 0, 58 }, new double[] { 3, 26, 0, 76 } }),
            ("none.first", new[] { new double[] { 0, 22, 8, 65 }, new double[] { 0, 44, 15, 55 }, new double[] { 0, 120, 7, 32 }, new double[] { 0, 12, 3, 14 },
                new double[] { 0, 10, 9, 21 }, new double[] { 0, 63, 5, 21 } }),
            ("all.first", new[] { new double[] { 11, 11, 19, 69 }, new double[] { 48, 48, 5, 27 }, new double[] { 17, 17, 9, 41 }, new double[] { 76, 76, 28, 87 },
                new double[] { 32, 32, 3, 22 }, new double[] { 11, 11, 4, 23 } }),
            ("double.zero", new[] { new double[] { 15, 35, 10, 33 }, new double[] { 0, 52, 0, 53 }, new double[] { 3, 15, 48, 176 }, new double[] { 18, 30, 6, 23 },
                new double[] { 0, 28, 0, 30 }, new double[] { 22, 90, 3, 19 }, new double[] { 9, 15, 22, 58 } }),
            ("double.all", new[] { new double[] { 12, 35, 5, 21 }, new double[] { 8, 15, 7, 19 }, new double[] { 15, 15, 19, 19 }, new double[] { 16, 34, 7, 14 },
                new double[] { 1, 20, 4, 24 }, new double[] { 15, 40, 37, 75 }, new double[] { 8, 30, 10, 47 } }),
            ("one.each", new[] { new double[] { 16, 57, 6, 21 }, new double[] { 1, 1, 0, 1 }, new double[] { 4, 17, 8, 19 }, new double[] { 20, 54, 7, 27 }, new double[] { 3, 12, 36, 99 } }),
            ("empty.group", new[] { new double[] { 59, 172, 12, 35 }, new double[] { 11, 45, 23, 63 }, new double[] { 49, 148, 27, 42 }, new double[] { 0, 0, 3, 10 }, new double[] { 8, 40, 27, 63 } }),
            ("one.subject", new[] { new double[] { 1, 1, 0, 0 }, new double[] { 4, 17, 8, 32 }, new double[] { 13, 43, 25, 53 }, new double[] { 8, 25, 10, 41 }, new double[] { 1, 13, 6, 21 } }),
            // given the margins, the total of the a cells is exactly as probable at 10 as at the observed 3 (7056 ways each), so
            // the exact two sided P counts both, as the report does, whatever the rounding of the two probabilities
            ("tied.total", new[] { new double[] { 0, 6, 6, 9 }, new double[] { 3, 9, 2, 2 } })
        };
        var settings = new (bool exact, double cc, bool delay, double level)[]
        {
            (true, -9, false, .95), (false, -9, false, .95), (true, .5, false, .9), (false, .5, false, .99),
            (true, .01, false, .99), (false, .01, false, .9), (true, -9, true, .95), (false, .5, true, .95), (false, -9, true, .9)
        };
        List<string> failures = new();
        // the typed-in Mantel-Haenszel test pools the tables as the odds ratio meta-analysis does, with the exact methods of its
        // own option: each table's exposed and unexposed columns, cases above non-cases
        foreach (string operation in new[] { "RelativeRiskMeta", "RiskDifference", "PetoMeta", "Mantel", "MantelHaenszelScreen" })
        foreach (var setting in settings)
        {
            // the Peto odds ratio has no continuity correction, so it needs only the two methods
            if (operation == "PetoMeta" && (setting.cc != -9 || setting.delay)) continue;
            MetaExact = setting.exact; MetaCC = setting.cc; DelayContinuityCorrection = setting.delay;
            string settingLabel = (setting.exact ? "exact" : "approximate") + "-cc" + setting.cc.ToString(inv) + (setting.delay ? "-delayed" : "") + "-" + setting.level.ToString(inv);
            List<(string, string)> replays = new();
            foreach (var (name, rows) in sets)
            {
                string label = operation + "-" + name + "-" + settingLabel;
                RecordCells cells;
                Dictionary<string, object> answers;
                if (operation == "MantelHaenszelScreen")
                {
                    double[][] typed = { rows.SelectMany(r => new[] { r[0], r[1] - r[0] }).ToArray(), rows.SelectMany(r => new[] { r[2], r[3] - r[2] }).ToArray() };
                    cells = new RecordCells(typed.Select(c => c.Cast<object>().ToArray()).ToArray());
                    DataFrame data = new();
                    for (int j = 0; j < 2; j++) data.Variables.Add(Meta539Frame(cells, j, typed[j].Length).Variables[0]);
                    answers = new() { ["cco"] = setting.level, ["try_exact"] = setting.exact, ["plot_forest"] = false, ["data"] = data };
                }
                else
                {
                    cells = new RecordCells(Enumerable.Range(0, 4).Select(j => rows.Select(r => (object)r[j]).ToArray()).ToArray());
                    answers = new() { ["gamma"] = setting.level, ["strata"] = null,
                        ["sr"] = Meta539Frame(cells, 0, rows.Length), ["sn"] = Meta539Frame(cells, 1, rows.Length),
                        ["xr"] = Meta539Frame(cells, 2, rows.Length), ["xn"] = Meta539Frame(cells, 3, rows.Length) };
                }
                OperationRun run; StepOutput native;
                try { (run, native) = Run(operation, answers); }
                catch (Exception ex)
                {
                    // every set has a study that can be pooled, so the native analysis must complete
                    failures.Add(label + ": the native analysis refuses it (" + ex.GetBaseException().Message + ")");
                    continue;
                }
                ParameterBag bag = native.ParameterBag;
                RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
                Check(script.HasRecipe && !script.DataUnavailable, label + " replays the acquired tables");
                if (script.Text.Contains("delay_cc <- ") == (operation != "PetoMeta")) Check(true, label + " replays with the recorded continuity correction");
                else failures.Add(label + ": the replay does not take the recorded continuity correction");
                replays.Add((label, script.Text + "\n" + Meta539Assertions(operation == "MantelHaenszelScreen" ? "Mantel" : operation, bag)));
            }
            failures.AddRange(Meta539Batch(Path.Combine(folder, operation + "-" + settingLabel), replays));
        }
        // every disagreement is listed before the check fails, so that one run shows them all
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " empty-cell replays disagree with the native analysis");
    }

    // The replays at the limits of their data. A proportion meta-analysis in which no study has a responder, every subject of
    // every study responds, or so few respond that the Miller inverse gives 0: the report pools the proportion to 0 or 1 (and
    // its near limit with it) and gives no Harbord figure, since no score then has a variance; the replay used to stop at
    // Harbord's regression ("0 (non-NA) cases"), or print a pooled proportion just inside 0 or 1 with Harbord figures. And a
    // summary meta-analysis whose limits were selected the wrong way round, the upper before the lower: the report puts them
    // the right way round, where the replay used to take each standard error as negative and give Egger's bias the other sign.
    private static void CheckMeta539Limits(string folder)
    {
        List<(string, string)> replays = new();
        var proportions = new (string name, double[] n, double[] r)[]
        {
            ("no-responder-equal", new double[] { 20, 20, 20, 20, 20 }, new double[] { 0, 0, 0, 0, 0 }),
            ("no-responder", new double[] { 12, 20, 35, 50, 80 }, new double[] { 0, 0, 0, 0, 0 }),
            ("all-responders-equal", new double[] { 20, 20, 20, 20, 20 }, new double[] { 20, 20, 20, 20, 20 }),
            ("all-responders", new double[] { 12, 20, 35, 50, 80 }, new double[] { 12, 20, 35, 50, 80 }),
            ("one-responder", new double[] { 12, 20, 35, 50, 80 }, new double[] { 0, 0, 1, 0, 0 }),
            ("ordinary", new double[] { 311, 65, 67, 285, 73, 116 }, new double[] { 214, 58, 59, 182, 65, 66 })
        };
        foreach (var (name, n, r) in proportions)
        foreach (string method in new[] { "arcsine", "doubleArcsine" })
        foreach (double confidence in new[] { .95, .8 })
        foreach (bool exact in new[] { false, true })
        {
            MetaExact = exact;
            string label = "ProportionMeta-" + name + "-" + method + "-" + confidence.ToString(inv) + (exact ? "-exact" : "");
            var cells = new RecordCells(new[] { n, r }.Select(c => c.Cast<object>().ToArray()).ToArray());
            Dictionary<string, object> answers = new() { ["gamma"] = confidence, ["method"] = method, ["strata"] = null,
                ["sn"] = Meta539Frame(cells, 0, n.Length), ["sr"] = Meta539Frame(cells, 1, n.Length) };
            var (run, native) = Run("ProportionMeta", answers);
            ParameterBag bag = native.ParameterBag;
            RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
            Check(script.HasRecipe && !script.DataUnavailable, label + " replays its acquired studies");
            ParameterBag egger = Meta539Rows(bag, "*egger").Single(), harbord = Meta539Rows(bag, "*harbord").Single();
            IEnumerable<double> Figures(params string[] keys) => keys.Select(key => Meta539Number(bag, key));
            string checks = "if (!identical(grep('^Pooled proportion = ', printed, value = TRUE)[1], paste0('Pooled proportion = ', six(" + Meta539Value(Meta539Number(bag, "rmh"))
                + "), ' (', pct, 'CI = ', six(" + Meta539Value(Meta539Number(bag, "from")) + "), ' to ', six(" + Meta539Value(Meta539Number(bag, "to"))
                + "), ')'))) stop('the printed fixed effects line differs: ', grep('^Pooled proportion = ', printed, value = TRUE)[1])\n"
                + Meta539Compare("fixed effects", "fixed", Figures("rmh", "from", "to"))
                + Meta539Compare("random effects", "inv(xr), inv(xr - z * se_r), inv(xr + z * se_r)", Figures("dspr", "from_ds", "to_ds"))
                + Meta539Compare("heterogeneity", "q, tau2, i2", Figures("qc", "tausq", "isq"))
                + Meta539Compare("I-squared interval", "i2_ci", Figures("llisq", "ulisq"), 1e-6)
                + Meta539Compare("study limits", "ci[, 1], ci[, 2]", Meta539Rows(bag, "*proportions").Select(row => Meta539Number(row, "from_y"))
                    .Concat(Meta539Rows(bag, "*proportions").Select(row => Meta539Number(row, "to_y"))))
                + Meta539Compare("Begg-Mazumdar", "begg$estimate, begg$p.value", new[] { "tau", "p2" }.Select(key => Meta539Number(egger, key)))
                + Meta539Compare("Egger", "egger_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(egger, key)))
                + Meta539Compare("Harbord", "harbord_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(harbord, key)))
                + "if (sum(grepl('^Harbord: bias = ', printed)) != 1 || sum(grepl('^Egger: bias = ', printed)) != 1) stop('the replay prints the Egger and Harbord lines other than once each')\n";
            replays.Add((label, "printed <- capture.output({\n" + script.Text + "\n})\ncat(printed, sep = '\\n')\n" + checks));
        }

        // every one of the five types of summary statistic, with all of its limits, or two of them, selected the wrong way round
        foreach (string type in new[] { "oddsRatio", "relativeRisk", "otherRatio", "riskDifference", "otherDifference" })
        foreach (bool all in new[] { true, false })
        foreach (bool exact in new[] { false, true })
        {
            MetaExact = exact;
            bool ratio = type.EndsWith("Ratio", StringComparison.Ordinal) || type == "relativeRisk";
            double[] y = ratio ? new[] { 1.5, .8, 2, 1.15, 2.6, 1.3 } : new[] { .05, -.02, .10, .03, .08, -.04 };
            double[] se = { .12, .18, .21, .15, .25, .2 };
            double[] lower = y.Select((v, i) => ratio ? Math.Exp(Math.Log(v) - 1.959964 * se[i]) : v - 1.959964 * se[i]).ToArray();
            double[] upper = y.Select((v, i) => ratio ? Math.Exp(Math.Log(v) + 1.959964 * se[i]) : v + 1.959964 * se[i]).ToArray();
            double[] first = lower.Select((v, i) => all || i % 3 == 1 ? upper[i] : v).ToArray(), second = upper.Select((v, i) => all || i % 3 == 1 ? lower[i] : v).ToArray();
            string label = "MetaSummary-" + type + (all ? "-all" : "-some") + "-limits-reversed" + (exact ? "-exact" : "");
            var cells = new RecordCells(new[] { y, first, second }.Select(c => c.Cast<object>().ToArray()).ToArray());
            Dictionary<string, object> answers = new() { ["gamma"] = .95, ["type"] = type, ["use_ci"] = "true", ["studies"] = null,
                ["y"] = Meta539Frame(cells, 0, 6), ["ll_y"] = Meta539Frame(cells, 1, 6), ["ul_y"] = Meta539Frame(cells, 2, 6) };
            var (run, native) = Run("MetaSummary", answers);
            ParameterBag bag = native.ParameterBag;
            RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
            Check(script.HasRecipe && !script.DataUnavailable, label + " replays its acquired summaries");
            IList<ParameterBag> studies = Meta539Rows(bag, "*studies");
            ParameterBag bias = Meta539Rows(bag, "*bias").Single();
            string checks = Meta539Compare("Egger", "egger_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(bias, key)))
                + Meta539Compare("study limits and standard errors", "lower, upper, se", studies.Select(row => Meta539Number(row, "from"))
                    .Concat(studies.Select(row => Meta539Number(row, "to"))).Concat(studies.Select(row => Meta539Number(row, "se"))))
                + Meta539Compare("fixed effects", "back(pooled), back(pooled + c(-1, 1) * z * se_pooled), z_fixed", new[] { "rmh", "from_fixed", "to_fixed", "z" }.Select(key => Meta539Number(bag, key)))
                + Meta539Compare("random effects", "back(pooled_dl), back(pooled_dl + c(-1, 1) * z * se_dl), z_dl", new[] { "dsrr", "dsll", "dsul", "dz" }.Select(key => Meta539Number(bag, key)))
                + Meta539Compare("heterogeneity", "q, tau2, i2, i2_ci", new[] { "qc", "tausq", "isq", "llisq", "ulisq" }.Select(key => Meta539Number(bag, key)), 1e-6)
                + Meta539Compare("Begg-Mazumdar", "kt$estimate, kt$p.value", new[] { "tau", "p2" }.Select(key => Meta539Number(bias, key)));
            replays.Add((label, script.Text + "\n" + checks));
        }
        List<string> failures = Meta539Batch(folder, replays);
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " replays at the limits of their data disagree with the native analysis");
    }

    // The checks of a replay against the native output bag of its analysis
    private static string Meta539Assertions(string operation, ParameterBag bag)
    {
        StringBuilder s = new();
        void Same(string what, string expression, IEnumerable<double> expected, double tolerance = 2e-7) =>
            s.Append(Meta539Compare(what, expression, expected, tolerance));
        void Labels(string expression, IEnumerable<string> expected) =>
            s.Append("if (!identical(trimws(").Append(expression).Append("), ").Append(RScriptWriter.Vector(expected.Select(e => (object)e.Trim()).ToArray()))
             .Append(")) stop('the study labels differ: ', paste(").Append(expression).Append(", collapse = '|'))\n");
        IEnumerable<double> Column(string block, string key) => Meta539Rows(bag, block).Select(row => Meta539Number(row, key));
        IEnumerable<double> Figures(params string[] keys) => keys.Select(key => Meta539Number(bag, key));
        string block = operation switch { "RelativeRiskMeta" => "*risks", "RiskDifference" => "*differences", "PetoMeta" => "*odds", _ => "*or" };
        switch (operation)
        {
            case "RelativeRiskMeta":
                Same("relative risks", "rr", Column(block, "rr"));
                Same("study limits", "ci[, 1], ci[, 2]", Column(block, "lci").Concat(Column(block, "uci")));
                Same("variances", "v_i", Column(block, "vi"));
                Same("weights", "100 * w_mh / sum(w_mh), 100 * w_dl / sum(w_dl)", Column(block, "wt").Concat(Column(block, "dwt")));
                Same("pooled relative risk", "rr_mh, exp(log(rr_mh) + c(-1, 1) * z * sqrt(v_mh)), x2, rr_dl, dl_ci, x2_dl", Figures("rr", "from", "to", "x2", "dsrr", "dsll", "dsul", "dsx2"));
                Labels("note", Meta539Rows(bag, block).Select(row => Convert.ToString(row["lb"].AsObject, inv)));
                break;
            case "RiskDifference":
                Same("risk differences", "rd", Column(block, "rd"));
                Same("study limits", "ci[, 1], ci[, 2]", Column(block, "lci").Concat(Column(block, "uci")));
                Same("variances", "v_iv", Column(block, "vi"));
                Same("weights", "100 * w_mh / sum(w_mh), 100 * w_dl / sum(w_dl)", Column(block, "wt").Concat(Column(block, "dwt")));
                Same("pooled risk difference", "rd_mh, rd_mh + c(-1, 1) * z * se_mh, x2, rd_dl, rd_dl + c(-1, 1) * z * se_dl, x2_dl", Figures("rmh", "from", "to", "x2", "dsrd", "dsll", "dsul", "dsx2"));
                Labels("note", Meta539Rows(bag, "*inputs").Select(row => Convert.ToString(row["lb"].AsObject, inv)));
                break;
            case "PetoMeta":
                Same("Peto odds ratios", "or, lower, upper", Column(block, "or").Concat(Column(block, "lci")).Concat(Column(block, "uci")));
                Same("weights", "100 * V / sum(V)", Column(block, "wt"));
                Same("pooled Peto odds ratio", "pooled, pooled_lower, pooled_upper, z_pooled", Figures("por", "from", "to", "z"));
                Labels("note", Meta539Rows(bag, block).Select(row => Convert.ToString(row["lb"].AsObject, inv)));
                break;
            default:
                Same("odds ratios", "or, lci, uci", Column(block, "or").Concat(Column(block, "lci")).Concat(Column(block, "uci")));
                Same("variances", "vi", Column(block, "vi"));
                Same("weights", "wt_fixed, wt_random", Column(block, "wt").Concat(Column(block, "dwt")));
                Same("Mantel-Haenszel odds ratio", "or_mh, mh_ci, x2", Figures("odds", "from", "to", "chi_mantel"));
                Same("Breslow-Day", "bd", Figures("bd"));
                Same("random effects odds ratio", "or_re, re_ci, x2_re", Figures("dsor", "dsll", "dsul", "dsx2"));
                if (Meta539Rows(bag, "*cml").Count == 1)
                {
                    ParameterBag cml = Meta539Rows(bag, "*cml")[0];
                    Same("conditional maximum likelihood", "cml, fisher, midp", new[] { "eor", "llf", "ulf", "llm", "ulm" }.Select(key => Meta539Number(cml, key)));
                    Same("exact P values", "min(p_upper, p_lower), p2_fisher, p1_mid, min(1, 2 * p1_mid)", new[] { "p1f", "p2f", "p1m", "p2m" }.Select(key => Meta539Number(cml, key)));
                }
                else s.Append("if (exists('cml', inherits = FALSE)) stop('the approximate method has no exact pooled block')\n");
                Labels("note", Meta539Rows(bag, block).Select(row => Convert.ToString(row["lb"].AsObject, inv)));
                break;
        }
        Same("heterogeneity", operation == "PetoMeta" ? "Q, i2" : "q, tau2, i2", operation == "PetoMeta" ? Figures("qc", "isq") : Figures("qc", "tausq", "isq"));
        // the native search for an exact limit of I-squared is good to one part in a million
        Same("I-squared interval", "i2_ci", Figures("llisq", "ulisq"), 1e-6);
        // the bias indicators: none with three studies or fewer, as the report has them otherwise
        if (Meta539Rows(bag, "*fewStrata").Count == 1)
            s.Append("if (exists('egger_figures', inherits = FALSE)) stop('the replay gives bias indicators that the report withholds')\n");
        else
        {
            ParameterBag egger = Meta539Rows(bag, "*egger")[0];
            string begg = operation == "RelativeRiskMeta" ? "kt" : "begg";
            Same("Begg-Mazumdar", begg + "$estimate, " + begg + "$p.value", new[] { "tau", "p2" }.Select(key => Meta539Number(egger, key)));
            Same("Egger", "egger_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(egger, key)));
            if (Meta539Rows(bag, "*harbord").Count == 1)
                Same("Harbord", "harbord_figures", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(Meta539Rows(bag, "*harbord")[0], key)));
        }
        return s.ToString();
    }
}

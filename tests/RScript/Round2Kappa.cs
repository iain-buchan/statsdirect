using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private const string KappaGradeNote = "Weighted kappa uses the grade order shown in the crosstab. Code grades as numbers to set their order.";

    private static void CheckRound2Kappa()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(TemporaryRoot, "round2-kappa");
        var failures = Failed539.Count;
        (OperationRun run, StepOutput output) Columns(double level, params string[][] columns)
        {
            var cells = new RecordCells(columns.Select((column, i) =>
                new object[] { "Rater " + (i + 1) }.Concat(column.Cast<object>()).ToArray()).ToArray());
            return Run("Kappa", new()
            {
                ["responses"] = Select539(cells, columns[0].Length + 1, Enumerable.Range(0, columns.Length).ToArray()),
                ["ci"] = level,
                ["method"] = "1"
            });
        }
        (OperationRun run, StepOutput output) Table(double level, double[,] counts) =>
            Run("KappaScreen", new()
            {
                ["ci"] = level, ["method"] = "1", ["doExactP"] = false, ["iterations"] = 1000, ["seed"] = 1,
                ["responsesCrosstab"] = SheetFrame(Workbook, "Round two kappa", 0, 0, Enumerable.Range(0, counts.GetLength(1))
                    .Select(j => ("Column " + (j + 1), Enumerable.Range(0, counts.GetLength(0)).Select(i => counts[i, j]).ToArray())).ToArray())
            });

        Case539("Q3b Mac category record", () =>
        {
            string file = Path.GetFullPath(Path.Combine(Metadata("Examples"), "../fixtures/kappa/Q3b-category-record.json"));
            byte[] bytes = File.ReadAllBytes(file);
            Check(bytes.Length == 1876 && Convert.ToHexString(SHA256.HashData(bytes)) ==
                "47FAF926F99369E763D1BC354DEA58D9C1DBFF790980683F4156D3C5D2D296F6", "Q3b uses the exact Mac record bytes");
            using var record = JsonDocument.Parse(bytes);
            RScript script = RScriptWriter.Write(record.RootElement, null, _ => throw new InvalidOperationException("Q3b holds its own data"));
            Check(script.HasRecipe && !script.DataUnavailable && script.Text.Contains("Grades.xlsx"), "Windows Continue in R reads the Mac's held categories");
            var values = record.RootElement.GetProperty("values");
            string assertion = "stopifnot(n == 10, identical(grades, c('high','low','medium')))\n" +
                Equal539("kappa, se0, se, kappaw, se0w, sew, (po - spe)/(1 - spe), ac1, x2, df, x2m",
                    new[] { "kappa", "se", "seci", "kappaw", "sekw", "sekwci", "spi", "gama", "x2", "df", "x2m" }.Select(k => values.GetProperty(k).GetDouble()), 1e-10);
            string text = DeferredR538(script.Text + "\n" + assertion, Path.Combine(folder, "q3b"));
            Check(text.Contains("Kappa = 0.431818", StringComparison.Ordinal) && text.Contains(KappaGradeNote, StringComparison.Ordinal),
                "Q3b reports weighted kappa in native grade order and explains that order");
        });

        string[] first = { "10", "A", "a", "2", "B", "b", "b", "10", "a", "B", "A", "2" };
        string[] second = { "10", "A", "2", "2", "B", "b", "10", "10", "a", "A", "A", "B" };
        Case539("X8 mixed labels", () =>
        {
            var (run, result) = Columns(.95, first, second);
            ParameterBag b = result.ParameterBag;
            string[] order = { "2", "10", "A", "B", "a", "b" };
            Check(Rows539(b, "*y").Select(r => r["y"].AsString).SequenceEqual(order), "native weighted kappa orders numbers by size and text ordinally");
            string text = Replay539(run, "stopifnot(identical(grades, c('2','10','A','B','a','b')))\n" +
                Equal539("kappaw, se0w, sew", new[] { Num539(b, "kappaw"), Num539(b, "sekw"), Num539(b, "sekwci") }), Path.Combine(folder, "mixed"));
            Check(b.ContainsKey("*kappaGradeOrder") && text.Contains(KappaGradeNote, StringComparison.Ordinal),
                "text grades explain their order beside weighted kappa in the report and replay");
        });
        Case539("X8 decimal comma and text grades", () =>
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            (OperationRun run, StepOutput result) pair;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                pair = Columns(.95, "10 2 1,2 a 10 2 1,2 a".Split(), "2 10 1,2 a 10 2 1,2 a".Split());
            }
            finally { CultureInfo.CurrentCulture = previous; }
            Check(Rows539(pair.result.ParameterBag, "*y").Select(r => r["y"].AsString).SequenceEqual(new[] { "1,2", "2", "10", "a" }),
                "native kappa takes the original culture's decimal comma as part of the grade order");
            Replay539(pair.run, "stopifnot(identical(grades,c('1,2','2','10','a')))\n" +
                Equal539("kappaw", new[] { Num539(pair.result.ParameterBag, "kappaw") }), Path.Combine(folder, "comma"));
        });

        void Donner(string label, (OperationRun run, StepOutput result) pair, double level, double low, double high, bool hasInterval = true) =>
            Case539("X4 " + label, () =>
            {
                var (run, result) = pair;
                ParameterBag b = result.ParameterBag;
                if (hasInterval)
                {
                    ParameterBag row = Rows539(b, "*deci").Single();
                    Check(Math.Abs(Num539(row, "lwr") - low) <= 5.1e-7 && Math.Abs(Num539(row, "upr") - high) <= 5.1e-7,
                        label + ": native Donner-Eliasziw interval agrees with the independent roots");
                }
                string text = Replay539(run, "", Path.Combine(folder, label));
                double[] interval = Printed539(text, "^" + Figure539 + "% Donner-Eliasziw confidence interval = " + Figure539 + " to " + Figure539);
                Check(hasInterval
                    ? interval.Length == 3 && interval[0] == level && Math.Abs(interval[1] - low) <= 5.1e-7 && Math.Abs(interval[2] - high) <= 5.1e-7
                    : interval.Length == 0, label + ": replay reports the same Donner-Eliasziw interval as the native analysis");
            });

        double[,] two = { { 6, 2 }, { 1, 7 } };
        var ratings = Pairs5310(two);
        Donner("donner-columns", Columns(.95, ratings.first.Select(x => x.ToString(inv)).ToArray(), ratings.second.Select(x => x.ToString(inv)).ToArray()), 95, .137808, .867632);
        Donner("donner-90", Table(.90, two), 90, .222121, .844309);
        Donner("donner-99", Table(.99, new double[,] { { 3, 9 }, { 1, 2 } }), 99, -.740250, .296818);
        Donner("donner-perfect", Table(.95, new double[,] { { 8, 0 }, { 0, 8 } }), 95, .612785, 1);
        Donner("donner-empty-margin", Table(.95, new double[,] { { 8, 0 }, { 0, 0 } }), 95, 0, 0, false);

        foreach (double level in new[] { 0d, 1d })
            Case539("S5 invalid interval " + level, () =>
            {
                var pairs = new[]
                {
                    Columns(level, first, second),
                    Table(level, two),
                    Columns(level, first, second, first)
                };
                for (int i = 0; i < pairs.Length; i++)
                {
                    Check(Num539(pairs[i].output.ParameterBag, "pc") == 95, "native kappa uses 95% for level " + level);
                    JsonObject saved = JsonNode.Parse(pairs[i].run.Record).AsObject();
                    saved["parameters"].AsObject().Remove("kappaConfidence"); // legacy records carry the invalid level uncorrected
                    saved["parameters"]["ci"] = level;
                    RScript script = RScriptWriter.Write(JsonSerializer.SerializeToElement(saved), null, null);
                    string output = DeferredR538(script.Text + "\nstopifnot(confidence == .95)\n", Path.Combine(folder, $"fallback-{level}-{i}"));
                    Check(output.Contains("95% confidence interval", StringComparison.Ordinal), "the legacy replay uses a 95% interval too");
                }
            });

        void Maxwell(string label, string a, string b, double statistic, int df, double probability) =>
            Case539("S2 " + label, () =>
            {
                var (run, result) = Columns(.95, a.Split(), b.Split());
                ParameterBag bag = result.ParameterBag;
                Check(df == 0 ? Num539(bag, "x2") == Constant.MISSING && Num539(bag, "df") == Constant.MISSING :
                    Math.Abs(Num539(bag, "x2") - statistic) <= 1e-12 && Num539(bag, "df") == df &&
                    Math.Abs(Num539(bag, "pmaxwell") - probability) <= 1e-7, label + ": native Maxwell matches the independent calculation");
                Replay539(run, df == 0 ? "stopifnot(df == 0)\n" :
                    Equal539("x2, df, pchisq(x2, df, lower.tail=FALSE)", new[] { statistic, df, probability }), Path.Combine(folder, label));
            });
        Maxwell("isolated-grade", "a b c", "a c b", 0, 1, 1);
        Maxwell("no-disagreements", "a b", "a b", 0, 0, 0);
        Maxwell("two-disjoint-pairs", "p q r s", "q p s r", 0, 2, 1);
        Maxwell("all-joined", "a b c a", "b c a a", 0, 2, 1);
        Maxwell("asymmetric-pairs", "p q p q r s r s p r", "q p p q s r r s q s", 2d / 3, 2, Math.Exp(-1d / 3));
        Maxwell("two-triangles", "1 2 3 1 2 3 4 5 6 4 5 6 1 4", "2 3 1 1 2 3 5 6 4 4 5 6 3 6",
            .8, 4, .938448064449895);

        void Raters(string label, double kappa, double lower, double upper, params string[][] columns) =>
            Case539("S6 and multi-rater limits " + label, () =>
            {
                var (run, result) = Columns(.95, columns);
                ParameterBag b = result.ParameterBag;
                string estimate = b.ContainsKey("kc") ? "kc" : "k";
                Check(Math.Abs(Num539(b, estimate) - kappa) <= 1e-10 && Math.Abs(Num539(b, "ll") - lower) <= 5.1e-7 &&
                    Math.Abs(Num539(b, "ul") - upper) <= 5.1e-7, label + ": native kappa and limits agree with the Fleiss-Cuzick/Fleiss-Nee-Landis calculation");
                Replay539(run, Equal539("kappa, lower, upper", new[] { kappa, Num539(b, "ll"), Num539(b, "ul") }), Path.Combine(folder, label));
            });

        Raters("three-raters-two-grades", 1d / 3, -.232460, .899126, "a a b b".Split(), "a b b b".Split(), "a a b a".Split());
        Raters("three-raters-three-grades", .4, .042161, .757839, "a b c c a".Split(), "a b c b a".Split(), "a c c b b".Split());
        Raters("three-raters-stopped", .25, -.5, 1, "a b".Split(), "a b".Split(), "a a".Split());
        Raters("three-raters-varying", -67d / 108, -1, .378782, "a a".Split(), "a b".Split(), new[] { "b", null });
        Raters("three-raters-all-disagree", -.5, -.5, -.038032, "a b c".Split(), "b c a".Split(), "c a b".Split());
        Case539("multi-rater labels", () =>
        {
            var (run, result) = Columns(.95,
                "100000 2 B a 100000 2 B a".Split(),
                "100000 2 a a 100000 2 B B".Split(),
                "100000 2 B a 2 2 B a".Split());
            ParameterBag b = result.ParameterBag;
            string[] labels = { "2", "100000", "B", "a" };
            IList<ParameterBag> rows = Rows539(b, "*cats");
            Check(rows.Select(row => row["resp"].AsString).SequenceEqual(labels), "multi-rater report uses numeric then ordinal grade labels");
            Replay539(run, "stopifnot(identical(categories, c('2','100000','B','a')))\n" +
                Equal539("kappa_j, kappa", rows.Select(row => Num539(row, "k")).Append(Num539(b, "kc"))), Path.Combine(folder, "multi-labels"));
        });

        void Ac1(string label, string a, string b, double estimate, double lower, bool kappaLower) =>
            Case539("S7 and AC1 limits " + label, () =>
            {
                var (run, result) = Columns(.95, a.Split(), b.Split());
                ParameterBag bag = result.ParameterBag;
                Check(Num539(bag, kappaLower ? "from" : "to") == (kappaLower ? -1 : 1) &&
                    Num539(bag, kappaLower ? "fromw" : "tow") == (kappaLower ? -1 : 1), label + ": the existing stops for both kappas are retained");
                Check(Math.Abs(Num539(bag, "gamacil") - lower) < 5.1e-7 && Num539(bag, "gamaciu") == 1 &&
                    Math.Abs(Num539(bag, "gama") - estimate) < 1e-12, label + ": AC1 keeps its estimate and stops its bounds at the limits of the scale");
                string replay = Replay539(run, Equal539("ac1, max(-1,ac1-z*sqrt(v)), min(1,ac1+z*sqrt(v))",
                    new[] { estimate, Num539(bag, "gamacil"), 1d }), Path.Combine(folder, label));
                string text = replay[replay.IndexOf("Gwet's AC1", StringComparison.Ordinal)..];
                double[] printed = Printed539(text, "^95% confidence interval = " + Figure539 + " to " + Figure539);
                Check(printed.Length == 2 && Math.Abs(printed[0] - lower) < 5.1e-7 && printed[1] == 1,
                    label + ": the AC1 interval line gives the same limits as the report");
            });
        Ac1("upper-limit", "a b b", "a a b", 1d / 3, -.733536, false);
        Ac1("both-limits", "a b b", "b a b", -.2, -1, true);
        Ac1("upper-limit-control", "a b b a b", "a a b a b", .6, -.101218, false);

        foreach (bool typed in new[] { false, true })
            Case539("follow-on parent level " + typed, () =>
            {
                var parent = typed ? Table(.90, two) : Columns(.90, first, second);
                var (run, result) = Run("KappaSimulateExactP", new() { ["iterations"] = 100, ["seed"] = 1, ["ci"] = .99 },
                    parent.output.ParameterBag.CopyWithoutOutputParameters());
                Check(Num539(result.ParameterBag, "pc") == 99, "the simulation itself keeps its requested 99% interval");
                Replay539(run, "stopifnot(confidence == .90)\n" +
                    Equal539("max(-1,kappa-z*se), min(1,kappa+z*se)",
                        new[] { Num539(parent.output.ParameterBag, "from"), Num539(parent.output.ParameterBag, "to") }),
                    Path.Combine(folder, "follow-" + typed));
            });
        if (Failed539.Count != failures)
            throw new Exception("FAIL round-two kappa: " + string.Join(" | ", Failed539.Skip(failures)));
    }
}

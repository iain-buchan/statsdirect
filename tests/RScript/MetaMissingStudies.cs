using System.Text;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckMetaMissingStudies(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        double[] exposed = { 100, 150, 110, 80, 200, 120 };
        double[] events = { 20, 35, 23, 10, 60, 50 };
        double[] control = { 100, 140, 125, 90, 210, 115 };
        double[] controlEvents = { 12, 20, 18, 12, 30, 15 };
        double[] estimates = { 1.5, .8, 1.7, 2, 1.15, 2.6 };
        double[] errors = { .12, .18, .22, .21, .15, .25 };
        double[] propEvents = { 0, 35, 23, 10, 60, 50 };
        (string, double[])[] tables = { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) };
        (string, double[])[] rates = { ("pt1", exposed), ("a", events), ("pt2", control), ("b", controlEvents) };

        foreach (string operation in new[] { "RelativeRiskMeta", "RiskDifference", "PetoMeta", "Mantel" })
            CheckPatterns(operation, tables, "xr");
        foreach (string operation in new[] { "MetaIncidenceRateRatio", "MetaIncidenceRateDifference" })
            CheckPatterns(operation, rates, "pt2");

        CheckPatterns("MetaSummary", new[] { ("y", estimates), ("se_y", errors) }, "se_y",
            new() { ["type"] = "oddsRatio", ["use_ci"] = "false" });
        // The current picker requires complete confidence-limit columns. An
        // estimate may still be missing; stored runs can also carry blank limits.
        CheckPatterns("MetaSummary", new[] { ("y", estimates), ("ll_y", new[] { 1.2, .55, 1.1, 1.3, .85, 1.6 }),
            ("ul_y", new[] { 1.9, 1.2, 2.2, 3.1, 1.6, 4.2 }) }, "y",
            new() { ["type"] = "oddsRatio", ["use_ci"] = "true" });

        double[] en = { 34, 50, 39, 24, 41, 28 };
        double[] cn = { 39, 44, 35, 29, 48, 36 };
        CheckPatterns("Effect", new[] { ("en", en), ("cn", cn), ("g", new[] { .42, -.1, .3, .22, .85, 1.1 }) }, "g",
            new() { ["type"] = "g" });
        foreach (string type in new[] { "d", "m" })
            CheckPatterns("Effect", new[] { ("en", en), ("cn", cn), ("em", new[] { 11d, 8, 9, 10, 14, 13 }),
                ("es", new[] { 2.1, 2.5, 2.8, 3.4, 2.2, 2.6 }), ("cm", new[] { 8d, 10, 9, 8, 11, 8 }),
                ("cs", new[] { 1.8, 2, 3.1, 3.2, 2.9, 2.4 }) }, "cs", new() { ["type"] = type });
        CheckPatterns("ProportionMeta", new[] { ("sn", exposed), ("sr", propEvents) }, "sr",
            new() { ["fudge"] = .5, ["method"] = "doubleArcsine" });

        void CheckPatterns(string operation, (string name, double[] values)[] columns, string missingInput,
            Dictionary<string, object> settings = null)
        {
            bool original = MetaExact;
            try
            {
                foreach (bool exact in new[] { false, true })
                {
                    MetaExact = exact;
                    Verify(operation, columns, missingInput, settings, wholeRow: true, labelled: false);
                    Verify(operation, columns, missingInput, settings, wholeRow: false, labelled: true);
                }
            }
            finally { MetaExact = original; }
        }

        void Verify(string operation, (string name, double[] values)[] columns, string missingInput,
            Dictionary<string, object> settings, bool wholeRow, bool labelled)
        {
            const int omitted = 2;
            int count = columns[0].values.Length;
            string[] names = { "Alpha trial", "Beta trial", "Incomplete trial", "Delta trial", "Epsilon trial", "Zeta trial" };
            object[][] raw = columns.Select(column => column.values.Cast<object>().ToArray()).ToArray();
            for (int i = 0; i < columns.Length; i++)
                if (wholeRow || columns[i].name == missingInput)
                    raw[i][omitted] = wholeRow ? null : "*";
            object[] namedColumn = new object[] { "Study names" }.Concat(names.Cast<object>()).ToArray();
            var cells = new RecordCells(raw.Append(namedColumn).ToArray());
            var answers = settings == null ? new Dictionary<string, object>() : new(settings);
            answers["gamma"] = .95;
            answers[operation == "MetaSummary" ? "studies" : "strata"] =
                labelled ? cells.Acquire(columns.Length, 0, namedColumn.Length, DataAcquisitionMode.Text) : null;
            for (int i = 0; i < columns.Length; i++)
                answers[columns[i].name] = cells.Acquire(i, 0, count, DataAcquisitionMode.NumericReplaceMissing);
            var (run, native) = Run(operation, answers);
            string label = operation + "-" + (settings?.Values.FirstOrDefault()?.ToString() ?? "default") +
                "-" + (settings?.GetValueOrDefault("use_ci") ?? "") + "-" + MetaExact + "-" + (wholeRow ? "row" : "cell");
            ParameterBag report = native.ParameterBag;
            var (block, labelVector, defaultLabel, q, limits, low, high) = operation switch
            {
                "RelativeRiskMeta" => ("*risks", "study", "Study", "q", "ci[,1],ci[,2]", "lci", "uci"),
                "RiskDifference" => ("*differences", "study", "Study", "q", "ci[,1],ci[,2]", "lci", "uci"),
                "PetoMeta" => ("*odds", "study", "Trial", "Q", "lower,upper", "lci", "uci"),
                "Mantel" => ("*or", "strata", "stratum", "q", "lci,uci", "lci", "uci"),
                "MetaSummary" => ("*studies", "study", "Study", "q", "lower,upper", "from", "to"),
                "Effect" => ("*approximate", "trial", "Trial", "Q", "lcid,ucid", "lci", "uci"),
                "ProportionMeta" => ("*proportions", "trial", "Trial", "q", "ci[,1],ci[,2]", "from_y", "to_y"),
                "MetaIncidenceRateRatio" => ("*ir", "strata", "Stratum", "res_r$q", "exact[1,],exact[2,]", "lci", "uci"),
                "MetaIncidenceRateDifference" => ("*ir", "strata", "Stratum", "res$q", "ird-z*se_ird,ird+z*se_ird", "lci", "uci"),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            var studies = (IList<ParameterBag>)report[block].AsObject;
            int n = count - 1;
            Check(studies.Count == n, label + " native analysis omits only the incomplete study");
            var expectedNames = labelled ? names.Where((_, i) => i != omitted) :
                Enumerable.Range(1, n).Select(i => defaultLabel + " " + i);
            if (labelled)
                Check(studies.Zip(expectedNames).All(pair =>
                    Convert.ToString(pair.First["lb"].AsObject, inv).StartsWith(pair.Second, StringComparison.Ordinal)),
                    label + " native report keeps the acquired label of each complete study");
            string Vector(IEnumerable<object> values) => RScriptWriter.Vector(values.ToArray());
            object Number(ParameterBag bag, string key)
            {
                double value = Convert.ToDouble(bag[key].AsObject, inv);
                return value == Constant.MISSING || double.IsNaN(value) ? null : value;
            }
            void Equal(StringBuilder code, string expression, IEnumerable<object> expected) =>
                code.Append("stopifnot(isTRUE(all.equal(unname(c(").Append(expression).Append(")), ")
                    .Append(Vector(expected)).Append(", tolerance=2e-7, check.attributes=FALSE)))\n");

            StringBuilder checks = new();
            checks.Append("stopifnot(k == ").Append(n).Append(", identical(").Append(labelVector).Append(", ")
                .Append(Vector(expectedNames.Cast<object>())).Append("))\n");
            ParameterBag pooled = operation == "Effect" ? ((IList<ParameterBag>)report["*poolok"].AsObject).Single() : report;
            Equal(checks, q, new[] { Number(pooled, "qc") });
            string i2 = operation is "MetaIncidenceRateDifference" or "MetaIncidenceRateRatio"
                ? $"max(0, 100 * (1-(k-1)/({q})))" : "i2,i2_ci";
            Equal(checks, i2, (i2 == "i2,i2_ci" ? new[] { "isq", "llisq", "ulisq" } : new[] { "isq" }).Select(key => Number(pooled, key)));
            Equal(checks, limits, studies.Select(row => Number(row, low)).Concat(studies.Select(row => Number(row, high))));
            if (operation == "ProportionMeta")
                checks.Append("stopifnot(adherent[1] == 0, p[1] == 0, ci[1,1] == 0)\n");
            checks.Append("cat('META_MISSING_STUDIES_OK\\n')\n");

            RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
            Check(script.HasRecipe && !script.DataUnavailable, label + " replays the acquired worksheet selection");
            string output = DeferredR538(script.Text + "\n" + checks, Path.Combine(folder, label));
            Check(output.Contains("META_MISSING_STUDIES_OK") && output.Contains("(1 other rows not used)"),
                label + " preserves study order, labels and native results in R");
        }
    }
}

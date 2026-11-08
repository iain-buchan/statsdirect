using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Exercise actual operation records rather than retyping the recipe's settings.
    // These data differ from the help examples, and every non-default level would
    // fail if a helper silently reverted to 95%.
    private static void CheckSelectedConfidenceReview(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        DataFrame Frame(params (string title, double[] values)[] columns)
        {
            DataFrame frame = new();
            foreach (var column in columns) frame.Variables.Add(Column(column.title, column.values));
            return frame;
        }
        IEnumerable<ParameterBag> Bags(ParameterBag bag)
        {
            yield return bag;
            foreach (var pair in bag)
                if (pair.Value?.AsObject is IList<ParameterBag> children)
                    foreach (var child in children) foreach (var item in Bags(child)) yield return item;
        }
        object Value(ParameterBag bag, string key) => Bags(bag).First(b => b.ContainsKey(key) && b[key]?.AsObject != null)[key].AsObject;
        double Number(ParameterBag bag, string key) => Convert.ToDouble(Value(bag, key), inv);
        string Native(ParameterBag bag, params string[] keys) => RScriptWriter.Vector(keys.Select(k => (object)Number(bag, k)).ToArray());
        string Equal(string expression, string expected, double tolerance = 1e-8) =>
            "stopifnot(isTRUE(all.equal(" + expression + "," + expected + ",tolerance=" + tolerance.ToString("R", inv) + ",check.attributes=FALSE)))\n";
        string Verify(string label, OperationRun run, string assertions)
        {
            RScript script = RScriptWriter.Write(Record(run), null, null);
            Check(script.HasRecipe && !script.DataUnavailable, label + " records its selected data and confidence");
            string output = RunR(script.Text + "\n" + assertions + "\ncat('SELECTED_CONFIDENCE_OK\\n')\n", Path.Combine(folder, label));
            Check(output.Contains("SELECTED_CONFIDENCE_OK"), label + " reproduces the native selected-confidence intervals");
            return output;
        }
        void PowerLabel(string output, ParameterBag bag, double confidence, string label)
        {
            Match expected = Regex.Match(Convert.ToString(Value(bag, "pwr"), inv), @"for ([0-9.]+)% significance\) = ([0-9.]+)%");
            Match actual = Regex.Match(output, @"Approximate power \(for ([0-9.]+)% significance\) = ([0-9.]+)%");
            Check(expected.Success && actual.Success &&
                Math.Abs(double.Parse(actual.Groups[1].Value, inv) - 100 * (1 - confidence)) < 1e-9 &&
                expected.Groups[1].Value == actual.Groups[1].Value &&
                Math.Abs(double.Parse(expected.Groups[2].Value, inv) - double.Parse(actual.Groups[2].Value, inv)) <= 0.011,
                label + " labels and rounds power at the selected significance level");
        }

        foreach (double confidence in new[] { 0.8, 0.9, 0.95, 0.99 })
        {
            string suffix = confidence.ToString("R", inv);
            var (prospective, prospectiveOutput) = Run("MiscRelRisk", new()
            {
                ["scrap"] = new double[] { 60, 25, 440, 475 }, ["pe"] = null, ["cco"] = confidence
            });
            string text = Verify("prospective-" + suffix, prospective,
                Equal("c(k,mn)", Native(prospectiveOutput.ParameterBag, "koopman_from", "koopman_to", "miettinen_from", "miettinen_to"), 2e-7));
            PowerLabel(text, prospectiveOutput.ParameterBag, confidence, "prospective " + suffix);

            var (retrospective, retrospectiveOutput) = Run("MiscRetroRisk", new()
            {
                ["scrap"] = new double[] { 120, 35, 80, 65 }, ["pe"] = null, ["cco"] = confidence
            });
            text = Verify("retrospective-" + suffix, retrospective, Equal("alpha", (1 - confidence).ToString("R", inv)));
            PowerLabel(text, retrospectiveOutput.ParameterBag, confidence, "retrospective " + suffix);

            var (diagnostic, diagnosticOutput) = Run("MiscDiagnostic", new()
            {
                ["cco"] = confidence, ["scrap"] = new double[] { 53, 17, 22, 108 }
            });
            Verify("diagnostic-" + suffix, diagnostic, Equal("c(lr_pos,lr_neg)", Native(diagnosticOutput.ParameterBag,
                "lr_pos", "lr_pos_from", "lr_pos_to", "lr_neg", "lr_neg_from", "lr_neg_to")));

            var (likelihood, likelihoodOutput) = Run("MiscLikely", new()
            {
                ["data"] = Frame(("Disease present", new double[] { 35, 47, 18, 20 }), ("Disease absent", new double[] { 8, 16, 22, 54 })),
                ["z1"] = confidence
            });
            ParameterBag[] rows = Bags(likelihoodOutput.ParameterBag).Where(b => b.ContainsKey("likely")).ToArray();
            Check(rows.Length == 4, "likelihood " + suffix + " has four native category intervals");
            string nativeRows = "rbind(" + string.Join(",", rows.Select(b => Native(b, "likely", "from", "to"))) + ")";
            Verify("likelihood-" + suffix, likelihood,
                "observed <- t(vapply(seq_along(mi),function(i)koopman(mi[i],sum(mi),no_mi[i],sum(no_mi),level=confidence),numeric(3)))\n" +
                Equal("observed", nativeRows));

            var (nnt, nntOutput) = Run("MiscNumberNeededToTreat", new()
            {
                ["cco"] = confidence, ["nc"] = 240d, ["xc"] = 72d, ["nt"] = 230d, ["xt"] = 39d, ["brr"] = 0.25
            });
            text = Verify("nnt-" + suffix, nnt,
                Equal("c(rre,rrne,rd)", Native(nntOutput.ParameterBag, "rre", "rre_from", "rre_to", "rrne", "rrne_from", "rrne_to", "rd", "rd_from", "rd_to"), 2e-7));
            Check(text.Contains("Estimates with " + (100 * confidence).ToString("G", inv) + "% confidence intervals:"),
                "NNT " + suffix + " labels its actual selected-confidence intervals");
        }

        bool originalMetaExact = MetaExact;
        try
        {
            // The non-central chi-square interval is the application's exact option.
            // Approximate I-squared intervals are a separate, deferred recipe feature.
            MetaExact = true;
            foreach (double confidence in new[] { 0.8, 0.9, 0.95, 0.99 })
            {
                string suffix = confidence.ToString("R", inv);
                foreach (bool lowHeterogeneity in new[] { false, true })
                {
                    string fixture = lowHeterogeneity ? "-low-Q-" : "-heterogeneous-";
                    foreach (string operation in new[] { "RelativeRiskMeta", "RiskDifference", "PetoMeta" })
                    {
                        var (run, output) = Run(operation, new()
                        {
                            ["gamma"] = confidence,
                            ["sn"] = Frame(("Treated total", lowHeterogeneity ? new double[] { 100, 100, 100, 100, 100 } : new double[] { 100, 150, 80, 200, 120 })),
                            ["sr"] = Frame(("Treated events", lowHeterogeneity ? new double[] { 20, 21, 18, 22, 19 } : new double[] { 20, 35, 10, 60, 50 })),
                            ["xn"] = Frame(("Control total", lowHeterogeneity ? new double[] { 100, 100, 100, 100, 100 } : new double[] { 100, 140, 90, 210, 115 })),
                            ["xr"] = Frame(("Control events", lowHeterogeneity ? new double[] { 16, 18, 16, 20, 18 } : new double[] { 12, 20, 12, 30, 15 })),
                            ["strata"] = null
                        });
                        string interval = operation == "PetoMeta" ? "i2_ci" : operation == "RelativeRiskMeta" ? "100*lim/(k-1+lim)" : "100*lim/(dfree+lim)";
                        if (lowHeterogeneity) Check(Number(output.ParameterBag, "llisq") == 0, operation + " " + suffix + " exercises a zero-clamped I-squared limit");
                        Verify(operation + fixture + suffix, run,
                            Equal(interval, Native(output.ParameterBag, "llisq", "ulisq"), 2e-7));
                    }
                    var (summary, summaryOutput) = Run("MetaSummary", new()
                    {
                        ["gamma"] = confidence, ["type"] = "oddsRatio", ["use_ci"] = "false", ["studies"] = null,
                        ["y"] = Frame(("Observed odds ratio", lowHeterogeneity ? new double[] { 1.1, 1.08, 1.12, 1.09, 1.11 } : new double[] { 1.5, 0.8, 2.0, 1.15, 2.6 })),
                        ["se_y"] = Frame(("Log odds ratio SE", new double[] { 0.12, 0.18, 0.21, 0.15, 0.25 }))
                    });
                    if (lowHeterogeneity) Check(Number(summaryOutput.ParameterBag, "llisq") == 0, "MetaSummary " + suffix + " exercises a zero-clamped I-squared limit");
                    Verify("MetaSummary" + fixture + suffix, summary,
                        Equal("100*lim/(dfree+lim)", Native(summaryOutput.ParameterBag, "llisq", "ulisq"), 2e-7));
                }
            }
        }
        finally { MetaExact = originalMetaExact; }
    }
}

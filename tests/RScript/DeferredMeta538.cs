using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckDeferredMeta538(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        DataFrame Frame(string title, double[] values)
        {
            DataFrame frame = new(); frame.Variables.Add(Column(title, values)); return frame;
        }
        double Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        string Vector(params double[] values) => "as.numeric(" + RScriptWriter.Vector(values.Select(v => v == Constant.MISSING || double.IsNaN(v) ? null : (object)v).ToArray()) + ")";
        string Equal(string expression, params double[] expected) => "stopifnot(isTRUE(all.equal(unname(c(" + expression + "))," + Vector(expected) + ",tolerance=2e-7,check.attributes=FALSE)))\n";
        string Verify(JsonElement record, string assertion, string label)
        {
            RScript script = RScriptWriter.Write(record, null, null);
            Check(script.HasRecipe && !script.DataUnavailable, label + " replays its recorded meta-analysis");
            string output = DeferredR538(script.Text + "\n" + assertion + "\ncat('META_METHOD_OK\\n')\n", Path.Combine(folder, label));
            Check(output.Contains("META_METHOD_OK"), label + " I-squared point and selected interval match the native method");
            return output;
        }
        Host host = new(new());
        bool original = MetaExact;
        try
        {
            foreach (bool exact in new[] { false, true })
            foreach (bool low in new[] { false, true })
            foreach (double confidence in new[] { .8, .9, .95, .99 })
            foreach (string operation in new[] { "RelativeRiskMeta", "RiskDifference", "PetoMeta", "MetaSummary" })
            {
                MetaExact = exact;
                Dictionary<string, object> answers;
                if (operation == "MetaSummary") answers = new()
                {
                    ["gamma"] = confidence, ["type"] = "oddsRatio", ["use_ci"] = "false", ["studies"] = null,
                    ["y"] = Frame("Observed effect", low ? new double[] { 1.1, 1.08, 1.12, 1.09, 1.11 } : new double[] { 1.5, .8, 2, 1.15, 2.6 }),
                    ["se_y"] = Frame("Log effect SE", new double[] { .12, .18, .21, .15, .25 })
                };
                else answers = new()
                {
                    ["gamma"] = confidence, ["strata"] = null,
                    ["sn"] = Frame("Treated total", low ? new double[] { 100, 100, 100, 100, 100 } : new double[] { 100, 150, 80, 200, 120 }),
                    ["sr"] = Frame("Treated events", low ? new double[] { 20, 21, 18, 22, 19 } : new double[] { 20, 35, 10, 60, 50 }),
                    ["xn"] = Frame("Control total", low ? new double[] { 100, 100, 100, 100, 100 } : new double[] { 100, 140, 90, 210, 115 }),
                    ["xr"] = Frame("Control events", low ? new double[] { 16, 18, 16, 20, 18 } : new double[] { 12, 20, 12, 30, 15 })
                };
                var (run, output) = Run(operation, answers);
                ParameterBag bag = output.ParameterBag;
                JsonElement record = Record(run);
                string label = operation + "-" + exact + "-" + low + "-" + confidence;
                Check(record.GetProperty("parameters").TryGetProperty("metaExact", out var selected) && selected.GetBoolean() == exact && bag["metaExact"].Direction == FilledParameterDirection.Input,
                    label + " records the actual method as a setting, without a new prompt");

                // Re-export after changing the host preference. The saved setting
                // must keep the method used for the result, including after reopening.
                MetaExact = !exact;
                JsonElement reopened = JsonDocument.Parse(RunRecord.Write(run, bag)).RootElement.Clone();
                Check(reopened.GetProperty("parameters").GetProperty("metaExact").GetBoolean() == exact,
                    label + " keeps its acquired method when preferences later change");
                string studyAssertion = "";
                if (operation is "RelativeRiskMeta" or "RiskDifference")
                {
                    var studyRows = (IList<ParameterBag>)bag[operation == "RelativeRiskMeta" ? "*risks" : "*differences"].AsObject;
                    studyAssertion = Equal("ci[,1],ci[,2]", studyRows.Select(b => Number(b, "lci")).Concat(studyRows.Select(b => Number(b, "uci"))).ToArray()) +
                        "cat('RAW_STUDY_CI_OK\\n')\n";
                }
                string replayOutput = Verify(reopened, "stopifnot(identical(exact_meta," + (exact ? "TRUE" : "FALSE") + "))\n" +
                    Equal("i2", Number(bag, "isq")) + Equal("i2_ci", Number(bag, "llisq"), Number(bag, "ulisq")) + studyAssertion, label);
                if (studyAssertion.Length > 0)
                    Check(replayOutput.Contains("RAW_STUDY_CI_OK") && replayOutput.Contains(Convert.ToString(bag["method"].AsObject, inv)),
                        label + " reproduces every native raw study limit and labels its score/Wald method");

                // Records predating this setting always used the exact R recipe.
                // Preserve that behavior rather than guessing today's preference.
                if (!exact)
                {
                    JsonObject legacy = JsonNode.Parse(record.GetRawText()).AsObject();
                    legacy["parameters"].AsObject().Remove("metaExact");
                    Meta.IsquareNcc(host, Number(bag, "qc"), 5, confidence, PDF.gauinv((1 + confidence) / 2), out double point, out double lower, out double upper, exact: true);
                    Verify(JsonDocument.Parse(legacy.ToJsonString()).RootElement.Clone(),
                        "stopifnot(isTRUE(exact_meta))\n" + Equal("i2", point) + Equal("i2_ci", lower, upper), label + "-legacy");
                }
            }

            // Exercise the same recipe interval blocks against the public native
            // routine at boundaries. These are interval-unit checks: a one-study
            // full meta recipe also contains unrelated, undefined bias analyses.
            string[] files = { "relative-risk-meta.R", "meta-summary.R", "risk-difference.R", "peto-meta.R" };
            foreach (string file in files)
            foreach (bool exact in new[] { false, true })
            foreach (double confidence in new[] { .8, .99 })
            foreach (var test in new (int k, double q)[] { (1, 0d), (2, 0d), (2, 20d), (3, 0d), (5, 0d), (5, 4d), (5, 5d), (5, 10d), (5, 1000d) })
            {
                Meta.IsquareNcc(host, test.q, test.k, confidence, PDF.gauinv((1 + confidence) / 2), out double point, out double lower, out double upper, exact: exact);
                string source = RRecipes.Recipe(file);
                int start = source.IndexOf("# I-squared interval: recorded exact/test-based choice.", StringComparison.Ordinal);
                int end = source.IndexOf("# End of I-squared interval.", start, StringComparison.Ordinal);
                Check(start >= 0 && end > start, file + " exposes its independently testable interval block");
                string setup = "q<-Q<-" + test.q.ToString("R", inv) + ";k<-" + test.k + ";confidence<-" + confidence.ToString("R", inv) + ";exact_meta<-" + (exact ? "TRUE" : "FALSE") +
                    "\nz<-qnorm((1+confidence)/2);pct<-paste0(100*confidence,'%');one<-function(x)formatC(x,digits=1,format='f',drop0trailing=TRUE)\n";
                string output = DeferredR538(setup + source[start..end] + Equal("i2", point) + Equal("i2_ci", lower, upper) + "\ncat('META_EDGE_OK\\n')\n",
                    Path.Combine(folder, file + "-edge-" + exact + "-" + confidence + "-" + test.k + "-" + test.q));
                Check(output.Contains("META_EDGE_OK") && !output.Contains("NaN"), file + " exact=" + exact + " confidence=" + confidence + " studies=" + test.k + " Q=" + test.q + " matches native boundaries without NaN output");
            }
        }
        finally { MetaExact = original; }
    }
}

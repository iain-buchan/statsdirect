using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckPairedDifferenceReplay(string folder)
    {
        double m = Constant.MISSING;
        DataFrame Frame(params (string title, double[] values)[] columns)
        {
            DataFrame frame = new();
            foreach (var (title, values) in columns) frame.Variables.Add(Column(title, values));
            return frame;
        }
        double Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        string Vec(IEnumerable<double> values) => RScriptWriter.Vector(values.Cast<object>().ToArray());
        string Compare(string expression, IEnumerable<double> expected) =>
            "stopifnot(isTRUE(all.equal(unname(c(" + expression + "))," + Vec(expected) + ",tolerance=1e-10,check.attributes=FALSE)))\n";
        double[] differences = { 12, 41, 108, 76, 76, -2, 63, 72, 59 };
        foreach (bool missing in new[] { false, true })
        foreach (double confidence in new[] { .8, .95, .99 })
        {
            double[] selected = missing ? new[] { m, 12, 41, 108, 76, m, 76, -2, 63, 72, 59, m } : differences;
            var (run, output) = Run("TPaired", new() { ["data"] = Frame(("Selected differences", selected)), ["gamma"] = confidence });
            JsonElement record = Record(run);
            RScript script = RScriptWriter.Write(record, null, null);
            Check(script.HasRecipe && !script.DataUnavailable, "one-column paired test exports recorded differences without inventing a second sample");
            ParameterBag bag = output.ParameterBag;
            double[] expected = new[] { "n", "mean", "sd", "sem", "df", "t", "tail_2", "from", "to" }.Select(key => Number(bag, key)).ToArray();
            double power = double.Parse(Regex.Match(bag["pwr"].AsObject.ToString(), @"([0-9.]+)%$").Groups[1].Value, inv);
            string assertions = "\nstopifnot(identical(d," + Vec(differences) + "),!paired,is.null(after),!agreement,!exists('loa'))\n" +
                Compare("n,mean(d),sd(d),sd(d)/sqrt(n),r$parameter,r$statistic,r$p.value,r$conf.int[1:2]", expected) +
                "stopifnot(abs(100*pw$power-" + power.ToString("R", inv) + ")<=0.00500001)\ncat('PAIRED_DIFFERENCE_OK\\n')\n";
            string probe = "plot <- function(...) stop('Agreement must not be plotted for a column of differences')\n";
            string result = RunR(probe + script.Text + assertions, Path.Combine(folder, "paired-one-" + missing + "-" + confidence.ToString("R", inv)));
            Check(result.Contains("PAIRED_DIFFERENCE_OK") && !result.Contains("Limits of agreement") && result.Contains("Power (for " + (100 * (1 - confidence)).ToString("G6", inv) + "% significance)"), "one-column paired t test reproduces native results and selected-level power, with no agreement output");
        }

        // Zeros and tied absolute differences exercise StatsDirect's advanced
        // conditional probabilities and Walsh limits, beyond a bare wilcox.test.
        double[] tied = { 2, 0, -1, 3, 1, 2, -1, 4, 1, -2, 3, 0 };
        foreach (bool oneColumn in new[] { false, true })
        foreach (bool missing in new[] { false, true })
        foreach (double confidence in new[] { .8, .95, .99 })
            SignedRanks(tied, oneColumn, missing, confidence, "tied");
        SignedRanks(new[] { 12.0, 41, 108, 76, -2 }, true, true, .95, "short-no-interval");
        SignedRanks(new[] { 2.0, -1, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true, true, .95, "few-nonzero-no-interval");
        foreach (bool oneColumn in new[] { false, true })
        foreach (bool missing in new[] { false, true })
        {
            double[] values = missing ? new[] { m, 0, 0, 0, m, 0, 0, 0, m } : new double[6];
            double[] pairedValues = missing ? new[] { 10.0, 20, 30, 40, m, 50, 60, 70, m } : new[] { 20.0, 30, 40, 50, 60, 70 };
            DataFrame frame = oneColumn ? Frame(("Zero differences", values)) : Frame(("First", pairedValues), ("Second", missing ? new[] { m, 20.0, 30, 40, 999, 50, 60, 70, m } : pairedValues));
            bool refused = false;
            try { Run("Wilcoxon", new() { ["data"] = frame, ["gamma"] = .95 }); }
            catch (Exception ex) when (ex.Message == "the operation refused: Calculation Error") { refused = true; }
            Check(refused, "native signed ranks refuses all-zero differences in " + (oneColumn ? "one-column" : "two-column") + " data");
            // A refused operation has no completed report record. Make an
            // entered-data record for those same inputs to test recipe refusal.
            var columns = frame.Variables.Cast<DoubleVariable>().Select(v => new { title = v.Title, values = Enumerable.Range(0, v.Length).Select(i => v.Data[i] == m ? null : (object)v.Data[i]).ToArray() }).ToArray();
            string fixture = JsonSerializer.Serialize(new { operation = "Wilcoxon", data = columns, settings = new { gamma = .95 } });
            using JsonDocument example = JsonDocument.Parse(fixture);
            using JsonDocument record = JsonDocument.Parse(RecordOf(example.RootElement));
            RScript script = RScriptWriter.Write(record.RootElement, null, null);
            string code = "error_message <- tryCatch({\n" + script.Text + "\nNULL\n},error=function(e) conditionMessage(e))\n" +
                "stopifnot(identical(error_message,'All differences are zero; at least one non-zero difference is required.'),n==6L,all(d==0),length(nz)==0L,!exists('Tplus'),!exists('lower'),!exists('K'))\ncat('ALL_ZERO_REFUSED\\n')\n";
            Check(script.HasRecipe && !script.DataUnavailable && RunR(code, Path.Combine(folder, "all-zero-" + oneColumn + "-" + missing)).Contains("ALL_ZERO_REFUSED"), "R signed-ranks recipe refuses all-zero differences clearly without reporting invented probabilities or intervals");
        }

        void SignedRanks(double[] expectedDifferences, bool oneColumn, bool missing, double confidence, string name)
        {
            var first = new List<double>(); var second = new List<double>();
            if (missing) { first.Add(m); second.Add(100); }
            for (int i = 0; i < expectedDifferences.Length; i++)
            {
                first.Add(oneColumn ? expectedDifferences[i] : 100 + i + expectedDifferences[i]);
                second.Add(100 + i);
                if (missing && i == 3) { first.Add(oneColumn ? m : 99); second.Add(m); }
            }
            if (missing) { first.Add(m); second.Add(m); }
            DataFrame frame = oneColumn ? Frame(("Selected differences", first.ToArray())) : Frame(("Selected first", first.ToArray()), ("Selected second", second.ToArray()));
            var (run, output) = Run("Wilcoxon", new() { ["data"] = frame, ["gamma"] = confidence });
            RScript script = RScriptWriter.Write(Record(run), null, null);
            Check(script.HasRecipe && !script.DataUnavailable, "signed ranks exports complete paired data or a column of differences");
            ParameterBag bag = output.ParameterBag;
            double rankSum = double.Parse(Regex.Match(bag["sum"].AsObject.ToString(), @"(-?[0-9.]+)$").Value, inv);
            string assertions = "\nstopifnot(identical(d," + Vec(expectedDifferences) + "),n==" + expectedDifferences.Length + "L)\n" +
                Compare("length(nz),Tplus,lower,upper,min(1,2*min(lower,upper))", new[] { Number(bag, "non_0"), rankSum, Number(bag, "p_l"), Number(bag, "p_u"), Number(bag, "p_2") });
            bool hasInterval = bag["*conf"].AsObject is IList<ParameterBag> intervals && intervals.Count > 0;
            if (hasInterval)
            {
                ParameterBag interval = ((IList<ParameterBag>)bag["*conf"].AsObject)[0];
                int k = int.Parse(Regex.Match(interval["k"].AsObject.ToString(), @"\d+").Value, inv);
                assertions += "stopifnot(K==" + k + "L)\n" + Compare("median(a),a[K],rev(a)[K]", new[] { Number(interval, "med_diff"), Number(interval, "from"), Number(interval, "to") });
            }
            else assertions += "stopifnot(K==0 || length(nz)<4)\n";
            assertions += "cat('SIGNED_DIFFERENCE_OK\\n')\n";
            string result = RunR(script.Text + assertions, Path.Combine(folder, "signed-" + name + "-" + oneColumn + "-" + missing + "-" + confidence.ToString("R", inv)));
            Check(result.Contains("SIGNED_DIFFERENCE_OK") && (hasInterval || result.Contains("interval unavailable")), "signed ranks reproduces native nonzero count, tied-rank sum, exact conditional tails and advanced Walsh limits for " + (oneColumn ? "differences" : "paired samples"));
        }
    }
}

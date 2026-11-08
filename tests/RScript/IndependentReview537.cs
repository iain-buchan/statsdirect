using System.Text;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // Native operations and the real worksheet acquisition are the oracle here.
    // In particular, a typed missing marker must survive recording before the
    // recipe applies the native analysis's own missing-value rules.
    private static void CheckIndependentReview537(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        double Num(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        string Vec(IEnumerable<double> values) => RScriptWriter.Vector(values.Cast<object>().ToArray());
        string Equal(string actual, IEnumerable<double> expected, double tolerance = 1e-9) =>
            "stopifnot(isTRUE(all.equal(" + actual + "," + Vec(expected) + ",tolerance=" + tolerance.ToString("R", inv) + ",check.attributes=FALSE)))\n";
        var scripts = new StringBuilder();
        int cases = 0;
        void Add(string operation, string label, RecordCells grid, DataFrame data, Dictionary<string, object> settings, Func<ParameterBag, string> assertions)
        {
            var answers = new Dictionary<string, object>(settings) { ["data"] = data };
            var (run, output) = Run(operation, answers);
            var script = RScriptWriter.Write(Record(run), null, grid.Read);
            Check(script.HasRecipe && !script.DataUnavailable && !script.Text.Contains("has changed in the workbook"), operation + " " + label + ": acquired worksheet replays without a false change warning");
            scripts.Append("local({\n# ").Append(operation).Append(' ').Append(label).Append("\nrecipe_output<-capture.output({\n").Append(script.Text).Append("\n})\ncat(recipe_output,sep='\\n')\n").Append(assertions(output.ParameterBag)).Append("\n})\n");
            cases++;
        }
        void Finish(string group)
        {
            scripts.Append("cat('INDEPENDENT_REVIEW_537_OK\\n')\n");
            Check(RunR(scripts.ToString(), Path.Combine(folder, group)).Contains("INDEPENDENT_REVIEW_537_OK"), group + ": " + cases + " real-operation replay cases match native results in R");
            scripts.Clear(); cases = 0;
        }
        object[] Cells(string title, IEnumerable<double> values, object marker, bool trailing)
        {
            var cells = new List<object> { title };
            double[] array = values.ToArray();
            cells.AddRange(array.Take(trailing ? array.Length : 3).Cast<object>());
            cells.Add(marker);
            if (!trailing) cells.AddRange(array.Skip(3).Cast<object>());
            return cells.ToArray();
        }
        DataFrame Acquire(RecordCells grid, int count) => grid.Acquire(0, 0, count, DataAcquisitionMode.NumericReplaceMissing);
        double[] counts = { 3, 5, 2, 4, 6, 1, 3 };
        foreach (object marker in new object[] { "*", ".", "MISSING", null })
        foreach (bool trailing in new[] { false, true })
        {
            string label = (marker?.ToString() ?? "blank") + (trailing ? "-tail" : "-interior");
            object[] cells = Cells("Selected arrivals", counts, marker, trailing);
            foreach (double confidence in new[] { 0.9, 0.95, 0.99 })
            {
                var grid = new RecordCells(cells);
                var data = Acquire(grid, cells.Length);
                if (marker != null || !trailing)
                    Check(((DoubleVariable)data.Variables[0]).Data.Count(RunRecord.Missing) == 1, "Poisson " + label + ": production acquisition retains the selected missing observation");
                Add("PoissonConfidenceInterval", label + "-" + confidence, grid, data, new() { ["gamma"] = confidence }, bag =>
                {
                    var row = ((IList<ParameterBag>)bag["*sample"].AsObject)[0];
                    return Equal("c(n,mean(counts),r$conf.int,ci)", new[] { Num(row, "n"), Num(row, "mean"), Num(row, "lower2"), Num(row, "upper2"), Num(row, "lower1"), Num(row, "upper1") }) + "stopifnot(identical(counts," + Vec(counts) + "))\n";
                });
            }
            // Zero, negative and noninteger classes are excluded by the engine,
            // as well as the selected missing observation (not counted as classes).
            object[] diversityCells = Cells("Observed classes", counts.Concat(new[] { 0d, -2d, 1.5 }), marker, trailing);
            var diversityGrid = new RecordCells(diversityCells);
            Add("Diversity", label, diversityGrid, Acquire(diversityGrid, diversityCells.Length), new() { ["gamma"] = 0.95, ["boots"] = 120, ["seed"] = 2001 }, bag =>
            {
                var row = ((IList<ParameterBag>)bag["*var"].AsObject)[0];
                return Equal("c(N,S,Ds,l,1/l,seL,seS,Ds-z*seL,Ds+z*seL,H,hL,hS,H-z*hL,H+z*hL)", new[] {
                    Num(row,"n"),Num(row,"s"),Num(row,"simpson"),Num(row,"dom"),Num(row,"ds"),Num(row,"se-simpson-largeSample"),Num(row,"ses-simpson"),Num(row,"from-simpson-largeSample"),Num(row,"to-simpson-largeSample"),Num(row,"shannon"),Num(row,"se-shannon-largeSample"),Num(row,"ses-shannon"),Num(row,"from-shannon-largeSample"),Num(row,"to-shannon-largeSample") }) +
                    "stopifnot(identical(counts," + Vec(counts) + "),all(is.finite(boot)))\n";
            });
        }
        // Above the record's held-data threshold the pointer-only reread must
        // also retain a trailing typed marker; omit it only inside the recipe.
        var longCounts = Enumerable.Range(1, 5001).Select(i => (double)(i % 7)).ToArray();
        var longCells = Cells("Long count sample", longCounts, "*", true);
        var longGrid = new RecordCells(longCells);
        Add("PoissonConfidenceInterval", "pointer-only-typed-tail", longGrid, Acquire(longGrid, longCells.Length), new() { ["gamma"] = 0.95 }, bag =>
        {
            var row = ((IList<ParameterBag>)bag["*sample"].AsObject)[0];
            return Equal("c(n,mean(counts),r$conf.int,ci)", new[] { Num(row,"n"),Num(row,"mean"),Num(row,"lower2"),Num(row,"upper2"),Num(row,"lower1"),Num(row,"upper1") }) + "stopifnot(n==5001L,!anyNA(counts))\n";
        });
        Finish("count-missing-markers");

        foreach (int n in new[] { 12, 200, 201 })
        foreach (var level in new[] { (0.8, 0.95), (0.9, 0.99), (0.95, 0.8), (0.95, 0.95) })
        foreach (bool conservative in new[] { false, true })
            Reference(Enumerable.Range(1, n).Select(i => 3.0 + i / 10.0 + (i % 7) / 50.0).ToArray(), level.Item1, level.Item2, conservative, n + "-" + level + "-" + conservative);
        Reference(new[] { -5d, -2, 0, 2, 3, 6, 7, 10, 12, 13, 15, 21 }, 0.8, 0.95, false, "nonpositive");
        Reference(new[] { 0d, 1, 2, 3, 4, 6, 7, 10, 12, 13, 15, 21 }, 0.9, 0.99, true, "zero");
        Finish("reference-coverage-confidence-quantiles");
        void Reference(double[] values, double coverage, double confidence, bool conservative, string label)
        {
            var cells = Cells("Selected reference sample", values, "*", true);
            var grid = new RecordCells(cells);
            Add("ReferenceRange", label, grid, Acquire(grid, cells.Length), new() { ["gamma"] = confidence, ["reference-interval"] = coverage, ["do_conservative"] = conservative }, bag =>
            {
                string result = Equal("c(n,mean(igm),sd(igm))", new[] { Num(bag,"size"),Num(bag,"mean"),Num(bag,"sd") }) +
                    "captured<-numeric()\ninvisible(capture.output(normal_range(igm,function(v){captured<<-c(captured,v);v})))\n" +
                    Equal("captured", new[] { Num(bag,"lrr"),Num(bag,"urr"),Num(bag,"lx_l"),Num(bag,"ux_l"),Num(bag,"lx_u"),Num(bag,"ux_u") }) +
                    "stopifnot(rpct==" + RScriptWriter.Quote((100 * coverage).ToString("G", inv) + "%") + ",pct==" + RScriptWriter.Quote((100 * confidence).ToString("G", inv) + "%") + ")\n";
                var logs = bag["*lognormal"].AsObject as IList<ParameterBag>;
                if (logs != null)
                {
                    var row = logs[0];
                    result += "stopifnot(all(igm>0),any(grepl('For log-normal data',recipe_output)))\ncaptured<-numeric()\ninvisible(capture.output(normal_range(log(igm),function(v){captured<<-c(captured,exp(v));exp(v)})))\n" +
                        Equal("captured", new[] { Num(row,"lrr_lognormal"),Num(row,"urr_lognormal"),Num(row,"lx_l_lognormal"),Num(row,"ux_l_lognormal"),Num(row,"lx_u_lognormal"),Num(row,"ux_u_lognormal") });
                }
                else result += "stopifnot(!all(igm>0),!any(grepl('For log-normal data',recipe_output)))\n";
                string[] suffixes = { "_any", "" };
                for (int i = 0; i < suffixes.Length; i++)
                {
                    string s = suffixes[i], q = "reference_quantiles[[" + (i + 1) + "]]";
                    result += Equal("unlist(" + q + "[c('value','lower','upper','coverage')])", new[] { Num(bag,"qxv" + s),Num(bag,"from" + s),Num(bag,"to" + s),Num(bag,"co" + s) }) +
                        "stopifnot(identical(" + q + "$capLower," + (bag.ContainsKey("cap_from" + s) ? "TRUE" : "FALSE") + "),identical(" + q + "$capUpper," + (bag.ContainsKey("cap_to" + s) ? "TRUE" : "FALSE") + "))\n";
                }
                return result;
            });
        }

        var varianceCases = new List<(double[], double[])> {
            (new[] {2.2,2.3,2.4},new[] {3.7,3.7,3.8,3.9,3.9}),
            (new[] {12.1,14.3,13.7,15.2,12.9},new[] {22.1,24.3,23.7,25.2,22.9}) };
        foreach (double d in new[] { .1, .3, .7, 1.1, .01, .03, 2.2, .17 })
        foreach (double a in new[] { .2, 1.3, 7.1 })
        foreach (double b in new[] { 1d, .45, 3.3 })
            varianceCases.Add((new[] { a-d,a,a+d }, new[] { b-d,b-d,b,b+d,b+d }));
        scripts.Append("ordering_disagreements<-0L\n");
        int number = 0;
        foreach (var pair in varianceCases)
        {
            object[] firstCells = Cells("First actual group", pair.Item1, "*", true), secondCells = Cells("Second actual group", pair.Item2, ".", false);
            var grid = new RecordCells(firstCells, secondCells);
            var data = Acquire(grid, firstCells.Length);
            foreach (var variable in grid.Acquire(1, 0, secondCells.Length, DataAcquisitionMode.NumericReplaceMissing).Variables) data.Variables.Add(variable);
            Add("VarianceRatio", "rounding-" + number++, grid, data, new(), bag =>
            {
                string high = bag["title_0"].AsObject.ToString(), low = bag["title_1"].AsObject.ToString();
                return Equal("c(unname(f$parameter),var(high),var(low),unname(f$statistic),p,2*min(p,1-p))", new[] { Num(bag,"df_0"),Num(bag,"df_1"),Num(bag,"var_0"),Num(bag,"var_1"),Num(bag,"f"),Num(bag,"p_1"),Num(bag,"p_2") }) +
                    "stopifnot(identical(high_title," + RScriptWriter.Quote(high) + "),identical(low_title," + RScriptWriter.Quote(low) + "))\n" +
                    "r_first<-var(slight)>var(marked)\nnative_first<-identical(high_title,slight_title)\nif(r_first!=native_first) ordering_disagreements<<-ordering_disagreements+1L\n";
            });
        }
        scripts.Append("stopifnot(ordering_disagreements>0L)\n");
        Finish("variance-ratio-rounding-ties");
    }
}

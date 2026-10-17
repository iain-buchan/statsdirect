using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckAnalysisReplay()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-analysis-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        bool passed = false;
        int id = 0;
        string Vector(IEnumerable<double> values) => RScriptWriter.Vector(values.Select(x => x == Constant.MISSING || double.IsNaN(x) ? null : (object)x).ToArray());
        double Number(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        void Verify(OperationRun run, string assertion, string description)
        {
            JsonElement record = Record(run);
            RScript script = RScriptWriter.Write(record, null, null);
            Check(script.HasRecipe && !script.DataUnavailable, description + " has all recorded data");
            Check(RScriptWriter.Write(JsonDocument.Parse(record.GetRawText()).RootElement, null, null).Text == script.Text,
                description + " survives saving and reopening the run");
            string output = RunBoxR(script.Text + "\n" + assertion + "\ncat('ANALYSIS_REPLAY_OK\\n')\n", Path.Combine(folder, (++id).ToString()));
            Check(output.Contains("ANALYSIS_REPLAY_OK"), description + " agrees with the native calculation in R");
        }
        try
        {
            foreach (double confidence in new[] { 0.8, 0.95, 0.99 })
            foreach (string sample in new[] { "ordinary", "ties-and-missing", "five-pairs", "large", "edgeworth" })
            {
                if (sample == "edgeworth" && confidence != 0.95) continue;
                double[] x = sample is "large" or "edgeworth" ? Enumerable.Range(1, sample == "large" ? 140 : 1001).Select(i => (double)i).ToArray() :
                    sample == "five-pairs" ? new[] { 1.0, 2, 3, 4, 5 } :
                    sample == "ties-and-missing" ? new[] { 1.0, 2, 2, 4, Constant.MISSING, 6, 7, 8, 9 } : Enumerable.Range(1, 8).Select(i => (double)i).ToArray();
                double[] y = sample is "large" or "edgeworth" ? x.Select((v, i) => 3 + 0.4 * v + Math.Sin(i * 0.71)).ToArray() :
                    sample == "five-pairs" ? new[] { 2.0, 4, 3, 7, 6 } :
                    sample == "ties-and-missing" ? new[] { 2.0, 4, 3, 7, 6, 9, 8, 12, Constant.MISSING } : new[] { 2.0, 4, 3, 7, 6, 9, 8, 12 };
                var (run, output) = Run("NonparametricLinearRegression", new()
                {
                    ["gamma"] = confidence,
                    ["outcome"] = SheetFrame(Workbook, "Selected regression", 1, 0, ("Chosen outcome", y)),
                    ["predictor"] = SheetFrame(Workbook, "Selected regression", 2, 0, ("Chosen predictor", x))
                });
                ParameterBag result = ((IList<ParameterBag>)output.ParameterBag["*results"].AsObject)[0];
                double[] expected = { Number(output.ParameterBag, "obs"), Number(result, "mdn"), Number(result, "intercept"), Number(result, "from"), Number(result, "to") };
                string assertion = "stopifnot(isTRUE(all.equal(c(np_result$n,np_result$slope,np_result$intercept,np_result$lower,np_result$upper)," + Vector(expected) + ",tolerance=1e-10,check.attributes=FALSE)))\n";
                Verify(run, assertion, "Theil regression " + sample + " at " + confidence);
            }

            foreach (int n in new[] { 1, 20, 201, 400 })
            foreach (bool conservative in new[] { false, true })
            foreach (double p in new[] { 0.03, 0.5, 0.9 })
            {
                double[] first = Enumerable.Range(1, n).Select(i => (double)i).Concat(new[] { Constant.MISSING }).ToArray();
                double[] second = first.Select(v => v == Constant.MISSING ? v : 100 + v * 2).ToArray();
                double confidence = n == 20 ? 0.8 : 0.99;
                var (run, output) = Run("Quantile", new()
                {
                    ["data"] = SheetFrame(Workbook, "Selected quantiles", 0, 0, ("First selected", first), ("Second selected", second)),
                    ["gamma"] = confidence, ["quantile"] = p, ["conservative-ci"] = conservative
                });
                IList<ParameterBag> results = (IList<ParameterBag>)output.ParameterBag["*variable"].AsObject;
                string assertion = "stopifnot(length(quantile_results)==2L,identical(names(quantile_results),c('First selected','Second selected')))\n";
                for (int i = 0; i < results.Count; i++)
                {
                    ParameterBag result = results[i];
                    double[] expected = { Number(result, "size"), Number(result, "value"), Number(result, "from"), Number(result, "to"), Number(result, "exact") };
                    assertion += "q <- quantile_results[[" + (i + 1) + "]]\nstopifnot(q$missing==1L,isTRUE(all.equal(c(q$n,q$value,q$lower,q$upper,q$coverage)," + Vector(expected) + ",tolerance=1e-9,check.attributes=FALSE)),q$capLower==" + (result["note_from"].AsString.Length > 0 ? "TRUE" : "FALSE") + ",q$capUpper==" + (result["note_to"].AsString.Length > 0 ? "TRUE" : "FALSE") + ")\n";
                }
                Verify(run, assertion, "Both quantile columns n=" + n + ", p=" + p + ", conservative=" + conservative);
            }

            foreach (int centileType in new[] { 1, 2 })
            foreach (double confidence in new[] { 0.8, 0.99 })
            {
                string[] reportFlags = { "valid-data", "missing-data", "sum", "mean", "variance", "sd", "variance-coeff", "sem", "u95cl", "l95cl", "geometric-mean", "skewness", "kurtosis", "maximum", "uq", "median", "lq", "iqr", "minimum", "range", "udc" };
                DataFrame frame = SheetFrame(Workbook, "Selected summaries", 0, 0,
                    ("First summary", new[] { 3.0, 1, 8, Constant.MISSING, 5, 9, 14, 2 }),
                    ("One observation", new[] { Constant.MISSING, 7.0, Constant.MISSING }),
                    ("No observations", new[] { Constant.MISSING, Constant.MISSING }),
                    ("Zero mean", new[] { -2.0, -1, 0, 1, 2 }));
                var (run, output) = Run("UnivariateSummary", new()
                {
                    ["data"] = frame, ["gamma"] = confidence, ["report-centile-type"] = centileType.ToString(),
                    ["options"] = reportFlags.ToDictionary(flag => "report-" + flag, _ => true),
                    ["report-udca"] = 0.29, ["report-udcb"] = 0.9, ["output-to-frame"] = true, ["statsdirect-frame-pane"] = null
                });
                DataFrame native = output.ParameterBag["output"].AsDataFrame;
                string assertion = "stopifnot(length(summary_results)==4L,all(summary_shown),identical(names(summary_results),c('First summary','One observation','No observations','Zero mean')))\n";
                for (int c = 0; c < frame.VariableCount; c++)
                {
                    double[] expected = native.Variables.Skip(1).Cast<DoubleVariable>().Select(v => v.Data[c]).ToArray();
                    assertion += "stopifnot(isTRUE(all.equal(unname(summary_results[[" + (c + 1) + "]])," + Vector(expected) + ",tolerance=1e-9,check.attributes=FALSE)))\n";
                }
                Verify(run, assertion, "Every descriptive column with missing and small samples, centile type " + centileType + " and confidence " + confidence);

                // The run stores all report choices individually. Replay a saved
                // report requesting only mean and its two chosen centiles.
                JsonNode changed = JsonNode.Parse(run.Record);
                JsonObject parameters = changed["parameters"].AsObject();
                foreach (string flag in reportFlags) parameters["report-" + flag] = flag is "mean" or "udc";
                string edited = RScriptWriter.Write(JsonDocument.Parse(changed.ToJsonString()).RootElement, null, null).Text;
                string text = RunBoxR(edited + "\nstopifnot(identical(which(summary_shown),c(4L,21L,22L)))\ncat('SELECTED_SUMMARY_OK\\n')\n", Path.Combine(folder, "selected-" + (++id)));
                Check(text.Contains("SELECTED_SUMMARY_OK") && !text.Contains("Standard deviation") && text.Contains("Centile 90") && text.Contains("Centile 29"), "a reopened summary respects the selected statistics and user centiles");
            }
            foreach (string operation in new[] { "NonparametricLinearRegression", "Quantile", "UnivariateSummary", "UnivariateSummaryToWorksheetOnly" })
            {
                JsonElement example = JsonDocument.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".json"))).RootElement;
                string script = RScriptWriter.Write(JsonDocument.Parse(RecordOf(example)).RootElement, null, null).Text;
                string actual = RunBoxR(script, Path.Combine(folder, operation));
                string reference = RunBoxR(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".R")), Path.Combine(folder, operation + "-reference"));
                Check(Figures(actual) == Figures(reference), operation + " retains every numerical value of its independent worked-example reference");
            }
            passed = true;
        }
        finally
        {
            if (passed && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(folder, true);
            else if (!passed) Console.WriteLine("      analysis replay artifacts: " + folder);
        }
    }
}

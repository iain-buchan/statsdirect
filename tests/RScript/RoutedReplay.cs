using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void CheckRoutedReplay()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-routed-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        bool passed = false; int id = 0;
        double Num(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
        string Vec(IEnumerable<double> values) => RScriptWriter.Vector(values.Select(x => x == Constant.MISSING || double.IsNaN(x) ? null : (object)x).ToArray());
        void Verify(OperationRun run, string assertion, string description)
        {
            JsonElement record = Record(run);
            RScript script = RScriptWriter.Write(record, null, null);
            Check(script.HasRecipe && !script.DataUnavailable && RScriptWriter.Write(JsonDocument.Parse(record.GetRawText()).RootElement, null, null).Text == script.Text,
                description + " retains its data and recipe after reopening");
            string output = RunBoxR(script.Text + "\n" + assertion + "\ncat('ROUTED_REPLAY_OK\\n')\n", Path.Combine(folder, (++id).ToString()));
            Check(output.Contains("ROUTED_REPLAY_OK"), description + " agrees with the native calculation in R");
        }
        string Anova(ParameterBag output) => "stopifnot(isTRUE(all.equal(unname(regression_anova)," + Vec(new[] { "reg_sum", "res_sum", "tot_sum", "reg_df", "res_df", "tot_df", "f", "p", "r", "mse" }.Select(key => Num(output, key))) + ",tolerance=1e-9,check.attributes=FALSE)))\n";
        try
        {
            double[] ordinaryX = { -3, -2, -1, 0, 1, 2, 3, 4 }, ordinaryY = { 2, 4, 3, Constant.MISSING, 8, 10, 15, 17 };
            Dictionary<string, object> Data(double[] x, double[] y) => new()
            {
                ["x"] = SheetFrame(Workbook, "Routed regression", 1, 0, ("Selected X", x)),
                ["y"] = SheetFrame(Workbook, "Routed regression", 2, 0, ("Selected Y", y))
            };
            var simpleAnswers = Data(ordinaryX, ordinaryY); simpleAnswers["gamma"] = 0.95;
            var (simple, simpleOut) = Run("SimpleLinearRegression", simpleAnswers);
            var (simpleAnova, simpleAnovaOut) = Run("Ranv", new() { ["gamma"] = 0.95 }, simpleOut.ParameterBag);
            Check(Record(simpleAnova).GetProperty("rootOperation").GetString() == "SimpleLinearRegression"
                  && RRecipes.Find(Record(simpleAnova)).File == "simple-regression-anova.R", "regression ANOVA selects the actual simple-regression parent");
            Verify(simpleAnova, Anova(simpleAnovaOut.ParameterBag), "Simple regression ANOVA with negative X and incomplete pairs");
            JsonNode legacySimple = JsonNode.Parse(simpleAnova.Record); legacySimple.AsObject().Remove("rootOperation");
            Check(RRecipes.Find(JsonDocument.Parse(legacySimple.ToJsonString()).RootElement).File == "simple-regression-anova.R", "a legacy simple-regression ANOVA is identified without a model parameter");

            ParameterBag lastLinearized = null;
            foreach (int model in new[] { 0, 1, 2 })
            {
                double[] x = model == 0 ? ordinaryX : new[] { 1.0, 2, 3, 4, 5, 6, 7, 8 };
                var answers = Data(x, ordinaryY); answers["model"] = model.ToString();
                var (run, output) = Run("LinearizedEstimates", answers);
                string expected = "stopifnot(model==" + model + "L,length(kwh)==7L,isTRUE(all.equal(unname(linearized_result)," + Vec(new[] { Num(output.ParameterBag, "a"), Num(output.ParameterBag, "b"), Num(output.ParameterBag, "r"), Num(output.ParameterBag, "ste") }) + ",tolerance=1e-9,check.attributes=FALSE)))\n";
                Verify(run, expected, "Selected linearized model " + model + " with incomplete pairs");
                var (anova, anovaOut) = Run("Ranv", new() { ["gamma"] = 0.99 }, output.ParameterBag);
                Check(Record(anova).GetProperty("rootOperation").GetString() == "LinearizedEstimates" && RRecipes.Find(Record(anova)).File == "linearized-regression-anova.R", "ANOVA retains selected linearized model " + model);
                Verify(anova, Anova(anovaOut.ParameterBag), "ANOVA on selected linearized scale " + model);
                JsonNode legacy = JsonNode.Parse(anova.Record); legacy.AsObject().Remove("rootOperation");
                Check(RRecipes.Find(JsonDocument.Parse(legacy.ToJsonString()).RootElement).File == "linearized-regression-anova.R", "a legacy linearized ANOVA retains its explicit model");
                var (plot, _) = Run("LinearizedEstimatePlot", new(), output.ParameterBag);
                Verify(plot, expected, "The plot follow-on retains linearized model " + model);
                var (interpolation, _) = Run("LinearizedEstimateInterpolation", new() { ["newx"] = 2.5 }, output.ParameterBag);
                Verify(interpolation, expected, "The interpolation follow-on retains linearized model " + model);
                int next = (model + 1) % 3;
                var change = Data(new[] { 1.0, 2, 3, 4, 5, 6, 7, 8 }, ordinaryY); change["model"] = next.ToString();
                ParameterBag changedInputs = new(); foreach (var pair in output.ParameterBag) if (pair.Key is not "x" and not "y") changedInputs.Add(pair.Key, pair.Value);
                var (changed, changedOut) = Run("LinearizedEstimatesWithModel", change, changedInputs);
                Verify(changed, "stopifnot(model==" + next + "L,isTRUE(all.equal(unname(linearized_result)," + Vec(new[] { Num(changedOut.ParameterBag, "a"), Num(changedOut.ParameterBag, "b"), Num(changedOut.ParameterBag, "r"), Num(changedOut.ParameterBag, "ste") }) + ",tolerance=1e-9,check.attributes=FALSE)))\n", "Changing the selected linearized model refits the original data");
                lastLinearized = output.ParameterBag;
            }
            // Keep the preceding numerical cache and provenance marker, but
            // acquire genuinely new columns instead of reusing its inputs.
            ParameterBag fresh = new(); foreach (var pair in lastLinearized) if (pair.Key is not "x" and not "y") fresh.Add(pair.Key, pair.Value);
            var freshAnswers = Data(new[] { 1.0, 2, 3, 4, 5, 6, 7, 8 }, new[] { 5.0, 3, 9, 7, 12, 15, 18, 17 }); freshAnswers["gamma"] = 0.95;
            var (freshSimple, freshSimpleOut) = Run("SimpleLinearRegression", freshAnswers, fresh);
            Check(Record(freshSimple).GetProperty("rootOperation").GetString() == "SimpleLinearRegression", "a new root analysis resets the previous linearized provenance");
            var (_, independentSimple) = Run("SimpleLinearRegression", freshAnswers);
            Check(Num(freshSimpleOut.ParameterBag, "slope") == Num(independentSimple.ParameterBag, "slope")
                  && Num(freshSimpleOut.ParameterBag, "abs_yintercept") == Num(independentSimple.ParameterBag, "abs_yintercept"),
                "new simple-regression data replace the preceding fitted numerical context");
            ParameterBag freshSe = ((IList<ParameterBag>)freshSimpleOut.ParameterBag["*se"].AsObject)[0];
            ParameterBag independentSe = ((IList<ParameterBag>)independentSimple.ParameterBag["*se"].AsObject)[0];
            Check(new[] { "slope_err", "se_from", "se_to", "se_r", "se_r2" }.All(key => Num(freshSe, key) == Num(independentSe, key)),
                "a fresh fit replaces the preceding slope uncertainty and confidence limits");
            var (freshAnova, freshAnovaOut) = Run("Ranv", new() { ["gamma"] = 0.95 }, freshSimpleOut.ParameterBag);
            Verify(freshAnova, Anova(freshAnovaOut.ParameterBag), "Fresh simple-regression ANOVA ignores the old linearized cache and model marker");

            foreach (bool binary in new[] { false, true })
            {
                double[][] columns = binary ? new[] { new[] { 1.0, 0, 1, 0, 1, 1 }, new[] { 0.0, 1, 1, 1, 0, 1 }, new[] { 0.0, 0, 1, 0, 0, 1 } } :
                    new[] { new[] { 4.0, 1, 6, 2, 7, 3 }, new[] { 3.0, 5, 2, Constant.MISSING, 6, 2 }, new[] { 1.0, 2, 3, 6, 4, 5 }, new[] { 2.0, 4, 5, 1, 3, 4 } };
                DataFrame Frame(double[][] values) => SheetFrame(Workbook, "Selected treatments", 0, 0, values.Select((v, i) => ("Selected treatment " + (i + 1), v)).ToArray());
                var (run, output) = Run("Friedman", new() { ["data"] = Frame(columns) });
                double[] expected = { Num(output.ParameterBag, "nb"), columns.Length, Num(output.ParameterBag, "a2"), Num(output.ParameterBag, "t1"), Num(output.ParameterBag, "t2"), Num(output.ParameterBag, "p") };
                string check = "stopifnot(binary==" + (binary ? "TRUE" : "FALSE") + ",isTRUE(all.equal(unname(friedman_result)," + Vec(expected) + ",tolerance=1e-10,check.attributes=FALSE)))\n";
                Verify(run, check, binary ? "Cochran Q uses only the selected binary matrix" : "Friedman drops only incomplete selected blocks");
                var (simulation, _) = Run("FriedmanSimulateExactP", new() { ["iterations"] = 200, ["seed"] = 7, ["ci"] = 0.95 }, output.ParameterBag);
                Verify(simulation, check, "The simulation follow-on retains its selected base test");
                foreach (double confidence in new[] { 0.8, 0.99 }) foreach (string sided in new[] { "1", "2" })
                {
                    var (multi, multiOut) = Run("FriedmanMulti", new() { ["confidence"] = confidence, ["sided"] = sided }, output.ParameterBag);
                    IList<ParameterBag> pairs = (IList<ParameterBag>)multiOut.ParameterBag["*pair"].AsObject;
                    string comparison = "stopifnot(abs(critical_t-" + Num(multiOut.ParameterBag, "t").ToString("R", inv) + ")<1e-10,abs(critical_difference-" + Num(pairs[0], "tcrit").ToString("R", inv) + ")<1e-9,isTRUE(all.equal(friedman_comparisons$difference," + Vec(pairs.Select(p => Num(p, "stata"))) + ",tolerance=1e-10)),isTRUE(all.equal(friedman_comparisons$p," + Vec(pairs.Select(p => Num(p, "p"))) + ",tolerance=1e-10)))\n";
                    Verify(multi, comparison, "Selected Friedman comparisons at " + confidence + " confidence and " + sided + " sides");
                }
                if (!binary) foreach (int treatments in new[] { 4, 3 })
                {
                    double[][] replacement = new[] { new[] { 1.0, 5, 3, 7, 2, 6 }, new[] { 4.0, 2, 6, 1, 5, 3 }, new[] { 3.0, 4, 2, 6, 1, 5 }, new[] { 2.0, 1, 5, 3, 6, 4 } }.Take(treatments).ToArray();
                    double[] oldRanks = ((double[])output.ParameterBag["w2"].AsObject).ToArray();
                    ParameterBag prior = new(); foreach (var pair in output.ParameterBag) if (pair.Key != "data") prior.Add(pair.Key, pair.Value);
                    var answers = new Dictionary<string, object> { ["data"] = Frame(replacement), ["confidence"] = 0.8, ["sided"] = "2" };
                    var (reselected, reselectedOut) = Run("FriedmanMulti", answers, prior);
                    var (_, independent) = Run("FriedmanMulti", answers);
                    IList<ParameterBag> expectedPairs = (IList<ParameterBag>)independent.ParameterBag["*pair"].AsObject;
                    IList<ParameterBag> actualPairs = (IList<ParameterBag>)reselectedOut.ParameterBag["*pair"].AsObject;
                    Check(actualPairs.Count == expectedPairs.Count && actualPairs.Zip(expectedPairs).All(p => Num(p.First, "stata") == Num(p.Second, "stata") && Num(p.First, "tcrit") == Num(p.Second, "tcrit"))
                          && oldRanks.SequenceEqual((double[])output.ParameterBag["w2"].AsObject),
                        "reselected Friedman columns refresh cached ranks without mutating the original result");
                    Verify(reselected, "stopifnot(isTRUE(all.equal(friedman_comparisons$difference," + Vec(expectedPairs.Select(p => Num(p, "stata"))) + ",tolerance=1e-10)),isTRUE(all.equal(friedman_comparisons$p," + Vec(expectedPairs.Select(p => Num(p, "p"))) + ",tolerance=1e-10)))\n",
                        "Reselected Friedman comparisons on " + treatments + " treatments");
                }
            }
            foreach (string operation in new[] { "Friedman", "CochranQ", "FriedmanMulti", "FriedmanSimulateExactP", "Ranv", "LinearizedEstimates", "LinearizedEstimatesWithModel", "LinearizedEstimatePlot", "LinearizedEstimateInterpolation" })
            {
                JsonElement example = JsonDocument.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".json"))).RootElement;
                string script = RScriptWriter.Write(JsonDocument.Parse(RecordOf(example)).RootElement, null, null).Text;
                string actual = RunBoxR("invisible(capture.output({\n" + script + "\n}))\nemit <- function(x)cat(formatC(as.double(x),digits=8,format='f'),'\\n')\n" + SelectedAnalysisExampleProbe(operation), Path.Combine(folder, operation));
                string reference = RunBoxR(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".R")), Path.Combine(folder, operation + "-reference"));
                Check(Figures(actual) == Figures(reference), operation + " matches every value of its independent selected-analysis reference");
            }
            passed = true;
        }
        finally
        {
            if (passed && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(folder, true);
            else if (!passed) Console.WriteLine("      routed replay artifacts: " + folder);
        }
    }
}

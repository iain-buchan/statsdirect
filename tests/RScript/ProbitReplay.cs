using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    private static double DoseResponseNumber(ParameterBag bag, string key) => Convert.ToDouble(bag[key].AsObject, inv);
    private static string DoseResponseVector(IEnumerable<double> values) => "c(" + string.Join(",", values.Select(v => v.ToString("R", inv))) + ")";

    private static void CheckProbitReplay()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-probit-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        using JsonDocument example = JsonDocument.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), "Probit.json")));
        double[] Column(string input) => example.RootElement.GetProperty("inputs").EnumerateArray()
            .First(i => i.GetProperty("name").GetString() == input).GetProperty("columns")[0].GetProperty("values")
            .EnumerateArray().Select(v => v.GetDouble()).ToArray();
        double[] doses = Column("dose"), subjects = Column("subjects"), responders = Column("responders");

        (OperationRun run, StepOutput output) Parent(string operation, double confidence, double[] d, double[] s, double[] r,
            bool log = false, int controlSubjects = 0, int controlResponses = 0)
        {
            // Worksheet frames exercise the same acquisition snapshots and named bindings as a live run.
            return Run(operation, new()
            {
                ["dose"] = SheetFrame(Workbook, "Dose response", 0, 0, ("dose", (double[])d.Clone())),
                ["subjects"] = SheetFrame(Workbook, "Dose response", 1, 0, ("subjects", (double[])s.Clone())),
                ["responders"] = SheetFrame(Workbook, "Dose response", 2, 0, ("responders", (double[])r.Clone())),
                ["conf"] = confidence, ["calc-log10-doses"] = log, ["nc"] = controlSubjects,
                ["nrc"] = controlResponses, ["qld"] = 90.0
            });
        }

        string Reopen(OperationRun run, string label)
        {
            string path = Path.Combine(folder, label + ".json");
            File.WriteAllText(path, run.Record);
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            RScript script = RScriptWriter.Write(saved.RootElement, null, null);
            Check(script.HasRecipe && !script.DataUnavailable
                  && !script.Text.Contains("no \"dose\"") && !script.Text.Contains("no \"responders\""),
                label + " reopens with the named dose-response inputs");
            return script.Text;
        }

        void Compare(string script, ParameterBag native, string label, string model, string prediction = null)
        {
            string[] keys = { "a", "b", "mxd", "from", "to", "doseq", "from_cent", "to_cent", "dev", "t_slope" };
            string expected = DoseResponseVector(keys.Select(key => DoseResponseNumber(native, key)));
            string assertions = $$"""

                actual <- c(a, b, back(dose_for(.5)), back(fieller(.5, confidence)),
                            back(dose_for(centile/100)), back(fieller(centile/100, confidence)), chi, t_slope)
                expected <- {{expected}}
                stopifnot(identical(model, "{{model}}"), identical(fit$family$link, "{{model}}"))
                if (!all(abs(actual-expected) <= 1e-6 * pmax(1,abs(expected)))) {
                  print(rbind(actual=actual,expected=expected),digits=16)
                  stop("The replay differs from the actual native dose-response report.")
                }
                {{prediction ?? ""}}
                cat("DOSE RESPONSE VERIFIED\n")
                """;
            bool plotted = label.EndsWith("-ProbitPlot", StringComparison.Ordinal);
            const string plotChecks = """

                stopifnot(plot_count==1L,nrow(dose_response_curve)==401L,
                          all(is.finite(as.matrix(dose_response_curve))),
                          isTRUE(all.equal(dose_response_curve$response,
                            as.numeric(predict(fit,newdata=data.frame(age=curve_transformed_doses),type='response')),tolerance=1e-12)),
                          max(abs(range(dose_response_curve$dose)-range(back(age))))<1e-9,
                          identical(par('family'),initial_family))
                cat('DOSE RESPONSE PLOT VERIFIED\n')
                """;
            string plotTrace = "\npar(family='serif');initial_family<-par('family');plot_count<-0L\ntrace(graphics::plot.new,tracer=quote(plot_count<<-plot_count+1L),print=FALSE)\n";
            string prefix = plotted ? "options(device=function(...)grDevices::svg(filename='dose-response-%03d.svg',...))\n" + plotTrace : "";
            string output = RunR(prefix + script + assertions + (plotted ? plotChecks : ""), Path.Combine(folder, label));
            Check(output.Contains("DOSE RESPONSE VERIFIED"),
                label + " R link, coefficients, selected confidence intervals, doses, heterogeneity and slope test match the actual native report");
            if (plotted)
            {
                Check(output.Contains("DOSE RESPONSE PLOT VERIFIED") && Directory.GetFiles(Path.Combine(folder, label), "dose-response-*.svg").Length == 1,
                    label + " draws one SVG page of the selected model's fitted response curve and restores its font");
                string pdfFolder = Path.Combine(folder, label + "-pdf");
                string pdfOutput = RunR("pdf('dose-response.pdf')\n" + plotTrace + script + plotChecks, pdfFolder);
                Check(pdfOutput.Contains("DOSE RESPONSE PLOT VERIFIED")
                    && System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(pdfFolder, "dose-response.pdf"))), @"/Type /Page\s").Count == 1,
                    label + " draws the same selected curve on one PDF page using portable fonts");
            }
        }

        foreach (string operation in new[] { "Probit", "Logit" })
        {
            string model = operation.ToLowerInvariant();
            foreach (double confidence in new[] { 0.95, 0.80, 0.99 })
            {
                var (run, output) = Parent(operation, confidence, doses, subjects, responders);
                string label = operation + "-" + confidence.ToString("F2", inv);
                Compare(Reopen(run, label), output.ParameterBag, label, model);
                if (confidence == 0.95)
                {
                    JsonNode olderParent = JsonNode.Parse(run.Record);
                    olderParent.AsObject().Remove("rootOperation");
                    olderParent["parameters"].AsObject().Remove("doseResponseModel");
                    RScript olderScript = RScriptWriter.Write(JsonDocument.Parse(olderParent.ToJsonString()).RootElement, null, null);
                    Compare(olderScript.Text, output.ParameterBag, label + "-legacy-parent", model);
                    foreach (string child in new[] { "ProbitMore", "ProbitPlot", "ProbitInterpolateX", "ProbitInterpolateY" })
                    {
                        Dictionary<string, object> answers = child switch
                        {
                            "ProbitInterpolateX" => new() { ["newx"] = 13.2 },
                            "ProbitInterpolateY" => new() { ["newy"] = 0.75 },
                            _ => new()
                        };
                        var (continued, continuedOutput) = Run(child, answers, output.ParameterBag);
                        string prediction = child switch
                        {
                            "ProbitMore" => "stopifnot(isTRUE(all.equal(dose_response_table$expected,"
                                + DoseResponseVector(((IList<ParameterBag>)continuedOutput.ParameterBag["*obs"].AsObject).Select(row => DoseResponseNumber(row, "exp")))
                                + ",tolerance=1e-7,check.attributes=FALSE)),isTRUE(all.equal(dose_response_table$deviation,"
                                + DoseResponseVector(((IList<ParameterBag>)continuedOutput.ParameterBag["*obs"].AsObject).Select(row => DoseResponseNumber(row, "dev")))
                                + ",tolerance=1e-7,check.attributes=FALSE)))",
                            "ProbitInterpolateX" => "stopifnot(abs(predicted_response - "
                                + DoseResponseNumber(continuedOutput.ParameterBag, "resp").ToString("R", inv) + ") < 1e-6)",
                            "ProbitInterpolateY" => "stopifnot(abs(predicted_dose - "
                                + DoseResponseNumber(continuedOutput.ParameterBag, "x").ToString("R", inv) + ") < 1e-6)",
                            _ => null
                        };
                        Compare(Reopen(continued, label + "-" + child), continuedOutput.ParameterBag, label + "-" + child, model, prediction);
                        if (child == "ProbitMore")
                        {
                            JsonNode olderChild = JsonNode.Parse(continued.Record);
                            olderChild.AsObject().Remove("rootOperation");
                            RScript modelOnly = RScriptWriter.Write(JsonDocument.Parse(olderChild.ToJsonString()).RootElement, null, null);
                            Compare(modelOnly.Text, continuedOutput.ParameterBag, label + "-model-only-child", model);
                            olderChild["parameters"].AsObject().Remove("doseResponseModel");
                            RScript ambiguous = RScriptWriter.Write(JsonDocument.Parse(olderChild.ToJsonString()).RootElement, null, null);
                            Check(!ambiguous.HasRecipe && !ambiguous.Text.Contains("fit <- glm"),
                                label + " an old shared child with no parent/model provenance does not guess a logit fit");
                        }
                    }
                    // Editing an embedded observation must refit the new values, rather than reuse report coefficients.
                    double[] changed = (double[])responders.Clone(); changed[11] += 2;
                    var (_, changedOutput) = Parent(operation, confidence, doses, subjects, changed);
                    JsonNode edited = JsonNode.Parse(run.Record);
                    edited["data"]["responders"]["responders"][11] = changed[11];
                    RScript changedScript = RScriptWriter.Write(JsonDocument.Parse(edited.ToJsonString()).RootElement, null, null);
                    Compare(changedScript.Text, changedOutput.ParameterBag, label + "-edited-data", model);
                }
            }

            var (logged, loggedOutput) = Parent(operation, 0.99, doses, subjects, responders, log: true);
            Compare(Reopen(logged, operation + "-log-doses"), loggedOutput.ParameterBag, operation + "-log-doses", model);
            foreach (string child in new[] { "ProbitInterpolateX", "ProbitInterpolateY", "ProbitPlot" })
            {
                var (continued, continuedOutput) = Run(child,
                    child == "ProbitInterpolateX" ? new() { ["newx"] = 13.2 } : child == "ProbitInterpolateY" ? new() { ["newy"] = 0.75 } : new(),
                    loggedOutput.ParameterBag);
                string prediction = child == "ProbitInterpolateX"
                    ? "stopifnot(abs(predicted_response - " + DoseResponseNumber(continuedOutput.ParameterBag, "resp").ToString("R", inv) + ") < 1e-6)"
                    : child == "ProbitInterpolateY" ? "stopifnot(abs(predicted_dose - " + DoseResponseNumber(continuedOutput.ParameterBag, "x").ToString("R", inv) + ") < 1e-6)" : null;
                Compare(Reopen(continued, operation + "-log-" + child), continuedOutput.ParameterBag, operation + "-log-" + child, model, prediction);
            }
            double[] incompleteDoses = (double[])doses.Clone(), incompleteResponses = (double[])responders.Clone();
            incompleteDoses[2] = StatsDirect.Numerics.Constant.MISSING;
            incompleteResponses[12] = StatsDirect.Numerics.Constant.MISSING;
            var (incomplete, incompleteOutput) = Parent(operation, 0.80, incompleteDoses, subjects, incompleteResponses);
            Compare(Reopen(incomplete, operation + "-missing-data"), incompleteOutput.ParameterBag, operation + "-missing-data", model);

            // A zero-response control contributes no fitted background parameter. Its row is excluded before logging.
            var (zeroControl, zeroOutput) = Parent(operation, 0.80, new[] { 0.0 }.Concat(doses).ToArray(),
                new[] { 200.0 }.Concat(subjects).ToArray(), new[] { 0.0 }.Concat(responders).ToArray());
            Compare(Reopen(zeroControl, operation + "-zero-control"), zeroOutput.ParameterBag, operation + "-zero-control", model);

            // Positive natural response needs a joint background-rate model; never silently fit the unadjusted observations.
            double[] controlDoses = { 1, 2, 3, 4, 5, 6, 7, 8 };
            double[] controlSubjects = Enumerable.Repeat(100.0, 8).ToArray();
            double[] controlResponders = { 12, 18, 30, 50, 70, 85, 94, 98 };
            foreach (bool inData in new[] { false, true })
            {
                var (controlled, _) = Parent(operation, 0.95,
                    inData ? new[] { 0.0 }.Concat(controlDoses).ToArray() : controlDoses,
                    inData ? new[] { 200.0 }.Concat(controlSubjects).ToArray() : controlSubjects,
                    inData ? new[] { 20.0 }.Concat(controlResponders).ToArray() : controlResponders,
                    controlSubjects: inData ? 0 : 200, controlResponses: inData ? 0 : 20);
                string script = Reopen(controlled, operation + "-controls-" + inData);
                string guarded = RunR("tryCatch({\n" + script + "\nstop('An unsupported control model was silently fitted.')\n}, error=function(e) {\n"
                    + "stopifnot(grepl('does not yet support a fitted natural response rate', conditionMessage(e),fixed=TRUE)); cat('CONTROL GUARD VERIFIED\\n')\n})\n",
                    Path.Combine(folder, operation + "-controls-" + inData));
                Check(guarded.Contains("CONTROL GUARD VERIFIED"),
                    operation + (inData ? " zero-dose" : " separate") + " positive-response controls stop with an explicit unsupported-model explanation");
            }
            // The native Finney interval adjustment differs from an ordinary scaled-covariance R interval.
            // Preserve that separate numerical issue rather than present a substitute interval as equivalent.
            var (heterogeneous, heterogeneousOutput) = Parent(operation, 0.95, controlDoses, controlSubjects,
                new double[] { 10, 30, 15, 70, 45, 85, 70, 98 });
            string heterogeneityScript = Reopen(heterogeneous, operation + "-heterogeneity");
            string heterogeneityExpected = DoseResponseVector(new[] { "a", "b", "dev", "t_slope" }
                .Select(key => DoseResponseNumber(heterogeneousOutput.ParameterBag, key)));
            string heterogeneityR = RunR("tryCatch({\n" + heterogeneityScript
                + "\nstop('Unsupported heterogeneity intervals were silently reported.')\n}, error=function(e) {\n"
                + "stopifnot(grepl('cannot yet faithfully reproduce the native heterogeneity-adjusted dose intervals', conditionMessage(e),fixed=TRUE));"
                + "actual<-c(a,b,chi,b/sqrt(v[2,2]*chi/df));expected<-" + heterogeneityExpected + ";"
                + "stopifnot(heterogeneity,all(abs(actual-expected)<=1e-6*pmax(1,abs(expected))));cat('HETEROGENEITY GUARD VERIFIED\\n')\n})\n",
                Path.Combine(folder, operation + "-heterogeneity"));
            Check(heterogeneityR.Contains("HETEROGENEITY GUARD VERIFIED"),
                operation + " heterogeneous data match the native fit, Pearson statistic and adjusted slope test, then explicitly stop before unsupported dose intervals");
        }
        foreach (string operation in new[] { "Probit", "Logit", "ProbitMore", "ProbitPlot", "ProbitInterpolateX", "ProbitInterpolateY" })
        {
            using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".json")));
            RScript replay = RScriptWriter.Write(JsonDocument.Parse(RecordOf(fixture.RootElement)).RootElement, null, null);
            string actual = RunR(replay.Text, Path.Combine(folder, operation + "-fixture"));
            string expected = RunR(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".R")), Path.Combine(folder, operation + "-reference"));
            Check(replay.HasRecipe && Figures(actual) == Figures(expected),
                operation + " named fixture agrees with every figure in its independent R model reference");
        }
        Console.WriteLine("      dose-response replay artifacts: " + folder);
    }
}

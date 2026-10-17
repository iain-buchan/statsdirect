using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Charting;
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
        string oracle = File.ReadAllText(Path.Combine(Metadata("Examples"), "DoseResponseOracle.R"));

        (OperationRun run, StepOutput output) Parent(string operation, double confidence, double[] d, double[] s, double[] r,
            bool log = false, int controlSubjects = 0, int controlResponses = 0, double centile = 90)
        {
            // Worksheet frames exercise the same acquisition snapshots and named bindings as a live run.
            return Run(operation, new()
            {
                ["dose"] = SheetFrame(Workbook, "Dose response", 0, 0, ("dose", (double[])d.Clone())),
                ["subjects"] = SheetFrame(Workbook, "Dose response", 1, 0, ("subjects", (double[])s.Clone())),
                ["responders"] = SheetFrame(Workbook, "Dose response", 2, 0, ("responders", (double[])r.Clone())),
                ["conf"] = confidence, ["calc-log10-doses"] = log, ["nc"] = controlSubjects,
                ["nrc"] = controlResponses, ["qld"] = centile
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
                            observed_response_for(a+b*curve_transformed_doses),tolerance=1e-12)),
                          length(plotted_curves)==3L,
                          isTRUE(all.equal(as.numeric(plotted_curves[[1]]$x),dose_response_curve$dose,tolerance=1e-12)),
                          isTRUE(all.equal(as.numeric(plotted_curves[[1]]$y),response_for(a+b*curve_transformed_doses),tolerance=1e-12)),
                          isTRUE(all.equal(as.numeric(plotted_curves[[2]]$y),dose_response_curve$lower,tolerance=1e-12)),
                          isTRUE(all.equal(as.numeric(plotted_curves[[3]]$y),dose_response_curve$upper,tolerance=1e-12)),
                          isTRUE(all.equal(as.numeric(plotted_observations$x),dose_response_plot_points$dose,tolerance=1e-12)),
                          isTRUE(all.equal(as.numeric(plotted_observations$y),(menses/girls-natural_response)/(1-natural_response),tolerance=1e-12)),
                          isTRUE(all.equal(dose_response_curve$lower,response_for(a+b*curve_transformed_doses-critical*sqrt(
                            selected_covariance[1,1]+2*curve_transformed_doses*selected_covariance[1,2]+curve_transformed_doses^2*selected_covariance[2,2])),tolerance=1e-12)),
                          max(abs(range(dose_response_curve$dose)-range(back(age))))<1e-9,
                          identical(par('family'),initial_family))
                cat('DOSE RESPONSE PLOT VERIFIED\n')
                """;
            string plotTrace = "\npar(family='serif');initial_family<-par('family');plot_count<-0L;plotted_curves<-list()\ntrace(graphics::plot.new,tracer=quote(plot_count<<-plot_count+1L),print=FALSE)\n"
                + "trace('plot.default',where=asNamespace('graphics'),tracer=quote(.GlobalEnv$plotted_observations<-list(x=x,y=y)),print=FALSE)\n"
                + "trace('lines.default',where=asNamespace('graphics'),tracer=quote(.GlobalEnv$plotted_curves[[length(.GlobalEnv$plotted_curves)+1L]]<-list(x=x,y=y)),print=FALSE)\n";
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
                string freshPdfFolder = Path.Combine(folder, label + "-fresh-pdf");
                string freshPdf = RunR("options(device=function(...)pdf('dose-response.pdf',...))\n"
                    + "stopifnot(dev.cur()==1L)\n" + script + "\nstopifnot(all(is.finite(as.matrix(dose_response_curve))))\ncat('FRESH PDF VERIFIED\\n')\n",
                    freshPdfFolder);
                Check(freshPdf.Contains("FRESH PDF VERIFIED")
                    && System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(freshPdfFolder, "dose-response.pdf"))), @"/Type /Page\s").Count == 1,
                    label + " opens one PDF from a fresh null graphics device without a font error");
            }
        }

        string MoreChecks(ParameterBag native)
        {
            IList<ParameterBag> rows = (IList<ParameterBag>)native["*obs"].AsObject;
            string[] columns = { "subjects", "responses", "expected", "deviation" };
            string[] nativeKeys = { "sub", "res", "exp", "dev" };
            string checksR = "stopifnot(nrow(dose_response_table)==" + rows.Count + "L)\n";
            for (int index = 0; index < columns.Length; index++)
                checksR += "stopifnot(isTRUE(all.equal(dose_response_table$" + columns[index] + ","
                    + DoseResponseVector(rows.Select(row => DoseResponseNumber(row, nativeKeys[index])))
                    + ",tolerance=2e-6,check.attributes=FALSE)))\n";
            foreach (string key in new[] { "sxx", "sxy", "syy", "var_b", "seb" })
                checksR += "stopifnot(abs(dose_response_diagnostics$" + key + "-"
                    + DoseResponseNumber(native, key).ToString("R", inv) + ")<=2e-6*max(1,abs("
                    + DoseResponseNumber(native, key).ToString("R", inv) + ")))\n";
            checksR += "stopifnot(abs(dose_response_diagnostics$var_b-dose_response_diagnostics$seb^2)<1e-12)\n";
            if (native["*natural"].AsObject is IList<ParameterBag> natural && natural.Count > 0)
                checksR += "stopifnot(abs(natural_response-" + DoseResponseNumber(natural[0], "c").ToString("R", inv)
                    + ")<2e-6,abs(dose_response_diagnostics$natural_se-"
                    + DoseResponseNumber(natural[0], "seb_c").ToString("R", inv) + ")<2e-6)\n";
            return checksR;
        }

        void Children(OperationRun parentRun, ParameterBag parentOutput, string label, string model, double interpolationDose)
        {
            foreach (string child in new[] { "ProbitMore", "ProbitPlot", "ProbitInterpolateX", "ProbitInterpolateY" })
            {
                ChartDefinition nativeChart = null;
                var (continued, output) = Run(child, child switch
                {
                    "ProbitInterpolateX" => new() { ["newx"] = interpolationDose },
                    "ProbitInterpolateY" => new() { ["newy"] = .75 },
                    _ => new()
                }, parentOutput, amend: item => nativeChart = item as ChartDefinition);
                string checksR = child switch
                {
                    "ProbitMore" => MoreChecks(output.ParameterBag),
                    "ProbitInterpolateX" => "stopifnot(abs(predicted_response-" + DoseResponseNumber(output.ParameterBag, "resp").ToString("R", inv)
                        + ")<2e-6,abs(predicted_observed_response-(natural_response+(1-natural_response)*predicted_response))<1e-12)\n",
                    "ProbitInterpolateY" => "stopifnot(abs(predicted_dose-" + DoseResponseNumber(output.ParameterBag, "x").ToString("R", inv)
                        + ")<2e-6*max(1,abs(predicted_dose)))\n",
                    _ => ""
                };
                if (child == "ProbitPlot")
                {
                    nativeChart ??= output.ParameterBag["chart"].AsObject as ChartDefinition;
                    LogitOptions options = (LogitOptions)nativeChart.ChartOptions;
                    Check(options.VarianceIntercept.HasValue && options.CovarianceInterceptSlope.HasValue && options.VarianceSlope.HasValue,
                        label + " native plot receives the full selected intercept/slope covariance for its confidence bands");
                    checksR += "stopifnot(isTRUE(all.equal(c(selected_covariance[1,1],selected_covariance[1,2],selected_covariance[2,2],critical),"
                        + DoseResponseVector(new[] { options.VarianceIntercept.Value, options.CovarianceInterceptSlope.Value,
                            options.VarianceSlope.Value, options.t }) + ",tolerance=2e-6,check.attributes=FALSE)))\n";
                    checksR += "stopifnot(isTRUE(all.equal(dose_response_plot_points$response,"
                        + DoseResponseVector(((DoubleSeries)nativeChart.YSeries[0]).Data.Where(double.IsFinite)
                            .Where(x => x != StatsDirect.Numerics.Constant.MISSING))
                        + ",tolerance=2e-6,check.attributes=FALSE)))\n";
                }
                Compare(Reopen(continued, label + "-" + child), output.ParameterBag, label + "-" + child, model,
                    oracle + "\nverify_dose_response_oracle()\n" + checksR);
                if (child == "ProbitMore")
                {
                    // A saved follow-on must replay both an explicit model and the retained original root.
                    JsonNode legacy = JsonNode.Parse(continued.Record);
                    legacy.AsObject().Remove("rootOperation");
                    Compare(RScriptWriter.Write(JsonDocument.Parse(legacy.ToJsonString()).RootElement, null, null).Text,
                        output.ParameterBag, label + "-legacy-More", model, oracle + "\nverify_dose_response_oracle()\n" + checksR);
                }
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

            double[] controlDoses = { 1, 2, 3, 4, 5, 6, 7, 8 };
            double[] controlSubjects = Enumerable.Repeat(100.0, 8).ToArray();
            double[] controlResponders = { 12, 18, 30, 50, 70, 85, 94, 98 };
            foreach (bool separatedLogged in new[] { false, true })
            {
                double[] separatedDoses = new double[] { 1, 100, 101, 102, 103, 104, 105, 106 };
                if (separatedLogged) separatedDoses = separatedDoses.Select(x => Math.Pow(10, x/2)).ToArray();
                var (separated, separatedNative) = Parent(operation, .95, separatedDoses, controlSubjects,
                    new double[] { 10, 18, 30, 50, 70, 85, 94, 98 }, separatedLogged, 200, 20);
                string separatedLabel = operation + "-widely-separated-doses-" + separatedLogged;
                Compare(Reopen(separated, separatedLabel), separatedNative.ParameterBag, separatedLabel, model,
                    oracle + "\nverify_dose_response_oracle()\nstopifnot(df==6L,nc==200,nrc==20,length(age)==8L)\n");
            }
            foreach (double background in new[] { .02, .40, .75 })
            {
                double[] rateSubjects = Enumerable.Repeat(1000.0, 8).ToArray();
                // A deliberately different, rounded dose-response curve at each background rate.
                double[] rateResponders = new[] { .01, .06, .19, .42, .69, .88, .97, .995 }
                    .Select(p => Math.Round(1000 * (background + (1-background)*p))).ToArray();
                double requestedCentile = background == .02 ? 10 : 80;
                var (rateRun, rateNative) = Parent(operation, .95, controlDoses, rateSubjects, rateResponders,
                    controlSubjects: 2000, controlResponses: (int)(2000*background), centile: requestedCentile);
                string rateLabel = operation + "-background-" + background.ToString("F2", inv);
                Compare(Reopen(rateRun, rateLabel), rateNative.ParameterBag, rateLabel, model,
                    oracle + "\nverify_dose_response_oracle()\nstopifnot(centile==" + requestedCentile.ToString("R", inv) + ")\n");
            }
            foreach (bool log in new[] { false, true })
            {
                double[] selectedDoses = log ? controlDoses.Select(x => Math.Pow(10, x / 2)).ToArray() : controlDoses;
                double interpolationDose = log ? Math.Pow(10, 2.25) : 4.5;
                foreach (double confidence in new[] { .80, .95, .99 })
                foreach (bool embedded in new[] { false, true })
                {
                    var (controlled, native) = Parent(operation, confidence,
                        embedded ? new[] { 0.0 }.Concat(selectedDoses).ToArray() : selectedDoses,
                        embedded ? new[] { 200.0 }.Concat(controlSubjects).ToArray() : controlSubjects,
                        embedded ? new[] { 20.0 }.Concat(controlResponders).ToArray() : controlResponders,
                        log, embedded ? 0 : 200, embedded ? 0 : 20);
                    string label = operation + "-positive-control-" + log + "-" + embedded + "-" + confidence.ToString("F2", inv);
                    Compare(Reopen(controlled, label), native.ParameterBag, label, model,
                        oracle + "\nverify_dose_response_oracle()\nstopifnot(nc==200,nrc==20,natural_response>0,!heterogeneity)\n");
                    if (confidence == .95 && embedded)
                        Children(controlled, native.ParameterBag, label, model, interpolationDose);
                }
                // Leading low-response groups can initialize the fit, but each original group enters the likelihood once.
                var (pooled, pooledNative) = Parent(operation, .95, selectedDoses, controlSubjects,
                    new double[] { 4, 8, 20, 40, 65, 80, 90, 97 }, log, 200, 20);
                string pooledLabel = operation + "-leading-low-response-" + log;
                Compare(Reopen(pooled, pooledLabel), pooledNative.ParameterBag, pooledLabel, model,
                    oracle + "\nverify_dose_response_oracle()\nstopifnot(nc==200,nrc==20,length(age)==8L)\n");

                foreach (bool controls in new[] { false, true })
                foreach (double confidence in controls ? new[] { .80, .95 } : new[] { .80, .95, .99 })
                {
                    double[] changedResponses = controls ? new double[] { 12, 35, 18, 75, 48, 88, 75, 98 }
                        : new double[] { 10, 30, 15, 70, 45, 85, 70, 98 };
                    var (heterogeneous, heterogeneousNative) = Parent(operation, confidence, selectedDoses,
                        controlSubjects, changedResponses, log, controls ? 200 : 0, controls ? 20 : 0);
                    string label = operation + "-heterogeneous-" + controls + "-" + log + "-" + confidence.ToString("F2", inv);
                    Compare(Reopen(heterogeneous, label), heterogeneousNative.ParameterBag, label, model,
                        oracle + "\nverify_dose_response_oracle()\nstopifnot(heterogeneity,critical==qt((1+confidence)/2,df))\n");
                    if (confidence == .95)
                        Children(heterogeneous, heterogeneousNative.ParameterBag, label, model, interpolationDose);
                    if (controls && confidence == .95)
                    {
                        bool refused = false;
                        try { Parent(operation, .99, selectedDoses, controlSubjects, changedResponses, log, 200, 20); }
                        catch (Exception error) { refused = error.ToString().Contains("confidence", StringComparison.OrdinalIgnoreCase); }
                        Check(refused, label + " native analysis explicitly refuses its unbounded 99% confidence interval");
                        JsonNode unbounded = JsonNode.Parse(heterogeneous.Record);
                        unbounded["parameters"]["conf"] = .99;
                        RScript unboundedR = RScriptWriter.Write(JsonDocument.Parse(unbounded.ToJsonString()).RootElement, null, null);
                        string verified = RunR(unboundedR.Text + oracle + "\nverify_dose_response_oracle()\n"
                            + "stopifnot(heterogeneity,all(heterogeneity_interval_status!='bounded'),any(!is.finite(fieller(.5))))\ncat('UNBOUNDED SET VERIFIED\\n')\n",
                            Path.Combine(folder, label + "-unbounded"));
                        Check(verified.Contains("UNBOUNDED SET VERIFIED"), label + " R reports the actual unbounded/disjoint set rather than a false finite interval");
                    }
                }
            }
            // More must remove the control/missing rows and retain the complete treated worksheet order and final row.
            double missing = StatsDirect.Numerics.Constant.MISSING;
            double[] unsortedDoses = { 0, 8, 1, missing, 4, 2, 7, 3, 6, 5 };
            double[] unsortedSubjects = { 200, 100, 100, 100, 100, 100, 100, 100, 100, 100 };
            double[] unsortedResponses = { 20, 98, 12, 80, 50, 18, 94, 30, 85, 70 };
            var (unsorted, unsortedNative) = Parent(operation, .95, unsortedDoses, unsortedSubjects, unsortedResponses);
            Children(unsorted, unsortedNative.ParameterBag, operation + "-unsorted-missing", model, 4.5);

            // A changed, reopened follow-on must refit its snapshot, including the natural response.
            var (beforeEdit, beforeNative) = Parent(operation, .95, controlDoses, controlSubjects, controlResponders, false, 200, 20);
            foreach (int invalidResponses in new[] { 200, 201 })
            {
                JsonNode invalidControls = JsonNode.Parse(beforeEdit.Record);
                invalidControls["parameters"]["nrc"] = invalidResponses;
                string invalidScript = RScriptWriter.Write(JsonDocument.Parse(invalidControls.ToJsonString()).RootElement, null, null).Text;
                string explanation = invalidResponses == 200 ? "at least one responding and one nonresponding" : "no more responses than subjects";
                string result = RunR("tryCatch({\n" + invalidScript + "\nstop('Invalid controls were accepted.')\n},error=function(e){\n"
                    + "stopifnot(grepl('" + explanation + "',conditionMessage(e),fixed=TRUE));cat('INVALID CONTROLS VERIFIED\\n')\n})\n",
                    Path.Combine(folder, operation + "-invalid-controls-" + invalidResponses));
                Check(result.Contains("INVALID CONTROLS VERIFIED"), operation + " invalid control counts have a clear explanation rather than a false fitted report");
            }
            var (oldMore, _) = Run("ProbitMore", new(), beforeNative.ParameterBag);
            double[] editedResponses = (double[])controlResponders.Clone(); editedResponses[4] += 3;
            var (_, freshNative) = Parent(operation, .95, controlDoses, controlSubjects, editedResponses, false, 200, 20);
            var (_, freshMore) = Run("ProbitMore", new(), freshNative.ParameterBag);
            JsonNode editedMore = JsonNode.Parse(oldMore.Record);
            editedMore["data"]["responders"]["responders"][4] = editedResponses[4];
            Compare(RScriptWriter.Write(JsonDocument.Parse(editedMore.ToJsonString()).RootElement, null, null).Text,
                freshMore.ParameterBag, operation + "-edited-controlled-followon", model,
                oracle + "\nverify_dose_response_oracle()\n" + MoreChecks(freshMore.ParameterBag));

            // Unused H/t intervals may be unbounded when the selected normal interval is valid.
            double[] weakResponses = operation == "Probit" ? new double[] { 25, 24, 15, 22, 28, 25, 33, 33 }
                : new double[] { 29, 21, 30, 33, 23, 33, 35, 36 };
            var (weak, weakNative) = Parent(operation, .95, controlDoses, controlSubjects, weakResponses);
            Compare(Reopen(weak, operation + "-unused-unbounded-branch"), weakNative.ParameterBag,
                operation + "-unused-unbounded-branch", model,
                oracle + "\nverify_dose_response_oracle()\n"
                + "stopifnot(!heterogeneity,interval_for(.5)$status=='bounded',qt(.975,df)^2*v[2,2]*chi/df/b^2>1)\n");
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

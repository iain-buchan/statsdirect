using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Selected-output replay checks: exercise actual operation records, then compare
    // the exported script with the native result and independent standard R calls.
    private static void CheckRecipeAdvancedSimplicity()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-advanced-simplicity-" + Guid.NewGuid().ToString("N"));
        int column = 0;
        DataFrame Sheet(string title, params double[] values) => SheetFrame(Workbook, "Advanced", column++, 0, (title, values));
        string Number(double value) => value.ToString("R", inv);
        IEnumerable<ParameterBag> Bags(ParameterBag bag)
        {
            yield return bag;
            foreach (var pair in bag)
                if (pair.Value?.AsObject is IList<ParameterBag> list)
                    foreach (ParameterBag child in list)
                        foreach (ParameterBag descendant in Bags(child)) yield return descendant;
        }
        double Value(ParameterBag bag, string key) => Convert.ToDouble(Bags(bag).First(b => b.ContainsKey(key) && b[key]?.AsObject != null)[key].AsObject, inv);
        string Script(OperationRun run) => RScriptWriter.Write(Record(run), null, null).Text;
        string RunScript(string name, string script, string assertion, int pages)
        {
            string runFolder = Path.Combine(folder, name);
            string output = RunR("options(device = function(...) grDevices::svg(filename = 'chart-%03d.svg', ...))\n"
                + script + "\nnear <- function(a,b) isTRUE(all.equal(as.numeric(a),as.numeric(b),tolerance=2e-7,check.attributes=FALSE))\n"
                + assertion + "\ncat('ADVANCED_OK\\n')\n", runFolder).Replace("\r", "");
            Check(output.Contains("ADVANCED_OK"), name + " replays selected statistics and preserves advanced inference");
            Check(Directory.GetFiles(runFolder, "chart-*.svg").Length == pages, name + " draws only its selected report charts");
            return output;
        }

        bool earlierExact = MetaExact;
        try
        {
            MetaExact = true;
            foreach (double confidence in new[] { .8, .99 })
                foreach (bool ratio in new[] { false, true })
                {
                    string operation = ratio ? "MetaIncidenceRateRatio" : "MetaIncidenceRateDifference";
                    var (run, output) = Run(operation, new()
                    {
                        ["gamma"] = confidence, ["strata"] = null,
                        ["a"] = Sheet("exposed cases", 4, 9, 16), ["pt1"] = Sheet("exposed time", 1000, 800, 1200),
                        ["b"] = Sheet("control cases", 3, 5, 11), ["pt2"] = Sheet("control time", 1100, 950, 1600)
                    });
                    ParameterBag bag = output.ParameterBag;
                    string assertion = ratio
                        ? $"stopifnot(!exists('ird'),near(exp(res_r$fixed),{Number(Value(bag, "rmh"))}),near(exp(res_r$random),{Number(Value(bag, "dsirr"))}),near(cmle,{Number(Value(bag, "eor"))}),near(fisher,c({Number(Value(bag, "llf"))},{Number(Value(bag, "ulf"))})),near(mid,c({Number(Value(bag, "llm"))},{Number(Value(bag, "ulm"))})))\n"
                          + "stopifnot(all(vapply(seq_along(a),function(i) near(exact[,i],poisson.test(c(a[i],b[i]),c(pt1[i],pt2[i]),conf.level=confidence)$conf.int),logical(1))))"
                        : $"stopifnot(!exists('irr'),near(res$fixed,{Number(Value(bag, "rmh"))}),near(res$random,{Number(Value(bag, "dsird"))}),near(res$fixed+c(-1,1)*z*res$se,c({Number(Value(bag, "from"))},{Number(Value(bag, "to"))})))";
                    string text = RunScript(operation + "-" + confidence, Script(run), assertion, 2);
                    Check(Regex.Matches(text, "meta-analysis\\n").Count == 1 && !text.Contains(ratio ? "Incidence rate difference (IRD)" : "Incidence rate ratio (IRR)"), operation + " excludes the other menu option's analysis");
                }

            // The ratio solver must follow selected data beyond the old example-only
            // interval [.01,100], in either direction, without changing its tails.
            foreach (double exposedTimeScale in new[] { .0001, 10000 })
            {
                var (run, output) = Run("MetaIncidenceRateRatio", new()
                {
                    ["gamma"] = .95, ["strata"] = null,
                    ["a"] = Sheet("exposed cases", 4, 9, 16), ["pt1"] = Sheet("exposed time", 1000 * exposedTimeScale, 800 * exposedTimeScale, 1200 * exposedTimeScale),
                    ["b"] = Sheet("control cases", 3, 5, 11), ["pt2"] = Sheet("control time", 1100, 950, 1600)
                });
                ParameterBag bag = output.ParameterBag;
                string assertion = $"stopifnot(cmle<.01||cmle>100,near(cmle,{Number(Value(bag, "eor"))}),near(fisher,c({Number(Value(bag, "llf"))},{Number(Value(bag, "ulf"))})),near(mid,c({Number(Value(bag, "llm"))},{Number(Value(bag, "ulm"))})))";
                RunScript("MetaIncidenceRateRatio-wide-" + exposedTimeScale, Script(run), assertion, 2);
            }

            foreach (double confidence in new[] { .8, .95, .99 })
                foreach (bool screen in new[] { false, true })
                {
                    string operation = screen ? "MantelHaenszelScreen" : "Mantel";
                    Dictionary<string, object> answers = screen
                        ? new() { ["cco"] = confidence, ["plot_forest"] = true, ["try_exact"] = true,
                            ["data"] = SheetFrame(Workbook, "Advanced", column++, 0, ("exposed", new double[] { 8, 24, 12, 31, 17, 25 }), ("control", new double[] { 5, 29, 7, 38, 8, 33 })) }
                        : new() { ["gamma"] = confidence, ["strata"] = null, ["sn"] = Sheet("exposed", 32, 43, 42), ["sr"] = Sheet("cases", 8, 12, 17), ["xn"] = Sheet("control", 34, 45, 41), ["xr"] = Sheet("cases", 5, 7, 8) };
                    var (run, output) = Run(operation, answers);
                    ParameterBag bag = output.ParameterBag;
                    string script = Script(run);
                    string assertion = "standard <- mantelhaen.test(tables,conf.level=confidence)\n"
                        + "stopifnot(near(or_mh,standard$estimate),near(mh_ci,standard$conf.int))\n"
                        // The native exact solver is the independent high-precision oracle;
                        // keep the recipe's tighter exact and mid-P inversion, not R defaults.
                        + $"stopifnot(near(cml,{Number(Value(bag, "eor"))}),near(fisher,c({Number(Value(bag, "llf"))},{Number(Value(bag, "ulf"))})),near(midp,c({Number(Value(bag, "llm"))},{Number(Value(bag, "ulm"))})))\n"
                        + "stopifnot(near(lci,vapply(seq_len(k),function(i)exact_limits(conditional(a[i],b[i],c[i],d[i]),a[i],level=confidence)[1],numeric(1))))";
                    RunScript(operation + "-" + confidence, script, assertion, 4);
                    Check(script.Contains("or_mh <- unname(r$estimate)") && !script.Contains("exact = TRUE"), operation + " reuses standard approximate inference without rerunning a lower-precision exact analysis");
                }

            foreach (string method in new[] { "arcsine", "doubleArcsine" })
                foreach (double confidence in new[] { .8, .99 })
                {
                    var (run, output) = Run("ProportionMeta", new()
                    {
                        ["gamma"] = confidence, ["method"] = method, ["strata"] = null,
                        ["sn"] = Sheet("subjects", 50, 85, 43, 92), ["sr"] = Sheet("responses", 12, 36, 20, 63)
                    });
                    string assertion = $"stopifnot(near(inv(xf),{Number(Value(output.ParameterBag, "rmh"))}),near(inv(xr),{Number(Value(output.ParameterBag, "dspr"))}))";
                    assertion += $"\nstopifnot(near(round(i2(lambda_at((1+confidence)/2)),1),{Number(Math.Round(Value(output.ParameterBag, "llisq"), 1))}),near(round(i2(lambda_at((1-confidence)/2)),1),{Number(Math.Round(Value(output.ParameterBag, "ulisq"), 1))}))";
                    if (method == "doubleArcsine") assertion += "\nstopifnot(inv(-1)==0,inv(4)==1)";
                    string text = RunScript("ProportionMeta-" + method + "-" + confidence, Script(run), assertion, 3);
                    Check(Regex.Matches(text, "(?m)^Method:").Count == 1 && Regex.Matches(text, "(?m)^Harbord:").Count == 1, "proportion meta-analysis prints only the selected inverse and its bias result");
                }

            foreach (double? exposure in new double?[] { null, 0, .37, 1 })
                foreach (double confidence in new[] { .9, .99 })
                {
                    var (run, output) = Run("MiscRetroRisk", new() { ["scrap"] = new double[] { 255, 49, 93, 46 }, ["pe"] = exposure, ["cco"] = confidence });
                    ParameterBag risk = Bags(output.ParameterBag).First(b => b.ContainsKey("par"));
                    string assertion = $"stopifnot(near(100*px,{Number(Value(risk, "pe"))}),near(100*par,{Number(Value(risk, "par"))}),near(100*par_ci,c({Number(Value(risk, "from"))},{Number(Value(risk, "to"))})))";
                    string text = RunScript("MiscRetroRisk-" + (exposure?.ToString(inv) ?? "controls") + "-" + confidence, Script(run), assertion, 0);
                    Check(Regex.Matches(text, "(?m)^Population exposure %").Count == 1 && text.Contains(exposure.HasValue ? "(as entered)" : "(exposure among the controls)"), "retrospective risk uses one selected population-exposure scenario");
                }
            {
                var (run, _) = Run("MiscRetroRisk", new() { ["scrap"] = new double[] { 49, 255, 46, 93 }, ["cco"] = .95 });
                string text = RunScript("MiscRetroRisk-protective", Script(run), "stopifnot(odds<1,!exists('par_ci'))", 0);
                Check(!text.Contains("Population exposure %"), "retrospective risk omits the attributable fraction when the odds ratio is below one");
            }
            foreach (string operation in new[] { "MiscDiagnostic", "ExactORCML" })
                foreach (double confidence in new[] { .8, .99 })
                {
                    var (run, _) = Run(operation, new() { ["scrap"] = new double[] { 40, 10, 15, 60 }, [operation == "ExactORCML" ? "gamma" : "cco"] = confidence });
                    string script = Script(run);
                    string text = RunScript(operation + "-standard-level-" + confidence, script, "stopifnot(is.finite(cmle))", 0);
                    Check(text.Contains((100 * confidence).ToString(inv) + " percent confidence interval:") && !text.Contains("95 percent confidence interval:"), operation + " standard R output uses the same selected confidence level as its precise inference");
                }
        }
        finally { MetaExact = earlierExact; }

        // Existing independent worked examples still compare every printed figure.
        // Here verify the simplified regression CI extraction against direct lm algebra.
        foreach (string operation in new[] { "Effect", "MetaSummary", "RelativeRiskMeta" })
        {
            string example = File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".json"));
            string script = RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(example).RootElement)).RootElement, null, null).Text;
            string fitName = operation == "Effect" ? "egger" : operation == "MetaSummary" ? "fit" : "NULL";
            string assertion = fitName != "NULL"
                ? $"fit_check <- {fitName}\nse_check <- sqrt(vcov(fit_check)[1,1])\nstopifnot(near(confint(fit_check,level=.9)[1,],coef(fit_check)[1]+c(-1,1)*qt(.95,df.residual(fit_check))*se_check))"
                : "test_fit <- lm(I(lrr/se_i)~I(1/se_i))\nse_check <- sqrt(vcov(test_fit)[1,1])\nstopifnot(near(confint(test_fit,level=.9)[1,],coef(test_fit)[1]+c(-1,1)*qt(.95,df.residual(test_fit))*se_check))";
            RunScript(operation + "-standard-lm", script, assertion, 3);
        }

        foreach (string operation in new[] { "Mantel", "MantelHaenszelScreen", "MetaIncidenceRateDifference", "MetaIncidenceRateRatio", "ProportionMeta", "MiscRetroRisk" })
        {
            string example = File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".json"));
            string script = RScriptWriter.Write(JsonDocument.Parse(RecordOf(JsonDocument.Parse(example).RootElement)).RootElement, null, null).Text;
            string selected = RunR(script, Path.Combine(folder, operation + "-worked-selected"));
            string reference = RunR(File.ReadAllText(Path.Combine(Metadata("Examples"), operation + ".R")), Path.Combine(folder, operation + "-worked-independent"));
            Check(Figures(selected) == Figures(reference), operation + " preserves every selected worked-example figure against its independent reference");
        }
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckPosthocMeta537(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        bool savedPreference = MetaExact;
        try
        {
            CheckPosthocRiskDifference537(Path.Combine(folder, "risk-difference"));
            CheckPosthocMetaMethods537(Path.Combine(folder, "meta-methods"));
        }
        finally { MetaExact = savedPreference; }
    }

    // Numeric acquisition uses the same cell conversion and origin handling as
    // the worksheet. The operation and saved record are produced by the engine.
    private static DataFrame PosthocMetaFrame537(params double[][] columns)
    {
        RecordCells cells = new(columns.Select(column => column.Cast<object>().ToArray()).ToArray());
        DataFrame frame = new();
        for (int i = 0; i < columns.Length; i++)
            frame.Variables.Add(cells.Acquire(i, 0, columns[i].Length, DataAcquisitionMode.NumericReplaceMissing).Variables[0]);
        return frame;
    }

    private static string PosthocMetaEqual537(string expression, IEnumerable<double> values, double tolerance = 2e-7)
    {
        string vector = RScriptWriter.Vector(values.Select(value => RunRecord.Missing(value) ? null : (object)value).ToArray());
        return DeferredEqual538("unname(c(" + expression + "))", "as.numeric(" + vector + ")", tolerance);
    }

    private static string PosthocStudyLimits537(ParameterBag bag, string key, string limits)
    {
        var rows = (IList<ParameterBag>)bag[key].AsObject;
        return PosthocMetaEqual537(limits, rows.Select(row => DeferredNumber538(row, "lci"))
            .Concat(rows.Select(row => DeferredNumber538(row, "uci"))));
    }

    private static string VerifyPosthocMeta537(JsonElement record, string assertions, string folder, bool noWarnings = false)
    {
        RScript script = RScriptWriter.Write(record, null, null);
        Check(script.HasRecipe && !script.DataUnavailable, Path.GetFileName(folder) + " records the selected data and method");
        string output = DeferredR538((noWarnings ? "options(warn=2)\n" : "") + script.Text + "\n" + assertions +
            "\ncat('POSTHOC_META_OK\\n')\n", folder);
        Check(output.Contains("POSTHOC_META_OK"), Path.GetFileName(folder) + " agrees with the native and R interval references");
        return output;
    }

    private static void CheckPosthocRiskDifference537(string folder)
    {
        double[] n1 = [20, 20, 50, 50, 10, 100, 200, 16];
        double[] a = [8, 16, 13, 30, 4, 50, 100, 12];
        double[] n2 = [20, 20, 50, 50, 10, 100, 200, 8];
        double[] b = [12, 4, 37, 20, 6, 50, 100, 3];
        MetaExact = true;
        foreach (double confidence in new[] { .8, .9, .95, .99 })
        {
            var (run, output) = Run("RiskDifference", new()
            {
                ["gamma"] = confidence, ["strata"] = null,
                ["sn"] = PosthocMetaFrame537(n1), ["sr"] = PosthocMetaFrame537(a),
                ["xn"] = PosthocMetaFrame537(n2), ["xr"] = PosthocMetaFrame537(b)
            });
            // Independently estimate the constrained proportions by solving the
            // binomial likelihood derivative, avoiding the recipe's cubic.
            string assertion = PosthocStudyLimits537(output.ParameterBag, "*differences", "ci[,1],ci[,2]") +
                """
                mn_reference <- function(a,n1,b,n2) {
                  est <- a/n1-b/n2
                  score <- function(delta) {
                    if (delta == est) return(0)
                    slope <- function(p2) {
                      p1 <- p2+delta
                      a/p1-(n1-a)/(1-p1)+b/p2-(n2-b)/(1-p2)
                    }
                    p2 <- uniroot(slope,c(max(0,-delta)+1e-12,min(1,1-delta)-1e-12),tol=1e-14)$root
                    p1 <- p2+delta
                    variance <- (p1*(1-p1)/n1+p2*(1-p2)/n2)*(n1+n2)/(n1+n2-1)
                    (est-delta)/sqrt(variance)
                  }
                  c(uniroot(function(d) score(d)-z,c(-1+1e-8,est),tol=1e-12)$root,
                    uniroot(function(d) score(d)+z,c(est,1-1e-8),tol=1e-12)$root)
                }
                reference <- t(mapply(mn_reference,a,n1,b,n2))
                stopifnot(max(abs(ci-reference)) < 1e-9)
                """;
            VerifyPosthocMeta537(Record(run), assertion, Path.Combine(folder, "symmetric-and-unequal-" + confidence.ToString("R", inv)), noWarnings: true);
        }

        // Test the two finite boundary endpoints directly, independently of
        // the unrelated pooling/weight handling for studies with empty cells.
        string recipe = RRecipes.Recipe("risk-difference.R");
        int start = recipe.IndexOf("mn_limits <- function", StringComparison.Ordinal);
        int end = recipe.IndexOf("if (isTRUE(exact_meta))", start, StringComparison.Ordinal);
        string limits = recipe[start..end];
        foreach (double confidence in new[] { .8, .95, .99 })
        {
            double critical = PDF.gauinv((1 + confidence) / 2);
            MathDbl.uppci(10, 10, 0, 10, out double l1, out double u1, critical, confidence * 100);
            MathDbl.uppci(0, 10, 10, 10, out double l2, out double u2, critical, confidence * 100);
            MathDbl.uppci(20, 20, 0, 3, out double l3, out double u3, critical, confidence * 100);
            MathDbl.uppci(0, 7, 21, 21, out double l4, out double u4, critical, confidence * 100);
            string result = DeferredR538("options(warn=2)\nz <- qnorm(" + ((1 + confidence) / 2).ToString("R", inv) + ")\n" + limits +
                PosthocMetaEqual537("mn_limits(10,10,0,10),mn_limits(0,10,10,10),mn_limits(20,20,0,3),mn_limits(0,7,21,21)",
                    new[] { l1, u1, l2, u2, l3, u3, l4, u4 }) +
                "cat('POSTHOC_BOUNDARY_OK\\n')\n", Path.Combine(folder, "endpoints-" + confidence.ToString("R", inv)));
            Check(result.Contains("POSTHOC_BOUNDARY_OK"), "risk difference score interval has the native finite endpoints at " + confidence);
        }
    }

    private static void CheckPosthocMetaMethods537(string folder)
    {
        double[] sn = [615, 758, 832, 317, 810, 2267, 8587];
        double[] sr = [49, 44, 102, 32, 85, 246, 1570];
        double[] xn = [624, 771, 850, 309, 406, 2257, 8600];
        double[] xr = [67, 64, 126, 38, 52, 219, 1720];
        double[] left = sr.SelectMany((events, i) => new[] { events, sn[i] - events }).ToArray();
        double[] right = xr.SelectMany((events, i) => new[] { events, xn[i] - events }).ToArray();
        Host host = new(new());
        foreach (string variant in new[] { "Mantel", "MantelHaenszelScreen", "Effect-d", "Effect-m", "MetaCorrelation", "ProportionMeta" })
        foreach (double confidence in new[] { .8, .95, .99 })
        {
            string operation = variant.StartsWith("Effect-", StringComparison.Ordinal) ? "Effect" : variant;
            bool screen = operation == "MantelHaenszelScreen";
            bool odds = operation == "Mantel" || screen;
            string setting = screen ? "try_exact" : "metaExact";
            int studies = operation == "MetaCorrelation" ? 6 : operation == "ProportionMeta" ? 8 : 7;
            ParameterBag exactReference = null;
            foreach (bool exact in new[] { true, false })
            {
                // The typed-in screen's answer takes precedence over the app preference.
                MetaExact = screen ? !exact : exact;
                Dictionary<string, object> answers = operation switch
                {
                    "Mantel" => new()
                    {
                        ["gamma"] = confidence, ["strata"] = null, ["sn"] = PosthocMetaFrame537(sn), ["sr"] = PosthocMetaFrame537(sr),
                        ["xn"] = PosthocMetaFrame537(xn), ["xr"] = PosthocMetaFrame537(xr)
                    },
                    "MantelHaenszelScreen" => new()
                    {
                        ["cco"] = confidence, ["try_exact"] = exact, ["plot_forest"] = false, ["data"] = PosthocMetaFrame537(left, right)
                    },
                    "Effect" => new()
                    {
                        ["gamma"] = confidence, ["type"] = variant[^1].ToString(), ["strata"] = null, ["g"] = null,
                        ["en"] = PosthocMetaFrame537([27, 16, 25, 62, 9, 63, 23]),
                        ["em"] = PosthocMetaFrame537([18.5, 60, 10.72, 9.2, 3.75, 60, 9.24]),
                        ["es"] = PosthocMetaFrame537([14.9, 29.2, 6.46, 6.16, 2.55, 13.3, 5.35]),
                        ["cn"] = PosthocMetaFrame537([17, 15, 66, 66, 22, 40, 23]),
                        ["cm"] = PosthocMetaFrame537([5.4, 55, 6.92, 6.92, 1.05, 46.3, 5.33]),
                        ["cs"] = PosthocMetaFrame537([17.3, 29.2, 6.83, 6.83, 2.12, 18.6, 4.48])
                    },
                    "MetaCorrelation" => new()
                    {
                        ["gamma"] = confidence, ["studies"] = null,
                        ["r"] = PosthocMetaFrame537([.3, .52, .1, .45, .21, .60]), ["n"] = PosthocMetaFrame537([50, 80, 40, 120, 60, 90])
                    },
                    _ => new()
                    {
                        ["gamma"] = confidence, ["method"] = "arcsine", ["strata"] = null,
                        ["sn"] = PosthocMetaFrame537([311, 65, 67, 285, 73, 116, 183, 696]),
                        ["sr"] = PosthocMetaFrame537([214, 58, 59, 182, 65, 66, 99, 600])
                    }
                };
                var (run, output) = Run(operation, answers);
                ParameterBag bag = output.ParameterBag;
                if (exact) exactReference = bag;
                string label = variant + "-" + exact + "-" + confidence.ToString("R", inv);
                Check(Record(run).GetProperty("parameters").GetProperty(setting).GetBoolean() == exact &&
                    bag[setting].Direction == FilledParameterDirection.Input, label + " records the method used by the native operation");
                MetaExact = !MetaExact;
                JsonElement replay = JsonDocument.Parse(RunRecord.Write(run, bag)).RootElement.Clone();
                Check(replay.GetProperty("parameters").GetProperty(setting).GetBoolean() == exact,
                    label + " keeps the recorded method when the host preference changes");
                string mode = exact ? "TRUE" : "FALSE";
                string assertion = "stopifnot(identical(exact_meta," + mode + "))\n" +
                    PosthocMetaEqual537("i2,i2_ci", new[] { "isq", "llisq", "ulisq" }.Select(key => DeferredNumber538(bag, key)));
                if (odds)
                {
                    assertion += PosthocStudyLimits537(bag, "*or", "lci,uci") +
                        "stopifnot(exists('cml',inherits=FALSE)==" + mode + ")\n";
                    if (!exact)
                        assertion += "stopifnot(max(abs(lci-exp(log(a*d/(b*c))-z*sqrt(1/a+1/b+1/c+1/d))))<1e-10,\n" +
                            "max(abs(uci-exp(log(a*d/(b*c))+z*sqrt(1/a+1/b+1/c+1/d))))<1e-10)\n";
                }
                string result = VerifyPosthocMeta537(replay, assertion, Path.Combine(folder, label));
                if (odds)
                    Check(result.Contains("CI (" + (exact ? "CML" : "logit") + ")") && result.Contains("Exact mid-P") == exact,
                        label + " labels the selected study limits and prints the pooled exact block only when selected");

                if (!exact && confidence == .95)
                {
                    JsonObject legacy = JsonNode.Parse(replay.GetRawText()).AsObject();
                    legacy["parameters"].AsObject().Remove(setting);
                    double q = DeferredNumber538(bag, "qc");
                    Meta.IsquareNcc(host, q, studies, confidence, PDF.gauinv((1 + confidence) / 2),
                        out double point, out double lower, out double upper, exact: true);
                    string legacyAssertion = "stopifnot(isTRUE(exact_meta))\n" + PosthocMetaEqual537("i2,i2_ci", new[] { point, lower, upper });
                    if (odds) legacyAssertion += PosthocStudyLimits537(exactReference, "*or", "lci,uci") + "stopifnot(exists('cml',inherits=FALSE))\n";
                    VerifyPosthocMeta537(JsonDocument.Parse(legacy.ToJsonString()).RootElement.Clone(), legacyAssertion, Path.Combine(folder, label + "-legacy"));
                }
            }
        }
    }
}

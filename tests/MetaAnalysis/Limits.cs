// The Meta-analysis menu at its limits: what R gives no figure for.  Studies that cannot be pooled, the labels of the studies that are
// left out, and the lower limit of Sato when the pooled odds ratio is infinite, which is worked out here from its definition.
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void Refused(string title, string analysis, double[] r1, double[] n1, double[] r2, double[] n2, string words)
    {
        var figures = Report("x", h => Binary(analysis, h, 0.95, r1, n1, r2, n2));
        string error = figures.TryGetValue("x|error", out object e) ? (string)e : "no error";
        Say(error.StartsWith("TemplateOperationCancelledException") && error.Contains(words), title + $": refused with a message that has \"{words}\" ({error})");
    }

    private static void Limits()
    {
        Console.WriteLine();
        Console.WriteLine("At the limits");
        int before = failures;
        Set(settings[0]);

        // no study can be pooled
        double[] none = { 0, 0, 0 }, n1 = { 7, 12, 12 }, n2 = { 11, 9, 8 };
        foreach (string analysis in new[] { "or", "peto", "rr" })
            Refused($"no events in any study, {analysis}", analysis, none, n1, none, n2, "None of the studies can be pooled");
        Refused("every group of nobody, risk difference", "rd", new double[] { 0, 0 }, new double[] { 0, 0 }, new double[] { 1, 2 }, new double[] { 5, 6 }, "nothing to pool");
        // the risk difference of studies without events is nothing, and they are pooled
        var rd = Report("x", h => Binary("rd", h, 0.95, none, n1, none, n2));
        Say(rd.TryGetValue("x|rmh", out object pooled) && pooled is double zero && zero == 0, "no events in any study, risk difference: a pooled difference of nothing");

        // the labels of the studies that are left out
        double[] r1 = { 20, 33, 21, 0, 15, 0, 9 }, m1 = { 64, 42, 108, 0, 42, 30, 9 }, r2 = { 38, 95, 14, 3, 16, 0, 12 }, m2 = { 75, 130, 33, 10, 34, 25, 12 };
        foreach (var (analysis, block, excluded) in new[] { ("or", "or", new[] { 4, 6, 7 }), ("peto", "odds", new[] { 4, 6, 7 }), ("rr", "risks", new[] { 4, 6 }), ("rd", "differences", new[] { 4 }) })
        {
            var figures = Report("x", h => Binary(analysis, h, 0.95, r1, m1, r2, m2));
            for (int i = 1; i <= 7; i++)
            {
                string label = figures.TryGetValue($"x|{block}.{i}|lb", out object l) ? (string)l : "no label";
                bool marked = label.Contains("excluded");
                Say(marked == excluded.Contains(i), $"{analysis}, study {i} ({r1[i - 1]} of {m1[i - 1]} against {r2[i - 1]} of {m2[i - 1]}): {(excluded.Contains(i) ? "marked as excluded" : "not marked as excluded")} (\"{label}\")");
            }
        }

        // Sato's lower limit: no events in the second group of any study, the continuity correction delayed.  The limits are the odds
        // ratios psi for which (R - psi S)^2 is no more than z^2 psi W; with S nothing, those from R^2 / (z^2 W) up
        {
            Set(("x0.cc5.d1", false, 0.5, true));
            double[] a = { 5, 2, 6, 3, 10, 3 }, g1 = { 18, 14, 17, 12, 66, 16 }, b = { 0, 0, 0, 0, 0, 0 }, g2 = { 33, 35, 21, 213, 57, 14 };
            double sumR = 0, sumW = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double c = g1[i] - a[i], d = g2[i] - b[i], n = g1[i] + g2[i];
                sumR += a[i] * d / n;
                sumW += ((b[i] + c) / n + 1 / n) * a[i] * d / n + ((a[i] + d) / n + 1 / n) * b[i] * c / n;
            }
            const double z = 1.959963984540054;
            var figures = Report("x", h => Binary("or", h, 0.95, a, g1, b, g2));
            double from = figures.TryGetValue("x|from", out object f) && f is double lower ? lower : double.NaN;
            double expected = sumR * sumR / (z * z * sumW);
            Say(Math.Abs(from - expected) <= 1e-9 * expected, $"no events in the second group, the correction delayed: Sato's lower limit ({from}; from the definition {expected})");
            Say(figures.TryGetValue("x|to", out object t) && t is double upper && double.IsPositiveInfinity(upper), "and no upper limit");
            Say(figures.TryGetValue("x|odds", out object o) && o is string, "and a pooled odds ratio that is not a number");
        }
        // The bias assessment plot takes the standard error of each study from the limits that it is handed: with the exact methods
        // they must be the estimate plus and minus z standard errors all the same, the standard error being that of the weights
        {
            Set(settings[0]);
            const double z = 1.959963984540054;
            double[] a = { 12, 5, 30, 9, 41 }, g1 = { 80, 61, 150, 44, 310 }, b = { 7, 9, 21, 3, 52 }, g2 = { 75, 66, 160, 50, 290 };
            var (lower, upper) = PlotLimits(Binary("rd", new Host(), 0.95, a, g1, b, g2));
            for (int i = 0; i < a.Length; i++)
            {
                double se = Math.Sqrt(a[i] * (g1[i] - a[i]) / Math.Pow(g1[i], 3) + b[i] * (g2[i] - b[i]) / Math.Pow(g2[i], 3));
                double given = lower == null ? double.NaN : (upper[i + 1] - lower[i + 1]) / (2 * z);
                Say(Math.Abs(given - se) <= 1e-9 * se, $"risk difference, exact methods, study {i + 1}: the standard error of the bias assessment plot ({given}; from the definition {se})");
            }
            double[] t1 = { 1510, 949, 2210, 870, 4020 }, t2 = { 1701, 2245, 1980, 905, 3790 };
            foreach (bool ratio in new[] { true, false })
            {
                ParameterBag bag = new();
                bag.AddInput("gamma", 0.95);
                bag.AddInput("a", Column("a", a)); bag.AddInput("pt1", Column("pt1", t1));
                bag.AddInput("b", Column("b", b)); bag.AddInput("pt2", Column("pt2", t2));
                (lower, upper) = PlotLimits(ratio ? Meta.RptMetaIncidenceRateRatio(new Host(), bag) : Meta.RptMetaIncidenceRateDifference(new Host(), bag));
                for (int i = 0; i < a.Length; i++)
                {
                    double se = ratio ? Math.Sqrt(1 / a[i] + 1 / b[i]) : Math.Sqrt(a[i] / (t1[i] * t1[i]) + b[i] / (t2[i] * t2[i]));
                    double given = lower == null ? double.NaN : ratio ? (Math.Log(upper[i + 1]) - Math.Log(lower[i + 1])) / (2 * z) : (upper[i + 1] - lower[i + 1]) / (2 * z);
                    Say(Math.Abs(given - se) <= 1e-9 * se, $"rate {(ratio ? "ratio" : "difference")}, exact methods, study {i + 1}: the standard error of the bias assessment plot ({given}; from the definition {se})");
                }
            }
        }
        // Incidence rates, studies without events.  One without an event in either group is left out; one without an event in one of
        // its groups has the continuity correction in the events of both groups, which is a half unless a number is set
        {
            Set(settings[0]);
            const double z = 1.959963984540054;
            double[] nothing = { 0, 0, 0 }, time1 = { 120, 90, 300 }, time2 = { 110, 95, 280 };
            foreach (bool ratio in new[] { true, false })
            {
                var refused = Report("x", h => Rates(ratio, h, 0.95, nothing, time1, nothing, time2));
                string error = refused.TryGetValue("x|error", out object e) ? (string)e : "no error";
                Say(error.StartsWith("TemplateOperationCancelledException") && error.Contains("None of the studies can be pooled"), $"no events in any study, rate {(ratio ? "ratio" : "difference")}: refused with a message that says so ({error})");
            }
            double[] a = { 0, 5, 0, 7 }, t1 = { 100, 120, 90, 150 }, b = { 0, 3, 4, 0 }, t2 = { 110, 100, 95, 160 };
            foreach (double set in new[] { -9, 0.25 })
            {
                Set(("x1.cc.d0", true, set, false));
                double cc = set > 0 ? set : 0.5;
                string[] labels = { "* (excluded)", "", " [CC = " + cc.ToString() + "]", " [CC = " + cc.ToString() + "]" };
                double[] ac = a.Select((v, i) => a[i] == 0 || b[i] == 0 ? v + cc : v).ToArray(), bc = b.Select((v, i) => a[i] == 0 || b[i] == 0 ? v + cc : v).ToArray();
                foreach (bool ratio in new[] { true, false })
                {
                    string what = $"rate {(ratio ? "ratio" : "difference")}, correction {(set > 0 ? "set to 0.25" : "not set")}";
                    var figures = Report("x", h => Rates(ratio, h, 0.95, a, t1, b, t2));
                    double figure(string key) => figures.TryGetValue(key, out object v) && v is double d ? d : double.NaN;
                    double total = 0;
                    double[] weight = new double[4];
                    for (int i = 1; i < 4; i++)
                    {
                        weight[i] = ratio ? 1 / (1 / ac[i] + 1 / bc[i]) : 1 / (ac[i] / (t1[i] * t1[i]) + bc[i] / (t2[i] * t2[i]));
                        total += weight[i];
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        string label = figures.TryGetValue($"x|inputs.{i + 1}|lb", out object l) ? (string)l : "no label";
                        Say(label == labels[i], $"{what}, study {i + 1} ({a[i]} and {b[i]} events): the label \"{labels[i]}\" (\"{label}\")");
                        if (i == 0) continue;
                        double estimate = ratio ? ac[i] / t1[i] / (bc[i] / t2[i]) : a[i] / t1[i] - b[i] / t2[i];
                        double given = figure($"x|ir.{i + 1}|{(ratio ? "irr" : "ird")}");
                        Say(Math.Abs(given - estimate) <= 1e-12 * Math.Max(1, Math.Abs(estimate)), $"{what}, study {i + 1}: the rate {(ratio ? "ratio" : "difference")} ({given}; from the definition {estimate})");
                        given = figure($"x|ir.{i + 1}|wt");
                        Say(Math.Abs(given - 100 * weight[i] / total) <= 1e-9, $"{what}, study {i + 1}: the fixed effects weight ({given}; from the definition {100 * weight[i] / total})");
                        if (!ratio)
                        {
                            double se = Math.Sqrt(1 / weight[i]);
                            Say(Math.Abs(figure($"x|ir.{i + 1}|lci") - (estimate - z * se)) <= 1e-12 && Math.Abs(figure($"x|ir.{i + 1}|uci") - (estimate + z * se)) <= 1e-12, $"{what}, study {i + 1}: the limits are the difference plus and minus z standard errors");
                        }
                    }
                }
            }
            Set(settings[0]);
        }
        if (failures == before) Console.WriteLine("ok    studies that cannot be pooled, the labels of the studies left out, Sato's limit, the standard errors of the bias plots, rates without events");
    }

    internal static StepOutput Rates(bool ratio, Host h, double level, double[] a, double[] t1, double[] b, double[] t2)
    {
        ParameterBag bag = new();
        bag.AddInput("gamma", level);
        bag.AddInput("a", Column("a", a)); bag.AddInput("pt1", Column("pt1", t1));
        bag.AddInput("b", Column("b", b)); bag.AddInput("pt2", Column("pt2", t2));
        return ratio ? Meta.RptMetaIncidenceRateRatio(h, bag) : Meta.RptMetaIncidenceRateDifference(h, bag);
    }

    // the limits that a report hands to its bias assessment plot, a place for each study from 1
    private static (double[] lower, double[] upper) PlotLimits(StepOutput output)
    {
        foreach (ParameterBag row in Rows(output.ParameterBag, "*chart"))
            if (row["chart"].AsObject is StatsDirect.Charting.ChartDefinition { ChartOptions: StatsDirect.Charting.BiasMAOptions options })
                return (options.cl, options.cu);
        return (null, null);
    }
}

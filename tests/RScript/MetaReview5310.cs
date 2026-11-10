using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.Charting;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // StatsDirect 5.3.10, meta-analysis: the incidence-rate replays complete when a stratum has no events in one group and give
    // the report's corrected figures, its exact block only when the exact methods were on, its I-squared interval and its bias
    // indicators; the continuity correction chosen in the calculation options is the one applied and shown in a culture with a
    // decimal comma; a study whose label is blank has its place among the studies kept in the replay as in the report; the
    // forest plots' axis gives the level of the analysis; and Begg's test has the report's tie handling and its low power note.
    // Every run goes through the real cell acquisition, the real template run and its saved record, and R.
    // One part can be checked alone: "rates", "correction", "labels", "axis" or "bias".
    private static void CheckMetaReview5310(string folder, string part = null)
    {
        RRecipes.Folder = Metadata("Recipes");
        bool exact = MetaExact, delay = DelayContinuityCorrection;
        double correction = MetaCC;
        try
        {
            if (part is null or "rates") CheckMeta5310IncidenceRates(Path.Combine(folder, "rates"));
            if (part is null or "correction") CheckMeta5310CorrectionCulture(Path.Combine(folder, "correction"));
            if (part is null or "labels") CheckMeta5310BlankLabels(Path.Combine(folder, "labels"));
            if (part is null or "axis") CheckMeta5310AxisLabels(Path.Combine(folder, "axis"));
            if (part is null or "bias") CheckMeta5310RankCorrelation(Path.Combine(folder, "bias"));
            if (part is null or "round-two") CheckRound2Meta(Path.Combine(folder, "round-two"));
        }
        finally { MetaExact = exact; MetaCC = correction; DelayContinuityCorrection = delay; }
    }

    // Numeric columns acquired from one sheet of cells, as the worksheet gives them
    private static (RecordCells cells, DataFrame[] frames) Meta5310Sheet(params double[][] columns)
    {
        var cells = new RecordCells(columns.Select(c => c.Cast<object>().ToArray()).ToArray());
        return (cells, columns.Select((c, i) => Meta539Frame(cells, i, c.Length)).ToArray());
    }

    private static string Meta5310Strings(IEnumerable<string> values) =>
        "c(" + string.Join(", ", values.Select(v => "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + ")";

    // twelve studies for each kind of meta-analysis: fourfold tables (events and total of each group, which also serve as events
    // and person-time), two groups' sizes, means and standard deviations, estimates with standard errors, correlations with
    // sample sizes, and responders among totals
    private static readonly double[][] Meta5310Tables =
    {
        new double[] { 20, 100, 12, 100 }, new double[] { 35, 150, 20, 140 }, new double[] { 10, 80, 12, 90 }, new double[] { 60, 200, 30, 210 },
        new double[] { 50, 120, 15, 115 }, new double[] { 23, 110, 18, 125 }, new double[] { 14, 95, 9, 90 }, new double[] { 41, 180, 33, 175 },
        new double[] { 8, 60, 11, 64 }, new double[] { 27, 130, 16, 128 }, new double[] { 19, 75, 22, 82 }, new double[] { 31, 160, 14, 150 }
    };

    // The answers of one of the meta-analyses for the chosen studies, unlabelled, and the cells they were acquired from
    private static Dictionary<string, object> Meta5310Answers(string operation, int[] take, double confidence, out RecordCells cells)
    {
        double[] en = { 27, 16, 25, 62, 9, 63, 30, 22, 45, 18, 36, 51 }, em = { 18.5, 60, 10.72, 9.2, 3.75, 60, 12.1, 33.4, 8.9, 15.2, 22.8, 41.5 };
        double[] es = { 14.9, 29.2, 6.46, 6.16, 2.55, 13.3, 7.7, 12.5, 4.1, 6.6, 9.3, 15.8 }, cn = { 17, 15, 66, 66, 22, 40, 28, 25, 43, 20, 33, 49 };
        double[] cm = { 5.4, 55, 6.92, 6.92, 1.05, 46.3, 9.8, 29.9, 7.5, 13.1, 18.4, 36.2 }, cs = { 17.3, 29.2, 6.83, 6.83, 2.12, 18.6, 8.1, 11.9, 4.4, 7.0, 9.9, 14.6 };
        double[] y = { 1.5, .8, 2, 1.15, 2.6, 1.3, 1.7, .95, 1.4, 2.2, 1.05, 1.9 }, se = { .12, .18, .21, .15, .25, .2, .14, .22, .17, .19, .11, .24 };
        double[] r = { .3, .52, .1, .45, .21, .6, .38, .15, .48, .27, .55, .33 }, n = { 50, 80, 40, 120, 60, 90, 70, 45, 110, 55, 95, 65 };
        double[] total = { 311, 65, 67, 285, 73, 116, 150, 92, 204, 58, 133, 176 }, responders = { 214, 58, 59, 182, 65, 66, 101, 70, 150, 39, 88, 121 };
        double[] Pick(double[] values) => take.Select(i => values[i]).ToArray();
        double[] Cell(int j) => take.Select(i => Meta5310Tables[i][j]).ToArray();
        Dictionary<string, object> answers = new() { ["gamma"] = confidence };
        DataFrame[] f;
        switch (operation)
        {
            case "MantelHaenszelScreen":
                (cells, f) = Meta5310Sheet(take.SelectMany(i => new[] { Meta5310Tables[i][0], Meta5310Tables[i][1] - Meta5310Tables[i][0] }).ToArray(),
                    take.SelectMany(i => new[] { Meta5310Tables[i][2], Meta5310Tables[i][3] - Meta5310Tables[i][2] }).ToArray());
                DataFrame data = new();
                data.Variables.Add(f[0].Variables[0]); data.Variables.Add(f[1].Variables[0]);
                answers = new() { ["cco"] = confidence, ["try_exact"] = false, ["plot_forest"] = false, ["data"] = data };
                break;
            case "Effect":
                (cells, f) = Meta5310Sheet(Pick(en), Pick(em), Pick(es), Pick(cn), Pick(cm), Pick(cs));
                answers["type"] = "d"; answers["strata"] = null; answers["g"] = null;
                answers["en"] = f[0]; answers["em"] = f[1]; answers["es"] = f[2]; answers["cn"] = f[3]; answers["cm"] = f[4]; answers["cs"] = f[5];
                break;
            case "MetaSummary":
                (cells, f) = Meta5310Sheet(Pick(y), Pick(se));
                answers["type"] = "oddsRatio"; answers["use_ci"] = "false"; answers["studies"] = null; answers["y"] = f[0]; answers["se_y"] = f[1];
                break;
            case "MetaCorrelation":
                (cells, f) = Meta5310Sheet(Pick(r), Pick(n));
                answers["studies"] = null; answers["r"] = f[0]; answers["n"] = f[1];
                break;
            case "ProportionMeta":
                (cells, f) = Meta5310Sheet(Pick(total), Pick(responders));
                answers["method"] = "arcsine"; answers["strata"] = null; answers["sn"] = f[0]; answers["sr"] = f[1];
                break;
            case "MetaIncidenceRateRatio":
            case "MetaIncidenceRateDifference":
                (cells, f) = Meta5310Sheet(Cell(1), Cell(0), Cell(3), Cell(2));
                answers["strata"] = null; answers["pt1"] = f[0]; answers["a"] = f[1]; answers["pt2"] = f[2]; answers["b"] = f[3];
                break;
            default:
                (cells, f) = Meta5310Sheet(Cell(0), Cell(1), Cell(2), Cell(3));
                answers["strata"] = null; answers["sr"] = f[0]; answers["sn"] = f[1]; answers["xr"] = f[2]; answers["xn"] = f[3];
                break;
        }
        return answers;
    }

    // The checks of an incidence-rate replay against the native output bag of its analysis: each stratum's estimate, limits,
    // effect, variance and weights, the pooled estimates, the heterogeneity figures with the I-squared interval, the exact
    // conditional block when the report has it, the bias indicators when the report gives them, and the stratum labels
    private static string Meta5310RateAssertions(bool ratio, bool labelled, ParameterBag bag)
    {
        StringBuilder s = new();
        void Same(string what, string expression, IEnumerable<double> expected, double tolerance = 2e-7) =>
            s.Append(Meta539Compare(what, expression, expected, tolerance));
        IEnumerable<double> Column(string key) => Meta539Rows(bag, "*ir").Select(row => Meta539Number(row, key));
        IEnumerable<double> Figures(params string[] keys) => keys.Select(key => Meta539Number(bag, key));
        string res = ratio ? "res_r" : "res";
        if (ratio)
        {
            Same("rate ratios", "irr", Column("irr"));
            Same("exact limits of the strata", "exact[1, ], exact[2, ]", Column("lci").Concat(Column("uci")));
            Same("log rate ratios and their variances", "log(irr), v_log", Column("yi").Concat(Column("vi")));
            Same("pooled rate ratio", "exp(res_r$fixed), exp(res_r$fixed + c(-1, 1) * z * res_r$se), res_r$fixed / res_r$se, 2 * pnorm(-abs(res_r$fixed / res_r$se))",
                Figures("rmh", "from", "to", "z", "p_z"));
            Same("random effects rate ratio", "exp(res_r$random), exp(res_r$random + c(-1, 1) * z * res_r$se_r), res_r$random / res_r$se_r, 2 * pnorm(-abs(res_r$random / res_r$se_r))",
                Figures("dsirr", "dsll", "dsul", "dz", "dp"));
            IList<ParameterBag> conditional = Meta539Rows(bag, "*poolok");
            if (conditional.Count == 1)
            {
                Same("conditional maximum likelihood", "cmle, fisher, mid", new[] { "eor", "llf", "ulf", "llm", "ulm" }.Select(key => Meta539Number(conditional[0], key)));
                Same("exact P values", "one_f, two_f, one_m, min(1, 2 * one_m)", new[] { "p1f", "p2f", "p1m", "p2m" }.Select(key => Meta539Number(conditional[0], key)));
            }
            else s.Append("if (exists('cmle', inherits = FALSE) || any(grepl('^Exact Fisher', printed))) stop('the replay gives the exact conditional block that the report does not have')\n");
        }
        else
        {
            Same("rate differences", "ird", Column("ird"));
            Same("limits of the strata", "ird - z * se_ird, ird + z * se_ird", Column("lci").Concat(Column("uci")));
            Same("rate differences and their variances", "ird, v_ird", Column("yi").Concat(Column("vi")));
            Same("pooled rate difference", "res$fixed, res$fixed + c(-1, 1) * z * res$se, res$fixed / res$se, 2 * pnorm(-abs(res$fixed / res$se))",
                Figures("rmh", "from", "to", "z", "p_z"));
            Same("random effects rate difference", "res$random, res$random + c(-1, 1) * z * res$se_r, res$random / res$se_r, 2 * pnorm(-abs(res$random / res$se_r))",
                Figures("dsird", "dsll", "dsul", "dz", "dp"));
        }
        Same("weights", res + "$fixed_pct, " + res + "$random_pct", Column("wt").Concat(Column("dwt")));
        Same("heterogeneity", res + "$q, pchisq(" + res + "$q, k - 1, lower.tail = FALSE), " + res + "$tau2", Figures("qc", "xp", "tausq"));
        Same("I-squared and its interval", "i2_figures", Figures("isq", "llisq", "ulisq"), 1e-6);
        s.Append("line <- grep('I2 (inconsistency)', printed, value = TRUE, fixed = TRUE)\n")
         .Append("if (length(line) != 1 || grepl('% CI = ', line, fixed = TRUE) != ").Append(Meta539Number(bag, "llisq") != Constant.MISSING ? "TRUE" : "FALSE")
         .Append(") stop('the printed I-squared line differs from the report: ', paste(line, collapse = ' | '))\n");
        // the bias indicators: none with three strata or fewer, otherwise as the report has them, Egger's interval at the bias tests' level
        if (Meta539Rows(bag, "*fewStrata").Count == 1)
            s.Append("if (!is.null(bias_figures) || any(grepl('^(Begg-Mazumdar|Egger): ', printed))) stop('the replay gives bias indicators that the report withholds')\n")
             .Append("if (!any(printed == 'Bias indicators need more than three studies with an estimate')) stop('the replay does not say why there are no bias indicators')\n");
        else
        {
            ParameterBag egger = Meta539Rows(bag, "*egger")[0];
            Same("Begg-Mazumdar", "bias_figures$begg", new[] { "tau", "p2" }.Select(key => Meta539Number(egger, key)));
            Same("Egger", "bias_figures$egger", new[] { "a", "cl", "cu", "p" }.Select(key => Meta539Number(egger, key)));
            s.Append("line <- grep('^Egger: bias = ', printed, value = TRUE)\n")
             .Append("expected <- paste0('(', ").Append(Meta539Value(Math.Round(Meta539Number(egger, "pc_egger"), 2))).Append(", '% CI = ', six(")
             .Append(Meta539Value(Meta539Number(egger, "cl"))).Append("), ' to ', six(").Append(Meta539Value(Meta539Number(egger, "cu"))).Append("), ')')\n")
             .Append("if (length(line) != 1 || !grepl(expected, line, fixed = TRUE)) stop('the printed Egger line differs: ', paste(line, collapse = ' | '), ' against ', expected)\n")
             .Append(Meta5310LowPower(egger));
        }
        // a stratum that is left out is marked, and so is one that has the continuity correction, beside its label
        s.Append("if (!identical(ifelse(included, ").Append(labelled ? "shown" : "note").Append(", note), ")
         .Append(Meta5310Strings(Meta539Rows(bag, "*inputs").Select(row => Convert.ToString(row["lb"].AsObject, inv).Trim())))
         .Append(")) stop('the stratum labels differ: ', paste(shown, collapse = ' | '))\n");
        return s.ToString();
    }

    // The printed Begg-Mazumdar line says "(low power)" exactly when the report does
    private static string Meta5310LowPower(ParameterBag indicators)
    {
        string note = indicators.TryGetValue("low_power_warn", out FilledParameter value) ? Convert.ToString(value?.AsObject, inv) : null;
        if (note == null) return "stop('the report has no low power note to compare with')\n";
        return "line <- grep('^Begg-Mazumdar: ', printed, value = TRUE)\n" +
            "if (length(line) != 1 || grepl('(low power)', line, fixed = TRUE) != " + (note.Contains("low power") ? "TRUE" : "FALSE") +
            ") stop('the report " + (note.Contains("low power") ? "notes" : "does not note") + " low power, unlike the replay: ', paste(line, collapse = ' | '))\n";
    }

    // Incidence-rate meta-analysis with a stratum that has no events in one group. The report adds the continuity correction of
    // the calculation options (the constant chosen if it is between 0 and 1, otherwise a half) to the events of both groups of
    // such a stratum; the replay used to stop at "missing value where TRUE/FALSE needed" (the rate ratio) or pool uncorrected
    // variances (the rate difference), always printed the exact conditional block, and had no I-squared interval and no bias
    // indicators. The record now carries the two options, and the replay must give the native figures under each of them, with
    // two, three and six strata, with a stratum that is left out, and from a record saved without the options.
    private static void CheckMeta5310IncidenceRates(string folder)
    {
        double[] pt1 = { 300, 410, 520, 150, 610, 205 }, a = { 0, 5, 9, 2, 12, 3 }, pt2 = { 310, 400, 505, 160, 590, 198 }, b = { 4, 3, 7, 0, 6, 0 };
        var sets = new (string name, double[][] columns, object[] labels)[]
        {
            // a text column's first cell is its title; the third stratum's label is blank
            ("six-labelled", new[] { pt1, a, pt2, b }, new object[] { "Age band", "A", "B", null, "D", "E", "F" }),
            ("three", new[] { pt1[..3], a[..3], pt2[..3], b[..3] }, null),
            ("two", new[] { pt1[..2], a[..2], pt2[..2], b[..2] }, null),
            // the fourth stratum has no events at all, so it is left out
            ("one-excluded", new[] { new double[] { 300, 410, 520, 100, 610 }, new double[] { 0, 5, 9, 0, 12 }, new double[] { 310, 400, 505, 100, 590 }, new double[] { 4, 3, 7, 0, 6 } },
                new object[] { "Age band", "A", "B", "C", "D", "E" })
        };
        var settings = new (bool exact, double cc, double level)[] { (true, -9, .95), (false, -9, .95), (true, .5, .9), (false, .01, .9), (true, .01, .99), (false, .5, .99) };
        string[] names = { "pt1", "a", "pt2", "b" };
        List<string> failures = new();
        void Expect(bool ok, string what) { if (ok) Check(true, what); else failures.Add(what + ": NOT SO"); }
        foreach (string operation in new[] { "MetaIncidenceRateRatio", "MetaIncidenceRateDifference" })
        {
            bool ratio = operation == "MetaIncidenceRateRatio";
            List<(string, string)> replays = new();
            foreach (var setting in settings)
            foreach (var (name, columns, labels) in sets)
            {
                MetaExact = setting.exact; MetaCC = setting.cc; DelayContinuityCorrection = false;
                string label = operation + "-" + name + "-" + (setting.exact ? "exact" : "approximate") + "-cc" + setting.cc.ToString(inv) + "-" + setting.level.ToString(inv);
                object[][] raw = columns.Select(c => c.Cast<object>().ToArray()).ToArray();
                var cells = new RecordCells(labels == null ? raw : raw.Append(labels).ToArray());
                Dictionary<string, object> answers = new() { ["gamma"] = setting.level, ["strata"] = labels == null ? null : cells.Acquire(4, 0, labels.Length, DataAcquisitionMode.Text) };
                for (int j = 0; j < 4; j++) answers[names[j]] = Meta539Frame(cells, j, columns[j].Length);
                if (name == "three" && setting == settings[0])
                {
                    var (_, _, refusals) = RunAtPicker(operation, answers);
                    Expect(refusals.Count == 0, operation + ": the picker accepts a stratum without an event in one group" + (refusals.Count > 0 ? " (it refused " + string.Join(", ", refusals) + ")" : ""));
                }
                OperationRun run; StepOutput native;
                try { (run, native) = Run(operation, answers); }
                catch (Exception ex)
                {
                    failures.Add(label + ": the native analysis refuses it (" + ex.GetBaseException().Message + ")");
                    continue;
                }
                ParameterBag bag = native.ParameterBag;
                double constant = setting.cc > 0 && setting.cc < 1 ? setting.cc : .5;
                string[] reported = Meta539Rows(bag, "*inputs").Select(row => Convert.ToString(row["lb"].AsObject, inv)).ToArray();
                Expect(reported.Count(l => l.EndsWith("[CC = " + constant.ToString(inv) + "]", StringComparison.Ordinal)) == (name == "six-labelled" ? 3 : 1),
                    label + ": the report marks each stratum without an event in one group with the correction " + constant.ToString(inv));
                if (ratio) Expect(Meta539Rows(bag, "*poolok").Count == 1 == setting.exact, label + ": the report has the exact conditional block exactly when the exact methods are on");
                JsonObject parameters = JsonNode.Parse(run.Record)["parameters"].AsObject();
                Expect(parameters.ContainsKey("metaExact") && parameters.ContainsKey("metaCC"), label + ": the record has the exact methods option and the continuity correction");
                RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
                Check(script.HasRecipe && !script.DataUnavailable, label + " replays the acquired strata");
                Expect(script.Text.Contains("exact_meta <- " + (setting.exact ? "TRUE" : "FALSE")) && script.Text.Contains("continuity <- " + setting.cc.ToString(inv)),
                    label + ": the script has the recorded options");
                string assertions = Meta5310RateAssertions(ratio, labels != null, bag);
                replays.Add((label, "printed <- capture.output({\n" + script.Text + "\n})\ncat(printed, sep = '\\n')\n" + assertions));
                if (name == "six-labelled" && setting == settings[0])
                {
                    // a record saved before 5.3.10 has neither option: its replay takes the exact methods to be on, as the recipe
                    // had them, and the default correction, which this run used
                    JsonNode older = JsonNode.Parse(run.Record);
                    older["parameters"].AsObject().Remove("metaExact");
                    older["parameters"].AsObject().Remove("metaCC");
                    RScript olderScript = RScriptWriter.Write(JsonDocument.Parse(older.ToJsonString()).RootElement, null, cells.Read);
                    Check(olderScript.HasRecipe && !olderScript.DataUnavailable, label + " replays from a record without the options");
                    replays.Add((label + "-older-record", "printed <- capture.output({\n" + olderScript.Text + "\n})\ncat(printed, sep = '\\n')\n" + assertions));
                }
            }
            failures.AddRange(Meta539Batch(Path.Combine(folder, operation), replays));
        }
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " incidence-rate replays with a stratum without an event in one group disagree with the native analysis");
    }

    // The continuity correction of the calculation options in a culture with a decimal comma. The dialog's option values are
    // written with a dot; read in the current culture, "0.01" became 1 and "0.5" became 5, which the analyses took as a half and
    // showed as "[CC = 1]" or "[CC = 5]", and the dialog's default "0,5" matched none of its options. The real operation is run
    // in de-DE: the default it offers is one of its options, the constant chosen is the one stored, applied and shown, and the
    // corrected figures are those of the formulae in R and of the replay.
    private static void CheckMeta5310CorrectionCulture(string folder)
    {
        List<string> failures = new();
        void Expect(bool ok, string what) { if (ok) Check(true, what); else failures.Add(what + ": NOT SO"); }
        List<(string, string)> replays = new();
        CultureInfo culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            // the choice made in the dialog, from each stored constant
            void Choose(double stored, string choice)
            {
                MetaCC = stored; MetaExact = false; DelayContinuityCorrection = false;
                string offered = null;
                List<string> values = new();
                Run("MetaCalculationOptions", new()
                {
                    ["meta-exact"] = false, ["meta-delay"] = false,
                    ["meta-cc"] = new Func<ITemplateProcessor, Parameter, ParameterBag, ITemplateHost, ParameterBag>((processor, parameter, context, host) =>
                    {
                        OptionParameter option = (OptionParameter)parameter;
                        offered = option.DefaultValue(processor, context);
                        values = option.Options.Select(o => o.Value).ToList();
                        return new ParameterBag(parameter.Name, FilledParameterFactory.Input(choice));
                    })
                });
                Expect(values.Contains(offered) && offered == stored.ToString(inv),
                    "de-DE: with " + stored.ToString(inv) + " stored, the dialog offers that exact default (" + offered + ")");
                Expect(MetaCC == double.Parse(choice, inv), "de-DE: choosing " + choice + " stores " + choice + " (stored: " + MetaCC.ToString(inv) + ")");
            }
            Choose(.01, "0.5");
            double[][] rows = { new double[] { 0, 50, 4, 50 }, new double[] { 5, 60, 9, 60 }, new double[] { 8, 70, 10, 70 }, new double[] { 12, 80, 15, 80 } };
            (OperationRun run, ParameterBag bag, RecordCells cells) Tables(string operation)
            {
                var (cells, f) = Meta5310Sheet(Enumerable.Range(0, 4).Select(j => rows.Select(r => r[j]).ToArray()).ToArray());
                var (run, native) = Run(operation, operation.StartsWith("MetaIncidenceRate", StringComparison.Ordinal)
                    ? new() { ["gamma"] = .95, ["strata"] = null, ["a"] = f[0], ["pt1"] = f[1], ["b"] = f[2], ["pt2"] = f[3] }
                    : new() { ["gamma"] = .95, ["strata"] = null, ["sr"] = f[0], ["sn"] = f[1], ["xr"] = f[2], ["xn"] = f[3] });
                return (run, native.ParameterBag, cells);
            }
            string First(ParameterBag bag, string block) => Convert.ToString(Meta539Rows(bag, block)[0]["lb"].AsObject, inv).Trim();
            Expect(First(Tables("RelativeRiskMeta").bag, "*risks") == "[CC = 0.5]", "de-DE: with 0.5 chosen, the relative risk report marks the study without an event [CC = 0.5]");
            Choose(.5, "-9");
            Choose(-9, "0.01");
            // the first study has no event in its first group: 0/50 v 4/50, or 0 events in 50 person-time v 4 in 50
            const string x = "0.01";
            var analyses = new (string operation, string block, string key, string what, string formula)[]
            {
                ("RelativeRiskMeta", "*risks", "rr", "relative risk", $"(0 + {x}) / (50 + 2 * {x}) / ((4 + {x}) / (50 + 2 * {x}))"),
                ("Mantel", "*or", "or", "odds ratio", $"(0 + {x}) * (46 + {x}) / ((50 + {x}) * (4 + {x}))"),
                ("MetaIncidenceRateRatio", "*ir", "irr", "rate ratio", $"(0 + {x}) / 50 / ((4 + {x}) / 50)"),
                ("MetaIncidenceRateDifference", "*ir", "vi", "variance of the rate difference", $"(0 + {x}) / 50^2 + (4 + {x}) / 50^2")
            };
            foreach (var (operation, block, key, what, formula) in analyses)
            {
                var (run, bag, cells) = Tables(operation);
                bool rates = operation.StartsWith("MetaIncidenceRate", StringComparison.Ordinal);
                Expect(First(bag, rates ? "*inputs" : block) == "[CC = 0.01]",
                    "de-DE: with 0.01 chosen, the " + what + " report marks the study without an event [CC = 0.01] (it reads " + First(bag, rates ? "*inputs" : block) + ")");
                RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
                Check(script.HasRecipe && !script.DataUnavailable, "de-DE: " + operation + " replays the acquired tables");
                // the report's corrected figure against its formula in R, then the replay against the report
                string independent = Meta539Compare("the corrected " + what + " of the report against its formula", formula, new[] { Meta539Number(Meta539Rows(bag, block)[0], key) }, 1e-12);
                replays.Add(("de-DE-" + operation + "-cc0.01", "printed <- capture.output({\n" + script.Text + "\n})\ncat(printed, sep = '\\n')\n" + independent +
                    (rates ? Meta5310RateAssertions(operation == "MetaIncidenceRateRatio", false, bag) : Meta539Assertions(operation, bag))));
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
        failures.AddRange(Meta539Batch(folder, replays));
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " checks of the continuity correction in a culture with a decimal comma fail");
    }

    // A blank cell in a labels column. The report and its plots give such a study its place among the studies kept ("stratum 4",
    // or "study 4" in the summary meta-analysis); the replay used to show a blank. The third row is blank in every column, so
    // five studies are kept, and the label of the fifth row, the fourth kept, is blank.
    private static void CheckMeta5310BlankLabels(string folder)
    {
        double[] exposed = { 100, 150, 110, 80, 200, 120 }, events = { 20, 35, 23, 10, 60, 50 };
        double[] control = { 100, 140, 125, 90, 210, 115 }, controlEvents = { 12, 20, 18, 12, 30, 15 };
        var cases = new (string operation, (string name, double[] values)[] columns, Dictionary<string, object> settings, string labels, string format, string block, string vector)[]
        {
            ("RelativeRiskMeta", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}", "*risks", "study"),
            ("RiskDifference", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}", "*differences", "study"),
            ("PetoMeta", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}", "*odds", "study"),
            ("Mantel", new[] { ("sn", exposed), ("sr", events), ("xn", control), ("xr", controlEvents) }, new(), "strata", "stratum {0}", "*or", "strata"),
            ("MetaIncidenceRateRatio", new[] { ("pt1", exposed), ("a", events), ("pt2", control), ("b", controlEvents) }, new(), "strata", "stratum {0}", "*ir", "strata"),
            ("MetaIncidenceRateDifference", new[] { ("pt1", exposed), ("a", events), ("pt2", control), ("b", controlEvents) }, new(), "strata", "stratum {0}", "*ir", "strata"),
            ("Effect", new[] { ("en", new double[] { 34, 50, 39, 24, 41, 28 }), ("cn", new double[] { 39, 44, 35, 29, 48, 36 }), ("em", new double[] { 11, 8, 9, 10, 14, 13 }),
                ("es", new[] { 2.1, 2.5, 2.8, 3.4, 2.2, 2.6 }), ("cm", new double[] { 8, 10, 9, 8, 11, 8 }), ("cs", new[] { 1.8, 2, 3.1, 3.2, 2.9, 2.4 }) },
                new() { ["type"] = "d", ["g"] = null }, "strata", "stratum {0}", "*approximate", "trial"),
            ("MetaSummary", new[] { ("y", new[] { .05, -.02, .10, .03, .08, -.04 }), ("se_y", new[] { .03, .04, .05, .02, .06, .03 }) },
                new() { ["type"] = "riskDifference", ["use_ci"] = "false" }, "studies", "study {0}", "*studies", "study"),
            ("ProportionMeta", new[] { ("sn", exposed), ("sr", events) }, new() { ["method"] = "arcsine" }, "strata", "stratum {0}", "*proportions", "trial")
        };
        List<string> failures = new();
        List<(string, string)> replays = new();
        MetaExact = false; MetaCC = -9; DelayContinuityCorrection = false;
        foreach (var (operation, columns, settings, labels, format, block, vector) in cases)
        {
            object[][] raw = columns.Select(column => column.values.Select((v, i) => i == 2 ? null : (object)v).ToArray()).ToArray();
            object[] names = { "Study names", "Alpha", "Beta", "Gamma", "Delta", null, "Zeta" };   // a text column's first cell is its title
            var cells = new RecordCells(raw.Append(names).ToArray());
            Dictionary<string, object> answers = new(settings) { ["gamma"] = .95, [labels] = cells.Acquire(columns.Length, 0, names.Length, DataAcquisitionMode.Text) };
            for (int i = 0; i < columns.Length; i++) answers[columns[i].name] = Meta539Frame(cells, i, 6);
            var (run, native) = Run(operation, answers);
            string[] expected = { "Alpha", "Beta", "Delta", string.Format(inv, format, 4), "Zeta" };
            string[] reported = Meta539Rows(native.ParameterBag, block).Select(row => Convert.ToString(row["lb"].AsObject, inv).Trim()).ToArray();
            string label = operation + "-blank-label";
            if (reported.SequenceEqual(expected)) Check(true, label + ": the report gives the study with a blank label its place among the studies kept, " + expected[3]);
            else failures.Add(label + ": the report labels the studies " + string.Join(", ", reported));
            RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
            Check(script.HasRecipe && !script.DataUnavailable, label + " replays the acquired studies");
            replays.Add((label, script.Text + "\nif (!identical(as.character(" + vector + "), " + Meta5310Strings(reported) + ")) stop('the replay labels the studies ', paste0('[', " + vector +
                ", ']', collapse = ' '), ' where the report has " + string.Join(", ", reported) + "')\n"));
        }
        failures.AddRange(Meta539Batch(folder, replays));
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " replays label a study with a blank label otherwise than the report");
    }

    // The x axis of the forest plots gives the confidence level of the analysis, as the native plots do ("relative risk (90%
    // confidence interval)"): the replays of the relative risk, risk difference, Peto odds ratio and correlation analyses said
    // 95% whatever the level. Each replay's plots are drawn through a wrapper that keeps their x axis labels.
    private static void CheckMeta5310AxisLabels(string folder)
    {
        const string wrapper = """
            axis_labels <- character()
            plot <- function(..., xlab = NULL) {
              if (is.null(xlab)) return(graphics::plot(...))
              axis_labels <<- c(axis_labels, xlab)
              graphics::plot(..., xlab = xlab)
            }

            """;
        List<string> failures = new();
        List<(string, string)> replays = new();
        MetaExact = false; MetaCC = -9; DelayContinuityCorrection = false;
        foreach (string operation in new[] { "RelativeRiskMeta", "RiskDifference", "PetoMeta", "Mantel", "MetaIncidenceRateRatio", "MetaIncidenceRateDifference", "MetaCorrelation" })
        {
            var (run, native) = Run(operation, Meta5310Answers(operation, Enumerable.Range(0, 6).ToArray(), .9, out RecordCells cells));
            List<string> drawn = new();
            foreach (ParameterBag chart in Meta539Rows(native.ParameterBag, "*chart"))
                if (chart.TryGetValue("chart", out FilledParameter value) && value.AsObject is ChartDefinition definition)
                {
                    if (definition.ChartOptions is MHOptions forest) drawn.Add(forest.qid + " (" + Formatting.XRound(forest.cco * 100, 1) + "% confidence interval)");
                    else if (definition.ChartOptions is CorrelationOptions correlation) drawn.Add(correlation.Qid);
                }
            string label = operation + "-90-percent";
            string[] axes = drawn.Distinct().ToArray();
            if (axes.Length == 1 && axes[0].EndsWith(" (90% confidence interval)", StringComparison.Ordinal))
                Check(true, label + ": the native forest plots have the axis label " + axes[0]);
            else { failures.Add(label + ": the native forest plots have the axis labels " + string.Join(" | ", axes)); continue; }
            RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
            Check(script.HasRecipe && !script.DataUnavailable, label + " replays the acquired studies");
            replays.Add((label, wrapper + script.Text + "\nif (!any(axis_labels == " + Meta5310Strings(axes) + ") || any(grepl('95%', axis_labels, fixed = TRUE))) " +
                "stop('the forest plot axis differs from the report (" + axes[0] + "): ', paste(axis_labels, collapse = ' | '))\n"));
        }
        // The effect size forest plots have no x axis label: the level is in the line above each plot that gives the pooled
        // estimate ("pooled effect size = 0.5  (90% CI = 0.3 to 0.7)"), which the replay gave as a 95% CI whatever the level,
        // beside limits that were those of the analysis's level. The replay's lines are kept by a wrapper of mtext.
        {
            const string annotated = """
                annotations <- character()
                mtext <- function(text, ...) {
                  annotations <<- c(annotations, text)
                  graphics::mtext(text, ...)
                }

                """;
            const string label = "Effect-90-percent";
            var (run, native) = Run("Effect", Meta5310Answers("Effect", Enumerable.Range(0, 6).ToArray(), .9, out RecordCells cells));
            string[] levels = Meta539Rows(native.ParameterBag, "*chart")
                .Select(chart => chart.TryGetValue("chart", out FilledParameter value) && value.AsObject is ChartDefinition { ChartOptions: EffectOptions effect }
                    ? "(" + Formatting.XRound(effect.cco * 100, 1) + "% CI = " : null)
                .Where(level => level != null).ToArray();
            if (levels.Length == 2 && levels.All(level => level == "(90% CI = "))
            {
                Check(true, label + ": the native forest plots give the pooled effect size with a 90% CI");
                RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
                Check(script.HasRecipe && !script.DataUnavailable, label + " replays the acquired studies");
                replays.Add((label, annotated + script.Text + "\nif (length(annotations) != 2 || !all(grepl('(90% CI = ', annotations, fixed = TRUE)) || any(grepl('95%', annotations, fixed = TRUE))) " +
                    "stop('the level above the forest plots differs from the report (90% CI): ', paste(annotations, collapse = ' | '))\n"));
            }
            else failures.Add(label + ": the native forest plots give the pooled effect size with " + string.Join(" | ", levels));
        }
        failures.AddRange(Meta539Batch(folder, replays));
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " forest plot axis labels differ from the native plots at the 90% level");
    }

    // Begg and Mazumdar's rank correlation and its low power note. With eleven or more studies the report has no "(low power)",
    // which some replays printed whatever the number of studies; and when studies tie (here two pairs of identical studies) the
    // report gives Kendall's tau b with the P value of the normal approximation corrected for ties and for continuity, where
    // cor.test's default for ties has no continuity correction. Twelve studies, six, and seven with ties, for every analysis.
    private static void CheckMeta5310RankCorrelation(string folder)
    {
        var sets = new (string name, int[] take)[]
        {
            ("12-studies", Enumerable.Range(0, 12).ToArray()), ("6-studies", Enumerable.Range(0, 6).ToArray()), ("7-studies-with-ties", new[] { 0, 1, 0, 2, 1, 3, 4 })
        };
        var analyses = new (string operation, string begg, string block)[]
        {
            ("RelativeRiskMeta", "kt$estimate, kt$p.value", "*egger"), ("RiskDifference", "begg$estimate, begg$p.value", "*egger"),
            ("PetoMeta", "begg$estimate, begg$p.value", "*egger"), ("Mantel", "begg$estimate, begg$p.value", "*egger"),
            ("MantelHaenszelScreen", "begg$estimate, begg$p.value", "*egger"), ("Effect", "begg$estimate, begg$p.value", "*egger"),
            ("MetaSummary", "kt$estimate, kt$p.value", "*bias"), ("MetaCorrelation", "bm$estimate, bm$p.value", "*bias"),
            ("ProportionMeta", "begg$estimate, begg$p.value", "*egger"),
            ("MetaIncidenceRateRatio", "bias_figures$begg", "*egger"), ("MetaIncidenceRateDifference", "bias_figures$begg", "*egger")
        };
        List<string> failures = new();
        MetaExact = false; MetaCC = -9; DelayContinuityCorrection = false;
        foreach (var (operation, begg, block) in analyses)
        {
            List<(string, string)> replays = new();
            foreach (var (name, take) in sets)
            {
                string label = operation + "-" + name;
                var (run, native) = Run(operation, Meta5310Answers(operation, take, .95, out RecordCells cells));
                IList<ParameterBag> indicators = Meta539Rows(native.ParameterBag, block);
                string note = indicators.Count == 1 && indicators[0].TryGetValue("low_power_warn", out FilledParameter value) ? Convert.ToString(value?.AsObject, inv) : null;
                if (note != null && note.Contains("low power") == take.Length < 11) Check(true, label + ": the report notes low power only with fewer than eleven studies");
                else { failures.Add(label + ": the report's low power note is " + (note == null ? "absent" : "'" + note + "'")); continue; }
                RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
                Check(script.HasRecipe && !script.DataUnavailable, label + " replays the acquired studies");
                replays.Add((label, "printed <- capture.output({\n" + script.Text + "\n})\ncat(printed, sep = '\\n')\n" +
                    Meta539Compare("Begg-Mazumdar", begg, new[] { "tau", "p2" }.Select(key => Meta539Number(indicators[0], key))) + Meta5310LowPower(indicators[0])));
            }
            failures.AddRange(Meta539Batch(Path.Combine(folder, operation), replays));
        }
        foreach (string failure in failures) Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " Begg-Mazumdar replays disagree with the native analysis");
    }
}

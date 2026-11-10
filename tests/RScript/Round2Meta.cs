using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    private static void CheckRound2Meta(string folder)
    {
        var replays = new List<(string label, string code)>();
        var failures = new List<string>();
        void Replay(string label, string operation, (string name, object[] data)[] columns,
            Dictionary<string, object> answers, string labels, object[] names,
            Func<ParameterBag, string> assertions)
        {
            var cells = new RecordCells(columns.Select(c => c.data).Concat(names == null ? [] : new[] { names }).ToArray());
            foreach (var (column, i) in columns.Select((column, i) => (column, i)))
            {
                var mode = Load(operation).Steps.OfType<ParametersStep>()
                    .SelectMany(step => step.Parameters).OfType<FrameParameter>()
                    .FirstOrDefault(parameter => parameter.Name == column.name)?.SelectionMode
                    ?? DataAcquisitionMode.NumericReplaceMissing;
                answers[column.name] = cells.Acquire(i, 0, column.data.Length, mode);
            }
            answers[labels] = names == null ? null : cells.Acquire(columns.Length, 0, names.Length, DataAcquisitionMode.Text);
            try
            {
                var (run, native, refusals) = RunAtPicker(operation, answers);
                if (refusals.Count != 0)
                    failures.Add(label + " refused at the picker: " + string.Join(", ", refusals));
                RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
                if (!script.HasRecipe || script.DataUnavailable)
                    failures.Add(label + " did not generate a replay");
                else
                    replays.Add((label, "printed <- capture.output({\n" + script.Text + "\n})\n" + assertions(native.ParameterBag)));
            }
            catch (Exception exception)
            {
                failures.Add(label + ": " + exception.GetBaseException().Message);
            }
        }

        static (string, object[]) Column(string name, params object[] values) => (name, values);
        static string Same(ParameterBag bag, string expression, params string[] keys) =>
            Meta539Compare(expression, expression, keys.Select(key => Meta539Number(bag, key)));
        const string OneStudy = """
            if (!any(grepl('^Cochran Q = +0 +[(]df = 0[)] +P = [*]$', trimws(printed)))) stop('one study has Q = 0 and no P value')
            if (any(grepl('%CI', printed, fixed = TRUE))) stop('confidence intervals need a space before CI')
            """;
        string Labels(ParameterBag bag, string block, string expected, string rLabel, string key = "lb")
        {
            string[] native = Meta539Rows(bag, block).Select(row => (string)row[key].AsObject).ToArray();
            return "if (!identical(" + rLabel + ", " + Meta5310Strings(native) + ") || !identical(" + rLabel + ", " + expected + ")) stop('the study labels changed')\n";
        }

        foreach (string operation in new[] { "MetaIncidenceRateRatio", "MetaIncidenceRateDifference" })
        {
            bool ratio = operation == "MetaIncidenceRateRatio";
            var columns = new[] { Column("pt1", 1200), Column("a", 15), Column("pt2", 1100), Column("b", 9) };
            string model = ratio ? "exp(c(res_r$fixed, res_r$random))" : "res$fixed, res$random";
            string random = ratio ? "dsirr" : "dsird";
            Replay(operation + "-one", operation, columns, new() { ["gamma"] = .95 }, "strata", null,
                bag => OneStudy + "\n" + Same(bag, model, "rmh", random) +
                    Same(bag, (ratio ? "res_r" : "res") + "$tau2", "tausq"));

            // S1 and S10: both modes and exact methods, including a stratum that cannot be pooled.
            foreach (bool exact in new[] { false, true })
            {
                MetaExact = exact;
                MetaCC = -9;
                var mac = new[] { Column("pt1", 1000, 1500, 1200, 900), Column("a", 12, 20, 9, 14),
                    Column("pt2", 1100, 1400, 1300, 950), Column("b", 8, 25, 15, 9) };
                Replay(operation + "-S10-" + exact, operation, mac, new() { ["gamma"] = .95 }, "strata", null,
                    bag => Meta5310RateAssertions(ratio, false, bag));
                var empty = new[] { Column("pt1", 1000, 1500, 1200), Column("a", 12, 0, 9),
                    Column("pt2", 1100, 1400, 1300), Column("b", 8, 0, 15) };
                Replay(operation + "-S1-" + exact, operation, empty, new() { ["gamma"] = .95 }, "strata", null,
                    bag => Meta5310RateAssertions(ratio, false, bag));
                var oneGroup = new[] { Column("pt1", 1000, 1500), Column("a", 12, 0),
                    Column("pt2", 1100, 1400), Column("b", 8, 25) };
                foreach (double correction in new[] { -9.0, .01, .5 })
                {
                    MetaCC = correction;
                    Replay(operation + "-S1-one-group-" + exact + "-" + MetaCC.ToString(inv), operation,
                        oneGroup, new() { ["gamma"] = .95 }, "strata", null, bag => Meta5310RateAssertions(ratio, false, bag));
                }
            }
        }

        MetaExact = true;
        MetaCC = -9;
        var invalidTime = new[] { Column("pt1", 1000, 0), Column("a", 12, 4), Column("pt2", 1100, 1400), Column("b", 8, 25) };
        Replay("MetaIncidenceRateRatio-zero-exposure", "MetaIncidenceRateRatio", invalidTime, new() { ["gamma"] = .95 }, "strata", null,
            bag => {
                var exact = Meta539Rows(bag, "*poolok").Single();
                return Same(exact, "cmle, fisher", "eor", "llf", "ulf") +
                    "reference <- poisson.test(c(12, 8), c(1000, 1100), conf.level = .95)\n" +
                    Same(exact, "reference$estimate, reference$conf.int", "eor", "llf", "ulf") + OneStudy + "\n";
            });

        Replay("MetaCorrelation-one", "MetaCorrelation", [Column("r", .31), Column("n", 40)],
            new() { ["gamma"] = .95 }, "studies", null,
            bag => OneStudy + "\n" + Same(bag, "fixed", "rmh", "from_fixed", "to_fixed") +
                Same(bag, "random", "dsrr", "dsll", "dsul") + Same(bag, "tau2", "tausq"));
        foreach (bool missingEstimate in new[] { true, false })
        {
            object[] names = ["Study", "North", "omitted", "South"];
            Replay("MetaCorrelation-missing-" + missingEstimate, "MetaCorrelation",
                [Column("r", .10, missingEstimate ? null : .3, .41), Column("n", 44, missingEstimate ? 36 : null, 40)],
                new() { ["gamma"] = .95 }, "studies", names,
                bag => Labels(bag, "*studies", "c('North', 'South')", "study") +
                    "if (!any(grepl('North', printed, fixed = TRUE)) || !any(grepl('South', printed, fixed = TRUE))) stop('the report labels must print')\n" +
                    Same(bag, "fixed", "rmh", "from_fixed", "to_fixed") +
                    "weights <- c(44, 40) - 3\n" +
                    "reference <- tanh(coef(lm(atanh(c(.10, .41)) ~ 1, weights = weights)) + c(0, -1, 1) * qnorm(.975) / sqrt(sum(weights)))\n" +
                    Same(bag, "reference", "rmh", "from_fixed", "to_fixed"));
            Replay("ProportionMeta-missing-" + missingEstimate, "ProportionMeta",
                [Column("sn", 45, missingEstimate ? 36 : null, 100), Column("sr", 10, missingEstimate ? null : 13, 43)],
                new() { ["gamma"] = .95, ["method"] = "arcsine" }, "strata", names,
                bag => Labels(bag, "*proportions", "c('North', 'South')", "trial") +
                    Same(bag, "fixed", "rmh", "from", "to") +
                    "totals <- c(45, 100); events <- c(10, 43); weights <- totals + .5\n" +
                    "transformed <- asin(sqrt(events/(totals + 1))) + asin(sqrt((events + 1)/(totals + 1)))\n" +
                    "reference <- sin((coef(lm(transformed ~ 1, weights = weights)) + c(0, -1, 1) * qnorm(.975) / sqrt(sum(weights)))/2)^2\n" +
                    Same(bag, "reference", "rmh", "from", "to"));
        }
        foreach (string method in new[] { "doubleArcsine", "arcsine" })
        {
            Replay("ProportionMeta-one-" + method, "ProportionMeta", [Column("sn", 30), Column("sr", 7)],
                new() { ["gamma"] = .95, ["method"] = method }, "strata", null,
                bag => OneStudy + "\n" + Same(bag, "fixed", "rmh", "from", "to") +
                    Same(bag, "inv(xr), inv(xr - z * se_r), inv(xr + z * se_r)", "dspr", "from_ds", "to_ds") + Same(bag, "tau2", "tausq"));
        }
        Replay("MetaSummary-one", "MetaSummary", [Column("y", .05), Column("se_y", .04)],
            new() { ["gamma"] = .95, ["type"] = "riskDifference", ["use_ci"] = "false" }, "studies", null,
            bag => OneStudy + "\n" + Same(bag, "pooled, pooled_dl, tau2", "rmh", "dsrr", "tausq"));
        Replay("Effect-one", "Effect", [Column("en", 25), Column("em", 7.4), Column("es", 2.0), Column("cn", 27), Column("cm", 6.3), Column("cs", 1.8)],
            new() { ["gamma"] = .95, ["type"] = "d", ["g"] = null }, "strata", null,
            bag => {
                Check(Meta539Rows(bag, "*poolok").Count == 0, "the effect size report does not pool a single study");
                return "if (any(grepl('^(Pooled |Cochran Q|Moment-based)', printed))) stop('effect size does not pool a single study')\n" +
                    Same(Meta539Rows(bag, "*approximate").Single(), "d, lcid, ucid", "d", "lci", "uci");
            });
        foreach (string operation in new[] { "RelativeRiskMeta", "RiskDifference", "Mantel", "PetoMeta" })
        {
            Replay(operation + "-one", operation, [Column("sn", 60), Column("sr", 21), Column("xn", 58), Column("xr", 30)],
                new() { ["gamma"] = .95 }, "strata", null, _ => OneStudy + "\n");
        }
        Replay("ProportionMeta-unicode-label", "ProportionMeta", [Column("sn", 30, 45), Column("sr", 7, 13)],
            new() { ["gamma"] = .95, ["method"] = "arcsine" }, "strata", ["Label", "\u00a0", "\u2003 South \u00a0"],
            bag => Labels(bag, "*proportions", "c('stratum 1', 'South')", "trial"));

        object[] rateLabels = ["Label", "North", "\u00a0", "South"];
        Replay("RateDirect-blank-label", "RateDirect", [Column("idxn", 7, 9, 11), Column("times", 110, 100, 130), Column("refn", 400, 420, 430)],
            new() { ["cco"] = .95, ["nunit"] = "1" }, "strata", rateLabels,
            bag => Labels(bag, "*cis", "c('North', 'stratum 2', 'South')", "age", "label"));
        Replay("RateSmr-blank-label", "RateSmr", [Column("rates", .07, .09, .13), Column("times", 110, 100, 130)],
            new() { ["cco"] = .95, ["nunit"] = "1", ["dead"] = 25 }, "strata", rateLabels,
            bag => Labels(bag, "*groups", "c('North', 'stratum 2', 'South')", "age"));
        Replay("Stdrr-blank-label", "Stdrr",
            [Column("a", 7, 9, 11), Column("pt1", 110, 100, 130), Column("b", 8, 11, 9), Column("pt2", 100, 90, 120), Column("ref", 400, 420, 430)],
            new() { ["cco"] = .95, ["nunit"] = "1", ["model"] = "poisson" }, "strata", rateLabels,
            bag => Labels(bag, "*strata", "c('North', 'stratum 2', 'South')", "strata"));

        failures.AddRange(Meta539Batch(folder, replays));
        foreach (string failure in failures)
            Console.WriteLine("      " + failure);
        Check(failures.Count == 0, failures.Count + " round-two meta-analysis discrepancies");
    }
}

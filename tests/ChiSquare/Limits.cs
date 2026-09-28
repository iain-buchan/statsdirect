// The reports of the Chi-square Tests menu where there is no test to make, or a part of the report cannot be given: what is
// refused and with what words, what the report says in the place of a figure, and what is analysed when counts are not whole
// numbers.  Tables of very many subjects are here too.
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal static partial class Program
{
    private static string Note(ParameterBag report) => string.Join(" | ", Rows(report, "*note").Select(n => n["note"].AsString));

    // what a report is refused with, or nothing if it is given
    private static string Refused(Func<ParameterBag> report)
    {
        try { report(); return null; } catch (Exception ex) { return Message(ex); }
    }

    private static void Limits(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("At the limits");
        int before = failures;

        // ---- the 2 by 2 test
        {
            // a cohort in which none of those without the characteristic has the outcome: no risk ratio, and the tests all the same
            ParameterBag cohort = null, neither = null;
            string refused = Refused(() => { cohort = TwoByTwo(5, 0, 3, 8, 0.95, 1, false); neither = TwoByTwo(5, 0, 3, 8, 0.95, 2, false); return cohort; });
            Say(refused == null, $"the table 5 0 / 3 8 of a cohort study has a report ({refused})");
            if (refused == null)
            {
                Say(Note(cohort).Contains("risk ratio is not given") && Rows(cohort, "*relrisk").Count == 0, $"which says that the risk ratio is not given ({Note(cohort)})");
                Say(cohort["chi"].AsDouble == neither["chi"].AsDouble && Math.Abs(cohort["chi"].AsDouble - 16.0 * 40 * 40 / (5.0 * 11 * 8 * 8)) < 1e-12, $"and has the chi-square of the table ({cohort["chi"].AsDouble})");
                List<ParameterBag> exact = Rows(cohort, "*fisher");
                Say(exact.Count == 1 && exact[0]["p_2"].AsDouble == Fisher(5, 0, 3, 8)["p_2"].AsDouble, "and Fisher's exact test, for the expected counts are few");
            }
            // a table with an empty row, and one with an empty column
            foreach (double[] t in new[] { new double[] { 0, 0, 3, 4 }, new double[] { 3, 0, 4, 0 } })
            {
                refused = Refused(() => TwoByTwo(t[0], t[1], t[2], t[3], 0.95, 2, false));
                Say(refused != null && refused.Contains("has no observations"), $"the table {Table(t)} is refused, and the refusal says why ({refused})");
            }
            // a table that is too large for the exact method of the odds ratio
            ParameterBag large = TwoByTwo(1000000, 2000000, 1502000, 2998000, 0.95, 0, false);
            ParameterBag odds = Rows(large, "*odds")[0];
            Say(Note(odds).Contains("too large for the exact method") && odds["llf"].AsDouble == M, $"a table of 7.5 million of a case-control study: the report says that the exact method is not used ({Note(odds)})");
            List<ParameterBag> test = Rows(large, "*fisher");
            Say(test.Count == 1 && Differs(test[0]["p_2"].AsObject, Fisher(1000000, 2000000, 1502000, 2998000)["p_2"].AsDouble) == 0 && test[0]["p_2"].AsDouble > 0.1 && test[0]["p_2"].AsDouble < 0.4,
                $"and gives Fisher's exact test ({(test.Count == 1 ? test[0]["p_2"].AsObject : "none")})");
            ParameterBag small = TwoByTwo(41, 216, 64, 180, 0.95, 0, false);
            Say(Note(Rows(small, "*odds")[0]) == "" && Rows(small, "*fisher").Count == 0 && Note(small) == "", "a table of 501: nothing is said, and Fisher's exact test is not added to the exact P values of the odds ratio");
            // counts that are not whole numbers: chi-square is of the counts as they are, and the exact figures are of the counts rounded
            ParameterBag parts = TwoByTwo(2.5, 3.5, 4.5, 5.5, 0.95, 0, false);
            double f = 2.5 * 5.5 - 3.5 * 4.5;
            Say(Math.Abs(parts["chi"].AsDouble - f * f * 16 / (6 * 10 * 7 * 9.0)) < 1e-12 && Differs(Rows(parts, "*odds")[0]["p2f"].AsObject, Fisher(2, 4, 4, 6)["p_2"].AsDouble) < 1e-12,
                $"the table 2.5 3.5 / 4.5 5.5: chi-square is of the counts as they are ({parts["chi"].AsDouble}), and the exact P value is that of 2 4 / 4 6 ({Rows(parts, "*odds")[0]["p2f"].AsObject})");
        }

        // ---- the 2 by k test
        {
            double[] s = { 5, 8, 20 }, f = { 10, 20, 40 }, same = { 2, 2, 2 };
            ParameterBag report = TwoByK(2, s, f, same);
            var (total, _) = OfRows(s, f, new double[] { 1, 2, 3 });
            Say(Note(report) == "The scores are all the same: there is no trend to test." && Rows(report, "*z").Count == 0 && Differs(report["chi"].AsObject, total) < 1e-12,
                $"scores that are all the same: the report has the total chi-square and says that there is no trend to test ({Note(report)})");
            ParameterBag simulated = Simulated(2, s, f, same, 20000, 11, 0.99);
            Say(Rows(simulated, "*result").Count == 0 && Note(simulated).Contains("no trend to test"), $"and no P value is simulated ({Note(simulated)})");
            // scores that are the same but for one
            report = TwoByK(2, s, f, new double[] { 2, 2, 2.5 });
            Say(Note(report) == "" && Rows(report, "*z").Count == 1, "scores 2, 2 and 2.5: the test for trend is made");

            foreach (var (successes, failures, what) in new[] { (new double[] { 0, 0, 0 }, new double[] { 9, 5, 2 }, "successes"), (new double[] { 4, 5, 6 }, new double[] { 0, 0, 0 }, "failures") })
            {
                report = TwoByK(1, successes, failures, null);
                Say(Note(report) == $"Chi-square can not be calculated: there are no {what}." && Rows(report, "*z").Count == 0 && double.IsNaN(report["chi"].AsDouble), $"a table without {what}: the report says that chi-square can not be calculated ({Note(report)})");
                simulated = Simulated(1, successes, failures, null, 20000, 11, 0.99);
                Say(Rows(simulated, "*result").Count == 0 && Note(simulated).Contains("there are no " + what), $"and no P value is simulated ({Note(simulated)})");
            }

            simulated = Simulated(1, new double[] { 1200000, 3, 7 }, new double[] { 3900000, 4, 1 }, null, 1000, 11, 0.99);
            Say(Rows(simulated, "*result").Count == 0 && Note(simulated).Contains("no more than 5,000,000"), $"a table of 5,100,015: no P value is simulated, and the report says why ({Note(simulated)})");
            simulated = Simulated(1, new double[] { 1200000, 3, 7 }, new double[] { 3700000, 4, 1 }, null, 1000, 11, 0.99);
            Say(Rows(simulated, "*result").Count == 1, "a table of 4,900,015: a P value is simulated");

            // counts that are not whole numbers are rounded, a half to the even number: the P value is that of the counts rounded
            ParameterBag parts = Simulated(1, new double[] { 2.5, 3.5, 7.25 }, new double[] { 6.5, 4.5, 1.75 }, null, 50000, 77, 0.99);
            ParameterBag whole = Simulated(1, new double[] { 2, 4, 7 }, new double[] { 6, 4, 2 }, null, 50000, 77, 0.99);
            double probability = TrendProbability(new[] { 2, 4, 7 }, new[] { 8, 8, 9 }, new double[] { 1, 2, 3 });
            double p = Rows(parts, "*result")[0]["p"].AsDouble;
            Say(p == Rows(whole, "*result")[0]["p"].AsDouble && Math.Abs(p - probability) < 5 * Math.Sqrt(probability * (1 - probability) / 50000),
                $"successes 2.5, 3.5 and 7.25 with failures 6.5, 4.5 and 1.75: the simulated P value is that of the counts rounded ({p}; the probability of the tables is {probability})");
            Say(Rows(parts, "*result")[0]["rounded"].AsString.Contains("rounded") && Rows(whole, "*result")[0]["rounded"].AsString == "", "and the report says that the counts were rounded");

            Say((Refused(() => TwoByK(1, new double[] { 8 }, new double[] { 2 }, null)) ?? "").Contains("At least two rows"), "one row is refused");
            Say((Refused(() => TwoByK(1, new double[] { 4, 0, 6 }, new double[] { 3, 0, 2 }, null)) ?? "").Contains("row 2 total"), "a row without observations is refused, and the refusal names it");
        }

        // tables of very many subjects, with a chi-square that is small: the benchmarks are sums of squares worked out in R
        {
            Dictionary<string, object> figures = new();
            foreach (string line in File.ReadAllLines(Path.Combine(folder, "cases-many.txt")))
            {
                string[] part = line.Split('\t');
                double[] v = part.Skip(2).Select(x => double.Parse(x, inv)).ToArray();
                var (s, f, w) = RowsOf(v, 2, (int)v[1]);
                Report("chi2k|" + part[1], () => TwoByK((int)v[0], s, f, w), figures);
            }
            // the total chi-square to 12 figures; the chi-square for trend, and what is left, to 9
            Compare(Path.Combine(folder, "r-many.txt"), figures, 1e-12, key => !key.EndsWith("|chi"));
            Compare(Path.Combine(folder, "r-many.txt"), figures, 1e-9, key => key.EndsWith("|chi"));
        }

        // ---- the test of Mantel and Haenszel of tables that are typed
        {
            string refused = Refused(() => MantelTyped(0.95, true, new double[] { 0, 0, 4, 9, 0, 3, 0, 8 }));
            Say(refused != null && refused.Contains("None of the tables can be pooled"), $"tables of which none can be pooled are refused, and the refusal says why ({refused})");

            // the table of the report has the counts as the heading of the report names them: exposed with the outcome, not exposed
            // with it, exposed without it, not exposed without it
            ParameterBag report = MantelTyped(0.95, false, new double[] { 83, 3, 72, 14, 90, 3, 227, 43 });
            ParameterBag first = Rows(report, "*inputs")[0];
            Say(first["a"].AsDouble == 83 && first["b"].AsDouble == 3 && first["c"].AsDouble == 72 && first["d"].AsDouble == 14, "the table 83 3 / 72 14 is in the report as 83, 3, 72, 14");

            // A table with an empty cell has the continuity correction of the treatment arm: what is added to each count of a group
            // is in proportion to the size of the group, the two sum to 1, and the groups are the columns of the table
            double[] t = { 5, 0, 20, 7, 12, 7, 5, 14 };
            report = MantelTyped(0.95, false, t);
            double ratio = (0.0 + 7) / (5 + 20), exposed = 1 / (ratio + 1), control = ratio / (ratio + 1);
            double a = 5 + exposed, b = 0 + control, c = 20 + exposed, d = 7 + control;
            double pooled = (a * d / (a + b + c + d) + 12.0 * 14 / 38) / (b * c / (a + b + c + d) + 7.0 * 5 / 38);
            Say(Differs(report["odds"].AsObject, pooled) < 1e-12, $"the tables 5 0 / 20 7 and 12 7 / 5 14: the pooled odds ratio is {report["odds"].AsObject}, and is to be {pooled}");
            Say(Differs(Rows(report, "*or")[0]["or"].AsObject, a * d / (b * c)) < 1e-12, $"and the odds ratio of the first table is {Rows(report, "*or")[0]["or"].AsObject}, and is to be {a * d / (b * c)}");

            // "Try exact methods?" is for the limits of each table too, whatever the preference of the meta-analysis menu is
            double[] two = { 12, 7, 5, 14, 9, 4, 6, 11 };
            foreach (bool preference in new[] { false, true })
            {
                PreferencesProxy.MetaExact = preference;
                try
                {
                    ParameterBag exact = MantelTyped(0.95, true, two), approximate = MantelTyped(0.95, false, two);
                    ParameterBag p = new();
                    p.AddInput("a", 12.0); p.AddInput("b", 7.0); p.AddInput("c", 5.0); p.AddInput("d", 14.0); p.AddInput("gamma", 0.95);
                    ParameterBag one = Exact.RptExactORCML(host, p).ParameterBag;
                    ParameterBag row = Rows(exact, "*or")[0];
                    Say(exact["method"].AsString == "CML" && Differs(row["lci"].AsObject, one["llf"].AsDouble) < 1e-9 && Differs(row["uci"].AsObject, one["ulf"].AsDouble) < 1e-9 && Rows(exact, "*cml").Count == 1,
                        $"the exact methods asked for, the preference {(preference ? "on" : "off")}: the limits of the first table are the exact limits ({row["lci"].AsObject} to {row["uci"].AsObject})");
                    row = Rows(approximate, "*or")[0];
                    double z = Deviate(0.95), l = Math.Log(12.0 * 14 / (7 * 5)), se = Math.Sqrt(1 / 12.0 + 1 / 7.0 + 1 / 5.0 + 1 / 14.0);
                    Say(approximate["method"].AsString == "logit" && Differs(row["lci"].AsObject, Math.Exp(l - z * se)) < 1e-9 && Differs(row["uci"].AsObject, Math.Exp(l + z * se)) < 1e-9 && Rows(approximate, "*cml").Count == 0,
                        $"the exact methods not asked for, the preference {(preference ? "on" : "off")}: the limits of the first table are the limits of Woolf ({row["lci"].AsObject} to {row["uci"].AsObject})");
                }
                finally { PreferencesProxy.MetaExact = true; }
            }

            // counts that are not whole numbers are rounded for the exact method, a half to the even number, in both analyses
            double[] parts = { 2.5, 3.5, 4.5, 5.5, 10.4, 3.6, 7.2, 12.49 }, rounded = { 2, 4, 4, 6, 10, 4, 7, 12 };
            foreach (var (name, of) in new (string, Func<double[], ParameterBag>)[] { ("typed", x => MantelTyped(0.95, true, x)), ("from a worksheet", x => MantelSheet(0.95, true, x)) })
            {
                ParameterBag given = Rows(of(parts), "*cml")[0], whole = Rows(of(rounded), "*cml")[0];
                bool equal = new[] { "eor", "llf", "ulf", "llm", "ulm", "p1f", "p2f", "p1m", "p2m" }.All(k => given[k].AsDouble == whole[k].AsDouble);
                Say(equal && given["eor"].AsDouble != M, $"tables {name} whose counts are not whole numbers: the exact figures are those of the counts rounded (the estimate is {given["eor"].AsObject})");
            }
        }

        // ---- Woolf's analysis
        {
            string refused = Refused(() => WoolfTyped(0.95, true, new double[] { 12, 7, 5, 14, 0, 0, 4, 9 }));
            Say(refused != null && refused.Contains("Table 2") && refused.Contains("without observations"), $"a table with a row without observations is refused, and the refusal names the table ({refused})");
            refused = Refused(() => WoolfSheet(0.95, true, new double[] { 19, 12, 19, 5, 10, 12, 13, 4 }));
            Say(refused != null && refused.Contains("Row 2"), $"from a worksheet, more with the outcome than the group has: the refusal names the row ({refused})");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  what is refused, what the reports say when there is no test, counts that are not whole numbers, tables of very many subjects");
    }
}

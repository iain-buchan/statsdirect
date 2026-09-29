// The reports of the Rates menu where the numbers are at the ends of what they can be: what is refused and with what words, a
// confidence level or a multiplier that cannot be, populations and strata without events, ratios that are infinite, a ratio of
// thousands of millions, strata with a number missing, the same numbers from a worksheet and from the screen form, a stratum
// with more events than person-time, the person-times in other units and the groups the other way round, every subject with
// the event, and millions of events.
using StatsDirect.Charting;
using StatsDirect.Templates;

internal static partial class Program
{
    // what a report is refused with, or nothing if it is given
    private static string Refused(Func<ParameterBag> report)
    {
        try { report(); return null; } catch (Exception ex) { return Message(ex); }
    }

    // the first figure of one report that is not the figure of another, or nothing; the labels may be left out of it
    private static string Unlike(ParameterBag one, ParameterBag other, bool labels = true)
    {
        Dictionary<string, object> a = new(), b = new();
        Collect("", one, a);
        Collect("", other, b);
        foreach (string name in a.Keys.Union(b.Keys))
        {
            if (!labels && (name.EndsWith("|label") || name.EndsWith("|lb"))) continue;
            if (!a.ContainsKey(name) || !b.ContainsKey(name) || !a[name].Equals(b[name]))
                return $"{name}: {(a.ContainsKey(name) ? a[name] : "none")} and {(b.ContainsKey(name) ? b[name] : "none")}";
        }
        return null;
    }

    private static double Seconds(Action what)
    {
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        what();
        return clock.Elapsed.TotalSeconds;
    }

    private static void Refuses(string what, Func<ParameterBag> report, string words)
    {
        string refused = Refused(report);
        Say(refused != null && refused.Contains(words), $"{what} is refused, and the refusal says why ({refused ?? "it is not refused"})");
    }

    private static void Limits()
    {
        Console.WriteLine();
        Console.WriteLine("At the limits");
        int before = failures;
        double[] r2 = { 0.01, 0.02 }, y2 = { 100, 300 }, e2 = { 3, 40 }, n2 = { 50, 100 }, w2 = { 1, 3 };
        double[] a2 = { 5, 3 }, p2 = { 100, 80 }, b2 = { 9, 4 }, q2 = { 150, 90 }, f2 = { 1, 2 };

        // ---- what is refused
        Refuses("a number of cases below zero", () => Two(-3, 4, 10, 10, 0.95, false), "must not be negative");
        Refuses("a number of cases below zero in the second group", () => Two(5, -4, 10, 10, 0.95, false), "must not be negative");
        Refuses("a person-time of zero", () => Two(3, 4, 0, 10, 0.95, false), "Person-time must be greater than zero");
        Refuses("a person-time below zero", () => Two(3, 4, 10, -2, 0.95, false), "Person-time must be greater than zero");
        Refuses("two groups without cases", () => Two(0, 0, 10, 10, 0.95, true), "no cases in either group");
        Refuses("the conditional analysis of 2.5 cases", () => Two(2.5, 7.5, 40, 90, 0.95, true), "whole numbers");
        Say(Refused(() => Two(2.5, 7.5, 40, 90, 0.95, false)) == null, "2.5 cases are taken without the conditional analysis");
        foreach (bool screen in new[] { false, true })
        {
            string from = screen ? "from the screen form" : "from a worksheet";
            Refuses($"a reference rate below zero {from}", () => Smr(0.95, "1", 5, new[] { -0.01, 0.02 }, y2, screen), "must not be negative");
            Refuses($"a person-time below zero {from}", () => Smr(0.95, "1", 5, r2, new double[] { -100, 300 }, screen), "must not be negative");
            Refuses($"reference rates of zero {from}", () => Smr(0.95, "1", 5, new double[] { 0, 0 }, y2, screen), "No deaths are expected");
            Refuses($"a number of deaths below zero {from}", () => Smr(0.95, "1", -5, r2, y2, screen), "must not be negative");
            Refuses($"direct standardization with a person-time of zero {from}", () => Direct(0.95, "1", e2, new double[] { 0, 100 }, w2, screen), "Person-time must be greater than zero");
            Refuses($"direct standardization with a number of events below zero {from}", () => Direct(0.95, "1", new double[] { -3, 40 }, n2, w2, screen), "must not be negative");
            Refuses($"direct standardization with a reference group size below zero {from}", () => Direct(0.95, "1", e2, n2, new double[] { -1, 3 }, screen), "must not be negative");
            Refuses($"direct standardization with reference group sizes of zero {from}", () => Direct(0.95, "1", e2, n2, new double[] { 0, 0 }, screen), "Total reference group size must be greater than zero");
        }
        Refuses("rates that are all missing", () => Smr(0.95, "1", 5, new[] { M, M }, y2, false), "no stratum");
        Refuses("events that are all missing", () => Direct(0.95, "1", new[] { M, M }, n2, w2, false), "no stratum");
        Refuses("a grid of one column", () =>
        {
            ParameterBag p = new();
            p.AddInput("cco", 0.95); p.AddInput("nunit", "1"); p.AddInput("dead", 5); p.AddInput("data", Frame(r2));
            return StatsDirect.Builtins.Rates.RptRateSmr(p).ParameterBag;
        }, "Enter the reference rate");
        Refuses("a grid of two columns for direct standardization", () =>
        {
            ParameterBag p = new();
            p.AddInput("cco", 0.95); p.AddInput("nunit", "1"); p.AddInput("data", Frame(e2, n2));
            return StatsDirect.Builtins.Analysis.RptRateDirectStd(p).ParameterBag;
        }, "Enter the events");
        foreach (string model in new[] { "poisson", "binomial" })
        {
            Refuses($"two populations with a person-time of zero ({model})", () => Standardized(0.95, "1", model, a2, new double[] { 0, 80 }, b2, q2, f2), "Person-time must be greater than zero");
            Refuses($"two populations with a person-time of zero in the second ({model})", () => Standardized(0.95, "1", model, a2, p2, b2, new double[] { 150, 0 }, f2), "Person-time must be greater than zero");
            Refuses($"two populations with reference group sizes of zero ({model})", () => Standardized(0.95, "1", model, a2, p2, b2, q2, new double[] { 0, 0 }), "Total reference group size must be greater than zero");
            Refuses($"two populations with a reference group size below zero ({model})", () => Standardized(0.95, "1", model, a2, p2, b2, q2, new double[] { -1, 2 }), "must not be negative");
            Refuses($"two populations with a number of events below zero ({model})", () => Standardized(0.95, "1", model, new double[] { -5, 3 }, p2, b2, q2, f2), "must not be negative");
            Refuses($"two populations with events that are all missing ({model})", () => Standardized(0.95, "1", model, new[] { M, M }, p2, b2, q2, f2), "no stratum");
        }
        Refuses("the binomial model with more events than person-time", () => Standardized(0.95, "1", "binomial", new double[] { 500, 3 }, p2, b2, q2, f2), "must not exceed the person-time");
        Say(Refused(() => Standardized(0.95, "1", "poisson", new double[] { 500, 3 }, p2, b2, q2, f2)) == null, "the Poisson model takes more events than person-time");
        Refuses("a rate in a time of zero", () => Rate(3, 0, 0.95), "time at risk must be greater than zero");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  what is refused, and the words of it");

        // ---- a confidence level that is not between 0 and 1 is taken as 0.95, and a multiplier that is not above 0 as 1
        before = failures;
        foreach (double level in new[] { 0, 1, 1.5, -0.2 })
        {
            string differs = Unlike(Two(3, 4, 10, 10, level, true), Two(3, 4, 10, 10, 0.95, true))
                ?? Unlike(Smr(level, "1", 5, r2, y2, false), Smr(0.95, "1", 5, r2, y2, false)) ?? Unlike(Smr(level, "1", 5, r2, y2, true), Smr(0.95, "1", 5, r2, y2, true))
                ?? Unlike(Direct(level, "1", e2, n2, w2, false), Direct(0.95, "1", e2, n2, w2, false)) ?? Unlike(Direct(level, "1", e2, n2, w2, true), Direct(0.95, "1", e2, n2, w2, true))
                ?? Unlike(Standardized(level, "1", "poisson", a2, p2, b2, q2, f2), Standardized(0.95, "1", "poisson", a2, p2, b2, q2, f2))
                ?? Unlike(Standardized(level, "1", "binomial", a2, p2, b2, q2, f2), Standardized(0.95, "1", "binomial", a2, p2, b2, q2, f2))
                ?? Unlike(Rate(14, 400, level), Rate(14, 400, 0.95));
            Say(differs == null, $"a confidence level of {level.ToString(inv)} is taken as 0.95 ({differs})");
        }
        foreach (string multiplier in new[] { "0", "-1000", "", "per thousand" })
        {
            string differs = Unlike(Smr(0.95, multiplier, 5, r2, y2, false), Smr(0.95, "1", 5, r2, y2, false))
                ?? Unlike(Direct(0.95, multiplier, e2, n2, w2, false), Direct(0.95, "1", e2, n2, w2, false)) ?? Unlike(Direct(0.95, multiplier, e2, n2, w2, true), Direct(0.95, "1", e2, n2, w2, true))
                ?? Unlike(Standardized(0.95, multiplier, "poisson", a2, p2, b2, q2, f2), Standardized(0.95, "1", "poisson", a2, p2, b2, q2, f2));
            Say(differs == null, $"a multiplier of \"{multiplier}\" is taken as 1 ({differs})");
        }
        // the rates are given by the multiplier, and the ratios are not
        {
            ParameterBag one = Standardized(0.95, "1", "poisson", a2, p2, b2, q2, f2), thousand = Standardized(0.95, "1000", "poisson", a2, p2, b2, q2, f2);
            string differs = First(thousand, 1e-14, ("cre", 1000 * one["cre"].AsDouble), ("cre_to", 1000 * one["cre_to"].AsDouble), ("sre", 1000 * one["sre"].AsDouble), ("srne_from", 1000 * one["srne_from"].AsDouble),
                ("srr", one["srr"].AsDouble), ("srr_from", one["srr_from"].AsDouble), ("srr_to", one["srr_to"].AsDouble));
            Say(differs == null && thousand["units"].AsString == "1000 units" && one["units"].AsString == "1 unit", $"two populations with rates by the thousand: {differs}");
            ParameterBag direct = Direct(0.95, "1", e2, n2, w2, false), byThousand = Direct(0.95, "1000", e2, n2, w2, false);
            differs = First(byThousand, 1e-14, ("crude", 1000 * direct["crude"].AsDouble), ("stdr", 1000 * direct["stdr"].AsDouble), ("ser_any", 1000 * direct["ser_any"].AsDouble), ("to_small", 1000 * direct["to_small"].AsDouble),
                ("from_dobson", 1000 * direct["from_dobson"].AsDouble), ("stde", direct["stde"].AsDouble), ("events", direct["events"].AsDouble));
            Say(differs == null, $"direct standardization with rates by the thousand: {differs}");
            // reference rates by the million are rates of a millionth
            double[] million = r2.Select(r => r * 1e6).ToArray();
            differs = First(Smr(0.95, "1000000", 5, million, y2, false), 1e-12, ("total", 7), ("ratio", 5.0 / 7), ("from", Smr(0.95, "1", 5, r2, y2, false)["from"].AsDouble));
            Say(differs == null, $"reference rates by the million: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  confidence levels and multipliers that cannot be");

        // ---- the standardized mortality ratio as a whole number
        before = failures;
        {
            // 5 deaths where 3 in 10 million are expected: a ratio of 16,666,667, which times 100 is more than a whole number of 32
            // bits holds
            ParameterBag tiny = Smr(0.95, "1", 5, new[] { 1e-9, 2e-9 }, new double[] { 100, 100 }, false);
            var (lower, upper) = PoissonLimits(5, 0.025);
            string differs = First(tiny, 1e-9, ("ratio", 5 / 3e-7), ("smr", Math.Round(500 / 3e-7)), ("from", lower / 3e-7), ("to", upper / 3e-7), ("from100", Math.Round(100 * lower / 3e-7)), ("to100", Math.Round(100 * upper / 3e-7)));
            Say(differs == null && tiny["to100"].AsDouble > int.MaxValue, $"5 deaths where 0.0000003 are expected: {differs}; the upper limit times 100 is {tiny["to100"].AsObject}");
            // 1 death where 250 are expected: the ratio times 100 is 0.4, which as a whole number is 0
            ParameterBag below = Smr(0.95, "1", 1, new[] { 0.5, 0.5 }, new double[] { 250, 250 }, true);
            differs = First(below, 1e-12, ("ratio", 0.004), ("smr", 0), ("from100", 0), ("to100", 2));
            Say(differs == null, $"1 death where 250 are expected: {differs}");
            // 5 deaths where 8 are expected: 62.5, which is given as 63; and no deaths
            differs = First(Smr(0.95, "1", 5, new[] { 0.5 }, new double[] { 16 }, false), 1e-12, ("ratio", 0.625), ("smr", 63));
            Say(differs == null, $"5 deaths where 8 are expected: {differs}");
            ParameterBag none = Smr(0.95, "1", 0, r2, y2, false);
            differs = First(none, 1e-12, ("ratio", 0), ("smr", 0), ("from", 0), ("to", -Math.Log(0.025) / 7), ("from100", 0), ("p_hi", 1), ("p_lo", Math.Exp(-7)));
            Say(differs == null, $"no deaths where 7 are expected: {differs}");
            // two thousand million deaths
            var (least, most) = PoissonLimits(2000000000, 0.005);
            var (orMore, orFewer) = PoissonTails(2000000000, 2e9);
            double seconds = Seconds(() => differs = First(Smr(0.99, "1", 2000000000, new[] { 0.5 }, new[] { 4e9 }, false), 1e-9, ("ratio", 1), ("smr", 100), ("from100", 100), ("to100", 100),
                ("from", least / 2e9), ("to", most / 2e9), ("p_hi", orMore), ("p_lo", orFewer)));
            Say(differs == null && seconds < 2, $"2,000,000,000 deaths where as many are expected: {differs}; {seconds:F2} seconds");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  the standardized mortality ratio as a whole number");

        // ---- a worksheet and the screen form; strata with a number missing; the labels
        before = failures;
        {
            double[] rates = { 0.011, 0.02, 0.003, 0.04 }, times = { 120, 300, 45.5, 60 }, events = { 3, 40, 0, 7 }, reference = { 1000, 3000, 250, 0 };
            Say(Unlike(Smr(0.9, "100", 6, rates, times, false), Smr(0.9, "100", 6, rates, times, true)) == null, $"the SMR from two columns and from the grid ({Unlike(Smr(0.9, "100", 6, rates, times, false), Smr(0.9, "100", 6, rates, times, true))})");
            string differs = Unlike(Direct(0.9, "1000", events, times, reference, false), Direct(0.9, "1000", events, times, reference, true), false);
            Say(differs == null, $"direct standardization from three columns and from the grid ({differs})");
            List<ParameterBag> sheet = Rows(Direct(0.9, "1000", events, times, reference, false), "*cis"), grid = Rows(Direct(0.9, "1000", events, times, reference, true), "*cis");
            Say(sheet.Select(r => r["label"].AsString).SequenceEqual(new[] { "stratum 1", "stratum 2", "stratum 3", "stratum 4" }) && grid.All(r => r["label"].AsString == ""),
                "the strata of a worksheet without labels are named by their numbers, and those of the grid have no names");

            // a stratum with a number missing is left out, and the labels stay with their strata
            string[] labels = { "young", "  middle  ", "", "old", "oldest" };
            double[] e5 = { 3, M, 40, 7, 2 }, n5 = { 50, 70, 100, M, 80 }, w5 = { 1, 2, 3, 4, 5 };
            ParameterBag with = Direct(0.95, "1", e5, n5, w5, false, labels), without = Direct(0.95, "1", new double[] { 3, 40, 2 }, new double[] { 50, 100, 80 }, new double[] { 1, 3, 5 }, false);
            Say(Unlike(with, without, false) == null && Rows(with, "*cis").Select(r => r["label"].AsString).SequenceEqual(new[] { "young", "stratum 3", "oldest" }),
                $"direct standardization of five strata of which two have a number missing ({Unlike(with, without, false)}); the labels are {string.Join(", ", Rows(with, "*cis").Select(r => r["label"].AsString))}");
            with = Smr(0.95, "1", 4, new[] { 0.01, M, 0.02, 0.05, 0.01 }, new[] { 100, 200, 300, M, 50 }, false, labels);
            without = Smr(0.95, "1", 4, new[] { 0.01, 0.02, 0.01 }, new double[] { 100, 300, 50 }, false);
            Say(Unlike(with, without, false) == null && Rows(with, "*groups").Select(r => r["lb"].AsString).SequenceEqual(new[] { "young", "stratum 3", "oldest" }) && Rows(without, "*groups").All(r => r["lb"].AsString == ""),
                $"the SMR of five strata of which two have a number missing ({Unlike(with, without, false)}); the labels are {string.Join(", ", Rows(with, "*groups").Select(r => r["lb"].AsString))}");
            foreach (string model in new[] { "poisson", "binomial" })
            {
                with = Standardized(0.95, "1", model, new[] { 5, M, 3, 1, 2 }, new[] { 100, 100, 80, 40, M }, new[] { 9, 1, 4, M, 3 }, new double[] { 150, 100, 90, 50, 60 }, w5, labels);
                without = Standardized(0.95, "1", model, new double[] { 5, 3 }, new double[] { 100, 80 }, new double[] { 9, 4 }, new double[] { 150, 90 }, new double[] { 1, 3 });
                Say(Unlike(with, without, false) == null && Rows(with, "*rates").Select(r => r["lb"].AsString).SequenceEqual(new[] { "young", "stratum 3", "All (crude)" }),
                    $"two populations of five strata of which three have a number missing, {model} ({Unlike(with, without, false)}); the labels are {string.Join(", ", Rows(with, "*rates").Select(r => r["lb"].AsString))}");
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  a worksheet and the screen form, strata with a number missing, and the labels");

        // ---- direct standardization of a stratum with more events than person-time, which is a rate above 1: the Poisson model
        // has its figures, and the binomial model has none and a note that says why
        before = failures;
        foreach (bool screen in new[] { false, true })
        {
            string from = screen ? "from the screen form" : "from a worksheet";
            double[] events = { 3, 400 }, times = { 50, 100 }, reference = { 1, 3 };
            double z = Deviate(0.95);
            double rate = 0.25 * 3 / 50 + 0.75 * 400 / 100, variance = 0.0625 * 3 / 2500 + 0.5625 * 400 / 10000;
            var (lower, upper) = PoissonLimits(403, 0.025);
            var (least, most) = PoissonLimits(400, 0.025);
            string refused = Refused(() => Direct(0.95, "1", events, times, reference, screen));
            Say(refused == null, $"3 events in 50 and 400 in 100 {from} are taken ({refused})");
            if (refused != null) continue;
            ParameterBag report = Direct(0.95, "1", events, times, reference, screen);
            string differs = First(report, 1e-9, ("events", 403), ("crude", 403.0 / 150), ("stdr", rate), ("stde", rate * 150), ("ser_small", Math.Sqrt(variance)), ("from_small", rate - z * Math.Sqrt(variance)), ("to_small", rate + z * Math.Sqrt(variance)),
                    ("from_dobson", rate + Math.Sqrt(variance / 403) * (lower - 403)), ("to_dobson", rate + Math.Sqrt(variance / 403) * (upper - 403)), ("ser_any", M), ("from_any", M), ("to_any", M))
                ?? First(Rows(report, "*cis")[1], 1e-9, ("idxr", 4), ("from", least / 100), ("to", most / 100));
            List<ParameterBag> notes = Rows(report, "*note");
            Say(differs == null && notes.Count == 1 && notes[0]["note"].AsString.Contains("binomial model is not given") && notes[0]["note"].AsString.Contains("more events than person-time"),
                $"3 events in 50 and 400 in 100 {from}: {differs}; the report has {notes.Count} notes{(notes.Count > 0 ? " (" + notes[0]["note"].AsString + ")" : "")}");
            // the person-time in thousands for units: the rates by the unit of the one are the rates by the thousand of the other,
            // with the Poisson model, of which the figures do not change with the unit of the person-time
            ParameterBag units = Direct(0.9, "1000", new double[] { 3, 40, 7 }, new double[] { 50000, 100000, 2000 }, new double[] { 1, 3, 2 }, screen),
                thousands = Direct(0.9, "1", new double[] { 3, 40, 7 }, new double[] { 50, 100, 2 }, new double[] { 1, 3, 2 }, screen);
            differs = First(thousands, 1e-12, ("crude", units["crude"].AsDouble), ("stdr", units["stdr"].AsDouble), ("ser_small", units["ser_small"].AsDouble), ("from_small", units["from_small"].AsDouble), ("to_small", units["to_small"].AsDouble),
                    ("from_dobson", units["from_dobson"].AsDouble), ("to_dobson", units["to_dobson"].AsDouble), ("ser_any", M))
                ?? First(Rows(thousands, "*cis")[2], 1e-12, ("idxr", Rows(units, "*cis")[2]["idxr"].AsDouble), ("from", Rows(units, "*cis")[2]["from"].AsDouble), ("to", Rows(units, "*cis")[2]["to"].AsDouble));
            Say(differs == null && Rows(units, "*note").Count == 0 && Rows(thousands, "*note").Count == 1 && units["ser_any"].AsDouble != M,
                $"person-time in thousands, of which a stratum has 7 events in 2, against the same in units {from}: {differs}");
            // as many events as person-time in every stratum: the rate is 1, the binomial model has the variance 0 and no note
            ParameterBag every = Direct(0.95, "1", new double[] { 50, 100 }, times, reference, screen);
            differs = First(every, 1e-12, ("stdr", 1), ("ser_any", 0), ("from_any", 1), ("to_any", 1), ("ser_small", Math.Sqrt(0.0625 / 50 + 0.5625 / 100)));
            Say(differs == null && Rows(every, "*note").Count == 0, $"as many events as person-time in every stratum {from}: {differs}; {Rows(every, "*note").Count} notes");
        }
        {
            // the check of the grid of the screen form, which is made before the analysis
            Type processor = typeof(StatsDirect.Builtins.Rates).Assembly.GetType("StatsDirect.TemplateProcessing.ValidationProcessor");
            System.Reflection.MethodInfo check = processor?.GetMethod("ValidatePersonTimeSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            string Checked(double[] events, double[] times, double[] reference)
            {
                if (check == null) return "the check was not found";
                StatsDirect.TemplateProcessing.ValidationResult result = (StatsDirect.TemplateProcessing.ValidationResult)check.Invoke(null, new object[] { Frame(events, times, reference) });
                return result.Validity.ToString() == "Valid" ? null : result.FailedValidationMessage ?? "not valid";
            }
            string more = Checked(new double[] { 3, 400 }, n2, w2), none = Checked(e2, new double[] { 0, 100 }, w2), nothing = Checked(e2, n2, new double[] { 0, 0 });
            Say(more == null && Checked(e2, n2, w2) == null, $"the check of the grid takes more events than person-time ({more})");
            Say(none != null && none.Contains("Person-time must be greater than zero") && nothing != null && nothing.Contains("Total reference group size must be greater than zero"),
                $"and it refuses a person-time of zero ({none}) and reference group sizes of zero ({nothing})");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  direct standardization of a stratum with more events than person-time");

        // ---- two crude rates: the person-times in other units, the groups the other way round, rates that are the same
        before = failures;
        {
            foreach (var (a, b, pt1, pt2) in new[] { (30, 60, 54308.7, 51477.5), (0, 12, 100.0, 140.0), (12, 0, 100.0, 140.0), (7, 7, 100.0, 100.0), (1, 1, 1.0, 1.0), (1000, 3, 50.0, 5000.0) })
            {
                ParameterBag report = Two(a, b, pt1, pt2, 0.95, true), months = Two(a, b, 12 * pt1, 12 * pt2, 0.95, true), turned = Two(b, a, pt2, pt1, 0.95, true);
                ParameterBag exact = Rows(report, "*exact")[0], exactMonths = Rows(months, "*exact")[0], exactTurned = Rows(turned, "*exact")[0];
                double Reciprocal(double x) => x == 0 ? double.PositiveInfinity : double.IsPositiveInfinity(x) ? 0 : 1 / x;
                string differs = First(months, 1e-12, ("ird", report["ird"].AsDouble / 12), ("ird_from", report["ird_from"].AsDouble / 12), ("ird_to", report["ird_to"].AsDouble / 12), ("p", report["p"].AsDouble),
                        ("irr", report["irr"].AsDouble), ("irr_from", report["irr_from"].AsDouble), ("irr_to", report["irr_to"].AsDouble))
                    ?? First(exactMonths, 1e-10, ("eor", exact["eor"].AsDouble), ("llf", exact["llf"].AsDouble), ("ulf", exact["ulf"].AsDouble), ("llm", exact["llm"].AsDouble), ("ulm", exact["ulm"].AsDouble), ("p2f", exact["p2f"].AsDouble), ("p1m", exact["p1m"].AsDouble));
                Say(differs == null, $"{a} and {b} cases in {pt1.ToString(inv)} and {pt2.ToString(inv)} years, and in as many months: {differs}");
                differs = First(turned, 1e-12, ("ird", -report["ird"].AsDouble), ("ird_from", -report["ird_to"].AsDouble), ("ird_to", -report["ird_from"].AsDouble), ("p", report["p"].AsDouble), ("xmh", report["xmh"].AsDouble),
                        ("irr", Reciprocal(report["irr"].AsDouble)), ("irr_from", Reciprocal(report["irr_to"].AsDouble)), ("irr_to", Reciprocal(report["irr_from"].AsDouble)))
                    ?? First(exactTurned, 1e-10, ("eor", Reciprocal(exact["eor"].AsDouble)), ("llf", Reciprocal(exact["ulf"].AsDouble)), ("ulf", Reciprocal(exact["llf"].AsDouble)), ("llm", Reciprocal(exact["ulm"].AsDouble)), ("ulm", Reciprocal(exact["llm"].AsDouble)),
                        ("p1f", exact["p1f"].AsDouble), ("p2f", exact["p2f"].AsDouble), ("p1m", exact["p1m"].AsDouble), ("p2m", exact["p2m"].AsDouble));
                Say(differs == null, $"{a} and {b} cases in {pt1.ToString(inv)} and {pt2.ToString(inv)} years, and the groups the other way round: {differs}");
            }
            // two rates that are the same: the difference is 0 and has limits, the chi-square is 0 and P is 1
            ParameterBag same = Two(7, 7, 100, 100, 0.95, true);
            double error = Math.Sqrt(14) / 100;
            string unlike = First(same, 1e-12, ("ird", 0), ("ird_from", -Deviate(0.95) * error), ("ird_to", Deviate(0.95) * error), ("xmh", 0), ("p", 1), ("irr", 1)) ?? First(Rows(same, "*exact")[0], 1e-12, ("eor", 1), ("p2f", 1), ("p2m", 1));
            Say(unlike == null, $"7 cases in 100 years against 7 in 100: {unlike}");
            // no cases in a group
            ParameterBag noneFirst = Two(0, 12, 100, 100, 0.95, true), noneSecond = Two(12, 0, 100, 100, 0.95, true);
            double limit = Math.Pow(0.025, 1.0 / 12);   // 12 of 12 have the probability 0.025 with this proportion
            unlike = First(noneFirst, 1e-10, ("irr", 0), ("irr_from", 0), ("irr_to", (1 - limit) / limit), ("ird", -0.12), ("ird_from", -0.12 - Deviate(0.95) * Math.Sqrt(12) / 100)) ?? First(Rows(noneFirst, "*exact")[0], 1e-10, ("eor", 0), ("llf", 0), ("ulf", (1 - limit) / limit), ("llm", 0), ("p1f", Math.Pow(0.5, 12)))
                ?? First(noneSecond, 1e-10, ("irr", double.PositiveInfinity), ("irr_from", limit / (1 - limit)), ("irr_to", double.PositiveInfinity)) ?? First(Rows(noneSecond, "*exact")[0], 1e-10, ("eor", double.PositiveInfinity), ("llf", limit / (1 - limit)), ("ulf", double.PositiveInfinity), ("ulm", double.PositiveInfinity));
            Say(unlike == null, $"12 cases in one group and none in the other: {unlike}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  two crude rates: other units of time, the groups the other way round, rates that are the same, a group without cases");

        // ---- two populations: strata and populations without events
        before = failures;
        foreach (string model in new[] { "poisson", "binomial" })
        {
            bool poisson = model == "poisson";
            double z = Deviate(0.95);
            // the row of a stratum, and that of all strata together, is the comparison of two crude rates
            double[] a = { 5, 0, 7, 0, 2 }, pt1 = { 100, 80, 60, 40, 30 }, b = { 9, 4, 0, 0, 3 }, pt2 = { 150, 90, 70, 50, 20 }, reference = { 1, 2, 3, 4, 0 };
            ParameterBag report = Standardized(0.95, "1", model, a, pt1, b, pt2, reference);
            List<ParameterBag> rows = Rows(report, "*rates");
            for (int i = 0; i <= 5; i++)
            {
                double x = i < 5 ? a[i] : a.Sum(), t1 = i < 5 ? pt1[i] : pt1.Sum(), y = i < 5 ? b[i] : b.Sum(), t2 = i < 5 ? pt2[i] : pt2.Sum();
                string differs;
                if (x + y == 0) differs = First(rows[i], 0, ("rr", M), ("lci", M), ("uci", M));
                else if (poisson)
                {
                    ParameterBag two = Two(x, y, t1, t2, 0.95, false);
                    differs = First(rows[i], 1e-13, ("rr", two["irr"].AsDouble), ("lci", two["irr_from"].AsDouble), ("uci", two["irr_to"].AsDouble));
                }
                else
                {
                    var (lower, upper) = ScoreLimits(x, t1, y, t2, z);
                    differs = First(rows[i], 1e-8, ("rr", y == 0 ? double.PositiveInfinity : x / t1 / (y / t2)), ("lci", lower), ("uci", upper));
                }
                differs ??= First(rows[i], 1e-14, ("wt", i < 5 ? reference[i] / 10 : 1));
                Say(differs == null, $"{model}, {x} events in {t1} against {y} in {t2}{(i == 5 ? " (all strata)" : "")}: {differs}");
            }
            // the plot has the rows that have a ratio with an end: the first, the second, all strata and the standardized ratio
            ChartDefinition chart = Rows(report, "*chart")[0]["chart"].AsObject as ChartDefinition;
            CorrelationOptions plot = chart?.ChartOptions as CorrelationOptions;
            Say(plot != null && plot.K == 7 && Enumerable.Range(1, 7).Select(i => plot.Odr[i] != M).SequenceEqual(new[] { true, true, false, false, true, true, true })
                && plot.Odr[3] == M && plot.Odrl[3] == M && plot.Odru[3] == M && plot.Odr[1] == rows[0]["rr"].AsDouble && plot.Odrl[1] == rows[0]["lci"].AsDouble && plot.Odru[6] == rows[5]["uci"].AsDouble
                && plot.Odr[7] == report["srr"].AsDouble && plot.Odrl[7] == report["srr_from"].AsDouble && plot.Titles[6] == "All (crude)" && plot.Titles[7] == "Standardized",
                $"{model}: the rows of the plot are those with a ratio that is not infinite ({(plot == null ? "no plot" : string.Join(" ", Enumerable.Range(1, 7).Select(i => plot.Odr[i] == M ? "none" : plot.Odr[i].ToString(inv))))})");

            // a population without events
            ParameterBag noneFirst = Standardized(0.95, "1", model, new double[] { 0, 0 }, p2, b2, q2, f2), noneSecond = Standardized(0.95, "1", model, a2, p2, new double[] { 0, 0 }, q2, f2),
                neither = Standardized(0.95, "1", model, new double[] { 0, 0 }, p2, new double[] { 0, 0 }, q2, f2);
            string unlike = First(noneFirst, 1e-12, ("sre", 0), ("sre_from", 0), ("sre_to", 0), ("srr", 0), ("srr_from", M), ("srr_to", M), ("cre", 0), ("cre_from", 0)) ?? First(Rows(noneFirst, "*rates")[2], 1e-12, ("rr", 0), ("lci", 0))
                ?? First(noneSecond, 1e-12, ("srne", 0), ("srr", double.PositiveInfinity), ("srr_from", M), ("srr_to", M)) ?? First(Rows(noneSecond, "*rates")[2], 1e-12, ("rr", double.PositiveInfinity), ("uci", double.PositiveInfinity))
                ?? First(neither, 1e-12, ("sre", 0), ("srne", 0), ("srr", M), ("srr_from", M), ("srr_to", M)) ?? First(Rows(neither, "*rates")[2], 1e-12, ("rr", M), ("lci", M), ("uci", M));
            Say(unlike == null, $"{model}: a population without events, and two: {unlike}");
            CorrelationOptions ofNone = (Rows(noneFirst, "*chart")[0]["chart"].AsObject as ChartDefinition)?.ChartOptions as CorrelationOptions;
            Say(ofNone != null && ofNone.Odr[4] == M && ofNone.Odrl[4] == M && ofNone.Odr[1] == 0, $"{model}: a standardized ratio of 0 is not in the plot");

            // one stratum: the standardized rates are the crude rates
            ParameterBag one = Standardized(0.95, "1000", model, new double[] { 5 }, new double[] { 100 }, new double[] { 9 }, new double[] { 150 }, new double[] { 17 });
            unlike = First(one, 1e-14, ("sre", one["cre"].AsDouble), ("srne", one["crne"].AsDouble), ("srr", Rows(one, "*rates")[0]["rr"].AsDouble), ("srr", Rows(one, "*rates")[1]["rr"].AsDouble));
            Say(unlike == null, $"{model}: one stratum: {unlike}");
        }
        // every subject with the event, with the binomial model: the limits are those of the score statistic
        {
            double z = Deviate(0.95);
            ParameterBag all = Standardized(0.95, "1", "binomial", new double[] { 12, 30 }, new double[] { 12, 30 }, new double[] { 20, 11 }, new double[] { 20, 40 }, f2);
            List<ParameterBag> rows = Rows(all, "*rates");
            string differs = First(rows[0], 1e-10, ("rr", 1), ("lci", 12 / (12 + z * z)), ("uci", (20 + z * z) / 20));
            Say(differs == null, $"12 of 12 against 20 of 20: {differs}");
            var (lower, upper) = ScoreLimits(30, 30, 11, 40, z);
            differs = First(rows[1], 1e-8, ("rr", 40.0 / 11), ("lci", lower), ("uci", upper));
            Say(differs == null && Math.Abs(Score(30, 30, 11, 40, rows[1]["lci"].AsDouble) - z) < 1e-6 && Math.Abs(Score(30, 30, 11, 40, rows[1]["uci"].AsDouble) + z) < 1e-6,
                $"30 of 30 against 11 of 40: {differs}; the score statistic at the limits is {Score(30, 30, 11, 40, rows[1]["lci"].AsDouble)} and {Score(30, 30, 11, 40, rows[1]["uci"].AsDouble)}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  two populations: strata and populations without events, the rows of the plot, one stratum, every subject with the event");

        // ---- millions of events, against the definitions
        before = failures;
        {
            double longest = 0;
            foreach (var (events, time, level) in new[] { (250000, 3e7, 0.95), (2000000, 1.5e6, 0.99), (40000000, 9e9, 0.9) })
            {
                var (lower, upper) = PoissonLimits(events, 0.5 * (1 - level));
                var (orMore, orFewer) = PoissonTails(events, events + 1500.0);
                string differs = null;
                longest = Math.Max(longest, Seconds(() => differs = First(Rate(events, time, level), 1e-9, ("rate", events / time), ("from", lower / time), ("to", upper / time))
                    ?? First(Smr(level, "1", events, new[] { 0.5 }, new[] { 2.0 * events + 3000 }, false), 1e-9, ("from", lower / (events + 1500.0)), ("to", upper / (events + 1500.0)), ("p_hi", orMore), ("p_lo", orFewer))));
                Say(differs == null, $"{events} events: {differs}");
            }
            foreach (var (a, b, pt1, pt2) in new[] { (1000000, 1000000, 3e6, 3.1e6), (2500000, 1200, 7.5e7, 31000.0), (40, 3000000, 25.0, 1.9e6) })
            {
                double[] nine = Conditional(a, b, pt1, pt2, 0.95);
                double z = Deviate(0.95), difference = a / pt1 - b / pt2, error = Math.Sqrt(a / pt1 / pt1 + b / pt2 / pt2);
                double share = pt1 / (pt1 + pt2), m = (double)a + b;
                double chi = (a - m * share) * (a - m * share) / (m * share * (1 - share));
                string differs = null;
                longest = Math.Max(longest, Seconds(() => differs = First(Two(a, b, pt1, pt2, 0.95, false), 1e-9, ("irr", a / pt1 / (b / pt2)), ("irr_from", nine[1]), ("irr_to", nine[2]), ("ird_from", difference - z * error), ("ird_to", difference + z * error),
                    ("xmh", chi), ("p", 2 * NormalAbove(Math.Sqrt(chi))))));
                Say(differs == null, $"{a} and {b} cases in {pt1.ToString(inv)} and {pt2.ToString(inv)} years: {differs}");
            }
            {
                // direct standardization and two populations with millions of events in each stratum
                double[] events = { 2500000, 3000000, 1200000 }, times = { 2e8, 3e8, 4e7 }, reference = { 1e6, 2e6, 5e5 }, others = { 2400000, 3100000, 1100000 }, otherTimes = { 2.1e8, 2.9e8, 4.2e7 };
                double z = Deviate(0.95);
                double rate = 0, variance = 0, other = 0, otherVariance = 0;
                for (int i = 0; i < 3; i++)
                {
                    double w = reference[i] / 3.5e6;
                    rate += w * events[i] / times[i]; variance += w * w * events[i] / times[i] / times[i];
                    other += w * others[i] / otherTimes[i]; otherVariance += w * w * others[i] / otherTimes[i] / otherTimes[i];
                }
                var (lower, upper) = PoissonLimits(6700000, 0.025);
                double[] first = Conditional(2500000, 2400000, 2e8, 2.1e8, 0.95);
                string differs = null;
                longest = Math.Max(longest, Seconds(() => differs = First(Direct(0.95, "100000", events, times, reference, false), 1e-9, ("stdr", 1e5 * rate), ("from_small", 1e5 * (rate - z * Math.Sqrt(variance))),
                        ("from_dobson", 1e5 * (rate + Math.Sqrt(variance / 6700000) * (lower - 6700000))), ("to_dobson", 1e5 * (rate + Math.Sqrt(variance / 6700000) * (upper - 6700000))))
                    ?? First(Standardized(0.95, "100000", "poisson", events, times, others, otherTimes, reference), 1e-9, ("sre", 1e5 * rate), ("srne", 1e5 * other), ("srr", rate / other),
                        ("srr_from", Math.Exp(Math.Log(rate / other) - z * Math.Sqrt(variance / rate / rate + otherVariance / other / other))), ("cre_from", 1e5 * lower / 5.4e8))
                    ?? First(Rows(Standardized(0.95, "100000", "poisson", events, times, others, otherTimes, reference), "*rates")[0], 1e-9, ("lci", first[1]), ("uci", first[2]))));
                Say(differs == null, $"strata of millions of events: {differs}");
            }
            Say(longest < 2, $"the report that takes longest takes {longest:F2} seconds");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  millions of events, against the definitions");
    }
}

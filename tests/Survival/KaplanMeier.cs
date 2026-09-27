// Kaplan-Meier survival estimates (Survival.RptKaplan): for each group the table of times with the numbers at risk, dead and censored, the
// survival proportion S with its standard error, the cumulative hazard H = -log S with its standard error, the median with two confidence
// intervals, the mean with its interval, and the rows saved to the worksheet.  Everything is worked out here from the definitions, from the
// records themselves.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    // a record: time, the code of the death/event column (0 censored, 1 dead, more than 1 that number of deaths), and the label of its group
    internal sealed record Life(double Time, double Code, string Group);

    private sealed class Step
    {
        public double Time; public int Risk, Dead, Censored; public double S, VarS, H, VarH; public System.Numerics.BigInteger Top, Bottom;
        public bool HalfOrLess => 2 * Top <= Bottom;
        public bool ExactlyHalf => 2 * Top == Bottom;
    }

    // the product-limit estimate of one group, from the definitions
    private static List<Step> ProductLimit(IEnumerable<(double time, bool dead)> subjects)
    {
        var all = subjects.ToList();
        List<Step> steps = new();
        double s = 1, sum = 0;
        System.Numerics.BigInteger top = 1, bottom = 1;
        int risk = all.Count;
        bool ended = false;
        foreach (var at in all.GroupBy(v => v.time).OrderBy(g => g.Key))
        {
            int dead = at.Count(v => v.dead), censored = at.Count() - dead;
            Step step = new() { Time = at.Key, Risk = risk, Dead = dead, Censored = censored };
            s *= (risk - dead) / (double)risk;
            top *= risk - dead; bottom *= risk;
            step.Top = top; step.Bottom = bottom;
            if (risk - dead > 0) sum += dead / ((double)risk * (risk - dead)); else ended = true;
            step.S = s;
            if (s > 0 && !ended) { step.VarS = s * s * sum; step.H = -Math.Log(s); step.VarH = sum; }
            else { step.VarS = M; step.H = double.PositiveInfinity; step.VarH = M; }
            steps.Add(step);
            risk -= dead + censored;
        }
        return steps;
    }

    private static ParameterBag KaplanInputs(List<Life> records, double gamma, bool save, bool withGroups)
    {
        ParameterBag bag = new();
        bag.AddInput("gamma", gamma);
        bag.AddInput("times", new DataFrame(new DoubleVariable(records.Select(r => r.Time).ToArray(), "Time")));
        bag.AddInput("deaths", new DataFrame(new DoubleVariable(records.Select(r => r.Code).ToArray(), "Death")));
        if (withGroups) bag.AddInput("groups", new DataFrame(Classifier("Group", records.Select(r => r.Group).ToArray())));
        bag.AddInput("save", save);
        return bag;
    }

    private static void KaplanCase(string title, List<Life> records, double gamma, bool withGroups)
    {
        int before = failures;
        try
        {
            ParameterBag o = Survival.RptKaplan(new Plain(), KaplanInputs(records, gamma, true, withGroups)).ParameterBag;
            double z = NormalQuantile(1 - (1 - gamma) / 2);
            // the records that can be used: a time, a code and (with groups) a group; the groups in the order in which they are first met
            List<Life> used = records.Where(r => r.Time != M && r.Code != M && (!withGroups || r.Group != null)).ToList();
            List<string> groups = withGroups ? used.Select(r => r.Group).Distinct().ToList() : new List<string> { null };
            List<ParameterBag> given = Rows(o, "*group");
            Say(given.Count == groups.Count, title + $": a table for each of the {groups.Count} groups ({given.Count} given)");
            int blank = records.Count - used.Count;
            List<ParameterBag> note = Rows(o, "*note");
            Say(blank == 0 ? note.Count == 0 : note.Count == 1 && note[0]["note"].AsString.StartsWith(blank.ToString() + " record"), title + $": the note of the {blank} records left out");
            DataFrame saved = o["results"].AsDataFrame;
            for (int g = 0; g < Math.Min(groups.Count, given.Count); g++)
            {
                string name = title + (withGroups ? $", group {groups[g]}" : "");
                var subjects = used.Where(r => !withGroups || r.Group == groups[g])
                    .SelectMany(r => Enumerable.Repeat((r.Time, r.Code >= 1), r.Code > 1 ? (int)r.Code : 1)).ToList();
                List<Step> steps = ProductLimit(subjects);
                if (withGroups)
                {
                    List<ParameterBag> label = Rows(given[g], "*grp");
                    Say(label.Count == 1 && label[0]["grp"].AsString == "Group = " + groups[g], name + ": the label of the group" + (label.Count == 1 ? $" (\"{label[0]["grp"].AsString}\")" : ""));
                }
                List<ParameterBag> rows = Rows(given[g], "*est");
                Say(rows.Count == steps.Count, name + $": a row for each of the {steps.Count} times ({rows.Count} given)");
                if (rows.Count != steps.Count) continue;
                double worstCount = 0, worstS = 0, worstSe = 0, worstH = 0;
                for (int i = 0; i < steps.Count; i++)
                {
                    Step e = steps[i]; ParameterBag r = rows[i];
                    worstCount = Math.Max(worstCount, Math.Abs(r["time"].AsDouble - e.Time) + Math.Abs(r["risk"].AsInt32 - e.Risk) + Math.Abs(r["dead"].AsInt32 - e.Dead) + Math.Abs(r["cen"].AsInt32 - e.Censored));
                    worstS = Math.Max(worstS, Math.Abs(r["s"].AsDouble - e.S));
                    worstSe = Math.Max(worstSe, e.VarS == M ? (r["ses"].AsDouble == M ? 0 : 1) : Math.Abs(r["ses"].AsDouble - Math.Sqrt(e.VarS)));
                    worstH = Math.Max(worstH, double.IsInfinity(e.H) ? (double.IsPositiveInfinity(r["h"].AsDouble) && r["seh"].AsDouble == M ? 0 : 1)
                        : Math.Abs(r["h"].AsDouble - e.H) + Math.Abs(r["seh"].AsDouble - Math.Sqrt(e.VarH)));
                }
                Check(name + ": times and numbers at risk, dead and censored", worstCount, 0);
                Check(name + ": S", worstS, 1e-12);
                Check(name + ": standard error of S", worstSe, 1e-12);
                Check(name + ": H and its standard error", worstH, 1e-12);

                // the median: the first time at which S is a half or less; where S is exactly a half from that time to the next time of
                // death, the middle of the two times
                Step median = steps.FirstOrDefault(e => e.HalfOrLess);
                double middle = median == null ? M : median.Time;
                if (median != null && median.ExactlyHalf)
                {
                    Step next = steps.FirstOrDefault(e => e.Time > median.Time && e.Dead > 0);
                    if (next != null) middle = (median.Time + next.Time) / 2;
                }
                string printed = given[g]["med"].AsString;
                Say(median == null ? printed == "can not estimate" : Math.Abs(double.Parse(printed) - middle) <= 1e-9 * Math.Max(1, middle), name + $": median ({printed}; S is first a half or less at {(median == null ? "no time" : median.Time.ToString())}{(median != null && median.ExactlyHalf ? ", where it is exactly a half" : "")})");
                // the interval from the slope of the curve at the median: the variance of S at the median over the square of the slope, which
                // is taken between the last time at which S is a half plus a margin or more and the first at which it is a half less the
                // margin or less; the margin is 0.05 at every confidence level
                double margin = 0.05;
                int lowerAt = steps.FindIndex(e => 2 * e.Top * 1000000 <= e.Bottom * (System.Numerics.BigInteger)Math.Round((1 - 2 * margin) * 1000000));
                int upperAt = steps.FindLastIndex(e => 2 * e.Top * 1000000 >= e.Bottom * (System.Numerics.BigInteger)Math.Round((1 + 2 * margin) * 1000000));
                if (median != null && median.VarS != M && lowerAt >= 0 && upperAt >= 0 && steps[lowerAt].Time != steps[upperAt].Time)
                {
                    double slope = (steps[upperAt].S - steps[lowerAt].S) / (steps[lowerAt].Time - steps[upperAt].Time), se = Math.Sqrt(median.VarS) / Math.Abs(slope);
                    Check(name + ": limits of the median from the slope of the curve", Relative(given[g]["all"].AsDouble, middle - z * se) + Relative(given[g]["aul"].AsDouble, middle + z * se), 1e-9);
                }
                else Say(given[g]["all"].AsDouble == M && given[g]["aul"].AsDouble == M, name + ": no limits of the median from the slope of the curve");
                // the interval of Brookmeyer and Crowley: the times at which S does not differ from a half by more than z standard errors,
                // and on to the time of death next after the last of them; no upper limit if there is no such time of death, or if nobody
                // is left after it, when S has no standard error.  A limit that is not reached is given as infinite
                var within = steps.Where(e => e.VarS != M && e.VarS > 0 && Math.Abs(e.S - 0.5) / Math.Sqrt(e.VarS) <= z).ToList();
                Step after = within.Count > 0 ? steps.FirstOrDefault(e => e.Time > within.Last().Time && e.Dead > 0) : null;
                bool reached = median != null || within.Count > 0;
                double lower = within.Count > 0 ? within.First().Time : reached ? double.NegativeInfinity : M;
                double upper = after != null && after.VarS != M ? after.Time : reached ? double.PositiveInfinity : M;
                Check(name + $": Brookmeyer-Crowley limits ({Shown(given[g]["bll"].AsDouble)} to {Shown(given[g]["bul"].AsDouble)}; from the definition {Shown(lower)} to {Shown(upper)})", Same(given[g]["bll"].AsDouble, lower) + Same(given[g]["bul"].AsDouble, upper), 0);

                // the mean: the area under S from 0 to the greatest time; its variance from the areas beyond each time with a death
                Step lastDeath = steps.LastOrDefault(e => e.Dead > 0);
                double greatest = steps.Last().Time, mean = steps[0].Time;
                for (int i = 0; i + 1 < steps.Count; i++) mean += steps[i].S * (steps[i + 1].Time - steps[i].Time);
                Check(name + ": mean", Relative(given[g]["mu"].AsDouble, mean), 1e-12);
                double variance = 0, deaths = 0;
                for (int i = 0; i < steps.Count; i++)
                {
                    if (steps[i].Dead == 0) continue;
                    double beyond = 0;
                    for (int l = i; l + 1 < steps.Count; l++) beyond += steps[l].S * (steps[l + 1].Time - steps[l].Time);
                    if (steps[i].Risk - steps[i].Dead > 0) variance += beyond * beyond * steps[i].Dead / ((double)steps[i].Risk * (steps[i].Risk - steps[i].Dead));
                    deaths += steps[i].Dead;
                }
                if (deaths > 1 && variance > 0)
                {
                    double se = Math.Sqrt(variance * deaths / (deaths - 1));
                    Check(name + ": limits of the mean", Relative(given[g]["ll"].AsDouble, mean - z * se) + Relative(given[g]["ul"].AsDouble, mean + z * se), 1e-10);
                }
                else Say(given[g]["ll"].AsDouble == M && given[g]["ul"].AsDouble == M, name + ": no limits of the mean");
                string limit = lastDeath == null ? "no deaths" : lastDeath.Time != greatest ? "limit" : "";
                Say(limit == "" ? given[g]["lim"].AsString == "" : given[g]["lim"].AsString.Contains(limit), name + ": the note on the limit of the mean");

                // what is saved: a row for each subject, in order of time, with the figures of its time
                int first = g * 10;
                if (saved.VariableCount < first + 10) { Say(false, name + ": ten columns saved for the group"); continue; }
                double[] savedTime = ((DoubleVariable)saved.Variables[first]).Data, savedS = ((DoubleVariable)saved.Variables[first + 2]).Data;
                double[] savedLow = ((DoubleVariable)saved.Variables[first + 4]).Data, savedHigh = ((DoubleVariable)saved.Variables[first + 5]).Data;
                string[] savedCode = ((StringVariable)saved.Variables[first + 1]).Data;
                var ordered = subjects.OrderBy(v => v.Item1).ToList();
                Say(savedTime.Length == ordered.Count, name + $": a row saved for each of the {ordered.Count} subjects ({savedTime.Length} saved)");
                if (savedTime.Length != ordered.Count) continue;
                double worstSaved = 0, worstLimit = 0; int deadSaved = 0;
                for (int i = 0; i < ordered.Count; i++)
                {
                    Step e = steps.First(v => v.Time == ordered[i].Item1);
                    worstSaved = Math.Max(worstSaved, Math.Abs(savedTime[i] - e.Time) + Math.Abs(savedS[i] - e.S));
                    if (savedCode[i] == "1") deadSaved++;
                    if (e.S < 1 && e.S > 0 && e.VarH != M)
                    {
                        // limits of S from those of log(-log S), whose variance is that of H over the square of log S
                        double centre = Math.Log(-Math.Log(e.S)), se = Math.Sqrt(e.VarH) / Math.Abs(Math.Log(e.S));
                        worstLimit = Math.Max(worstLimit, Math.Abs(savedLow[i] - Math.Exp(-Math.Exp(centre + z * se))) + Math.Abs(savedHigh[i] - Math.Exp(-Math.Exp(centre - z * se))));
                    }
                }
                Check(name + ": saved times and S", worstSaved, 1e-12);
                Check(name + ": saved limits of S", worstLimit, 1e-10);
                Say(deadSaved == subjects.Count(v => v.Item2), name + ": as many deaths saved as there are");
            }
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    private static double Same(double given, double expected) =>
        given == expected || (double.IsFinite(expected) && double.IsFinite(given) && given != M && Math.Abs(given - expected) <= 1e-9 * Math.Max(1, Math.Abs(expected))) ? 0 : 1;

    private static string Shown(double value) => value == M ? "none" : value.ToString("G10");

    // the median and the limits of Brookmeyer and Crowley against R's, for sets of records that a script drew and put through R
    // (KaplanMeierBenchmarks.cs).  Where R gives no limit the program prints infinity, or nothing if there is neither a median nor a limit
    private static void KaplanBenchmarks()
    {
        Console.WriteLine();
        Console.WriteLine("Kaplan-Meier: the median and its limits against R's");
        int before = failures;
        for (int b = 0; b < Benchmarks.Length; b++)
        {
            Benchmark e = Benchmarks[b];
            string name = $"benchmark {b + 1} ({e.Time.Length} records, {e.Level:P0})";
            try
            {
                List<Life> records = e.Time.Select((t, i) => new Life(t, e.Code[i], "a")).ToList();
                ParameterBag given = Rows(Survival.RptKaplan(new Plain(), KaplanInputs(records, e.Level, false, false)).ParameterBag, "*group")[0];
                string printed = given["med"].AsString;
                Say(e.Median == M ? printed == "can not estimate" : printed != "can not estimate" && Same(double.Parse(printed), e.Median) == 0, name + $": median ({printed}; R's {Shown(e.Median)})");
                bool reached = e.Median != M || e.Lower != M;
                double lower = e.Lower != M ? e.Lower : reached ? double.NegativeInfinity : M, upper = e.Upper != M ? e.Upper : reached ? double.PositiveInfinity : M;
                Check(name + $": limits ({Shown(given["bll"].AsDouble)} to {Shown(given["bul"].AsDouble)}; R's {Shown(e.Lower)} to {Shown(e.Upper)})", Same(given["bll"].AsDouble, lower) + Same(given["bul"].AsDouble, upper), 0);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + name + ": " + Message(ex)); }
        }
        if (failures == before) Console.WriteLine($"ok    {Benchmarks.Length} sets");
    }

    private static List<Life> SurvivalData(System.Random random, int n, string[] groups, bool ties, bool frequencies, double censoring)
    {
        List<Life> records = new();
        for (int i = 0; i < n; i++)
        {
            int g = random.Next(groups.Length);
            double time = -Math.Log(1 - random.NextDouble()) * 10 / (1 + 0.6 * g);
            time = ties ? Math.Ceiling(time) : Math.Round(time, 3) + (i + 1) * 1e-5;
            double code = random.NextDouble() < censoring ? 0 : frequencies && i % 4 == 0 ? 2 + i % 2 : 1;
            records.Add(new Life(time, code, groups[g]));
        }
        return records;
    }

    private static void KaplanMeier()
    {
        Console.WriteLine("Kaplan-Meier survival estimates");
        System.Random random = new(71);
        for (int set = 1; set <= 36; set++)
        {
            int n = 8 + random.Next(set % 3 == 0 ? 150 : 40);
            string[] groups = set % 4 == 0 ? new[] { "treated", "control", "other" } : set % 2 == 0 ? new[] { "b", "a" } : new[] { "only" };
            bool ties = set % 3 != 1, frequencies = set % 5 == 0;
            double censoring = new[] { 0.0, 0.3, 0.6 }[set % 3];
            double gamma = new[] { 0.95, 0.9, 0.99 }[set % 3];
            List<Life> records = SurvivalData(random, n, groups, ties, frequencies, censoring);
            KaplanCase($"set {set}: {n} records, {groups.Length} group{(groups.Length > 1 ? "s" : "")}{(ties ? ", tied times" : "")}{(frequencies ? ", codes above 1" : "")}, {censoring:P0} censored, {gamma:P0}", records, gamma, groups.Length > 1 || set % 7 == 0);
        }

        // records with a blank: each should be left out, whichever cell is blank
        Console.WriteLine();
        Console.WriteLine("Kaplan-Meier: records with a blank cell");
        foreach (string blank in new[] { "time", "code", "group", "first group", "one of each" })
        {
            List<Life> records = SurvivalData(random, 40, new[] { "x", "y" }, true, false, 0.3);
            if (blank == "time") { records[3] = records[3] with { Time = M }; records[17] = records[17] with { Time = M }; }
            if (blank == "code") { records[5] = records[5] with { Code = M }; records[20] = records[20] with { Code = M }; }
            if (blank == "group") { records[7] = records[7] with { Group = null }; records[30] = records[30] with { Group = null }; }
            if (blank == "first group") records[0] = records[0] with { Group = null };
            if (blank == "one of each") { records[2] = records[2] with { Time = M }; records[9] = records[9] with { Code = M }; records[12] = records[12] with { Group = null }; }
            KaplanCase($"blank {blank}", records, 0.95, true);
        }
        {
            List<Life> records = SurvivalData(random, 30, new[] { "only" }, true, false, 0.3);
            records[4] = records[4] with { Time = M }; records[11] = records[11] with { Code = M };
            KaplanCase("blank time and blank code, without groups", records, 0.95, false);
        }

        // at the limits
        Console.WriteLine();
        Console.WriteLine("Kaplan-Meier: at the limits");
        KaplanCase("no deaths", Enumerable.Range(1, 8).Select(i => new Life(i, 0, "a")).ToList(), 0.95, false);
        KaplanCase("every subject dead, one at a time", Enumerable.Range(1, 8).Select(i => new Life(i * 1.5, 1, "a")).ToList(), 0.95, false);
        KaplanCase("one record", new List<Life> { new(4, 1, "a") }, 0.95, false);
        KaplanCase("every subject at the same time", Enumerable.Range(1, 6).Select(i => new Life(3, i % 2, "a")).ToList(), 0.95, false);
        KaplanCase("the last subject censored after the last death", new List<Life> { new(1, 1, "a"), new(2, 1, "a"), new(3, 0, "a"), new(5, 1, "a"), new(9, 0, "a") }, 0.95, false);
        KaplanCase("a time of nothing", new List<Life> { new(0, 1, "a"), new(0, 0, "a"), new(2, 1, "a"), new(3, 1, "a") }, 0.95, false);

        // S exactly a half from one time to the next time of death: the median is the middle of the two
        Console.WriteLine();
        Console.WriteLine("Kaplan-Meier: a survival proportion of exactly a half");
        foreach (int n in new[] { 2, 4, 6, 10, 14, 22, 30, 50, 98, 200 })
        {
            KaplanCase($"{n} subjects, none censored", Enumerable.Range(1, n).Select(i => new Life(i * 1.25, 1, "a")).ToList(), 0.95, false);
            KaplanCase($"{n} subjects, none censored, a censored time between the two middle times", Enumerable.Range(1, n).Select(i => new Life(i * 1.25, 1, "a")).Append(new Life(n / 2 * 1.25 + 0.5, 0, "a")).Append(new Life(0.5, 0, "a")).ToList(), 0.95, false);
        }
        KaplanCase("half dead and the rest censored later: S is a half to the end", new List<Life> { new(1, 1, "a"), new(2, 1, "a"), new(3, 1, "a"), new(4, 0, "a"), new(5, 0, "a"), new(6, 0, "a") }, 0.95, false);
        KaplanCase("S a half by censoring and deaths together", new List<Life> { new(1, 0, "a"), new(2, 1, "a"), new(3, 0, "a"), new(4, 1, "a"), new(7, 1, "a"), new(9, 1, "a") }, 0.95, false);

        KaplanBenchmarks();
    }
}

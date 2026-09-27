// The log-rank and generalised Wilcoxon tests (Survival.RptLogRank): for each stratum, and for the strata together, the deaths observed and
// expected in each group, the rank statistics with their matrix of variances and covariances, the chi-square tests of any difference and of
// trend, the hazard ratios with their approximate limits and, for two groups, the hazard ratio by conditional maximum likelihood with its
// exact limits.  Everything is worked out here from the definitions.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    internal sealed record Subject(double Time, double Code, string Group, string Stratum);

    // the quadratic form u' V- u in the generalised inverse of V, and the rank of V
    private static double QuadraticForm(double[] u, double[,] v, out int rank)
    {
        int k = u.Length;
        double[] roots = LatentRoots(v, out double[,] vectors);
        double greatest = roots.Max(Math.Abs), form = 0;
        rank = 0;
        for (int r = 0; r < k; r++)
        {
            if (roots[r] <= 1e-9 * Math.Max(1e-300, greatest)) continue;
            rank++;
            double along = 0;
            for (int i = 0; i < k; i++) along += vectors[i, r] * u[i];
            form += along * along / roots[r];
        }
        return form;
    }

    private sealed class RankTest
    {
        public double[] Observed, Expected, U; public double[,] V;
        public List<(double a, double m1, double n1, double n0)> Tables = new();
    }

    // one stratum: subjects as (time, dead, group number from 0); weight 0 log-rank, 1 Peto-Prentice, 2 Gehan-Breslow, 3 Tarone-Ware
    private static RankTest Ranks(List<(double time, bool dead, int group)> subjects, int groups, int weight)
    {
        RankTest t = new() { Observed = new double[groups], Expected = new double[groups], U = new double[groups], V = new double[groups, groups] };
        double survivor = 1;
        int total = subjects.Count;
        foreach (double time in subjects.Select(s => s.time).Distinct().OrderBy(v => v))
        {
            double[] risk = new double[groups], dead = new double[groups];
            foreach (var s in subjects)
            {
                if (s.time >= time) risk[s.group]++;
                if (s.time == time && s.dead) dead[s.group]++;
            }
            double n = risk.Sum(), d = dead.Sum();
            // The weight of Peto and Prentice is defined for times without ties: each death multiplies the estimate of the proportion
            // surviving by m / (m + 1), m being the number at risk just before it, and the weight of a death is the estimate after it.
            // The deaths of a time are taken here one after another, and the weight of the time is that of the first of them
            double afterFirst = d > 0 ? survivor * n / (n + 1) : survivor;
            for (int i = 0; i < d; i++) survivor *= (n - i) / (n - i + 1);
            double w = weight switch { 0 => 1, 1 => afterFirst, 2 => n / (total + 1), _ => Math.Sqrt(n) };
            for (int g = 0; g < groups; g++)
            {
                t.Observed[g] += dead[g];
                t.Expected[g] += d * risk[g] / n;
                t.U[g] += w * (dead[g] - d * risk[g] / n);
                if (n > 1)
                    for (int h = 0; h < groups; h++)
                        t.V[g, h] += w * w * d * (n - d) / (n * n * (n - 1)) * (g == h ? risk[g] * (n - risk[g]) : -risk[g] * risk[h]);
            }
            if (groups == 2) t.Tables.Add((dead[0], d, risk[0], risk[1]));
        }
        return t;
    }

    // the distribution of the number of deaths in the first group, over the tables, given the totals of each: the probabilities when the
    // hazard ratio is 1, and the least number possible
    private static double[] DeathsDistribution(List<(double a, double m1, double n1, double n0)> tables, out int least)
    {
        double[] all = { 1.0 };
        least = 0;
        foreach (var t in tables)
        {
            int low = (int)Math.Max(0, t.m1 - t.n0), high = (int)Math.Min(t.n1, t.m1);
            if (high == low) { least += low; continue; }
            double[] p = new double[high - low + 1];
            for (int a = low; a <= high; a++)
                p[a - low] = Math.Exp(LogGamma(t.n1 + 1) - LogGamma(a + 1) - LogGamma(t.n1 - a + 1) + LogGamma(t.n0 + 1) - LogGamma(t.m1 - a + 1) - LogGamma(t.n0 - t.m1 + a + 1)
                    - (LogGamma(t.n1 + t.n0 + 1) - LogGamma(t.m1 + 1) - LogGamma(t.n1 + t.n0 - t.m1 + 1)));
            double[] next = new double[all.Length + p.Length - 1];
            for (int i = 0; i < all.Length; i++) for (int j = 0; j < p.Length; j++) next[i + j] += all[i] * p[j];
            all = next;
            least += low;
        }
        return all;
    }

    // the probabilities at a hazard ratio of psi
    private static double[] Tilt(double[] atOne, double logPsi)
    {
        double[] logs = atOne.Select((p, i) => p > 0 ? Math.Log(p) + i * logPsi : double.NegativeInfinity).ToArray();
        double greatest = logs.Max();
        double[] q = logs.Select(l => Math.Exp(l - greatest)).ToArray();
        double sum = q.Sum();
        return q.Select(v => v / sum).ToArray();
    }

    private static double Solve(Func<double, double> f, double low, double high)
    {
        double fl = f(low);
        for (int i = 0; i < 200; i++) { double mid = (low + high) / 2; if (Math.Sign(f(mid)) == Math.Sign(fl)) low = mid; else high = mid; }
        return (low + high) / 2;
    }

    private static ParameterBag LogRankInputs(List<Subject> records, double gamma, int method, bool stratified, double[] scores)
    {
        ParameterBag bag = new();
        bag.AddInput("gid", new DataFrame(Classifier("Group", records.Select(r => r.Group).ToArray())));
        bag.AddInput("times", new DataFrame(new DoubleVariable(records.Select(r => r.Time).ToArray(), "Time")));
        bag.AddInput("deaths", new DataFrame(new DoubleVariable(records.Select(r => r.Code).ToArray(), "Death")));
        if (stratified) bag.AddInput("strata", new DataFrame(Classifier("Centre", records.Select(r => r.Stratum).ToArray())));
        bag.AddInput("gamma", gamma);
        bag.AddInput("wt_method", method.ToString());
        if (scores != null) bag.AddInput("group_scores", new DataFrame(new DoubleVariable((double[])scores.Clone(), "scores")));
        return bag;
    }

    private static void LogRankCase(string title, List<Subject> records, double gamma, int method, bool stratified, double[] scores)
    {
        int before = failures;
        try
        {
            ParameterBag o = Survival.RptLogRank(new NoProgress(), LogRankInputs(records, gamma, method, stratified, scores)).ParameterBag;
            double z = NormalQuantile(1 - (1 - gamma) / 2);
            // groups and strata are numbered in the order in which they are first met among the records that have one
            List<string> groups = records.Where(r => r.Group != null).Select(r => r.Group).Distinct().ToList();
            List<string> strata = stratified ? records.Where(r => r.Stratum != null).Select(r => r.Stratum).Distinct().ToList() : new List<string> { null };
            int k = groups.Count;
            List<Subject> used = records.Where(r => r.Time != M && r.Code != M && r.Group != null && (!stratified || r.Stratum != null)).ToList();
            int blank = records.Count - used.Count;
            List<ParameterBag> note = Rows(o, "*note");
            Say(blank == 0 ? note.Count == 0 : note.Count == 1 && note[0]["note"].AsString.StartsWith(blank.ToString() + " record"), title + $": the note of the {blank} records left out");
            List<ParameterBag> outer = Rows(o, "*outer");
            Say(outer.Count == 2 * strata.Count, title + $": a log-rank and a Wilcoxon test for each of the {strata.Count} strata ({outer.Count} given)");
            if (outer.Count != 2 * strata.Count) return;
            double[] observedAll = new double[k], expectedAll = new double[k];
            double[][] uAll = { new double[k], new double[k] };
            double[][,] vAll = { new double[k, k], new double[k, k] };
            List<(double a, double m1, double n1, double n0)> tablesAll = new();
            for (int s = 0; s < strata.Count; s++)
            {
                var subjects = used.Where(r => !stratified || r.Stratum == strata[s])
                    .SelectMany(r => Enumerable.Repeat((r.Time, r.Code >= 1, groups.IndexOf(r.Group)), r.Code > 1 ? (int)r.Code : 1)).ToList();
                for (int test = 0; test < 2; test++)
                {
                    string name = title + (stratified ? $", stratum {strata[s]}" : "") + (test == 0 ? ", log-rank" : ", Wilcoxon");
                    ParameterBag given = outer[2 * s + test];
                    RankTest expected = Ranks(subjects, k, test == 0 ? 0 : method);
                    if (test == 0)
                    {
                        List<ParameterBag> rows = Rows(given, "*groups");
                        double worst = rows.Count == k ? 0 : double.PositiveInfinity;
                        for (int g = 0; g < k && rows.Count == k; g++)
                        {
                            if (rows[g]["grp"].AsString != $"{g + 1} (Group = {groups[g]})") worst = double.PositiveInfinity;
                            worst = Math.Max(worst, Math.Abs(rows[g]["obs"].AsDouble - expected.Observed[g]) + Math.Abs(rows[g]["ext"].AsDouble - expected.Expected[g]));
                            observedAll[g] += expected.Observed[g]; expectedAll[g] += expected.Expected[g];
                        }
                        Check(name + ": the groups with their labels, and the deaths observed and expected", worst, 1e-9);
                        tablesAll.AddRange(expected.Tables);
                    }
                    List<ParameterBag> rank = Rows(given, "*rank"), covariance = Rows(given, "*covar");
                    double worstU = 0, worstV = 0;
                    for (int g = 0; g < k; g++)
                    {
                        worstU = Math.Max(worstU, Math.Abs(rank[g]["cell"].AsDouble - expected.U[g]));
                        List<ParameterBag> cells = Rows(covariance[g], "*mat");
                        for (int h = 0; h < k; h++) worstV = Math.Max(worstV, Math.Abs(cells[h]["cell"].AsDouble - expected.V[h, g]));
                        uAll[test][g] += expected.U[g];
                        for (int h = 0; h < k; h++) vAll[test][g, h] += expected.V[g, h];
                    }
                    Check(name + ": rank statistics", worstU, 1e-9);
                    Check(name + ": their variances and covariances", worstV, 1e-9);
                    double chi = QuadraticForm(expected.U, expected.V, out int rank1);
                    if (rank1 == k - 1)
                    {
                        Check(name + ": chi-square", Relative(given["chi"].AsDouble, chi), 1e-8);
                        Check(name + ": its P", Math.Abs(given["p"].AsDouble - ChiSquareUpper(chi, k - 1)), 1e-7);
                    }
                    else Say(given["chi"].AsDouble == M, name + $": no chi-square when the matrix has rank {rank1}, not {k - 1}");
                    if (k > 2)
                    {
                        double top = 0, bottom = 0;
                        for (int g = 0; g < k; g++) { top += scores[g] * expected.U[g]; for (int h = 0; h < k; h++) bottom += scores[g] * expected.V[g, h] * scores[h]; }
                        ParameterBag trend = Rows(given, "*trends")[0];
                        Check(name + ": chi-square for trend and its P", Relative(trend["trend"].AsDouble, top * top / bottom) + Math.Abs(trend["p_trend"].AsDouble - ChiSquareUpper(top * top / bottom, 1)), 1e-7);
                    }
                    if (stratified && s == strata.Count - 1)
                    {
                        ParameterBag together = Rows(given, "*strata")[0];
                        double chiAll = QuadraticForm(uAll[test], vAll[test], out int rankAll);
                        if (rankAll == k - 1)
                            Check(name + ": chi-square of the strata together and its P", Relative(together["chi_strata"].AsDouble, chiAll) + Math.Abs(together["p_strata"].AsDouble - ChiSquareUpper(chiAll, k - 1)), 1e-7);
                        List<ParameterBag> sums = Rows(together, "*stratum");
                        double worst = 0;
                        for (int g = 0; g < k; g++) worst = Math.Max(worst, Math.Abs(sums[g]["res"].AsDouble - observedAll[g]) + Math.Abs(sums[g]["sum"].AsDouble - expectedAll[g]));
                        Check(name + ": deaths observed and expected over the strata", worst, 1e-9);
                        if (k > 2)
                        {
                            double top = 0, bottom = 0;
                            for (int g = 0; g < k; g++) { top += scores[g] * uAll[test][g]; for (int h = 0; h < k; h++) bottom += scores[g] * vAll[test][g, h] * scores[h]; }
                            ParameterBag trend = Rows(together, "*strata_trend")[0];
                            Check(name + ": chi-square for trend over the strata", Relative(trend["strata_trend"].AsDouble, top * top / bottom) + Math.Abs(trend["p_strata_trend"].AsDouble - ChiSquareUpper(top * top / bottom, 1)), 1e-7);
                        }
                    }
                    if (test == 0 && s == strata.Count - 1)
                    {
                        // hazard ratios of each pair of groups, from the deaths observed and expected over the strata
                        ParameterBag hazards = Rows(given, "*hazards")[0];
                        List<ParameterBag> pairs = Rows(hazards, "*hazard");
                        int at = 0; double worst = 0;
                        for (int g = 0; g < k - 1; g++)
                            for (int h = g + 1; h < k; h++)
                            {
                                ParameterBag pair = pairs[at++];
                                if (observedAll[g] > 0 && observedAll[h] > 0)
                                {
                                    double ratio = observedAll[g] / expectedAll[g] / (observedAll[h] / expectedAll[h]), se = Math.Sqrt(1 / expectedAll[g] + 1 / expectedAll[h]);
                                    worst = Math.Max(worst, Relative(pair["haz"].AsDouble, ratio) + Relative(pair["from"].AsDouble, ratio * Math.Exp(-z * se)) + Relative(pair["to"].AsDouble, ratio * Math.Exp(z * se)));
                                }
                            }
                        Check(name + ": hazard ratios and their approximate limits", worst, 1e-9);
                        if (k == 2 && !stratified)
                        {
                            ParameterBag exact = Rows(hazards, "*cml")[0];
                            double[] atOne = DeathsDistribution(tablesAll, out int least);
                            int seen = (int)observedAll[0] - least;
                            double alpha = (1 - gamma) / 2;
                            if (atOne.Length > 1 && seen > 0 && seen < atOne.Length - 1)
                            {
                                double Mean(double l) { double[] q = Tilt(atOne, l); return q.Select((p, i) => p * i).Sum() - seen; }
                                double Upper(double l, double share) { double[] q = Tilt(atOne, l); return q.Skip(seen + 1).Sum() + share * q[seen]; }
                                double Lower(double l, double share) { double[] q = Tilt(atOne, l); return q.Take(seen).Sum() + share * q[seen]; }
                                double ratio = Math.Exp(Solve(Mean, -30, 30));
                                Check(name + $": hazard ratio by conditional maximum likelihood ({exact["hr"].AsDouble:G8})", Relative(exact["hr"].AsDouble, ratio), 1e-6);
                                Check(name + ": its exact (Fisher) limits", Relative(exact["llf"].AsDouble, Math.Exp(Solve(l => Upper(l, 1) - alpha, -30, 30))) + Relative(exact["ulf"].AsDouble, Math.Exp(Solve(l => Lower(l, 1) - alpha, -30, 30))), 1e-5);
                                Check(name + ": its exact (mid-P) limits", Relative(exact["llm"].AsDouble, Math.Exp(Solve(l => Upper(l, 0.5) - alpha, -30, 30))) + Relative(exact["ulm"].AsDouble, Math.Exp(Solve(l => Lower(l, 0.5) - alpha, -30, 30))), 1e-5);
                                double up = Upper(0, 1), down = Lower(0, 1), upMid = Upper(0, 0.5), downMid = Lower(0, 0.5);
                                double point = Tilt(atOne, 0)[seen];
                                double twoSided = Tilt(atOne, 0).Where(p => p <= point * (1 + 1e-7)).Sum();
                                Check(name + $": one sided exact P (Fisher {exact["p1f"].AsDouble:G6}, mid-P {exact["p1m"].AsDouble:G6})", Math.Abs(exact["p1f"].AsDouble - Math.Min(up, down)) + Math.Abs(exact["p1m"].AsDouble - Math.Min(upMid, downMid)), 1e-7);
                                double doubled = Math.Min(1, 2 * Math.Min(up, down));
                                // two sided: for the P of the exact test the sum of the probabilities of the outcomes that are no more likely than
                                // the outcome seen; for the mid-P twice the one sided mid-P
                                Check(name + ": two sided exact P", Math.Abs(exact["p2f"].AsDouble - twoSided) + Math.Abs(exact["p2m"].AsDouble - Math.Min(1, 2 * Math.Min(upMid, downMid))), 1e-7);
                                exactConventions.Add((Math.Abs(exact["p2f"].AsDouble - doubled), Math.Abs(exact["p2f"].AsDouble - twoSided), Math.Abs(exact["p2m"].AsDouble - Math.Min(1, 2 * Math.Min(upMid, downMid))), Math.Abs(exact["p2m"].AsDouble - (twoSided - point / 2))));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    // how far the two sided exact P values are from each of two ways of making them: twice the one sided P, and the sum of the
    // probabilities of the outcomes that are no more likely than the outcome seen
    private static readonly List<(double fisherDoubled, double fisherLikely, double midDoubled, double midLikely)> exactConventions = new();

    private static void LogRank()
    {
        Console.WriteLine();
        Console.WriteLine("Log-rank and generalised Wilcoxon tests");
        System.Random random = new(73);
        string[] treatments = { "placebo", "low", "high", "highest" }, centres = { "north", "south", "east" };
        for (int set = 1; set <= 48; set++)
        {
            int k = 2 + set % 3, n = 12 + random.Next(set % 4 == 0 ? 120 : 45);
            bool stratified = set % 4 == 0, ties = set % 2 == 0, frequencies = set % 5 == 0;
            int method = 1 + set % 3;
            double gamma = new[] { 0.95, 0.9, 0.99 }[set % 3];
            List<Subject> records = new();
            for (int i = 0; i < n; i++)
            {
                int g = random.Next(k);
                double time = -Math.Log(1 - random.NextDouble()) * 10 / (1 + 0.5 * g);
                time = ties ? Math.Ceiling(time) : Math.Round(time, 3) + (i + 1) * 1e-5;
                double code = random.NextDouble() < 0.3 ? 0 : frequencies && i % 4 == 0 ? 2 : 1;
                records.Add(new Subject(time, code, treatments[g], centres[random.Next(stratified ? 2 + set % 2 : 1)]));
            }
            if (records.Select(r => r.Group).Distinct().Count() < k) continue;
            double[] scores = k > 2 ? (set % 2 == 0 ? Enumerable.Range(1, k).Select(v => (double)v).ToArray() : new[] { 0.0, 1, 4, 9 }.Take(k).ToArray()) : null;
            LogRankCase($"set {set}: {n} records, {k} groups{(stratified ? ", strata" : "")}{(ties ? ", tied times" : "")}{(frequencies ? ", codes above 1" : "")}, " + new[] { "", "Peto-Prentice", "Gehan-Breslow", "Tarone-Ware" }[method], records, gamma, method, stratified, scores);
        }
        if (exactConventions.Count > 0)
            Console.WriteLine($"      two sided exact P in {exactConventions.Count} sets: from twice the one sided P by at most {exactConventions.Max(v => v.fisherDoubled):E1} (Fisher) and {exactConventions.Max(v => v.midDoubled):E1} (mid-P); from the sum of the outcomes no more likely by at most {exactConventions.Max(v => v.fisherLikely):E1} and {exactConventions.Max(v => v.midLikely):E1}");

        Console.WriteLine();
        Console.WriteLine("Log-rank: records with a blank cell");
        foreach (string blank in new[] { "time", "code", "group", "stratum" })
        {
            List<Subject> records = new();
            for (int i = 0; i < 50; i++)
                records.Add(new Subject(Math.Ceiling(-Math.Log(1 - random.NextDouble()) * 8 / (1 + i % 2)), random.NextDouble() < 0.3 ? 0 : 1, treatments[i % 2], centres[i % 3 == 0 ? 0 : 1]));
            foreach (int at in new[] { 4, 11, 30 })
                records[at] = blank switch { "time" => records[at] with { Time = M }, "code" => records[at] with { Code = M }, "group" => records[at] with { Group = null }, _ => records[at] with { Stratum = null } };
            LogRankCase($"blank {blank}", records, 0.95, 1, blank == "stratum", null);
        }
    }
}

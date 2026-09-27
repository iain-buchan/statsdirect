// The Wei-Lachin tests (Survival.RptWeiLachin): two groups of subjects, each with a time to failure, which may be censored, at each of
// several repeats.  For each repeat the statistic is the log-rank (or Gehan) score over the square root of the number of subjects, with
// a variance estimated from the subjects themselves; the repeats are taken together in an omnibus chi-square and in a test of stochastic
// ordering.  The scores are worked out here from the definition; the chi-square, the tests of the repeats together and the P values from
// the statistics and variances that are printed; and the variances by seeing that, in data drawn without a difference between the groups,
// the statistics have the distributions that they are referred to.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static ParameterBag WeiLachinReport(string[] group, double[][] time, double[][] censor)
    {
        ParameterBag bag = new();
        bag.AddInput("gid", new DataFrame(Classifier("Group", group)));
        bag.AddInput("nr", time.Length);
        DataFrame times = new(new DoubleVariable((double[])time[0].Clone(), "T1")), codes = new(new DoubleVariable((double[])censor[0].Clone(), "C1"));
        for (int k = 1; k < time.Length; k++)
        {
            times.Variables.Add(new DoubleVariable((double[])time[k].Clone(), "T" + (k + 1)));
            codes.Variables.Add(new DoubleVariable((double[])censor[k].Clone(), "C" + (k + 1)));
        }
        bag.AddInput("times", times);
        bag.AddInput("censor", codes);
        return Survival.RptWeiLachin(bag).ParameterBag;
    }

    // the score of one repeat: over the failures, the weight times (1 if the failure is in the first group) less the share of the first
    // group among those at risk; a subject without a time is at risk of nothing.  The weight is 1, or for Gehan's test the share of all
    // the subjects who are at risk
    private static double Score(string[] group, string first, double[] time, double[] censor, bool gehan, out int failuresFirst, out int failuresSecond)
    {
        int n = group.Length;
        double score = 0;
        failuresFirst = failuresSecond = 0;
        for (int j = 0; j < n; j++)
        {
            if (time[j] == M || censor[j] != 1) continue;
            double atRisk = 0, atRiskFirst = 0;
            for (int i = 0; i < n; i++)
                if (time[i] != M && time[i] >= time[j]) { atRisk++; if (group[i] == first) atRiskFirst++; }
            double weight = gehan ? atRisk / n : 1;
            score += weight * ((group[j] == first ? 1 : 0) - atRiskFirst / atRisk);
            if (group[j] == first) failuresFirst++; else failuresSecond++;
        }
        return score / Math.Sqrt(n);
    }

    private static void WeiLachinCase(string title, string[] group, double[][] time, double[][] censor)
    {
        int before = failures;
        try
        {
            ParameterBag o = WeiLachinReport(group, time, censor);
            int repeats = time.Length, n = group.Length;
            string first = group[0];
            List<ParameterBag> outer = Rows(o, "*outer");
            Say(outer.Count == 2, title + ": Gehan's and the log-rank tests");
            for (int test = 0; test < 2 && outer.Count == 2; test++)
            {
                string name = title + (test == 0 ? ", Gehan" : ", log-rank");
                ParameterBag given = outer[test];
                Say(given["tot"].AsInt32 == n && given["grp_1"].AsInt32 == group.Count(g => g == first) && given["grp_2"].AsInt32 == group.Count(g => g != first), name + ": the numbers of subjects");
                List<ParameterBag> rows = Rows(given, "*repeats");
                double[] t = new double[repeats];
                double worst = 0, worstChi = 0;
                for (int k = 0; k < repeats; k++)
                {
                    double score = Score(group, first, time[k], censor[k], test == 0, out int f1, out int f2);
                    t[k] = rows[k]["t"].AsDouble;
                    worst = Math.Max(worst, Math.Abs(t[k] - score) + Math.Abs(rows[k]["fail_1"].AsInt32 - f1) + Math.Abs(rows[k]["fail_2"].AsInt32 - f2));
                    double variance = rows[k]["var"].AsDouble;
                    if (variance != M)
                        worstChi = Math.Max(worstChi, Relative(rows[k]["chi"].AsDouble, t[k] * t[k] / variance) + Math.Abs(rows[k]["p"].AsDouble - ChiSquareUpper(t[k] * t[k] / variance, 1)));
                }
                Check(name + ": the statistic of each repeat, and the failures", worst, 1e-10);
                Check(name + ": the chi-square of each repeat and its P", worstChi, 1e-7);
                // the matrix of variances and covariances as it is printed, a row for each repeat up to the diagonal
                double[,] sigma = new double[repeats, repeats];
                List<ParameterBag> printed = Rows(given, "*covar");
                for (int k = 0; k < repeats; k++)
                {
                    List<ParameterBag> cells = Rows(printed[k], "*mat");
                    for (int l = 0; l <= k; l++) sigma[k, l] = sigma[l, k] = cells[l]["cell"].AsDouble;
                }
                double omnibus = QuadraticForm(t, sigma, out int rank);
                Check(name + ": omnibus chi-square", Relative(given["stat"].AsDouble, omnibus), 1e-7);
                if (rank > 0) Check(name + ": its P, on the rank of the matrix", Math.Abs(given["p_omnibus"].AsDouble - ChiSquareUpper(omnibus, rank)), 1e-7);
                double all = 0;
                foreach (double v in sigma) all += v;
                if (all > 0)
                {
                    double z = t.Sum() / Math.Sqrt(all);
                    Check(name + ": stochastic ordering z and its P values", Math.Abs(given["z"].AsDouble - z) + Math.Abs(given["p_1"].AsDouble - NormalUpper(Math.Abs(z))) + Math.Abs(given["p_2"].AsDouble - 2 * NormalUpper(Math.Abs(z))), 1e-7);
                }
                // the variance of a repeat can be no less than nothing, and the matrix must be one that variances and covariances can make
                double[] roots = LatentRoots(sigma, out _);
                Check(name + ": the matrix has no latent root below nothing", Math.Max(0, -roots.Min()), 1e-10 * Math.Max(1e-300, roots.Max()));
            }
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    private static (string[] group, double[][] time, double[][] censor) WeiLachinData(System.Random random, int n, int repeats, double effect, bool ties, double censoring, double missing)
    {
        string[] group = new string[n];
        double[][] time = Enumerable.Range(0, repeats).Select(k => new double[n]).ToArray(), censor = Enumerable.Range(0, repeats).Select(k => new double[n]).ToArray();
        for (int i = 0; i < n; i++)
        {
            group[i] = i % 2 == 0 ? "active" : "placebo";
            double frailty = -Math.Log(1 - random.NextDouble());        // what the repeats of a subject share
            for (int k = 0; k < repeats; k++)
            {
                double t = (0.5 * frailty - 0.5 * Math.Log(1 - random.NextDouble())) * 10 * (group[i] == "active" ? Math.Exp(effect) : 1);
                time[k][i] = ties ? Math.Ceiling(t) : Math.Round(t, 3) + (i + 1) * 1e-5;
                censor[k][i] = random.NextDouble() < censoring ? 0 : 1;
                if (random.NextDouble() < missing) { time[k][i] = M; censor[k][i] = M; }
            }
        }
        return (group, time, censor);
    }

    private static void WeiLachin()
    {
        Console.WriteLine();
        Console.WriteLine("Wei-Lachin tests");
        System.Random random = new(79);
        for (int set = 1; set <= 24; set++)
        {
            int n = 10 + random.Next(set % 3 == 0 ? 120 : 40), repeats = 1 + set % 4;
            bool ties = set % 2 == 0;
            double censoring = new[] { 0.0, 0.25, 0.5 }[set % 3], missing = set % 5 == 0 ? 0.1 : 0;
            var data = WeiLachinData(random, n, repeats, set % 2 == 0 ? 0.5 : 0, ties, censoring, missing);
            WeiLachinCase($"set {set}: {n} subjects, {repeats} repeat{(repeats > 1 ? "s" : "")}{(ties ? ", tied times" : "")}, {censoring:P0} censored{(missing > 0 ? ", some times missing" : "")}", data.group, data.time, data.censor);
        }

        // without a difference between the groups, the chi-square of a repeat should have a mean of 1 and be above 3.841 one time in
        // twenty, and the omnibus chi-square of three repeats a mean of 3 and be above 7.815 one time in twenty
        foreach (int test in new[] { 0, 1 })
        {
            int draws = 2000, above = 0, aboveOmnibus = 0, aboveOrdering = 0;
            double sum = 0, sumOmnibus = 0;
            for (int d = 0; d < draws; d++)
            {
                var data = WeiLachinData(random, 80, 3, 0, d % 2 == 0, 0.3, 0);
                ParameterBag given = Rows(WeiLachinReport(data.group, data.time, data.censor), "*outer")[test];
                double chi = Rows(given, "*repeats")[0]["chi"].AsDouble;
                if (chi == M) chi = 0;
                sum += chi; if (chi > 3.841458820694124) above++;
                sumOmnibus += given["stat"].AsDouble; if (given["stat"].AsDouble > 7.814727903251179) aboveOmnibus++;
                if (given["p_2"].AsDouble < 0.05) aboveOrdering++;
            }
            string name = (test == 0 ? "Gehan" : "log-rank") + $", {draws} sets of 80 subjects drawn without a difference";
            // the variance is estimated from the subjects, and for the log-rank statistic it is a little small in samples of this size: the
            // mean chi-square is about 1.18 with 40 subjects, 1.10 with 80 and 1.03 with 300
            Check(name + $": mean chi-square of a repeat ({sum / draws:F3})", Math.Abs(sum / draws - (test == 0 ? 1.0 : 1.08)), 0.12, true);
            Check(name + $": proportion of them above the 5% point ({above / (double)draws:F3})", Math.Abs(above / (double)draws - (test == 0 ? 0.05 : 0.06)), 0.02, true);
            Check(name + $": mean omnibus chi-square of three repeats ({sumOmnibus / draws:F3})", Math.Abs(sumOmnibus / draws - (test == 0 ? 3.0 : 3.2)), 0.3, true);
            Check(name + $": proportion of them above the 5% point ({aboveOmnibus / (double)draws:F3})", Math.Abs(aboveOmnibus / (double)draws - (test == 0 ? 0.05 : 0.06)), 0.02, true);
            Check(name + $": proportion of two sided P values of the stochastic ordering test below 0.05 ({aboveOrdering / (double)draws:F3})", Math.Abs(aboveOrdering / (double)draws - (test == 0 ? 0.05 : 0.06)), 0.02, true);
        }
    }
}

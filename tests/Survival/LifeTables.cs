// The follow-up life table and the abridged life table (Survival.RptFollowUpLifetable, Survival.RptAbridgedLifetable), each worked out
// here from the definitions.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal sealed class PlainWithProgress : IPreferencesAndProgressBar
{
    private readonly Plain plain = new();
    private readonly NoProgress none = new();
    public string RoundU(double amount) => plain.RoundU(amount);
    public string pval(double p) => plain.pval(p);
    public string pval_half(double p) => plain.pval_half(p);
    public SDPreferences Preferences => plain.Preferences;
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => none;
}

internal static partial class Program
{
    private static void FollowUpCase(string title, double[] time, double[] deaths, double[] withdrawn, double start, double gamma)
    {
        int before = failures;
        try
        {
            ParameterBag bag = new();
            bag.AddInput("gamma", gamma);
            bag.AddInput("times", new DataFrame(new DoubleVariable((double[])time.Clone(), "Interval")));
            bag.AddInput("deaths", new DataFrame(new DoubleVariable((double[])deaths.Clone(), "Deaths")));
            bag.AddInput("withdrawals", new DataFrame(new DoubleVariable((double[])withdrawn.Clone(), "Withdrawn")));
            double least = Survival.RptFollowUpLifetableCalculateNatst(bag).ParameterBag["natst-min"].AsDouble;
            int[] rows = Enumerable.Range(0, time.Length).Where(r => time[r] != M && deaths[r] != M && withdrawn[r] != M).ToArray();
            Check(title + ": the least number alive at the start", Math.Abs(least - rows.Sum(r => deaths[r] + withdrawn[r])), 0);
            bag.AddInput("natst", start);
            ParameterBag o = Survival.RptFollowUpLifetable(bag).ParameterBag;
            double z = NormalQuantile(1 - (1 - gamma) / 2);
            // the intervals: the rows with the same whole number of years at their start are one interval
            var intervals = rows.GroupBy(r => Math.Floor(time[r])).OrderBy(g => g.Key).Select(g => (start: g.Key, deaths: g.Sum(r => deaths[r]), withdrawn: g.Sum(r => withdrawn[r]))).ToList();
            List<ParameterBag> first = Rows(o, "*deaths"), second = Rows(o, "*survival");
            Say(first.Count == intervals.Count && second.Count == intervals.Count, title + $": a row for each of the {intervals.Count} intervals in both tables");
            if (first.Count != intervals.Count || second.Count != intervals.Count) return;
            double atRisk = start, survival = 1, sum = 0, worst = 0, worstSurvival = 0, worstLimits = 0;
            for (int i = 0; i < intervals.Count; i++)
            {
                var v = intervals[i];
                bool last = i == intervals.Count - 1;
                string label = last ? $"{v.start} up" : $"{v.start} to {intervals[i + 1].start}";
                if (first[i]["int"].AsString != label || second[i]["int"].AsString != label) worst = double.PositiveInfinity;
                double adjusted = atRisk - v.withdrawn / 2, q = v.deaths / adjusted;
                worst = Math.Max(worst, Math.Abs(first[i]["death"].AsDouble - v.deaths) + Math.Abs(first[i]["wdrawn"].AsDouble - v.withdrawn) + Math.Abs(first[i]["risk"].AsDouble - atRisk));
                if (!last) worst = Math.Max(worst, Math.Abs(first[i]["nx"].AsDouble - adjusted) + Math.Abs(first[i]["q"].AsDouble - q));
                else if (first[i]["nx"].AsDouble != M || first[i]["q"].AsDouble != M) worst = double.PositiveInfinity;
                // the second table: the proportion who survive the interval, and the percentage who are alive at its start
                worstSurvival = Math.Max(worstSurvival, Math.Abs(second[i]["lx"].AsDouble - 100 * survival));
                if (!last) worstSurvival = Math.Max(worstSurvival, Math.Abs(second[i]["p"].AsDouble - (1 - q)));
                if (i > 0 && sum > 0 && survival > 0 && survival < 1)
                {
                    double sd = 100 * survival * Math.Sqrt(sum), se = Math.Sqrt(sum) / Math.Abs(Math.Log(survival));
                    worstLimits = Math.Max(worstLimits, Relative(second[i]["var"].AsDouble, sd) + Relative(second[i]["lci"].AsDouble, 100 * Math.Pow(survival, Math.Exp(z * se))) + Relative(second[i]["uci"].AsDouble, 100 * Math.Pow(survival, Math.Exp(-z * se))));
                }
                survival *= 1 - q;
                if ((1 - q) * adjusted != 0) sum += q / (adjusted * (1 - q));
                atRisk -= v.deaths + v.withdrawn;
            }
            Check(title + ": intervals, deaths, withdrawn, at risk, adjusted at risk and probability of death", worst, 1e-10);
            Check(title + ": probability of survival and percentage of survivors", worstSurvival, 1e-10);
            Check(title + ": standard deviation and limits of the percentage of survivors", worstLimits, 1e-9);
            int blank = time.Length - rows.Length;
            List<ParameterBag> note = Rows(o, "*note");
            Say(blank == 0 ? note.Count == 0 : note.Count == 1 && note[0]["note"].AsString.StartsWith(blank.ToString()), title + ": the note of rows left out");
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    private sealed class Abridged
    {
        public double[] Start, Rate, Q, Alive, Dying, Years, Beyond, Expectation, VarQ, VarE;
        public double Median;
    }

    // the life table from the deaths and populations of intervals of age: lengths has the lengths of all the intervals but the last, which
    // is open; fraction is the share of an interval that those who die in it live through
    private static Abridged AbridgedTable(double[] lengths, double[] population, double[] deaths, double[] fraction)
    {
        int n = population.Length;
        Abridged t = new() { Start = new double[n], Rate = new double[n], Q = new double[n], Alive = new double[n], Dying = new double[n], Years = new double[n], Beyond = new double[n], Expectation = new double[n], VarQ = new double[n], VarE = new double[n] };
        for (int i = 1; i < n; i++) t.Start[i] = t.Start[i - 1] + lengths[i - 1];
        t.Alive[0] = 100000;
        for (int i = 0; i < n; i++)
        {
            t.Rate[i] = deaths[i] / population[i];
            if (i == n - 1) { t.Q[i] = 1; t.Dying[i] = t.Alive[i]; t.Years[i] = t.Alive[i] / t.Rate[i]; }
            else
            {
                t.Q[i] = lengths[i] * t.Rate[i] / (1 + (1 - fraction[i]) * lengths[i] * t.Rate[i]);
                t.Dying[i] = t.Alive[i] * t.Q[i];
                t.Alive[i + 1] = t.Alive[i] - t.Dying[i];
                t.Years[i] = lengths[i] * (t.Alive[i] - t.Dying[i]) + fraction[i] * lengths[i] * t.Dying[i];
            }
        }
        for (int i = n - 1; i >= 0; i--)
        {
            t.Beyond[i] = t.Years[i] + (i < n - 1 ? t.Beyond[i + 1] : 0);
            t.Expectation[i] = t.Beyond[i] / t.Alive[i];
        }
        // the variances: of the probability of dying, q^2 (1 - q) / deaths; of the expectation at the start of interval i, the sum over the
        // intervals from i on (but the last) of alive^2 [(1 - fraction) length + expectation at the end of the interval]^2 var(q), over alive(i)^2
        for (int i = 0; i < n - 1; i++) t.VarQ[i] = t.Q[i] * t.Q[i] * (1 - t.Q[i]) / deaths[i];
        for (int i = 0; i < n - 1; i++)
        {
            double sum = 0;
            for (int j = i; j < n - 1; j++) sum += t.Alive[j] * t.Alive[j] * Math.Pow((1 - fraction[j]) * lengths[j] + t.Expectation[j + 1], 2) * t.VarQ[j];
            t.VarE[i] = sum / (t.Alive[i] * t.Alive[i]);
        }
        // the median: the age at which half are alive, between the starts of the two intervals that it lies between
        t.Median = double.PositiveInfinity;
        for (int i = 1; i < n; i++)
            if (t.Alive[i] <= 50000) { t.Median = t.Start[i - 1] + (t.Alive[i - 1] - 50000) / (t.Alive[i - 1] - t.Alive[i]) * (t.Start[i] - t.Start[i - 1]); break; }
        return t;
    }

    private static void AbridgedCase(string title, double[] lengths, double[] population, double[] deaths, double[] fraction, double[] weights, double gamma, System.Random random)
    {
        int before = failures;
        try
        {
            int n = population.Length;
            ParameterBag bag = new();
            bag.AddInput("gamma", gamma);
            bag.AddInput("intervals", new DataFrame(new DoubleVariable((double[])lengths.Clone(), "Length")));
            bag.AddInput("population", new DataFrame(new DoubleVariable((double[])population.Clone(), "Population")));
            bag.AddInput("deaths", new DataFrame(new DoubleVariable((double[])deaths.Clone(), "Deaths")));
            if (fraction != null) bag.AddInput("fractions", new DataFrame(new DoubleVariable((double[])fraction.Clone(), "Fraction")));
            if (weights != null) bag.AddInput("weights", new DataFrame(new DoubleVariable((double[])weights.Clone(), "Healthy")));
            bag.AddInput("iterations", "20000");
            bag.AddInput("save", true);
            ParameterBag o = Survival.RptAbridgedLifetable(new PlainWithProgress(), bag).ParameterBag;
            double z = NormalQuantile(1 - (1 - gamma) / 2);
            // the fractions that are taken when none are given: a half, but 0.1 for a first interval of a year or less and 0.4 for a second
            // interval of five years or less
            double[] a = fraction ?? Enumerable.Range(0, n - 1).Select(i => i == 0 && lengths[0] <= 1 ? 0.1 : i == 1 && lengths[1] <= 5 ? 0.4 : 0.5).ToArray();
            Abridged t = AbridgedTable(lengths, population, deaths, a);
            List<ParameterBag> inputs = Rows(o, "*inputs"), dying = Rows(o, "*pdying"), living = Rows(o, "*living"), years = Rows(o, "*years"), expectation = Rows(o, "*expectation");
            Say(inputs.Count == n && dying.Count == n && living.Count == n && years.Count == n && expectation.Count == n, title + $": {n} rows in each table");
            double worstRate = 0, worstQ = 0, worstCounts = 0, worstE = 0, worstLimits = 0;
            bool variances = deaths.All(d => d > 0);
            for (int i = 0; i < n; i++)
            {
                worstRate = Math.Max(worstRate, Relative(inputs[i]["rate"].AsDouble, t.Rate[i]));
                worstQ = Math.Max(worstQ, Math.Abs(dying[i]["q"].AsDouble - t.Q[i]));
                worstCounts = Math.Max(worstCounts, Math.Abs(double.Parse(living[i]["l"].AsString) - Math.Round(t.Alive[i], MidpointRounding.ToEven)) + Math.Abs(double.Parse(living[i]["d"].AsString) - Math.Round(t.Dying[i], MidpointRounding.ToEven))
                    + Math.Abs(double.Parse(years[i]["L"].AsString) - Math.Round(t.Years[i], MidpointRounding.ToEven)) + Math.Abs(double.Parse(years[i]["T"].AsString) - Math.Round(t.Beyond[i], MidpointRounding.ToEven)));
                worstE = Math.Max(worstE, Relative(expectation[i]["e"].AsDouble, t.Expectation[i]));
                if (variances && i < n - 1)
                    worstLimits = Math.Max(worstLimits, Relative(dying[i]["se"].AsDouble, Math.Sqrt(t.VarQ[i])) + Math.Abs(dying[i]["lci"].AsDouble - (t.Q[i] - z * Math.Sqrt(t.VarQ[i]))) + Math.Abs(dying[i]["uci"].AsDouble - (t.Q[i] + z * Math.Sqrt(t.VarQ[i])))
                        + Relative(expectation[i]["se"].AsDouble, Math.Sqrt(t.VarE[i])) + Relative(expectation[i]["lci"].AsDouble, t.Expectation[i] - z * Math.Sqrt(t.VarE[i])) + Relative(expectation[i]["uci"].AsDouble, t.Expectation[i] + z * Math.Sqrt(t.VarE[i])));
                else if (dying[i]["se"].AsDouble != M || expectation[i]["se"].AsDouble != M) worstLimits = double.PositiveInfinity;
            }
            Check(title + ": death rates", worstRate, 1e-12);
            Check(title + ": probabilities of dying", worstQ, 1e-12);
            Check(title + ": numbers living and dying, years lived in and beyond each interval", worstCounts, 0);
            Check(title + ": expectations of life", worstE, 1e-12);
            Check(title + ": standard errors and limits of the probabilities of dying and the expectations", worstLimits, 1e-10);
            Check(title + ": expectation of life at birth", Relative(o["elb"].AsDouble, t.Expectation[0]), 1e-12);
            object median = o["med"].AsObject;
            Say(double.IsInfinity(t.Median) ? median is string text && text.StartsWith("more than") : median is double value && Math.Abs(value - t.Median) <= 1e-9 * t.Median, title + $": median ({median})");
            if (weights != null)
            {
                // the adjusted expectation at the start of an interval: the years lived in that interval and in each of those after it,
                // each times the weight of its interval, over the number alive at the start of the interval
                List<ParameterBag> adjusted = Rows(Rows(o, "*util")[0], "*adjusted");
                double[] expected = Enumerable.Range(0, n).Select(i => Enumerable.Range(i, n - i).Sum(j => weights[j] * t.Years[j]) / t.Alive[i]).ToArray();
                Check(title + ": expectations of life adjusted by the weights", Enumerable.Range(0, n).Max(i => Relative(adjusted[i]["eh"].AsDouble, expected[i])), 1e-12);
                double[] savedAdjusted = ((DoubleVariable)o["results"].AsDataFrame.Variables[14]).Data;
                Check(title + ": saved adjusted expectations", Enumerable.Range(0, n).Max(i => Relative(savedAdjusted[i], expected[i])), 1e-12);
            }
            if (variances)
            {
                // the limits by simulation, against limits from a simulation made here: deaths drawn from the Poisson distribution
                double[] drawn = new double[20000], medians = new double[20000];
                for (int s = 0; s < drawn.Length; s++)
                {
                    double[] d = deaths.Select(v => Poisson(random, v)).ToArray();
                    Abridged sim = AbridgedTable(lengths, population, d, a);
                    drawn[s] = sim.Expectation[0]; medians[s] = sim.Median;
                }
                Array.Sort(drawn); Array.Sort(medians);
                double se = Math.Sqrt(t.VarE[0]), low = drawn[(int)(drawn.Length * (1 - gamma) / 2)], high = drawn[(int)(drawn.Length * (1 + gamma) / 2)];
                Check(title + $": limits of the expectation at birth by simulation ({o["elb_mc_lci"].AsDouble:F4} to {o["elb_mc_uci"].AsDouble:F4}), in standard errors from those of another simulation", (Math.Abs(o["elb_mc_lci"].AsDouble - low) + Math.Abs(o["elb_mc_uci"].AsDouble - high)) / (drawn[(int)(drawn.Length * 0.84)] - drawn[(int)(drawn.Length * 0.16)]) * 2, 0.2);
                // the limits by formula leave out the uncertainty of the death rate of the open interval, which the simulation has: the
                // limits by simulation can be no narrower, to within what a simulation can tell
                Say(o["elb_mc_uci"].AsDouble - o["elb_mc_lci"].AsDouble >= 0.93 * 2 * z * se, title + ": the limits by simulation are no narrower than those by formula");
                if (o["med_lci"].AsObject is double lowMedian && o["med_uci"].AsObject is double highMedian && double.IsFinite(medians[(int)(medians.Length * (1 + gamma) / 2)]))
                {
                    double spread = medians[(int)(medians.Length * 0.84)] - medians[(int)(medians.Length * 0.16)];
                    Check(title + ": limits of the median by simulation, against those of another simulation", (Math.Abs(lowMedian - medians[(int)(medians.Length * (1 - gamma) / 2)]) + Math.Abs(highMedian - medians[(int)(medians.Length * (1 + gamma) / 2)])) / Math.Max(1e-9, spread / 2), 0.35);
                }
            }
            // what is saved
            DataFrame saved = o["results"].AsDataFrame;
            Say(saved.VariableCount == (weights != null ? 15 : 14), title + ": the columns saved");
            double[] savedQ = ((DoubleVariable)saved.Variables[1]).Data, savedE = ((DoubleVariable)saved.Variables[10]).Data, savedAlive = ((DoubleVariable)saved.Variables[5]).Data;
            Check(title + ": saved probabilities of dying, numbers alive and expectations", Enumerable.Range(0, n).Max(i => Math.Abs(savedQ[i] - t.Q[i]) + Relative(savedAlive[i], t.Alive[i]) + Relative(savedE[i], t.Expectation[i])), 1e-12);
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    " + title);
    }

    private static double Poisson(System.Random random, double mean)
    {
        if (mean > 50)
        {
            // the normal distribution with the mean and variance of the Poisson, rounded: near enough for deaths in their hundreds
            double u = Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
            return Math.Max(0, Math.Round(mean + Math.Sqrt(mean) * u));
        }
        double limit = Math.Exp(-mean), product = random.NextDouble();
        int k = 0;
        while (product > limit) { product *= random.NextDouble(); k++; }
        return k;
    }

    private static void LifeTables()
    {
        Console.WriteLine();
        Console.WriteLine("Follow-up life table");
        System.Random random = new(83);
        for (int set = 1; set <= 20; set++)
        {
            int n = 3 + random.Next(10);
            double[] time = new double[n], deaths = new double[n], withdrawn = new double[n];
            for (int i = 0; i < n; i++)
            {
                time[i] = set % 4 == 0 ? i / 2 : i;       // in some sets two rows start in the same year
                if (set % 3 == 0) time[i] += 0.5;
                deaths[i] = random.Next(30);
                withdrawn[i] = random.Next(set % 2 == 0 ? 20 : 1);
            }
            if (set % 5 == 0) { deaths[1] = M; if (n > 4) withdrawn[4] = M; }
            // rows in any order
            if (set % 6 == 0) { (time[0], time[n - 1]) = (time[n - 1], time[0]); (deaths[0], deaths[n - 1]) = (deaths[n - 1], deaths[0]); (withdrawn[0], withdrawn[n - 1]) = (withdrawn[n - 1], withdrawn[0]); }
            double needed = Enumerable.Range(0, n).Where(r => time[r] != M && deaths[r] != M && withdrawn[r] != M).Sum(r => deaths[r] + withdrawn[r]);
            FollowUpCase($"set {set}: {n} rows{(set % 4 == 0 ? ", rows that share a year" : "")}{(set % 5 == 0 ? ", blank cells" : "")}{(set % 6 == 0 ? ", out of order" : "")}", time, deaths, withdrawn, needed + random.Next(set % 2 == 0 ? 50 : 1), new[] { 0.95, 0.9, 0.99 }[set % 3]);
        }

        Console.WriteLine();
        Console.WriteLine("Abridged life table");
        for (int set = 1; set <= 16; set++)
        {
            double[] lengths = set % 3 == 0 ? new double[] { 1, 4, 5, 5, 5, 10, 10, 10, 10, 10, 10 } : set % 3 == 1 ? new double[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 } : new double[] { 1, 4, 10, 10, 20, 20, 20 };
            int n = lengths.Length + 1;
            double[] population = new double[n], deaths = new double[n];
            double age = 0;
            for (int i = 0; i < n; i++)
            {
                double middle = age + (i < n - 1 ? lengths[i] / 2 : 5);
                population[i] = Math.Round((set % 2 == 0 ? 2000 : 60000) * (i < n - 1 ? lengths[i] : 8) * (0.7 + 0.6 * random.NextDouble()));
                double rate = (i == 0 ? 0.004 : 0.0002) + 0.00003 * Math.Exp(0.095 * middle);
                deaths[i] = Math.Max(set % 4 == 0 && i == 2 ? 0 : 1, Math.Round(population[i] * rate * (0.8 + 0.4 * random.NextDouble())));
                if (i < n - 1) age += lengths[i];
            }
            double[] fraction = set % 5 == 0 ? Enumerable.Range(0, n - 1).Select(i => i == 0 ? 0.15 : 0.45 + 0.1 * random.NextDouble()).ToArray() : null;
            double[] weights = set % 3 == 1 ? Enumerable.Range(0, n).Select(i => 1 - 0.03 * i).ToArray() : null;
            AbridgedCase($"set {set}: {n} intervals{(fraction != null ? ", fractions given" : "")}{(weights != null ? ", weights" : "")}{(set % 4 == 0 ? ", an interval without a death" : "")}, populations in {(set % 2 == 0 ? "thousands" : "hundreds of thousands")}", lengths, population, deaths, fraction, weights, new[] { 0.95, 0.9, 0.99 }[set % 3], random);
        }
        AbridgedWeights(random);
        LifeTableLimits();
    }

    // The expectation of life adjusted by weights, for weights whose answer is known from the table itself: with every weight 1 it is the
    // expectation of life; with a weight of 1 up to an age and of nothing after it, it is the years lived before that age by those alive
    // at the start of the interval, over their number; with a weight in one interval only, that weight times the years lived in it
    private static void AbridgedWeights(System.Random random)
    {
        Console.WriteLine();
        Console.WriteLine("Abridged life table: the expectation of life adjusted by weights");
        double[] lengths = { 1, 4, 5, 5, 10, 10, 10, 10, 10, 10 };
        int n = lengths.Length + 1;
        double[] population = new double[n], deaths = new double[n];
        double age = 0;
        for (int i = 0; i < n; i++)
        {
            double middle = age + (i < n - 1 ? lengths[i] / 2 : 5);
            population[i] = Math.Round(50000 * (i < n - 1 ? lengths[i] : 8) * (0.7 + 0.6 * random.NextDouble()));
            deaths[i] = Math.Max(1, Math.Round(population[i] * ((i == 0 ? 0.004 : 0.0002) + 0.00003 * Math.Exp(0.095 * middle))));
            if (i < n - 1) age += lengths[i];
        }
        double[] a = Enumerable.Range(0, n - 1).Select(i => i == 0 ? 0.1 : i == 1 ? 0.4 : 0.5).ToArray();
        Abridged t = AbridgedTable(lengths, population, deaths, a);
        (double[] adjusted, double[] expectation) Run(double[] weights)
        {
            ParameterBag bag = new();
            bag.AddInput("gamma", 0.95);
            bag.AddInput("intervals", new DataFrame(new DoubleVariable((double[])lengths.Clone(), "Length")));
            bag.AddInput("population", new DataFrame(new DoubleVariable((double[])population.Clone(), "Population")));
            bag.AddInput("deaths", new DataFrame(new DoubleVariable((double[])deaths.Clone(), "Deaths")));
            bag.AddInput("weights", new DataFrame(new DoubleVariable((double[])weights.Clone(), "Healthy")));
            bag.AddInput("iterations", "3000");
            bag.AddInput("save", false);
            ParameterBag o = Survival.RptAbridgedLifetable(new PlainWithProgress(), bag).ParameterBag;
            return (Rows(Rows(o, "*util")[0], "*adjusted").Select(r => r["eh"].AsDouble).ToArray(), Rows(o, "*expectation").Select(r => r["e"].AsDouble).ToArray());
        }
        int before = failures;
        try
        {
            var ones = Run(Enumerable.Repeat(1.0, n).ToArray());
            Check("every weight 1: the adjusted expectation is the expectation of life", Enumerable.Range(0, n).Max(i => Relative(ones.adjusted[i], ones.expectation[i]) + Relative(ones.adjusted[i], t.Expectation[i])), 1e-12);
            var none = Run(Enumerable.Repeat(0.0, n).ToArray());
            Check("every weight nothing: the adjusted expectation is nothing", none.adjusted.Max(Math.Abs), 0);
            for (int k = 1; k < n; k++)
            {
                // a weight of 1 in the intervals before interval k (counted from 0) and of nothing from it on
                var early = Run(Enumerable.Range(0, n).Select(i => i < k ? 1.0 : 0.0).ToArray());
                double worst = 0;
                for (int i = 0; i < n; i++)
                    worst = Math.Max(worst, Relative(early.adjusted[i], i < k ? (t.Beyond[i] - t.Beyond[k]) / t.Alive[i] : 0));
                Check($"a weight of 1 before the age of {t.Start[k]} and of nothing after: the years lived before that age, over the number alive", worst, 1e-12);
                // a weight in interval k only
                var one = Run(Enumerable.Range(0, n).Select(i => i == k ? 0.6 : 0.0).ToArray());
                worst = 0;
                for (int i = 0; i < n; i++)
                    worst = Math.Max(worst, Relative(one.adjusted[i], i <= k ? 0.6 * t.Years[k] / t.Alive[i] : 0));
                Check($"a weight of 0.6 in the interval from the age of {t.Start[k]} only: 0.6 of the years lived in it, over the number alive", worst, 1e-12);
            }
        }
        catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  the expectation of life adjusted by weights: " + Message(ex)); }
        if (failures == before) Console.WriteLine("ok    weights whose answer is known from the table");
    }
}

// Cox regression: checks by calculation of the fit, and of the reports that follow it, against figures that are worked out here from the definitions.
// The reports are given their parameters as the program passes them on from the regression.
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private const double M = double.MinValue;   // the program's missing value
    private static int failures;
    private static int checks;
    private static readonly List<string> notes = new();

    // a check: nothing is printed for one that passes, unless it is one of the checks that are made once
    private static void Check(string what, double worst, double tolerance, bool print = false)
    {
        checks++;
        bool ok = worst <= tolerance;
        if (!ok) failures++;
        string line = $"{(ok ? "ok  " : "FAIL")}  {what}: worst difference {worst:E2} (tolerance {tolerance:E0})";
        if (print) Console.WriteLine(line); else if (!ok) notes.Add(line);
    }

    private static void Say(bool ok, string what, bool print = false)
    {
        checks++;
        if (!ok) failures++;
        string line = $"{(ok ? "ok  " : "FAIL")}  {what}";
        if (print) Console.WriteLine(line); else if (!ok) notes.Add(line);
    }

    private static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return inner.GetType().Name + ": " + inner.Message;
    }

    // Code is the event code as it is selected: 0 for a censored record, 1 for an event, more than 1 for that number of subjects with the event
    private sealed record Subject(double Time, double Code, double[] Z, double Stratum)
    {
        public bool Event => Code >= 1;
        public double Frequency => Code > 1 ? Code : 1;
        public bool Complete => Time != M && Code != M && Stratum != M && Z.All(v => v != M);
    }

    // the log partial likelihood at beta, with its gradient and the matrix of its second derivatives with the sign changed; ties by Breslow's approximation
    private static double LogLikelihood(List<Subject> data, double[] beta, out double[] score, out double[,] information)
    {
        int p = beta.Length;
        score = new double[p];
        information = new double[p, p];
        double total = 0;
        foreach (var stratum in data.GroupBy(s => s.Stratum))
        {
            List<Subject> subjects = stratum.ToList();
            foreach (double time in subjects.Where(s => s.Event).Select(s => s.Time).Distinct())
            {
                double s0 = 0; double[] s1 = new double[p]; double[,] s2 = new double[p, p];
                foreach (Subject s in subjects.Where(s => s.Time >= time))
                {
                    double u = s.Frequency * Math.Exp(Enumerable.Range(0, p).Sum(k => beta[k] * s.Z[k]));
                    s0 += u;
                    for (int j = 0; j < p; j++) { s1[j] += u * s.Z[j]; for (int k = 0; k < p; k++) s2[j, k] += u * s.Z[j] * s.Z[k]; }
                }
                double d = 0;
                foreach (Subject s in subjects.Where(s => s.Event && s.Time == time))
                {
                    d += s.Frequency;
                    total += s.Frequency * Enumerable.Range(0, p).Sum(k => beta[k] * s.Z[k]);
                    for (int j = 0; j < p; j++) score[j] += s.Frequency * s.Z[j];
                }
                total -= d * Math.Log(s0);
                for (int j = 0; j < p; j++)
                {
                    score[j] -= d * s1[j] / s0;
                    for (int k = 0; k < p; k++) information[j, k] += d * (s2[j, k] / s0 - s1[j] * s1[k] / (s0 * s0));
                }
            }
        }
        return total;
    }

    private static double[,] Inverse(double[,] a)
    {
        int n = a.GetLength(0);
        double[,] w = new double[n, 2 * n];
        for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) w[i, j] = a[i, j]; w[i, n + i] = 1; }
        for (int c = 0; c < n; c++)
        {
            int pivot = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(w[r, c]) > Math.Abs(w[pivot, c])) pivot = r;
            for (int j = 0; j < 2 * n; j++) (w[c, j], w[pivot, j]) = (w[pivot, j], w[c, j]);
            double d = w[c, c];
            for (int j = 0; j < 2 * n; j++) w[c, j] /= d;
            for (int r = 0; r < n; r++)
                if (r != c) { double f = w[r, c]; for (int j = 0; j < 2 * n; j++) w[r, j] -= f * w[c, j]; }
        }
        double[,] inv = new double[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = w[i, n + j];
        return inv;
    }

    // the coefficients that make the log partial likelihood greatest, by Newton's method
    private static (double[] beta, double[] se, double ll, double ll0, double[,] information) Maximise(List<Subject> data, int p)
    {
        double[] beta = new double[p];
        double ll0 = LogLikelihood(data, beta, out _, out _);
        for (int iteration = 0; iteration < 60; iteration++)
        {
            LogLikelihood(data, beta, out double[] score, out double[,] information);
            double[,] inverse = Inverse(information);
            double change = 0;
            for (int j = 0; j < p; j++)
            {
                double step = 0; for (int k = 0; k < p; k++) step += inverse[j, k] * score[k];
                beta[j] += step;
                change = Math.Max(change, Math.Abs(step));
            }
            if (change < 1e-13) break;
        }
        double ll = LogLikelihood(data, beta, out _, out double[,] final);
        double[,] inv = Inverse(final);
        return (beta, Enumerable.Range(0, p).Select(j => Math.Sqrt(inv[j, j])).ToArray(), ll, ll0, final);
    }

    // The baseline survival and cumulative hazard at each record's time, from the definitions.  At a time with events, those at risk are the
    // subjects of the stratum whose times are no earlier.  The cumulative hazard rises by the number of events over the sum of the hazard
    // ratios of those at risk.  The survival is multiplied by alpha, where the sum over the subjects with events of
    // theta / (1 - alpha ^ theta) is the sum of the hazard ratios of those at risk: found by bisection, except that alpha is 0 if everybody
    // at risk has the event.
    private static void Baseline(List<Subject> data, double[] theta, out double[] survival, out double[] hazard)
    {
        int n = data.Count;
        survival = new double[n];
        hazard = new double[n];
        foreach (var stratum in Enumerable.Range(0, n).GroupBy(i => data[i].Stratum))
        {
            List<int> members = stratum.ToList();
            double s = 1, h = 0;
            Dictionary<double, (double s, double h)> at = new();
            foreach (double time in members.Select(i => data[i].Time).Distinct().OrderBy(t => t))
            {
                List<int> dead = members.Where(i => data[i].Event && data[i].Time == time).ToList();
                if (dead.Count > 0)
                {
                    List<int> risk = members.Where(i => data[i].Time >= time).ToList();
                    double riskTheta = risk.Sum(i => data[i].Frequency * theta[i]);
                    double d = dead.Sum(i => data[i].Frequency);
                    double atRisk = risk.Sum(i => data[i].Frequency);
                    double alpha;
                    if (d == atRisk) alpha = 0;
                    else
                    {
                        double low = 0, high = 1;
                        for (int it = 0; it < 200; it++)
                        {
                            double mid = (low + high) / 2;
                            double g = dead.Sum(i => data[i].Frequency * theta[i] / (1 - Math.Pow(mid, theta[i])));
                            if (g < riskTheta) low = mid; else high = mid;
                        }
                        alpha = (low + high) / 2;
                    }
                    s *= alpha;
                    h += d / riskTheta;
                }
                at[time] = (s, h);
            }
            foreach (int i in members) { survival[i] = at[data[i].Time].s; hazard[i] = at[data[i].Time].h; }
        }
    }

    private static ParameterBag Regression(List<Subject> data, int p, bool centre, double accuracy = 1e-12)
    {
        ParameterBag bag = new();
        bag.AddInput("times", new DataFrame(new DoubleVariable(data.Select(s => s.Time).ToArray(), "Time")));
        bag.AddInput("events", new DataFrame(new DoubleVariable(data.Select(s => s.Code).ToArray(), "Event")));
        DataFrame predictors = new(new DoubleVariable(data.Select(s => s.Z[0]).ToArray(), "Z1"));
        for (int k = 1; k < p; k++) predictors.Variables.Add(new DoubleVariable(data.Select(s => s.Z[k]).ToArray(), "Z" + (k + 1)));
        bag.AddInput("predictors", predictors);
        if (data.Select(s => s.Stratum).Distinct().Count() > 1)
        {
            ClassifierVariable strata = new();
            strata.Title = "Stratum";
            strata.Data = data.Select(s => s.Stratum == M ? M : s.Stratum - 1).ToArray();
            bag.AddInput("strata", new DataFrame(strata));
        }
        bag.AddInput("accuracy", accuracy);
        bag.AddInput("centre-continuous-covariates", centre);
        return bag;
    }

    // as the template processor does for an operation that follows another: what went in, and then the inputs that the report passes on
    private static ParameterBag PassedOn(ParameterBag given, StepOutput output, string name = null, object value = null)
    {
        ParameterBag passedOn = new();
        foreach (var pair in given.Pairs) passedOn[pair.Key] = pair.Value;
        foreach (var pair in output.ParameterBag.CopyWithoutOutputParameters().Pairs) passedOn[pair.Key] = pair.Value;
        if (name != null) passedOn[name] = FilledParameterFactory.Input(value);
        return passedOn;
    }

    private static List<ParameterBag> Rows(StepOutput output, string block) =>
        ((System.Collections.IEnumerable)output.ParameterBag[block].AsObject).Cast<ParameterBag>().ToList();

    private static double Worst(IEnumerable<int> which, Func<int, double> program, Func<int, double> reference)
    {
        double worst = 0;
        foreach (int i in which)
        {
            double a = program(i), b = reference(i);
            double difference = (double.IsNaN(a) || a == M) ? double.PositiveInfinity : Math.Abs(a - b) / Math.Max(1, Math.Abs(b));
            worst = Math.Max(worst, difference);
        }
        return worst;
    }

    // one set of data through the regression and every report that follows it
    private static void Run(string title, List<Subject> data, int p, bool centre)
    {
        int before = failures, checksBefore = checks;
        notes.Clear();
        int n = data.Count;
        ParameterBag bag = Regression(data, p, centre);
        try
        {
            StepOutput fit = Coxreg.RptCoxRegression(bag);
            ParameterBag passedOn = PassedOn(bag, fit);

            // the data as the fit should see them: the records in which nothing is missing, with a predictor that is not binary centred on its
            // mean over the subjects, if that was asked for
            List<int> complete = Enumerable.Range(0, n).Where(i => data[i].Complete).ToList();
            double[][] z = data.Select(s => (double[])s.Z.Clone()).ToArray();
            for (int k = 0; k < p; k++)
            {
                bool binary = complete.Select(i => data[i].Z[k]).Distinct().Count() == 2;
                if (centre && !binary)
                {
                    double mean = complete.Sum(i => data[i].Frequency * data[i].Z[k]) / complete.Sum(i => data[i].Frequency);
                    foreach (int i in complete) z[i][k] -= mean;
                }
            }
            List<Subject> used = complete.Select(i => data[i] with { Z = z[i] }).ToList();
            var reference = Maximise(used, p);

            double[,,] arr3 = (double[,,])fit.ParameterBag["ARR3"].AsObject;
            double[] b = Enumerable.Range(1, p).Select(i => arr3[1, i, 1]).ToArray();
            double[] se = Enumerable.Range(1, p).Select(i => arr3[1, i, 2]).ToArray();
            Check("coefficients", Worst(Enumerable.Range(0, p), j => b[j], j => reference.beta[j]), 1e-7);
            Check("standard errors", Worst(Enumerable.Range(0, p), j => se[j], j => reference.se[j]), 1e-7);
            Check("likelihood ratio chi-square", Math.Abs(fit.ParameterBag["x2"].AsDouble - 2 * (reference.ll - reference.ll0)), 1e-7);
            Say(fit.ParameterBag["df"].AsDouble == p, "degrees of freedom");
            Say(fit.ParameterBag["n"].AsDouble == used.Sum(s => s.Frequency), $"the number of subjects reported, {fit.ParameterBag["n"].AsDouble}, is {used.Sum(s => s.Frequency)}");
            Say(fit.ParameterBag["d"].AsDouble == used.Where(s => s.Event).Sum(s => s.Frequency), $"the number of events reported, {fit.ParameterBag["d"].AsDouble}, is {used.Where(s => s.Event).Sum(s => s.Frequency)}");
            List<ParameterBag> warnings = Rows(fit, "*warn");
            Say(warnings.Count == (complete.Count < n ? 1 : 0) && (complete.Count == n || warnings[0]["warn"].AsString.StartsWith((n - complete.Count) + " observations dropped")), "the warning of records left out is given when there are any, with their number");

            double[] theta = new double[n];
            foreach (int i in complete) theta[i] = Math.Exp(Enumerable.Range(0, p).Sum(k => b[k] * z[i][k]));
            double[] survival = new double[n], hazard = new double[n];
            Baseline(used, complete.Select(i => theta[i]).ToArray(), out double[] s0, out double[] h0);
            for (int k = 0; k < complete.Count; k++) { survival[complete[k]] = s0[k]; hazard[complete[k]] = h0[k]; }
            List<int> leftOut = Enumerable.Range(0, n).Except(complete).ToList();

            // the baseline, saved to the worksheet
            DataFrame results = Coxreg.RptCoxBaselineToWorksheet(passedOn).ParameterBag["results"].AsDataFrame;
            double[] ps = ((DoubleVariable)results.Variables[0]).Data, ph = ((DoubleVariable)results.Variables[1]).Data, pr = ((DoubleVariable)results.Variables[2]).Data;
            Say(ps.Length == n && ph.Length == n && pr.Length == n, "worksheet: a row for every record selected");
            Check("worksheet: hazard ratio of each record", Worst(complete, i => pr[i], i => theta[i]), 1e-9);
            Check("worksheet: baseline survival at the time of each record", Worst(complete, i => ps[i], i => survival[i]), 1e-8);
            Check("worksheet: baseline cumulative hazard at the time of each record", Worst(complete, i => ph[i], i => hazard[i]), 1e-9);
            Say(leftOut.All(i => ps[i] == M && ph[i] == M && pr[i] == M), "worksheet: the row of a record that was left out is empty");

            // the baseline, as a report: for each stratum in turn, a row for each time at which there is an event
            List<ParameterBag> rows = Rows(Coxreg.RptCoxBaselineToReport(passedOn), "*time");
            var expected = complete.Where(i => data[i].Event).GroupBy(i => (data[i].Stratum, data[i].Time)).OrderBy(g => g.Key.Stratum).ThenBy(g => g.Key.Time).ToList();
            Say(rows.Count == expected.Count, $"report: {rows.Count} rows for {expected.Count} times with an event");
            if (rows.Count == expected.Count)
            {
                double worstS = 0, worstH = 0, worstT = 0;
                for (int r = 0; r < rows.Count; r++)
                {
                    int i = expected[r].First();
                    worstT = Math.Max(worstT, Math.Abs(rows[r]["time"].AsDouble - data[i].Time));
                    worstS = Math.Max(worstS, Math.Abs(rows[r]["sur"].AsDouble - survival[i]));
                    worstH = Math.Max(worstH, Math.Abs(rows[r]["haz"].AsDouble - hazard[i]) / Math.Max(1, hazard[i]));
                }
                Check("report: times", worstT, 0);
                Check("report: survival", worstS, 1e-8);
                Check("report: cumulative hazard", worstH, 1e-9);
            }

            // the residuals and diagnostics
            StepOutput residuals = Coxreg.RptCoxResiduals(PassedOn(bag, fit, "save", true));
            DataFrame saved = residuals.ParameterBag["results"].AsDataFrame;
            double[] Column(int c) => ((DoubleVariable)saved.Variables[c]).Data;
            double[] leverage = Column(0), proportionality = Column(1), coxOakes = Column(2), coxSnell = Column(3), martingale = Column(4), deviance = Column(5);
            double[] rc = new double[n], rm = new double[n], rd = new double[n];
            foreach (int i in complete)
            {
                double delta = data[i].Event ? 1 : 0;
                rc[i] = theta[i] * hazard[i];
                rm[i] = delta - rc[i];
                rd[i] = Math.Sign(rm[i]) * Math.Sqrt(Math.Max(0, -2 * (rm[i] + (delta == 0 ? 0 : Math.Log(rc[i])))));
            }
            Check("Cox-Snell residuals", Worst(complete, i => coxSnell[i], i => rc[i]), 1e-9);
            Check("martingale residuals", Worst(complete, i => martingale[i], i => rm[i]), 1e-9);
            Check("deviance residuals", Worst(complete, i => deviance[i], i => rd[i]), 1e-9);
            Check("the residuals that the fit works out are the Cox-Snell residuals", Worst(complete, i => coxOakes[i], i => rc[i]), 1e-7);
            double[] mean2 = Enumerable.Range(0, p).Select(k => used.Sum(s => s.Frequency * s.Z[k]) / used.Sum(s => s.Frequency)).ToArray();
            Check("proportionality: the hazard ratio against a subject at the means of the predictors", Worst(complete, i => proportionality[i], i => Math.Exp(Enumerable.Range(0, p).Sum(k => b[k] * (z[i][k] - mean2[k])))), 1e-9);
            // leverage of a record with an event: d'Vd, d being what its predictors differ by from their mean over those at risk weighted by the hazard ratios
            double[,] v = Inverse(reference.information);
            double[] expectedLeverage = new double[n];
            List<int> events = complete.Where(i => data[i].Event).ToList();
            foreach (int i in events)
            {
                List<int> risk = complete.Where(j => data[j].Stratum == data[i].Stratum && data[j].Time >= data[i].Time).ToList();
                double total = risk.Sum(j => data[j].Frequency * theta[j]);
                double[] d = Enumerable.Range(0, p).Select(k => z[i][k] - risk.Sum(j => data[j].Frequency * theta[j] * z[j][k]) / total).ToArray();
                for (int a = 0; a < p; a++) for (int c = 0; c < p; c++) expectedLeverage[i] += d[a] * v[a, c] * d[c];
            }
            Check("leverage of the records with an event", Worst(events, i => leverage[i], i => expectedLeverage[i]), 1e-6);
            Say(complete.Where(i => !data[i].Event).All(i => leverage[i] == M), "leverage of a censored record is missing");
            Say(leftOut.All(i => Enumerable.Range(0, 6).All(c => Column(c)[i] == M)), "residuals: the row of a record that was left out is empty");

            // the plot of the deviance residuals against time: each residual against the time of its own record
            XyOptions byTime = (XyOptions)((ChartDefinition)Rows(residuals, "*chart")[0]["chart"].AsObject).ChartOptions;
            var wanted = complete.Select(i => (t: data[i].Time, r: rd[i])).OrderBy(q => q.t).ThenBy(q => q.r).ToList();
            var plotted = byTime.X.Zip(byTime.Y, (t, r) => (t, r)).OrderBy(q => q.t).ThenBy(q => q.r).ToList();
            Say(wanted.Count == plotted.Count, $"plot of residuals: {plotted.Count} points for {wanted.Count} records");
            if (wanted.Count == plotted.Count)
            {
                double worstPoint = 0;
                for (int k = 0; k < wanted.Count; k++) worstPoint = Math.Max(worstPoint, Math.Abs(wanted[k].t - plotted[k].t) + (plotted[k].r == M || double.IsNaN(plotted[k].r) ? double.PositiveInfinity : Math.Abs(wanted[k].r - plotted[k].r)));
                Check("plot of residuals: each residual against its own time", worstPoint, 1e-9);
            }

            // the hazard ratios and the model analysis
            List<ParameterBag> hazards = Rows(Coxreg.RptCoxHazardRatios(PassedOn(bag, fit, "gamma", 0.95)), "*hazard");
            double worstRatio = 0;
            for (int j = 0; j < p; j++)
            {
                double scale = Math.Exp(reference.beta[j]);
                worstRatio = Math.Max(worstRatio, Math.Abs(hazards[j]["ec"].AsDouble - scale) / scale);
                worstRatio = Math.Max(worstRatio, Math.Abs(hazards[j]["ell"].AsDouble - Math.Exp(reference.beta[j] - 1.959963984540054 * reference.se[j])) / scale);
                worstRatio = Math.Max(worstRatio, Math.Abs(hazards[j]["eul"].AsDouble - Math.Exp(reference.beta[j] + 1.959963984540054 * reference.se[j])) / scale);
            }
            Check("hazard ratios and their 95% confidence limits", worstRatio, 1e-6);
            StepOutput analysis = Coxreg.RptCoxModelAnalysis(passedOn);
            Check("model analysis: the two log likelihoods", Math.Max(Math.Abs(analysis.ParameterBag["ll"].AsDouble - reference.ll), Math.Abs(analysis.ParameterBag["ll0"].AsDouble - reference.ll0)), 1e-7);
            Say(analysis.ParameterBag["df"].AsDouble == p, "model analysis: degrees of freedom");

            // the plots of survival and hazard are made without complaint
            ParameterBag forPlots = PassedOn(bag, fit, "use-tics", true);
            forPlots["use-markers"] = FilledParameterFactory.Input(true);
            int choices = ((StringVariable)fit.ParameterBag["subgroups"].AsDataFrame.Variables[0]).Length;
            bool[] group = new bool[choices];
            group[0] = true;
            forPlots["group"] = FilledParameterFactory.Input(group);
            Say(Rows(Coxreg.RptCoxHazardPlots(forPlots), "*chart").Count >= 2, "plots of survival and cumulative hazard");
        }
        catch (Exception ex)
        {
            checks++;
            failures++;
            notes.Add("FAIL  " + Message(ex));
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {title}: {checks - checksBefore} checks");
        foreach (string note in notes) Console.WriteLine("         " + note);
    }

    private static List<Subject> Make(System.Random random, int n, bool ties, int strata, bool frequencies)
    {
        List<Subject> list = new();
        for (int i = 0; i < n; i++)
        {
            double z1 = Math.Round(random.NextDouble() * 4 - 2, 2), z2 = random.NextDouble() < 0.5 ? 0 : 1;
            double hazard = Math.Exp(0.6 * z1 - 0.8 * z2);
            double time = -Math.Log(1 - random.NextDouble()) / hazard * 10;
            time = ties ? Math.Ceiling(time) : Math.Round(time, 4) + (i + 1) * 1e-6;
            bool isEvent = random.NextDouble() < 0.75;
            list.Add(new Subject(time, isEvent ? (frequencies ? 1 + i % 3 : 1) : 0, new[] { z1, z2 }, strata > 1 ? i % strata + 1 : 1));
        }
        return list;
    }

    private static int Main()
    {
        System.Random random = new(11);
        Console.WriteLine("The regression and the reports that follow it");
        Run("no ties, one stratum, centred", Make(random, 40, false, 1, false), 2, true);
        Run("no ties, one stratum, not centred", Make(random, 40, false, 1, false), 2, false);
        Run("tied times, one stratum", Make(random, 60, true, 1, false), 2, true);
        Run("tied times, three strata", Make(random, 90, true, 3, false), 2, true);
        Run("tied times, records given as frequencies", Make(random, 60, true, 1, true), 2, true);

        // at times 3 and 6 there is one event and one censored record
        List<Subject> tiedCensored = new()
        {
            new(2, 1, new[] { 0.1, 0 }, 1), new(3, 1, new[] { 1.0, 1 }, 1), new(3, 0, new[] { -1.5, 0 }, 1), new(4, 1, new[] { 0.7, 1 }, 1),
            new(5, 0, new[] { -0.4, 0 }, 1), new(6, 1, new[] { 0.9, 1 }, 1), new(6, 0, new[] { -1.9, 0 }, 1), new(7, 1, new[] { -1.2, 1 }, 1),
            new(8, 1, new[] { 0.3, 0 }, 1), new(9, 0, new[] { -0.8, 1 }, 1), new(10, 1, new[] { 1.1, 1 }, 1), new(11, 1, new[] { -0.2, 0 }, 1),
            new(12, 0, new[] { 0.5, 1 }, 1), new(13, 1, new[] { -1.5, 0 }, 1), new(14, 0, new[] { 0.0, 1 }, 1), new(15, 1, new[] { 0.6, 0 }, 1)
        };
        Run("an event and a censored record at the same time", tiedCensored, 2, true);

        List<Subject> missingPredictor = Make(random, 40, true, 1, false);
        missingPredictor[7] = missingPredictor[7] with { Z = new[] { M, missingPredictor[7].Z[1] } };
        missingPredictor[20] = missingPredictor[20] with { Z = new[] { missingPredictor[20].Z[0], M } };
        Run("a predictor missing in two records, centred", missingPredictor, 2, true);
        Run("a predictor missing in two records, not centred", missingPredictor, 2, false);
        List<Subject> missingTime = Make(random, 40, true, 1, false);
        missingTime[5] = missingTime[5] with { Time = M };
        Run("the time missing in one record", missingTime, 2, true);
        List<Subject> missingCode = Make(random, 40, true, 1, false);
        missingCode[5] = missingCode[5] with { Code = M };
        Run("the event code missing in one record", missingCode, 2, true);
        List<Subject> missingStratum = Make(random, 60, true, 2, false);
        missingStratum[9] = missingStratum[9] with { Stratum = M };
        missingStratum[30] = missingStratum[30] with { Stratum = M };
        Run("the stratum missing in two records", missingStratum, 2, true);

        for (int set = 1; set <= 60; set++)
        {
            int n = 25 + random.Next(60);
            int strata = 1 + random.Next(3);
            bool frequencies = random.Next(2) == 0, centre = random.Next(2) == 0, ties = random.Next(4) != 0;
            List<Subject> data = Make(random, n, ties, strata, frequencies);
            int gaps = random.Next(4);
            for (int g = 0; g < gaps; g++)
            {
                int r = random.Next(n);
                switch (random.Next(4))
                {
                    case 0: data[r] = data[r] with { Time = M }; break;
                    case 1: data[r] = data[r] with { Code = M }; break;
                    case 2: data[r] = data[r] with { Z = new[] { M, data[r].Z[1] } }; break;
                    default: data[r] = data[r] with { Z = new[] { data[r].Z[0], M } }; break;
                }
            }
            Run($"set {set}: {n} records, {strata} strata, ties {ties}, frequencies {frequencies}, centred {centre}, {gaps} values missing", data, 2, centre);
        }

        Console.WriteLine();
        OtherChecks();

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}

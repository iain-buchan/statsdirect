// Agreement of categories from columns of ratings (Tables.RptKappa): two raters, whose table the program makes for itself, and three or more
// raters, for whom the kappa of each category and of all together is given with standard errors under the hypothesis of no agreement.  Those
// standard errors are checked against the spread of kappa in ratings drawn at random without agreement.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static int CompareLabels(string a, string b)
    {
        bool na = double.TryParse(a, out double x), nb = double.TryParse(b, out double y);
        return na && nb ? x.CompareTo(y) : string.Compare(a, b, StringComparison.CurrentCulture);
    }

    private static void FromColumns()
    {
        Console.WriteLine();
        Console.WriteLine("Agreement of categories, two raters, from columns of ratings");
        System.Random random = new(43);
        for (int set = 1; set <= 24; set++)
        {
            int k = 2 + set % 4, n = 40 + random.Next(100);
            // labels that are words, or numbers whose order as numbers is not their order as text
            string[] names = set % 2 == 0 ? new[] { "2", "10", "1", "30", "200" } : new[] { "mild", "absent", "severe", "moderate", "extreme" };
            string[] first = new string[n], second = new string[n];
            for (int s = 0; s < n; s++)
            {
                int i = random.Next(k);
                int j = random.NextDouble() < 0.6 ? i : random.Next(k);
                first[s] = names[i];
                // in a third of the sets the second rater never uses the last category, so that the table has to be made square
                if (set % 3 == 0 && j == k - 1 && k > 2) j = 0;
                second[s] = names[j];
                if (set % 5 == 0 && s % 11 == 3) first[s] = null;
                if (set % 5 == 0 && s % 13 == 5) second[s] = null;
            }
            string[] order = first.Concat(second).Where(l => l != null).Distinct().OrderBy(l => l, Comparer<string>.Create(CompareLabels)).ToArray();
            int g = order.Length;
            double[,] o = new double[g, g];
            for (int s = 0; s < n; s++)
                if (first[s] != null && second[s] != null) o[Array.IndexOf(order, first[s]), Array.IndexOf(order, second[s])]++;
            int method = 1 + set % 3;
            double[,] w = method <= 2 ? Weights(g, method) : new double[g, g];
            if (method == 3)
                for (int i = 0; i < g; i++)
                    for (int j = 0; j < g; j++)
                        w[i, j] = i == j ? 1 : i < j ? Math.Round(0.8 / (j - i), 2) : Math.Round(0.4 / (i - j), 2);
            double gamma = new[] { 0.95, 0.9, 0.99 }[set % 3];
            string title = $"set {set}: {g} categories ({(set % 2 == 0 ? "numbers" : "words")}), {n} subjects"
                + (set % 3 == 0 && k > 2 ? ", a category unused by the second rater" : "") + (set % 5 == 0 ? ", some ratings missing" : "")
                + new[] { "", ", linear weights", ", quadratic weights", ", weights given, not symmetric" }[method];
            int before = failures;
            try
            {
                ParameterBag bag = new();
                DataFrame frame = new(Classifier("First", first));
                frame.Variables.Add(Classifier("Second", second));
                bag.AddInput("responses", frame);
                bag.AddInput("ci", gamma);
                bag.AddInput("method", method.ToString());
                if (method == 3)
                {
                    ParameterBag sizes = Tables.RptKappaSizeWeights(bag).ParameterBag;
                    Say(sizes["ycats"].AsInt32 == g && sizes["xcats"].AsInt32 == g, title + ": the size asked of the table of weights");
                    DataFrame weights = new(new DoubleVariable(Enumerable.Range(0, g).Select(i => w[i, 0]).ToArray(), "W1"));
                    for (int j = 1; j < g; j++) weights.Variables.Add(new DoubleVariable(Enumerable.Range(0, g).Select(i => w[i, j]).ToArray(), "W" + (j + 1)));
                    bag.AddInput("weights", weights);
                }
                ParameterBag report = Tables.RptKappa(new Plain(), bag).ParameterBag;
                // the table as the report prints it: a row for each category of the first rater
                List<ParameterBag> rows = Rows(report, "*y");
                double worst = rows.Count == g ? 0 : double.PositiveInfinity;
                for (int i = 0; i < g && rows.Count == g; i++)
                {
                    if (rows[i]["y"].AsString != order[i]) worst = double.PositiveInfinity;
                    List<ParameterBag> cells = Rows(rows[i], "*tot");
                    for (int j = 0; j < g; j++) worst = Math.Max(worst, Math.Abs(cells[j]["tot"].AsDouble - o[i, j]));
                }
                Check(title + ": the table, its categories in order", worst, 0);
                TwoRaters(title, report, o, w, gamma);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
            if (failures == before) Console.WriteLine("ok    " + title);
        }

        // two raters without a category in common: the table has every category of both, and every subject
        {
            string title = "two raters without a category in common";
            try
            {
                string[] first = { "b", "a", "a", "b", "a", "b", "b" }, second = { "c", "d", "c", "c", "d", "d", "d" };
                ParameterBag bag = new();
                DataFrame frame = new(Classifier("First", first));
                frame.Variables.Add(Classifier("Second", second));
                bag.AddInput("responses", frame);
                bag.AddInput("ci", 0.95);
                bag.AddInput("method", "1");
                ParameterBag report = Tables.RptKappa(new Plain(), bag).ParameterBag;
                List<ParameterBag> rows = Rows(report, "*y");
                double[,] expected = { { 0, 0, 1, 2 }, { 0, 0, 2, 2 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };
                double worst = rows.Count == 4 ? 0 : double.PositiveInfinity;
                for (int i = 0; i < 4 && rows.Count == 4; i++)
                {
                    if (rows[i]["y"].AsString != new[] { "a", "b", "c", "d" }[i]) worst = double.PositiveInfinity;
                    List<ParameterBag> cells = Rows(rows[i], "*tot");
                    for (int j = 0; j < 4; j++) worst = Math.Max(worst, Math.Abs(cells[j]["tot"].AsDouble - expected[i, j]));
                }
                Check(title + ": the table", worst, 0, true);
                Check(title + ": no agreement observed, and kappa below nothing", Math.Abs(report["po"].AsDouble) + Math.Max(0, report["kappa"].AsDouble), 0, true);
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
        }
    }

    // kappa of one category against the rest, from the number of ratings of each subject (m) and the number of them in the category (x)
    private static double KappaOfCategory(double[] m, double[] x, out double p)
    {
        double n = 0, total = 0, positive = 0, within = 0;
        for (int i = 0; i < m.Length; i++)
        {
            if (m[i] == 0) continue;
            n++; total += m[i]; positive += x[i];
            within += x[i] * (m[i] - x[i]) / m[i];
        }
        double mean = total / n;
        p = positive / total;
        return 1 - within / (n * (mean - 1) * p * (1 - p));
    }

    private static void ManyRaters()
    {
        Console.WriteLine();
        Console.WriteLine("Agreement of categories, three or more raters");
        System.Random random = new(47);
        for (int set = 1; set <= 24; set++)
        {
            int cats = 2 + set % 4, raters = 3 + random.Next(5), n = 30 + random.Next(60);
            bool gaps = set % 3 == 0;
            string[] names = { "b", "a", "d", "c", "e" };
            string[][] columns = Enumerable.Range(0, raters).Select(r => new string[n]).ToArray();
            for (int s = 0; s < n; s++)
            {
                int truth = random.Next(cats);
                for (int r = 0; r < raters; r++)
                {
                    columns[r][s] = names[random.NextDouble() < 0.5 ? truth : random.Next(cats)];
                    if (gaps && random.Next(6) == 0) columns[r][s] = null;
                }
            }
            if (gaps) for (int r = 0; r < raters; r++) columns[r][7] = null;        // a subject whom nobody rated
            double gamma = new[] { 0.95, 0.9, 0.99 }[set % 3], z = NormalQuantile(1 - (1 - gamma) / 2);
            string title = $"set {set}: {cats} categories, {raters} raters, {n} subjects" + (gaps ? ", some ratings missing" : "");
            int before = failures;
            try
            {
                ParameterBag bag = new();
                DataFrame frame = new(Classifier("R1", columns[0]));
                for (int r = 1; r < raters; r++) frame.Variables.Add(Classifier("R" + (r + 1), columns[r]));
                bag.AddInput("responses", frame);
                bag.AddInput("ci", gamma);
                ParameterBag report = Tables.RptKappa(new Plain(), bag).ParameterBag;
                string[] order = names.Take(cats).OrderBy(l => l, StringComparer.CurrentCulture).ToArray();
                double[] m = Enumerable.Range(0, n).Select(s => (double)columns.Count(c => c[s] != null)).ToArray();
                double rated = m.Count(v => v > 0), mean = m.Sum() / rated, harmonic = rated / m.Where(v => v > 0).Sum(v => 1 / v);
                bool constant = m.Where(v => v > 0).Distinct().Count() == 1;
                Say(report["categories"].AsInt32 == cats, title + ": the number of categories");
                if (cats == 2)
                {
                    double[] x = Enumerable.Range(0, n).Select(s => (double)columns.Count(c => c[s] == order[0])).ToArray();
                    double kappa = KappaOfCategory(m, x, out double p), q = 1 - p;
                    double se = Math.Sqrt(2 * (harmonic - 1) + (mean - harmonic) * (1 - 4 * p * q) / (mean * p * q)) / ((mean - 1) * Math.Sqrt(rated * harmonic));
                    Check(title + ": kappa", Math.Abs(report["k"].AsDouble - kappa), 1e-12);
                    Check(title + ": its standard error", Relative(report["se"].AsDouble, se), 1e-12);
                    Check(title + ": z and P", Math.Abs(report["z"].AsDouble - kappa / se) / Math.Max(1, Math.Abs(kappa / se)) + Math.Abs(report["p"].AsDouble - NormalUpper(kappa / se)), 1e-6);
                    Check(title + ": interval", Math.Abs(report["ll"].AsDouble - (kappa - z * se)) + Math.Abs(report["ul"].AsDouble - (kappa + z * se)), 1e-7);
                }
                else
                {
                    List<ParameterBag> rows = Rows(report, "*cats");
                    Say(rows.Count == cats && Enumerable.Range(0, cats).All(j => rows[j]["resp"].AsString == order[j]), title + ": a row for each category, in order");
                    double numerator = 0, denominator = 0, skew = 0, worst = 0, worstSe = 0;
                    for (int j = 0; j < cats; j++)
                    {
                        double[] x = Enumerable.Range(0, n).Select(s => (double)columns.Count(c => c[s] == order[j])).ToArray();
                        double kappa = KappaOfCategory(m, x, out double p), q = 1 - p;
                        numerator += p * q * kappa; denominator += p * q; skew += p * q * (q - p);
                        worst = Math.Max(worst, Math.Abs(rows[j]["k"].AsDouble - kappa));
                        if (constant) worstSe = Math.Max(worstSe, Relative(rows[j]["se"].AsDouble, Math.Sqrt(2 / (rated * mean * (mean - 1)))));
                        else Say(rows[j]["se"].AsDouble == M, title + ": no standard error when the number of ratings varies");
                    }
                    Check(title + ": kappa of each category", worst, 1e-12);
                    Check(title + ": kappa of all together", Math.Abs(report["kc"].AsDouble - numerator / denominator), 1e-12);
                    if (constant)
                    {
                        Check(title + ": standard error of each", worstSe, 1e-12);
                        double se = Math.Sqrt(2) / (denominator * Math.Sqrt(rated * mean * (mean - 1))) * Math.Sqrt(denominator * denominator - skew);
                        Check(title + ": z of all together", Relative(report["zc"].AsDouble, numerator / denominator / se), 1e-10);
                        Check(title + ": interval", Math.Abs(report["ll"].AsDouble - (numerator / denominator - z * se)) + Math.Abs(report["ul"].AsDouble - (numerator / denominator + z * se)), 1e-7);
                    }
                    else Say(report["ll"].AsDouble == M && report["ul"].AsDouble == M, title + ": no interval when the number of ratings varies");
                }
            }
            catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
            if (failures == before) Console.WriteLine("ok    " + title);
        }

        // the standard errors under the hypothesis of no agreement, against the spread of kappa in ratings drawn without agreement
        Console.WriteLine();
        Console.WriteLine("The standard errors for three or more raters, against ratings drawn at random");
        foreach ((int cats, bool varying) in new[] { (2, false), (2, true), (3, false), (4, false) })
        {
            int n = 150, raters = 6, draws = 20000;
            double[] chance = cats == 2 ? new[] { 0.3, 0.7 } : cats == 3 ? new[] { 0.2, 0.3, 0.5 } : new[] { 0.1, 0.2, 0.3, 0.4 };
            double[] m = Enumerable.Range(0, n).Select(s => varying ? 2.0 + s % 5 : raters).ToArray();
            double mean = m.Average(), harmonic = n / m.Sum(v => 1 / v);
            double[][] kappas = Enumerable.Range(0, cats + 1).Select(j => new double[draws]).ToArray();
            for (int d = 0; d < draws; d++)
            {
                double[][] x = Enumerable.Range(0, cats).Select(j => new double[n]).ToArray();
                for (int s = 0; s < n; s++)
                    for (int r = 0; r < m[s]; r++)
                    {
                        double u = random.NextDouble(), sum = 0;
                        for (int j = 0; j < cats; j++) { sum += chance[j]; if (u < sum || j == cats - 1) { x[j][s]++; break; } }
                    }
                double numerator = 0, denominator = 0;
                for (int j = 0; j < cats; j++)
                {
                    kappas[j][d] = KappaOfCategory(m, x[j], out double p);
                    numerator += p * (1 - p) * kappas[j][d]; denominator += p * (1 - p);
                }
                kappas[cats][d] = numerator / denominator;
            }
            double Spread(double[] v) { double a = v.Average(); return Math.Sqrt(v.Sum(t => (t - a) * (t - a)) / (v.Length - 1)); }
            string title = $"{cats} categories, {(varying ? "2 to 6" : raters.ToString())} raters, {n} subjects, {draws} draws";
            if (cats == 2)
            {
                double p = chance[0], q = 1 - p;
                double se = Math.Sqrt(2 * (harmonic - 1) + (mean - harmonic) * (1 - 4 * p * q) / (mean * p * q)) / ((mean - 1) * Math.Sqrt(n * harmonic));
                Check(title + ": standard error of kappa", Math.Abs(Spread(kappas[0]) / se - 1), 0.03, true);
            }
            else
            {
                double each = Math.Sqrt(2 / (n * mean * (mean - 1)));
                for (int j = 0; j < cats; j++) Check(title + $": standard error of the kappa of category {j + 1}", Math.Abs(Spread(kappas[j]) / each - 1), 0.03, true);
                double denominator = chance.Sum(p => p * (1 - p)), skew = chance.Sum(p => p * (1 - p) * (1 - 2 * p));
                double se = Math.Sqrt(2) / (denominator * Math.Sqrt(n * mean * (mean - 1))) * Math.Sqrt(denominator * denominator - skew);
                Check(title + ": standard error of the kappa of all together", Math.Abs(Spread(kappas[cats]) / se - 1), 0.03, true);
            }
        }
    }
}

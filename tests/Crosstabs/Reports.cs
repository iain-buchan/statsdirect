// The Crosstabs report: what it asks, the categories of its tables, the scores of the user, the tables in strata against the
// meta-analysis of the same tables, and the random tables of the simulation.
using System.Reflection;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void Reports()
    {
        Console.WriteLine();
        Console.WriteLine("The report");
        int before = failures;

        // The dialog that asks for the scores gives them back in the bag, as "values1" and "values2", and leaves the options as they
        // were: the host of these checks does the same, so that the analysis is checked as the program runs it
        {
            string said = null;
            Thread thread = new(() =>
            {
                try
                {
                    ScoresOptions options = new() { Title1 = "Row scores", Title2 = "Column scores" };
                    for (int i = 1; i <= 3; i++) options.Values1.Add(i);
                    for (int i = 1; i <= 4; i++) options.Values2.Add(i);
                    Type type = typeof(Tables).Assembly.GetType("StatsDirect.UI.ctlScores");
                    object control = Activator.CreateInstance(type, options);
                    DataGridView grid = (DataGridView)type.GetField("gridScores", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
                    double[] first = { 10, 20, 40 }, second = { 1, 3, 4, 8 };
                    for (int i = 0; i < 3; i++) grid[0, i].Value = first[i].ToString();
                    for (int i = 0; i < 4; i++) grid[1, i].Value = second[i].ToString();
                    ParameterBag bag = new();
                    type.GetMethod("Fill").Invoke(control, new object[] { bag, true });
                    bool given = bag.ContainsKey("values1") && bag.ContainsKey("values2") && ((double[])bag["values1"].AsObject).SequenceEqual(first) && ((double[])bag["values2"].AsObject).SequenceEqual(second);
                    said = given ? "" : "the bag has " + string.Join(", ", bag.Keys);
                }
                catch (Exception ex) { said = Message(ex); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Say(said == "", "the dialog of the scores gives what was typed back as \"values1\" and \"values2\" (" + said + ")");
        }

        // scores that the user gives are the scores of the analysis
        {
            double[][] t = { new double[] { 17, 9, 8, 4 }, new double[] { 6, 5, 3, 9 }, new double[] { 3, 5, 4, 12 } };
            double[] rs = { 10, 20, 40 }, cs = { 1, 3, 4, 8 };
            ParameterBag bag = Analysis(t, 0.95, rs, cs, false);
            double[] shownRows = Rows(bag, "*rows").Select(r => Rows(r, "*score")[0]["score"].AsDouble).ToArray();
            double[] shownColumns = Rows(Rows(bag, "*scores")[0], "*score").Select(r => r["score"].AsDouble).ToArray();
            Say(shownRows.SequenceEqual(rs) && shownColumns.SequenceEqual(cs), $"scores 10, 20, 40 and 1, 3, 4, 8 are given: the report shows {string.Join(", ", shownRows)} and {string.Join(", ", shownColumns)}");
            // the correlation of the two scores over the 95 subjects, from the subjects themselves
            List<double> x = new(), y = new();
            for (int i = 0; i < 3; i++) for (int j = 0; j < 4; j++) for (int k = 0; k < t[i][j]; k++) { x.Add(rs[i]); y.Add(cs[j]); }
            double mx = x.Average(), my = y.Average();
            double r = x.Zip(y, (a, b) => (a - mx) * (b - my)).Sum() / Math.Sqrt(x.Sum(a => (a - mx) * (a - mx)) * y.Sum(b => (b - my) * (b - my)));
            Say(Math.Abs(bag["r"].AsDouble - r) <= 1e-12 && Math.Abs(bag["chit"].AsDouble - (x.Count - 1) * r * r) <= 1e-9, $"and the correlation is that of the scores given ({bag["r"].AsDouble}; from the subjects {r})");
            HostProxy.CancelScores = true;
            bag = Analysis(t, 0.95, rs, cs, false);
            HostProxy.CancelScores = false;
            shownRows = Rows(bag, "*rows").Select(row => Rows(row, "*score")[0]["score"].AsDouble).ToArray();
            Say(shownRows.SequenceEqual(new double[] { 1, 2, 3 }), "the dialog of the scores is cancelled: the scores are 1, 2, 3");
        }

        // the questions are put once, and each column variable starts from the categories of the row variable
        {
            string[] rows = { "1", "2", "1", "2", "1", "2", "1", "2", "2", "1", "1", "2" };
            string[] first = { "1", "2", "3", "1", "2", "3", "3", "1", "2", "2", "1", "3" };
            string[] second = { "1", "2", "2", "1", "1", "2", "2", "1", "2", "1", "1", "2" };
            DataFrame columns = Classifier("First", first);
            columns.Variables.Add(Classifier("Second", second).Variables[0]);
            HostProxy.Symmetrise = true;
            HostProxy.Questions.Clear();
            ParameterBag bag = Crosstabs(Classifier("Rows", rows), columns, null, 0.95, null, null, null, false);
            HostProxy.Symmetrise = false;
            Say(HostProxy.Questions.Count == 1, $"a table of 2 rows and 3 columns, and one of 2 and 2: one question is put ({HostProxy.Questions.Count}: {string.Join("; ", HostProxy.Questions)})");
            string[] shape = Rows(bag, "*columns").Select(c => { ParameterBag x = Rows(c, "*xtab")[0]; return string.Join(",", Rows(x, "*y").Select(v => v["y"].AsString)) + " by " + string.Join(",", Rows(x, "*x").Select(v => v["x"].AsString)); }).ToArray();
            Say(shape.Length == 2 && shape[0] == "1,2,3 by 1,2,3" && shape[1] == "1,2 by 1,2", $"the first table is made symmetrical and the second is as it is ({string.Join("; ", shape)})");
            // the categories in the order of their labels: numbers as numbers
            string[] labels = { "10", "9", "100", "9", "10", "100", "2" };
            string[] other = { "b", "a", "B", "a", "b", "B", "a" };
            bag = Crosstabs(Classifier("Rows", labels), Classifier("Columns", other), null, 0.95, null, null, null, false);
            ParameterBag table = Rows(Rows(bag, "*columns")[0], "*xtab")[0];
            string order = string.Join(",", Rows(table, "*y").Select(v => v["y"].AsString)) + " by " + string.Join(",", Rows(table, "*x").Select(v => v["x"].AsString));
            Say(order == "2,9,10,100 by B,a,b", $"labels that are numbers are put in the order of the numbers, and others in the order of their characters ({order})");
            // labels of which some are numbers and some are not: the numbers first, in order of size, and then the others
            string[] mixed = { "10", "10a", "1.0", "9", "10a", "9", "10", "1.0", "1st", "2" };
            string[] beside = { "b", "a", "B", "a", "b", "B", "a", "b", "a", "B" };
            bag = Crosstabs(Classifier("Rows", mixed), Classifier("Columns", beside), null, 0.95, null, null, null, false);
            table = Rows(Rows(bag, "*columns")[0], "*xtab")[0];
            order = string.Join(",", Rows(table, "*y").Select(v => v["y"].AsString)) + " by " + string.Join(",", Rows(table, "*x").Select(v => v["x"].AsString));
            Say(order == "1.0,2,9,10,10a,1st by B,a,b", $"labels that are numbers are put before those that are not ({order})");
            // a subject without a value of a classifier is left out
            string[] some = { "1", null, "2", "1", "2", "* (missing)", "1", "2" };
            string[] more = { "1", "1", null, "2", "2", "2", "1", "1" };
            bag = Crosstabs(Classifier("Rows", some), Classifier("Columns", more), null, 0.95, null, null, null, false);
            double n = Rows(Rows(Rows(bag, "*columns")[0], "*chirxc")[0], "*tot").Last()["tot"].AsDouble;
            Say(n == 5, $"8 subjects of whom 3 lack a value of a classifier: the table is of 5 ({n})");
        }

        // The 2 by 2 tables of the strata are pooled as the meta-analysis of the same tables pools them: the second category of the
        // rows is the first group, and the second category of the columns is the event
        {
            double[][][] t =
            {
                new[] { new double[] { 30, 12 }, new double[] { 25, 20 } }, new[] { new double[] { 14, 3 }, new double[] { 10, 9 } },
                new[] { new double[] { 41, 0 }, new double[] { 36, 5 } }, new[] { new double[] { 8, 6 }, new double[] { 5, 11 } },
                new[] { new double[] { 22, 17 }, new double[] { 19, 26 } },
            };
            var (r, c, s) = Subjects(t);
            double[] events1 = t.Select(k => k[1][1]).ToArray(), total1 = t.Select(k => k[1][0] + k[1][1]).ToArray();
            double[] events2 = t.Select(k => k[0][1]).ToArray(), total2 = t.Select(k => k[0][0] + k[0][1]).ToArray();
            foreach (var (study, block) in new[] { ("casecontrol", "mantel"), ("cohort", "rrmeta") })
            {
                Dictionary<string, object> here = Report("x", () => Rows(Rows(Crosstabs(Classifier("Rows", r), Classifier("Columns", c), Classifier("Strata", s), 0.95, study, null, null), "*columns")[0], "*" + block)[0]);
                Dictionary<string, object> there = Report("x", () =>
                {
                    ParameterBag b = new();
                    b.AddInput("gamma", 0.95);
                    b.AddInput("sr", new DataFrame(new DoubleVariable(events1, "r1"))); b.AddInput("sn", new DataFrame(new DoubleVariable(total1, "n1")));
                    b.AddInput("xr", new DataFrame(new DoubleVariable(events2, "r2"))); b.AddInput("xn", new DataFrame(new DoubleVariable(total2, "n2")));
                    return (study == "casecontrol" ? Meta.RptMantel(HostProxy.New(), b) : Meta.RptRelativeRiskMeta(HostProxy.New(), b)).ParameterBag;
                });
                int same = 0, other = 0; string first = "";
                foreach (var f in there)
                {
                    if (f.Value is not double expected || !here.TryGetValue(f.Key, out object value)) continue;
                    bool equal = value is double given && (given == expected || Math.Abs(given - expected) <= 1e-12 * Math.Max(1, Math.Abs(expected)));
                    if (equal) same++; else { other++; if (first == "") first = $"{f.Key}: {value} v {expected}"; }
                }
                Say(other == 0 && same >= 60, $"five 2 by 2 tables, {study}: {same} figures are those of the meta-analysis of the same tables, {other} are not ({first})");
            }
        }

        // The random tables of the simulation have the totals of the table, and a cell of them has the mean and the variance of the
        // hypergeometric distribution: with totals in the tens, and with totals whose product is above what 32 bits hold
        foreach (int scale in new[] { 1, 2000 })
        {
            int[] rowTotals = { 0, 30 * scale, 50 * scale, 20 * scale }, columnTotals = { 0, 45 * scale, 55 * scale };
            int n = rowTotals.Sum(), draws = 20000, bad = 0;
            int[,] x = new int[4, 3];
            double[] fact = new double[3];
            int[] work = new int[3];
            bool primed = false; int total = 0, most = 5000000;
            StatsDirect.Numerics.MersenneTwister rng = new();
            rng.Seed(2718);
            double sum = 0, squares = 0;
            string failed = null;
            for (int i = 0; i < draws && failed == null; i++)
            {
                try
                {
                    Chi.Rcont2(1, 3, 2, rowTotals, columnTotals, ref primed, ref x, ref fact, ref total, ref most, ref work, out int fault, ref rng);
                    if (fault != 0) failed = "fault " + fault;
                }
                catch (Exception ex) { failed = ex.GetType().Name; }
                for (int r = 1; r <= 3; r++) if (x[r, 1] + x[r, 2] != rowTotals[r] || x[r, 1] < 0 || x[r, 2] < 0) bad++;
                if (x[1, 1] + x[2, 1] + x[3, 1] != columnTotals[1]) bad++;
                sum += x[1, 1]; squares += (double)x[1, 1] * x[1, 1];
            }
            double mean = sum / draws, variance = squares / draws - mean * mean;
            double r1 = rowTotals[1], c1 = columnTotals[1];
            double expected = r1 * c1 / n, expectedVariance = r1 * c1 * (n - r1) * (n - c1) / ((double)n * n * (n - 1.0));
            Say(failed == null && bad == 0 && Math.Abs(mean - expected) <= 4.5 * Math.Sqrt(expectedVariance / draws) && Math.Abs(variance / expectedVariance - 1) <= 4.5 * Math.Sqrt(2.0 / draws),
                $"random tables of {n} subjects: {failed ?? "drawn"}, {bad} without the totals; a cell has the mean {mean:F3} (expected {expected:F3}) and the variance {variance:F3} (expected {expectedVariance:F3})");
        }

        // the scores that the tests in strata can make from the counts, against their definitions from the subjects
        {
            double[] sum = { 0, 7, 0, 12, 3, 9 };
            int len = 5; double n = 31;
            double[] midrank = new double[len + 1]; double gone = 0;
            for (int i = 1; i <= len; i++) { double total = 0; for (int s = 1; s <= sum[i]; s++) total += gone + s; midrank[i] = sum[i] > 0 ? total / sum[i] : gone + 0.5; gone += sum[i]; }
            double[] logrank = new double[len + 1]; double atRisk = n, hazard = 0;
            for (int i = 1; i <= len; i++) { hazard += atRisk > 0 ? sum[i] / atRisk : 0; logrank[i] = 1 - hazard; atRisk -= sum[i]; }
            foreach (int kind in new[] { 2, 3, 4, 5 })
            {
                double[] ab = new double[len + 1];
                Tables.Cmhrcs(kind, len, len, sum, (double[])sum.Clone(), ab);
                double[] expected = kind == 3 ? midrank : kind == 5 ? logrank : midrank.Select(v => v / n).ToArray();
                Say(Enumerable.Range(1, len).All(i => Math.Abs(ab[i] - expected[i]) <= 1e-12), $"scores of kind {kind} from the totals 7, 0, 12, 3, 9: {string.Join(", ", ab.Skip(1))}");
            }
            // what is wrong with what a test in strata is given
            double[] table = { 0, 3, 1, 2, 4, 5, 2, 1, 3, 2, 2, 4, 1 };
            foreach (var (what, type, rowScores, columnScores, counts, wanted) in new (string, int, double[], double[], double[], int)[]
            {
                ("a test of kind 4", 4, new double[] { 0, 1, 2, 3 }, new double[] { 0, 1, 2 }, table, 4),
                ("scores of the columns that are all the same, for the mean scores", 2, new double[] { 0, 2, 2, 2 }, new double[] { 0, 1, 2 }, table, 10),
                ("scores of the rows that are all the same, for the correlation", 3, new double[] { 0, 1, 2, 3 }, new double[] { 0, 5, 5 }, table, 12),
                ("a count below nothing", 1, new double[] { 0, 1, 2, 3 }, new double[] { 0, 1, 2 }, table.Select((v, i) => i == 4 ? -1.0 : v).ToArray(), 13),
                ("nothing wrong", 3, new double[] { 0, 1, 2, 3 }, new double[] { 0, 1, 2 }, table, 0),
            })
            {
                Tables.Gencmh(2, 2, 3, counts, rowScores, columnScores, type, out double x2, out double df, out double p, out int fault);
                Say(fault == wanted, $"{what}: fault {wanted} ({fault})");
            }
        }
        if (failures == before) Console.WriteLine("ok    the scores of the user, the questions, the categories, the tables in strata, the random tables, the scores from the counts");
    }
}

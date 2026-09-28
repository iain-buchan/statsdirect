// The Fisher-Freeman-Halton exact test of tables with large totals, of tables that the check of the keys used to refuse, and of
// tables at the limit of the keys; and the progress bar of the test, with which it can be stopped.
using System.Globalization;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    // The analysis of a table with the exact test, as the r by c chi-square test makes it
    private static ParameterBag Analysis(int[,] t, bool exact = true)
    {
        int rows = t.GetLength(0), cols = t.GetLength(1);
        double[,] o = new double[rows + 1, cols + 1];
        for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) o[i + 1, j + 1] = t[i, j];
        double cco = 0.95;
        return Tables.SChi(HostProxy.New(), ref cco, o, rows, cols, exact, false, false, false, false, false, false, 0.99, 1000, 1);
    }

    private static Dictionary<string, int[,]> Cases(string file)
    {
        Dictionary<string, int[,]> cases = new();
        string[] lines = File.ReadAllLines(file);
        for (int at = 0; at < lines.Length; at++)
        {
            string[] head = lines[at].Split('\t');
            if (head[0] != "case") continue;
            int rows = int.Parse(head[3], inv), cols = int.Parse(head[4], inv);
            int[,] t = new int[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                string[] v = lines[at + 1 + i].Split('\t');
                for (int j = 0; j < cols; j++) t[i, j] = int.Parse(v[j], inv);
            }
            cases[head[1]] = t;
            at += rows;
        }
        return cases;
    }

    // Tables of which all the rows but the last have few subjects, and the last has hundreds or thousands in each column: the number
    // of tables with the same totals is small enough for them to be listed, and the column totals are large enough for the keys of
    // the search for the least sum to be above what a whole number of 32 bits holds
    private static void LargeTotals()
    {
        Console.WriteLine();
        Console.WriteLine("The exact test of tables with large totals against every table with the same totals");
        int before = failures, compared = 0;
        double worst = 0;
        List<int[,]> tables = new() { Table("1 2 0 1 0 0 / 0 0 1 0 0 1 / 1 1 1 0 1 0 / 294 264 274 201 268 186") };
        // rows of few subjects, columns, the most subjects in such a row, the least and the greatest count of the last row, tables
        foreach (int[] set in new[] { new[] { 4, 5, 3, 200, 400, 10 }, new[] { 4, 5, 3, 400, 3000, 10 }, new[] { 3, 6, 4, 100, 300, 16 }, new[] { 3, 4, 5, 1200, 4000, 10 }, new[] { 5, 6, 2, 80, 200, 6 } })
        {
            System.Random random = new(20260928 + set[0] * 100 + set[1]);
            for (int made = 0; made < set[5]; made++)
            {
                int[,] t = new int[set[0] + 1, set[1]];
                for (int i = 0; i < set[0]; i++)
                {
                    int n = 1 + random.Next(set[2]);
                    for (int s = 0; s < n; s++) t[i, random.Next(set[1])]++;
                }
                for (int j = 0; j < set[1]; j++) t[set[0], j] = set[3] + random.Next(set[4] - set[3] + 1);
                tables.Add(t);
            }
        }
        foreach (int[,] t in tables)
        {
            int rows = t.GetLength(0), cols = t.GetLength(1);
            int[] r = Enumerable.Range(0, rows).Select(i => Enumerable.Range(0, cols).Sum(j => t[i, j])).ToArray();
            int[] c = Enumerable.Range(0, cols).Select(j => Enumerable.Range(0, rows).Sum(i => t[i, j])).ToArray();
            var all = EveryTable(r, c, t);
            object given = Analysis(t)["p2"].AsObject;
            compared++;
            double by = given is double p ? Math.Abs(p - all.p) / all.p : double.PositiveInfinity;
            if (by > worst && !double.IsInfinity(by)) worst = by;
            Say(by <= 1e-8, $"the table {Text(t)}: P = {given}; from every table with its totals ({all.tables}) {all.p}");
            // the two bounds of the method for the totals of the table
            int[] rs = (int[])r.Clone(), cs = (int[])c.Clone();
            Array.Sort(rs); Array.Sort(cs);
            var (least, greatest) = Bounds(rs, cs, all.least, all.greatest);
            Say(least == null, $"row totals {string.Join(",", rs)}, column totals {string.Join(",", cs)}: the least sum {all.least}; the routine gives {least}");
            Say(greatest == null, $"row totals {string.Join(",", rs)}, column totals {string.Join(",", cs)}: the greatest sum {all.greatest}; the routine gives {greatest}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {compared} tables, and the two bounds of each; the greatest difference of a P value from the sum over every table is {worst:G3} of it");
    }

    // Tables that the check of the keys refused though their keys can be held, and tables at the limit of the keys, against R
    private static void WiderTables(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("The exact test of tables that were refused, and of tables at the limit of the keys");
        int before = failures, compared = 0;
        double worst = 0;
        Dictionary<string, int[,]> cases = Cases(Path.Combine(folder, "cases-exact-large.txt"));
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "r-exact-large.txt")))
        {
            string[] part = line.Split('\t');
            string name = part[0].Split('|')[0];
            // the test of this table takes a quarter of a minute: it is for the progress bar, below
            if (name == "wider04") continue;
            object given = Analysis(cases[name])["p2"].AsObject;
            compared++;
            if (part[1] == "\"refused\"")
                Say(given is string said && said == "not possible, use Monte Carlo", $"{name}, whose keys cannot be held: the test is given as {given}");
            else
            {
                double expected = double.Parse(part[1], inv);
                double by = given is double p ? Math.Abs(p - expected) / expected : double.PositiveInfinity;
                if (by > worst && !double.IsInfinity(by)) worst = by;
                Say(by <= 1e-8, $"{name} ({Text(cases[name])}): P = {given}, benchmark {part[1]}");
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {compared} tables; the greatest difference of a P value from its benchmark is {worst:G3} of it");
    }

    private static void ProgressOfTheTest(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("The progress bar of the exact test");
        int before = failures;
        Dictionary<string, int[,]> cases = Cases(Path.Combine(folder, "cases-exact-large.txt"));
        try
        {
            // a test that takes less than a second shows no bar
            RecordedProgress recorded = new();
            HostProxy.Progress = recorded;
            Analysis(cases["wider01"]);
            Say(recorded.Bars.Count == 0, $"a test of less than a second: {recorded.Bars.Count} bars were shown");

            // a test of some seconds: a bar for each stage that is met after the first second, and the P value that there is without
            HostProxy.Progress = null;
            object without = Analysis(cases["wider05"])["p2"].AsObject;
            recorded = new();
            HostProxy.Progress = recorded;
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            object with = Analysis(cases["wider05"])["p2"].AsObject;
            double took = watch.Elapsed.TotalSeconds;
            Say(without is double a && with is double b && a == b, $"the P value with the bar is {with}, and without it {without}");
            Say(recorded.Bars.Count >= 1 && recorded.Bars.Count <= 3, $"a test of {took:F1} seconds: {recorded.Bars.Count} bars were shown");
            int stage = 0;
            foreach (RecordedProgress.Bar bar in recorded.Bars)
            {
                var words = System.Text.RegularExpressions.Regex.Match(bar.Words, @"^Fisher-Freeman-Halton exact test: stage (\d+) of 3$");
                Say(words.Success && int.Parse(words.Groups[1].Value, inv) > stage, $"the words of a bar, after stage {stage}: {bar.Words}");
                if (words.Success) stage = int.Parse(words.Groups[1].Value, inv);
                bool inOrder = bar.Shares.Count > 0 && bar.Shares[0] >= 0 && bar.Shares[^1] <= 1;
                for (int i = 1; i < bar.Shares.Count; i++) if (bar.Shares[i] < bar.Shares[i - 1]) inOrder = false;
                Say(inOrder, $"the shares of \"{bar.Words}\" are from 0 to 1 and none is less than the one before: {string.Join(" ", bar.Shares.Select(s => s.ToString("F3", inv)))}");
                Say(bar.Finished == 1, $"\"{bar.Words}\" was finished {bar.Finished} times");
            }
            // the bar is brought up to date 10 times a second after the first second
            double expected = (took - 1) * 10;
            Say(recorded.Updates >= expected * 0.5 && recorded.Updates <= expected * 1.2 + 2, $"the bar was brought up to date {recorded.Updates} times in {took:F1} seconds");

            // the user stops the test: no P value, no hybrid approximation, and the rest of the analysis as it is without the test
            ParameterBag plain = Analysis(cases["wider04"], false);
            recorded = new() { StopAt = 3 };
            HostProxy.Progress = recorded;
            watch.Restart();
            ParameterBag stopped = Analysis(cases["wider04"]);
            took = watch.Elapsed.TotalSeconds;
            Say(stopped["p2"].AsObject is string said && said == "not calculated (stopped)", $"a test that was stopped: the P value is given as {stopped["p2"].AsObject}");
            Say(stopped["lb"].AsObject is string label && label == "", $"a test that was stopped: the label of the P value is \"{stopped["lb"].AsObject}\"");
            Say(recorded.Updates == 3 && took < 5, $"a test that was stopped at the third time that the bar was brought up to date: {recorded.Updates} times, {took:F1} seconds");
            Say(recorded.Bars.All(b => !b.Words.Contains("hybrid")) && recorded.Bars.All(b => b.Finished == 1),
                $"a test that was stopped: the bars were {string.Join("; ", recorded.Bars.Select(b => b.Words + ", finished " + b.Finished))}");
            foreach (string name in new[] { "chio", "dfo", "po", "g2", "pog2" })
                Say(stopped[name].AsObject.Equals(plain[name].AsObject), $"a test that was stopped: {name} is {stopped[name].AsObject}, and without the test {plain[name].AsObject}");

            // what goes wrong in the showing of the bar is not taken for a fault of the test
            recorded = new() { FailAt = 2 };
            HostProxy.Progress = recorded;
            string outcome;
            try { outcome = "P = " + Analysis(cases["wider04"])["p2"].AsObject; }
            catch (Exception ex) { outcome = Message(ex); }
            Say(outcome == "InvalidOperationException: the bar failed", $"a bar that fails: {outcome}");
            Say(recorded.Bars.Count == 1 && recorded.Bars[0].Finished == 1, $"a bar that fails: {recorded.Bars.Count} bars, the first finished {(recorded.Bars.Count > 0 ? recorded.Bars[0].Finished : 0)} times");
        }
        finally
        {
            HostProxy.Progress = null;
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  no bar for a short test; a bar for each stage of a long one; the test stopped; a bar that fails");
    }
}

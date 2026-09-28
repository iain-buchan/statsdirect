// The Frequencies analysis: checks by calculation.  Columns are handed to the analysis as the program reads them from a worksheet, and
// what it gives is compared with benchmarks worked out in R (in the folder benchmarks, with the scripts that made them) and with
// figures worked out here from the definitions: the frequency of each value, the percentages, the cumulative frequencies, and the
// order of the rows.
using System.Globalization;
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static class Program
{
    private const string Missing = "* (missing)";
    private static int failures, checks;
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;

    private static void Say(bool ok, string what)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("FAIL  " + what); }
    }

    private static string Message(Exception ex)
    {
        Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return inner.GetType().Name + ": " + inner.Message.Replace('\n', ' ').Replace('\r', ' ');
    }

    // The columns of a block of a worksheet as the program makes them for an analysis of categories in which the missing values are
    // kept.  A cell that is empty or has an asterisk, down to the last row of the block in which a column has something, is made a
    // value of the category "* (missing)": that is done by the routine of the program.  The categories of a column are then taken
    // in the order in which they are first met, as the program takes them.
    private static DataFrame Read(string[] titles, string[][] columns)
    {
        int rows = columns.Max(c => c.Length);
        string[,] hold = new string[Math.Max(rows, 1), columns.Length];
        for (int c = 0; c < columns.Length; c++)
            for (int r = 0; r < hold.GetLength(0); r++)
                hold[r, c] = r < columns[c].Length ? (columns[c][r] ?? "").Trim() : "";
        Type reader = typeof(Describe).Assembly.GetType("StatsDirect.UI.CellArrayProcessor");
        reader.GetMethod("PreprocessCellArrayCategoryReplaceMissing", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 0, hold });
        DataFrame frame = new();
        for (int c = 0; c < columns.Length; c++)
        {
            List<string> found = new();
            List<int> counts = new();
            ClassifierVariable v = new() { Title = titles[c] };
            int size = 0;
            for (int r = 0; r < rows; r++)
            {
                string cell = hold[r, c];
                if (string.IsNullOrEmpty(cell) || cell == "*") continue;
                int at = found.IndexOf(cell);
                if (at < 0) { found.Add(cell); counts.Add(0); at = found.Count - 1; }
                counts[at]++;
                size++;
                v.EnsureLength(size);
                v.Data[size - 1] = cell.Contains(Missing) ? Constant.MISSING : at;
            }
            v.EnsureGroups(found.Count);
            for (int i = 0; i < found.Count; i++) v.Groups[i] = new Group(found[i], i) { NBin = counts[i] };
            frame.Variables.Add(v);
        }
        return frame;
    }

    private sealed class Row
    {
        public string Value;
        public int Frequency;
        public object Percent, Cumulative, CumulativePercent;
    }

    private sealed class Table
    {
        public string Title;
        public int Total;
        // what the report says after the total: the records that are missing and those that are not; nothing where none is missing
        public List<(object missing, object others)> AfterTotal;
        public List<Row> Rows = new();
    }

    private static List<Table> Report(DataFrame frame, string by, string order)
    {
        ParameterBag p = new();
        p.AddInput("data", frame);
        p.AddInput("sortBy", by);
        p.AddInput("sortOrder", order);
        ParameterBag output = Describe.RptFrequency(p).ParameterBag;
        List<Table> tables = new();
        foreach (ParameterBag variable in (System.Collections.IEnumerable)output["*variable"].AsObject)
        {
            Table table = new() { Title = (string)variable["ti"].AsObject, Total = (int)variable["n"].AsObject };
            if (variable.ContainsKey("*missing"))
            {
                table.AfterTotal = new();
                foreach (ParameterBag said in (System.Collections.IEnumerable)variable["*missing"].AsObject) table.AfterTotal.Add((said["k"].AsObject, said["m"].AsObject));
            }
            foreach (ParameterBag bin in (System.Collections.IEnumerable)variable["*bin"].AsObject)
                table.Rows.Add(new Row { Value = (string)bin["x"].AsObject, Frequency = (int)bin["fx"].AsObject, Percent = bin["pc"].AsObject, Cumulative = bin["cm"].AsObject, CumulativePercent = bin["pc2"].AsObject });
            tables.Add(table);
        }
        return tables;
    }

    // The order of the labels, from its definition: a label that is a number is before one that is not; numbers are in order of
    // size; labels that are not numbers, and labels that are the same number, are in the order of their characters; a label that
    // is empty is before every other
    private static (int kind, decimal number) Place(string label) =>
        label.Length == 0 ? (-1, 0m) : decimal.TryParse(label, NumberStyles.Float | NumberStyles.AllowThousands, inv, out decimal number) ? (0, number) : (1, 0m);

    private static IOrderedEnumerable<T> InOrderOfLabel<T>(IEnumerable<T> items, Func<T, string> label) =>
        items.OrderBy(i => Place(label(i)).kind).ThenBy(i => Place(label(i)).number).ThenBy(i => label(i), StringComparer.Ordinal);

    // The order of two labels as the program has it; nothing if the program has no routine for it (it had none before the order was
    // corrected)
    private static readonly MethodInfo compareLabels = typeof(Formatting).GetMethod("CompareLabels", BindingFlags.Public | BindingFlags.Static);

    private static int Compare(string x, string y) => (int)compareLabels.Invoke(null, new object[] { x, y });

    private static readonly string[][] Orders = { new[] { "value", "asc" }, new[] { "value", "desc" }, new[] { "frequency", "asc" }, new[] { "frequency", "desc" } };

    // What the analysis is to give for a column, from the definitions
    private static (int total, List<(string value, int frequency)> rows, int missing) Expected(string[] cells, int lastRow, string by, string order)
    {
        Dictionary<string, int> counts = new();
        int missing = 0;
        for (int r = 0; r <= lastRow; r++)
        {
            string cell = r < cells.Length ? (cells[r] ?? "").Trim() : "";
            if (cell.Length == 0 || cell == "*") { missing++; continue; }
            counts[cell] = counts.TryGetValue(cell, out int had) ? had + 1 : 1;
        }
        IEnumerable<KeyValuePair<string, int>> inOrder = InOrderOfLabel(counts, c => c.Key);
        if (by == "value") { if (order == "desc") inOrder = inOrder.Reverse(); }
        else inOrder = order == "asc" ? inOrder.OrderBy(c => c.Value) : inOrder.OrderByDescending(c => c.Value);   // the sort keeps the order of the values among equal frequencies
        return (lastRow + 1, inOrder.Select(c => (c.Key, c.Value)).ToList(), missing);
    }

    private static int LastRow(string[][] columns)
    {
        int last = -1;
        foreach (string[] column in columns)
            for (int r = column.Length - 1; r > last; r--)
                if (!string.IsNullOrEmpty(column[r]?.Trim()) && column[r].Trim() != "*") { last = r; break; }
        return last;
    }

    private static string Short(string[] cells) => cells.Length <= 24 ? string.Join(" ", cells.Select(c => string.IsNullOrEmpty(c) ? "[]" : c)) : $"{cells.Length} cells that begin {string.Join(" ", cells.Take(12).Select(c => string.IsNullOrEmpty(c) ? "[]" : c))}";

    // One table of the analysis against the definitions; nothing is said if it agrees
    private static string Differs(Table table, string[] cells, int lastRow, string by, string order)
    {
        var expected = Expected(cells, lastRow, by, order);
        if (table.Total != expected.total) return $"Total = {table.Total}, and the column has {expected.total} cells";
        // after the total: how many are missing and how many are not, where any are missing
        if (table.AfterTotal == null && expected.missing > 0) return $"{expected.missing} of {expected.total} records are missing, and the analysis has nothing for the report to say of them after the total";
        if (expected.missing == 0 && table.AfterTotal != null && table.AfterTotal.Count != 0) return "no record is missing, and the report says that some are";
        if (expected.missing > 0 && !(table.AfterTotal.Count == 1 && table.AfterTotal[0].missing is int k && k == expected.missing && table.AfterTotal[0].others is int m && m == expected.total - expected.missing))
            return $"{expected.missing} of {expected.total} records are missing, and the report is to say after the total: {string.Join("; ", table.AfterTotal.Select(a => a.missing + " missing, " + a.others + " others"))}";
        List<Row> rows = table.Rows;
        int first = 0;
        if (expected.missing > 0)
        {
            if (rows.Count == 0 || rows[0].Value != Missing) return "the first row is not that of the missing values";
            if (rows[0].Frequency != expected.missing) return $"the missing values are given as {rows[0].Frequency}, and are {expected.missing}";
            if (!(rows[0].Percent is string p && p == "na" && rows[0].Cumulative is string c && c == "na" && rows[0].CumulativePercent is string q && q == "na")) return "the row of the missing values has a percentage or a cumulative frequency";
            first = 1;
        }
        if (rows.Count - first != expected.rows.Count) return $"{rows.Count - first} rows of values, and the column has {expected.rows.Count} values";
        int n = expected.total - expected.missing, cumulative = 0;
        for (int i = 0; i < expected.rows.Count; i++)
        {
            Row row = rows[first + i];
            if (row.Value != expected.rows[i].value) return $"row {i + 1} is the value {row.Value}, and is to be {expected.rows[i].value}: {string.Join(" ", rows.Skip(first).Select(r => r.Value).Take(30))}";
            if (row.Frequency != expected.rows[i].frequency) return $"the frequency of {row.Value} is given as {row.Frequency}, and is {expected.rows[i].frequency}";
            cumulative += row.Frequency;
            if (!(row.Cumulative is int cm && cm == cumulative)) return $"the cumulative frequency at {row.Value} is given as {row.Cumulative}, and is {cumulative}";
            if (!(row.Percent is double pc && Math.Abs(pc - 100.0 * row.Frequency / n) <= 1e-12 * 100)) return $"the percentage of {row.Value} is given as {row.Percent}, and is {100.0 * row.Frequency / n}";
            if (!(row.CumulativePercent is double pc2 && Math.Abs(pc2 - 100.0 * cumulative / n) <= 1e-12 * 100)) return $"the cumulative percentage at {row.Value} is given as {row.CumulativePercent}, and is {100.0 * cumulative / n}";
        }
        if (expected.rows.Count > 0 && cumulative != n) return $"the last cumulative frequency is {cumulative}, and the values that are not missing are {n}";
        return null;
    }

    private static void AgainstDefinitions(string what, string[][] columns, ref int tables)
    {
        string[] titles = Enumerable.Range(1, columns.Length).Select(i => "column " + i).ToArray();
        int last = LastRow(columns);
        foreach (string[] o in Orders)
        {
            List<Table> given;
            try { given = Report(Read(titles, columns), o[0], o[1]); }
            catch (Exception ex) { Say(false, $"{what}, by {o[0]} {o[1]}: {Message(ex)}: {Short(columns[0])}"); continue; }
            Say(given.Count == columns.Length, $"{what}: {given.Count} tables for {columns.Length} columns");
            for (int c = 0; c < Math.Min(given.Count, columns.Length); c++)
            {
                string differs = given[c].Title != titles[c] ? "the title is " + given[c].Title : Differs(given[c], columns[c], last, o[0], o[1]);
                tables++;
                Say(differs == null, $"{what}, by {o[0]} {o[1]}: {differs}: {Short(columns[c])}");
            }
        }
    }

    // The order of two labels: that it is an order, and that it is the order of the definition
    private static void OrderOfLabels()
    {
        Console.WriteLine("The order of the labels of categories");
        int before = failures;
        string[] labels = { "0", "1", "2", "3", "9", "10", "20", "100", "-1", "-10", "0.5", ".5", "2.25", "1e2", "01", "1.0", "007", "+3", "1,000", " 4", "a", "b", "A", "B", "yes", "no", "1st",
            "2nd", "10a", "x1", "ward 2", "ward 10", "#", "+", "~", "-", "", "* (missing)", "NaN", "Infinity", "-Infinity", "1e999", "0x10", "1 2", "1-2", "10-12", "3-4" };
        if (compareLabels == null)
        {
            Say(false, "the program has no routine for the order of two labels");
            return;
        }
        int pairs = 0, triples = 0, wrongPair = 0, wrongTriple = 0, wrongPlace = 0;
        string example = "";
        foreach (string x in labels)
            foreach (string y in labels)
            {
                pairs++;
                int c = Math.Sign(Compare(x, y)), back = Math.Sign(Compare(y, x));
                if (c != -back || (x == y) != (c == 0)) { wrongPair++; example = $"\"{x}\" and \"{y}\": {c} and {back}"; }
                // the definition
                var (kx, nx) = Place(x); var (ky, ny) = Place(y);
                int expected = kx != ky ? kx.CompareTo(ky) : nx != ny ? nx.CompareTo(ny) : Math.Sign(string.CompareOrdinal(x, y));
                if (c != expected) { wrongPlace++; example = $"\"{x}\" and \"{y}\": {c}, and by the definition {expected}"; }
            }
        foreach (string x in labels)
            foreach (string y in labels)
                foreach (string z in labels)
                {
                    triples++;
                    if (Compare(x, y) < 0 && Compare(y, z) < 0 && Compare(x, z) >= 0) { wrongTriple++; example = $"\"{x}\" before \"{y}\" before \"{z}\", but not \"{x}\" before \"{z}\""; }
                }
        Say(wrongPair == 0, $"{wrongPair} of {pairs} pairs of labels are not in the opposite order when they are given the other way round: {example}");
        Say(wrongTriple == 0, $"{wrongTriple} of {triples} threes of labels are not in one order: {example}");
        Say(wrongPlace == 0, $"{wrongPlace} of {pairs} pairs of labels are not in the order of the definition: {example}");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {labels.Length} labels: {pairs} pairs and {triples} threes");
    }

    private static void Benchmarks(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("The columns of the benchmarks");
        int before = failures, compared = 0;
        string[] lines = File.ReadAllLines(Path.Combine(folder, "cases-frequencies.txt"));
        Dictionary<string, object> figures = new();
        for (int at = 0; at + 1 < lines.Length; at += 2)
        {
            string name = lines[at].Split('\t')[1];
            string[] cells = lines[at + 1].Split('\t').Select(c => c == "NA" ? "" : c).ToArray();
            foreach (string[] o in Orders)
            {
                string key = $"{name}|{o[0]}.{o[1]}";
                try
                {
                    Table table = Report(Read(new[] { name }, new[] { cells }), o[0], o[1])[0];
                    List<Row> rows = table.Rows.Where(r => r.Value != Missing).ToList();
                    figures[key + "|total"] = table.Total;
                    figures[key + "|missing"] = table.Rows.Where(r => r.Value == Missing).Sum(r => r.Frequency);
                    figures[key + "|rows"] = rows.Count;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        figures[$"{key}|{i + 1}|value"] = rows[i].Value;
                        figures[$"{key}|{i + 1}|frequency"] = rows[i].Frequency;
                        figures[$"{key}|{i + 1}|percent"] = rows[i].Percent;
                        figures[$"{key}|{i + 1}|cumulative"] = rows[i].Cumulative;
                        figures[$"{key}|{i + 1}|cumulative.percent"] = rows[i].CumulativePercent;
                    }
                }
                catch (Exception ex) { figures[key + "|error"] = Message(ex); }
            }
        }
        Dictionary<string, (int n, int bad, string worst)> kinds = new();
        foreach (string line in File.ReadAllLines(Path.Combine(folder, "r-frequencies.txt")))
        {
            string[] part = line.Split('\t');
            string kind = part[0].Substring(part[0].LastIndexOf('|') + 1);
            bool have = figures.TryGetValue(part[0], out object value);
            bool same;
            if (part[1].StartsWith("\"")) same = have && value is string text && "\"" + text + "\"" == part[1];
            else
            {
                double expected = double.Parse(part[1], inv);
                same = have && (value is int i ? i == expected : value is double d && Math.Abs(d - expected) <= 1e-10 * Math.Max(1, Math.Abs(expected)));
            }
            compared++;
            var k = kinds.TryGetValue(kind, out var had) ? had : (0, 0, "");
            k.Item1++;
            if (!same) { k.Item2++; if (k.Item3 == "") k.Item3 = $"{part[0]}: {(have ? value : "not given")}, benchmark {part[1]}"; }
            kinds[kind] = k;
        }
        foreach (var k in kinds.OrderBy(k => k.Key, StringComparer.Ordinal))
            Say(k.Value.bad == 0, $"{k.Key}: {k.Value.bad} of {k.Value.n} figures differ from their benchmarks; the first is {k.Value.worst}");
        foreach (string key in figures.Keys.Where(k => k.EndsWith("|error"))) Say(false, $"{key}: {figures[key]}");
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  against the benchmarks: {compared} figures compared, in {kinds.Count} kinds");
    }

    private static void Columns()
    {
        Console.WriteLine();
        Console.WriteLine("Columns drawn at random, against the definitions");
        int before = failures, tables = 0;
        string[] numbers = { "0", "1", "2", "3", "9", "10", "20", "100", "-1", "-10", "0.5", "2.25", "1e2", "01", "1.0", "007" };
        string[] words = { "a", "b", "A", "B", "yes", "no", "1st", "2nd", "10a", "x1", "ward 2", "ward 10", "#", "+", "~", "1-2", "10-12", "3-4" };
        System.Random random = new(20260928);
        string[] Draw(string[] pool, int cells, double missing, int most)
        {
            // a column of so many cells from some of the labels of the pool, a share of the cells being empty or an asterisk
            string[] some = pool.OrderBy(_ => random.Next()).Take(1 + random.Next(Math.Min(most, pool.Length))).ToArray();
            string[] column = Enumerable.Range(0, cells).Select(_ => random.NextDouble() < missing ? (random.Next(2) == 0 ? "" : "*") : some[random.Next(some.Length)]).ToArray();
            // a block in which no cell has anything is not a column of data (the program takes its first row for a missing value)
            if (column.All(cell => cell.Length == 0 || cell == "*")) column[0] = some[0];
            return column;
        }
        string[][] pools = { numbers, words, numbers.Concat(words).ToArray() };
        string[] kinds = { "numbers", "labels that are not numbers", "numbers and labels that are not" };
        for (int trial = 0; trial < 300; trial++)
        {
            int kind = trial % 3;
            double missing = trial % 4 == 0 ? 0 : 0.25 * random.NextDouble();
            AgainstDefinitions("a column of " + kinds[kind], new[] { Draw(pools[kind], 1 + random.Next(60), missing, 34) }, ref tables);
        }
        // several columns of different lengths in one block: a column that ends before another has missing values to the end of the block
        for (int trial = 0; trial < 40; trial++)
        {
            string[][] block = Enumerable.Range(0, 2 + random.Next(4)).Select(_ => Draw(pools[random.Next(3)], 1 + random.Next(40), 0.1, 34)).ToArray();
            AgainstDefinitions("a block of " + block.Length + " columns", block, ref tables);
        }
        // columns of thousands of cells and hundreds of values, which a sort does not do as it does a few
        string[] many = Enumerable.Range(0, 400).Select(i => (i * 7 - 300).ToString(inv)).Concat(Enumerable.Range(0, 150).Select(i => "w" + i)).Concat(numbers).Concat(words).ToArray();
        for (int trial = 0; trial < 12; trial++)
            AgainstDefinitions("a column of thousands", new[] { Draw(many, 2000 + random.Next(4000), trial % 2 == 0 ? 0 : 0.05, many.Length) }, ref tables);
        // columns at the limits
        AgainstDefinitions("one cell", new[] { new[] { "7" } }, ref tables);
        AgainstDefinitions("cells that are all the same", new[] { new[] { "x", "x", "x", "x" } }, ref tables);
        AgainstDefinitions("empty cells after the last value", new[] { new[] { "2", "", "1", "2", "", "*", "" } }, ref tables);
        AgainstDefinitions("empty cells before the first value", new[] { new[] { "", "*", "2", "1", "2" } }, ref tables);
        AgainstDefinitions("a column of missing values beside another", new[] { new[] { "", "", "*" }, new[] { "1", "2", "2" } }, ref tables);
        AgainstDefinitions("spaces about the values", new[] { new[] { " 1", "1 ", "2", " a ", "a" } }, ref tables);
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {tables} tables of the analysis");
    }

    private static void Numbers()
    {
        Console.WriteLine();
        Console.WriteLine("Columns that are handed over as numbers");
        int before = failures, tables = 0;
        double m = Constant.MISSING;
        System.Random random = new(271828);
        List<double[]> columns = new()
        {
            new[] { 3.0, 3, 4, 1, 1, 2, 5, 3 }, new[] { m, 3, 3, 4, 1 }, new[] { m, m, 3, 3, 4, 1 }, new[] { 3, m, 3, 4, 1 }, new[] { 3, 3, 4, 1, m }, new[] { m, m }, new double[0], new[] { 2.5 }, new[] { -1, 0.5, -10, 100, 0.5 }
        };
        for (int trial = 0; trial < 60; trial++)
            columns.Add(Enumerable.Range(0, 1 + random.Next(200)).Select(_ => random.NextDouble() < 0.15 ? m : Math.Round(random.Next(-5, 12) * 0.5, 1)).ToArray());
        foreach (double[] column in columns)
        {
            // the cells of the column as text, for the definitions: every cell of the column is in the analysis
            string[] cells = column.Select(v => v == m ? "" : v.ToString(inv)).ToArray();
            foreach (string[] o in Orders)
            {
                tables++;
                string differs;
                try { differs = Differs(Report(new DataFrame(new DoubleVariable((double[])column.Clone(), "numbers")), o[0], o[1])[0], cells, cells.Length - 1, o[0], o[1]); }
                catch (Exception ex) { differs = Message(ex); }
                Say(differs == null, $"a column of numbers, by {o[0]} {o[1]}: {differs}: {Short(cells)}");
            }
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {tables} tables of the analysis");
    }

    // The frequencies that the bar chart of frequencies is drawn from: the labels in order, and for each column the frequency of each
    // label, or its share of the column where there is more than one column
    private static void Chart()
    {
        Console.WriteLine();
        Console.WriteLine("The frequencies of the bar chart");
        int before = failures, charts = 0;
        MethodInfo routine = typeof(Sheet).GetMethod("ValuesToFrequencies", BindingFlags.NonPublic | BindingFlags.Static);
        System.Random random = new(161803);
        string[] pool = { "0", "1", "2", "10", "20", "-1", "0.5", "a", "b", "B", "1st", "10a", "ward 2", "ward 10" };
        for (int trial = 0; trial < 60; trial++)
        {
            string[][] columns = Enumerable.Range(0, 1 + random.Next(3)).Select(_ => Enumerable.Range(0, 5 + random.Next(40)).Select(_ => random.NextDouble() < 0.1 ? "" : pool[random.Next(pool.Length)]).ToArray()).ToArray();
            int last = LastRow(columns);
            charts++;
            string differs = null;
            try
            {
                ParameterBag p = new();
                p.AddInput("rawValues", Read(columns.Select((_, i) => "column " + (i + 1)).ToArray(), columns));
                ParameterBag output = ((StepOutput)routine.Invoke(null, new object[] { p })).ParameterBag;
                string[] labels = ((StringVariable)output["labels"].AsDataFrame.Variables[0]).Data;
                HashSet<string> met = new();
                foreach (string[] column in columns)
                    for (int r = 0; r <= last && r < column.Length; r++)
                        if (!string.IsNullOrEmpty(column[r]) && column[r] != "*") met.Add(column[r]);
                string[] expected = InOrderOfLabel(met, l => l).ToArray();
                if (!labels.SequenceEqual(expected)) differs = $"the labels are {string.Join(" ", labels)}, and are to be {string.Join(" ", expected)}";
                DataFrame values = output["values"].AsDataFrame;
                for (int c = 0; c < columns.Length && differs == null; c++)
                {
                    double[] given = ((DoubleVariable)values.Variables[c]).Data;
                    int n = columns[c].Take(last + 1).Count(cell => !string.IsNullOrEmpty(cell) && cell != "*");
                    for (int i = 0; i < expected.Length && differs == null; i++)
                    {
                        int count = columns[c].Take(last + 1).Count(cell => cell == expected[i]);
                        double share = columns.Length > 1 ? (n > 0 ? (double)count / n : count) : count;
                        if (Math.Abs(given[i] - share) > 1e-12) differs = $"column {c + 1}, label {expected[i]}: {given[i]}, and is to be {share}";
                    }
                }
            }
            catch (Exception ex) { differs = Message(ex); }
            Say(differs == null, $"the frequencies of a bar chart of {columns.Length} columns: {differs}");
        }
        Console.WriteLine($"{(failures == before ? "ok  " : "FAIL")}  {charts} charts");
    }

    private static int Main(string[] args)
    {
        // the labels of a worksheet are read as numbers in the way of the country of the computer: here, in one way on every computer
        CultureInfo.CurrentCulture = inv;
        string which = args.Length > 0 ? args[0] : "all";
        string folder = Path.Combine(AppContext.BaseDirectory, "benchmarks");
        if (which is "all" or "order") OrderOfLabels();
        if (which is "all" or "benchmarks") Benchmarks(folder);
        if (which is "all" or "columns") Columns();
        if (which is "all" or "numbers") Numbers();
        if (which is "all" or "chart") Chart();
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? $"ALL {checks} CHECKS PASS" : $"{failures} OF {checks} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}

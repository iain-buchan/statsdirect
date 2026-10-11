using System.Text.Json;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // The three analyses whose data are two-level: the replicated two-way and the nested analyses of variance take a two-level frame
    // (blocks of treatments, groups of subgroups), the grouped covariance its own structure of groups with the predictor's levels and
    // the outcome's replicates at each.  Each is run through the engine on the help's example, filled as the grid fills it, and the
    // script written from the record must give the help's figures.
    private static void CheckTwoLevel()
    {
        RRecipes.Folder = Metadata("Recipes");
        string examples = Metadata("Examples");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "two-level");
        Directory.CreateDirectory(folder);
        List<string> failures = new();
        void Compare(string op, OperationRun run)
        {
            RScript script = RScriptWriter.Write(Record(run), null, null);
            if (!script.HasRecipe) { failures.Add(op + ": no recipe"); return; }
            string ours = Figures(RunR(script.Text, Path.Combine(folder, op)));
            string expected = Figures(RunR(File.ReadAllText(Path.Combine(examples, op + ".R")).Replace("\r", ""), Path.Combine(folder, op + "-help")));
            if (ours == expected) return;
            string[] a = ours.Split(' '), b = expected.Split(' ');
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
            failures.Add($"{op}: differs from the {i + 1}th figure: run {(i < a.Length ? a[i] : "(end)")}, help {(i < b.Length ? b[i] : "(end)")} ({a.Length} and {b.Length} figures)");
        }
        DataFrame2D TwoLevel(double[][][] cells)   // [outer][inner] = the values; the grid's frame by columns has untitled variables
        {
            DataFrame2D frame = new();
            foreach (double[][] outer in cells)
                frame.Variables.Add(outer.Select(values => (IVariable)new DoubleVariable(values, "")).ToList());
            return frame;
        }
        try
        {
            // the replicated two-way analysis: Variables[block][treatment], each the repeats (the help's three subjects, three treatments, three repeats)
            var (replicate, _) = Run("ReplicateTwoWay", new() { ["data2d"] = TwoLevel(new[] {
                new[] { new double[] { 9.8, 10.1, 9.8 }, new double[] { 9.9, 9.5, 10.0 }, new double[] { 11.3, 10.7, 10.7 } },
                new[] { new double[] { 9.2, 8.6, 9.2 }, new double[] { 9.1, 9.1, 9.4 }, new double[] { 10.3, 10.7, 10.2 } },
                new[] { new double[] { 8.4, 7.9, 8.0 }, new double[] { 8.6, 8.0, 8.0 }, new double[] { 9.8, 10.1, 10.1 } } }) });
            JsonElement input = Record(replicate).GetProperty("inputs")[0];
            JsonElement frames = input.GetProperty("value").GetProperty("frames");
            Check(input.GetProperty("name").GetString() == "data2d" && input.GetProperty("kind").GetString() == "frame2d" && frames.GetArrayLength() == 3 && frames[2][1].GetProperty("values")[2].GetDouble() == 8.0,
                  "the record of the replicated two-way analysis holds its two-level frame: three blocks of three treatments, each the repeats");
            Compare("ReplicateTwoWay", replicate);
            // the nested analysis: Variables[group][subgroup], each the observations (the help's four plants, three leaves, two samples)
            var (nested, _) = Run("TwoWayNested", new() { ["data2d"] = TwoLevel(new[] {
                new[] { new double[] { 3.28, 3.09 }, new double[] { 3.52, 3.48 }, new double[] { 2.88, 2.80 } },
                new[] { new double[] { 2.46, 2.44 }, new double[] { 1.87, 1.92 }, new double[] { 2.19, 2.19 } },
                new[] { new double[] { 2.77, 2.66 }, new double[] { 3.74, 3.44 }, new double[] { 2.55, 2.55 } },
                new[] { new double[] { 3.78, 3.87 }, new double[] { 4.07, 4.12 }, new double[] { 3.31, 3.31 } } }) });
            Compare("TwoWayNested", nested);
            // the grouped covariance: the structure the grid fills, each group's predictor levels with the outcome's replicates at each
            double[][] x = { new double[] { 0.544, 0.845, 1.146 }, new double[] { 0.398, 0.699, 1, 1.301, 1.602 }, new double[] { 0.398, 0.699, 1 } };
            double[][][] y =
            {
                new[] { new double[] { 0, 0, 1, 2.75, 2.75, 1.75, 2.75, 2.25, 2.25, 2.5 }, new double[] { 1.5, 2.5, 5, 6, 4.25, 2.75, 1.5, 3 }, new double[] { 2, 2.5, 5, 4, 5, 4, 2.5, 3.5, 3, 2, 3, 4, 4 } },
                new[] { new double[] { 0, 1, 0, 0, 0, 0.5 }, new double[] { 1, 1.5, 1.5, 1, 1, 0.5 }, new double[] { 1.5, 1, 2, 3.5, 2, 0 }, new double[] { 3, 3, 5.5, 2.5, 1, 2 }, new double[] { 3.5, 3.5, 4.5, 3.5, 3.5, 3 } },
                new[] { new double[] { 2.75, 2, 1.25, 2, 0, 0.5 }, new double[] { 2.5, 2.75, 2.25, 2.25, 3.75 }, new double[] { 3.75, 5.25, 6, 5.5, 2.25, 3.5 } },
            };
            string[] titles = { "Log Dose_Std", "Log Dose_I", "Log Dose_F" };
            var (covariance, _) = Run("GroupedCovariance", new() { ["gcd"] = Covariance(titles, x, y, 0.95), ["mx0-prompted"] = 0.88437179487179485 });
            JsonElement record = Record(covariance);
            JsonElement gcd = record.GetProperty("inputs").EnumerateArray().First(i => i.GetProperty("name").GetString() == "gcd");
            JsonElement groups = gcd.GetProperty("value").GetProperty("frames");
            Check(gcd.GetProperty("kind").GetString() == "covariance" && groups.GetArrayLength() == 3 && groups[1].GetArrayLength() == 6 && groups[1][0].GetProperty("title").GetString() == "Log Dose_I"
                  && groups[1][0].GetProperty("values").GetArrayLength() == 5 && groups[1][5].GetProperty("values")[5].GetDouble() == 3 && record.GetProperty("parameters").GetProperty("ci").GetDouble() == 0.95,
                  "the record of the grouped covariance holds each group's predictor levels then the outcome's replicates at each level, and the confidence level asked through the grid as the setting ci");
            Compare("GroupedCovariance", covariance);
        }
        catch (Exception ex) when (!ex.Message.StartsWith("FAIL", StringComparison.Ordinal))
        {
            string text = ex.ToString().Replace("\r", "").Replace("\n", " | ");
            failures.Add(text.Substring(0, Math.Min(700, text.Length)));
        }
        foreach (string f in failures) Console.WriteLine("      " + f);
        Check(failures.Count == 0, "the replicated two-way and nested analyses of variance and the grouped covariance, run through the engine on the help's examples, give the help's figures from their scripts");
    }

    /// <summary>The grouped covariance's data as the grid fills them: per group the predictor's levels, the outcome's replicates at each, and
    /// the group's own regression line, means and sum of squares of x, which the report prints.</summary>
    private static GroupedCovarianceData Covariance(string[] titles, double[][] x, double[][][] y, double confidence)
    {
        int k = titles.Length, maxr = x.Max(g => g.Length), maxreps = y.Max(g => g.Max(level => level.Length));
        GroupedCovarianceData d = new()
        {
            k = k, maxr = maxr, maxreps = maxreps, GAMMA = confidence, xlab = string.Join(" ", titles) + " ",
            a = new double[k + 1], b = new double[k + 1], bnam = new string[k + 1], cx = new ColumnData[k + 1], nxi = new int[k + 1], ny = new int[k + 1, maxr + 1],
            rssx = new double[k + 1], xmean = new double[k + 1], ymean = new double[k + 1], xt = new double[k + 1, maxr + 1], y = new double[k + 1, maxr + 1, maxreps + 1],
            minMax = new MinMax { MinX = double.MaxValue, MaxX = double.MinValue, MinY = double.MaxValue, MaxY = double.MinValue },
        };
        for (int g = 1; g <= k; g++)
        {
            double[] levels = x[g - 1];
            d.cx[g] = new ColumnData { Title = titles[g - 1], Rows = levels.Length, Sum = levels.Sum() };
            d.bnam[g] = titles[g - 1];
            d.nxi[g] = levels.Length;
            List<(double x, double y)> pairs = new();
            for (int j = 1; j <= levels.Length; j++)
            {
                d.xt[g, j] = levels[j - 1];
                d.ny[g, j] = y[g - 1][j - 1].Length;
                for (int r = 1; r <= d.ny[g, j]; r++) { d.y[g, j, r] = y[g - 1][j - 1][r - 1]; pairs.Add((levels[j - 1], d.y[g, j, r])); }
            }
            d.minMax.MinX = Math.Min(d.minMax.MinX, levels.Min()); d.minMax.MaxX = Math.Max(d.minMax.MaxX, levels.Max());
            d.minMax.MinY = Math.Min(d.minMax.MinY, pairs.Min(p => p.y)); d.minMax.MaxY = Math.Max(d.minMax.MaxY, pairs.Max(p => p.y));
            // the group's line by least squares over its subjects, each replicate with its level's x
            double mx = pairs.Average(p => p.x), my = pairs.Average(p => p.y);
            double sxx = pairs.Sum(p => (p.x - mx) * (p.x - mx)), sxy = pairs.Sum(p => (p.x - mx) * (p.y - my));
            d.xmean[g] = mx; d.ymean[g] = my; d.b[g] = sxy / sxx; d.a[g] = my - d.b[g] * mx; d.rssx[g] = 1 / sxx;
        }
        return d;
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // The Continue in R recipes of 5.3.10. Each case is acquired from worksheet cells and run as the 5.3.9 cases are:
    // through GridSelectionProcessor.FillFrameParameter and the template processor, then the script of the record in R
    // within a time limit, with R's figures compared with the native output bag.
    private static readonly string[] Recipes5310Parts = { "maxwell", "raters", "limits", "exact" };
    private static void CheckRecipes5310(string folder, string only = null)
    {
        if (only != null && !Recipes5310Parts.Contains(only)) throw new ArgumentException("Unknown 5.3.10 recipe check: " + only);
        RRecipes.Folder = Metadata("Recipes");
        Directory.CreateDirectory(folder);
        if (only is null or "maxwell") CheckKappaMaxwell5310(Path.Combine(folder, "maxwell"));
        if (only is null or "raters") CheckKappaRaters5310(Path.Combine(folder, "raters"));
        if (only is null or "limits") CheckKappaLimits5310(Path.Combine(folder, "limits"));
        if (only is null or "exact") CheckKappaExact5310(Path.Combine(folder, "exact"));
        if (Failed539.Count > 0) throw new Exception("FAIL " + Failed539.Count + " of the 5.3.10 recipe cases: " + string.Join(" | ", Failed539));
    }

    // The two raters' gradings of the subjects of a table of counts: the first rater's grade in the rows, the second's in the columns.
    private static (double[] first, double[] second) Pairs5310(double[,] table)
    {
        List<double> first = new(), second = new();
        for (int i = 0; i < table.GetLength(0); i++)
            for (int j = 0; j < table.GetLength(1); j++)
                for (int m = 0; m < table[i, j]; m++) { first.Add(i + 1); second.Add(j + 1); }
        return (first.ToArray(), second.ToArray());
    }

    // Maxwell's and McNemar's tests of the two-rater kappa replays, from the raters' columns and from a table typed in.
    // When a grade is never taken for another the covariance matrix of all grades but the last has no inverse: the replay
    // stopped there ("system is exactly singular") where the report groups the grades that disagreements join.
    private static void CheckKappaMaxwell5310(string folder)
    {
        const string maxwellLine = @"^Marginal homogeneity \(Maxwell\) chi-square = " + Figure539 + @"\s+df = " + Figure539 + @"\s+P [=<] " + Figure539;
        const string mcnemarLine = @"^Symmetry \(generalised McNemar\) chi-square = " + Figure539 + @"\s+df = " + Figure539 + @"\s+P [=<] " + Figure539;
        // the statistic as it was computed before the grouping: right whenever every grade is joined to every other
        const string ungrouped = "local({ d <- (rowSums(counts) - colSums(counts))[-g]; V <- -(counts + t(counts)); diag(V) <- rowSums(counts) + colSums(counts) - 2 * diag(counts)\n" +
            "  stopifnot(identical(x2, as.numeric(t(d) %*% solve(V[-g, -g]) %*% d))) })\n";
        void Compare(string label, OperationRun run, ParameterBag b, double reference, string also)
        {
            bool maxwell = Num539(b, "x2") != Constant.MISSING, mcnemar = b["x2m"].AsObject is not string;
            double x2 = Num539(b, "x2"), df = Num539(b, "df"), p = Num539(b, "pmaxwell");
            double x2m = mcnemar ? Num539(b, "x2m") : Constant.MISSING, dfm = Num539(b, "dfmcnemar"), pm = Num539(b, "pmcnemar");
            if (!double.IsNaN(reference))
                Check(maxwell && Math.Abs(x2 - reference) <= 1e-9 * Math.Max(1, Math.Abs(reference)), label + ": the report's Maxwell chi-square is the sum of its groups' own statistics (" + reference.ToString("R", inv) + ")");
            string assertion = also +
                (maxwell ? Equal539("x2", new[] { x2 }) + Equal539("pchisq(x2, " + df.ToString("R", inv) + ", lower.tail = FALSE)", new[] { p }, 1e-7) : "") +
                (mcnemar ? Equal539("x2m, dfm", new[] { x2m, dfm }) + Equal539("pchisq(x2m, dfm, lower.tail = FALSE)", new[] { pm }, 1e-7) : "stopifnot(dfm == 0)\n");
            string text = Replay539(run, assertion, Path.Combine(folder, label));
            double[] printed = Printed539(text, maxwellLine);
            Check(maxwell ? printed.Length == 3 && Math.Abs(printed[0] - x2) <= 5.1e-7 && printed[1] == df && Math.Abs(printed[2] - Math.Max(p, .0001)) <= 5.1e-5
                : text.Contains("Marginal homogeneity (Maxwell) chi-square not calculated"), label + ": Maxwell's line gives the report's chi-square, degrees of freedom and P");
            printed = Printed539(text, mcnemarLine);
            Check(mcnemar ? printed.Length == 3 && Math.Abs(printed[0] - x2m) <= 5.1e-7 && printed[1] == dfm && Math.Abs(printed[2] - Math.Max(pm, .0001)) <= 5.1e-5
                : text.Contains("Symmetry (generalised McNemar) chi-square = [not calculated - zero cells]"), label + ": McNemar's line gives the report's chi-square, degrees of freedom and P");
        }
        void Columns(string label, double[] first, double[] second, double reference = double.NaN, string also = "") => Case539("maxwell " + label, () =>
        {
            var cells = new RecordCells(Cells539("First", first), Cells539("Second", second));
            var (run, output) = Run("Kappa", new() { ["responses"] = Select539(cells, first.Length + 1, 0, 1), ["ci"] = .95, ["method"] = "1" });
            Compare(label, run, output.ParameterBag, reference, also);
        });
        void Typed(string label, double[,] table, double reference = double.NaN, string also = "") => Case539("maxwell " + label, () =>
        {
            int g = table.GetLength(0);
            var (run, output) = Run("KappaScreen", new() { ["method"] = "1", ["doExactP"] = false, ["iterations"] = 1000, ["seed"] = 1, ["ci"] = .95,
                ["responsesCrosstab"] = SheetFrame(Workbook, "Recipes 5.3.10", 0, 0, Enumerable.Range(0, g).Select(j => ("Column " + (j + 1), Enumerable.Range(0, g).Select(i => table[i, j]).ToArray())).ToArray()) });
            Compare(label, run, output.ParameterBag, reference, also);
        });

        // the case of the review: grades 1 and 2 are taken for each other, grade 3 never for another
        double[] reviewFirst = { 1, 2, 3, 2, 1, 3 }, reviewSecond = { 2, 2, 3, 1, 1, 3 };
        Columns("never-confused", reviewFirst, reviewSecond, reference: 0);
        Typed("never-confused-typed", new double[,] { { 1, 1, 0 }, { 1, 1, 0 }, { 0, 0, 2 } }, reference: 0);
        // no grade taken for another: nothing to test, which both lines say, and the script still completes
        Columns("never-disagree", new double[] { 1, 2, 3, 1, 2, 3, 2 }, new double[] { 1, 2, 3, 1, 2, 3, 2 });

        // Two groups with marginal differences. The reference does not use the matrix: a group of two grades has the
        // McNemar statistic (b - c)^2 / (b + c), and a group of three has the closed form of Fleiss (Statistical Methods
        // for Rates and Proportions, 1981, section 8.2): [m23 d1^2 + m13 d2^2 + m12 d3^2] / [2 (m12 m13 + m12 m23 + m13 m23)],
        // where m is the mean of a pair of cells across the diagonal and d the row total less the column total.
        double[,] groups =
        {
            { 4, 5, 0, 0, 0 },
            { 1, 3, 0, 0, 0 },
            { 0, 0, 2, 4, 2 },
            { 0, 0, 1, 3, 3 },
            { 0, 0, 1, 0, 2 },
        };
        double pair = Math.Pow(groups[0, 1] - groups[1, 0], 2) / (groups[0, 1] + groups[1, 0]);
        double m12 = (groups[2, 3] + groups[3, 2]) / 2, m13 = (groups[2, 4] + groups[4, 2]) / 2, m23 = (groups[3, 4] + groups[4, 3]) / 2;
        double d1 = groups[2, 3] + groups[2, 4] - groups[3, 2] - groups[4, 2], d2 = groups[3, 2] + groups[3, 4] - groups[2, 3] - groups[4, 3], d3 = groups[4, 2] + groups[4, 3] - groups[2, 4] - groups[3, 4];
        double triple = (m23 * d1 * d1 + m13 * d2 * d2 + m12 * d3 * d3) / (2 * (m12 * m13 + m12 * m23 + m13 * m23));
        (double[] first, double[] second) = Pairs5310(groups);
        Columns("two-groups", first, second, reference: pair + triple);
        Typed("two-groups-typed", groups, reference: pair + triple);

        // every grade joined to every other: one group, and the figure is the one the replay gave before, to the last bit
        double[,] joined =
        {
            { 9, 3, 1, 0 },
            { 2, 8, 4, 1 },
            { 0, 5, 7, 2 },
            { 1, 0, 3, 6 },
        };
        (first, second) = Pairs5310(joined);
        Columns("one-group", first, second, also: ungrouped);
        Typed("one-group-typed", joined, also: ungrouped);
    }
}

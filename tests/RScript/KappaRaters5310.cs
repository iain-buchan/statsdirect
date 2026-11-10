using System.Text.Json;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // Kappa of more than two raters. Its record took the two raters' recipe, which read the first two columns and printed
    // their kappa, weighted kappa and tests: another analysis. It has the recipe of its own analysis now: Fleiss and
    // Cuzick's kappa for two categories, and for more the kappa of each category and of all together with the test of
    // Fleiss, Nee and Landis. Kappa cannot be more than 1, so the report and the replay stop the upper limit there.
    private static void CheckKappaRaters5310(string folder)
    {
        const string interval = "^" + Figure539 + "% confidence interval = " + Figure539 + " to " + Figure539;
        // The kappa of a category as Fleiss and Cuzick (1979) print it: 1 - [sum of x (m - x) / m] / [n (mean m - 1) p q],
        // where a subject has m ratings and x of them in the category. Neither the recipe nor the report computes it so.
        const string published = "published <- function(category) { m <- rowSums(!is.na(ratings)); x <- rowSums(!is.na(ratings) & ratings == category)[m > 0]; m <- m[m > 0]\n" +
            "  p <- sum(x) / sum(m); 1 - sum(x * (m - x) / m) / (length(m) * (mean(m) - 1) * p * (1 - p)) }\n";
        // Fleiss's (1971) kappa of subjects all rated the same number of times, from the pairs of a subject's ratings that agree.
        const string fleiss = "local({ counts <- sapply(categories, function(category) rowSums(ratings == category)); m <- ncol(ratings); N <- nrow(ratings)\n" +
            "  agree <- sum(counts * (counts - 1)) / (N * m * (m - 1)); chance <- sum((colSums(counts) / (N * m))^2)\n" +
            "  stopifnot(isTRUE(all.equal(kappa, (agree - chance) / (1 - chance), tolerance = 1e-12))) })\n";
        void Raters(string label, double confidence, bool stopped, params object[][] ratings) => Case539("raters " + label, () =>
        {
            int subjects = ratings.Max(r => r.Length);
            var cells = new RecordCells(ratings.Select((r, j) => new object[] { "Rater " + (j + 1) }.Concat(r).ToArray()).ToArray());
            var (run, output) = Run("Kappa", new() { ["responses"] = Select539(cells, subjects + 1, Enumerable.Range(0, ratings.Length).ToArray()), ["ci"] = confidence });
            ParameterBag b = output.ParameterBag;
            Check(RRecipes.Find(Record(run))?.File == "kappa-raters.R", label + ": the record of more than two raters has the recipe of their analysis, not the two raters'");
            bool two = b["categories"].AsInt32 == 2, constant = Num539(b, "ll") != Constant.MISSING;
            double kappa = Num539(b, two ? "k" : "kc"), lower = Num539(b, "ll"), upper = Num539(b, "ul");
            string limit = stopped ? "stopifnot(kappa + z_ci * se > 1, upper == 1)\n" : "stopifnot(upper == kappa + z_ci * se)\n";
            string assertion = published;
            if (two)
                assertion += "stopifnot(length(categories) == 2, isTRUE(all.equal(kappa, published(categories[2]), tolerance = 1e-12)))\n" +
                    Equal539("kappa, se, lower, upper, kappa / se", new[] { kappa, Num539(b, "se"), lower, upper, Num539(b, "z") }) +
                    "stopifnot(abs(pnorm(kappa / se, lower.tail = FALSE) - " + Num539(b, "p").ToString("R", inv) + ") < 1e-7)\n" + limit;
            else
            {
                IList<ParameterBag> rows = Rows539(b, "*cats");
                assertion += "stopifnot(identical(categories, c(" + string.Join(", ", rows.Select(r => "\"" + r["resp"].AsString + "\"")) + ")))\n" +
                    "stopifnot(isTRUE(all.equal(unname(kappa_j), unname(vapply(categories, published, 0)), tolerance = 1e-12)))\n" +
                    Equal539("kappa_j", rows.Select(r => Num539(r, "k"))) + Equal539("kappa", new[] { kappa });
                if (constant)
                    assertion += fleiss + Equal539("rep(se_j, length(categories))", rows.Select(r => Num539(r, "se"))) + Equal539("kappa_j / se_j", rows.Select(r => Num539(r, "z"))) +
                        Equal539("lower, upper, kappa / se", new[] { lower, upper, Num539(b, "zc") }) +
                        "stopifnot(constant, abs(pnorm(kappa / se, lower.tail = FALSE) - " + Num539(b, "p").ToString("R", inv) + ") < 1e-7)\n" + limit;
                else assertion += "stopifnot(!constant)\n";
            }
            string text = Replay539(run, assertion, Path.Combine(folder, label));
            Check(text.Contains("General agreement over all (" + b["categories"].AsInt32 + ") categories with " + b["r"].AsString + " raters per subject", StringComparison.Ordinal),
                label + ": the replay's heading gives the report's numbers of categories and of raters per subject (" + b["r"].AsString + ")");
            double[] printed = Printed539(text, interval);
            if (two || constant)
                Check(printed.Length == 3 && printed[0] == 100 * confidence && Math.Abs(printed[1] - lower) <= 5.1e-7 && (stopped ? printed[2] == 1 && upper == 1 : upper < 1 && Math.Abs(printed[2] - upper) <= 5.1e-7),
                    label + ": the interval line gives the report's limits" + (stopped ? ", the upper one stopped at 1 by both" : ""));
            else
                Check(printed.Length == 0 && text.Contains("not constant", StringComparison.Ordinal) && b["p"].AsObject is string said && said.Contains("not constant", StringComparison.Ordinal) && upper == Constant.MISSING,
                    label + ": with numbers of ratings that vary neither the report nor the replay gives a test or an interval");
        });
        object[] Column(params object[] values) => values;
        const string y = "yes", n = "no", a = "absent", m = "mild", s = "severe";

        // two categories, every subject rated by all three: grades as numbers
        Raters("two-categories", .95, false,
            Column(1d, 1d, 2d, 2d, 1d, 2d, 1d, 1d, 2d, 1d, 2d, 2d, 1d, 2d),
            Column(1d, 2d, 2d, 2d, 1d, 1d, 1d, 2d, 2d, 1d, 2d, 1d, 1d, 2d),
            Column(1d, 1d, 2d, 1d, 1d, 2d, 2d, 1d, 2d, 1d, 2d, 2d, 2d, 2d));
        // two categories as words, with cells left empty: three or four ratings of a subject, and a subject whom nobody rated
        Raters("two-categories-gaps", .9, false,
            Column(y, n, y, y, null, n, y, null, n, y, y, n, n, y, n, y),
            Column(y, n, n, y, null, n, y, n, n, null, y, n, y, y, n, y),
            Column(y, y, y, null, null, n, y, n, n, y, n, n, n, y, null, y),
            Column(n, n, y, y, null, null, y, n, y, y, y, n, n, null, n, y));
        // three categories and four raters: numbers whose order as text, which the report uses, is not their order as numbers
        Raters("three-categories", .99, false,
            Column(2d, 10d, 30d, 2d, 10d, 30d, 2d, 30d, 10d, 2d, 30d, 10d, 2d, 30d, 10d, 2d),
            Column(2d, 10d, 30d, 2d, 10d, 30d, 10d, 30d, 10d, 2d, 2d, 10d, 2d, 30d, 30d, 2d),
            Column(2d, 10d, 30d, 10d, 10d, 30d, 2d, 30d, 2d, 2d, 30d, 10d, 30d, 30d, 10d, 2d),
            Column(2d, 2d, 30d, 2d, 10d, 10d, 2d, 30d, 10d, 2d, 30d, 30d, 2d, 30d, 10d, 10d));
        // three categories with cells left empty: the kappas, and no test or interval
        Raters("three-categories-gaps", .95, false,
            Column(a, m, s, a, m, s, a, s, m, a, null, m),
            Column(a, m, s, a, m, s, m, s, m, null, s, m),
            Column(a, m, s, m, null, s, a, s, a, a, s, s),
            Column(a, a, s, a, m, m, a, null, m, a, s, m));
        // nearly complete agreement in few subjects: by the formula the upper limit is more than 1
        Raters("stopped-two-categories", .95, true,
            Column(1d, 1d, 2d, 2d, 1d, 2d, 1d, 1d),
            Column(1d, 1d, 2d, 2d, 1d, 2d, 1d, 2d),
            Column(1d, 1d, 2d, 2d, 1d, 2d, 1d, 1d));
        Raters("stopped-three-categories", .99, true,
            Column(1d, 2d, 3d, 1d, 2d, 3d, 1d, 3d),
            Column(1d, 2d, 3d, 1d, 2d, 3d, 2d, 3d),
            Column(1d, 2d, 3d, 1d, 2d, 3d, 1d, 3d),
            Column(1d, 2d, 3d, 2d, 2d, 3d, 1d, 3d));
    }

    // The limits of the two raters' kappa and weighted kappa. The report stops them at -1 and 1; the replay printed what
    // the formula gave, which few subjects can carry past either end.
    private static void CheckKappaLimits5310(string folder)
    {
        const string plain = "^" + Figure539 + "% confidence interval = " + Figure539 + " to " + Figure539;
        const string weighted = "^" + Figure539 + "% confidence interval for kappa = " + Figure539 + " to " + Figure539;
        void Compare(string label, OperationRun run, ParameterBag b, bool above)
        {
            double from = Num539(b, "from"), to = Num539(b, "to"), fromw = Num539(b, "fromw"), tow = Num539(b, "tow");
            Check(above ? to == 1 && tow == 1 && from > -1 && fromw > -1 : from == -1 && fromw == -1 && to < 1 && tow < 1,
                label + ": the report stops the " + (above ? "upper limits of kappa and weighted kappa at 1" : "lower limits of kappa and weighted kappa at -1"));
            string text = Replay539(run, (above ? "stopifnot(kappa + z * se > 1, kappaw + z * sew > 1)\n" : "stopifnot(kappa - z * se < -1, kappaw - z * sew < -1)\n") +
                Equal539("kappa, kappaw", new[] { Num539(b, "kappa"), Num539(b, "kappaw") }) +
                Equal539("max(-1, kappa - z * se), min(1, kappa + z * se), max(-1, kappaw - z * sew), min(1, kappaw + z * sew)", new[] { from, to, fromw, tow }), Path.Combine(folder, label));
            double[] printed = Printed539(text, plain), printedw = Printed539(text, weighted);
            Check(printed.Length == 3 && printedw.Length == 3 && Math.Abs(printed[1] - from) <= 5.1e-7 && Math.Abs(printed[2] - to) <= 5.1e-7 && Math.Abs(printedw[1] - fromw) <= 5.1e-7 && Math.Abs(printedw[2] - tow) <= 5.1e-7 &&
                (above ? printed[2] == 1 && printedw[2] == 1 : printed[1] == -1 && printedw[1] == -1), label + ": the replay's interval lines give the report's limits, stopped as the report stops them");
        }
        void Both(string label, double[,] table, bool above)
        {
            Case539("limits " + label, () =>
            {
                (double[] first, double[] second) = Pairs5310(table);
                var cells = new RecordCells(Cells539("First", first), Cells539("Second", second));
                var (run, output) = Run("Kappa", new() { ["responses"] = Select539(cells, first.Length + 1, 0, 1), ["ci"] = .95, ["method"] = "1" });
                Check(RRecipes.Find(Record(run))?.File == "kappa.R", label + ": the record of two raters keeps the two raters' recipe");
                Compare(label, run, output.ParameterBag, above);
            });
            Case539("limits " + label + "-typed", () =>
            {
                int g = table.GetLength(0);
                var (run, output) = Run("KappaScreen", new() { ["method"] = "1", ["doExactP"] = false, ["iterations"] = 1000, ["seed"] = 1, ["ci"] = .95,
                    ["responsesCrosstab"] = SheetFrame(Workbook, "Recipes 5.3.10", 0, 0, Enumerable.Range(0, g).Select(j => ("Column " + (j + 1), Enumerable.Range(0, g).Select(i => table[i, j]).ToArray())).ToArray()) });
                Compare(label + "-typed", run, output.ParameterBag, above);
            });
        }
        // every grade is taken for another here, so that Maxwell's test has one group and only the limits are in question
        Both("upper", new double[,] { { 3, 1, 0 }, { 0, 3, 1 }, { 0, 0, 3 } }, above: true);
        Both("lower", new double[,] { { 0, 3, 0 }, { 3, 0, 1 }, { 0, 0, 0 } }, above: false);
    }

    // The simulated P of kappa continues two analyses: the raters' columns and the table typed in. Its record took the
    // columns' recipe in both cases, which after a typed table has no data to work on.
    private static void CheckKappaExact5310(string folder)
    {
        double[,] table = { { 9, 3, 1, 0 }, { 2, 8, 4, 1 }, { 0, 5, 7, 2 }, { 1, 0, 3, 6 } };
        void After(string label, string root, string file, Func<(OperationRun run, StepOutput output)> parent) => Case539("exact " + label, () =>
        {
            ParameterBag first = parent().output.ParameterBag;
            double kappa = Num539(first, "kappa"), kappaw = Num539(first, "kappaw");
            var (run, _) = Run("KappaSimulateExactP", new() { ["iterations"] = 1000, ["seed"] = 1, ["ci"] = .99 }, first);
            JsonElement record = Record(run);
            Check(record.GetProperty("rootOperation").GetString() == root && RRecipes.Find(record)?.File == file, label + ": the simulated P after " + root + " is replayed by " + file);
            Check(RRecipes.Find(Legacy539(run))?.File == file, label + ": saved without its root analysis (before 5.2.3) it is still replayed by " + file + ", from the frame it kept");
            Replay539(run, Equal539("kappa, kappaw", new[] { kappa, kappaw }) + "stopifnot(sum(counts) == " + table.Cast<double>().Sum().ToString("R", inv) + ")\n", Path.Combine(folder, label));
        });
        After("after-columns", "Kappa", "kappa.R", () =>
        {
            (double[] first, double[] second) = Pairs5310(table);
            var cells = new RecordCells(Cells539("First", first), Cells539("Second", second));
            return Run("Kappa", new() { ["responses"] = Select539(cells, first.Length + 1, 0, 1), ["ci"] = .95, ["method"] = "1" });
        });
        After("after-typed-table", "KappaScreen", "kappa-screen.R", () => Run("KappaScreen", new() { ["method"] = "1", ["doExactP"] = false, ["iterations"] = 1000, ["seed"] = 1, ["ci"] = .95,
            ["responsesCrosstab"] = SheetFrame(Workbook, "Recipes 5.3.10", 0, 0, Enumerable.Range(0, 4).Select(j => ("Column " + (j + 1), Enumerable.Range(0, 4).Select(i => table[i, j]).ToArray())).ToArray()) }));
    }
}

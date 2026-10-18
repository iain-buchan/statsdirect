using StatsDirect.Templates;

internal static partial class Program
{
    // A dose group in which every subject responds, at a dose far above the others, is fitted as all-or-none: it has no weight in the
    // fit and no degree of freedom, and adds nothing to the heterogeneity chi-square. The analysis of the other groups must stand, with
    // the median dose and finite limits. (A group that falls short of everyone at such a dose is another matter: the model flattens
    // its slope to meet it, and may then honestly refuse the limits as insignificant.)
    private static void CheckProbitExtremeGroup()
    {
        foreach (string operation in new[] { "Probit", "Logit" })
        {
            var (_, output) = Run(operation, new()
            {
                ["dose"] = SheetFrame(Workbook, "Extreme", 0, 0, ("dose", new double[] { 1, 2, 3, 4, 5, 60 })),
                ["subjects"] = SheetFrame(Workbook, "Extreme", 1, 0, ("subjects", new double[] { 20, 20, 20, 20, 20, 20 })),
                ["responders"] = SheetFrame(Workbook, "Extreme", 2, 0, ("responders", new double[] { 2, 6, 10, 14, 18, 20 })),
                ["conf"] = 0.95, ["calc-log10-doses"] = false, ["nc"] = 0, ["nrc"] = 0, ["qld"] = 90.0
            });
            double median = DoseResponseNumber(output.ParameterBag, "mxd"), from = DoseResponseNumber(output.ParameterBag, "from"), to = DoseResponseNumber(output.ParameterBag, "to");
            Console.WriteLine($"      {operation} with every subject responding at dose 60: median dose {median:G6}, limits {from:G6} to {to:G6}");
            Check(double.IsFinite(median) && double.IsFinite(from) && double.IsFinite(to) && from < median && median < to && median > 2 && median < 4,
                  operation + " with a group in which every subject responds at a far dose still gives the median dose of the others with finite limits");
        }
    }
}

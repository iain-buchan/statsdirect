using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    // The method comparison regression's charts are drawn by R's svg device at the sizes the definition asks for: the test against
    // reference plot square, so that the line of equality runs at 45 degrees; the difference plot and the others wider
    private static void CheckMethodComparisonCharts()
    {
        double[] reference = Enumerable.Range(1, 30).Select(i => 10.0 + 3 * i).ToArray();
        double[] test = reference.Select((v, i) => 1.05 * v + 2 + ((i * 7) % 5 - 2) * 0.8).ToArray();   // a small, fixed scatter about a line
        DataFrame testData = new();
        testData.Variables.Add(Column("Test", test));
        DataFrame referenceData = new();
        referenceData.Variables.Add(Column("Reference", reference));
        var (_, output) = Run("MethodComparisonRegression", new()
        {
            ["testData"] = testData, ["referenceData"] = referenceData,
            ["methods"] = new Dictionary<string, bool> { ["do.deming.jackknife"] = true },
            ["ci"] = 0.95, ["error-ratio"] = 1.0, ["seed"] = 12345,
            ["desired-charts"] = new Dictionary<string, bool> { ["plot.testvsreference"] = true, ["plot.bland.altman"] = true },
            ["saveRScript"] = false
        });
        // R returns the methods' results as an array (named with a star), and each chart as an array of one block holding the picture
        Check(output.ParameterBag.ContainsKey("*methodResults") && output.ParameterBag["*methodResults"].AsParameterBagList.Count == 1, "the run gives the results of the one regression method asked for");
        ParameterBag deming = output.ParameterBag["*methodResults"].AsParameterBagList[0];
        ReportVectorPicture Chart(string name) => deming.ContainsKey("*" + name) ? deming["*" + name].AsParameterBagList[0]["chart"].AsObject as ReportVectorPicture : null;
        ReportVectorPicture scatter = Chart("testvsreference"), difference = Chart("difference");
        Check(scatter != null && difference != null, "the test against reference plot and the difference plot come back as SVG pictures");
        Console.WriteLine($"      test against reference {scatter.Width} by {scatter.Height} pixels; difference {difference.Width} by {difference.Height}");
        Check(scatter.Width > 0 && scatter.Width == scatter.Height, "the test against reference plot is square");
        Check(difference.Width > difference.Height, "the difference plot is wider than it is high");
    }
}

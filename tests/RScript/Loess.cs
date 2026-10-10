using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    // The LOESS chart is drawn by R's svg device at the size the definition asks for, and the report shows it at that size: the chart
    // is to be square (it was 6 by 4 inches)
    private static void CheckLoessChart()
    {
        double[] x = Enumerable.Range(1, 24).Select(i => (double)i).ToArray();
        double[] y = x.Select(v => 10 * Math.Sin(v / 4) + v + (v % 3) - 1).ToArray();   // a smooth curve with a small, fixed wobble
        DataFrame outcome = new();
        outcome.Variables.Add(Column("y", y));
        DataFrame predictors = new();
        predictors.Variables.Add(Column("x", x));
        var (_, output) = Run("LOESS", new()
        {
            ["outcome"] = outcome, ["predictors"] = predictors, ["model"] = "y~x", ["title"] = "y vs. x", ["ylab"] = "y", ["xlab"] = "x",
            ["span"] = 0.75, ["degree"] = "2", ["plotFits"] = false, ["plotFitsAndCi"] = true, ["ci"] = 0.95, ["saveFitsAndSe"] = false, ["saveRScript"] = false
        });
        Check(output.ParameterBag.ContainsKey("metafile1"), "a run of LOESS with one predictor gives its chart");
        ReportVectorPicture chart = output.ParameterBag["metafile1"].AsObject as ReportVectorPicture;
        Check(chart != null, "the chart is an SVG picture, as R's svg device draws it");
        Console.WriteLine($"      the LOESS chart is {chart.Width} by {chart.Height} pixels");
        Check(chart.Width > 0 && chart.Width == chart.Height, "the LOESS chart is square");
    }
}

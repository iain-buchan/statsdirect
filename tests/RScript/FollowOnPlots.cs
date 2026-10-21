using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // A chart follow-on of a regression (the residual plots, the regression line, its standard error and confidence bands, its prediction
    // interval) continues its parent's run and draws its plots after the parent's analysis: its script is the parent's script followed by
    // the plots section the manifest names, and run in R with an SVG device it draws the plots the program draws, one page each.
    private static void CheckFollowOnPlots()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "follow-on-plots");
        double[] x1 = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, x2 = { 3, 1, 4, 1, 5, 9, 2, 6, 5, 3, 5, 8 }, y = { 2.9, 5.2, 7.4, 8.1, 11.3, 14.2, 13.8, 17.1, 18.6, 19.9, 23.2, 25.1 };
        DataFrame outcome = SheetFrame("test.xlsx", "Plots", 3, 1, ("y", y));
        DataFrame predictors = SheetFrame("test.xlsx", "Plots", 1, 1, ("x1", x1), ("(log) x2", x2));   // a title that starts as the fit's weights column does
        var (multiple, multipleOutput) = Run("MultipleLinearRegression", new() { ["ci"] = 0.95, ["calculateIntercept"] = true, ["useWeights"] = false, ["outcome"] = outcome, ["predictors"] = predictors });
        // the residuals against the fitted values, against each of the two predictors, and the normal plot
        Plots("PlotMultipleLinearRegressionResiduals", multiple, multipleOutput, new(), "multiple-linear-regression-residual-plots.R", 4, folder);
        DataFrame yFrame = SheetFrame("test.xlsx", "Plots", 3, 1, ("y", y)), xFrame = SheetFrame("test.xlsx", "Plots", 1, 1, ("x1", x1));
        var (simple, simpleOutput) = Run("SimpleLinearRegression", new() { ["gamma"] = 0.95, ["y"] = yFrame, ["x"] = xFrame });
        Plots("PlotResidualsSimple", simple, simpleOutput, new(), "simple-linear-regression-residual-plots.R", 3, folder);
        Plots("PlotSimpleLinearRegression", simple, simpleOutput, new(), "simple-linear-regression-plot.R", 1, folder);
        string seCi = Plots("PlotSeCi", simple, simpleOutput, new() { ["reggamma"] = 0.9 }, "simple-linear-regression-se-ci-plot.R", 1, folder);
        Check(seCi.Contains("\nlevel <- 0.9\n", StringComparison.Ordinal), "the standard error and confidence bands plot takes the level the follow-on was given, 90%, as its own setting");
        Plots("PlotPredictionInterval", simple, simpleOutput, new(), "simple-linear-regression-prediction-plot.R", 1, folder);
    }

    /// <summary>Runs the follow-on on its parent's run, checks its script against the parent's and the plots section, runs it in R and
    /// counts the pages drawn; the script's text.</summary>
    private static string Plots(string name, OperationRun parent, StepOutput parentOutput, Dictionary<string, object> answers, string section, int expected, string folder)
    {
        var (followOn, _) = Run(name, answers, parentOutput.ParameterBag);
        RScript parentScript = RScriptWriter.Write(Record(parent), null, null), script = RScriptWriter.Write(Record(followOn), null, null);
        string[] Body(RScript s) => s.Text.Split('\n').Skip(1).ToArray();   // all but the title line
        string[] a = Body(parentScript), b = Body(script);
        string[] missing = a.Where(line => line != "# " + parentScript.Detail && !b.Contains(line)).ToArray();   // the header's detail line names the follow-on's plots
        if (missing.Length > 0) Console.WriteLine($"      {name}: the parent's line \"{missing[0]}\" is not in the follow-on's script");
        Check(script.HasRecipe && missing.Length == 0, name + "'s script carries the parent's script, the same recipe on the same data");
        Check(script.Detail.StartsWith(parentScript.Detail.TrimEnd('.') + ", then ", StringComparison.Ordinal) && b.Contains("# " + script.Detail), name + "'s header says the parent's analysis is followed by its plots");
        string plots = RRecipes.Recipe(section);
        string body = string.Join("\n", plots.Split('\n').SkipWhile(line => line.Length == 0   // the header only: the section's opening comment is part of what the script must end with
            || Regex.IsMatch(line, "^#\\s*(data|table|matrix|nested|settings|recipe):", RegexOptions.IgnoreCase) || Regex.IsMatch(line, "^# (StatsDirect recipe|From the help|hand-edited)", RegexOptions.IgnoreCase))).TrimEnd();
        Check(body.Length > 0 && b.Length > a.Length && script.Text.TrimEnd().EndsWith(body, StringComparison.Ordinal), name + "'s script ends with the plots section " + section);
        string run = Path.Combine(folder, name);
        if (Directory.Exists(run)) foreach (string old in Directory.GetFiles(run, "plot-*.svg")) File.Delete(old);
        RunR("options(device = function(...) grDevices::svg(filename = \"plot-%03d.svg\", ...))\n" + script.Text, run);
        int drawn = Directory.GetFiles(run, "plot-*.svg").Length;
        Check(drawn == expected, $"{name}'s script draws {expected} plot(s) in R, one page each ({drawn} drawn)");
        return script.Text;
    }
}

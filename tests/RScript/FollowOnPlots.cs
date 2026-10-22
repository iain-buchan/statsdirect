using System.Text.Json;
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
        // the four chart follow-ons that drew nothing until 22 October 2026, each on its parent's run of the help's example: the Cox
        // regression's survival and hazard plots split by its one binary predictor, which adds the log-log plot; the logistic
        // regression's eight diagnostic plots; the linearized estimates' selected model through the data; the polynomial's curve
        // alone and with its confidence and prediction bands, at the level the follow-on is given
        var coxAnswers = ExampleAnswers("CoxRegression.json", new() { ["accuracy"] = 0.000000001, ["centre-continuous-covariates"] = true, ["useTimesAdjustment"] = false, ["strata"] = null });
        var (cox, coxOutput) = Run("CoxRegression", coxAnswers);
        Plots("CoxHazardPlots", cox, coxOutput, new() { ["use-tics"] = true, ["use-markers"] = true, ["group"] = new[] { true, false } }, "cox-regression-plots.R", 3, folder);
        var logisticAnswers = ExampleAnswers("LogisticRegression.json", new() { ["grouping"] = "grouped", ["accuracy"] = "0.0000001", ["intercept"] = true, ["weights"] = false, ["gamma"] = 0.95 });
        var (logistic, logisticOutput) = Run("LogisticRegression", logisticAnswers);
        Plots("LogisticRegressionDiagnostics", logistic, logisticOutput, new(), "logistic-regression-diagnostic-plots.R", 8, folder);
        var linearizedAnswers = ExampleAnswers("LinearizedEstimatePlot.json", new() { ["model"] = "2" });
        var (linearized, linearizedOutput) = Run("LinearizedEstimates", linearizedAnswers);
        Plots("LinearizedEstimatePlot", linearized, linearizedOutput, new() { ["y"] = linearizedAnswers["y"], ["x"] = linearizedAnswers["x"] }, "linearized-estimate-plot.R", 1, folder);
        var polynomialAnswers = ExampleAnswers("PolynomialRegression.json", new() { ["degree"] = "2" });
        var (polynomial, polynomialOutput) = Run("PolynomialRegression", polynomialAnswers);
        string bands = Plots("PolynomialRegressionPlot", polynomial, polynomialOutput, new() { ["y"] = polynomialAnswers["y"], ["x"] = polynomialAnswers["x"], ["gamma"] = 0.9 }, "polynomial-regression-plots.R", 3, folder);
        Check(bands.Contains("\nlevel <- 0.9\n", StringComparison.Ordinal), "the polynomial's bands take the level the follow-on was given, 90%, as its own setting");
    }
    /// <summary>The answers a run of the help's example takes: its frames from the example file, with the settings given.</summary>
    private static Dictionary<string, object> ExampleAnswers(string example, Dictionary<string, object> settings)
    {
        JsonElement root = JsonDocument.Parse(File.ReadAllText(Path.Combine(Metadata("Examples"), example))).RootElement;
        Dictionary<string, object> answers = new(settings);
        foreach (JsonElement input in root.GetProperty("inputs").EnumerateArray())
        {
            DataFrame f = new();
            foreach (JsonElement c in input.GetProperty("columns").EnumerateArray())
                f.Variables.Add(Column(c.GetProperty("title").GetString(), c.GetProperty("values").EnumerateArray().Select(v => v.GetDouble()).ToArray()));
            string name = input.GetProperty("name").GetString();
            if (f.Variables.Count > 0 || !answers.ContainsKey(name)) answers[name] = f.Variables.Count > 0 ? f : null;   // an input the example leaves empty is skipped
        }
        return answers;
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

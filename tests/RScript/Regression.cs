using System.Text.Json;
using System.Text.Json.Nodes;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // The multiple linear regression of the Longley data, whose columns are titled "x1 (Longley)" and so on, and a follow-on after it, as
    // run on 11 Oct 2026: the script failed in R, which read "x1 (Longley)" as a call of a function x1; the recipe was written for the
    // help's two predictors; the data line came twice, once for each frame from the one sheet; and a grid the follow-on inherited from an
    // earlier follow-on (the prediction's new values) was written as stray vectors.
    private static void CheckRegressionFollowOn()
    {
        RRecipes.Folder = Metadata("Recipes");
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "longley");
        double[] y = { 60323, 61122, 60171, 61187, 63221, 63639, 64989, 63761, 66019, 67857, 68169, 66513, 68655, 69564, 69331, 70551 };
        double[][] x =
        {
            new double[] { 83, 88.5, 88.2, 89.5, 96.2, 98.1, 99, 100, 101.2, 104.6, 108.4, 110.8, 112.6, 114.2, 115.7, 116.9 },
            new double[] { 234289, 259426, 258054, 284599, 328975, 346999, 365385, 363112, 397469, 419180, 442769, 444546, 482704, 502601, 518173, 554894 },
            new double[] { 2356, 2325, 3682, 3351, 2099, 1932, 1870, 3578, 2904, 2822, 2936, 4681, 3813, 3931, 4806, 4007 },
            new double[] { 1590, 1456, 1616, 1650, 3099, 3594, 3547, 3350, 3048, 2857, 2798, 2637, 2552, 2514, 2572, 2827 },
            new double[] { 107608, 108632, 109773, 110929, 112075, 113270, 115094, 116219, 117388, 118734, 120445, 121950, 123366, 125368, 127852, 130081 },
            new double[] { 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960, 1961, 1962 },
        };
        DataFrame outcome = SheetFrame("test.xlsx", "Regression", 1, 1, ("y (Longley)", y));
        DataFrame predictors = SheetFrame("test.xlsx", "Regression", 2, 1, Enumerable.Range(0, 6).Select(i => ("x" + (i + 1) + " (Longley)", x[i])).ToArray());
        var (parent, parentOutput) = Run("MultipleLinearRegression", new() { ["ci"] = 0.95, ["calculateIntercept"] = true, ["useWeights"] = false, ["outcome"] = outcome, ["predictors"] = predictors });
        JsonElement parentRecord = Record(parent);
        RScript script = RScriptWriter.Write(parentRecord, null, null);
        Check(script.HasRecipe && script.Text.Split('\n').Count(l => l.StartsWith("# Data:", StringComparison.Ordinal)) == 1, "the data line of the two frames from the one sheet is written once");
        string output = RunR(script.Text, folder);
        if (output.Contains("Error in ")) Console.WriteLine("      R: " + output.Substring(output.IndexOf("Error in ")).Split('\n')[0]);
        // NIST certifies b1 of the Longley data as 15.0618722713733; the total has the 15 degrees of freedom of 16 observations
        Check(output.Contains("b6 = ") && output.Contains("b1 = 15.061872"), "the regression script runs in R on six predictors titled \"x1 (Longley)\" and so on, and gives NIST's certified b1 to six places");
        Check(output.Contains("Total (corrected) 185008826 15 "), "the sums of squares are those of the run's six predictors: the total line has 15 degrees of freedom");
        // a follow-on continues the run: its script is the regression's
        var (followOn, _) = Run("MultipleLinearRegressionResiduals", new() { ["options"] = new Dictionary<string, bool>() }, parentOutput.ParameterBag);   // nothing saved to the worksheet
        RScript followOnScript = RScriptWriter.Write(Record(followOn), null, null);
        string[] a = script.Text.Split('\n').Skip(1).ToArray(), b = followOnScript.Text.Split('\n').Skip(1).ToArray();
        int differs = Enumerable.Range(0, Math.Max(a.Length, b.Length)).FirstOrDefault(i => i >= a.Length || i >= b.Length || a[i] != b[i], -1);
        if (differs >= 0) Console.WriteLine($"      the scripts differ at line {differs + 2}: regression \"{(differs < a.Length ? a[differs] : "(end)")}\", follow-on \"{(differs < b.Length ? b[differs] : "(end)")}\"");
        Check(differs < 0, "the residuals follow-on's script is the regression's script");
        // the frames in the bag that no variable of the recipe takes are left out of the script: the regression leaves the source of the
        // prediction's grid (Name, Value, Old value) and the predictors' names there, which the record lists as inherited frames, and an
        // earlier follow-on may leave a grid of its own, as here
        Check(!script.Text.Contains("\nold_value <- ") && !script.Text.Contains("\nname <- "), "the regression's script has no stray vectors from the bag (the prediction's grid source, the predictors' names)");
        JsonNode node = JsonNode.Parse(parent.Record);
        node["inputs"].AsArray().Add(JsonNode.Parse("{\"name\":\"newValues\",\"prompt\":\"\",\"inherited\":true,\"kind\":\"entered\",\"columns\":[{\"title\":\"New name\",\"values\":[\"x1 (Longley)\"]},{\"title\":\"New value\",\"values\":[101.68125]}]}"));
        RScript withGrid = RScriptWriter.Write(JsonDocument.Parse(node.ToJsonString()).RootElement, null, null);
        Check(withGrid.Text == script.Text, "an inherited grid the recipe does not take (a follow-on's new values) is left out of the script");
        // the same grid asked for by the run itself (the prediction's own new values): the recipe names every input it takes, so it is left out too
        JsonArray added = node["inputs"].AsArray();
        added[added.Count - 1]["inherited"] = false;
        RScript withOwnGrid = RScriptWriter.Write(JsonDocument.Parse(node.ToJsonString()).RootElement, null, null);
        Check(withOwnGrid.Text == script.Text, "a grid the run asked for that the recipe does not name is left out as well, the recipe naming every input it takes");
        node["operation"] = "NoSuchOperation";
        RScript withoutRecipe = RScriptWriter.Write(JsonDocument.Parse(node.ToJsonString()).RootElement, null, null);
        Check(!withoutRecipe.HasRecipe && withoutRecipe.Text.Contains("\nnew_value <- c(101.68125)") && withoutRecipe.Text.Contains("\nold_value <- c("), "without a recipe the inherited frames are written with the rest of the data");
    }
}

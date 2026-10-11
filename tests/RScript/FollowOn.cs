using System.Text.Json;
using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;

internal static partial class Program
{
    // A follow-on operation (an exact P by simulation, a prediction, the residuals) continues the run of an earlier operation
    // without asking for its data again: the record of its run must carry the frames it inherited, marked so, and its script must
    // be the earlier run's.  A table typed on screen reaches the record as an entered frame under the grid's name.
    private static void CheckFollowOn()
    {
        RRecipes.Folder = Metadata("Recipes");
        double[][] grass = { new double[] { 4, 3, 2, 1 }, new double[] { 4, 2, 3, 1 }, new double[] { 3, 1.5, 1.5, 4 }, new double[] { 3, 1, 2, 4 }, new double[] { 4, 2, 1, 3 }, new double[] { 2, 2, 2, 4 },
                             new double[] { 1, 3, 2, 4 }, new double[] { 2, 4, 1, 3 }, new double[] { 3.5, 1, 2, 3.5 }, new double[] { 4, 1, 3, 2 }, new double[] { 4, 2, 3, 1 }, new double[] { 3.5, 1, 2, 3.5 } };
        DataFrame frame = new();
        for (int c = 0; c < 4; c++)
            frame.Variables.Add(Column("Grass " + (c + 1), grass.Select(r => r[c]).ToArray()));
        var (parent, parentOutput) = Run("Friedman", new() { ["data"] = frame });
        JsonElement parentRecord = Record(parent);
        Check(parentRecord.GetProperty("inputs").GetArrayLength() == 1 && parentRecord.GetProperty("inputs")[0].GetProperty("name").GetString() == "data", "the Friedman run records its frame as its one input");
        var (followOn, _) = Run("FriedmanSimulateExactP", new() { ["iterations"] = 2000, ["seed"] = 1, ["ci"] = 0.95 }, parentOutput.ParameterBag);
        JsonElement record = Record(followOn);
        JsonElement inherited = record.GetProperty("inputs").EnumerateArray().FirstOrDefault(i => i.GetProperty("name").GetString() == "data");
        Check(inherited.ValueKind == JsonValueKind.Object && inherited.TryGetProperty("inherited", out JsonElement flag) && flag.ValueKind == JsonValueKind.True && inherited.GetProperty("kind").GetString() == "entered",
              "the follow-on's record carries the frame it inherited from the Friedman run, marked inherited");
        Check(inherited.GetProperty("columns").GetArrayLength() == 4 && inherited.GetProperty("columns")[0].GetProperty("values")[2].GetDouble() == 3, "the inherited frame holds the columns and values of the earlier run");
        string[] Body(RScript s) => s.Text.Split('\n').Skip(1).ToArray();   // all but the title line
        RScript parentScript = RScriptWriter.Write(parentRecord, null, null), followOnScript = RScriptWriter.Write(record, null, null);
        string[] a = Body(parentScript), b = Body(followOnScript);
        int differs = Enumerable.Range(0, Math.Max(a.Length, b.Length)).FirstOrDefault(i => i >= a.Length || i >= b.Length || a[i] != b[i], -1);
        if (differs >= 0) Console.WriteLine($"      the scripts differ at line {differs + 2}: parent \"{(differs < a.Length ? a[differs] : "(end)")}\", follow-on \"{(differs < b.Length ? b[differs] : "(end)")}\"");
        Check(followOnScript.HasRecipe && differs < 0, "the follow-on's script is the earlier run's script, the same recipe on the same data");
        // a grid typed on screen: the special parameter's frame, its columns Column 1 and Column 2
        DataFrame grid = new();
        grid.Variables.Add(Column("Column 1", new double[] { 19, 29, 24 }));
        grid.Variables.Add(Column("Column 2", new double[] { 497, 560, 269 }));
        var (screen, _) = Run("Chi2byNWithoutTrend", new() { ["data"] = grid });
        JsonElement screenRecord = Record(screen);
        JsonElement entered = screenRecord.GetProperty("inputs").EnumerateArray().FirstOrDefault(i => i.GetProperty("name").GetString() == "data");
        Check(entered.ValueKind == JsonValueKind.Object && entered.GetProperty("kind").GetString() == "entered" && entered.GetProperty("columns").GetArrayLength() == 2, "a table typed on screen is recorded as an entered frame under the grid's name");
        RScript screenScript = RScriptWriter.Write(screenRecord, null, null);
        string output = RunR(screenScript.Text, Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "screen"));
        Check(screenScript.HasRecipe && Figures(output).Length > 0 && output.Contains("X-squared"), "the recipe of the 2 by k table runs on the recorded grid and gives R's chi-square");
    }
}

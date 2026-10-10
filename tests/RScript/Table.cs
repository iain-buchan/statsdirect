using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.R;

internal static partial class Program
{
    /// <summary>Pearson's chi-square of a 2 by 2 table from its definition, without continuity correction.</summary>
    private static double PearsonChiSquare(double a, double b, double c, double d)
    {
        double n = a + b + c + d, chi = 0;
        double[,] o = { { a, b }, { c, d } };
        double[] rows = { a + b, c + d }, cols = { a + c, b + d };
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
            {
                double e = rows[i] * cols[j] / n;
                chi += (o[i, j] - e) * (o[i, j] - e) / e;
            }
        return chi;
    }

    // A 2 by 2 table typed into the dialog: the program holds it as four named counts; the record puts it back together as the
    // recipes take it, and R's chi-square on the table agrees with the definition
    private static void CheckTable()
    {
        RRecipes.Folder = Metadata("Recipes");
        var (run, _) = Run("Chi2by2", new() { ["scrap"] = new double[] { 10, 5, 3, 12 }, ["cco"] = 0.95, ["study_type"] = "casecontrol", ["doFisher"] = false });
        JsonElement r = Record(run);
        JsonElement input = r.GetProperty("inputs")[0];
        Check(input.GetProperty("name").GetString() == "scrap" && input.GetProperty("kind").GetString() == "table", "the table is the first input of the run, of the kind table");
        JsonElement columns = input.GetProperty("columns");
        Check(columns.GetArrayLength() == 2 && columns[0].GetProperty("title").GetString() == "Present" && columns[1].GetProperty("title").GetString() == "Absent"
              && columns[0].GetProperty("values").EnumerateArray().Select(v => v.GetDouble()).SequenceEqual(new double[] { 10, 3 }) && columns[1].GetProperty("values").EnumerateArray().Select(v => v.GetDouble()).SequenceEqual(new double[] { 5, 12 }),
              "the table's columns are recorded as a frame, each column's counts top to bottom under the column's label");
        Check(input.GetProperty("counts")[0].EnumerateArray().Select(v => v.GetDouble()).SequenceEqual(new double[] { 10, 5 }) && input.GetProperty("counts")[1][1].GetDouble() == 12
              && input.GetProperty("rowLabels")[1].GetString() == "Absent" && r.GetProperty("parameters").GetProperty("counts")[1][0].GetDouble() == 3 && r.GetProperty("parameters").GetProperty("columnLabels")[0].GetString() == "Present",
              "the table's rows of counts and the labels of its rows and columns are recorded, among the settings too");
        Check(!r.GetProperty("inputs").EnumerateArray().Any(i => i.GetProperty("name").GetString() == "a") && r.GetProperty("parameters").GetProperty("a").GetDouble() == 10,
              "the four named counts stay among the settings and are not listed as inputs of their own");
        RScript script = RScriptWriter.Write(r, null, null);
        Check(script.HasRecipe && script.Text.Contains("data_frames[[\"scrap\"]] <- setNames(list(c(10, 3),\n  c(5, 12)), c(\"Present\", \"Absent\"))") && script.Text.Contains("\"counts\""),
              "the script writes the table as columns and as rows of counts");
        string output = RunR(script.Text, Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks"));
        double chi = Figure(output, "X-squared = ([0-9.eE+-]+), df = 1"), expected = PearsonChiSquare(10, 5, 3, 12);
        Console.WriteLine($"      R: X-squared = {chi}; from the definition {expected.ToString("R", inv)}");
        Check(Close(chi, expected, 1e-4), "R's chi-square on the recorded table agrees with the definition");
        Check(Regex.IsMatch(output, "Present\\s+Absent") , "R's table carries the labels of the rows and columns");
    }
}

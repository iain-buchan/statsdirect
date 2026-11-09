using System.Text.Json;
using StatsDirect.R;

internal static partial class Program
{
    private static void CheckDuplicatePredictorReplay(string folder)
    {
        RRecipes.Folder = Metadata("Recipes");
        string[] titles = { "outcome", "outcome_2", "outcome", null, "", "Column" };
        string[] storedTitles = { "outcome", "outcome_2", "outcome_3", "", "_2", "Column" };
        string[] expectedTitles = { "outcome", "outcome_2", "outcome_3", "Column_2", "_2", "Column" };
        int[] rows = Enumerable.Range(0, 16).ToArray();
        double[][] x = Enumerable.Range(0, titles.Length)
            .Select(j => rows.Select(i => i / 2 == j ? 1d : 0d).ToArray()).ToArray();
        double[] y = rows.Select(i => 7 + (i / 2 < titles.Length ? i / 2 + 1 : 0) + (i % 2 == 0 ? -.25 : .25)).ToArray();

        var outcome = new { name = "outcome", kind = "entered", columns = new[] { new { title = "Y", values = y } } };
        var entered = new { name = "predictors", kind = "entered", columns = titles.Select((title, i) => new { title, values = x[i] }).ToArray() };
        var frame = new
        {
            name = "predictors", kind = "frame", file = "test.xlsx", sheet = "Data",
            columns = titles.Select((title, i) => new { title, column = i + 1, rows = new[] { 1, rows.Length + 1 }, hasTitle = true }).ToArray()
        };
        var held = new { predictors = storedTitles.Select((title, i) => (title, values: x[i])).ToDictionary(c => c.title, c => c.values) };
        FrameValues Read(FramePointer pointer)
        {
            FrameValues values = new();
            foreach (ColumnPointer column in pointer.Columns)
                values.Columns.Add(new ColumnValues { Title = column.Title, Values = x[column.Column - 1].Cast<object>().ToArray() });
            return values;
        }

        Verify("entered", entered);
        Verify("held", frame, held);
        Verify("reread", frame, read: Read);

        void Verify(string path, object predictors, object data = null, Func<FramePointer, FrameValues> read = null)
        {
            JsonElement record = JsonSerializer.SerializeToElement(new
            {
                version = 1, operation = "MultipleLinearRegression",
                inputs = new[] { (object)outcome, predictors },
                parameters = new { ci = .95, calculateIntercept = true }, data
            });
            RScript script = RScriptWriter.Write(record, null, read);
            Check(script.HasRecipe && !script.DataUnavailable, path + " predictor data are available for model replay");
            string names = string.Join(", ", expectedTitles.Select(RScriptWriter.Quote));
            string assertions = "\nstopifnot(identical(names(predictors), c(" + names + ")),\n" +
                "  fit$rank == 7, sd_own[1] != 'outcome',\n" +
                "  isTRUE(all.equal(unname(coef(fit)), c(7, 1:6), tolerance=1e-10)))\n" +
                "cat('DUPLICATE_PREDICTORS_OK\\n')\n";
            Check(RunR(script.Text + assertions, Path.Combine(folder, path)).Contains("DUPLICATE_PREDICTORS_OK"),
                path + " replays every predictor with stable names, order and coefficients");
        }
    }
}

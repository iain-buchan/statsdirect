using System.Text.Json;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

internal static partial class Program
{
    // A whole-column selection on a sheet whose other columns are longer takes in blank rows below the data. The record keeps each
    // column up to the last value in any of them, so the R script's vectors are the data, not padded with NA to the sheet's used
    // range, and the recipes' own summary lines agree with the report. Interior missing values stay, keeping paired rows aligned.
    private static void CheckTrailingBlankRows(string folder)
    {
        double[] before = { 312, 242, 340, 388, 296, 254, 391, 402, 290 }, after = { 300, 201, 232, 312, 220, 256, 328, 330, 231 };
        const int selected = 298;   // the data rows a whole-column selection took in, below the title row, on a sheet used to row 299
        double[] Padded(double[] values)
        {
            double[] v = Enumerable.Repeat(Constant.MISSING, selected).ToArray();
            values.CopyTo(v, 0);
            return v;
        }
        DataFrame frame = new();
        frame.Variables.Add(Column("PEFR Before", Padded(before), new WorksheetOrigin(Workbook, "Parametric", 0, 0, selected + 1, DataAcquisitionMode.NumericPreserveRows, true, false, 1)));
        frame.Variables.Add(Column("PEFR After", Padded(after), new WorksheetOrigin(Workbook, "Parametric", 1, 0, selected + 1, DataAcquisitionMode.NumericPreserveRows, true, false, 1)));
        var (run, _) = Run("TPaired", new() { ["data"] = frame, ["gamma"] = 0.95, ["doAgreement"] = true });
        JsonElement record = Record(run);
        JsonElement columns = AcquisitionInput(record, "data").GetProperty("columns");
        Check(columns.GetArrayLength() == 2 && columns[0].GetProperty("rows")[0].GetInt32() == 1 && columns[0].GetProperty("rows")[1].GetInt32() == 10
              && columns[1].GetProperty("rows")[1].GetInt32() == 10,
            "a whole-column selection's record ends each column's row range at the last value in any column, after the title row");
        Check(columns[0].GetProperty("hash").GetString() == AcquisitionHash(before) && columns[1].GetProperty("hash").GetString() == AcquisitionHash(after),
            "the pointers' hashes are those of the kept values, which reading the recorded rows back gives again");
        JsonElement held = record.GetProperty("data").GetProperty("data");
        Check(held.GetProperty("PEFR Before").GetArrayLength() == 9 && held.GetProperty("PEFR After").GetArrayLength() == 9 && held.GetProperty("PEFR After")[8].GetDouble() == 231,
            "the values held with the record stop at the last value in any column");
        RScript script = RScriptWriter.Write(record, null, pointer => throw new InvalidOperationException("the held values should be used, not a re-read"));
        Check(script.HasRecipe && script.Text.Contains("rows 1 to 10") && !script.Text.Contains(", NA"),
            "the script's data block names rows 1 to 10 and carries no NA padding");
        string output = RunR(script.Text + "\nstopifnot(length(before) == 9L, length(after) == 9L)\ncat(\"TRAILING BLANK ROWS VERIFIED\\n\")\n", Path.Combine(folder, "trailing-blank-rows"));
        Check(output.Contains("Mean of differences = 56.111111  (n = 9)") && output.Contains("Power (for 5% significance) = 98.98%")
              && output.Contains("95% Limits of agreement = -10.868665 to 123.090887") && !output.Contains("= NA") && !output.Contains("Error") && output.Contains("TRAILING BLANK ROWS VERIFIED"),
            "the paired t recipe reproduces the report from the kept rows, with its agreement plot and no NA lines");
        CheckRecordPadding(folder);
        // interior missing values stay, and a column's own blank within the rows another column still fills stays too
        double[] x = { 1, 2, Constant.MISSING, 4, 5, Constant.MISSING, Constant.MISSING }, y = { 2, 4, 6, 8, Constant.MISSING, 12, Constant.MISSING };
        DataFrame ragged = new();
        ragged.Variables.Add(Column("x", x, new WorksheetOrigin(Workbook, "Parametric", 0, 0, x.Length + 1, DataAcquisitionMode.NumericPreserveRows, true, false, 1)));
        ragged.Variables.Add(Column("y", y, new WorksheetOrigin(Workbook, "Parametric", 1, 0, y.Length + 1, DataAcquisitionMode.NumericPreserveRows, true, false, 1)));
        var (raggedRun, _) = Run("TPaired", new() { ["data"] = ragged, ["gamma"] = 0.95, ["doAgreement"] = false });
        JsonElement raggedRecord = Record(raggedRun);
        JsonElement raggedHeld = raggedRecord.GetProperty("data").GetProperty("data");
        Check(raggedHeld.GetProperty("x").GetArrayLength() == 6 && raggedHeld.GetProperty("y").GetArrayLength() == 6
              && raggedHeld.GetProperty("x")[2].ValueKind == JsonValueKind.Null && raggedHeld.GetProperty("x")[5].ValueKind == JsonValueKind.Null && raggedHeld.GetProperty("y")[5].GetDouble() == 12
              && AcquisitionInput(raggedRecord, "data").GetProperty("columns")[0].GetProperty("rows")[1].GetInt32() == 7,
            "interior missing values and a column's blank within the kept rows stay, so the pairs keep their alignment");
    }
}

using StatsDirect.Data;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    /// <summary>
    /// Reads the columns a record points to, for the R script of a result: from the workbook open in the program, or from the file the
    /// record names, opened in the program if it is still there.  Each column's values come back with whether they differ from those the
    /// analysis used, by the hash kept with the result.
    /// </summary>
    internal static class FrameReader
    {
        internal static FrameValues Read(FramePointer pointer)
        {
            if (pointer == null || pointer.Columns.Count == 0)
                return new FrameValues { Unavailable = "the record names no columns" };
            string file = string.IsNullOrEmpty(pointer.Path) ? pointer.File : pointer.Path;
            StatsDirectForm form;
            try { form = SdApplication.SoleInstance.FindOrOpenGrid(file); }
            catch (Exception ex) { return new FrameValues { Unavailable = ex.Message }; }
            if (form is not IGrid grid)
                return new FrameValues { Unavailable = $"the workbook {pointer.File} is not open and was not found at {file}" };
            if (!Enum.TryParse(pointer.Mode, out DataAcquisitionMode mode) || mode == DataAcquisitionMode.NotSet)
                mode = DataAcquisitionMode.NumericReplaceMissing;
            List<WorksheetOrigin> origins = new();
            foreach (ColumnPointer column in pointer.Columns)
                origins.Add(new WorksheetOrigin(file, column.Sheet ?? pointer.Sheet, column.Column - 1, column.FirstRow - 1, column.LastRow - column.FirstRow + 1, mode, column.HasTitle, false, 0));
            DataFrame frame;
            try { frame = grid.ReadColumns(origins); }
            catch (Exception ex) { return new FrameValues { Unavailable = ex.Message }; }
            if (frame == null || frame.VariableCount != pointer.Columns.Count)
                return new FrameValues { Unavailable = $"sheet {pointer.Sheet} of {pointer.File} no longer gives {pointer.Columns.Count} columns at the recorded place" };
            FrameValues values = new();
            for (int i = 0; i < frame.VariableCount; i++)
            {
                IVariable variable = frame.Variables[i];
                values.Columns.Add(new ColumnValues
                {
                    Title = string.IsNullOrEmpty(pointer.Columns[i].Title) ? variable.Title : pointer.Columns[i].Title,
                    Values = RunRecord.Values(variable),
                    Changed = pointer.Columns[i].Hash != null && RunRecord.Hash(variable) != pointer.Columns[i].Hash
                });
            }
            return values;
        }
    }
}

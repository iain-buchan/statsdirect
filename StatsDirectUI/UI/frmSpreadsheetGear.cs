using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SpreadsheetGear.Commands;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using SpreadsheetGear.Advanced.Cells;
using SpreadsheetGear;
using SpreadsheetGear.Windows.Forms;
using Color = System.Drawing.Color;
using SystemColors = System.Drawing.SystemColors;

namespace StatsDirect.UI
{
    internal partial class frmSpreadsheetGear : StatsDirectForm, IGrid
    {
        /// <summary>
        /// While batching, a storage point for the data area we've most recently selected.
        /// </summary>
        private CellSelection mostRecentCellSelectionDuringBatch;

        public frmSpreadsheetGear()
        {
            InitializeComponent();
            SdApplication.SoleInstance.MainWindow.EnsureBuiltInMenuItemsCanShowHelp(menuStrip);
            workbookView.GetLock();
            try
            {
                workbookView.ActiveWorkbookSet.DefaultFontName = "Calibri";
                workbookView.ActiveWorkbookSet.DefaultFontSize = 11;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void frmSpreadsheetGear_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AllowClose())
            {
                e.Cancel = true;
                return;
            }
            SdApplication.SoleInstance.NoteFormClosing(this, e);
        }

        private void frmSpreadsheetGear_Activated(object sender, EventArgs e)
        {
            if (null != Tag)
                SdApplication.SoleInstance.NoteFormActivated((WindowInformation)Tag);
            // SDApplication.SoleInstance.MostRecentlySelectedGrid = new PaneAndBoolean(this.SelectedPane, true); // By default, new grid data is inserted not appended
            tableLayoutPanel1.Visible = true;
        }

        private void workbookView_CellEndEdit(object sender, CellEndEditEventArgs e)
        {
            dirty = true;
        }

        private void workbookView_RangeChanged(object sender, RangeChangedEventArgs e)
        {
            dirty = true;
        }

        /// <summary>
        /// Save the active workbook if a path is already known; fall back to SaveAs if no path is known for this workbook.
        /// </summary>
        /// <returns>true if the save went OK, false if the save was cancelled</returns>
        internal override bool SaveContents()
        {
            if (string.IsNullOrEmpty(path))
                return SaveAsContents();

            if (new FileInfo(path).IsReadOnly)
                return SaveAsContents();

            // Known path, overwrite
            workbookView.GetLock();
            try
            {
                workbookView.ActiveWorkbook.Save();
                dirty = false;
                return true;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        internal override void ClearBatchMode()
        {
            base.ClearBatchMode();
            mostRecentCellSelectionDuringBatch = null;
        }

        /// <summary>
        /// Request a filename from the user and save the active workbook.
        /// </summary>
        /// <returns>true if the save went OK, false if the save was cancelled</returns>
        internal override bool SaveAsContents()
        {
            // Unknown path, prompt for path to which to save
            saveFileDialog.Title = "Save " + Text + " as...";
            if (null == path)
            {
                saveFileDialog.FileName = ((WindowInformation)Tag).FriendlyName;
            }
            else
            {
                saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(path);
                saveFileDialog.FileName = System.IO.Path.GetFileName(path);
            }
            DialogResult result = saveFileDialog.ShowDialog(SdApplication.SoleInstance.MainWindow);
            if (DialogResult.Cancel == result)
            {
                // User cancelled, failed save
                return false;
            }
            // User wants to save the file
            Path = saveFileDialog.FileName;
            string extension = System.IO.Path.GetExtension(Path);
            if (!string.IsNullOrEmpty(extension))
                extension = extension.ToLower();
            FileFormat format = ".xlsx".Equals(extension) ? FileFormat.OpenXMLWorkbook : FileFormat.Excel8;
            workbookView.GetLock();
            try
            {
                workbookView.ActiveWorkbook.SaveAs(path, format);
                dirty = false;
                SdApplication.SoleInstance.NoteRecentFile(path, true);
                return true;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        public override bool OpenFile(string filename, bool isTempFile)
        {
            if (null != workbookView.ActiveWorkbook)
            {
                workbookView.GetLock();
                try
                {
                    workbookView.ActiveWorkbook.Close();
                }
                finally
                {
                    workbookView.ReleaseLock();
                }
            }
            workbookView.GetLock();
            IWorkbook wb;
            try
            {
                wb = workbookView.ActiveWorkbookSet.Workbooks.Open(filename);
                if (!isTempFile)
                    Path = filename;
                dirty = isTempFile;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
            if (null != wb)
                workbookView.ActiveWorkbook = wb;
            return null != wb;
        }

        #region IGrid Members

        bool IGrid.Dirty
        {
            get { return dirty; }
        }

        object[,] IGrid.GetValues(int top, int left, int bottom, int right)
        {
            workbookView.GetLock();
            try
            {
                object val = workbookView.ActiveWorksheet.Cells[top, left, bottom, right].Value;
                if (null != val && val.GetType().IsArray)
                {
                    return (object[,])val;
                }
                return new[,] { { val } };
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        string IGrid.ActiveWorksheetName
        {
            get
            {
                workbookView.GetLock();
                try
                {
                    string name = workbookView.ActiveWorksheet.Name;
                    return name;
                }
                finally
                {
                    workbookView.ReleaseLock();
                }
            }
        }

        string IGrid.WorkbookPath
        {
            get
            {
                workbookView.GetLock();
                try
                {
                    return workbookView.ActiveWorkbook.FullName;
                }
                finally
                {
                    workbookView.ReleaseLock();
                }
            }
        }

        void IGrid.SetValues(int top, int left, object[,] values)
        {
            int RowsMinusOne = values.GetUpperBound(0) - values.GetLowerBound(0);
            int ColsMinusOne = values.GetUpperBound(1) - values.GetLowerBound(1);
            workbookView.GetLock();
            try
            {
                workbookView.ActiveWorksheet.Cells[top, left, top + RowsMinusOne, left + ColsMinusOne].Value = values;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
            dirty = true;
        }

        void IGrid.Refill(Variable variable, WorksheetOrigin worksheetOrigin)
        {
            // Set the active worksheet
            workbookView.ActiveWorkbookSet.GetLock();
            try
            {
                string sheetName = worksheetOrigin.WorksheetName;
                IWorksheet worksheet = workbookView.ActiveWorkbook.Worksheets[sheetName];
                bool succeeded = (null != worksheet);
                if (!succeeded)
                    throw new Exception("Cannot refill variable as the worksheet \"" + worksheetOrigin.WorksheetName + "\" in workbook \"" + worksheetOrigin.WorkbookPath + "\" no longer exists.");
                workbookView.ActiveSheet = worksheet;
            }
            finally
            {
                workbookView.ActiveWorkbookSet.ReleaseLock();
            }

            CellColumnSelection cellColumnSelection = new CellColumnSelection
                                                          {
                                                              ColumnIndex = worksheetOrigin.Column,
                                                              RowCount = worksheetOrigin.Rows,
                                                              RowIndex = worksheetOrigin.TopRow
                                                          };
            MaybeExpandCellColumnSelection(cellColumnSelection);
            CellSelection revisedCellSelection = new CellSelection();
            revisedCellSelection.ColumnSelections.Add(cellColumnSelection);
            revisedCellSelection.LongestRowCount = cellColumnSelection.RowCount;
            DataFrame refilledFrame = ProcessCellArray(revisedCellSelection, worksheetOrigin.Mode, 0, true, worksheetOrigin.HasTitle);
            Variable refilledVariable = refilledFrame.Variables[0];
            variable.StealDataFrom(refilledVariable);
        }

        /// <summary>
        /// If the selection was previously shorter, but there is now more data in the column, expand it.
        /// </summary>
        /// <param name="cellColumnSelection"></param>
        private void MaybeExpandCellColumnSelection(CellColumnSelection cellColumnSelection)
        {
            const int PROBE_ROWS = 1000; // This many rows will be examined for new data in each iteration

            // If there's more contiguous data below the selection, include it in the selection.  Stop as soon as there's a blank cell.
            Area usedArea = ((IGrid)this).UsedArea;
            int lastUsedRow = usedArea.Bottom;
            int probeColumn = cellColumnSelection.ColumnIndex;
            int probeTop = cellColumnSelection.RowIndex + cellColumnSelection.RowCount;
            int probeBottom = Math.Min(probeTop + PROBE_ROWS - 1, lastUsedRow);
            while (probeTop <= probeBottom)
            {
                object[,] probe = GetCellObjects(probeColumn, probeTop, probeBottom);
                for (int offset = 0; offset <= probe.GetUpperBound(0); offset++)
                {
                    object value = probe[offset, 0];
                    if (null == value)
                    {
                        int lastRowWithContents = probeTop + offset - 1;
                        cellColumnSelection.RowCount = lastRowWithContents - cellColumnSelection.RowIndex + 1;
                        return;
                    }
                }
                probeTop = probeBottom + 1;
                probeBottom = Math.Min(probeTop + PROBE_ROWS - 1, lastUsedRow);
            }
        }

        void IGrid.WriteDataFrame(DataFrame frame, bool isFormulae, string missingIndicator, RelativePosition writePosition)
        {
            // We can get blank frames passed in; handle this by doing nothing.
            if (frame.VariableCount <= 0)
                return;

            bool isLocked = false;
            try
            {
                workbookView.GetLock();
                isLocked = true;
                IWorksheet worksheet = workbookView.ActiveWorksheet;
                IRange rawRange = worksheet.UsedRange;
                int firstFreeColumn = rawRange.Column + rawRange.ColumnCount;
                // Don't believe the free columns - there may be more space!
                while (firstFreeColumn > 0)
                {
                    bool allBlank = true;
                    for (int r = rawRange.Row; r < rawRange.Row + rawRange.RowCount; r++)
                    {
                        if (null != worksheet.Cells[r, firstFreeColumn - 1].Value)
                        {
                            allBlank = false;
                            break;
                        }
                    }
                    if (!allBlank)
                        break;
                    firstFreeColumn--;
                }
                int availableColumns = workbookView.ActiveWorksheet.Cells.ColumnCount;
                if (availableColumns - firstFreeColumn < frame.Variables.Count)
                    throw new Exception("No room to write output data on the sheet");

                int offsetForTitles = 0;
                foreach (Variable v in frame.Variables)
                {
                    if (null != v.Title)
                    {
                        offsetForTitles = 1;
                        break;
                    }
                }

                // Work out the first column we're going to insert into, moving the existing contents out of the way if we're inserting in existing data
                int firstColumnOfData;
                bool shouldMove = false;
                switch (writePosition)
                {
                    case RelativePosition.FirstColumn:
                        firstColumnOfData = 0;
                        // Move existing contents out of the way
                        shouldMove = true;
                        break;
                    case RelativePosition.BeforeSelection:
                        // The first column is the first column of the current selection
                        firstColumnOfData = workbookView.RangeSelection.Column;
                        // Move existing contents out of the way
                        shouldMove = true;
                        break;
                    case RelativePosition.AfterSelection:
                        // The first column is just past the current selection
                        firstColumnOfData = workbookView.RangeSelection.Column + workbookView.RangeSelection.ColumnCount;
                        // Move existing contents out of the way
                        shouldMove = true;
                        break;
                    case RelativePosition.LastColumn:
                        firstColumnOfData = firstFreeColumn;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("writePosition", writePosition, "Unknown write position");
                }

                IRange range = worksheet.Range[0, firstColumnOfData, rawRange.Row + rawRange.RowCount + offsetForTitles - 1, firstColumnOfData + frame.VariableCount - 1];
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection.EntireColumn, "Insert data", () => { WriteDataFrameInternal(frame, isFormulae, missingIndicator, range, shouldMove, offsetForTitles); return true; }));
            }
            finally
            {
                if (isLocked)
                    workbookView.ReleaseLock();
            }
        }

        private void WriteDataFrameInternal(DataFrame frame, bool isFormulae, string missingIndicator, IRange range, bool shouldMove, int offsetForTitles)
        {
            bool isLocked = false;
            try
            {
                workbookView.GetLock();
                isLocked = true;
                IWorksheet worksheet = workbookView.ActiveWorksheet;
                IValues values = (IValues)worksheet;
                // Work out the first column we're going to insert into, moving the existing contents out of the way if we're inserting in existing data
                int firstColumnOfData = range.Column;
                if (shouldMove)
                {
                    // Move existing contents out of the way
                    range.Insert(InsertShiftDirection.Right);
                }

                // Now do the inserts.  Frames may contain any types of variables.
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    string title = frame.Variables[v].Title;
                    if (null != title)
                        values.SetText(0, firstColumnOfData + v, title);
                    switch (frame.Variables[v].VariableType)
                    {
                        case VariableType.StringType:
                            {
                                StringVariable variable = frame.Variables[v].AsStringVariable;
                                string[] data = variable.Data;
                                for (int i = 0; i < data.Length; i++)
                                {
                                    string value = data[i] ?? "";
                                    if (isFormulae && value.Length > 0)
                                    {
                                        try
                                        {
                                            values.SetFormula(i + offsetForTitles, firstColumnOfData + v, value);
                                        }
                                        catch (ArgumentException)
                                        {
                                            // Almost certainly trying to set something that's not legal as a formula.  Try it as text instead.
                                            values.SetText(i + offsetForTitles, firstColumnOfData + v, value);
                                        }
                                    }
                                    else
                                    {
                                        values.SetText(i + offsetForTitles, firstColumnOfData + v, value);
                                    }
                                }
                            }
                            break;
                        case VariableType.DoubleType:
                            {
                                DoubleVariable variable = frame.Variables[v].AsDoubleVariable;
                                double[] data = variable.Data;
                                if (null != data)
                                {
                                    for (int i = 0; i < data.Length; i++)
                                        if (Constant.MISSING == data[i] || double.IsNaN(data[i]))
                                        {
                                            values.SetText(i + offsetForTitles, firstColumnOfData + v, missingIndicator);
                                        }
                                        else
                                        {
                                            values.SetNumber(i + offsetForTitles, firstColumnOfData + v, data[i]);
                                        }
                                }
                            }
                            break;
                        case VariableType.ClassifierType:
                            {
                                ClassifierVariable variable = frame.Variables[v].AsClassifierVariable;
                                double[] data = variable.Data;
                                if (null != data)
                                {
                                    for (int i = 0; i < data.Length; i++)
                                        values.SetText(i + offsetForTitles, firstColumnOfData + v,
                                                       Constant.MISSING == data[i]
                                                           ? missingIndicator
                                                           : variable.Groups[(int)data[i]].Label);
                                }
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("frame", frame.Variables[v].VariableType, "Unknown variable type");
                    }
                }
                IRange insertedRange = range[0, 0, frame.MaxRows + offsetForTitles - 1, range.ColumnCount - 1];
                insertedRange.Select();
                insertedRange.NumberFormat = "";
                insertedRange.Columns.AutoFit();
                dirty = true;
            }
            finally
            {
                if (isLocked)
                    workbookView.ReleaseLock();
            }
        }

        Area IGrid.UsedArea
        {
            get
            {
                bool isLocked = false;
                try
                {
                    workbookView.GetLock();
                    isLocked = true;
                    IRange rawRange = workbookView.ActiveWorksheet.UsedRange;
                    return IRangeToArea(this, rawRange);
                }
                finally
                {
                    if (isLocked)
                        workbookView.ReleaseLock();
                }
            }
        }

        Range IGrid.Selection
        {
            get
            {
                IRange rawSelection = workbookView.RangeSelection;
                IGrid grid = this;
                IList<Area> areas = new List<Area>(rawSelection.AreaCount);
                foreach (IRange range in rawSelection.Areas)
                    areas.Add(IRangeToArea(grid, range));
                return new Range(areas);
            }
            set
            {
                if (value.Areas.Count > 0)
                {
                    Area area = value.Areas[0];
                    IWorksheet worksheet = workbookView.ActiveWorksheet;
                    IRange newDataRange = worksheet.Range[area.Top, area.Left, area.Bottom, area.Right];
                    workbookView.GetLock();
                    try
                    {
                        newDataRange.Select();
                    }
                    finally
                    {
                        workbookView.ReleaseLock();
                    }
                }
            }
        }

        bool IGrid.HasSelection()
        {
            IRange rawSelection = workbookView.RangeSelection;
            return rawSelection.AreaCount > 1 || rawSelection.RowCount > 1 || rawSelection.ColumnCount > 1;
        }

        public void ClearSelection()
        {
            workbookView.GetLock();
            try
            {
                workbookView.ActiveCell.Select();
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        #endregion

        private static Area IRangeToArea(IGrid grid, IRange range)
        {
            return new Area(grid, range.Row, range.Column, range.Row + range.RowCount - 1, range.Column + range.ColumnCount - 1);
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't close workbook", ex, false);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SaveContents();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't save workbook", ex, false);
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SaveAsContents();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't save workbook", ex, false);
            }
        }

        internal ParameterBag FillGridParameter(Parameter parameter, ITemplateProcessor processor, ITemplateHost host, ParameterBag parameters)
        {
            GridParameter gridParameter = (GridParameter)parameter;
            if (gridParameter.ShouldClearSelectionFirst)
                ((IGrid)this).ClearSelection();

            while (true)
            {
                bool userCancelled;
                DataFrame frame;
                if (gridParameter.ColumnsAreSameLength || gridParameter.HasLength || null != gridParameter.SameLengthAsParameter)
                {
                    int requiredLength = 0;
                    if (gridParameter.HasLength)
                        requiredLength = gridParameter.Length(processor, parameters);
                    else if (null != gridParameter.SameLengthAsParameter)
                    {
                        if (parameters.ContainsKey(gridParameter.SameLengthAsParameter) && null != parameters[gridParameter.SameLengthAsParameter])
                        {
                            FilledParameter fp = parameters[gridParameter.SameLengthAsParameter];
                            if (fp.HasData && fp.IsDataFrame)
                            {
                                DataFrame f = fp.AsDataFrame;
                                requiredLength = f.MaxRows;
                            }
                        }
                    }
                    frame = GetCellEqual(requiredLength,
                        gridParameter.DataAcquisitionMode,
                        gridParameter.MinimumColumns(processor, parameters),
                        gridParameter.MaximumColumns(processor, parameters),
                        gridParameter.Prompt(processor, parameters),
                        gridParameter.CancelSkipsParameter,
                        gridParameter.ShouldAskForGroupId,
                        DataAcquisitionWidth.RespectPivotSetting,
                        false,
                        out userCancelled);
                }
                else
                {
                    int minimumColumns = gridParameter.MinimumColumns(processor, parameters);
                    int maximumColumns = gridParameter.MaximumColumns(processor, parameters);
                    string selectionMessage = gridParameter.Prompt(processor, parameters);
                    while (true)
                    {
                        bool wasPivoted;
                        if (gridParameter.ShouldAskForGroupId && SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier)
                        {
                            frame = Gidx(gridParameter.DataAcquisitionMode, minimumColumns, maximumColumns, -1, null, out userCancelled, out wasPivoted);
                        }
                        else
                        {
                            frame = GetCellArray(0, gridParameter.DataAcquisitionMode, minimumColumns, maximumColumns, selectionMessage, gridParameter.CancelSkipsParameter, gridParameter.ShouldAskForGroupId, false, out userCancelled, out wasPivoted);
                        }
                        if (!wasPivoted)
                            break;
                    }
                }
                if (null != frame)
                {
                    ParameterBag outputParameters = new ParameterBag();
                    if (!string.IsNullOrEmpty(gridParameter.AppendToFrame))
                    {
                        DataFrame targetFrame;
                        if (parameters.ContainsKey(gridParameter.AppendToFrame))
                        {
                            targetFrame = parameters[gridParameter.AppendToFrame].AsDataFrame;
                        }
                        else
                        {
                            targetFrame = new DataFrame();
                            outputParameters.Add(gridParameter.AppendToFrame, new FilledParameter(true, targetFrame));
                        }
                        foreach (Variable v in frame.Variables)
                            targetFrame.Variables.Add(v);
                        // frame.Variables.Clear(); Removed as this prevents validation - the original frame's variables have to stay intact until after the validation phase.
                        // HACK: As an unpleasant side effect, this means that *both* frames share a pointer to the variable.
                    }
                    outputParameters.Add(parameter.Name, new FilledParameter(true, frame));
                    return outputParameters;
                }

                // No frame was returned, either because the user cancelled or because of an error.  Distinguish the two cases!
                if (userCancelled)
                {
                    if (null != gridParameter.CancelSkipsParameter)
                        return null;
                    throw new TemplateOperationCancelledException();
                }


                // User error - go round again, but clear the selection to avoid unbreakable loops!
                ((IGrid)this).ClearSelection();
            }
        }

        private class CellColumnSelection
        {
            public string WorkbookPath { get; set; }
            public string WorksheetName { get; set; }
            /// <summary>
            /// The grid column (from the grid's base - 0 for SpreadsheetGear)
            /// </summary>
            public int ColumnIndex { get; set; }
            /// <summary>
            /// The first grid row (from the grid's base - 0 for SpreadsheetGear)
            /// </summary>
            public int RowIndex { get; set; }
            /// <summary>
            /// The number of rows
            /// </summary>
            public int RowCount { get; set; }
        }

        /// <summary>
        /// Holds a set of selections in a grid in a form that can be passed between functions conveniently.
        /// </summary>
        private class CellSelection
        {
            public List<CellColumnSelection> ColumnSelections { get; set; }
            /// <summary>
            /// The number of rows in the longest column in this selection
            /// </summary>
            public int LongestRowCount { get; set; }

            public CellSelection()
            {
                Clear();
            }

            /// <summary>
            /// Remove all data in this selection, preparing it to hold another selection.
            /// </summary>
            public void Clear()
            {
                ColumnSelections = new List<CellColumnSelection>();
                LongestRowCount = 0;
            }

            public int TotalColumns
            {
                get { return ColumnSelections.Count; }
            }
        }

        /// <summary>
        /// Returns the user's cell selections within the active worksheet, or null if the user declined to select anything.
        /// </summary>
        /// <param name="minimumColumns">The smallest acceptable number of columns</param>
        /// <param name="maximumColumns">The largest acceptable number of columns</param>
        /// <param name="selectionMessage">The prompt for the user (column numbers will be appended if required)</param>
        /// <param name="cancelButtonLabel">If non-null, the cancel button will take this label</param>
        /// <param name="allowUserToPivot">If true, the user is prompted for groups by column / groups by identifier.  If false, the user is not prompted.</param>
        /// <param name="selectionWasDefaulted">If true, there's already a selection on the sheet but the user *must* interact to confirm it.  If false, if there's a selection we'll take it.</param>
        /// <param name="userCancelled">If true, the user cancelled the selection</param>
        /// <param name="wasPivoted">If true, the user changed from selecting groups by column to by identifier, or vice versa</param>
        /// <returns>the user's cell selections within the active worksheet, or null if the user declined to select anything</returns>
        private CellSelection GetSelection(int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, bool selectionWasDefaulted, out bool userCancelled, out bool wasPivoted)
        {
            // Handle default selections: we may need to come in with a pre-selected area and force the user to confirm it.  However, if the user gives an illegal selection, we need to give errors.  So we keep a state of whether the selection presently on the grid is the default or is user-selected.
            bool selectionIsDefault = selectionWasDefaulted;
            const string msgTi = "StatsDirect Data Selection";

            // Get the number of selections & Last Row, Col info
            int rowtotal = 0; // The total number of rows in all columns
            CellSelection sel = new CellSelection();
            while (true)
            {
                // Get total number of columns and indexes to them
                IGrid grid = this;
                Range selection = grid.Selection;
                Area usedArea = grid.UsedArea;
                foreach (Area from in selection.Areas)
                {
                    Area usedFrom = from.Intersect(usedArea);
                    if (usedFrom.Cells > 0)
                    {
                        int rows = usedFrom.Rows;

                        if (rows > sel.LongestRowCount)
                            sel.LongestRowCount = rows;
                        int cols = usedFrom.Columns;
                        if (cols >= 0)
                        {
                            // For each selected column, record the column index and how many rows it has
                            for (int c = usedFrom.Left; c <= usedFrom.Right; c++)
                            {
                                CellColumnSelection ccs = new CellColumnSelection
                                {
                                    WorkbookPath = grid.WorkbookPath,
                                    WorksheetName = grid.ActiveWorksheetName,
                                    ColumnIndex = c,
                                    RowIndex = usedFrom.Top,
                                    RowCount = rows
                                };
                                sel.ColumnSelections.Add(ccs);
                                rowtotal += rows;
                            }
                        }
                    }
                }

                if (sel.TotalColumns > 0)
                {
                    // Throw out single row columns unless all are
                    List<CellColumnSelection> filteredColumnSelections = new List<CellColumnSelection>();
                    for (int c = 0; c < sel.TotalColumns; c++)
                    {
                        if (rowtotal == sel.TotalColumns || sel.ColumnSelections[c].RowCount > 1)
                        {
                            filteredColumnSelections.Add(sel.ColumnSelections[c]);
                        }
                    }
                    if (filteredColumnSelections.Count != sel.TotalColumns)
                    {
                        // We've lost some columns; copy in the revised ones
                        sel.ColumnSelections = filteredColumnSelections;
                    }
                }

                // Check the selection
                bool hasSelection = grid.HasSelection();
                if (sel.TotalColumns >= minimumColumns && sel.TotalColumns <= maximumColumns && hasSelection && !selectionIsDefault)
                {
                    // Fits our criteria; return it
                    userCancelled = false;
                    wasPivoted = false;
                    return sel;
                }
                // There's a problem, and this selection is inappropriate.  Or, this is the first time we've come in with a default selection, so the user has to confirm it.

                // Create an intelligent message to the user and show it
                if (hasSelection && sel.TotalColumns > 0 && !selectionIsDefault)
                {
                    SelNumWarn(minimumColumns, maximumColumns, sel.TotalColumns, msgTi);
                }
                string fullSelectionMessage = selectionMessage;
                if (minimumColumns == maximumColumns)
                {
                    fullSelectionMessage += " (" + minimumColumns.ToString() + " column" + (minimumColumns > 1 ? "s" : "") + ")";
                }
                else
                {
                    fullSelectionMessage += " (Min " + minimumColumns.ToString() + ": Max " + maximumColumns.ToString() + ")";
                }
                SdApplication.SoleInstance.MainWindow.CanSelectMultipleRows = maximumColumns > 1;
                SdApplication.SoleInstance.MainWindow.CanSelectGroupMethod = allowUserToPivot;
                if (allowUserToPivot)
                    SdApplication.SoleInstance.MainWindow.GroupsByIdentifier = SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier;
                Color oldBackColor = BackColor;
                BackColor = SystemColors.Info;
                try
                {
                    if (!SdApplication.SoleInstance.MainWindow.SelectCells(fullSelectionMessage, cancelButtonLabel, out wasPivoted))
                    {
                        // The user either cancelled or pivoted
                        userCancelled = !wasPivoted;
                        return null;
                    }
                }
                finally
                {
                    BackColor = oldBackColor;
                }

                // Clear this inappropriate selection (and go round the loop again)
                sel.Clear();
                selectionIsDefault = false;
            }
        }

        /// <summary>
        /// Returns a DataFrame containing the selected data, or null if there was an error or selection was cancelled.
        /// </summary>
        /// <param name="rowLengthHint"> </param>
        /// <param name="mode">The way in which the acquired data will be placed into the data structure</param>
        /// <param name="minimumColumns">The minimum acceptable number of columns</param>
        /// <param name="maximumColumns">The maximum acceptable number of columns</param>
        /// <param name="selectionMessage">The prompt for the user</param>
        /// <param name="cancelButtonLabel">If non-null, the cancel button will take this label</param>
        /// <param name="allowUserToPivot">If true, the user is asked about grouping by identifier</param>
        /// <param name="mightBeBatching">If true, we might be selecting data in a batch.  If batching, GetCellEqual should remember where its data came from and should re-select if such memory is present.  If not batching, GetCellEqual should clear memory and not pre-select.</param>
        /// <param name="userCancelled">If true, the user cancelled the selection</param>
        /// <param name="wasPivoted">If true, the user changed from selecting groups by column to by identifier, or vice versa</param>
        /// <returns></returns>
        private DataFrame GetCellArray(int rowLengthHint, DataAcquisitionMode mode, int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, bool mightBeBatching, out bool userCancelled, out bool wasPivoted)
        {
            bool shouldDefaultSelection = null != mostRecentCellSelectionDuringBatch && mightBeBatching;
            if (shouldDefaultSelection)
            {
                // Set up the cell selection from its memory.
                IGrid grid = this;
                CellColumnSelection col = mostRecentCellSelectionDuringBatch.ColumnSelections[0];
                grid.Selection = new Range(new[] { new Area(grid, col.RowIndex, col.ColumnIndex, col.RowIndex + col.RowCount - 1, col.ColumnIndex) });
            }

            CellSelection cellSelection = GetSelection(minimumColumns, maximumColumns, selectionMessage, cancelButtonLabel, allowUserToPivot, shouldDefaultSelection, out userCancelled, out wasPivoted);

            if (mightBeBatching)
                mostRecentCellSelectionDuringBatch = cellSelection;

            if (null == cellSelection)
            {
                // No selection
                Application.UseWaitCursor = false;
                return null;
            }

            return ProcessCellArray(cellSelection, mode, rowLengthHint, false, false);
        }

        private DataFrame ProcessCellArray(CellSelection cellSelection, DataAcquisitionMode mode, int rowLengthHint, bool isRefill, bool titleWasInData)
        {
            // If we get here, the user selected some data.
            try
            {
                DataFrame frame = new DataFrame();
                WindowInformation info = (WindowInformation)Tag;
                frame.Name = info.FriendlyName;
                switch (mode)
                {
                    case DataAcquisitionMode.NumericSkipMissing:
                        // read in the cells, skipping any missing data
                        for (int c = 0; c < cellSelection.TotalColumns; c++)
                        {
                            int dataRows;
                            int gridColumn;
                            int gridFirstDataRow;
                            bool titleIsInData;
                            string title = GetColumnTitle(cellSelection.ColumnSelections[c], out dataRows, out gridFirstDataRow, out gridColumn, out titleIsInData);
                            double[] values = GetCellValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1);

                            // If there's no data in the row (for example if it's hidden), ignore the row
                            if (null == values)
                                continue;

                            DoubleVariable variable = new DoubleVariable(values.Length, title);
                            frame.Variables.Add(variable);

                            variable.Data = new double[values.Length];
                            // Eliminate MISSING values by copying, then truncating the array
                            int size = 0;
                            for (int row = values.GetLowerBound(0); row <= values.GetUpperBound(0); row++)
                            {
                                double v = values[row];
                                if (v != Constant.MISSING && v != Constant.MISSING * 10D)
                                    variable.Data[size++] = v;
                            }

                            if (size < 1)
                            {
                                PointNormal();
                                SdApplication.SoleInstance.MsgboxX("You must select numerical data for this function", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Worksheet Data Selection", true);
                                return null;
                            }
                            variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, gridColumn, cellSelection.ColumnSelections[c].RowIndex, dataRows, mode, titleIsInData);
                            variable.TruncateDataToLength(size);
                        }
                        break;
                    case DataAcquisitionMode.NumericReplaceMissing:
                        // Read in the cells replacing missing data with Constant.MISSING
                        for (int c = 0; c < cellSelection.TotalColumns; c++)
                        {
                            int dataRows;
                            int gridColumn;
                            int gridFirstDataRow;
                            bool titleIsInData;
                            string title = GetColumnTitle(cellSelection.ColumnSelections[c], out dataRows, out gridFirstDataRow, out gridColumn, out titleIsInData);
                            double[] values = GetCellValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1);

                            // If there's no data in the row (for example if it's hidden), ignore the row
                            if (null == values)
                                continue;

                            // Find the last row
                            int lrow;
                            for (lrow = dataRows - 1; lrow >= 0; lrow--)
                                if (values[lrow] != Constant.MISSING)
                                    break;

                            DoubleVariable variable = new DoubleVariable(lrow + 1, title);
                            frame.Variables.Add(variable);

                            variable.Data = new double[lrow + 1];
                            for (int row = 0; row <= lrow; row++)
                            {
                                double v = values[row];
                                if (Constant.MISSING * 10D == v)
                                    v = Constant.MISSING;
                                variable.Data[row] = v;
                            }
                            variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, gridColumn, cellSelection.ColumnSelections[c].RowIndex, dataRows, mode, titleIsInData);
                        }
                        break;
                    case DataAcquisitionMode.MODE3:
                    case DataAcquisitionMode.CategoryReplaceMissing:
                    case DataAcquisitionMode.CategoryCombineAllColumns:
                    case DataAcquisitionMode.MODE6:
                    case DataAcquisitionMode.Text:
                        {
                            // code text categories as numbers

                            // is the top row unique (i.e. not duplicated in the column data) and long?  Works out min and max lengths as a side-effect
                            bool isUnique = true;
                            bool isShort = false;
                            int minRowCount = int.MaxValue;
                            int maxRowCount = int.MinValue;
                            for (int c = 0; c < cellSelection.TotalColumns; c++)
                            {
                                int rc = cellSelection.ColumnSelections[c].RowCount;
                                if (rc < minRowCount)
                                    minRowCount = rc;
                                if (rc > maxRowCount)
                                    maxRowCount = rc;

                                int gridColumn = cellSelection.ColumnSelections[c].ColumnIndex;
                                string qtitle =
                                    GetCellText(cellSelection.ColumnSelections[c].RowIndex, gridColumn).Trim();
                                if (qtitle.Length < 3)
                                    isShort = true;
                                string[] ColumnArray = GetCellTexts(gridColumn,
                                                                    cellSelection.ColumnSelections[c].RowIndex + 1,
                                                                    cellSelection.ColumnSelections[c].RowIndex +
                                                                    cellSelection.ColumnSelections[c].RowCount - 1);
                                foreach (string candidate in ColumnArray)
                                {
                                    string bufr = (null == candidate) ? "" : candidate.Trim();
                                    if (bufr.Length > 0)
                                    {
                                        if (bufr.Equals(qtitle))
                                        {
                                            isUnique = false;
                                            break;
                                        }
                                    }
                                }
                                // If we have duplicate names, there's no point looking further
                                if (!isUnique)
                                    break;
                            }

                            // Work out the top row of data, depending on whether we think titles are present or not
                            int topRow;
                            if (isRefill)
                                topRow = titleWasInData ? 1 : 0;
                            else if (minRowCount == maxRowCount && minRowCount == rowLengthHint)
                                topRow = 0;
                            else if (minRowCount == maxRowCount && minRowCount == rowLengthHint + 1)
                                topRow = 1;
                            else if (isShort && isUnique)
                            {
                                PointNormal();
                                switch (
                                    SdApplication.SoleInstance.MsgboxX(
                                        "Does the top row of your selection contain titles?",
                                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
                                        "Worksheet Categorical Data Selection", true))
                                {
                                    case DialogResult.Yes:
                                        topRow = 1;
                                        break;
                                    case DialogResult.Cancel:
                                        return null;
                                    default:
                                        topRow = 0;
                                        break;
                                }
                            }
                            else if (isUnique)
                            {
                                topRow = 1;
                            }
                            else
                            {
                                topRow = 0;
                            }

                            // At this point, we know whether we have titles or not.  Now obtain our data strings.
                            string[,] hold = new string[cellSelection.LongestRowCount,cellSelection.TotalColumns];
                            for (int c = 0; c < cellSelection.TotalColumns; c++)
                            {
                                int rx = 0;
                                int gridColumn = cellSelection.ColumnSelections[c].ColumnIndex;
                                // get text not entry because we want formulae translated
                                string[] ColumnArray = GetCellTexts(gridColumn,
                                                                    cellSelection.ColumnSelections[c].RowIndex,
                                                                    cellSelection.ColumnSelections[c].RowIndex +
                                                                    cellSelection.ColumnSelections[c].RowCount - 1);
                                foreach (string t in ColumnArray)
                                {
                                    string bufr = (null == t) ? "" : t.Trim();
                                    hold[rx++, c] = bufr;
                                }
                            }

                            if (DataAcquisitionMode.CategoryReplaceMissing == mode)
                            {
                                // Include missing values
                                // Updated to match DeJagger by PJC July 2008, from code by IEB Sep 2007
                                int lrow = 0;
                                for (int c = 0; c <= hold.GetUpperBound(1); c++)
                                {
                                    int r;
                                    for (r = hold.GetUpperBound(0); r >= topRow; --r)
                                    {
                                        if (hold[r, c].Length > 0 && !Formatting.ASTERISK.Equals(hold[r, c]))
                                            break;
                                    }
                                    if (r > lrow)
                                        lrow = r;
                                }
                                // At this point, lrow is the index of the last row that contains data.
                                for (int c = 0; c <= hold.GetUpperBound(1); c++)
                                {
                                    for (int r = lrow; r >= topRow; --r)
                                    {
                                        if (hold[r, c].Length > 0)
                                        {
                                            if (Formatting.ASTERISK.Equals(hold[r, c]))
                                                hold[r, c] = Formatting.MISSINGLABEL;
                                        }
                                        else
                                        {
                                            hold[r, c] = Formatting.MISSINGLABEL;
                                        }
                                    }
                                }
                            } // of DataAcquisitionMode.CategoryReplaceMissing

                            if (DataAcquisitionMode.Text == mode)
                            {
                                for (int c = 0; c < cellSelection.TotalColumns; c++)
                                {
                                    int totRows = cellSelection.ColumnSelections[c].RowCount;
                                    // Remove any trailing blanks from the selection
                                    while (totRows > topRow && hold[totRows - 1, c].Length == 0)
                                        totRows--;

                                    string title = 0 == topRow
                                                       ? GetGridColumnTitle(
                                                           cellSelection.ColumnSelections[c].ColumnIndex)
                                                       : GetCellText(cellSelection.ColumnSelections[c].RowIndex,
                                                                     cellSelection.ColumnSelections[c].ColumnIndex).
                                                             Trim();
                                    if (totRows > topRow || !string.IsNullOrEmpty(title))
                                    {
                                        StringVariable variable = new StringVariable();
                                        frame.Variables.Add(variable);
                                        variable.EnsureLength(totRows - topRow);
                                        int size = 0;
                                        for (int r = topRow; r < totRows; r++)
                                        {
                                            variable.Data[size++] = hold[r, c];
                                        }

                                        // Fill in column title
                                        variable.Title = title;
                                        variable.Origin = new WorksheetOrigin(
                                            cellSelection.ColumnSelections[c].WorkbookPath,
                                            cellSelection.ColumnSelections[c].WorksheetName,
                                            cellSelection.ColumnSelections[c].ColumnIndex,
                                            cellSelection.ColumnSelections[c].RowIndex,
                                            cellSelection.ColumnSelections[c].RowCount, mode,
                                            topRow > 0);
                                    }
                                }
                            }
                            else if (mode == DataAcquisitionMode.CategoryCombineAllColumns)
                            {
                                // Categories combined across columns; output as row pattern
                                int totRows = cellSelection.LongestRowCount;
                                int totCols = cellSelection.TotalColumns;
                                string[] foundwhat = new string[totRows];
                                int[] nbin = new int[totRows];
                                int found = 0;
                                int size = 0;
                                ClassifierVariable variable = new ClassifierVariable();
                                frame.Variables.Add(variable);
                                for (int r = topRow; r < totRows; r++)
                                {
                                    // Build the pattern for row r
                                    StringBuilder patternBuilder = new StringBuilder();
                                    for (int c = 0; c < totCols; c++)
                                    {
                                        if (null == hold[r, c] || hold[r, c].Length == 0 || "*".Equals(hold[r, c]))
                                        {
                                            // Make the row pattern empty if any data are missing
                                            patternBuilder.Length = 0;
                                            break;
                                        }
                                        // Allow neat string pattern for later bin naming purposes
                                        if (c > 0)
                                            patternBuilder.Append(", ");
                                        patternBuilder.Append(hold[r, c]);
                                    }
                                    string pattern = patternBuilder.ToString();

                                    // Enumerate categories and put results in variable
                                    if (pattern.Length > 0)
                                    {
                                        bool wasFound = false;
                                        for (int i = 0; i < found; i++)
                                        {
                                            if (pattern.Equals(foundwhat[i]))
                                            {
                                                wasFound = true;
                                                size++;
                                                variable.EnsureLength(size);
                                                variable.Data[size - 1] = pattern.Contains(Formatting.MISSINGLABEL) ? Constant.MISSING : i;
                                                nbin[i]++;
                                                break;
                                            }
                                        }
                                        if (!wasFound)
                                        {
                                            nbin[found] = 1;
                                            foundwhat[found] = pattern;
                                            size++;
                                            variable.EnsureLength(size);
                                            variable.Data[size - 1] = pattern.Contains(Formatting.MISSINGLABEL) ? Constant.MISSING : found;
                                            found++;
                                        }
                                    }
                                }

                                // fill in column details
                                for (int i = 0; i < found; i++)
                                {
                                    Group group = new Group(foundwhat[i], i);
                                    group.NBin = nbin[i];
                                    variable.set_Group(i, group);
                                }

                                // column title is a hybrid of all columns
                                StringBuilder titleBuilder = new StringBuilder();
                                for (int c = 0; c < totCols; c++)
                                {
                                    if (c > 0)
                                        titleBuilder.Append(", ");
                                    titleBuilder.Append(0 == topRow
                                                            ? GetGridColumnTitle(
                                                                cellSelection.ColumnSelections[c].ColumnIndex)
                                                            : GetCellText(cellSelection.ColumnSelections[c].RowIndex,
                                                                          cellSelection.ColumnSelections[c].ColumnIndex)
                                                                  .Trim());
                                }
                                variable.Title = titleBuilder.ToString();
                                // TODO: This origin is incorrect; it should include all the columns that were combined, and it doesn't.
                                variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[0].WorkbookPath,
                                                                      cellSelection.ColumnSelections[0].WorksheetName,
                                                                      cellSelection.ColumnSelections[0].ColumnIndex,
                                                                      cellSelection.ColumnSelections[0].RowIndex,
                                                                      cellSelection.ColumnSelections[0].RowCount, mode,
                                                                      topRow > 0);
                            }
                            else
                            {
                                // Modes 3, 4 or 6: categories per column
                                for (int c = 0; c < cellSelection.TotalColumns; c++)
                                {
                                    int totRows = cellSelection.ColumnSelections[c].RowCount;
                                    string[] foundwhat = new string[totRows];
                                    int[] nbin = new int[totRows];
                                    int found = 0;
                                    int size = 0;
                                    ClassifierVariable variable = new ClassifierVariable();
                                    frame.Variables.Add(variable);
                                    for (int r = topRow; r < totRows; r++)
                                    {
                                        string pattern = hold[r, c];
                                        // Ignore missing values
                                        if (string.IsNullOrEmpty(pattern) || "*".Equals(pattern))
                                            continue;

                                        // Enumerate categories and put results in variable
                                        bool wasFound = false;
                                        for (int i = 0; i < found; i++)
                                        {
                                            if (pattern.Equals(foundwhat[i]))
                                            {
                                                wasFound = true;
                                                size++;
                                                variable.EnsureLength(size);
                                                if (pattern.Contains(Formatting.MISSINGLABEL))
                                                    variable.Data[size - 1] = Constant.MISSING;
                                                else
                                                    variable.Data[size - 1] = i;
                                                nbin[i]++;
                                                break;
                                            }
                                        }
                                        if (!wasFound)
                                        {
                                            foundwhat[found] = pattern;
                                            size++;
                                            variable.EnsureLength(size);
                                            if (pattern.Contains(Formatting.MISSINGLABEL))
                                                variable.Data[size - 1] = Constant.MISSING;
                                            else
                                                variable.Data[size - 1] = found;
                                            nbin[found] = 1;
                                            found++;
                                        }
                                    }

                                    // Fill in column details
                                    for (int i = 0; i < found; i++)
                                    {
                                        Group group = new Group(foundwhat[i], i);
                                        group.NBin = nbin[i];
                                        variable.set_Group(i, group);
                                    }

                                    // Fill in column title
                                    variable.Title = 0 == topRow
                                                         ? GetGridColumnTitle(
                                                             cellSelection.ColumnSelections[c].ColumnIndex)
                                                         : GetCellText(cellSelection.ColumnSelections[c].RowIndex,
                                                                       cellSelection.ColumnSelections[c].ColumnIndex).
                                                               Trim();
                                    variable.Origin = new WorksheetOrigin(
                                        cellSelection.ColumnSelections[c].WorkbookPath,
                                        cellSelection.ColumnSelections[c].WorksheetName,
                                        cellSelection.ColumnSelections[c].ColumnIndex,
                                        cellSelection.ColumnSelections[c].RowIndex,
                                        cellSelection.ColumnSelections[c].RowCount, mode, topRow > 0);
                                }
                            }
                        }
                        break;
                    case DataAcquisitionMode.DateReplaceMissing:
                        // Read in the cells replacing missing data with Constant.MISSING
                        for (int c = 0; c < cellSelection.TotalColumns; c++)
                        {
                            DateVariable variable = new DateVariable();
                            frame.Variables.Add(variable);

                            int dataRows;
                            int gridColumn;
                            int gridFirstDataRow;
                            bool titleIsInData;
                            variable.Title = GetColumnTitle(cellSelection.ColumnSelections[c], out dataRows, out gridFirstDataRow, out gridColumn, out titleIsInData);
                            // mrow isn't required, as GetColumnTitle adjusts rowindexes and totrows to skip the title.
                            DateTime[] values = GetCellDateValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1);

                            // Find the last row
                            int lrow;
                            for (lrow = dataRows - 1; lrow >= 0; lrow--)
                                if (values[lrow] != DateTime.MinValue)
                                    break;

                            int r = 0;
                            variable.Data = new DateTime[lrow + 1];
                            for (int row = 0; row <= lrow; row++)
                            {
                                variable.Data[r++] = values[row];
                            }
                            variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, gridColumn, cellSelection.ColumnSelections[c].RowIndex, dataRows, mode, titleIsInData);
                        }
                        break;
                    case DataAcquisitionMode.TextWithFormulae:
                        for (int c = 0; c < cellSelection.TotalColumns; c++)
                        {
                            StringVariable variable = new StringVariable(GetCellFormulae(cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowIndex + cellSelection.ColumnSelections[c].RowCount - 1), null)
                            {Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowCount, mode, false)};
                            frame.Variables.Add(variable);
                        }
                        break;
                    case DataAcquisitionMode.TextNoTitles:
                        for (int c = 0; c < cellSelection.TotalColumns; c++)
                        {
                            StringVariable variable = new StringVariable(GetCellTexts(cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowIndex + cellSelection.ColumnSelections[c].RowCount - 1), null)
                            {
                                Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowCount, mode, false)
                            };
                            frame.Variables.Add(variable);
                        }
                        break;
                    case DataAcquisitionMode.NumericCodingTextToCategories:
                    case DataAcquisitionMode.NumericCodingTextToDummies:
                        for (int c = 0; c < cellSelection.TotalColumns; c++)
                        {
                            int dataRows;
                            int gridColumn;
                            int gridFirstDataRow;
                            bool titleIsInData;
                            string title = GetColumnTitle(cellSelection.ColumnSelections[c], out dataRows, out gridFirstDataRow, out gridColumn, out titleIsInData);
                            double[] numericValues = GetCellValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1);

                            // If there's no data in the row (for example if it's hidden), ignore the row
                            if (null == numericValues)
                                continue;

                            string[] textValues = GetCellTexts(gridColumn, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowIndex + cellSelection.ColumnSelections[c].RowCount - 1);

                            // Find the last row
                            int lastNumericRow;
                            for (lastNumericRow = dataRows - 1; lastNumericRow >= 0; lastNumericRow--)
                                if (numericValues[lastNumericRow] != Constant.MISSING)
                                    break;
                            int lastTextRow;
                            for (lastTextRow = textValues.Length - 1; lastTextRow >= 0; lastTextRow--)
                                if (!string.IsNullOrWhiteSpace(textValues[lastTextRow]))
                                    break;
                            int lastRow = Math.Max(lastNumericRow, lastTextRow);
                            // If there are any numeric missing values where the text is not missing, treat the column as textual and code as a category
                            bool isTextual = false;
                            for (int row = 0; row <= lastRow; row++)
                            {
                                if ((numericValues.Length <= row || numericValues[row] == Constant.MISSING) && !string.IsNullOrWhiteSpace(textValues[row]))
                                {
                                    isTextual = true;
                                    break;
                                }
                            }
                            if (isTextual)
                            {
                                ClassifierVariable variable = new ClassifierVariable();
                                variable.EnsureLength(lastRow + 1);

                                // Code text categories as numbers
                                Dictionary<string, Group> groupsByLabel = new Dictionary<string, Group>();
                                int nextGroupNumber = 0;

                                for (int row = 0; row <= lastRow; row++)
                                {
                                    string pattern = textValues[row];
                                    // Ignore missing values
                                    if (string.IsNullOrEmpty(pattern) || "*".Equals(pattern))
                                        continue;

                                    // Enumerate categories and put results in variable
                                    Group probe;
                                    if (!groupsByLabel.TryGetValue(pattern, out probe))
                                    {
                                        // New group
                                        probe = new Group(pattern, nextGroupNumber++);
                                        groupsByLabel.Add(probe.Label, probe);
                                    }
                                    // By now, we have always found or created the group for this row
                                    probe.NBin++;
                                    variable.Data[row] = probe.Id;
                                }

                                // Fill in groups
                                variable.EnsureGroups(groupsByLabel.Count);
                                foreach (Group group in groupsByLabel.Values)
                                {
                                    variable.set_Group((int)group.Id, group);
                                }

                                // Fill in column title
                                variable.Title = title;
                                variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowCount, mode, titleIsInData);
                                if (mode == DataAcquisitionMode.NumericCodingTextToCategories)
                                {
                                    frame.Variables.Add(variable);
                                }
                                else
                                {
                                    // Dummies
                                    DataFrame dummyFrame = Sheet.ToDummyVariables(SdApplication.SoleInstance, variable);
                                    foreach (Variable v in dummyFrame.Variables)
                                        frame.Variables.Add(v);
                                }
                            }
                            else
                            {
                                DoubleVariable variable = new DoubleVariable(lastRow + 1, title);
                                for (int row = 0; row <= lastRow; row++)
                                {
                                    double v = numericValues[row];
                                    if (Constant.MISSING*10D == v)
                                        v = Constant.MISSING;
                                    variable.Data[row] = v;
                                }
                                variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, gridColumn, cellSelection.ColumnSelections[c].RowIndex, dataRows, mode, titleIsInData);
                                if (mode == DataAcquisitionMode.NumericCodingTextToCategories)
                                {
                                    frame.Variables.Add(variable);
                                }
                                else
                                {
                                    // Dummies?
                                    // Look for 2-12 non-unique integer values: if so, ask if categorical and dummy them
                                    Dictionary<int, Group> groupsByLabel = new Dictionary<int, Group>();
                                    bool allInteger = true;
                                    int nextGroupNumber = 0;
                                    double[] classifierData = new double[variable.Length];
                                    for (int row = 0; row <= lastRow; row++)
                                    {
                                        double v = variable.Data[row];
                                        if (v < int.MinValue || v > int.MaxValue || v != Math.Floor(v))
                                        {
                                            allInteger = false;
                                            break;
                                        }
                                        int pattern = (int)v;
                                        Group probe;
                                        if (!groupsByLabel.TryGetValue(pattern, out probe))
                                        {
                                            // New group
                                            probe = new Group(pattern.ToString(), nextGroupNumber++);
                                            groupsByLabel.Add(pattern, probe);
                                        }
                                        // By now, we have always found or created the group for this row
                                        probe.NBin++;
                                        classifierData[row] = probe.Id;
                                    }
                                    if (!allInteger)
                                    {
                                        // Some non-integer values.  Can't be categorical.  Treat as a single variable.
                                        frame.Variables.Add(variable);
                                    }
                                    else if (groupsByLabel.Count <= 2 || groupsByLabel.Count >= Math.Min(variable.Length, 12))
                                    {
                                        // All distinct, too many groups, or already binary.  Treat as a single variable.
                                        frame.Variables.Add(variable);
                                    }
                                    else
                                    {
                                        ClassifierVariable cv = new ClassifierVariable {Title = variable.Title, Data = classifierData};
                                        cv.EnsureLength(variable.Data.Length);
                                        cv.EnsureGroups(groupsByLabel.Count);
                                        foreach (Group group in groupsByLabel.Values)
                                        {
                                            cv.set_Group((int)group.Id, group);
                                        }
                                        DataFrame dummyFrame;
                                        try
                                        {
                                            dummyFrame = Sheet.ToDummyVariables(SdApplication.SoleInstance, cv);
                                        }
                                        catch (TemplateOperationCancelledException)
                                        {
                                            dummyFrame = null;
                                        }
                                        if (null != dummyFrame)
                                        {
                                            foreach (Variable v in dummyFrame.Variables)
                                                frame.Variables.Add(v);
                                        }
                                        else
                                        {
                                            frame.Variables.Add(variable);
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("mode", mode.ToString(), "Unknown mode");
                } // of switch
                return frame;
            }
            catch (ArithmeticException ex)
            {
                PointNormal();
                SdApplication.SoleInstance.FriendlyError("Internal error reading data from worksheet", ex, false);
                throw; // TODO: What is the correct behaviour here?  Merely returning null causes a infinite loop
            }
        }

        /// <summary>
        /// Return TRUE if all columns are equal length
        /// else returns FALSE (also if user cancels)
        /// </summary>
        /// <param name="requiredRows">If 0, no further requirement.  If non-zero, all rows must be requiredRows in length.</param>
        /// <param name="mode">The way in which the acquired data will be placed into the data structure</param>
        /// <param name="minimumColumns">The smallest acceptable number of columns</param>
        /// <param name="maximumColumns">The largest acceptable number of columns</param>
        /// <param name="selectionMessage">The prompt for the user</param>
        /// <param name="cancelButtonLabel">If non-null, the cancel button will take this label</param>
        /// <param name="allowUserToPivot">If true, the user is asked about grouping by identifier</param>
        /// <param name="width">If wide, always select columns.  If long, always select group IDs and values.  If RespectPivotSetting, use the user's current setting.</param>
        /// <param name="mightBeBatching"></param>
        /// <param name="userCancelled">Output. If true, the user explicitly cancelled the operation; if false, the data is valid or the user selected no data but did not explicitly cancel.</param>
        /// <returns></returns>
        /// <remarks>This version doesn't care about the user pivoting - it will always just go round again.</remarks>
        private DataFrame GetCellEqual(int requiredRows, DataAcquisitionMode mode, int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, DataAcquisitionWidth width, bool mightBeBatching, out bool userCancelled)
        {
            while (true)
            {
                bool wasPivoted;
                DataFrame frame = GetCellEqual(requiredRows, mode, minimumColumns, maximumColumns, selectionMessage, cancelButtonLabel, allowUserToPivot, width, mightBeBatching, out userCancelled, out wasPivoted);
                if (wasPivoted)
                    continue;
                return frame;
            }
        }

        /// <summary>
        /// Return TRUE if all columns are equal length
        /// else returns FALSE (also if user cancels)
        /// </summary>
        /// <param name="requiredRows">If 0, no further requirement.  If non-zero, all rows must be requiredRows in length.</param>
        /// <param name="mode">The way in which the acquired data will be placed into the data structure</param>
        /// <param name="minimumColumns">The smallest acceptable number of columns</param>
        /// <param name="maximumColumns">The largest acceptable number of columns</param>
        /// <param name="selectionMessage">The prompt for the user</param>
        /// <param name="cancelButtonLabel">If non-null, the cancel button will take this label</param>
        /// <param name="allowUserToPivot">If true, the user is asked about grouping by identifier</param>
        /// <param name="width">If wide, always select columns.  If long, always select group IDs and values.  If RespectPivotSetting, use the user's current setting.</param>
        /// <param name="mightBeBatching">If true, we might be selecting data in a batch.  If batching, GetCellEqual should remember where its data came from and should re-select if such memory is present.  If not batching, GetCellEqual should clear memory and not pre-select.</param>
        /// <param name="userCancelled">Output. If true, the user explicitly cancelled the operation; if false, the data is valid or the user selected no data but did not explicitly cancel.</param>
        /// <param name="wasPivoted">If true, the user switched from selecting groups by column to by identifier or vice versa.</param>
        /// <returns></returns>
        private DataFrame GetCellEqual(int requiredRows, DataAcquisitionMode mode, int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, DataAcquisitionWidth width, bool mightBeBatching, out bool userCancelled, out bool wasPivoted)
        {
            while (true)
            {
                bool isLong;
                switch (width)
                {
                    case DataAcquisitionWidth.RespectPivotSetting:
                        isLong = allowUserToPivot && SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier;
                        break;
                    case DataAcquisitionWidth.Wide:
                        isLong = false;
                        break;
                    case DataAcquisitionWidth.Long:
                        isLong = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("width");
                }
                DataFrame frame = isLong
                                      ? Gidx(mode, minimumColumns, maximumColumns, -1, null, out userCancelled, out wasPivoted)
                                      : GetCellArray(requiredRows, mode, minimumColumns, maximumColumns, selectionMessage, cancelButtonLabel, allowUserToPivot, mightBeBatching, out userCancelled, out wasPivoted);
                if (wasPivoted)
                    return null;

                if (null == frame)
                {
                    // User cancelled selection or other issue - either way, userCancelled is already set correctly
                    return null;
                }
                // Find the index of the first non-matching column - if everything matches, c will equal frame.Variables
                int maxRows = frame.MaxRows;
                int c;
                for (c = 0; c < frame.VariableCount; c++)
                    if (frame.Variables[c].Length != maxRows)
                        break;
                bool allEqualLength = c == frame.VariableCount;

                if (0 == requiredRows)
                {
                    if (allEqualLength)
                    {
                        // They're all equal and we have no minimum - safe to return
                        return frame;
                    }
                    // Unequal - ask the user, if they accept then force all the data to maximum length, missing-padded
                    if (SdApplication.SoleInstance.MsgboxX("Warning: unequal length columns. If you select OK then the jagged ends of columns will be padded with missing data.", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, "Worksheet Data Selection", true) == DialogResult.OK)
                    {
                        foreach (Variable v in frame.Variables)
                            v.EnsureLength(maxRows, true);
                        return frame;
                    }
                }
                else if (requiredRows == frame.MinRows && allEqualLength)
                {
                    return frame;
                }
                else
                {
                    // Defined minimum length, and we didn't hit it.
                    string xtra = "";
                    if (mode == DataAcquisitionMode.NumericSkipMissing || mode == DataAcquisitionMode.NumericReplaceMissing)
                    {
                        xtra = "\r\n\r\nThe rows must contain numeric data not text.";
                    }
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "all columns selected must be " + requiredRows.ToString() + " rows long." + xtra, MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Worksheet Data Selection", true);
                }
                ((IGrid)this).ClearSelection();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mode">NumericSkipMissing: Skip missing.  Anything else: include missing (but don't sum)</param>
        /// <param name="minimumColumns">Minimum number of variables</param>
        /// <param name="maximumColumns">Maximum number of variables</param>
        /// <param name="neq"></param>
        /// <param name="lab">Extra label for this variable</param>
        /// <param name="userCancelled"></param>
        /// <param name="wasPivoted"></param>
        /// <returns></returns>
        private DataFrame Gidx(DataAcquisitionMode mode, int minimumColumns, int maximumColumns, int neq, string lab, out bool userCancelled, out bool wasPivoted)
        {
            string labd = "Select DATA";
            string labg = "Select GROUP IDENTIFIERS";
            if (!string.IsNullOrEmpty(lab))
            {
                labd += " for " + lab;
                labg += " for " + lab;
            }

            while (true)
            {
                // call for group ID
                ClearSelection();
                DataFrame groupIdFrame = GetCellArray(0, DataAcquisitionMode.CategoryCombineAllColumns, 1, 20, labg, null, true, false, out userCancelled, out wasPivoted);
                if (userCancelled || wasPivoted)
                    return null;

                ClassifierVariable groupIdVariable = groupIdFrame.Variables[0].AsClassifierVariable;

                if (groupIdVariable.GroupCount < minimumColumns || groupIdVariable.GroupCount > maximumColumns)
                {
                    SelNumWarn(minimumColumns, maximumColumns, groupIdVariable.GroupCount, "StatsDirect Data Selection");
                    continue;
                }

                int mingn = int.MaxValue;
                int maxgn = int.MinValue;
                for (int i = 0; i < groupIdVariable.GroupCount; i++)
                {
                    if (groupIdVariable.Groups[i].NBin > maxgn)
                        maxgn = groupIdVariable.Groups[i].NBin;
                    if (groupIdVariable.Groups[i].NBin < mingn)
                        mingn = groupIdVariable.Groups[i].NBin;
                }

                // call for data
                ClearSelection();
                DataFrame dataFrame = GetCellEqual(groupIdVariable.Length, DataAcquisitionMode.NumericReplaceMissing, 1, 1, labd, null, true, DataAcquisitionWidth.Wide, true, out userCancelled, out wasPivoted);
                if (userCancelled || wasPivoted)
                    return null;

                DoubleVariable dataVariable = dataFrame.Variables[0].AsDoubleVariable;

                DataFrame outputFrame = new DataFrame();
                for (int i = 0; i < groupIdVariable.GroupCount; i++)
                {
                    double thisGroupId = groupIdVariable.Groups[i].Id;
                    DoubleVariable v = new DoubleVariable();
                    outputFrame.Variables.Add(v);
                    v.EnsureLength(maxgn, false);

                    int cnt = 0;
                    if (DataAcquisitionMode.NumericSkipMissing == mode)
                    {
                        // skip missing data
                        for (int j = 0; j < groupIdVariable.Length; j++)
                        {
                            if (groupIdVariable.Data[j] == thisGroupId && dataVariable.Data[j] != Constant.MISSING)
                                v.Data[cnt++] = dataVariable.Data[j];
                        }
                    }
                    else
                    {
                        // include missing data but do not sum them
                        for (int j = 0; j < groupIdVariable.Length; j++)
                        {
                            if (groupIdVariable.Data[j] == thisGroupId)
                                v.Data[cnt++] = dataVariable.Data[j];
                        }
                    }
                    v.TruncateDataToLength(cnt);
                    if (groupIdVariable.GroupCount > 1)
                        v.Title = dataVariable.Title + "_" + groupIdVariable.Title + "_" + groupIdVariable.Groups[i].Label;
                    else
                        v.Title = dataVariable.Title;
                }
                return outputFrame;
            }
        }

/*
        private DataFrame GIDXW(DataAcquisitionMode mode, int min, int max, int neq, string lab, out double[,] wt, ref string wtlab, ref double sumwt, out bool userCancelled)
        {
            const string msg_ti = "StatsDirect Data Selection";
            string labd = "Select DATA";
            string labw = "Select WEIGHTS";
            string labg = "Select GROUP IDENTIFIERS";
            if (!string.IsNullOrEmpty(lab))
            {
                labd += " for " + lab;
                labw += " for " + lab;
                labg += " for " + lab;
            }

            do
            {
                bool proceed = true;

                // call for group ID
                ClearSelection();
                DataFrame groupFrame = GetCellArray(0, DataAcquisitionMode.CategoryCombineAllColumns, 1, 10, labg, null, true, false, out userCancelled);
                if (userCancelled || null == groupFrame)
                {
                    // User cancelled
                    wt = null;
                    return null;
                }
                int rows = groupFrame.MinRows;
                ClassifierVariable groupVariable = groupFrame.Variables[0].AsClassifierVariable;
                int cats = groupVariable.GroupCount;
                string catlab = groupVariable.Title;
                double[] gid = groupVariable.Data;
                string[] gcat = new string[cats];
                int[] gin = new int[cats];
                double[] g = new double[cats];
                int mingn = int.MaxValue;
                int maxgn = int.MinValue;
                for (int i = 0; i < cats; i++)
                {
                    gcat[i] = groupVariable.get_Group(i).Label;
                    gin[i] = groupVariable.get_Group(i).NBin;
                    if (gin[i] > mingn)
                        mingn = gin[i];
                    if (gin[i] < maxgn)
                        maxgn = gin[i];
                    g[i] = i;
                }
                int ng = cats;

                if (ng < min || ng > max)
                {
                    SelNumWarn(min, max, ng, msg_ti);
                    proceed = false;
                }

                EqGpWarn(neq, gin, ng, msg_ti);

                if (proceed)
                {
                    // call for data
                    ClearSelection();
                    DataFrame dataFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 1, 1, labd, null, true, true, out userCancelled);
                    if (userCancelled || null == dataFrame)
                    {
                        // User cancelled
                        wt = null;
                        return null;
                    }
                    DoubleVariable dataVariable = dataFrame.Variables[0].AsDoubleVariable;
                    string datlab = dataVariable.Title;
                    double[] dt = dataVariable.Data;

                    // call for weights
                    ClearSelection();
                    DataFrame weightFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 1, 1, labw, null, true, false, out userCancelled);
                    if (userCancelled || null == weightFrame)
                    {
                        // User cancelled
                        wt = null;
                        return null;
                    }
                    DoubleVariable weightVariable = weightFrame.Variables[0].AsDoubleVariable;
                    wtlab = weightVariable.Title;
                    double[] w = weightVariable.Data;

                    double[,] ARR2 = new double[ng, maxgn];
                    ColumnData[] CDAT1 = new ColumnData[ng];
                    wt = new double[ng, maxgn];
                    for (int i = 0; i < ng; i++)
                    {
                        for (int j = 0; j < maxgn; j++)
                        {
                            ARR2[i, j] = Constant.MISSING;
                            wt[i, j] = Constant.MISSING;
                        }
                    }
                    sumwt = 0;
                    for (int i = 0; i < ng; i++)
                    {
                        int cnt = 0;
                        CDAT1[i].Sum = 0;
                        if (DataAcquisitionMode.NumericSkipMissing == mode)
                        {
                            // skip missing data
                            for (int j = 0; j < rows; j++)
                            {
                                if (gid[j] == g[i] && dt[j] != Constant.MISSING && w[j] != Constant.MISSING)
                                {
                                    ARR2[i, cnt] = dt[j];
                                    wt[i, cnt] = w[j];
                                    CDAT1[i].Sum += dt[j];
                                    sumwt += w[j];
                                    cnt++;
                                }
                            }
                        }
                        else
                        {
                            // include missing data but do not sum them
                            for (int j = 0; j < rows; j++)
                            {
                                if (gid[j] == g[i])
                                {
                                    ARR2[i, cnt] = dt[j];
                                    wt[i, cnt] = w[j];
                                    if (dt[j] != Constant.MISSING)
                                        CDAT1[i].Sum += dt[j];
                                    if (w[j] != Constant.MISSING)
                                        sumwt += w[j];
                                    cnt++;
                                }
                            }
                        }
                        CDAT1[i].Rows = cnt;
                        if (ng > 1)
                        {
                            CDAT1[i].Title = datlab + "_" + catlab + "_" + gcat[i];
                        }
                        else
                        {
                            CDAT1[i].Title = datlab;
                        }
                    }
                    if (sumwt != 0)
                        sumwt = rows / sumwt;
                    DataFrame outputFrame = null;
                    // Complete me
                    return outputFrame;
                }
            } while (true);
        }
*/


        /// <summary>
        /// Ensure the cursor is reset to normal from whatever it may presently be.
        /// </summary>
        private static void PointNormal()
        {
            if (Application.UseWaitCursor)
                Application.UseWaitCursor = false;
        }

        private static void SelNumWarn(int Min, int Max, int totcols, string msg_ti)
        {
            PointNormal();
            if (Min == Max)
            {
                SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "you must select " + Min.ToString() + " column" + (Min > 1 ? "s" : "") + " but you selected " + totcols.ToString() + ".", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
            }
            else
            {
                if (totcols < Min)
                {
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "you must select " + Min.ToString() + " column" + (Min > 1 ? "s" : "") + " or more but you selected " + totcols.ToString() + ".", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
                }
                else if (totcols > Max)
                {
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "you must select " + Max.ToString() + " column" + (Max > 1 ? "s" : "") + " or fewer but you selected " + totcols.ToString() + ".", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
                }
            }
        }

        /// <summary>
        /// Find and return the column title.
        /// Amends ccs.RowIndex and totrows to step past a column title if found.
        /// </summary>
        /// <param name="ccs"></param>
        /// <param name="dataRows">Filled in with the total number of DATA rows.  This may not be the same as the number of non-title rows, as there may be blanks between the titles and the data.</param>
        /// <param name="gridFirstDataRow">Filled in with the grid row index of the first non-title, non-missing data row.</param>
        /// <param name="gridColumn">Filled in with the grid column index of the column.</param>
        /// <param name="titleIsInData">true iff the title has been found within the data; false if the title is auto-generated.</param>
        /// <returns></returns>
        private string GetColumnTitle(CellColumnSelection ccs, out int dataRows, out int gridFirstDataRow, out int gridColumn, out bool titleIsInData)
        {
            string candidateTitle = string.Empty;
            titleIsInData = false;
            gridColumn = ccs.ColumnIndex;
            dataRows = ccs.RowCount;
            int firstGridRow = ccs.RowIndex;
            if (GetCellValue(firstGridRow, gridColumn) == Constant.MISSING)
            {
                // Could be text, i.e. a title - let's find out
                candidateTitle = GetCellText(firstGridRow, gridColumn).Trim();
                if (candidateTitle.Length > 0)
                {
                    firstGridRow++;
                    ccs.RowIndex = firstGridRow;
                    dataRows--;
                }
                else
                {
                    // It's not numeric or text - look back up to two rows to see whether there's a title in there.
                    for (int r = firstGridRow - 1; r >= Math.Max(0, firstGridRow - 2); r--)
                    {
                        if (GetCellValue(r, gridColumn) == Constant.MISSING)
                        {
                            candidateTitle = GetCellText(r, gridColumn).Trim();
                            if (candidateTitle.Length > 0)
                            {
                                gridFirstDataRow = firstGridRow;
                                titleIsInData = true;
                                return candidateTitle;
                            }
                        }
                    }
                }
            }
            else
            {
                // First row is numeric.  Look back up to two rows to see whether there's a title in there.
                for (int r = firstGridRow - 1; r >= Math.Max(0, firstGridRow - 2); r--)
                {
                    if (GetCellValue(r, gridColumn) == Constant.MISSING)
                    {
                        candidateTitle = GetCellText(r, gridColumn).Trim();
                        if (candidateTitle.Length > 0)
                        {
                            gridFirstDataRow = firstGridRow;
                            titleIsInData = true;
                            return candidateTitle;
                        }
                    }
                }
            }

            // If the data block is half way down the column, or otherwise has no title, then grab row 0 entry i.e. a,b,c etc.
            if (candidateTitle.Length == 0)
            {
                // titleIsInData = false; - always true if we get here
                candidateTitle = GetGridColumnTitle(gridColumn).Trim();
            }

            // Find the first data row - anything other than MISSING is fair game
            int skippedRows = 0;
            for (gridFirstDataRow = firstGridRow; gridFirstDataRow < firstGridRow + dataRows; gridFirstDataRow++)
            {
                if (GetCellValue(gridFirstDataRow, gridColumn) != Constant.MISSING)
                    break;
                skippedRows++;
            }
            dataRows -= skippedRows;
            return candidateTitle;
        }

        /// <summary>
        /// The guts of the data conversion to a double.
        /// This will be inlined in optimised code.
        /// </summary>
        /// <param name="val">The value to be converted</param>
        /// <returns>MISSING if the value could not be converted, MISSING * 10 if the value was previously MISSING, or the converted value</returns>
        public static double ToCellValue(object val)
        {
            if (null == val)
                return Constant.MISSING;
            if (val is double)
                return (double)val;
            if (val is Int32)
                return (int)val;
            if (val is string)
            {
                string buf = (string)val;
                double dval;
                if (Double.TryParse(buf, out dval))
                    return dval;
                string ubuf = buf.ToUpper();
                if (ubuf == "*" || ubuf == "MISSING" || ubuf == ".")
                    return Constant.MISSING * 10D;
                // if (ubuf == "#NULL!" || ubuf == "#NUM!")
                //    return Constant.MISSING;
                return Constant.MISSING; // If we can't parse it as a number, and we want numbers, it's MISSING.
            }
            // throw new NotImplementedException("Don't know how to handle type " + val.GetType().FullName);
            return Constant.MISSING;
        }

        /// <summary>
        /// The guts of the data conversion to a DateTime.
        /// This will be inlined in optimised code.
        /// </summary>
        /// <param name="val">The value to be converted</param>
        /// <returns>DateTime.MinValue if the value could not be converted, or the converted value</returns>
        public static DateTime ToCellDateValue(object val)
        {
            if (null == val)
                return DateTime.MinValue;
            if (val is DateTime)
                return (DateTime)val;
            if (val is double)
                return DateTime.FromOADate((double)val);
            if (val is Int32)
                return DateTime.FromOADate((int)val);
            if (val is string)
            {
                DateTime dt;
                return DateTime.TryParse((string)val, out dt) ? dt : DateTime.MinValue;
            }
            // throw new NotImplementedException("Don't know how to handle type " + val.GetType().FullName);
            return DateTime.MinValue;
        }

        /// <summary>
        /// Get the single cell value at the specified row and column.
        /// </summary>
        /// <param name="Row"></param>
        /// <param name="Column"></param>
        /// <returns>MISSING if the value could not be converted, MISSING * 10 if the value was previously MISSING, or the converted value</returns>
        double GetCellValue(int Row, int Column)
        {
            workbookView.GetLock();
            try
            {
                object val = workbookView.ActiveWorksheet.Cells[Row, Column].Value;
                return ToCellValue(val);
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        string GetCellText(int Row, int Column)
        {
            workbookView.GetLock();
            try
            {
                object val = workbookView.ActiveWorksheet.Cells[Row, Column].Value;
                return null == val ? "" : val.ToString();
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        /// <summary>
        /// Obtain a raw array of objects from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        object[,] GetCellObjects(int column, int firstRow, int lastRow)
        {
            if (lastRow < firstRow)
                return new object[0, 0];
            workbookView.GetLock();
            try
            {
                object val = workbookView.ActiveWorksheet.Cells[firstRow, column, lastRow, column].Value;
                if (null != val && val.GetType().IsArray)
                {
                    return (object[,])val;
                }
                return new[,] { { val } };
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        /// <summary>
        /// Obtain a raw array of formulae from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="Column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="FirstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="LastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        string[] GetCellFormulae(int Column, int FirstRow, int LastRow)
        {
            string[] result = new string[LastRow - FirstRow + 1];
            workbookView.GetLock();
            try
            {
                for (int row = FirstRow; row <= LastRow; row++)
                {
                    string formula = workbookView.ActiveWorksheet.Cells[row, Column].Formula;
                    result[row - FirstRow] = formula;
                }
                return result;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        double[] GetCellValues(int column, int firstRow, int lastRow)
        {
            if (IsHiddenColumn(column))
                return null;

            object[,] values = GetCellObjects(column, firstRow, lastRow);

            double[] returnValues = new double[values.GetUpperBound(0) + 1];
            for (int r = values.GetLowerBound(0); r <= values.GetUpperBound(0); r++)
                returnValues[r] = ToCellValue(values[r, 0]);
            return returnValues;
        }

        private bool IsHiddenColumn(int column)
        {
            workbookView.GetLock();
            bool isHidden = workbookView.ActiveWorksheet.Cells[0, column].EntireColumn.Hidden;
            workbookView.ReleaseLock();
            return isHidden;
        }

        DateTime[] GetCellDateValues(int column, int firstRow, int lastRow)
        {
            object[,] values = GetCellObjects(column, firstRow, lastRow);

            DateTime[] returnValues = new DateTime[values.GetUpperBound(0) + 1];
            for (int r = values.GetLowerBound(0); r <= values.GetUpperBound(0); r++)
                returnValues[r] = ToCellDateValue(values[r, 0]);
            return returnValues;
        }

        /// <summary>
        /// Obtain a raw array of display strings from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="Column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="FirstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="LastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        string[] GetCellTexts(int Column, int FirstRow, int LastRow)
        {
            workbookView.GetLock();
            try
            {
                string[] returnedValues = new string[LastRow - FirstRow + 1];
                for (int i = 0; i <= LastRow - FirstRow; i++)
                {
                    returnedValues[i] = workbookView.ActiveWorksheet.Cells[FirstRow + i, Column].Text;
                }
                return returnedValues;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        /// <summary>
        /// Fake a column name for this column: A for 0, B for 1, AA for 26, BA for 52 etc.
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        private static string GetGridColumnTitle(int col)
        {
            const int A = 65;
            int prefixValue = (col / 26) - 1;
            int suffixValue = (col % 26);
            if (prefixValue < 0)
                return new string(new[] { (char)(suffixValue + A) });
            return new string(new[] { (char)(prefixValue + A), (char)(suffixValue + A) });
        }

        public override bool ImplementsIGrid
        {
            get { return true; }
        }

        private bool Gidxyr(out double[,] x, ref double[, ,] y, ref int ng, ref int maxgn, ref int nrep, ref ColumnData[] cd, ref string xlab, ref MinMax minMax)
        {
            const string msgTi = "StatsDirect Data Selection";
            const string labd = "Select DATA";

            const int min = 2;
            const int max = 200;

            bool wasPivoted = true;
            while (wasPivoted)
            {
                // call for group ID
                ((IGrid)this).ClearSelection();
                bool cancelled;
                DataFrame groupIdentifiers = GetCellArray(0, DataAcquisitionMode.CategoryCombineAllColumns, 1, 10, "Select GROUP/SERIES IDENTIFIERS", null, true, false, out cancelled, out wasPivoted);
                if (cancelled)
                    break;
                if (wasPivoted)
                    continue;

                minMax = new MinMax();
                ClassifierVariable groupIdentifierVariable = groupIdentifiers.Variables[0].AsClassifierVariable;
                int rows = groupIdentifierVariable.Length;
                int cats = groupIdentifierVariable.GroupCount;
                string catlab = groupIdentifierVariable.Title;
                double[] gid = new double[rows + 1];
                string[] gcat = new string[cats + 1];
                for (int i = 1; i <= rows; i++)
                {
                    gid[i] = groupIdentifierVariable.Data[i - 1];
                }
                for (int i = 1; i <= cats; i++)
                {
                    gcat[i] = groupIdentifierVariable.Groups[i - 1].Label;
                }

                // find number of categories
                ng = 1;
                double[] g = new double[rows + 1];
                int[] gin = new int[rows + 1];
                for (int j = 1; j <= rows; j++)
                {
                    if (gid[j] != Constant.MISSING)
                    {
                        g[1] = gid[j];
                        gin[1] = 1;
                        break;
                    }
                }
                for (int j = 2; j <= rows; j++)
                {
                    bool newa = true;
                    if (gid[j] == Constant.MISSING)
                    {
                        newa = false;
                    }
                    else
                    {
                        for (int i = 1; i <= ng; i++)
                        {
                            if (gid[j] == g[i])
                            {
                                newa = false;
                                gin[i]++;
                                break;
                            }
                        }
                    }
                    if (newa)
                    {
                        ng++;
                        g[ng] = gid[j];
                        gin[ng] = 1;
                    }
                }

                if (ng < min || ng > max)
                {
                    SelNumWarn(min, max, ng, msgTi);
                    continue;
                }

                maxgn = int.MinValue;
                for (int i = 1; i <= ng; i++)
                {
                    if (gin[i] > maxgn)
                        maxgn = gin[i];
                }

                // Call for y data
                minMax.MinY = double.MaxValue;
                minMax.MaxY = double.MinValue;
                ((IGrid)this).ClearSelection();
                DataFrame replicatesFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 1, 200, labd + " for Y (VERTICAL AXIS) REPLICATES", null, true, DataAcquisitionWidth.Wide, false, out cancelled, out wasPivoted);
                if (cancelled || wasPivoted)
                    break;

                nrep = replicatesFrame.VariableCount;
                y = new double[ng + 1, maxgn + 1, nrep + 1];
                for (int k = 1; k <= nrep; k++)
                {
                    DoubleVariable v = replicatesFrame.Variables[k - 1].AsDoubleVariable;
                    for (int i = 1; i <= ng; i++)
                    {
                        int cnt = 0;
                        for (int j = 1; j <= rows; j++)
                        {
                            if (gid[j] == g[i])
                            {
                                cnt++;
                                double yz = v.Data[j - 1];
                                y[i, cnt, k] = yz;
                                if (yz != Constant.MISSING)
                                {
                                    if (yz > minMax.MaxY)
                                        minMax.MaxY = yz;
                                    if (yz < minMax.MinY)
                                        minMax.MinY = yz;
                                }
                            }
                        }
                    }
                }

                // Call for x data
                minMax.MinX = double.MaxValue;
                minMax.MaxX = double.MinValue;
                ((IGrid)this).ClearSelection();
                DataFrame xFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 1, 1, labd + " for X (HORIZONTAL AXIS)", null, true, DataAcquisitionWidth.Wide, false, out cancelled, out wasPivoted);
                if (cancelled || wasPivoted)
                    break;
                DoubleVariable xVariable = xFrame.Variables[0].AsDoubleVariable;
                xlab = xVariable.Title;
                x = new double[ng + 1, maxgn + 1];
                cd = new ColumnData[ng + 1];
                for (int i = 1; i <= ng; i++)
                {
                    int cnt = 0;
                    cd[i] = new ColumnData { Sum = 0 };
                    for (int j = 1; j <= rows; j++)
                    {
                        if (gid[j] == g[i])
                        {
                            cnt++;
                            double xz = xVariable.Data[j - 1];
                            x[i, cnt] = xz;
                            if (xz != Constant.MISSING)
                            {
                                cd[i].Sum += xz;
                                if (xz > minMax.MaxX)
                                    minMax.MaxX = xz;
                                if (xz < minMax.MinX)
                                    minMax.MinX = xz;
                            }
                        }
                    }
                    cd[i].Rows = cnt;
                    if (ng > 1)
                    {
                        cd[i].Title = catlab + "_" + gcat[i];
                    }
                    else
                    {
                        cd[i].Title = catlab;
                    }
                }
                return true;
            }

            // If we get here, the user cancelled
            x = null;
            return false;
        }

        public GroupedCovarianceData FillGroupedCovarianceParameter()
        {
            double[, ,] y = null;
            int k = 0;
            int maxr = 0;
            int maxreps = 0;
            ColumnData[] cx = null;
            string xlab = "";
            MinMax minMax = null;

            ((IGrid)this).ClearSelection();
            while (true)
            {
                double GAMMA;
                double[,] xt;
                if (SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier)
                {
                    bool ok = Gidxyr(out xt, ref y, ref k, ref maxr, ref maxreps, ref cx, ref xlab, ref minMax);
                    if (ok)
                    {
                        double[] b = new double[k + 1];
                        double[] a = new double[k + 1];
                        string[] bnam = new string[k + 1];
                        double[] rssx = new double[k + 1];
                        double[] xmean = new double[k + 1];
                        double[] ymean = new double[k + 1];
                        int[] nxi = new int[k + 1];
                        int[,] ny = new int[k + 1, maxr + 1];
                        for (int g = 1; g <= k; g++)
                        {
                            nxi[g] = cx[g].Rows;
                            for (int i = 1; i <= nxi[g]; i++)
                            {
                                ny[g, i] = maxreps;
                            }
                        }
                        ITemplateHost host = SdApplication.SoleInstance;
                        ConfidenceIntervalParameter ciParam = new ConfidenceIntervalParameter { CanDefault = true, Name = "ci" };
                        ParameterBag filledCi = host.FillParameter(new TemplateProcessor(host), ciParam, new ParameterBag(), false);
                        GAMMA = filledCi["ci"].AsDouble;

                        // If we get here, the operation acquired all its parameters successfully
                        GroupedCovarianceData gcd = new GroupedCovarianceData
                        {
                            a = a,
                            b = b,
                            bnam = bnam,
                            cx = cx,
                            GAMMA = GAMMA,
                            k = k,
                            maxr = maxr,
                            maxreps = maxreps,
                            minMax = minMax,
                            nxi = nxi,
                            ny = ny,
                            rssx = rssx,
                            xlab = xlab,
                            xmean = xmean,
                            xt = xt,
                            y = y,
                            ymean = ymean
                        };
                        return gcd;
                    }
                }
                else
                {
                    minMax = new MinMax
                    {
                        MinX = Double.MaxValue,
                        MaxX = Double.MinValue,
                        MinY = Double.MaxValue,
                        MaxY = Double.MinValue
                    };
                    bool cancelled;
                    bool wasPivoted;
                    DataFrame predictorsFrame = GetCellArray(0, DataAcquisitionMode.NumericSkipMissing, 2, 200, "Select data for PREDICTOR (x axis) SERIES", null, true, false, out cancelled, out wasPivoted);
                    if (cancelled)
                        break;
                    if (wasPivoted)
                        continue;

                    k = predictorsFrame.VariableCount;
                    // Store the X Data temporarily
                    maxr = predictorsFrame.MaxRows;
                    xt = new double[k + 1, maxr + 1];
                    cx = new ColumnData[k + 1];
                    for (int c = 1; c <= k; c++)
                    {
                        DoubleVariable v = predictorsFrame.Variables[c - 1].AsDoubleVariable;
                        cx[c] = new ColumnData { Title = v.Title, Rows = v.Length, Sum = v.Sum };
                        xlab += v.Title + " ";
                        for (int r = 1; r <= cx[c].Rows; r++)
                        {
                            xt[c, r] = v.Data[r - 1];
                            if (xt[c, r] != Constant.MISSING)
                            {
                                if (xt[c, r] > minMax.MaxX)
                                    minMax.MaxX = xt[c, r];
                                if (xt[c, r] < minMax.MinX)
                                    minMax.MinX = xt[c, r];
                            }
                        }
                    }
                    if (xlab.Length > 70)
                        xlab = xlab.Substring(0, 70);
                    OptionDescriptor descriptor = new OptionDescriptor { Title = "Grouped linear covariance" };
                    CheckBoxDescriptor useYReplicatesDescriptor = new CheckBoxDescriptor { Text = "Use Y replicates" };
                    descriptor.CheckBoxes.Add(useYReplicatesDescriptor);
                    SelectionBoxDescriptor ciDescriptor = null;
                    if (!SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
                    {
                        ciDescriptor = new SelectionBoxDescriptor { Title = "Confidence (%)" };
                        ciDescriptor.SetAsConfidence();
                        descriptor.SelectionBoxes.Add(ciDescriptor);
                    }
                    if (null == SdApplication.SoleInstance.DisplayOptions(descriptor))
                        break;

                    bool yrep = useYReplicatesDescriptor.Checked;
                    if (!SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval && null != ciDescriptor)
                    {
                        GAMMA = Parsing.Cdbl_Txt(ciDescriptor.Value) / 100.0;
                    }
                    else
                    {
                        GAMMA = SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval;
                    }
                    double[] b = new double[k + 1];
                    double[] a = new double[k + 1];
                    string[] bnam = new string[k + 1];
                    double[] rssx = new double[k + 1];
                    double[] xmean = new double[k + 1];
                    double[] ymean = new double[k + 1];
                    int[] nxi = new int[k + 1];
                    int[,] ny = new int[k + 1, maxr + 1];
                    y = new double[k + 1, maxr + 1, 2];
                    for (int g = 1; g <= k; g++)
                    {
                        int nx = cx[g].Rows;
                        nxi[g] = nx;
                        if (yrep)
                        {
                            // Get Y replicates
                            ((IGrid)this).ClearSelection();
                            DataFrame replicatesFrame = GetCellArray(0, DataAcquisitionMode.NumericSkipMissing, nx, nx, "Select Data for OUTCOME (Y axis) REPLICATES for x SERIES " + g.ToString() + " LEVELS", null, false, false, out cancelled, out wasPivoted);
                            if (cancelled || wasPivoted)
                                break; // This data selection cancelled, try again!

                            if (replicatesFrame.MaxRows > maxreps)
                            {
                                maxreps = replicatesFrame.MaxRows;
                                double[, ,] yNew = new double[k + 1, maxr + 1, maxreps + 1];
                                for (int i0 = 0; i0 <= y.GetUpperBound(0); i0++)
                                    for (int i1 = 0; i1 <= y.GetUpperBound(1); i1++)
                                        for (int i2 = 0; i2 <= y.GetUpperBound(2); i2++)
                                            yNew[i0, i1, i2] = y[i0, i1, i2];
                                y = yNew;
                            }
                            for (int j = 1; j <= nx; j++)
                            {
                                DoubleVariable v = replicatesFrame.Variables[j - 1].AsDoubleVariable;
                                ny[g, j] = v.Length;
                                for (int j2 = 1; j2 <= ny[g, j]; j2++)
                                {
                                    y[g, j, j2] = v.Data[j2 - 1];
                                    if (y[g, j, j2] != Constant.MISSING)
                                    {
                                        if (y[g, j, j2] > minMax.MaxY)
                                            minMax.MaxY = y[g, j, j2];
                                        if (y[g, j, j2] < minMax.MinY)
                                            minMax.MinY = y[g, j, j2];
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Get single column values for y
                            double[, ,] yNew = new double[k + 1, maxr + 1, 2];
                            for (int i0 = 0; i0 <= y.GetUpperBound(0); i0++)
                                for (int i1 = 0; i1 <= y.GetUpperBound(1); i1++)
                                    for (int i2 = 0; i2 <= y.GetUpperBound(2); i2++)
                                        yNew[i0, i1, i2] = y[i0, i1, i2];
                            y = yNew;
                            ((IGrid)this).ClearSelection();
                            DataFrame outcomeFrame = GetCellEqual(nx, DataAcquisitionMode.NumericSkipMissing, 1, 1, "Select Data for OUTCOME (Y) for PREDICTOR " + g + " {" + cx[g].Title.Substring(0, Math.Min(20, cx[g].Title.Length)) + "}", null, false, DataAcquisitionWidth.Wide, false, out cancelled, out wasPivoted);
                            if (cancelled || wasPivoted)
                                break; // Failed selection, go round again
                            DoubleVariable outcomeVariable = outcomeFrame.Variables[0].AsDoubleVariable;
                            for (int j = 1; j <= nx; j++)
                            {
                                ny[g, j] = 1;
                                y[g, j, 1] = outcomeVariable.Data[j - 1];
                                if (y[g, j, 1] != Constant.MISSING)
                                {
                                    if (y[g, j, 1] > minMax.MaxY)
                                        minMax.MaxY = y[g, j, 1];
                                    if (y[g, j, 1] < minMax.MinY)
                                        minMax.MinY = y[g, j, 1];
                                }
                            }
                        }
                    }

                    // If we get here, the operation acquired all its parameters successfully
                    GroupedCovarianceData gcd = new GroupedCovarianceData
                                                             {
                                                                 a = a,
                                                                 b = b,
                                                                 bnam = bnam,
                                                                 cx = cx,
                                                                 GAMMA = GAMMA,
                                                                 k = k,
                                                                 maxr = maxr,
                                                                 maxreps = maxreps,
                                                                 minMax = minMax,
                                                                 nxi = nxi,
                                                                 ny = ny,
                                                                 rssx = rssx,
                                                                 xlab = xlab,
                                                                 xmean = xmean,
                                                                 xt = xt,
                                                                 y = y,
                                                                 ymean = ymean
                                                             };
                    return gcd;
                }
            }
            // If we get here, the user cancelled the operation
            throw new TemplateOperationCancelledException();
        }


        private DataFrame2D Gidx3(int min, int max, int neq, string subGroupSelectionLabel, DataAcquisitionMode2D mode, out bool wasPivoted)
        {
            if (null == subGroupSelectionLabel)
                subGroupSelectionLabel = "Select SUB-GROUP IDENTIFIER";
            ((IGrid)this).ClearSelection();

            bool userCancelled;
            DataFrame groupFrame = GetCellArray(0, DataAcquisitionMode.MODE3, 1, 1, "Select GROUP IDENTIFIER", null, true, false, out userCancelled, out wasPivoted);
            if (userCancelled)
                throw new TemplateOperationCancelledException();
            if (wasPivoted)
                return null;

            ClassifierVariable groupVariable = groupFrame.Variables[0].AsClassifierVariable;
            int rows = groupVariable.Length;
            int cats = groupVariable.GroupCount;
            string catlab = groupVariable.Title;
            double[] gid = new double[rows + 1];
            double[] sgid = new double[rows + 1];
            double[] dt = new double[rows + 1];
            string[] gcat = new string[cats + 1];
            double[] g = new double[cats + 1];
            int[] gin = new int[cats + 1];

            for (int i = 1; i <= rows; i++)
            {
                gid[i] = groupVariable.Data[i - 1];
            }

            for (int i = 1; i <= cats; i++)
            {
                gcat[i] = groupVariable.Groups[i - 1].Label;
                gin[i] = groupVariable.Groups[i - 1].NBin;
                g[i] = (double)i - 1;
            }
            int numberOfGroups = cats;
            bool proceed = true;
            if (numberOfGroups < min || numberOfGroups > max)
            {
                SelNumWarn(min, max, numberOfGroups, "StatsDirect Data Selection");
                proceed = false;
            }
            int mingn = int.MaxValue;
            int maxgn = int.MinValue;
            for (int i = 1; i <= numberOfGroups; i++)
            {
                if (gin[i] > maxgn) maxgn = gin[i];
                if (gin[i] < mingn) mingn = gin[i];
            }

            proceed &= EqGpWarn(neq, gin, numberOfGroups, "StatsDirect Data Selection");
            if (!proceed)
                return null;

            ((IGrid)this).ClearSelection();

            DataFrame subGroupFrame = GetCellEqual(rows, DataAcquisitionMode.MODE3, 1, 1, subGroupSelectionLabel, null, true, DataAcquisitionWidth.Wide, false, out userCancelled);
            if (userCancelled)
                throw new TemplateOperationCancelledException();
            ClassifierVariable subGroupVariable = subGroupFrame.Variables[0].AsClassifierVariable;

            int scats = subGroupVariable.GroupCount;
            string scatlab = subGroupVariable.Title;
            string[] sgcat = new string[scats + 1];
            double[] sg = new double[scats + 1];
            int[] sgin = new int[scats + 1];

            for (int i = 1; i <= rows; i++)
            {
                sgid[i] = subGroupVariable.Data[i - 1];
            }
            for (int i = 1; i <= scats; i++)
            {
                sgcat[i] = subGroupVariable.Groups[i - 1].Label;
                sgin[i] = subGroupVariable.Groups[i - 1].NBin;
                sg[i] = (double)i - 1;
            }
            int numberOfSubGroups = scats;
            int minsgn = int.MaxValue;
            int maxsgn = int.MinValue;
            for (int i = 1; i <= numberOfSubGroups; i++)
            {
                if (sgin[i] > maxsgn) maxsgn = sgin[i];
                if (sgin[i] < minsgn) minsgn = sgin[i];
            }

            ((IGrid)this).ClearSelection();
            DataFrame dataFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 1, 1, "Select DATA column", null, true, DataAcquisitionWidth.Wide, false, out userCancelled);
            if (userCancelled)
                throw new TemplateOperationCancelledException();
            DoubleVariable dataVariable = dataFrame.Variables[0].AsDoubleVariable;

            string dlab = dataVariable.Title;
            for (int i = 1; i <= rows; i++)
            {
                dt[i] = dataVariable.Data[i - 1];
            }

            if (DataAcquisitionMode2D.Mode1 == mode)
            {
                DataFrame2D resultFrame = new DataFrame2D();
                for (int groupNumber = 1; groupNumber <= numberOfGroups; groupNumber++)
                {
                    for (int subGroupNumber = 1; subGroupNumber <= numberOfSubGroups; subGroupNumber++)
                    {
                        int cnt = 0;
                        double[] data = new double[maxgn];
                        for (int j = 1; j <= rows; j++)
                        {
                            if (gid[j] == g[groupNumber] && sgid[j] == sg[subGroupNumber])
                            {
                                data[cnt++] = dt[j];
                            }
                        }
                        // Skip entering variables with no members - typically found at the end of jagged sources
                        if (cnt > 0)
                        {
                            string title = catlab + "_" + gcat[groupNumber] + " (" + scatlab + "_" + sgcat[subGroupNumber] + ")";
                            Variable variable = new DoubleVariable(data, title);
                            variable.TruncateDataToLength(cnt);
                            resultFrame.EnsureVariablesJagged(groupNumber, subGroupNumber);
                            resultFrame.Variables[groupNumber - 1][subGroupNumber - 1] = variable;
                        }
                    }
                }
                return resultFrame;
            }
            else
            {
                DataFrame2D resultFrame = new DataFrame2D();
                resultFrame.EnsureVariablesSquare(maxgn, numberOfGroups);

                int highestCnt = 0;
                for (int i = 1; i <= numberOfGroups; i++)
                {
                    for (int k = 1; k <= numberOfSubGroups; k++)
                    {
                        int cnt = 0;
                        for (int j = 1; j <= rows; j++)
                        {
                            if (gid[j] == g[i] && sgid[j] == sg[k])
                            {
                                if (null == resultFrame.Variables[cnt][i - 1])
                                    resultFrame.Variables[cnt][i - 1] = new DoubleVariable();
                                DoubleVariable variable = resultFrame.Variables[cnt][i - 1].AsDoubleVariable;
                                variable.EnsureLength(numberOfSubGroups, Constant.MISSING);
                                variable.Data[k - 1] = dt[j];
                                cnt++;
                            }
                        }
                        // ADAT1[i - 1].Cols += gotsg;
                        // Titles are not used in anything that calls this
                        // CDAT2[i - 1, k - 1].Title = catlab + "_" + gcat[(int)g[i]] + " (" + scatlab + "_" + sgcat[(int)sg[k]] + ")";
                        // CDAT2[0, 0].Rows = nsg;
                        highestCnt = Math.Max(highestCnt, cnt);
                    }
                    // ADAT1[i - 1].Cols--;
                }
                resultFrame.TruncateToLength(highestCnt);
                resultFrame.Name = " " + dlab + " (data), " + catlab + " (group), " + scatlab + " (sub-group)";
                return resultFrame;
            }
        }

        private static bool EqGpWarn(int neq, int[] gin, int ng, string msg_ti)
        {
            if (0 == neq)
                return true;

            if (-1 == neq)
                neq = gin[1];

            for (int i = 0; i < ng; i++)
            {
                if (gin[i] != neq)
                {
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "all groups must be the same size.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
                    return false;
                }
            }
            return true;
        }

        internal DataFrame2D FillGridParameter2D(Parameter parameter, ITemplateProcessor processor, SdApplication sDApplication, ParameterBag parameters)
        {
            GridParameter2D gridParameter = (GridParameter2D)parameter;
            if (gridParameter.ShouldClearSelectionFirst)
                ((IGrid)this).ClearSelection();
            while (true)
            {
                if (SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier)
                {
                    bool wasPivoted;
                    DataFrame2D frame = Gidx3(gridParameter.MinimumColumns(processor, parameters),
                        gridParameter.MaximumColumns(processor, parameters),
                        0,
                        gridParameter.SubPrompt(processor, parameters),
                        gridParameter.DataAcquisitionMode,
                        out wasPivoted);
                    if (wasPivoted)
                        continue;
                    return frame;
                }
                switch (gridParameter.DataAcquisitionMode)
                {
                    case DataAcquisitionMode2D.Mode1:
                        {
                            DataFrame2D frame = new DataFrame2D();
                            bool userCancelled;
                            int groups = SdApplication.SoleInstance.GetInteger("Number of groups", gridParameter.Operation.ToString(), 1, out userCancelled);
                            if (userCancelled || groups < 1 || groups > 10)
                            {
                                // Cancelling the number of groups probably implies that the user wants to select by group
                                SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier = true;
                                continue;
                            }

                            // If we get here, we have some groups
                            bool ok = true;
                            for (int g = 0; g < groups; g++)
                            {
                                ((IGrid)this).ClearSelection();
                                bool wasPivoted;
                                DataFrame subFrame = GetCellArray(0, DataAcquisitionMode.NumericReplaceMissing, 1, 90, "Select subgroups for group " + (g + 1), parameter.CancelSkipsParameter, true, false, out userCancelled, out wasPivoted);
                                if (userCancelled)
                                    throw new TemplateOperationCancelledException();
                                if (wasPivoted)
                                {
                                    // Go round again
                                    ok = false;
                                    break;
                                }

                                frame.EnsureVariablesJagged(g + 1, subFrame.VariableCount);
                                for (int s = 0; s < subFrame.VariableCount; s++)
                                {
                                    frame.Variables[g][s] = subFrame.Variables[s];
                                    subFrame.Variables[s] = null; // Paranoia, to ensure any future destructors don't operate on the copied variable.
                                }
                            }
                            if (ok)
                            {
                                if (gridParameter.ShouldSquare)
                                {
                                    // Good to return.  Square up the data before we do.
                                    int maxRows = frame.MaxRows;
                                    for (int g = 0; g < frame.VariableCount; g++)
                                        for (int s = 0; s < frame.VariableCountTheOtherWay; s++)
                                            if (null != frame.Variables[g][s])
                                                frame.Variables[g][s].EnsureLength(maxRows);
                                }
                                return frame;
                            }
                        }
                        break;
                    case DataAcquisitionMode2D.Mode2:
                        {
                            DataFrame2D frame = new DataFrame2D();
                            bool userCancelled;
                            int repeats = SdApplication.SoleInstance.GetInteger("Number of repeats", gridParameter.Operation.ToString(), 2, out userCancelled);
                            if (userCancelled || repeats <= 1)
                            {
                                // Cancelling the number of groups probably implies that the user wants to select by group
                                SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier = true;
                                continue;
                            }

                            // If we get here, we have some repeats
                            int cols = 0;
                            int rows = 0;
                            bool startAgain = false;
                            frame.Name = "";
                            for (int rpt = 1; rpt <= repeats; rpt++)
                            {
                                frame.Name += " (";
                                ((IGrid)this).ClearSelection();
                                bool wasPivoted;
                                DataFrame repeatFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 2, 200, "Select subject (row) by treatment (column) data for repeat " + rpt, parameter.CancelSkipsParameter, true, DataAcquisitionWidth.Wide, false, out userCancelled, out wasPivoted);
                                // No frame was returned, either because the user cancelled or because of an error.  Distinguish the two cases!
                                if (userCancelled)
                                {
                                    if (null != gridParameter.CancelSkipsParameter)
                                        return null;
                                    throw new TemplateOperationCancelledException();
                                }
                                if (null == repeatFrame)
                                {
                                    if (wasPivoted)
                                        startAgain = true;
                                    break;
                                }

                                bool ok = true;
                                if (1 == rpt)
                                {
                                    // Wait for a selection then dim the array
                                    rows = repeatFrame.MinRows;
                                    cols = repeatFrame.VariableCount;
                                    frame.EnsureVariablesSquare(rows, cols);
                                    foreach (List<Variable> vl in frame.Variables)
                                    {
                                        for (int i = 0; i < vl.Count; i++)
                                        {
                                            DoubleVariable v = new DoubleVariable();
                                            v.EnsureLength(repeats);
                                            vl[i] = v;
                                        }
                                    }
                                }
                                else
                                {
                                    if (repeatFrame.MinRows != rows || repeatFrame.VariableCount != cols)
                                    {
                                        SdApplication.SoleInstance.MsgboxX("You must have the same number of subjects and treatments for each repeat, mark missing data with an asterisk if they are at the end of a column", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, gridParameter.Operation.ToString(), true);
                                        rpt--; // Try again
                                        ok = false;
                                    }
                                }
                                if (ok)
                                {
                                    for (int C = 0; C < cols; C++)
                                    {
                                        DoubleVariable v = repeatFrame.Variables[C].AsDoubleVariable;
                                        // 'CDAT2(rpt, c) = CDAT1(c)
                                        for (int r = 0; r < rows; r++)
                                        {
                                            frame.Variables[r][C].AsDoubleVariable.Data[rpt - 1] = v.Data[r];
                                        }
                                        if (0 == C)
                                        {
                                            frame.Name += repeatFrame.Variables[C].Title;
                                        }
                                        else
                                        {
                                            frame.Name += ", " + repeatFrame.Variables[C].Title;
                                        }
                                    }
                                }
                                frame.Name += ")";
                            }
                            if (!startAgain)
                            {
                                return frame;
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("parameter", gridParameter.DataAcquisitionMode, "Only Mode1 and Mode2 are known");
                }
            }
        }

        private void workbookView_KeyDown(object sender, KeyEventArgs e)
        {
            if (SdApplication.SoleInstance.IsSelecting && e.KeyCode == Keys.Enter)
            {
                // This changes the state both for selecting and for data input, but is OK because we only get here if we're selecting.
                SdApplication.SoleInstance.NoteEndOfSelection(true);
                e.Handled = true;
            }
        }

        public override IList<Pane> AvailablePanes
        {
            get
            {
                WindowInformation info = WindowInformation;
                string prefix = Text;
                List<Pane> panes = new List<Pane>();
                workbookView.ActiveWorkbookSet.GetLock();
                foreach (IWorksheet sheet in workbookView.ActiveWorkbook.Worksheets)
                {
                    panes.Add(new Pane(prefix + " " + sheet.Name, info, sheet.Name));
                }
                workbookView.ActiveWorkbookSet.ReleaseLock();
                panes.Add(new Pane("New sheet in " + prefix, info, null));
                return panes;
            }
        }

        public override bool SelectPane(Pane pane)
        {
            bool succeeded = true;
            workbookView.ActiveWorkbookSet.GetLock();
            if (null == pane || null == pane.Tag)
            {
                // New pane
                IWorksheet worksheet = workbookView.ActiveWorkbook.Worksheets.Add();
                workbookView.ActiveSheet = worksheet;
            }
            else
            {
                // Existing pane, selected by name.  If the name doesn't exist any more, return false.
                string sheetName = (string)pane.Tag;
                IWorksheet worksheet = workbookView.ActiveWorkbook.Worksheets[sheetName];
                succeeded = (null != worksheet);
                workbookView.ActiveSheet = worksheet;
            }
            workbookView.ActiveWorkbookSet.ReleaseLock();
            return succeeded;
        }

        public override Pane SelectedPane
        {
            get
            {
                WindowInformation info = WindowInformation;
                string prefix = Text;
                workbookView.ActiveWorkbookSet.GetLock();
                IWorksheet sheet = workbookView.ActiveWorksheet;
                Pane pane = new Pane(prefix + " " + sheet.Name, info, sheet.Name);
                workbookView.ActiveWorkbookSet.ReleaseLock();
                return pane;
            }
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Cut();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Copy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Paste();
        }

        private void pasteSpecialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (frmPasteSpecial frm = new frmPasteSpecial())
            {
                frm.ShowDialog(this);
                if (!frm.UserCancelled)
                    workbookView.PasteSpecial(frm.PasteType, PasteOperation.None, false, false);
            }
        }

        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Print(true);
        }

        private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.PrintPreview();
        }

        private void pageSetupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null != workbookView && null != workbookView.ActiveWorksheet)
            {
                IPageSetup pageSetup = workbookView.ActiveWorksheet.PageSetup;

                // Show the dialog
                using (PageSetupDialog pageSetupDialog = new PageSetupDialog())
                {
                    workbookView.GetLock();
                    try
                    {
                        // Pull settings into the page setup dialog
                        pageSetupDialog.PageSettings = new System.Drawing.Printing.PageSettings { Color = !pageSetup.BlackAndWhite, Landscape = pageSetup.Orientation == PageOrientation.Landscape, Margins = { Top = PointsToHundredths(pageSetup.TopMargin), Bottom = PointsToHundredths(pageSetup.BottomMargin), Left = PointsToHundredths(pageSetup.LeftMargin), Right = PointsToHundredths(pageSetup.RightMargin) } };
                        // pageSetupDialog.PageSettings.PaperSize = pageSetup.PaperSize;
                    }
                    finally
                    {
                        workbookView.ReleaseLock();
                    }

                    pageSetupDialog.AllowOrientation = true;
                    pageSetupDialog.AllowMargins = true;
                    // pageSetupDialog.AllowPaper = true;
                    DialogResult res = pageSetupDialog.ShowDialog(SdApplication.SoleInstance.MainWindow);
                    if (res == DialogResult.OK)
                    {
                        // Save settings into SSG's sheet settings
                        workbookView.GetLock();
                        try
                        {
                            pageSetup.BlackAndWhite = !pageSetupDialog.PageSettings.Color;
                            pageSetup.Orientation = pageSetupDialog.PageSettings.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                            pageSetup.TopMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Top);
                            pageSetup.BottomMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Bottom);
                            pageSetup.LeftMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Left);
                            pageSetup.RightMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Right);
                            // pageSetupDialog.PageSettings.PaperSize = pageSetup.PaperSize;
                        }
                        finally
                        {
                            workbookView.ReleaseLock();
                        }
                    }
                }
            }
        }

        private static double HundredthsToPoints(int hundredths)
        {
            return hundredths / 100.0 * 72.0;
        }

        private static int PointsToHundredths(double points)
        {
            return (int)(points / 72.0 * 100.0);
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workbookView.ActiveCommandManager.CanUndo)
                workbookView.ActiveCommandManager.Undo();
        }

        private void cellsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowRangeExplorer();
        }

        private void sheetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IWorksheet newSheet = workbookView.ActiveWorkbook.Worksheets.AddBefore(workbookView.ActiveWorksheet);
                workbookView.ActiveWorksheet = newSheet;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void rowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection;
                if (currentRange.IsEntireColumns)
                {
                    SdApplication.SoleInstance.MsgboxX("You cannot insert an entire worksheet's height of blank rows.  Please select fewer rows.", MessageBoxButtons.OK, MessageBoxIcon.Error, "Insert rows", false);
                    return;
                }

                currentRange = currentRange.EntireRow;
                currentRange.Insert();
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void columnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection;
                if (currentRange.IsEntireRows)
                {
                    SdApplication.SoleInstance.MsgboxX("You cannot insert an entire worksheet's width of blank columns.  Please select fewer columns.", MessageBoxButtons.OK, MessageBoxIcon.Error, "Insert columns", false);
                    return;
                }

                currentRange = currentRange.EntireColumn;
                currentRange.Insert();
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void cellsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            InsertCells();
        }

        private void InsertCells()
        {
            using (frmInsertCells frm = new frmInsertCells())
            {
                frm.ShowDialog(this);
                if (!frm.UserCancelled)
                {
                    workbookView.GetLock();
                    try
                    {
                        if (frm.IsEntire)
                        {
                            if (InsertShiftDirection.Right == frm.InsertShiftDirection)
                            {
                                workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection.EntireColumn, "Insert column", () =>
                                                                                                                                                         {
                                                                                                                                                             workbookView.RangeSelection.EntireColumn.Insert();
                                                                                                                                                             return true;
                                                                                                                                                         }));
                            }
                            else
                            {
                                workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection.EntireRow, "Insert row", () =>
                                                                                                                                                   {
                                                                                                                                                       workbookView.RangeSelection.EntireRow.Insert();
                                                                                                                                                       return true;
                                                                                                                                                   }));
                            }
                        }
                        else
                        {
                            InsertShiftDirection isd = frm.InsertShiftDirection; // Cached as frm is disposed before any undo might be called.
                            workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection, "Insert area", () =>
                                                                                                                                      {
                                                                                                                                          workbookView.RangeSelection.Insert(isd);
                                                                                                                                          return true;
                                                                                                                                      }));
                        }
                    }
                    finally
                    {
                        workbookView.ReleaseLock();
                    }
                }
            }
        }

        private void sheetSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IWorkbookSet workbookSet = workbookView.ActiveWorkbookSet;
                WorkbookExplorer explorer = new WorkbookExplorer(workbookSet) { Text = "Sheet settings" };
                explorer.Show(workbookView);
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Fix this rather nasty workaround once SpreadsheetGear has API support for its find dialog
            workbookView.Focus();
            SendKeys.Send("^f");
            Application.DoEvents(); // Force processing of events, in this case showing the find dialog
        }

        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Fix this rather nasty workaround once SpreadsheetGear has API support for its replace dialog
            workbookView.Focus();
            SendKeys.Send("^h");
            Application.DoEvents(); // Force processing of events, in this case showing the replace dialog
        }

        private void goToCellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoToCell();
        }

        private void GoToCell()
        {
            try
            {
                string cell = SdApplication.SoleInstance.GetString("Enter the cell address, for example G54", "Go to cell", "");
                workbookView.GetLock();
                if (null != cell)
                {
                    workbookView.ActiveWorksheet.Cells[cell].Activate();
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.Warning(ex.Message, "Go to cell");
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void clearSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Focus();
            SendKeys.Send("{DEL}");
            Application.DoEvents(); // Force processing of events, in this case clearing the selection
        }

        private void deleteSpecialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSpecial();
        }

        private void DeleteSpecial()
        {
            using (frmDeleteSpecial frm = new frmDeleteSpecial())
            {
                frm.ShowDialog(this);
                if (!frm.UserCancelled)
                {
                    workbookView.GetLock();
                    try
                    {
                        if (frm.IsEntire)
                        {
                            if (DeleteShiftDirection.Left == frm.DeleteShiftDirection)
                            {
                                workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection.EntireColumn, "Delete column", () =>
                                                                                                                                                         {
                                                                                                                                                             workbookView.RangeSelection.EntireColumn.Delete();
                                                                                                                                                             return true;
                                                                                                                                                         }));
                            }
                            else
                            {
                                workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection.EntireRow, "Delete row", () =>
                                                                                                                                                   {
                                                                                                                                                       workbookView.RangeSelection.EntireRow.Delete();
                                                                                                                                                       return true;
                                                                                                                                                   }));
                            }
                        }
                        else
                        {
                            DeleteShiftDirection dsd = frm.DeleteShiftDirection; // Cached as frm may be disposed before this is used
                            workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection, "Delete area", () =>
                                                                                                                                      {
                                                                                                                                          workbookView.RangeSelection.Delete(dsd);
                                                                                                                                          return true;
                                                                                                                                      }));
                        }
                    }
                    finally
                    {
                        workbookView.ReleaseLock();
                    }
                }
            }

        }

        private void deleteColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection.EntireColumn;
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(currentRange, "Delete column", () => { currentRange.Delete(); return true; }));
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void deleteRowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection.EntireRow;
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(currentRange, "Delete row", () => { currentRange.Delete(); return true; }));
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void deleteSheetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool shouldDelete = SdApplication.SoleInstance.Query("This will delete the current sheet.  You cannot undo this operation.  Are you sure you want to delete this sheet?", "Delete sheet");
            if (shouldDelete)
            {
                workbookView.GetLock();
                try
                {
                    workbookView.ActiveWorksheet.Delete();
                }
                finally
                {
                    workbookView.ReleaseLock();
                }
            }
        }

        private void describeColumnDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // This describes the column.  If there's not a column selection... select it!
            if (workbookView.RangeSelection.CellCount < 2)
            {
                workbookView.GetLock();
                try
                {
                    IRange cellSelection = workbookView.RangeSelection;
                    cellSelection.EntireColumn.Select();
                }
                finally
                {
                    workbookView.ReleaseLock();
                }
            }
            DoOperation("QuickSummary");
        }

        private void fillDownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection, "Fill down", () => { workbookView.RangeSelection.FillDown(); return true; }));
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void fillRightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection, "Fill right", () => { workbookView.RangeSelection.FillRight(); return true; }));
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void rowHideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection.EntireRow;
                currentRange.Hidden = true;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void rowUnhideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection.EntireRow;
                currentRange.Hidden = false;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void columnHideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection.EntireColumn;
                currentRange.Hidden = true;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void columnUnhideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IRange currentRange = workbookView.RangeSelection.EntireColumn;
                currentRange.Hidden = false;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void freezePanesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                IWorksheet worksheet = workbookView.ActiveWorksheet;
                IWorksheetWindowInfo info = worksheet.WindowInfo;
                if (info.FreezePanes)
                {
                    worksheet.WindowInfo.SplitColumns = 0;
                    worksheet.WindowInfo.SplitRows = 0;
                    info.FreezePanes = false;
                }
                else
                {
                    int row = workbookView.ActiveCell.Row;
                    int column = workbookView.ActiveCell.Column;

                    worksheet.WindowInfo.SplitColumns = column; // column + 1;
                    worksheet.WindowInfo.SplitRows = row; // row + 1;

                    // Freeze the panes.
                    info.FreezePanes = true;
                }
            }
            finally
            {
                workbookView.ReleaseLock();
            }

        }

        private void autoFitWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                workbookView.RangeSelection.Columns.AutoFit();
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void defaultFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FontDialog dlg = new FontDialog
                                 {
                                     Font =
                                         new Font(workbookView.ActiveWorkbookSet.DefaultFontName,
                                                  (float)workbookView.ActiveWorkbookSet.DefaultFontSize),
                                     ShowApply = false,
                                     ShowEffects = false,
                                     ShowHelp = false
                                 };
            DialogResult result = dlg.ShowDialog(this);
            if (DialogResult.OK == result)
            {
                workbookView.GetLock();
                try
                {
                    workbookView.ActiveWorkbookSet.DefaultFontName = dlg.Font.FontFamily.Name;
                    workbookView.ActiveWorkbookSet.DefaultFontSize = dlg.Font.SizeInPoints;
                }
                finally
                {
                    workbookView.ReleaseLock();
                }
            }
        }

        private void lockSheetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.GetLock();
            try
            {
                workbookView.ActiveWorksheet.ProtectContents = !workbookView.ActiveWorksheet.ProtectContents;
                lockSheetToolStripMenuItem.Checked = workbookView.ActiveWorksheet.ProtectContents;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void workbookView_ActiveTabChanged(object sender, ActiveTabChangedEventArgs e)
        {
            if (null != workbookView.ActiveWorksheet)
                lockSheetToolStripMenuItem.Checked = workbookView.ActiveWorksheet.ProtectContents;
        }

        private void frmSpreadsheetGear_Shown(object sender, EventArgs e)
        {
            if (null != workbookView.ActiveWorksheet)
            {
                lockSheetToolStripMenuItem.Checked = workbookView.ActiveWorksheet.ProtectContents;
            }
            workbookView.Focus();
        }

        private void importDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOperation("ImportWorksheet");
        }

        private static void DoOperation(string operationName)
        {
            SdApplication.SoleInstance.MainWindow.DoOperation(operationName);
        }

        private void exportDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOperation("ExportWorksheet");
        }

        internal override void EditCopy()
        {
            workbookView.Copy();
        }

        internal override void EditCut()
        {
            workbookView.Cut();
        }

        internal override void EditPaste()
        {
            workbookView.Paste();
        }

        internal override void Print()
        {
            workbookView.Print(true);
        }

        private void cutContextMenuItem1_Click(object sender, EventArgs e)
        {
            workbookView.Cut();
        }

        private void copyContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Copy();
        }

        private void pasteContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Paste();
        }

        private void pasteSpecialContextMenuItem_Click(object sender, EventArgs e)
        {
            using (frmPasteSpecial frm = new frmPasteSpecial())
            {
                frm.ShowDialog(this);
                if (!frm.UserCancelled)
                    workbookView.PasteSpecial(frm.PasteType, PasteOperation.None, false, false);
            }
        }

        private void insertContextMenuItem_Click(object sender, EventArgs e)
        {
            InsertCells();
        }

        private void deleteContextMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSpecial();
        }

        private void clearContentsContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.Focus();
            SendKeys.Send("{DEL}");
            Application.DoEvents(); // Force processing of events, in this case clearing the selection
        }

        private void insertCommentContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.ActiveWorkbookSet.GetLock();
            try
            {
                if (null == workbookView.ActiveCell.Comment)
                {
                    workbookView.ActiveCell.AddComment("");
                }
                // By now, the comment is known to exist.
                workbookView.ActiveCell.Comment.Visible = true;
            }
            finally
            {
                workbookView.ActiveWorkbookSet.ReleaseLock();
            }
        }

        private void goToContextMenuItem_Click(object sender, EventArgs e)
        {
            GoToCell();
        }

        private void findAndReplaceContextMenuItem_Click(object sender, EventArgs e)
        {
            // Fix this rather nasty workaround once SpreadsheetGear has API support for its replace dialog
            workbookView.Focus();
            SendKeys.Send("^h");
            Application.DoEvents(); // Force processing of events, in this case showing the replace dialog
        }

        private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            // If we're selecting, this cancels the selection instead of showing the menu.
            if (SdApplication.SoleInstance.SelectingData)
            {
                // NoteEndOfSelection clears both selectingData and inputtingData.  However, that's safe here, as we only get here if we're SelectingData.
                SdApplication.SoleInstance.NoteEndOfSelection(true);
                e.Cancel = true;
                return;
            }

            // If we get here, no selection is occurring
            workbookView.ActiveWorkbookSet.GetLock();
            try
            {
                IComment comment = workbookView.ActiveCell.Comment;
                insertCommentContextMenuItem.Visible = null == comment;
                deleteCommentContextMenuItem.Visible = null != comment;
                showCommentContextMenuItem.Visible = null != comment && !comment.Visible;
                hideCommentContextMenuItem.Visible = null != comment && comment.Visible;
                editCommentContextMenuItem.Visible = false; // null != comment;
            }
            finally
            {
                workbookView.ActiveWorkbookSet.ReleaseLock();
            }
        }

        private void deleteCommentContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.ActiveWorkbookSet.GetLock();
            try
            {
                if (null != workbookView.ActiveCell.Comment)
                {
                    workbookView.ActiveCell.ClearComments();
                }
            }
            finally
            {
                workbookView.ActiveWorkbookSet.ReleaseLock();
            }
        }

        private void showCommentContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.ActiveWorkbookSet.GetLock();
            try
            {
                if (null != workbookView.ActiveCell.Comment)
                {
                    workbookView.ActiveCell.Comment.Visible = true;
                }
            }
            finally
            {
                workbookView.ActiveWorkbookSet.ReleaseLock();
            }
        }

        private void editCommentContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.ActiveWorkbookSet.GetLock();
            try
            {
                if (null != workbookView.ActiveCell.Comment)
                {
                    // TODO: Do something!
                }
            }
            finally
            {
                workbookView.ActiveWorkbookSet.ReleaseLock();
            }
        }

        private void hideCommentContextMenuItem_Click(object sender, EventArgs e)
        {
            workbookView.ActiveWorkbookSet.GetLock();
            try
            {
                if (null != workbookView.ActiveCell.Comment)
                {
                    workbookView.ActiveCell.Comment.Visible = false;
                }
            }
            finally
            {
                workbookView.ActiveWorkbookSet.ReleaseLock();
            }
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workbookView.ActiveCommandManager.CanRedo)
                workbookView.ActiveCommandManager.Redo();
        }

        private delegate bool WrappedExecute();

        private class UndoWrapper : CommandRange
        {
            private readonly string displayText;
            private readonly WrappedExecute wrappedExecute;

            public UndoWrapper(IRange range, string displayText, WrappedExecute wrappedExecute)
                : base(range)
            {
                this.displayText = displayText;
                this.wrappedExecute = wrappedExecute;
            }

            public override string DisplayText
            {
                get
                {
                    // Text displayed in Undo menu. 
                    return displayText;
                }
            }

            protected override CommandRangeUndoFlags UndoFlags
            {
                get
                {
                    // Save everything!
                    return
                        CommandRangeUndoFlags.AutoFilters
                        | CommandRangeUndoFlags.ColumnInfo
                        | CommandRangeUndoFlags.Comments
                        | CommandRangeUndoFlags.FormatBorders
                        | CommandRangeUndoFlags.FormatConditions
                        | CommandRangeUndoFlags.Formats
                        | CommandRangeUndoFlags.FormulaFixups
                        | CommandRangeUndoFlags.Hyperlinks
                        | CommandRangeUndoFlags.MergeState
                        | CommandRangeUndoFlags.RowInfo
                        | CommandRangeUndoFlags.Validation
                        | CommandRangeUndoFlags.Values;
                }
            }

            protected override bool Execute()
            {
                return wrappedExecute();
            }
        }

        private void formatCellsContextMenuItem_Click(object sender, EventArgs e)
        {
            ShowRangeExplorer();
        }

        private void ShowRangeExplorer()
        {
            workbookView.GetLock();
            try
            {
                const RangeExplorerCategoryFlags categoryFlags = RangeExplorerCategoryFlags.All;
                IWorkbookSet workbookSet = workbookView.ActiveWorkbookSet;
                RangeExplorer explorer = new RangeExplorer(workbookSet, categoryFlags);
                explorer.Show(workbookView);
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void summaryContextMenuItem_Click(object sender, EventArgs e)
        {
            // This describes the column.  If there's not a column selection... select it!
            if (workbookView.RangeSelection.CellCount < 2)
            {
                workbookView.GetLock();
                try
                {
                    IRange cellSelection = workbookView.RangeSelection;
                    cellSelection.EntireColumn.Select();
                }
                finally
                {
                    workbookView.ReleaseLock();
                }
            }
            DoOperation("QuickSummary");
        }

    }
}
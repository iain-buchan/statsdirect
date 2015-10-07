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
using StatsDirect.Utilities;
using SpreadsheetGear.Advanced.Cells;
using SpreadsheetGear;
using SpreadsheetGear.Windows.Forms;
using Color = System.Drawing.Color;
using SystemColors = System.Drawing.SystemColors;
using StatsDirect.R;

namespace StatsDirect.UI
{
    internal partial class frmSpreadsheetGear : StatsDirectForm, IGrid, IGetCells
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
                if (null != workbookView.ActiveWorkbook)
                    workbookView.ActiveWorkbook.Close();
                string fontString = Properties.Settings.Default.DefaultWorkbookFont;
                if (null != fontString)
                {
                    using (Font f = Utilities.Utilities.FontFromSaveString(fontString))
                    {
                        workbookView.ActiveWorkbookSet.DefaultFontName = f.Name;
                        workbookView.ActiveWorkbookSet.DefaultFontSize = f.SizeInPoints;
                    }
                }
                else
                {
                    workbookView.ActiveWorkbookSet.DefaultFontName = "Calibri";
                    workbookView.ActiveWorkbookSet.DefaultFontSize = 11;
                }
                workbookView.ActiveWorkbook = workbookView.ActiveWorkbookSet.Workbooks.Add();
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
            Visible = false;
            MdiParent = null;
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

        public override bool OpenFile(string filename, bool isTempFile, string nameToDisplay)
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
                // dirty = isTempFile; Removed in #909
            }
            finally
            {
                workbookView.ReleaseLock();
            }
            if (null != wb)
                workbookView.ActiveWorkbook = wb;
            if (isTempFile && null != nameToDisplay)
                SetUnsavedName(nameToDisplay);
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

        void IGrid.Refill(List<Variable> variables)
        {
            CellSelection revisedCellSelection = new CellSelection();

            foreach (Variable variable in variables)
            {
                WorksheetOrigin worksheetOrigin = (WorksheetOrigin)variable.Origin;

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

                CellColumnSelection cellColumnSelection = new CellColumnSelection(this) { ColumnIndex = worksheetOrigin.Column, RowCount = worksheetOrigin.Rows, RowIndex = worksheetOrigin.TopRow };
                MaybeExpandCellColumnSelection(cellColumnSelection);
                revisedCellSelection.ColumnSelections.Add(cellColumnSelection);
                revisedCellSelection.LongestRowCount = cellColumnSelection.RowCount;
            }
            WorksheetOrigin firstWorksheetOrigin = (WorksheetOrigin)variables[0].Origin;
            DataFrame refilledFrame = CellArrayProcessor.ProcessCellArray(revisedCellSelection, firstWorksheetOrigin.Mode, 0, true, firstWorksheetOrigin.HasTitle, firstWorksheetOrigin.OriginGroup, ((WindowInformation)Tag).FriendlyName);
            if (refilledFrame.VariableCount != variables.Count)
                throw new Exception("Cannot refill frame as the number of variables present in the workbook \"" + firstWorksheetOrigin.WorkbookPath + "\" appears to differ now.");
            for (int i = 0; i < refilledFrame.VariableCount; i++)
                variables[i].StealDataFrom(refilledFrame.Variables[i]);
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
            int probeTop = cellColumnSelection.RowIndex + cellColumnSelection.RowCount;
            int probeBottom = Math.Min(probeTop + PROBE_ROWS - 1, lastUsedRow);
            while (probeTop <= probeBottom)
            {
                int nonHiddenRowCount;
                object[,] probe = GetCellObjects(cellColumnSelection.ColumnIndex, probeTop, probeBottom, out nonHiddenRowCount);
                for (int offset = 0; offset < nonHiddenRowCount; offset++)
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
                IRange usedRange = worksheet.UsedRange;
                int firstFreeColumn = usedRange.Column + usedRange.ColumnCount;
                // Don't believe the free columns - there may be more space!
                while (firstFreeColumn > 0)
                {
                    bool allBlank = true;
                    for (int r = usedRange.Row; r < usedRange.Row + usedRange.RowCount; r++)
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

                IRange range = worksheet.Range[0, firstColumnOfData, usedRange.Row + usedRange.RowCount + offsetForTitles - 1, firstColumnOfData + frame.VariableCount - 1];
                // Create a holder that can be captured by the lambda but then can be cleared out so that it doesn't retain large amounts of data
                WriteDataFrameParametersHolder holder = new WriteDataFrameParametersHolder { Frame = frame, IsFormulae = isFormulae, MissingIndicator = missingIndicator, Range = range, ShouldMove = shouldMove, OffsetForTitles = offsetForTitles };
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(/* workbookView.RangeSelection */ range.EntireColumn, "Insert data", () => { if (null != holder.Frame) WriteDataFrameInternal(holder.Frame, holder.IsFormulae, holder.MissingIndicator, holder.Range, holder.ShouldMove, holder.OffsetForTitles); return true; }));
                // Clear down reference variables
                holder.Range = null;
                holder.Frame = null;
                holder.MissingIndicator = null;
                workbookView.Focus();
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
                        case VariableType.Double:
                            {
                                DoubleVariable variable = frame.Variables[v].AsDoubleVariable;
                                double[] data = variable.Data;
                                if (null != data)
                                {
                                    for (int i = 0; i < data.Length; i++)
                                        if (Constant.MISSING == data[i] || double.IsNaN(data[i]))
                                            values.SetText(i + offsetForTitles, firstColumnOfData + v, missingIndicator);
                                        else
                                            values.SetNumber(i + offsetForTitles, firstColumnOfData + v, data[i]);
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
                        case VariableType.Variant:
                            {
                                VariantVariable variable = frame.Variables[v].AsVariantVariable;
                                object[] data = variable.Data;
                                if (null != data)
                                {
                                    for (int i = 0; i < data.Length; i++)
                                        if (data[i] is double)
                                        {
                                            double val = (double)data[i];
                                            if (Constant.MISSING == val || double.IsNaN(val))
                                                values.SetText(i + offsetForTitles, firstColumnOfData + v, missingIndicator);
                                            else
                                                values.SetNumber(i + offsetForTitles, firstColumnOfData + v, val);
                                        }
                                        else if (data[i] is bool)
                                        {
                                            values.SetLogical(i + offsetForTitles, firstColumnOfData + v, (bool)data[i]);
                                        }
                                        else if (null == data[i])
                                        {
                                            values.Clear(i + offsetForTitles, firstColumnOfData + v);
                                        }
                                        else
                                        {
                                            values.SetText(i + offsetForTitles, firstColumnOfData + v, data[i].ToString());
                                        }
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

        private static void DoOrWarn(Action func, string explanation)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                func();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError(explanation, ex, false);
            }
#endif
        }

        private static void DoOrSwallow(Action func)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                func();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception)
            {
                // TODO: It'd be nice to know that the exception happened for our diagnostic purposes.
            }
#endif
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(Close, "Couldn't close workbook");
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(() => SaveContents(), "Couldn't save workbook");
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(() => SaveAsContents(), "Couldn't save workbook");
        }

        private class CellSelectionResult
        {
            public CellSelection CellSelection { get; set; }
            /// <summary>If true, the user cancelled the selection</summary>
            public bool UserCancelled { get; set; }
            /// <summary>If true, the user changed from selecting groups by column to by identifier, or vice versa</summary>
            public bool WasPivoted { get; set; }
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
        /// <returns>the user's cell selections within the active worksheet, or null if the user declined to select anything</returns>
        private CellSelectionResult GetSelection(int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, bool selectionWasDefaulted)
        {
            // Handle default selections: we may need to come in with a pre-selected area and force the user to confirm it.  However, if the user gives an illegal selection, we need to give errors.  So we keep a state of whether the selection presently on the grid is the default or is user-selected.
            bool selectionIsDefault = selectionWasDefaulted;
            const string msgTi = "StatsDirect Data Selection";

            // Get the number of selections & Last Row, Col info
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
                                CellColumnSelection ccs = new CellColumnSelection(this) { WorkbookPath = grid.WorkbookPath, WorksheetName = grid.ActiveWorksheetName, ColumnIndex = c, RowIndex = usedFrom.Top, RowCount = rows };
                                sel.ColumnSelections.Add(ccs);
                            }
                        }
                    }
                }

                // Check the selection
                bool hasSelection = grid.HasSelection();
                if (sel.TotalColumns >= minimumColumns && sel.TotalColumns <= maximumColumns && hasSelection && !selectionIsDefault)
                {
                    // Fits our criteria; return it
                    return new CellSelectionResult { CellSelection = sel, UserCancelled = false, WasPivoted = false };
                }
                // There's a problem, and this selection is inappropriate.  Or, this is the first time we've come in with a default selection, so the user has to confirm it.

                // Create an intelligent message to the user and show it
                if (hasSelection && sel.TotalColumns > 0 && !selectionIsDefault)
                    GridSelectionProcessor.SelNumWarn(minimumColumns, maximumColumns, sel.TotalColumns, msgTi);
                string fullSelectionMessage = selectionMessage;
                if (minimumColumns == maximumColumns)
                    fullSelectionMessage += " (" + minimumColumns.ToString() + " column" + (minimumColumns > 1 ? "s" : "") + ")";
                else
                    fullSelectionMessage += " (Min " + minimumColumns.ToString() + ": Max " + maximumColumns.ToString() + ")";
                SdApplication.SoleInstance.MainWindow.CanSelectMultipleRows = maximumColumns > 1;
                SdApplication.SoleInstance.MainWindow.CanSelectGroupMethod = allowUserToPivot;
                if (allowUserToPivot)
                    SdApplication.SoleInstance.MainWindow.GroupsByIdentifier = SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier;
                Color oldBackColor = BackColor;
                BackColor = SystemColors.Info;
                try
                {
                    bool wasPivoted;
                    if (!SdApplication.SoleInstance.MainWindow.SelectCells(fullSelectionMessage, cancelButtonLabel, out wasPivoted))
                    {
                        // The user either cancelled or pivoted
                        return new CellSelectionResult { UserCancelled = !wasPivoted, WasPivoted = wasPivoted };
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
        public DataFrame GetCellArray(int rowLengthHint, DataAcquisitionMode mode, int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, bool mightBeBatching, out bool userCancelled, out bool wasPivoted, int originGroup)
        {
            bool shouldDefaultSelection = null != mostRecentCellSelectionDuringBatch && mightBeBatching;
            if (shouldDefaultSelection)
            {
                // Set up the cell selection from its memory.
                IGrid grid = this;
                CellColumnSelection col = mostRecentCellSelectionDuringBatch.ColumnSelections[0];
                grid.Selection = new Range(new[] { new Area(grid, col.RowIndex, col.ColumnIndex, col.RowIndex + col.RowCount - 1, col.ColumnIndex) });
            }

            CellSelectionResult cellSelectionResult = GetSelection(minimumColumns, maximumColumns, selectionMessage, cancelButtonLabel, allowUserToPivot, shouldDefaultSelection);
            userCancelled = cellSelectionResult.UserCancelled;
            wasPivoted = cellSelectionResult.WasPivoted;
            CellSelection cellSelection = cellSelectionResult.CellSelection;

            if (mightBeBatching)
                mostRecentCellSelectionDuringBatch = cellSelection;

            if (null == cellSelection)
            {
                // No selection
                return null;
            }

            return CellArrayProcessor.ProcessCellArray(cellSelection, mode, rowLengthHint, false, false, originGroup, ((WindowInformation)Tag).FriendlyName);
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
            return DateTime.MinValue;
        }

        /// <summary>
        /// Get the single cell value at the specified row and column.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="column"></param>
        /// <returns>MISSING if the value could not be converted, MISSING * 10 if the value was previously MISSING, or the converted value</returns>
        double IGetCells.GetCellValue(int row, int column)
        {
            workbookView.GetLock();
            try
            {
                object val = workbookView.ActiveWorksheet.Cells[row, column].Value;
                return ToCellValue(val);
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        string IGetCells.GetCellText(int row, int column)
        {
            workbookView.GetLock();
            try
            {
                object val = workbookView.ActiveWorksheet.Cells[row, column].Value;
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
        /// <param name="nonHiddenRowCount">The number of non-hidden objects in the array.  Note that raw retrieved values will have been copied down the array to obscure hidden objects in this case; the top end of the array will NOT have been null-filled, so the values in return[nonHiddenRowCount] and above should be considered unknown.</param>
        /// <returns></returns>
        public object[,] GetCellObjects(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
        {
            if (lastRow < firstRow)
            {
                nonHiddenRowCount = 0;
                return new object[0, 0];
            }
            workbookView.GetLock();
            try
            {
                object val = workbookView.ActiveWorksheet.Cells[firstRow, column, lastRow, column].Value;
                if (null != val && val.GetType().IsArray)
                {
                    // If we get here, the returned value is an array.  Some of the rows may be hidden; if so, we should return only the visible data.
                    object[,] valArray = (object[,])val;
                    int validRows = 0;
                    for (int rowOffset = 0; rowOffset <= lastRow - firstRow; rowOffset++)
                    {
                        bool hidden = workbookView.ActiveWorksheet.Cells[firstRow + rowOffset, column].EntireRow.Hidden;
                        if (!hidden)
                        {
                            if (validRows != rowOffset)
                            {
                                // We can guarantee there is only one column, so the array minor index must always be 0
                                valArray[validRows, 0] = valArray[rowOffset, 0];
                            }
                            validRows++;
                        }
                    }
                    nonHiddenRowCount = validRows;
                    return valArray;
                }

                // If we get here, the returned value is a single cell.  It may still be hidden.
                nonHiddenRowCount = (workbookView.ActiveWorksheet.Cells[firstRow, column].EntireRow.Hidden) ? 0 : 1;
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
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        string[] IGetCells.GetCellFormulae(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
        {
            workbookView.GetLock();
            try
            {
                string[] result = new string[lastRow - firstRow + 1];
                int validRows = 0;
                for (int row = firstRow; row <= lastRow; row++)
                {
                    bool hidden = workbookView.ActiveWorksheet.Cells[row, column].EntireRow.Hidden;
                    if (!hidden)
                        result[validRows++] = workbookView.ActiveWorksheet.Cells[row, column].Formula;
                }
                nonHiddenRowCount = validRows;
                return result;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        double[] IGetCells.GetCellValues(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
        {
            if (IsHiddenColumn(column))
            {
                nonHiddenRowCount = 0;
                return null;
            }

            object[,] values = GetCellObjects(column, firstRow, lastRow, out nonHiddenRowCount);

            double[] returnValues = new double[nonHiddenRowCount];
            for (int r = 0; r < nonHiddenRowCount; r++)
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

        DateTime[] IGetCells.GetCellDateValues(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
        {
            object[,] values = GetCellObjects(column, firstRow, lastRow, out nonHiddenRowCount);

            DateTime[] returnValues = new DateTime[nonHiddenRowCount];
            for (int r = 0; r < nonHiddenRowCount; r++)
                returnValues[r] = ToCellDateValue(values[r, 0]);
            return returnValues;
        }

        /// <summary>
        /// Obtain a raw array of display strings from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        string[] IGetCells.GetCellTexts(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
        {
            workbookView.GetLock();
            try
            {
                string[] returnedValues = new string[lastRow - firstRow + 1];
                int validRows = 0;
                for (int i = 0; i <= lastRow - firstRow; i++)
                {
                    bool hidden = workbookView.ActiveWorksheet.Cells[firstRow + i, column].EntireRow.Hidden;
                    if (!hidden)
                        returnedValues[validRows++] = workbookView.ActiveWorksheet.Cells[firstRow + i, column].Text;
                }
                nonHiddenRowCount = validRows;
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
        string IGetCells.GetColumnTitle(int col)
        {
            const int A = 65;
            int prefixValue = (col / 26) - 1;
            int suffixValue = (col % 26);
            if (prefixValue < 0)
                return new string(new[] { (char)(suffixValue + A) });
            return new string(new[] { (char)(prefixValue + A), (char)(suffixValue + A) });
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
                    panes.Add(new Pane(prefix + " " + sheet.Name, info, sheet.Name));
                workbookView.ActiveWorkbookSet.ReleaseLock();
                panes.Add(new Pane("New sheet in " + prefix, info, null));
                return panes;
            }
        }

        /// <summary>
        /// At present, this checks for bold, underline, or quotes.
        /// </summary>
        /// <param name="workbookPath"></param>
        /// <param name="worksheetName"></param>
        /// <param name="columnIndex"></param>
        /// <param name="rowIndex"></param>
        /// <returns></returns>
        bool IGetCells.IsFormattedLikeATitle(int columnIndex, int rowIndex)
        {
            // TODO: Cross-workbook cell selections
            workbookView.GetLock();
            try
            {
                IWorksheet worksheet = workbookView.ActiveWorksheet;
                IRange cell = worksheet.Range[rowIndex, columnIndex];
                if (cell.Font.Bold || (cell.Font.Underline != UnderlineStyle.None))
                    return true;
                string v = cell.Value.ToString();
                return v.StartsWith("\"") || v.StartsWith("'");
            }
            finally
            {
                workbookView.ReleaseLock();
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
            DoOrWarn(workbookView.Cut, "Cut failed");
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(workbookView.Copy, "Copy failed");
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(workbookView.Paste, "Paste failed");
        }

        private void pasteSpecialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(PasteSpecial, "Paste Special failed");
        }

        private void PasteSpecial()
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
            DoOrWarn(Print, "Print failed");
        }

        private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(workbookView.PrintPreview, "Print preview failed");
        }

        private void pageSetupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(PageSetup, "Page setup failed");
        }

        private void PageSetup()
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
            DoOrWarn(Undo, "Undo failed");
        }

        private void Undo()
        {
            if (workbookView.ActiveCommandManager.CanUndo)
                workbookView.ActiveCommandManager.Undo();
        }

        private void cellsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ShowRangeExplorer, "Format cells failed");
        }

        private void sheetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(InsertSheet, "Insert sheet failed");
        }

        private void InsertSheet()
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
            DoOrWarn(InsertRow, "Insert row failed");
        }

        private void InsertRow()
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
            DoOrWarn(InsertColumn, "Insert column failed");
        }

        private void InsertColumn()
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
            DoOrWarn(InsertCells, "Insert cells failed");
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
            DoOrWarn(SheetSettings, "Sheet settings failed");
        }

        private void SheetSettings()
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
            DoOrWarn(EditFind, "Find failed");
        }

        private void EditFind()
        {
            // Fix this rather nasty workaround once SpreadsheetGear has API support for its find dialog
            workbookView.Focus();
            SendKeys.Send("^f");
            Application.DoEvents(); // Force processing of events, in this case showing the find dialog
        }

        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditReplace, "Replace failed");
        }

        private void EditReplace()
        {
            // Fix this rather nasty workaround once SpreadsheetGear has API support for its replace dialog
            workbookView.Focus();
            SendKeys.Send("^h");
            Application.DoEvents(); // Force processing of events, in this case showing the replace dialog
        }

        private void goToCellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(GoToCell, "Go to cell failed");
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
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void clearSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ClearSelectedCells, "Clear selection failed");
        }

        private void ClearSelectedCells()
        {
            workbookView.Focus();
            SendKeys.Send("{DEL}");
            Application.DoEvents(); // Force processing of events, in this case clearing the selection
        }

        private void deleteSpecialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(DeleteSpecial, "Delete Special failed");
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
            DoOrWarn(DeleteColumn, "Delete column failed");
        }

        private void DeleteColumn()
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
            DoOrWarn(DeleteRow, "Delete row failed");
        }

        private void DeleteRow()
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
            DoOrWarn(DeleteSheet, "Delete sheet failed");
        }

        private void DeleteSheet()
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
            DoOrWarn(DescribeColumn, "Description failed");
        }

        private void DescribeColumn()
        {
            try
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
            catch (CancelCurrentOperationAndDoException)
            {
                // Give up!
            }
        }

        private void fillDownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditFillDown, "Fill down failed");
        }

        private void EditFillDown()
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
            DoOrWarn(EditFillRight, "Fill right failed");
        }

        private void EditFillRight()
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
            DoOrWarn(HideRow, "Hide row failed");
        }

        private void HideRow()
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
            DoOrWarn(UnhideRow, "Unhide row failed");
        }

        private void UnhideRow()
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
            DoOrWarn(HideColumn, "Hide column failed");
        }

        private void HideColumn()
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
            DoOrWarn(UnhideColumn, "Unhide column failed");
        }

        private void UnhideColumn()
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
            DoOrWarn(FreezePanes, "Freeze panes failed");
        }

        private void FreezePanes()
        {
            workbookView.GetLock();
            try
            {
                IWorksheet worksheet = workbookView.ActiveWorksheet;
                IWorksheetWindowInfo info = worksheet.WindowInfo;
                if (info.FreezePanes)
                {
                    // Unfreeze the panes
                    worksheet.WindowInfo.SplitColumns = 0;
                    worksheet.WindowInfo.SplitRows = 0;
                    info.FreezePanes = false;
                }
                else
                {
                    // Freeze the panes.  Defaults to freezing at the current row/column, so no need to set that before freezing.
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
            DoOrWarn(AutoFitWidth, "Auto-fit failed");
        }

        private void AutoFitWidth()
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
            DoOrWarn(SetDefaultFont, "Set default font failed");
        }

        private void SetDefaultFont()
        {
            FontDialog dlg = new FontDialog
            {
                Font = new Font(workbookView.ActiveWorkbookSet.DefaultFontName, (float)workbookView.ActiveWorkbookSet.DefaultFontSize),
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
                Properties.Settings.Default.DefaultWorkbookFont = Utilities.Utilities.SaveStringFromFont(dlg.Font);
                Properties.Settings.Default.Save();
            }
        }

        private void lockSheetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(LockSheet, "Lock sheet failed");
        }

        private void LockSheet()
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
            DoOrSwallow(NoteActiveTabChanged);
        }

        private void NoteActiveTabChanged()
        {
            if (null != workbookView.ActiveWorksheet)
                lockSheetToolStripMenuItem.Checked = workbookView.ActiveWorksheet.ProtectContents;
        }

        private void frmSpreadsheetGear_Shown(object sender, EventArgs e)
        {
            DoOrSwallow(NoteShown);
        }

        private void NoteShown()
        {
            if (null != workbookView.ActiveWorksheet)
            {
                lockSheetToolStripMenuItem.Checked = workbookView.ActiveWorksheet.ProtectContents;
            }
            workbookView.Focus();
        }

        private void importDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ImportData, "Import data failed");
        }

        private void ImportData()
        {
            DoOperation("ImportWorksheet");
        }

        private static void DoOperation(string operationName)
        {
            SdApplication.SoleInstance.MainWindow.DoOperation(operationName);
        }

        private void exportDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ExportData, "Export data failed");
        }

        private void ExportData()
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
            DoOrSwallow(EditCut);
        }

        private void copyContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(EditCopy);
        }

        private void pasteContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(EditPaste);
        }

        private void pasteSpecialContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(PasteSpecial);
        }

        private void insertContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(InsertCells);
        }

        private void deleteContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(DeleteSpecial);
        }

        private void clearContentsContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(ClearSelectedCells);
        }

        private void insertCommentContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(InsertComment);
        }

        private void InsertComment()
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
            DoOrWarn(GoToCell, "Go to cell failed");
        }

        private void findAndReplaceContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(EditReplace);
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
            DoOrSwallow(DeleteComment);
        }

        private void DeleteComment()
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
            DoOrSwallow(ShowComment);
        }

        private void ShowComment()
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
            DoOrSwallow(EditComment);
        }

        private void EditComment()
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
            DoOrSwallow(HideComment);
        }

        private void HideComment()
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
            DoOrWarn(EditRedo, "Couldn't redo");
        }

        private void EditRedo()
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
            DoOrWarn(ShowRangeExplorer, "Couldn't format cells");
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
            DoOrWarn(DescribeColumn, "Couldn't describe column");
        }

        internal void SetUnsavedName(string childName)
        {
            Text = childName;
            workbookView.GetLock();
            try
            {
                workbookView.ActiveWorkbook.FullName = childName;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void exportToRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ExportSelectionToR, "Couldn't export selection to clipboard in R format");
        }

        private void ExportSelectionToR()
        {
            bool userCancelled;
            bool wasPivoted;
            DataFrame frame = GetCellArray(0, DataAcquisitionMode.Variant, 1, 10000, "Select the data to be placed on the clipboard", null, false, false, out userCancelled, out wasPivoted, 0);
            if (userCancelled)
                return;
            StringBuilder sb = new StringBuilder();
            RConvert.ToR(sb, "copied.data", frame, FrameType.Long);
            Clipboard.Clear();
            Clipboard.SetText(sb.ToString(), TextDataFormat.Text);
        }

        internal void ToggleFilters()
        {
            workbookView.GetLock();
            try
            {
                workbookView.RangeSelection.AutoFilter();
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void renameWorksheetContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(RenameWorksheet, "Couldn't rename worksheet");
        }

        private void RenameWorksheet()
        {
            string currentWorksheetName = null;
            workbookView.GetLock();
            try
            {
                currentWorksheetName = workbookView.ActiveSheet.Name;
            }
            finally
            {
                workbookView.ReleaseLock();
            }
            if (null != currentWorksheetName)
            {
                string newWorksheetName = SdApplication.SoleInstance.GetString("Enter new name for worksheet", "Rename Worksheet", currentWorksheetName);
                if (!string.IsNullOrWhiteSpace(newWorksheetName))
                {
                    workbookView.GetLock();
                    try
                    {
                        workbookView.ActiveSheet.Name = newWorksheetName;
                    }
                    finally
                    {
                        workbookView.ReleaseLock();
                    }
                }
            }
        }

        private class WriteDataFrameParametersHolder
        {
            public DataFrame Frame { get; set; }
            public bool IsFormulae { get; set; }
            public string MissingIndicator { get; set; }
            public int OffsetForTitles { get; set; }
            public IRange Range { get; set; }
            public bool ShouldMove { get; set; }
        }
    }
}
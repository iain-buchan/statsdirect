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
using StatsDirect.R;

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
                int nonHiddenRowCount;
                object[,] probe = GetCellObjects(probeColumn, probeTop, probeBottom, out nonHiddenRowCount);
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

        private void DoOrWarn(Action func, string explanation)
        {
            try
            {
                func();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError(explanation, ex, false);
            }
        }

        private void DoOrSwallow(Action func)
        {
            try
            {
                func();
            }
            catch (Exception)
            {
                // TODO: It'd be nice to know that the exception happened for our diagnostic purposes.
            }
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
        public DataFrame GetCellArray(int rowLengthHint, DataAcquisitionMode mode, int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, bool mightBeBatching, out bool userCancelled, out bool wasPivoted)
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
                            bool wasFiltered;
                            string title = GetColumnTitle(cellSelection.ColumnSelections[c], out dataRows, out gridFirstDataRow, out gridColumn, out titleIsInData);
                            double[] values = GetCellValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1, out wasFiltered);

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
                                using (new DefaultCursor())
                                {
                                    SdApplication.SoleInstance.MsgboxX("You must select numerical data for this function", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Worksheet Data Selection", true);
                                }
                                return null;
                            }
                            variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, gridColumn, cellSelection.ColumnSelections[c].RowIndex, dataRows, mode, titleIsInData, wasFiltered);
                            variable.TruncateDataToLength(size);
                        }
                        break;
                    case DataAcquisitionMode.NumericReplaceMissing:
                        ProcessCellArrayNumericReplaceMissing(cellSelection, mode, frame);
                        break;
                    case DataAcquisitionMode.GroupIdentifiers:
                    case DataAcquisitionMode.CategoryReplaceMissing:
                    case DataAcquisitionMode.CategoryCombineAllColumns:
                    case DataAcquisitionMode.Text:
                        {
                            // code text categories as numbers

                            // is the top row unique (i.e. not duplicated in the column data) and long?  Works out min and max lengths as a side-effect
                            bool isUnique = true;
                            bool isUniqueLength = true;
                            bool isShort = false;
                            int minRowCount = int.MaxValue;
                            int maxRowCount = int.MinValue;
                            for (int c = 0; c < cellSelection.TotalColumns; c++)
                            {
                                CellColumnSelection columnSelection = cellSelection.ColumnSelections[c];
                                int rc = columnSelection.RowCount;
                                if (rc < minRowCount)
                                    minRowCount = rc;
                                if (rc > maxRowCount)
                                    maxRowCount = rc;

                                int gridColumn = columnSelection.ColumnIndex;
                                string qtitle = GetCellText(columnSelection.RowIndex, gridColumn).Trim();
                                int qtitleLength = qtitle.Length;
                                if (qtitleLength < 3)
                                    isShort = true;
                                int nonHiddenRowCount;
                                string[] columnArray = GetCellTexts(gridColumn,
                                                                    columnSelection.RowIndex + 1,
                                                                    columnSelection.RowIndex + columnSelection.RowCount - 1,
                                                                    out nonHiddenRowCount);
                                for (int i = 0; i < nonHiddenRowCount; i++)
                                {
                                    string candidate = columnArray[i];
                                    if (!string.IsNullOrWhiteSpace(candidate))
                                    {
                                        candidate = candidate.Trim();
                                        if (candidate.Equals(qtitle))
                                        {
                                            isUnique = false;
                                            isUniqueLength = false;
                                        }
                                        else if (candidate.Length == qtitleLength)
                                            isUniqueLength = false;
                                        if (!(isUnique || isUniqueLength))
                                            break;
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
                            else if (TopRowIsFormattedLikeTitles(cellSelection))
                                topRow = 1;
                            else if (isUnique)
                            {
                                if (isShort && !isUniqueLength)
                                {
                                    using (new DefaultCursor())
                                    {
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
                                }
                                else
                                {
                                    topRow = 1;
                                }
                            }
                            else
                            {
                                topRow = 0;
                            }

                            // At this point, we know whether we have titles or not.  Now obtain our data strings.
                            string[,] hold = new string[cellSelection.LongestRowCount,cellSelection.TotalColumns];
                            bool wasFiltered = false;
                            for (int c = 0; c < cellSelection.TotalColumns; c++)
                            {
                                int rx = 0;
                                int gridColumn = cellSelection.ColumnSelections[c].ColumnIndex;
                                // get text not entry because we want formulae translated
                                int nonHiddenRowCount;
                                string[] columnArray = GetCellTexts(gridColumn,
                                                                    cellSelection.ColumnSelections[c].RowIndex,
                                                                    cellSelection.ColumnSelections[c].RowIndex + cellSelection.ColumnSelections[c].RowCount - 1,
                                                                    out nonHiddenRowCount);
                                wasFiltered |= nonHiddenRowCount != cellSelection.ColumnSelections[c].RowCount;
                                for (int i = 0; i < nonHiddenRowCount; i++)
                                {
                                    string t = columnArray[i];
                                    string bufr = (null == t) ? "" : t.Trim();
                                    hold[rx++, c] = bufr;
                                }
                                // Fill in blanks for any rows that have been filtered out
                                while (rx < cellSelection.LongestRowCount)
                                    hold[rx++, c] = string.Empty;
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
                                ProcessCellArrayText(cellSelection, mode, frame, topRow, hold, wasFiltered);
                            else if (mode == DataAcquisitionMode.CategoryCombineAllColumns)
                                ProcessCellArrayCategoryCombineAllColumns(cellSelection, mode, frame, topRow, hold, wasFiltered);
                            else
                                ProcessCellArrayCategoriesPerColumn(cellSelection, mode, frame, topRow, hold, wasFiltered);
                        }
                        break;
                    case DataAcquisitionMode.DateReplaceMissing:
                        ProcessCellArrayDateReplaceMissing(cellSelection, mode, frame);
                        break;
                    case DataAcquisitionMode.TextWithFormulae:
                        ProcessCellArrayTextWithFormulae(cellSelection, mode, frame);
                        break;
                    case DataAcquisitionMode.Variant:
                        ProcessCellArrayVariant(cellSelection, mode, frame);
                        break;
                    case DataAcquisitionMode.TextNoTitles:
                        ProcessCellArrayTextNoTitles(cellSelection, mode, frame);
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
                            bool numericValuesWereFiltered;
                            double[] numericValues = GetCellValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1, out numericValuesWereFiltered);

                            // If there's no data in the row (for example if it's hidden), ignore the row
                            if (null == numericValues)
                                continue;

                            int nonHiddenTextRowCount;
                            string[] textValues = GetCellTexts(gridColumn, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowIndex + cellSelection.ColumnSelections[c].RowCount - 1, out nonHiddenTextRowCount);

                            // Find the last row
                            int lastNumericRow;
                            for (lastNumericRow = numericValues.GetUpperBound(0); lastNumericRow >= 0; lastNumericRow--)
                                if (numericValues[lastNumericRow] != Constant.MISSING)
                                    break;
                            int lastTextRow;
                            for (lastTextRow = nonHiddenTextRowCount - 1; lastTextRow >= 0; lastTextRow--)
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
                                variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowCount, mode, titleIsInData, nonHiddenTextRowCount != textValues.Length);
                                if (mode == DataAcquisitionMode.NumericCodingTextToCategories)
                                {
                                    frame.Variables.Add(variable);
                                }
                                else
                                {
                                    // Dummies
                                    DataFrame dummyFrame = Sheet.ToDummyVariables(SdApplication.SoleInstance, variable, true);
                                    if (null != dummyFrame)
                                    {
                                        foreach (Variable v in dummyFrame.Variables)
                                            frame.Variables.Add(v);
                                    }
                                    else
                                        frame.Variables.Add(variable);
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
                                variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, gridColumn, cellSelection.ColumnSelections[c].RowIndex, dataRows, mode, titleIsInData, nonHiddenTextRowCount != textValues.Length);
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
                                            cv.set_Group((int)group.Id, group);
                                        DataFrame dummyFrame;
                                        try
                                        {
                                            dummyFrame = Sheet.ToDummyVariables(SdApplication.SoleInstance, cv, true);
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
                SdApplication.SoleInstance.FriendlyError("Internal error reading data from worksheet", ex, false);
                throw; // TODO: What is the correct behaviour here?  Merely returning null causes a infinite loop
            }
        }

        private void ProcessCellArrayText(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame, int topRow, string[,] hold, bool wasFiltered)
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
                        topRow > 0,
                        wasFiltered);
                }
            }
        }

        private void ProcessCellArrayCategoryCombineAllColumns(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame, int topRow, string[,] hold, bool wasFiltered)
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
                                                  topRow > 0,
                                                  wasFiltered);
        }

        private void ProcessCellArrayCategoriesPerColumn(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame, int topRow, string[,] hold, bool wasFiltered)
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
                    cellSelection.ColumnSelections[c].RowCount,
                    mode,
                    topRow > 0,
                    wasFiltered);
            }
        }

        private void ProcessCellArrayNumericReplaceMissing(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame)
        {
            // Read in the cells replacing missing data with Constant.MISSING
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                int dataRows;
                int gridColumn;
                int gridFirstDataRow;
                bool titleIsInData;
                bool wasFiltered;
                string title = GetColumnTitle(cellSelection.ColumnSelections[c], out dataRows, out gridFirstDataRow, out gridColumn, out titleIsInData);
                double[] values = GetCellValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1, out wasFiltered);

                // If there's no data in the row (for example if it's hidden), ignore the row
                if (null == values)
                    continue;

                // Find the last row
                int lrow;
                for (lrow = values.GetUpperBound(0); lrow >= 0; lrow--)
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
                variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, gridColumn, cellSelection.ColumnSelections[c].RowIndex, dataRows, mode, titleIsInData, wasFiltered);
            }
        }

        private void ProcessCellArrayDateReplaceMissing(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame)
        {
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
                bool wasFiltered;
                DateTime[] values = GetCellDateValues(gridColumn, gridFirstDataRow, gridFirstDataRow + dataRows - 1, out wasFiltered);

                // Find the last row
                int lrow;
                for (lrow = values.GetUpperBound(0); lrow >= 0; lrow--)
                    if (values[lrow] != DateTime.MinValue)
                        break;

                int r = 0;
                variable.Data = new DateTime[lrow + 1];
                for (int row = 0; row <= lrow; row++)
                {
                    variable.Data[r++] = values[row];
                }
                variable.Origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath,
                    cellSelection.ColumnSelections[c].WorksheetName,
                    gridColumn,
                    cellSelection.ColumnSelections[c].RowIndex,
                    dataRows,
                    mode,
                    titleIsInData,
                    wasFiltered);
            }
        }

        private void ProcessCellArrayTextWithFormulae(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame)
        {
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                int nonHiddenRowCount;
                string[] formulae = GetCellFormulae(cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowIndex + cellSelection.ColumnSelections[c].RowCount - 1, out nonHiddenRowCount);
                // Trim any hidden rows before returning the variable - optimised for the very common case that there aren't any
                if (nonHiddenRowCount != formulae.Length)
                {
                    string[] temp = new string[nonHiddenRowCount];
                    Array.Copy(formulae, temp, nonHiddenRowCount);
                    formulae = temp;
                }
                StringVariable variable = new StringVariable(formulae, null);
                IOrigin origin = new WorksheetOrigin(cellSelection.ColumnSelections[c].WorkbookPath, cellSelection.ColumnSelections[c].WorksheetName, cellSelection.ColumnSelections[c].ColumnIndex, cellSelection.ColumnSelections[c].RowIndex, cellSelection.ColumnSelections[c].RowCount, mode, false, nonHiddenRowCount != formulae.Length);
                variable.Origin = origin;
                frame.Variables.Add(variable);
            }
        }

        private void ProcessCellArrayVariant(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame)
        {
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection cs = cellSelection.ColumnSelections[c];
                int dataRows;
                int gridColumn;
                int gridFirstDataRow;
                bool titleIsInData;
                string title = GetColumnTitle(cs, out dataRows, out gridFirstDataRow, out gridColumn, out titleIsInData);

                int nonHiddenRowCount;
                object[,] raw = GetCellObjects(cs.ColumnIndex, cs.RowIndex, cs.RowIndex + cs.RowCount - 1, out nonHiddenRowCount);
                while (nonHiddenRowCount > 0 && null == raw[nonHiddenRowCount - 1, 0])
                    --nonHiddenRowCount;
                object[] cooked = new object[nonHiddenRowCount];
                for (int i = 0; i < nonHiddenRowCount; i++)
                    cooked[i] = raw[i, 0];
                VariantVariable variable = new VariantVariable(cooked, title);
                IOrigin origin = new WorksheetOrigin(cs.WorkbookPath, cs.WorksheetName, cs.ColumnIndex, cs.RowIndex, cs.RowCount, mode, false, nonHiddenRowCount != raw.GetUpperBound(0));
                variable.Origin = origin;
                frame.Variables.Add(variable);
            }
        }

        private void ProcessCellArrayTextNoTitles(CellSelection cellSelection, DataAcquisitionMode mode, DataFrame frame)
        {
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection cs = cellSelection.ColumnSelections[c];
                int nonHiddenRowCount;
                string[] texts = GetCellTexts(cs.ColumnIndex, cs.RowIndex, cs.RowIndex + cs.RowCount - 1, out nonHiddenRowCount);
                // Trim any hidden rows before returning the variable - optimised for the very common case that there aren't any
                if (nonHiddenRowCount != texts.Length)
                {
                    string[] temp = new string[nonHiddenRowCount];
                    Array.Copy(texts, temp, nonHiddenRowCount);
                    texts = temp;
                }
                StringVariable variable = new StringVariable(texts, null)
                {
                    Origin = new WorksheetOrigin(cs.WorkbookPath, cs.WorksheetName, cs.ColumnIndex, cs.RowIndex, cs.RowCount, mode, false, nonHiddenRowCount != texts.Length)
                };
                frame.Variables.Add(variable);
            }
        }

        private bool TopRowIsFormattedLikeTitles(CellSelection cellSelection)
        {
            foreach (CellColumnSelection probe in cellSelection.ColumnSelections)
            {
                if (!CellIsFormattedLikeATitle(probe.WorkbookPath, probe.WorksheetName, probe.ColumnIndex, probe.RowIndex))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// At present, this checks for bold, underline, or quotes.
        /// </summary>
        /// <param name="workbookPath"></param>
        /// <param name="worksheetName"></param>
        /// <param name="columnIndex"></param>
        /// <param name="rowIndex"></param>
        /// <returns></returns>
        private bool CellIsFormattedLikeATitle(string workbookPath, string worksheetName, int columnIndex, int rowIndex)
        {
            // TODO: Cross-workbook cell selections
            workbookView.GetLock();
            try
            {
                IWorksheet worksheet = workbookView.ActiveWorkbookSet.Workbooks[workbookPath].Worksheets[worksheetName];
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

        /// <summary>
        /// Find and return the column title.
        /// Amends ccs.RowIndex and totrows to step past a column title if found.
        /// </summary>
        /// <param name="ccs"></param>
        /// <param name="dataRows">Filled in with the total number of visible and hidden DATA rows.  This may not be the same as the number of non-title rows, as there may be blanks between the titles and the data.</param>
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

            // Find the first data row - anything other than blank is fair game
            int skippedRows = 0;
            for (gridFirstDataRow = firstGridRow; gridFirstDataRow < firstGridRow + dataRows; gridFirstDataRow++)
            {
                if (!string.IsNullOrWhiteSpace(GetCellText(gridFirstDataRow, gridColumn)))
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
        /// <param name="nonHiddenRowCount">The number of non-hidden objects in the array.  Note that raw retrieved values will have been copied down the array to obscure hidden objects in this case; the top end of the array will NOT have been null-filled, so the values in return[nonHiddenRowCount] and above should be considered unknown.</param>
        /// <returns></returns>
        object[,] GetCellObjects(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
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
        string[] GetCellFormulae(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
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

        double[] GetCellValues(int column, int firstRow, int lastRow, out bool wasFiltered)
        {
            if (IsHiddenColumn(column))
            {
                wasFiltered = false;
                return null;
            }

            int nonHiddenRowCount;
            object[,] values = GetCellObjects(column, firstRow, lastRow, out nonHiddenRowCount);

            double[] returnValues = new double[nonHiddenRowCount];
            for (int r = 0; r < nonHiddenRowCount; r++)
                returnValues[r] = ToCellValue(values[r, 0]);
            wasFiltered = nonHiddenRowCount != returnValues.GetUpperBound(0) + 1;
            return returnValues;
        }

        private bool IsHiddenColumn(int column)
        {
            workbookView.GetLock();
            bool isHidden = workbookView.ActiveWorksheet.Cells[0, column].EntireColumn.Hidden;
            workbookView.ReleaseLock();
            return isHidden;
        }

        DateTime[] GetCellDateValues(int column, int firstRow, int lastRow, out bool wasFiltered)
        {
            int nonHiddenRowCount;
            object[,] values = GetCellObjects(column, firstRow, lastRow, out nonHiddenRowCount);

            DateTime[] returnValues = new DateTime[nonHiddenRowCount];
            for (int r = 0; r < nonHiddenRowCount; r++)
                returnValues[r] = ToCellDateValue(values[r, 0]);
            wasFiltered = nonHiddenRowCount != lastRow - firstRow + 1;
            return returnValues;
        }

        /// <summary>
        /// Obtain a raw array of display strings from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        string[] GetCellTexts(int column, int firstRow, int lastRow, out int nonHiddenRowCount)
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
                    worksheet.WindowInfo.SplitColumns = 0;
                    worksheet.WindowInfo.SplitRows = 0;
                    info.FreezePanes = false;
                }
                else
                {
                    int row = workbookView.ActiveCell.Row - worksheet.WindowInfo.ScrollRow;
                    int column = workbookView.ActiveCell.Column - worksheet.WindowInfo.ScrollColumn;

                    worksheet.WindowInfo.SplitColumns = column;
                    worksheet.WindowInfo.SplitRows = row;

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
            DataFrame frame = GetCellArray(0, DataAcquisitionMode.Variant, 1, 10000, "Select the data to be placed on the clipboard", null, false, false, out userCancelled, out wasPivoted);
            if (userCancelled)
                return;
            StringBuilder sb = new StringBuilder();
            RConvert.ToR(sb, "copied.data", frame);
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
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.R;
using StatsDirect.Templates;
using StatsDirect.Utilities;

using SpreadsheetGear;
using SpreadsheetGear.Advanced.Cells;
using SpreadsheetGear.Commands;
using SpreadsheetGear.Windows.Forms;
using Color = System.Drawing.Color;
using SystemColors = System.Drawing.SystemColors;
using StatsDirect.Charting;

namespace StatsDirect.UI
{
    internal partial class frmSpreadsheetGear : StatsDirectForm, IGrid, IGetCells
    {
        /// <summary>
        /// While batching, a storage point for the data area we've most recently selected.
        /// </summary>
        private CellSelection? mostRecentCellSelectionDuringBatch;

        private ISdPreferences SdPreferences { get; }
        private ITemplateProcessorFactory TemplateProcessorFactory { get; }
        private IUiPreferences UiPreferences { get; }
        private IUserInterface UserInterface { get; }

        public frmSpreadsheetGear(ISdApplication sdApplication, ISdPreferences sdPreferences, ITemplateProcessorFactory templateProcessorFactory, IUiPreferences uiPreferences, IUserInterface userInterface)
            : base(sdApplication)
        {
            SdPreferences = sdPreferences;
            TemplateProcessorFactory = templateProcessorFactory;
            UiPreferences = uiPreferences;
            UserInterface = userInterface;
            InitializeComponent();
            // See http://stackoverflow.com/questions/23637869/spreadsheetgear-for-winforms-paste-from-excel-removes-validation-on-target-cell - Tim Andersen's solution to adding a command manager to a workbook set.
            new ClipboardManglingCommandManager(workbookView.ActiveWorkbookSet);
            SdApplication.EnsureBuiltInMenuItemsCanShowHelp(menuStrip);
            workbookView.WithLock(() =>
            {
                workbookView.ActiveWorkbook?.Close();
                Charting.FontDescriptor? defaultWorkbookFontDescriptor = uiPreferences.DefaultWorkbookFont;
                if (null != defaultWorkbookFontDescriptor)
                {
                    workbookView.ActiveWorkbookSet.DefaultFontName = defaultWorkbookFontDescriptor.FontFamily;
                    workbookView.ActiveWorkbookSet.DefaultFontSize = defaultWorkbookFontDescriptor.SizeInPoints;
                }
                else
                {
                    // TODO: Parameterise default-default workbook font.
                    workbookView.ActiveWorkbookSet.DefaultFontName = "Calibri";
                    workbookView.ActiveWorkbookSet.DefaultFontSize = 11;
                }
                workbookView.ActiveWorkbook = workbookView.ActiveWorkbookSet.Workbooks.Add();
            });
        }

        private void frmSpreadsheetGear_FormClosing(object? sender, FormClosingEventArgs e)
        {
            DoOrWarn(() =>
            {
                if (!AllowClose())
                {
                    e.Cancel = true;
                    return;
                }
                SdApplication.NoteFormClosing(this, e);
                Visible = false;
                MdiParent = null;
            }, "Couldn't close form");
        }

        private void frmSpreadsheetGear_Activated(object? sender, EventArgs e)
        {
            if (null != Tag)
                SdApplication.NoteFormActivated((WindowInformation)Tag);
            tableLayoutPanel1.Visible = true;
        }

        private void workbookView_CellEndEdit(object? sender, CellEndEditEventArgs e)
        {
            Dirty = true;
        }

        private void workbookView_RangeChanged(object? sender, RangeChangedEventArgs e)
        {
            Dirty = true;
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
            return workbookView.WithLock(() =>
            {
                workbookView.ActiveWorkbook.Save();
                Dirty = false;
                return true;
            });
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
            DialogResult result = saveFileDialog.ShowDialog(SdApplication.DialogOwner);
            if (DialogResult.Cancel == result)
            {
                // User cancelled, failed save
                return false;
            }
            // User wants to save the file
            Path = saveFileDialog.FileName;
            string? extension = System.IO.Path.GetExtension(Path);
            if (!string.IsNullOrEmpty(extension))
                extension = extension.ToLower(CultureInfo.InvariantCulture);
            FileFormat format = ".xlsx".Equals(extension) ? FileFormat.OpenXMLWorkbook : FileFormat.Excel8;
            return workbookView.WithLock(() =>
            {
                workbookView.ActiveWorkbook.SaveAs(path, format);
                Dirty = false;
                SdApplication.NoteRecentFile(path, true);
                return true;
            });
        }

        public override bool OpenFile(string filename, bool isTempFile, string nameToDisplay)
        {
            if (null != workbookView.ActiveWorkbook)
                workbookView.WithLock(() => workbookView.ActiveWorkbook.Close());
            return workbookView.WithLock(() =>
            {
                IWorkbook wb = workbookView.ActiveWorkbookSet.Workbooks.Open(filename);
                if (!isTempFile)
                    Path = filename;
                if (null != wb)
                    workbookView.ActiveWorkbook = wb;
                if (isTempFile && null != nameToDisplay)
                    SetUnsavedName(nameToDisplay);
                return null != wb;
            });
        }

        bool IGrid.Dirty => Dirty;

        object?[,] IGrid.GetValues(int top, int left, int bottom, int right)
        {
            return workbookView.WithLock(() =>
            {
                object val = workbookView.ActiveWorksheet.Cells[top, left, bottom, right].Value;
                if (null != val && val.GetType().IsArray)
                    return (object?[,])val;
                return new[,] { { val } };
            });
        }

        string IGrid.ActiveWorksheetName
        {
            get
            {
                return workbookView.WithLock(() => workbookView.ActiveWorksheet.Name);
            }
        }

        string IGrid.WorkbookPath
        {
            get
            {
                return workbookView.WithLock(() => workbookView.ActiveWorkbook.FullName);
            }
        }

        void IGrid.SetValues(int top, int left, object[,] values)
        {
            int rowsMinusOne = values.GetUpperBound(0) - values.GetLowerBound(0);
            int colsMinusOne = values.GetUpperBound(1) - values.GetLowerBound(1);
            workbookView.WithLock(() => workbookView.ActiveWorksheet.Cells[top, left, top + rowsMinusOne, left + colsMinusOne].Value = values);
            Dirty = true;
        }

        void IGrid.Refill(List<IVariable> variables)
        {
            CellSelection revisedCellSelection = new();

            foreach (IVariable variable in variables)
            {
                WorksheetOrigin? worksheetOrigin = (WorksheetOrigin?)variable.Origin;

                // Set the active worksheet
                workbookView.ActiveWorkbookSet.WithLock(() =>
                {
                    string sheetName = worksheetOrigin.WorksheetName;
                    IWorksheet worksheet = workbookView.ActiveWorkbook.Worksheets[sheetName];
                    bool succeeded = null != worksheet;
                    if (!succeeded)
                        throw new Exception("Cannot refill variable as the worksheet \"" + worksheetOrigin.WorksheetName + "\" in workbook \"" + worksheetOrigin.WorkbookPath + "\" no longer exists.");
                    workbookView.ActiveSheet = worksheet;
                });

                CellColumnSelection cellColumnSelection = new(this) { ColumnIndex = worksheetOrigin.Column, RowCount = worksheetOrigin.Rows, RowIndex = worksheetOrigin.TopRow };
                MaybeExpandCellColumnSelection(cellColumnSelection);
                revisedCellSelection.ColumnSelections.Add(cellColumnSelection);
                revisedCellSelection.LongestRowCount = cellColumnSelection.RowCount;
            }
            WorksheetOrigin firstWorksheetOrigin = (WorksheetOrigin)variables[0].Origin;
            DataFrame? refilledFrame = new CellArrayProcessor(SdApplication).ProcessCellArray(revisedCellSelection, firstWorksheetOrigin.Mode, 0, true, firstWorksheetOrigin.HasTitle, firstWorksheetOrigin.OriginGroup, ((WindowInformation)Tag).FriendlyName);
            if (null == refilledFrame)
                throw new Exception("Cannot refill frame; have you deleted some variables?");
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
                (object?[,] probe, int nonHiddenRowCount) = GetCellObjects(cellColumnSelection.ColumnIndex, probeTop, probeBottom);
                for (int offset = 0; offset < nonHiddenRowCount; offset++)
                {
                    object? value = probe[offset, 0];
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

            workbookView.WithLock(() =>
            {
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

                // If any variable has a title, leave a title row.
                int offsetForTitles = 0;
                foreach (IVariable v in frame.Variables)
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
                    case RelativePosition.ReplaceSelection:
                        // The first column is the first column of the current selection
                        firstColumnOfData = workbookView.RangeSelection.Column;
                        // Destroy existing contents
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
                        throw new ArgumentOutOfRangeException(nameof(writePosition), writePosition, "Unknown write position");
                }

                IRange range = worksheet.Range[0, firstColumnOfData, usedRange.Row + usedRange.RowCount + offsetForTitles - 1, firstColumnOfData + frame.VariableCount - 1];
                // Create a holder that can be captured by the lambda but then can be cleared out so that it doesn't retain large amounts of data
                WriteDataFrameParametersHolder holder = new() { Frame = frame, IsFormulae = isFormulae, MissingIndicator = missingIndicator, Range = range, ShouldMove = shouldMove, OffsetForTitles = offsetForTitles };
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(range.EntireColumn, "Insert data", () => { if (null != holder.Frame) WriteDataFrameInternal(holder.Frame, holder.IsFormulae, holder.MissingIndicator, holder.Range, holder.ShouldMove, holder.OffsetForTitles); return true; }));
                workbookView.Focus();
            });
        }

        private class VariableInsertionVisitor : IVariableVisitor
        {
            public int Column { get; set; }
            public bool IsFormulae { get; set; }
            public string? MissingIndicator { get; set; }
            public int OffsetForTitles { get; set; }
            public IValues? Values { get; set; }

            public void Visit(GenericVariable<bool> variable)
            {
                bool[]? data = variable.Data;
                if (null != data)
                {
                    for (int i = 0; i < data.Length; i++)
                        Values.SetNumber(i + OffsetForTitles, Column, data[i] ? 1 : 0);
                }
            }

            public void Visit(DoubleVariable variable)
            {
                double[]? data = variable.Data;
                if (null != data)
                {
                    for (int i = 0; i < data.Length; i++)
                        if (Constant.MISSING == data[i] || double.IsNaN(data[i]))
                            Values.SetText(i + OffsetForTitles, Column, MissingIndicator);
                        else
                            Values.SetNumber(i + OffsetForTitles, Column, data[i]);
                }
            }

            public void Visit(GenericVariable<object> variable)
            {
                object[]? data = variable.Data;
                if (null != data)
                {
                    for (int i = 0; i < data.Length; i++)
                        if (data[i] is double val)
                        {
                            if (Constant.MISSING == val || double.IsNaN(val))
                                Values.SetText(i + OffsetForTitles, Column, MissingIndicator);
                            else
                                Values.SetNumber(i + OffsetForTitles, Column, val);
                        }
                        else if (data[i] is bool)
                            Values.SetLogical(i + OffsetForTitles, Column, (bool)data[i]);
                        else if (null == data[i])
                            Values.Clear(i + OffsetForTitles, Column);
                        else
                            Values.SetText(i + OffsetForTitles, Column, data[i].ToString());
                }
            }

            public void Visit(StringVariable variable)
            {
                string?[]? data = variable.Data;
                for (int i = 0; i < data.Length; i++)
                {
                    string value = data[i] ?? string.Empty;
                    if (IsFormulae && value.Length > 0)
                    {
                        try
                        {
                            Values.SetFormula(i + OffsetForTitles, Column, value);
                        }
                        catch (ArgumentException)
                        {
                            // Almost certainly trying to set something that's not legal as a formula.  Try it as text instead.
                            Values.SetText(i + OffsetForTitles, Column, value);
                        }
                    }
                    else
                    {
                        Values.SetText(i + OffsetForTitles, Column, value);
                    }
                }
            }

            public void Visit(GenericVariable<DateTime> variable)
            {
                DateTime[]? data = variable.Data;
                if (null != data)
                {
                    for (int i = 0; i < data.Length; i++)
                        if (DateTime.MinValue == data[i])
                            Values.SetText(i + OffsetForTitles, Column, MissingIndicator);
                        else
                            Values.SetNumber(i + OffsetForTitles, Column, data[i].ToOADate());
                }
                // TODO: Set date formatting for range
            }

            public void Visit(ClassifierVariable variable)
            {
                double[]? data = variable.Data;
                if (null != data)
                {
                    for (int i = 0; i < data.Length; i++)
                        Values.SetText(i + OffsetForTitles, Column,
                                       Constant.MISSING == data[i]
                                           ? MissingIndicator
                                           : variable.Groups[(int)data[i]].Label);
                }
            }
        }

        private void WriteDataFrameInternal(DataFrame frame, bool isFormulae, string missingIndicator, IRange range, bool shouldMove, int offsetForTitles)
        {
            workbookView.WithLock(() =>
            {
                IWorksheet worksheet = workbookView.ActiveWorksheet;
                IValues values = (IValues)worksheet;
                // Work out the first column we're going to insert into, moving the existing contents out of the way if we're inserting in existing data
                int firstColumnOfData = range.Column;
                if (shouldMove)
                {
                    // Move existing contents out of the way
                    range.Insert(InsertShiftDirection.Right);
                }
                else
                {
                    // We might be in clear space at the end of the sheet, or we might be replacing an existing selection.  Either way, blank the contents of the existing selection.
                    range.ClearContents();
                }

                // Now do the inserts.  Frames may contain any types of variables.
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    IVariable variable = frame.Variables[v];
                    string? title = variable.Title;
                    if (null != title)
                        values.SetText(0, firstColumnOfData + v, title);
                    variable.Accept(new VariableInsertionVisitor { Column = firstColumnOfData + v, IsFormulae = isFormulae, OffsetForTitles = offsetForTitles, MissingIndicator = missingIndicator, Values = values });
                }
                IRange insertedRange = range[0, 0, frame.MaxRows + offsetForTitles - 1, range.ColumnCount - 1];
                insertedRange.Select();
                insertedRange.NumberFormat = string.Empty;
                insertedRange.Columns.AutoFit();
                Dirty = true;
            });
        }

        Area IGrid.UsedArea
        {
            get
            {
                return workbookView.WithLock(() =>
                {
                    IRange rawRange = workbookView.ActiveWorksheet.UsedRange;
                    return IRangeToArea(this, rawRange);
                });
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
                    workbookView.WithLock(() =>
                    {
                        newDataRange.Select();
                    });
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
            workbookView.WithLock(() => workbookView.ActiveCell.Select());
        }

        private static Area IRangeToArea(IGrid grid, IRange range)
        {
            return new Area(grid, range.Row, range.Column, range.Row + range.RowCount - 1, range.Column + range.ColumnCount - 1);
        }

        private void DoOrWarn(Action func, string explanation)
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
                SdApplication.FriendlyError(explanation, ex, false);
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
            catch (Exception ex)
            {
                LastChanceCatcher.WriteToBlackbox("Unexpected exception in spreadsheet form", ex);
            }
#endif
        }

        private void closeToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(Close, "Couldn't close workbook");
        }

        private void saveToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(() => SaveContents(), "Couldn't save workbook");
        }

        private void saveAsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(() => SaveAsContents(), "Couldn't save workbook");
        }

        private class CellSelectionResult
        {
            public CellSelection? CellSelection { get; set; }
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
        private CellSelectionResult GetSelection(int minimumColumns, int maximumColumns, string? selectionMessage, string? cancelButtonLabel, bool allowUserToPivot, bool selectionWasDefaulted)
        {
            // Handle default selections: we may need to come in with a pre-selected area and force the user to confirm it.  However, if the user gives an illegal selection, we need to give errors.  So we keep a state of whether the selection presently on the grid is the default or is user-selected.
            bool selectionIsDefault = selectionWasDefaulted;
            const string msgTi = "StatsDirect Data Selection";

            // Get the number of selections & Last Row, Col info
            CellSelection sel = new();
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
                                CellColumnSelection ccs = new(this) { WorkbookPath = grid.WorkbookPath, WorksheetName = grid.ActiveWorksheetName, ColumnIndex = c, RowIndex = usedFrom.Top, RowCount = rows };
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
                    new GridSelectionProcessor(this, SdApplication, TemplateProcessorFactory, UiPreferences, UserInterface).SelNumWarn(minimumColumns, maximumColumns, sel.TotalColumns, msgTi);
                string fullSelectionMessage = selectionMessage ?? string.Empty;
                if (minimumColumns == maximumColumns)
                    fullSelectionMessage += " (" + minimumColumns.ToString() + " column" + (minimumColumns > 1 ? "s" : string.Empty) + ")";
                else
                    fullSelectionMessage += " (Min " + minimumColumns.ToString() + ": Max " + maximumColumns.ToString() + ")";
                Color oldBackColor = BackColor;
                BackColor = SystemColors.Info;
                try
                {
                    if (!SdApplication.SelectCells(fullSelectionMessage, cancelButtonLabel, maximumColumns > 1, allowUserToPivot, out bool wasPivoted))
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
        /// <param name="originGroup"></param>
        /// <returns></returns>
        public DataFrame? GetCellArray(int rowLengthHint, DataAcquisitionMode mode, int minimumColumns, int maximumColumns, string? selectionMessage, string? cancelButtonLabel, bool allowUserToPivot, bool mightBeBatching, out bool userCancelled, out bool wasPivoted, int originGroup)
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
            CellSelection? cellSelection = cellSelectionResult.CellSelection;

            if (mightBeBatching)
                mostRecentCellSelectionDuringBatch = cellSelection;

            if (null == cellSelection)
            {
                // No selection
                return null;
            }

            return new CellArrayProcessor(SdApplication).ProcessCellArray(cellSelection, mode, rowLengthHint, false, false, originGroup, ((WindowInformation)Tag).FriendlyName);
        }

        /// <summary>
        /// The guts of the data conversion to a double.
        /// This will be inlined in optimised code.
        /// </summary>
        /// <param name="val">The value to be converted</param>
        /// <returns>MISSING if the value could not be converted, MISSING * 10 if the value was previously MISSING, or the converted value</returns>
        public static double ToCellValue(object? val)
        {
            switch (val)
            {
                case null:
                    return Constant.MISSING;
                case double v:
                    return v;
                case int i:
                    return i;
                case string buf:
                    {
                        if (double.TryParse(buf, out double dval))
                            return dval;
                        string ubuf = buf.ToUpper(CultureInfo.InvariantCulture);
                        if (ubuf == "*" || ubuf == "MISSING" || ubuf == ".")
                            return Constant.MISSING * 10D;
                        // if (ubuf == "#NULL!" || ubuf == "#NUM!")
                        //    return Constant.MISSING;
                        return Constant.MISSING; // If we can't parse it as a number, and we want numbers, it's MISSING.
                    }
                default:
                    // throw new NotImplementedException("Don't know how to handle type " + val.GetType().FullName);
                    return Constant.MISSING;
            }
        }

        /// <summary>
        /// The guts of the data conversion to a DateTime.
        /// This will be inlined in optimised code.
        /// </summary>
        /// <param name="val">The value to be converted</param>
        /// <returns>DateTime.MinValue if the value could not be converted, or the converted value</returns>
        public static DateTime ToCellDateValue(object? val)
        {
            return val switch
            {
                null => DateTime.MinValue,
                DateTime dt => dt,
                double d => DateTime.FromOADate(d),
                int i => DateTime.FromOADate(i),
                string s => DateTime.TryParse(s, out DateTime parsed)
                    ? parsed
                    : DateTime.MinValue,
                _ => DateTime.MinValue
            };
        }

        /// <summary>
        /// Get the single cell value at the specified row and column.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="column"></param>
        /// <returns>MISSING if the value could not be converted, MISSING * 10 if the value was previously MISSING, or the converted value</returns>
        double IGetCells.GetCellValue(int row, int column)
        {
            return workbookView.WithLock(() => ToCellValue(workbookView.ActiveWorksheet.Cells[row, column].Value));
        }

        string? IGetCells.GetCellText(int row, int column)
        {
            return workbookView.WithLock(() =>
            {
                object val = workbookView.ActiveWorksheet.Cells[row, column].Value;
                return null == val ? string.Empty : val.ToString();
            });
        }

        /// <summary>
        /// Obtain a raw array of objects from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        public (object?[,], int) GetCellObjects(int column, int firstRow, int lastRow)
        {
            if (lastRow < firstRow)
                return (new object[0, 0], 0);

            return workbookView.WithLock(() =>
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
                    return (valArray, validRows);
                }

                // If we get here, the returned value is a single cell.  It may still be hidden.
                return (
                    new[,] { { val } },
                    workbookView.ActiveWorksheet.Cells[firstRow, column].EntireRow.Hidden ? 0 : 1
                );
            });
        }

        /// <summary>
        /// Obtain a raw array of formulae from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        (string[], int) IGetCells.GetCellFormulae(int column, int firstRow, int lastRow)
        {
            return workbookView.WithLock(() =>
            {
                string[] result = new string[lastRow - firstRow + 1];
                int validRows = 0;
                for (int row = firstRow; row <= lastRow; row++)
                {
                    bool hidden = workbookView.ActiveWorksheet.Cells[row, column].EntireRow.Hidden;
                    if (!hidden)
                        result[validRows++] = workbookView.ActiveWorksheet.Cells[row, column].Formula;
                }
                return (result, validRows);
            });
        }

        (double[]?, int) IGetCells.GetCellValues(int column, int firstRow, int lastRow)
        {
            if (IsHiddenColumn(column))
                return (null, 0);

            (object?[,] values, int nonHiddenRowCount) = GetCellObjects(column, firstRow, lastRow);

            double[] returnValues = new double[nonHiddenRowCount];
            for (int r = 0; r < nonHiddenRowCount; r++)
                returnValues[r] = ToCellValue(values[r, 0]);
            return (returnValues, nonHiddenRowCount);
        }

        private bool IsHiddenColumn(int column)
        {
            return workbookView.WithLock(() => workbookView.ActiveWorksheet.Cells[0, column].EntireColumn.Hidden);
        }

        (DateTime[], int) IGetCells.GetCellDateValues(int column, int firstRow, int lastRow)
        {
            (object?[,] values, int nonHiddenRowCount) = GetCellObjects(column, firstRow, lastRow);

            DateTime[] returnValues = new DateTime[nonHiddenRowCount];
            for (int r = 0; r < nonHiddenRowCount; r++)
                returnValues[r] = ToCellDateValue(values[r, 0]);
            return (returnValues, nonHiddenRowCount);
        }

        /// <summary>
        /// Obtain a raw array of display strings from SpreadsheetGear, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns>(dispplay strings, hidden row count)</returns>
        (string[], int) IGetCells.GetCellTexts(int column, int firstRow, int lastRow)
        {
            return workbookView.WithLock(() =>
            {
                string[] returnedValues = new string[lastRow - firstRow + 1];
                int validRows = 0;
                for (int i = 0; i <= lastRow - firstRow; i++)
                {
                    bool hidden = workbookView.ActiveWorksheet.Cells[firstRow + i, column].EntireRow.Hidden;
                    if (!hidden)
                        returnedValues[validRows++] = workbookView.ActiveWorksheet.Cells[firstRow + i, column].Text;
                }
                return (returnedValues, validRows);
            });
        }

        /// <summary>
        /// Fake a column name for this column: A for 0, B for 1, AA for 26, BA for 52 etc.
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        string IGetCells.GetColumnTitle(int col)
        {
            const int A = 65;
            int prefixValue = col / 26 - 1;
            int suffixValue = col % 26;
            if (prefixValue < 0)
                return new string(new[] { (char)(suffixValue + A) });
            return new string(new[] { (char)(prefixValue + A), (char)(suffixValue + A) });
        }

        private void workbookView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (SdApplication.IsSelecting && e.KeyCode == Keys.Enter)
            {
                // This changes the state both for selecting and for data input, but is OK because we only get here if we're selecting.
                SdApplication.NoteEndOfSelection(true);
                e.Handled = true;
            }
        }

        public override IList<Pane> AvailablePanes
        {
            get
            {
                WindowInformation info = WindowInformation;
                string prefix = Text;
                List<Pane> panes = new();
                workbookView.WithLock(() =>
                {
                    foreach (IWorksheet sheet in workbookView.ActiveWorkbook.Worksheets)
                        panes.Add(new Pane(prefix + " " + sheet.Name, info, sheet.Name));
                });
                panes.Add(new Pane("New sheet in " + prefix, info, null));
                return panes;
            }
        }

        /// <summary>
        /// At present, this checks for bold, underline, or quotes.
        /// </summary>
        /// <param name="columnIndex"></param>
        /// <param name="rowIndex"></param>
        /// <returns></returns>
        bool IGetCells.IsFormattedLikeATitle(int columnIndex, int rowIndex)
        {
            // TODO: Cross-workbook cell selections
            return workbookView.WithLock(() =>
            {
                IWorksheet worksheet = workbookView.ActiveWorksheet;
                IRange cell = worksheet.Range[rowIndex, columnIndex];
                if (cell.Font.Bold || cell.Font.Underline != UnderlineStyle.None)
                    return true;
                object? oV = cell.Value;
                if (null == oV)
                    return false;
                string? v = oV.ToString();
                return v.StartsWith("\"") || v.StartsWith("'");
            });
        }

        public override bool SelectPane(Pane pane)
        {
            bool succeeded = true;
            workbookView.ActiveWorkbookSet.WithLock(() =>
            {
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
                    succeeded = null != worksheet;
                    workbookView.ActiveSheet = worksheet;
                }
            });
            return succeeded;
        }

        public override Pane SelectedPane
        {
            get
            {
                WindowInformation info = WindowInformation;
                string prefix = Text;
                return workbookView.ActiveWorkbookSet.WithLock(() =>
                {
                    IWorksheet sheet = workbookView.ActiveWorksheet;
                    return new Pane(prefix + " " + sheet.Name, info, sheet.Name);
                });
            }
        }

        private void cutToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(workbookView.Cut, "Cut failed");
        }

        private void copyToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(workbookView.Copy, "Copy failed");
        }

        private void pasteToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(workbookView.Paste, "Paste failed");
        }

        private void pasteSpecialToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(PasteSpecial, "Paste Special failed");
        }

        private void PasteSpecial()
        {
            using frmPasteSpecial frm = new();
            frm.ShowDialog(this);
            if (!frm.UserCancelled)
                workbookView.PasteSpecial(frm.PasteType, PasteOperation.None, false, false);
        }

        private void printToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(Print, "Print failed");
        }

        private void printPreviewToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(workbookView.PrintPreview, "Print preview failed");
        }

        private void pageSetupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(PageSetup, "Page setup failed");
        }

        private void PageSetup()
        {
            if (null != workbookView && null != workbookView.ActiveWorksheet)
            {
                IPageSetup pageSetup = workbookView.ActiveWorksheet.PageSetup;

                // Show the dialog
                using PageSetupDialog pageSetupDialog = new();
                workbookView.WithLock(() =>
                {
                    // Pull settings into the page setup dialog
                    pageSetupDialog.PageSettings = new System.Drawing.Printing.PageSettings { Color = !pageSetup.BlackAndWhite, Landscape = pageSetup.Orientation == PageOrientation.Landscape, Margins = { Top = PointsToHundredths(pageSetup.TopMargin), Bottom = PointsToHundredths(pageSetup.BottomMargin), Left = PointsToHundredths(pageSetup.LeftMargin), Right = PointsToHundredths(pageSetup.RightMargin) } };
                    // pageSetupDialog.PageSettings.PaperSize = pageSetup.PaperSize;
                });

                pageSetupDialog.AllowOrientation = true;
                pageSetupDialog.AllowMargins = true;
                // pageSetupDialog.AllowPaper = true;
                DialogResult res = pageSetupDialog.ShowDialog(SdApplication.DialogOwner);
                if (res == DialogResult.OK)
                {
                    // Save settings into SSG's sheet settings
                    workbookView.WithLock(() =>
                    {
                        pageSetup.BlackAndWhite = !pageSetupDialog.PageSettings.Color;
                        pageSetup.Orientation = pageSetupDialog.PageSettings.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                        pageSetup.TopMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Top);
                        pageSetup.BottomMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Bottom);
                        pageSetup.LeftMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Left);
                        pageSetup.RightMargin = HundredthsToPoints(pageSetupDialog.PageSettings.Margins.Right);
                        // pageSetupDialog.PageSettings.PaperSize = pageSetup.PaperSize;
                    });
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

        private void undoToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(Undo, "Undo failed");
        }

        private void Undo()
        {
            if (workbookView.ActiveCommandManager.CanUndo)
                workbookView.ActiveCommandManager.Undo();
        }

        private void cellsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(ShowRangeExplorer, "Format cells failed");
        }

        private void sheetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(InsertSheet, "Insert sheet failed");
        }

        private void InsertSheet()
        {
            workbookView.WithLock(() => workbookView.ActiveWorksheet = workbookView.ActiveWorkbook.Worksheets.AddBefore(workbookView.ActiveWorksheet));
        }

        private void rowToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(InsertRow, "Insert row failed");
        }

        private void InsertRow()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection;
                if (currentRange.IsEntireColumns)
                {
                    SdApplication.MsgboxX("You cannot insert an entire worksheet's height of blank rows.  Please select fewer rows.", MessageBoxButtons.OK, MessageBoxIcon.Error, "Insert rows", false);
                    return;
                }

                currentRange = currentRange.EntireRow;
                currentRange.Insert();
            });
        }

        private void columnToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(InsertColumn, "Insert column failed");
        }

        private void InsertColumn()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection;
                if (currentRange.IsEntireRows)
                {
                    SdApplication.MsgboxX("You cannot insert an entire worksheet's width of blank columns.  Please select fewer columns.", MessageBoxButtons.OK, MessageBoxIcon.Error, "Insert columns", false);
                    return;
                }

                currentRange = currentRange.EntireColumn;
                currentRange.Insert();
            });
        }

        private void cellsToolStripMenuItem1_Click(object? sender, EventArgs e)
        {
            DoOrWarn(InsertCells, "Insert cells failed");
        }

        private void InsertCells()
        {
            using frmInsertCells frm = new();
            frm.ShowDialog(this);
            if (!frm.UserCancelled)
            {
                workbookView.WithLock(() =>
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
                });
            }
        }

        private void sheetSettingsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(SheetSettings, "Sheet settings failed");
        }

        private void SheetSettings()
        {
            workbookView.WithLock(() =>
            {
                IWorkbookSet workbookSet = workbookView.ActiveWorkbookSet;
                WorkbookExplorer explorer = new(workbookSet) { Text = "Sheet settings" };
                explorer.Show(workbookView);
            });
        }

        private void findToolStripMenuItem_Click(object? sender, EventArgs e)
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

        private void replaceToolStripMenuItem_Click(object? sender, EventArgs e)
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

        private void goToCellToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(GoToCell, "Go to cell failed");
        }

        private void GoToCell()
        {
            string cell = SdApplication.GetString("Enter the cell address, for example G54", "Go to cell", string.Empty);
            workbookView.WithLock(() =>
            {
                if (null != cell)
                    workbookView.ActiveWorksheet.Cells[cell].Activate();
            });
        }

        private void clearSelectionToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(ClearSelectedCells, "Clear selection failed");
        }

        private void ClearSelectedCells()
        {
            workbookView.Focus();
            SendKeys.Send("{DEL}");
            Application.DoEvents(); // Force processing of events, in this case clearing the selection
        }

        private void deleteSpecialToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(DeleteSpecial, "Delete Special failed");
        }

        private void DeleteSpecial()
        {
            using frmDeleteSpecial frm = new();
            frm.ShowDialog(this);
            if (!frm.UserCancelled)
            {
                workbookView.WithLock(() =>
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
                });
            }

        }

        private void deleteColumnToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(DeleteColumn, "Delete column failed");
        }

        private void DeleteColumn()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection.EntireColumn;
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(currentRange, "Delete column", () => { currentRange.Delete(); return true; }));
            });
        }

        private void deleteRowToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(DeleteRow, "Delete row failed");
        }

        private void DeleteRow()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection.EntireRow;
                workbookView.ActiveCommandManager.Execute(new UndoWrapper(currentRange, "Delete row", () => { currentRange.Delete(); return true; }));
            });
        }

        private void deleteSheetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(DeleteSheet, "Delete sheet failed");
        }

        private void DeleteSheet()
        {
            bool shouldDelete = SdApplication.Query("This will delete the current sheet. You cannot undo this operation. Are you sure you want to delete this sheet?", "Delete sheet");
            if (shouldDelete)
                workbookView.WithLock(() => workbookView.ActiveWorksheet.Delete());
        }

        private void describeColumnDataToolStripMenuItem_Click(object? sender, EventArgs e)
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
                    workbookView.WithLock(() =>
                    {
                        IRange cellSelection = workbookView.RangeSelection;
                        cellSelection.EntireColumn.Select();
                    });
                }
                DoOperation("QuickSummary");
            }
            catch (CancelCurrentOperationAndDoException)
            {
                // Give up!
            }
        }

        private void fillDownToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(EditFillDown, "Fill down failed");
        }

        private void EditFillDown()
        {
            workbookView.WithLock(() => workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection, "Fill down", () => { workbookView.RangeSelection.FillDown(); return true; })));
        }

        private void fillRightToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(EditFillRight, "Fill right failed");
        }

        private void EditFillRight()
        {
            workbookView.WithLock(() => workbookView.ActiveCommandManager.Execute(new UndoWrapper(workbookView.RangeSelection, "Fill right", () => { workbookView.RangeSelection.FillRight(); return true; })));
        }

        private void rowHideToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(HideRow, "Hide row failed");
        }

        private void HideRow()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection.EntireRow;
                currentRange.Hidden = true;
            });
        }

        private void rowUnhideToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(UnhideRow, "Unhide row failed");
        }

        private void UnhideRow()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection.EntireRow;
                currentRange.Hidden = false;
            });
        }

        private void columnHideToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(HideColumn, "Hide column failed");
        }

        private void HideColumn()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection.EntireColumn;
                currentRange.Hidden = true;
            });
        }

        private void columnUnhideToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(UnhideColumn, "Unhide column failed");
        }

        private void UnhideColumn()
        {
            workbookView.WithLock(() =>
            {
                IRange currentRange = workbookView.RangeSelection.EntireColumn;
                currentRange.Hidden = false;
            });
        }

        private void freezePanesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(FreezePanes, "Freeze panes failed");
        }

        private void FreezePanes()
        {
            workbookView.WithLock(() =>
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
            });
        }

        private void autoFitWidthToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(AutoFitWidth, "Auto-fit failed");
        }

        private void AutoFitWidth()
        {
            workbookView.WithLock(() => workbookView.RangeSelection.Columns.AutoFit());
        }

        private void defaultFontToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(SetDefaultFont, "Set default font failed");
        }

        private void SetDefaultFont()
        {
            FontDialog dlg = new()
            {
                Font = new Font(workbookView.ActiveWorkbookSet.DefaultFontName, (float)workbookView.ActiveWorkbookSet.DefaultFontSize),
                ShowApply = false,
                ShowEffects = false,
                ShowHelp = false
            };
            DialogResult result = dlg.ShowDialog(this);
            if (DialogResult.OK == result)
            {
                workbookView.WithLock(() =>
                {
                    workbookView.ActiveWorkbookSet.DefaultFontName = dlg.Font.FontFamily.Name;
                    workbookView.ActiveWorkbookSet.DefaultFontSize = dlg.Font.SizeInPoints;
                });
                UiPreferences.DefaultWorkbookFont = FontCache.DescriptorFromFont(dlg.Font);
                UiPreferences.Save();
            }
        }

        private void lockSheetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(LockSheet, "Lock sheet failed");
        }

        private void LockSheet()
        {
            workbookView.WithLock(() =>
            {
                workbookView.ActiveWorksheet.ProtectContents = !workbookView.ActiveWorksheet.ProtectContents;
                lockSheetToolStripMenuItem.Checked = workbookView.ActiveWorksheet.ProtectContents;
            });
        }

        private void workbookView_ActiveTabChanged(object? sender, ActiveTabChangedEventArgs e)
        {
            DoOrSwallow(NoteActiveTabChanged);
        }

        private void NoteActiveTabChanged()
        {
            if (null != workbookView.ActiveWorksheet)
                lockSheetToolStripMenuItem.Checked = workbookView.ActiveWorksheet.ProtectContents;
        }

        private void frmSpreadsheetGear_Shown(object? sender, EventArgs e)
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

        private void importDataToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(ImportData, "Import data failed");
        }

        private void ImportData()
        {
            DoOperation("ImportWorksheet");
        }

        private void DoOperation(string operationName)
        {
            SdApplication.DoOperation(operationName);
        }

        private void exportDataToolStripMenuItem_Click(object? sender, EventArgs e)
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

        private void cutContextMenuItem1_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(EditCut);
        }

        private void copyContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(EditCopy);
        }

        private void pasteContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(EditPaste);
        }

        private void pasteSpecialContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(PasteSpecial);
        }

        private void insertContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(InsertCells);
        }

        private void deleteContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(DeleteSpecial);
        }

        private void clearContentsContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(ClearSelectedCells);
        }

        private void insertCommentContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(InsertComment);
        }

        private void InsertComment()
        {
            workbookView.WithLock(() =>
            {
                if (null == workbookView.ActiveCell.Comment)
                {
                    workbookView.ActiveCell.AddComment(string.Empty);
                }
                // By now, the comment is known to exist.
                workbookView.ActiveCell.Comment.Visible = true;
            });
        }

        private void goToContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(GoToCell, "Go to cell failed");
        }

        private void findAndReplaceContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(EditReplace);
        }

        private void contextMenuStrip_Opening(object? sender, CancelEventArgs e)
        {
            // If we're selecting, this cancels the selection instead of showing the menu.
            if (SdApplication.SelectingData)
            {
                // NoteEndOfSelection clears both selectingData and inputtingData.  However, that's safe here, as we only get here if we're SelectingData.
                SdApplication.NoteEndOfSelection(true);
                e.Cancel = true;
                return;
            }

            // If we get here, no selection is occurring
            workbookView.WithLock(() =>
            {
                IComment comment = workbookView.ActiveCell.Comment;
                insertCommentContextMenuItem.Visible = null == comment;
                deleteCommentContextMenuItem.Visible = null != comment;
                showCommentContextMenuItem.Visible = null != comment && !comment.Visible;
                hideCommentContextMenuItem.Visible = null != comment && comment.Visible;
                editCommentContextMenuItem.Visible = false; // null != comment;
            });
        }

        private void deleteCommentContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(DeleteComment);
        }

        private void DeleteComment()
        {
            workbookView.WithLock(() =>
            {
                if (null != workbookView.ActiveCell.Comment)
                    workbookView.ActiveCell.ClearComments();
            });
        }

        private void showCommentContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(ShowComment);
        }

        private void ShowComment()
        {
            workbookView.WithLock(() =>
            {
                if (null != workbookView.ActiveCell.Comment)
                    workbookView.ActiveCell.Comment.Visible = true;
            });
        }

        private void editCommentContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(EditComment);
        }

        private void EditComment()
        {
            workbookView.WithLock(() =>
            {
                if (null != workbookView.ActiveCell.Comment)
                {
                    // TODO: Do something!
                }
            });
        }

        private void hideCommentContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrSwallow(HideComment);
        }

        private void HideComment()
        {
            workbookView.WithLock(() =>
            {
                if (null != workbookView.ActiveCell.Comment)
                {
                    workbookView.ActiveCell.Comment.Visible = false;
                }
            });
        }

        private void redoToolStripMenuItem_Click(object? sender, EventArgs e)
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

            public override string DisplayText => displayText;

            protected override CommandRangeUndoFlags UndoFlags => CommandRangeUndoFlags.AutoFilters
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

            protected override bool Execute()
            {
                return wrappedExecute();
            }
        }

        private void formatCellsContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(ShowRangeExplorer, "Couldn't format cells");
        }

        private void ShowRangeExplorer()
        {
            workbookView.WithLock(() =>
            {
                const RangeExplorerCategoryFlags categoryFlags = RangeExplorerCategoryFlags.All;
                IWorkbookSet workbookSet = workbookView.ActiveWorkbookSet;
                RangeExplorer explorer = new(workbookSet, categoryFlags);
                explorer.Show(workbookView);
            });
        }

        private void summaryContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(DescribeColumn, "Couldn't describe column");
        }

        internal void SetUnsavedName(string childName)
        {
            Text = childName;
            workbookView.WithLock(() =>
                workbookView.ActiveWorkbook.FullName = childName);
        }

        private void exportToRToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(ExportSelectionToR, "Couldn't export selection to clipboard in R format");
        }

        private void ExportSelectionToR()
        {
            DataFrame? frame = GetCellArray(0, DataAcquisitionMode.Variant, 1, 10000, "Select the data to be placed on the clipboard", null, false, false, out bool userCancelled, out bool wasPivoted, 0);
            if (userCancelled)
                return;
            StringBuilder sb = new();
            RConvert.ToR(sb, "copied.data", frame, FrameType.Long);
            Clipboard.Clear();
            Clipboard.SetText(sb.ToString(), TextDataFormat.Text);
        }

        internal void ToggleFilters()
        {
            workbookView.WithLock(() => workbookView.RangeSelection.AutoFilter());
        }

        private void renameWorksheetContextMenuItem_Click(object? sender, EventArgs e)
        {
            DoOrWarn(RenameWorksheet, "Couldn't rename worksheet");
        }

        private void RenameWorksheet()
        {
            string? currentWorksheetName = null;
            workbookView.WithLock(() =>
                currentWorksheetName = workbookView.ActiveSheet.Name);
            if (null != currentWorksheetName)
            {
                string newWorksheetName = SdApplication.GetString("Enter new name for worksheet", "Rename Worksheet", currentWorksheetName);
                if (!string.IsNullOrWhiteSpace(newWorksheetName))
                    workbookView.WithLock(() => workbookView.ActiveSheet.Name = newWorksheetName);
            }
        }

        private class WriteDataFrameParametersHolder
        {
            public DataFrame Frame { get; init; }
            public bool IsFormulae { get; init; }
            public string MissingIndicator { get; init; }
            public int OffsetForTitles { get; init; }
            public IRange Range { get; init; }
            public bool ShouldMove { get; init; }
        }
    }

    public class ClipboardManglingCommandManager : CommandManager
    {
        internal ClipboardManglingCommandManager(IWorkbookSet workbookSet)
            : base(workbookSet)
        { }

        // Gets called anytime a Paste command is invoked (Ctrl+V, context menu item, WorkbookView.Paste(), etc)
        public override Command CreateCommandPaste(IRange range)
        {
            return new ClipboardManglingPasteCommand(range);
        }
    }

    public class ClipboardManglingPasteCommand : CommandRange.Paste
    {
        public ClipboardManglingPasteCommand(IRange range)
            : base(range)
        {
        }

        protected override bool Execute()
        {
            try
            {
                return base.Execute();
            }
            catch (Exception rawPasteEx)
            {
                try
                {
                    MangleClipboardIfNecessary();
                    return base.Execute();
                }
                catch (Exception)
                {
                    throw new Exception("Cannot paste: the data on the clipboard is in a format that StatsDirect cannot interpret", rawPasteEx);
                }
            }
        }

        private void MangleClipboardIfNecessary()
        {
            ClipboardChecker.ConvertClipboardWithBiff5ToBiff8(true);
        }
    }
}
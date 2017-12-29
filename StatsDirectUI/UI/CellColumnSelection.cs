using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Utilities;
using System;
using System.Text;

namespace StatsDirect.UI
{
    public class CellColumnSelection
    {
        public string WorkbookPath { get; set; }
        public string WorksheetName { get; set; }
        /// <summary>The grid column (from the grid's base - 0 for SpreadsheetGear)</summary>
        public int ColumnIndex { get; set; }
        /// <summary>The first grid row (from the grid's base - 0 for SpreadsheetGear)</summary>
        public int RowIndex { get; set; }
        /// <summary>The number of rows</summary>
        public int RowCount { get; set; }
        public string ColumnTitle { get { EnsureHasColumnTitleValues(); return columnTitle; } }
        public int DataRows { get { EnsureHasColumnTitleValues(); return dataRows; } }
        public int GridFirstDataRow { get { EnsureHasColumnTitleValues(); return gridFirstDataRow; } }
        public bool TitleIsInData { get { EnsureHasColumnTitleValues(); return titleIsInData; } }
        public bool WasFiltered { get { EnsureKnowsNonHiddenRowCount(); return nonHiddenRowCount < RowCount; } }

        public int NonHiddenRowCount { get { EnsureKnowsNonHiddenRowCount(); return nonHiddenRowCount; } }
        public int NonHiddenDataRowCount { get { EnsureKnowsNonHiddenDataRowCount(); return nonHiddenDataRowCount; } }

        // Things that are calculated at the same time as titles
        private bool hasColumnTitleValues;
        private string columnTitle;
        private int dataRows;
        private int gridFirstDataRow;
        private bool titleIsInData;
        private IGetCells cellGetter;

        // Things that are calculated at the same time as knowing how long the column really is
        private int nonHiddenRowCount = -1;
        private int nonHiddenDataRowCount = -1;
        private double[] cachedDataValues;
        private string[] cachedTextValues;
        private string[] cachedTexts;
        private string[] cachedFormulae;
        private object[,] cachedObjects;

        public CellColumnSelection(IGetCells cellGetter)
        {
            this.cellGetter = cellGetter;
        }

        private void EnsureHasColumnTitleValues()
        {
            if (!hasColumnTitleValues)
            {
                SetColumnTitleValues();
                hasColumnTitleValues = true;
            }
        }

        /// <summary>
        /// Finds the column title and sets other variables associated with this.
        /// Amends RowIndex to step past a column title if found.
        /// </summary>
        /// <param name="ccs"></param>
        /// <param name="dataRows">Filled in with the total number of visible and hidden DATA rows.  This may not be the same as the number of non-title rows, as there may be blanks between the titles and the data.</param>
        /// <param name="gridFirstDataRow">Filled in with the grid row index of the first non-title, non-missing data row.</param>
        /// <param name="gridColumn">Filled in with the grid column index of the column.</param>
        /// <param name="titleIsInData">true iff the title has been found within the data; false if the title is auto-generated.</param>
        private void SetColumnTitleValues()
        {
            string candidateTitle = string.Empty;
            titleIsInData = false;
            dataRows = RowCount;
            int candidateFirstGridRow = RowIndex;
            if (cellGetter.GetCellValue(candidateFirstGridRow, ColumnIndex) == Constant.MISSING)
            {
                // Could be text, i.e. a title - let's find out
                candidateTitle = cellGetter.GetCellText(candidateFirstGridRow, ColumnIndex).Trim();
                if (candidateTitle.Length > 0)
                {
                    candidateFirstGridRow++;
                    dataRows--;
                }
                else
                {
                    // It's not numeric or text - look back up to two rows to see whether there's a title in there.
                    for (int r = candidateFirstGridRow - 1; r >= Math.Max(0, candidateFirstGridRow - 2); r--)
                    {
                        if (cellGetter.GetCellValue(r, ColumnIndex) == Constant.MISSING)
                        {
                            candidateTitle = cellGetter.GetCellText(r, ColumnIndex).Trim();
                            if (candidateTitle.Length > 0)
                            {
                                gridFirstDataRow = candidateFirstGridRow;
                                titleIsInData = true;
                                columnTitle = SafeTitle(candidateTitle);
                                return;
                            }
                        }
                    }
                }
            }
            else
            {
                // First row is numeric.  Look back up to two rows to see whether there's a title in there.
                for (int r = candidateFirstGridRow - 1; r >= Math.Max(0, candidateFirstGridRow - 2); r--)
                {
                    if (cellGetter.GetCellValue(r, ColumnIndex) == Constant.MISSING)
                    {
                        candidateTitle = cellGetter.GetCellText(r, ColumnIndex).Trim();
                        if (candidateTitle.Length > 0)
                        {
                            gridFirstDataRow = candidateFirstGridRow;
                            titleIsInData = true;
                            columnTitle = SafeTitle(candidateTitle);
                            return;
                        }
                    }
                }
            }

            // If the data block is half way down the column, or otherwise has no title, then grab row 0 entry i.e. a,b,c etc.
            if (candidateTitle.Length == 0)
                candidateTitle = cellGetter.GetColumnTitle(ColumnIndex).Trim();

            // Find the first data row - anything other than blank is fair game
            int skippedRows = 0;
            for (gridFirstDataRow = candidateFirstGridRow; gridFirstDataRow < candidateFirstGridRow + dataRows; gridFirstDataRow++)
            {
                if (!string.IsNullOrWhiteSpace(cellGetter.GetCellText(gridFirstDataRow, ColumnIndex)))
                    break;
                skippedRows++;
            }
            dataRows -= skippedRows;
            columnTitle = SafeTitle(candidateTitle);
        }

        /// <summary>
        /// Current policy: strip control characters, replace whitespace (any) with spaces.
        /// </summary>
        /// <param name="candidateTitle"></param>
        /// <returns></returns>
        private static string SafeTitle(string candidateTitle)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in candidateTitle)
            {
                if (char.IsWhiteSpace(ch))
                    sb.Append(' ');
                else if (!char.IsControl(ch))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        internal IOrigin GetWorksheetOrigin(DataAcquisitionMode mode, bool titleIsInData, int originGroup)
        {
            return new WorksheetOrigin(WorkbookPath, WorksheetName, ColumnIndex, RowIndex, RowCount, mode, titleIsInData, WasFiltered, originGroup);
        }

        internal string[] GetTexts()
        {
            if (null == cachedTexts)
                (cachedTexts, nonHiddenRowCount) = cellGetter.GetCellTexts(ColumnIndex, RowIndex, RowIndex + RowCount - 1);
            return cachedTexts;
        }

        internal string[] GetTextValues()
        {
            if (null == cachedTextValues)
                (cachedTextValues, nonHiddenDataRowCount) = cellGetter.GetCellTexts(ColumnIndex, GridFirstDataRow, GridFirstDataRow + DataRows - 1);
            return cachedTextValues;
        }

        internal string GetFirstCellText()
        {
            return cellGetter.GetCellText(RowIndex, ColumnIndex);
        }

        internal DateTime[] GetDateValues()
        {
            DateTime[] values;
            (values, nonHiddenDataRowCount) = cellGetter.GetCellDateValues(ColumnIndex, GridFirstDataRow, GridFirstDataRow + DataRows - 1);
            return values;
        }

        /// <summary>
        /// Note that this is DATA values, i.e. first data row (after titles) to last data row.
        /// </summary>
        internal double[] GetDataValues()
        {
            if (null == cachedDataValues)
                (cachedDataValues, nonHiddenDataRowCount) = cellGetter.GetCellValues(ColumnIndex, GridFirstDataRow, GridFirstDataRow + DataRows - 1);
            return cachedDataValues;
        }

        internal string[] GetFormulae()
        {
            if (null == cachedFormulae)
                (cachedFormulae, nonHiddenRowCount) = cellGetter.GetCellFormulae(ColumnIndex, RowIndex, RowIndex + RowCount - 1);
            return cachedFormulae;
        }

        internal object[,] GetObjects()
        {
            if (null == cachedObjects)
                (cachedObjects, nonHiddenRowCount) = cellGetter.GetCellObjects(ColumnIndex, RowIndex, RowIndex + RowCount - 1);
            return cachedObjects;
        }

        internal string GetColumnTitle()
        {
            return cellGetter.GetColumnTitle(ColumnIndex);
        }

        internal bool TopIsFormattedLikeATitle()
        {
            return cellGetter.IsFormattedLikeATitle(ColumnIndex, RowIndex);
        }

        private void EnsureKnowsNonHiddenRowCount()
        {
            // If we don't know the count yet, looking at the raw object array is by far the cheapest way of finding out whether we've been filtered.
            if (nonHiddenRowCount < 0)
                GetObjects();
        }

        private void EnsureKnowsNonHiddenDataRowCount()
        {
            if (nonHiddenDataRowCount < 0)
                GetDataValues();
        }

    }
}

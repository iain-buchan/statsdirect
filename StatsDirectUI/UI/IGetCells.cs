using System;

using System.Collections.Generic;

namespace StatsDirect.UI
{
    public interface IGetCells
    {
        /// <returns>(Array of values, hidden row count)</returns>
        (DateTime[], int) GetCellDateValues(int column, int firstRow, int lastRow);
        /// <summary>
        /// Obtain a raw array of formulae, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns>(Array of values, hidden row count)</returns>
        (string[], int) GetCellFormulae(int column, int firstRow, int lastRow);
        /// <summary>
        /// Obtain a raw array of objects, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns>(Array of values, hidden row count)</returns>
        (object[,], int) GetCellObjects(int column, int firstRow, int lastRow);

        /// <summary>The physical zero-based row indices included by this reader, in acquisition order.
        /// Existing readers can use the single-row fallback; native grids can obtain visibility directly.</summary>
        int[] GetVisibleRowIndices(int column, int firstRow, int lastRow)
        {
            List<int> rows = new();
            for (int row = firstRow; row <= lastRow; row++)
                if (GetCellObjects(column, row, row).Item2 > 0)
                    rows.Add(row);
            return rows.ToArray();
        }

        string GetCellText(int gridRow, int gridColumn);
        /// <summary>
        /// Obtain a raw array of display strings, ensuring that even a single cell is wrapped in a 2-D array for subsequent processing.
        /// </summary>
        /// <param name="column">The grid column (indexed from 0) from which to obtain the values</param>
        /// <param name="firstRow">The first grid row (indexed from 0) to include in the results</param>
        /// <param name="lastRow">The last grid row (indexed from 0) to include in the results</param>
        /// <returns></returns>
        (string[], int) GetCellTexts(int column, int firstRow, int lastRow);
        double GetCellValue(int gridRow, int gridColumn);
        /// <returns>(Array of values, hidden row count)</returns>
        (double[], int) GetCellValues(int column, int firstRow, int lastRow);
        string GetColumnTitle(int column);
        bool IsFormattedLikeATitle(int column, int row);
    }
}

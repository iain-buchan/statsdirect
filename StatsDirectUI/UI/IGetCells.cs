using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// <param name="nonHiddenRowCount">The number of non-hidden objects in the array.  Note that raw retrieved values will have been copied down the array to obscure hidden objects in this case; the top end of the array will NOT have been null-filled, so the values in return[nonHiddenRowCount] and above should be considered unknown.</param>
        /// <returns>(Array of values, hidden row count)</returns>
        (object[,], int) GetCellObjects(int column, int firstRow, int lastRow);
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

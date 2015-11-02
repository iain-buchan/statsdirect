using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    public interface IGrid: IForm
    {
        /// <summary>
        /// Set the selection back to a single cell
        /// </summary>
        void ClearSelection();

        /// <summary>
        /// Return true if the grid has changed since it was last saved, false if not
        /// </summary>
        bool Dirty
        {
            get;
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
        /// <param name="originGroup">An identifier to be recorded by all of the variables in this action to indicate they all came from the same selection.</param>
        /// <returns></returns>
        DataFrame GetCellArray(int rowLengthHint, DataAcquisitionMode mode, int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, bool mightBeBatching, out bool userCancelled, out bool wasPivoted, int originGroup);

        /// <summary>
        /// Obtain a 0-indexed array of [row,column] objects for the 0-indexed cells specified.
        /// Known data types are correctly returned as System.String, System.Double, System.Boolean, TODO: SpreadsheetGear.ValueError or null, and formulae return their values.
        /// </summary>
        /// <param name="top">The lowest row that will be returned (minimum 0)</param>
        /// <param name="left">The lowest column that will be returned (minimum 0)</param>
        /// <param name="bottom">The highest-numbered row that will be returned (minimum 0)</param>
        /// <param name="right">The highest-numbered column that will be returned (minimum 0)</param>
        /// <returns>a 0-indexed array of [row,column] objects for the 0-indexed cells specified.</returns>
        object[,] GetValues(int top, int left, int bottom, int right);

        /// <summary>
        /// Answer whether the selection on the grid is of more than one cell
        /// </summary>
        /// <returns>true if the selection on the grid is of more than one cell, false otherwise</returns>
        bool HasSelection();

        /// <summary>
        /// Set the [row,column] cells starting at [Top, Left] to the cells specified.
        /// Known data types are correctly set.  Formulae will be cleared.
        /// </summary>
        /// <param name="top">The lowest row that will be set (minimum 0)</param>
        /// <param name="left">The lowest column that will be set (minimum 0)</param>
        /// <param name="values">The array of [row,column] values to set</param>
        void SetValues(int top, int left, object[,] values);

        /// <summary>
        /// Write the variables in the frame, in order, to the appropriate position in the grid.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="isFormulae"> </param>
        /// <param name="missingIndicator"> </param>
        /// <param name="writePosition"> </param>
        void WriteDataFrame(DataFrame frame, bool isFormulae, string missingIndicator, RelativePosition writePosition);

        /// <summary>
        /// The portion of the sheet that is in use.
        /// This is a maximum; the actual part that is in use may be smaller than this.
        /// </summary>
        Area UsedArea
        {
            get;
        }

        Range Selection
        {
            get;
            set;
        }

        string WorkbookPath
        {
            get;
        }

        string ActiveWorksheetName
        {
            get;
        }

        void Refill(List<Variable> variable);
    }
}

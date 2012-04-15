using StatsDirect.Data;

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
        /// Obtain a 0-indexed array of [row,column] objects for the 0-indexed cells specified.
        /// Known data types are correctly returned as System.String, System.Double, System.Boolean, TODO: SpreadsheetGear.ValueError or null, and formulae return their values.
        /// </summary>
        /// <param name="Top">The lowest row that will be returned (minimum 0)</param>
        /// <param name="Left">The lowest column that will be returned (minimum 0)</param>
        /// <param name="Bottom">The highest-numbered row that will be returned (minimum 0)</param>
        /// <param name="Right">The highest-numbered column that will be returned (minimum 0)</param>
        /// <returns>a 0-indexed array of [row,column] objects for the 0-indexed cells specified.</returns>
        object[,] GetValues(int Top, int Left, int Bottom, int Right);

        /// <summary>
        /// Answer whether the selection on the grid is of more than one cell
        /// </summary>
        /// <returns>true if the selection on the grid is of more than one cell, false otherwise</returns>
        bool HasSelection();

        /// <summary>
        /// Set the [row,column] cells starting at [Top, Left] to the cells specified.
        /// Known data types are correctly set.  Formulae will be cleared.
        /// </summary>
        /// <param name="Top">The lowest row that will be set (minimum 0)</param>
        /// <param name="Left">The lowest column that will be set (minimum 0)</param>
        /// <param name="Values">The array of [row,column] values to set</param>
        void SetValues(int Top, int Left, object[,] Values);

        /// <summary>
        /// Write the variables in the frame, in order, to the appropriate position in the grid.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="isFormulae"> </param>
        /// <param name="writePosition"> </param>
        void WriteDataFrame(DataFrame frame, bool isFormulae, RelativePosition writePosition);

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

        void Refill(Variable variable, WorksheetOrigin worksheetOrigin);
    }
}

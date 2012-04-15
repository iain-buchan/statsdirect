using System;

namespace StatsDirect.UI
{
    /// <summary>
    /// An Area holds its parent Grid and its 0-indexed bounds, and may be passed around freely.
    /// </summary>
    public sealed class Area
    {
        private readonly IGrid grid;
        private readonly int top;
        private readonly int left;
        private readonly int bottom;
        private readonly int right;

        public Area(IGrid grid, int top, int left, int bottom, int right)
        {
            this.grid = grid;
            this.top = top;
            this.left = left;
            this.bottom = bottom;
            this.right = right;
        }

        /// <summary>
        /// The total number of cells included in this area (minimum 0)
        /// </summary>
        public int Cells
        {
            get { return Columns * Rows; }
        }

        /// <summary>
        /// The total number of columns incuded in this area (minimum 0)
        /// </summary>
        public int Columns
        {
            get { return Math.Max(right - left + 1, 0); }
        }

        /// <summary>
        /// The total number of rows incuded in this area (minimum 0)
        /// </summary>
        public int Rows
        {
            get { return Math.Max(bottom - top + 1, 0); }
        }

        /// <summary>
        /// The lowest-numbered row included in the area (minimum 0)
        /// </summary>
        public int Top
        {
            get { return top; }
        }

        /// <summary>
        /// The lowest-numbered column included in the area (minimum 0)
        /// </summary>
        public int Left
        {
            get { return left; }
        }

        /// <summary>
        /// The highest-numbered row included in the area (minimum 0)
        /// </summary>
        public int Bottom
        {
            get { return bottom; }
        }

        /// <summary>
        /// The highest-numbered column included in the area (minimum 0)
        /// </summary>
        public int Right
        {
            get { return right; }
        }

        /// <summary>
        /// Obtain a 0-indexed array of [row,column] objects for the 0-indexed cells specified.
        /// Known data types are correctly returned as System.String, System.Double, System.Boolean, TODO: SpreadsheetGear.ValueError or null, and formulae return their values.
        /// </summary>
        /// <returns>a 0-indexed array of [row,column] objects for the 0-indexed cells specified.</returns>
        public object[,] GetValues()
        {
            return grid.GetValues(top, left, bottom, right);
        }

        /// <summary>
        /// Set the [row,column] cells starting at [Top, Left] to the cells specified.
        /// Known data types are correctly set.  Formulae will be cleared.
        /// </summary>
        /// <param name="values">The array of [row,column] values to set</param>
        public void SetValues(object[,] values)
        {
            grid.SetValues(top, left, values);
        }

        /// <summary>
        /// Return a new Area representing the overlap between this Area and the other one.
        /// Typically used to clip (for example) a column to the used area of a grid.
        /// </summary>
        /// <param name="other">The Area to be intersected with this one</param>
        /// <returns>a new Area representing the overlap between this Area and the other one</returns>
        public Area Intersect(Area other)
        {
            return new Area(grid, Math.Max(top, other.top), Math.Max(left, other.left), Math.Min(bottom, other.bottom), Math.Min(right, other.right));
        }
    }
}

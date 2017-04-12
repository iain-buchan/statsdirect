using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.UI
{
    /// <summary>
    /// Holds a set of selections in a grid in a form that can be passed between functions conveniently.
    /// </summary>
    public class CellSelection
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

        public int TotalColumns => ColumnSelections.Count;
    }
}

using System;

namespace StatsDirect.R
{
    /// <summary>
    /// Finds a column that has moved since a record pointed at it.  An analysis that writes its result columns to the left of its data,
    /// or a column inserted or removed by hand, shifts the data along the sheet without changing it; the record is written at the report
    /// step, before such output.  The column is looked for by its hash, and by its title when it has one, first at the recorded place and
    /// then one column at a time to the right and to the left of it, within reach columns.
    /// </summary>
    public static class ColumnLocator
    {
        /// <summary>The column the pointer's data are in now, or null when no column within reach has them.</summary>
        /// <param name="column">The record's pointer to the column, with the hash of its values when the analysis ran.</param>
        /// <param name="at">Reads the title and the hash of the values at a column, numbered from 1, or gives null where there is none.</param>
        /// <param name="reach">How many columns to each side are looked at.</param>
        public static int? Find(ColumnPointer column, Func<int, (string title, string hash)?> at, int reach = 64)
        {
            if (column == null || at == null)
                return null;
            if (string.IsNullOrEmpty(column.Hash))
                return column.Column;   // nothing to tell a move by: the recorded place
            bool Matches(int index)
            {
                if (index < 1) return false;
                (string title, string hash)? read = at(index);
                return read != null && read.Value.hash == column.Hash && (!column.HasTitle || string.IsNullOrEmpty(column.Title) || read.Value.title == column.Title);
            }
            if (Matches(column.Column))
                return column.Column;
            for (int d = 1; d <= reach; d++)
            {
                if (Matches(column.Column + d)) return column.Column + d;   // a column inserted to the left moved the data right
                if (Matches(column.Column - d)) return column.Column - d;   // a column removed to the left moved the data left
            }
            return null;
        }
    }
}

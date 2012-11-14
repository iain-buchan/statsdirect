using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Windows.Forms;
using SpreadsheetGear;
using SpreadsheetGear.Commands;
using StatsDirect.Utilities;
using SpreadsheetGear.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlSort : UserControl, IOkable
    {
        private readonly IRange range;
        private readonly WorkbookView workbookView;

        public ctlSort(IRange range, WorkbookView workbookView)
        {
            this.range = range;
            this.workbookView = workbookView;
            InitializeComponent();
            SetControlFromParameter();
        }

        private void SetControlFromParameter()
        {
            gridKeys.Rows.Clear();
            bool useHeaders = chkUseHeaders.Checked;
            DataTable dt = new DataTable();
            dt.Columns.Add("Offset", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Rows.Add(-1, "<none>");
            range.WorkbookSet.GetLock();
            try
            {
                for (int col = 0; col < range.ColumnCount; col++)
                {
                    // Find the column name
                    string columnName = null;
                    if (useHeaders)
                    {
                        columnName = range[0, col].Text;
                    }
                    if (string.IsNullOrEmpty(columnName))
                    {
                        columnName = "Column " + Formatting.ToExcelColumnName(range.Column + col);
                    }

                    // Add the item to the drop-down
                    dt.Rows.Add(col, columnName);
                }
            }
            finally
            {
                range.WorkbookSet.ReleaseLock();
            }

            SortBy.DataSource = dt;
            SortBy.DisplayMember = "Name";

            // For each "real" item (i.e. not "<none>"), add a row to the UI
            for (int i = 1; i < SortBy.Items.Count; i++)
            {
                // Add a row for this column to the grid
                int newRow = gridKeys.Rows.Add();
                gridKeys.Rows[newRow].Cells[0].Value = i.ToString();
                gridKeys.Rows[newRow].Cells[1].Value = SortBy.Items[0];
                gridKeys.Rows[newRow].Cells[2].Value = "Low to high";
            }
        }

        #region IOkable Members

        void IOkable.OkClicked()
        {
            // Ignore any new incomplete row at the end of the grid while creating the array and iterating.  ASSUME: New row is always at the end.
            DataGridViewRow candidateNewRow = gridKeys.Rows[gridKeys.Rows.Count - 1];
            int newRowOffset = (null == candidateNewRow.Cells[1].Value || null == candidateNewRow.Cells[2].Value) ? -1 : 0;
            List<SortKey> keys = new List<SortKey>();
            for (int i = 0; i < gridKeys.Rows.Count + newRowOffset; i++)
            {
                DataGridViewRow r = gridKeys.Rows[i];
                DataRowView candidate = (DataRowView)r.Cells[1].Value;
                int candidateKey = (int)candidate.Row[0];
                if (candidateKey >= 0)
                {
                    keys.Add(new SortKey(
                        candidateKey,
                        "High to low".Equals(r.Cells[2].Value) ? SpreadsheetGear.SortOrder.Descending : SpreadsheetGear.SortOrder.Ascending,
                        SortDataOption.Normal));
                }
            }

            range.WorkbookSet.GetLock();
            try
            {
                IRange dataRange = chkUseHeaders.Checked ? range.Range[1, 0, range.RowCount - 1, range.ColumnCount - 1] : range;
                SortCommand sortCommand = new SortCommand(dataRange, keys.ToArray());
                workbookView.ActiveCommandManager.Execute(sortCommand);
                // sortCommand.Dispose(); must not be called, otherwise an Undo will fail as the command has already been disposed.
            }
            finally
            {
                range.WorkbookSet.ReleaseLock();
            }
        }

        #endregion

        private void chkUseHeaders_CheckedChanged(object sender, EventArgs e)
        {
            SetControlFromParameter();
        }

        private void gridKeys_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            /*
            // Find all the used variables
            Dictionary<object, bool> knownUsed = new Dictionary<object, bool>();
            foreach (DataGridViewRow row in e.Row.DataGridView.Rows)
            {
                if (!row.IsNewRow)
                {
                    object o = row.Cells[1].Value;
                    if (o is DataRowView)
                        knownUsed[o] = true;
                }
            }
            // Find the first unused variable
            object firstUnused = null;
            foreach (object o in SortBy.Items)
            {
                if (!knownUsed.ContainsKey(o))
                {
                    firstUnused = o;
                    break;
                }
            }
             */

            // Fill in the row
            e.Row.Cells[0].Value = e.Row.Index + 1;
            e.Row.Cells[1].Value = SortBy.Items[0];
            e.Row.Cells[2].Value = "Low to high";
        }

        private void gridKeys_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            gridKeys.BeginEdit(false);
            if (null != gridKeys.EditingControl && gridKeys.EditingControl is DataGridViewComboBoxEditingControl)
            {
                DataGridViewComboBoxEditingControl editingControl = (DataGridViewComboBoxEditingControl)gridKeys.EditingControl;
                if (editingControl.SelectedIndex == -1)
                    editingControl.SelectedIndex = 0;
                editingControl.DroppedDown = true;
            }
        }

        private void gridKeys_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            Control c = e.Control;
            if (c is DataGridViewComboBoxEditingControl)
            {
                DataGridViewComboBoxEditingControl dc = (DataGridViewComboBoxEditingControl)c;
                if (null == dc.Tag)
                {
                    dc.Tag = "event added";
                    dc.DropDownClosed += OnDropDownClosed;
                }
            }
        }

        private void OnDropDownClosed(object sender, EventArgs e)
        {
            DataGridViewComboBoxEditingControl ec = (DataGridViewComboBoxEditingControl)sender;
            ec.EditingControlDataGridView.EndEdit(DataGridViewDataErrorContexts.Commit);
            // ec.Tag = null;
            // ec.DropDownClosed -= OnDropDownClosed;
        }
    }

    internal class ColumnSort
    {
        public int ColumnOffset;
        public string ColumnName;

        public ColumnSort(int columnOffset, string columnName)
        {
            ColumnOffset = columnOffset;
            ColumnName = columnName;
        }

        public override string ToString()
        {
            return ColumnName;
        }
    }

    public class DGVComboBoxItemColumn : DataGridViewComboBoxColumn
    {
        public DGVComboBoxItemColumn()
        {
            CellTemplate = new DGVComboBoxItemCell();
        }
    }

    public class DGVComboBoxItemCell : DataGridViewComboBoxCell
    {
        private PropertyDescriptor displayProp;

        private CurrencyManager ListManager
        {
            get
            {
                BindingMemberInfo bmi = new BindingMemberInfo(base.DisplayMember);
                if (DataGridView != null)
                {
                    return (CurrencyManager)
                      DataGridView.BindingContext[DataSource, bmi.BindingPath];
                }
                return null;
            }
        }

        private PropertyDescriptor DisplayProp
        {
            get { return displayProp ?? (displayProp = ListManager.GetItemProperties().Find(DisplayMember, true)); }
        }

        protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle, TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
        {
            if (value == null || value == cellStyle.DataSourceNullValue)
                return "";

            return base.GetFormattedValue(DisplayProp.GetValue(value),
              rowIndex, ref cellStyle, valueTypeConverter,
              formattedValueTypeConverter, context);
        }

        public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle, TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
        {
            foreach (object item in ListManager.List)
            {
                if ((string)DisplayProp.GetValue(item) == (string)formattedValue)
                    return item;
            }

            return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
        }
     }

    internal class SortCommand : CommandRange
    {
        private readonly SortKey[] _sortKeys;

        public SortCommand(IRange range, SortKey[] keys)
            : base(range)
        {
            _sortKeys = keys;
        }

        public override string DisplayText
        {
            get
            {
                return "Sort";
            }
        }

        protected override CommandRangeUndoFlags UndoFlags
        {
            get
            {
                return CommandRangeUndoFlags.Formats | CommandRangeUndoFlags.Values;
            }
        }

        protected override bool Execute()
        {
            Range.Sort(SortOrientation.Rows, true, _sortKeys);
            return true;
        }
    }
}

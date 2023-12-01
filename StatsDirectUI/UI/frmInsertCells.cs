using System;
using System.Windows.Forms;
using SpreadsheetGear;

namespace StatsDirect.UI
{
    public partial class frmInsertCells : Form
    {
        private bool userCancelled = true;
        private InsertShiftDirection insertShiftDirection;
        private bool isEntire;

        public bool UserCancelled => userCancelled;

        public InsertShiftDirection InsertShiftDirection => insertShiftDirection;

        public bool IsEntire => isEntire;

        public frmInsertCells()
        {
            InitializeComponent();
        }

        private void cmdCancel_Click(object? sender, EventArgs e)
        {
            Close();
        }

        private void cmdOK_Click(object? sender, EventArgs e)
        {
            userCancelled = false;
            if (rdoRight.Checked)
            {
                insertShiftDirection = InsertShiftDirection.Right;
                isEntire = false;
            }
            else if (rdoColumn.Checked)
            {
                insertShiftDirection = InsertShiftDirection.Right;
                isEntire = true;
            }
            else if (rdoDown.Checked)
            {
                insertShiftDirection = InsertShiftDirection.Down;
                isEntire = false;
            }
            else
            {
                insertShiftDirection = InsertShiftDirection.Down;
                isEntire = true;
            }
            Close();
        }
    }
}

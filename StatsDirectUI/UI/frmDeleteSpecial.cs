using System;
using System.Windows.Forms;
using SpreadsheetGear;

namespace StatsDirect.UI
{
    public partial class frmDeleteSpecial : Form
    {
        private bool userCancelled = true;
        private DeleteShiftDirection deleteShiftDirection;
        private bool isEntire;

        public bool UserCancelled => userCancelled;

        public DeleteShiftDirection DeleteShiftDirection => deleteShiftDirection;

        public bool IsEntire => isEntire;

        public frmDeleteSpecial()
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
            if (rdoLeft.Checked)
            {
                deleteShiftDirection = DeleteShiftDirection.Left;
                isEntire = false;
            }
            else if (rdoColumn.Checked)
            {
                deleteShiftDirection = DeleteShiftDirection.Left;
                isEntire = true;
            }
            else if (rdoUp.Checked)
            {
                deleteShiftDirection = DeleteShiftDirection.Up;
                isEntire = false;
            }
            else
            {
                deleteShiftDirection = DeleteShiftDirection.Up;
                isEntire = true;
            }
            Close();
        }
    }
}

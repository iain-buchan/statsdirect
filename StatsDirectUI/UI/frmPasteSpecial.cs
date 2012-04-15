using System;
using System.Windows.Forms;
using SpreadsheetGear;

namespace StatsDirect.UI
{
    public partial class frmPasteSpecial : Form
    {
        private bool userCancelled = true;
        private PasteType pasteType;

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        public PasteType PasteType
        {
            get { return pasteType; }
        }

        public frmPasteSpecial()
        {
            InitializeComponent();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            userCancelled = false;
            if (rdoAll.Checked)
                pasteType = PasteType.All;
            else if (rdoFormats.Checked)
                pasteType = PasteType.Formats;
            else if (rdoFormulas.Checked)
                pasteType = PasteType.Formulas;
            else
                pasteType = PasteType.Values;
            Close();
        }
    }
}

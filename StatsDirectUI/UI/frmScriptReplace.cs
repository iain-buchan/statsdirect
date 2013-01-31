using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    ///  <summary>
    ///  This class provides four subroutines used to:
    ///  Find (find the first instance of a search term)
    ///  Find Next (find other instances of the search term after the first one is found)
    ///  Replace (replace the current selection with replacement text)
    ///  Replace All (replace all instances of search term with replacement text)
    ///  </summary>
    ///  <remarks></remarks>
    public partial class frmScriptReplace
    {
        private readonly frmScript mainForm;

        public frmScriptReplace(frmScript MainForm)
        {
            mainForm = MainForm;
            InitializeComponent();
            // events handled by btnFind_Click
            btnFind.Click += btnFind_Click;
            // events handled by btnFindNext_Click
            btnFindNext.Click += btnFindNext_Click;
            // events handled by btnReplace_Click
            btnReplace.Click += btnReplace_Click;
            // events handled by btnReplaceAll_Click
            btnReplaceAll.Click += btnReplaceAll_Click;
        }

        private void btnFind_Click(Object sender, EventArgs e)
        {

            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = mainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            int startPosition = text.IndexOf(searchTerm, 0, comp);

            if (startPosition < 0)
            {
                SDApplication.SoleInstance.MsgboxX("String: '" + txtSearchTerm.Text + "' not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false);
                return;
            }

            mainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            mainForm.rtbDoc.ScrollToCaret();
            mainForm.Focus();


        }

        private void btnFindNext_Click(Object sender, EventArgs e)
        {
            int startPosition = mainForm.rtbDoc.SelectionStart + 1;
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = mainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            startPosition = text.IndexOf(searchTerm, startPosition, comp);

            if (startPosition < 0)
            {
                SDApplication.SoleInstance.MsgboxX("String: " + txtSearchTerm.Text + " not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false);
                return;
            }

            mainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            mainForm.rtbDoc.ScrollToCaret();
            mainForm.Focus();
        }

        private void btnReplace_Click(Object sender, EventArgs e)
        {
            if (mainForm.rtbDoc.SelectedText.Length != 0)
            {
                mainForm.rtbDoc.SelectedText = txtReplacementText.Text;
            }

            int startPosition = mainForm.rtbDoc.SelectionStart + 1;
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;

            string text = mainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            startPosition = text.IndexOf(searchTerm, startPosition, comp);

            if (startPosition < 0)
            {
                SDApplication.SoleInstance.MsgboxX("String: '" + txtSearchTerm.Text + "' not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false);
                return;
            }

            mainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            mainForm.rtbDoc.ScrollToCaret();
            mainForm.Focus();
        }

        private void btnReplaceAll_Click(Object sender, EventArgs e)
        {
            // Save the selection
            int currentPosition = mainForm.rtbDoc.SelectionStart;
            int currentSelect = mainForm.rtbDoc.SelectionLength;

            string text = mainForm.rtbDoc.Rtf;
            string searchTerm = txtSearchTerm.Text.Trim();
            string replaceTerm = txtReplacementText.Text.Trim();
            mainForm.rtbDoc.Rtf = text.Replace(searchTerm, replaceTerm);

            // Restore the selection
            mainForm.rtbDoc.SelectionStart = currentPosition;
            mainForm.rtbDoc.SelectionLength = currentSelect;
            mainForm.Focus();
        }
    }
}

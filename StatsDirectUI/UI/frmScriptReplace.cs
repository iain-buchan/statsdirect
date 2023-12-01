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
    internal partial class frmScriptReplace
    {
        private frmScript MainForm { get; }
        private ISdApplication SdApplication { get; }

        public frmScriptReplace(frmScript mainForm, ISdApplication sdApplication)
        {
            MainForm = mainForm;
            SdApplication = sdApplication;
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

        private void btnFind_Click(object? sender, EventArgs e)
        {

            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = MainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            int startPosition = text.IndexOf(searchTerm, 0, comp);

            if (startPosition < 0)
            {
                SdApplication.MsgboxX("String: '" + txtSearchTerm.Text + "' not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false);
                return;
            }

            MainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            MainForm.rtbDoc.ScrollToCaret();
            MainForm.Focus();


        }

        private void btnFindNext_Click(object? sender, EventArgs e)
        {
            int startPosition = MainForm.rtbDoc.SelectionStart + 1;
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = MainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            startPosition = text.IndexOf(searchTerm, startPosition, comp);

            if (startPosition < 0)
            {
                SdApplication.MsgboxX("String: " + txtSearchTerm.Text + " not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false);
                return;
            }

            MainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            MainForm.rtbDoc.ScrollToCaret();
            MainForm.Focus();
        }

        private void btnReplace_Click(object? sender, EventArgs e)
        {
            if (MainForm.rtbDoc.SelectedText.Length != 0)
            {
                MainForm.rtbDoc.SelectedText = txtReplacementText.Text;
            }

            int startPosition = MainForm.rtbDoc.SelectionStart + 1;
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;

            string text = MainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            startPosition = text.IndexOf(searchTerm, startPosition, comp);

            if (startPosition < 0)
            {
                SdApplication.MsgboxX("String: '" + txtSearchTerm.Text + "' not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false);
                return;
            }

            MainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            MainForm.rtbDoc.ScrollToCaret();
            MainForm.Focus();
        }

        private void btnReplaceAll_Click(object? sender, EventArgs e)
        {
            // Save the selection
            int currentPosition = MainForm.rtbDoc.SelectionStart;
            int currentSelect = MainForm.rtbDoc.SelectionLength;

            string text = MainForm.rtbDoc.Rtf;
            string searchTerm = txtSearchTerm.Text.Trim();
            string replaceTerm = txtReplacementText.Text.Trim();
            MainForm.rtbDoc.Rtf = text.Replace(searchTerm, replaceTerm);

            // Restore the selection
            MainForm.rtbDoc.SelectionStart = currentPosition;
            MainForm.rtbDoc.SelectionLength = currentSelect;
            MainForm.Focus();
        }
    }
}

using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    ///  <summary>
    ///  This class provides two subroutines used to:
    ///  Find (find the first instance of a search term)
    ///  Find Next (find other instances of the search term after the first one is found)
    ///  </summary>
    internal partial class frmScriptFind  
    {
        private frmScript MainForm { get; }
        private ISdApplication SdApplication { get; }

        public frmScriptFind(frmScript mainForm, ISdApplication sdApplication) 
        { 
            MainForm = mainForm;
            SdApplication = sdApplication;
            InitializeComponent();
            btnFind.Click += btnFind_Click; 
            btnFindNext.Click += btnFindNext_Click; 
        } 
        
        private void btnFind_Click(object? sender, EventArgs e ) 
        {
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = MainForm.rtbDoc.Text; 
            string searchTerm = txtSearchTerm.Text; 
            int startPosition = text.IndexOf(searchTerm, 0, comp); 
            
            if (startPosition < 0)
            {
                SdApplication.MsgboxX("String: " + txtSearchTerm.Text + " not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false); 
                return; 
            }

            MainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            MainForm.rtbDoc.ScrollToCaret();
            MainForm.Focus(); 
        } 
        
        
        
        private void btnFindNext_Click(object? sender, EventArgs e ) 
        {
            int startPosition = MainForm.rtbDoc.SelectionStart + 1;
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = MainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            startPosition = text.IndexOf(searchTerm, startPosition, comp); 
            
            if ( startPosition < 0 ) 
            {
                SdApplication.MsgboxX("String: " + txtSearchTerm.Text + " not found", MessageBoxButtons.OK, MessageBoxIcon.Asterisk, "No Matches", false); 
                return; 
            }

            MainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            MainForm.rtbDoc.ScrollToCaret();
            MainForm.Focus(); 
        } 
    } 
} 

using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    ///  <summary>
    ///  This class provides two subroutines used to:
    ///  Find (find the first instance of a search term)
    ///  Find Next (find other instances of the search term after the first one is found)
    ///  </summary>
    public partial class frmScriptFind  
    {
        private readonly frmScript mainForm;

        public frmScriptFind(frmScript MainForm) 
        { 
            mainForm = MainForm;
            InitializeComponent();
            btnFind.Click += btnFind_Click; 
            btnFindNext.Click += btnFindNext_Click; 
        } 
        
        private void btnFind_Click( Object sender, EventArgs e ) 
        {
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = mainForm.rtbDoc.Text; 
            string searchTerm = txtSearchTerm.Text; 
            int startPosition = text.IndexOf(searchTerm, 0, comp); 
            
            if (startPosition < 0)
            { 
                MessageBox.Show( "String: " + txtSearchTerm.Text + " not found", "No Matches", MessageBoxButtons.OK, MessageBoxIcon.Asterisk ); 
                return; 
            }

            mainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            mainForm.rtbDoc.ScrollToCaret();
            mainForm.Focus(); 
        } 
        
        
        
        private void btnFindNext_Click( Object sender, EventArgs e ) 
        {
            int startPosition = mainForm.rtbDoc.SelectionStart + 1;
            StringComparison comp = chkMatchCase.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = mainForm.rtbDoc.Text;
            string searchTerm = txtSearchTerm.Text;
            startPosition = text.IndexOf(searchTerm, startPosition, comp); 
            
            if ( startPosition < 0 ) 
            { 
                MessageBox.Show( "String: " + txtSearchTerm.Text + " not found", "No Matches", MessageBoxButtons.OK, MessageBoxIcon.Asterisk ); 
                return; 
            }

            mainForm.rtbDoc.Select(startPosition, txtSearchTerm.Text.Length);
            mainForm.rtbDoc.ScrollToCaret();
            mainForm.Focus(); 
        } 
    } 
} 

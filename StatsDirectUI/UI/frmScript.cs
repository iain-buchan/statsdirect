using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class frmScript : StatsDirectForm, IScriptWindow
    {
        /// <summary>
        /// A small print class that allows scripts to throw output at the window.
        /// </summary>
        public class DebugPrinter
        {
            private readonly frmScript frmScript;

            internal DebugPrinter(frmScript f)
            {
                frmScript = f;
            }

            public void Print(string Message)
            {
                frmScript.rtbOutput.AppendText(Message + "\n");
                Application.DoEvents(); // Force a display update
            }
        }

        public frmScript()
        {
            InitializeComponent();
            SaveToolStripMenuItem.Click += SaveToolStripMenuItem_Click;
            SaveAsToolStripMenuItem.Click +=SaveAsToolStripMenuItem_Click;
            SelectAllToolStripMenuItem.Click += SelectAllToolStripMenuItem_Click;
            CopyToolStripMenuItem.Click +=CopyToolStripMenuItem_Click;
            CutToolStripMenuItem.Click +=CutToolStripMenuItem_Click;
            PasteToolStripMenuItem.Click += PasteToolStripMenuItem_Click;
            mnuUndo.Click += mnuUndo_Click;
            mnuRedo.Click +=mnuRedo_Click;
            FindToolStripMenuItem.Click += FindToolStripMenuItem_Click;
            FindAndReplaceToolStripMenuItem.Click += FindAndReplaceToolStripMenuItem_Click;
            PreviewToolStripMenuItem.Click += PreviewToolStripMenuItem_Click;
            PrintToolStripMenuItem.Click += PrintToolStripMenuItem_Click;
            mnuPageSetup.Click += mnuPageSetup_Click;
            tbrSave.Click += tbrSave_Click;
            tbrFind.Click += tbrFind_Click;
        }

        private string currentFile;

        private void frmScript_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AllowClose())
            {
                e.Cancel = true;
                return;
            }
            SdApplication.SoleInstance.NoteFormClosing(this, e);
            Visible = false;
            MdiParent = null;
        }

        private void frmScript_TextChanged(object sender, EventArgs e)
        {
            dirty = true;
        }

        #region Menu Methods

        /*
        private void NewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (rtbDoc.Modified)
            {
                DialogResult answer = MessageBox.Show("The current document has not been saved, would you like to continue without saving?", "Unsaved Document", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                    rtbDoc.Clear();
                else
                    return;
            }
            else
                rtbDoc.Clear();
            currentFile = "";
            this.Text = "Editor: New Document";
        }
         */

        /*
        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (rtbDoc.Modified)
            {
                DialogResult answer = MessageBox.Show("The current document has not been saved, would you like to continue without saving?", "Unsaved Document", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.No)
                    return;
                else
                    OpenFile();
            }
            else
                OpenFile();
        }
         */

        public override bool OpenFile(string filename, bool isTempFile, string nameToDisplay)
        {
            string strExt = System.IO.Path.GetExtension(filename);
            if (null != strExt)
                strExt = strExt.ToLower();
            if (".rtf".Equals(strExt))
                rtbDoc.LoadFile(filename, RichTextBoxStreamType.RichText);
            else
            {
                using (StreamReader txtReader = new StreamReader(filename))
                {
                    rtbDoc.Text = txtReader.ReadToEnd();
                }
                rtbDoc.SelectionStart = 0;
                rtbDoc.SelectionLength = 0;
            }
            if (!isTempFile)
            {
                currentFile = filename;
                Path = filename;
            }
            rtbDoc.Modified = isTempFile;
            // Set the selected language according to the loaded file
            if (".vb".Equals(strExt))
                cboLanguage.SelectedIndex = 2;
            else if(".cs".Equals(strExt))
                cboLanguage.SelectedIndex = 0;
            return true;
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null == currentFile)
            {
                SaveAsToolStripMenuItem_Click(this, e);
                return;
            }
            string strExt = System.IO.Path.GetExtension(currentFile);
            if (null != strExt)
                strExt = strExt.ToUpper();
            if (".RTF".Equals(strExt))
                rtbDoc.SaveFile(currentFile);
            else
            {
                // to save as plain text
                using (StreamWriter txtWriter = new StreamWriter(currentFile))
                {
                    txtWriter.Write(rtbDoc.Text);
                }
                rtbDoc.SelectionStart = 0;
                rtbDoc.SelectionLength = 0;
                rtbDoc.Modified = false;
            }
            Path = currentFile;
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog1.Title = "Save File";
            SaveFileDialog1.DefaultExt = "rtf";
            SaveFileDialog1.Filter = "Script Files|*.cs;*.vb|All Files|*.*";
            SaveFileDialog1.FilterIndex = 1;
            SaveFileDialog1.ShowDialog(SdApplication.SoleInstance.MainWindow);
            if (SaveFileDialog1.FileName.Length == 0)
                return;
            string strExt = System.IO.Path.GetExtension(SaveFileDialog1.FileName);
            if (null != strExt)
                strExt = strExt.ToUpper();
            if (".RTF".Equals(strExt))
            {
                rtbDoc.SaveFile(SaveFileDialog1.FileName, RichTextBoxStreamType.RichText);
            }
            else
            {
                using (StreamWriter txtWriter = new StreamWriter(SaveFileDialog1.FileName))
                {
                    txtWriter.Write(rtbDoc.Text);
                }
                rtbDoc.SelectionStart = 0;
                rtbDoc.SelectionLength = 0;
            }
            currentFile = SaveFileDialog1.FileName;
            rtbDoc.Modified = false;
            Path = currentFile;
            SdApplication.SoleInstance.NoteRecentFile(currentFile, true);
        }

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                rtbDoc.SelectAll();
            }
            catch (Exception)
            {
                SdApplication.SoleInstance.MsgboxX("Unable to select all document content.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                rtbDoc.Copy();
            }
            catch (Exception)
            {
                SdApplication.SoleInstance.MsgboxX("Unable to copy document content.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                rtbDoc.Cut();
            }
            catch (Exception)
            {
                SdApplication.SoleInstance.MsgboxX("Unable to cut document content.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                rtbDoc.Paste();
            }
            catch (Exception)
            {
                SdApplication.SoleInstance.MsgboxX("Unable to copy clipboard content to document.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PageColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog1.Color = rtbDoc.BackColor;
            if (ColorDialog1.ShowDialog(SdApplication.SoleInstance.MainWindow) == DialogResult.OK)
            {
                rtbDoc.BackColor = ColorDialog1.Color;
            }
        }

        private void mnuUndo_Click(object sender, EventArgs e)
        {
            if (rtbDoc.CanUndo)
                rtbDoc.Undo();
        }

        private void mnuRedo_Click(object sender, EventArgs e)
        {
            if (rtbDoc.CanRedo)
                rtbDoc.Redo();
        }

        private void FindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmScriptFind f = new frmScriptFind(this);
            f.Show();
        }

        private void FindAndReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmScriptReplace f = new frmScriptReplace(this);
            f.Show();
        }

        private void PreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                PrintPreviewDialog1.Document = PrintDocument1;
                PrintPreviewDialog1.ShowDialog(SdApplication.SoleInstance.MainWindow);
            }
            catch
            {
                // Do nothing - errors in Print Preview and the like
            }
        }

        private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrintDialog1.Document = PrintDocument1;
            if (PrintDialog1.ShowDialog(SdApplication.SoleInstance.MainWindow) == DialogResult.OK)
                PrintDocument1.Print();
        }

        private void mnuPageSetup_Click(object sender, EventArgs e)
        {
            PageSetupDialog1.Document = PrintDocument1;
            PageSetupDialog1.ShowDialog(SdApplication.SoleInstance.MainWindow);
        }

        #endregion

        #region Toolbar Methods
        private void tbrSave_Click(object sender, EventArgs e)
        {
            SaveToolStripMenuItem_Click(this, e);
        }

        private void tbrFind_Click(object sender, EventArgs e)
        {
            frmScriptFind f = new frmScriptFind(this);
            f.Show();
        }

        #endregion


        #region IScriptWindow Members

        public string RtfText
        {
            get { return rtbDoc.Rtf; }
            set { rtbDoc.Rtf = value; }
        }

        public string TextInRtfBox
        {
            get { return rtbDoc.Text; }
            set { rtbDoc.Text = value; }
        }

        public void AppendRtfText(string rtf)
        {
            // Move caret to the end of the text
            rtbDoc.Select(rtbDoc.TextLength, 0);

            // Since SelectedRtf is null, this will append the string to the
            // end of the existing RTF
            rtbDoc.SelectedRtf = rtf;
        }

        #endregion

        private void frmScript_Activated(object sender, EventArgs e)
        {
            if (null != Tag)
                SdApplication.SoleInstance.NoteFormActivated((WindowInformation)Tag);
        }

        private string ScriptLanguageForScript()
        {
            switch (cboLanguage.SelectedIndex)
            {
                case 0:
                    return "CSharp";
                case 1:
                    return "R";
                case 2:
                    return "VB";
                default:
                    throw new ArgumentOutOfRangeException("cboLanguage.SelectedIndex", cboLanguage.SelectedIndex, "Unexpected selection");
            }
        }

        private void cmdRun_Click(object sender, EventArgs e)
        {
            rtbOutput.AppendText("\n======== Start of run ========\n");
            try
            {
                string script = rtbDoc.Text;
                IScriptEngine engine = new ScriptEngine();
                string Language = ScriptLanguageForScript();
                object output = engine.Run(Language, script, ScriptType.Method, SdApplication.SoleInstance, null, null, null);
                if (null != output)
                {
                    if (output is ParameterBag)
                    {
                        DumpParameterBag(rtbOutput, (ParameterBag)output, 0);
                    }
                    else
                    {
                        rtbOutput.AppendText(output.ToString());
                        rtbOutput.AppendText("\n");
                    }
                }
            }
            catch (Exception ex)
            {
                rtbOutput.AppendText("Error: " + ex.Message + "\n");
            }
            rtbOutput.AppendText("======== End of run ========\n");
        }

        private void DumpParameterBag(RichTextBox rtb, ParameterBag output, int depth)
        {
            foreach (KeyValuePair<string, FilledParameter> pair in output.Pairs)
            {
                rtb.AppendText(new string(' ', depth * 3));
                rtb.AppendText(pair.Key);
                rtb.AppendText(" <- ");
                if (null == pair.Value || !pair.Value.HasData)
                {
                    rtb.AppendText("null");
                }
                else if (pair.Value.IsParameterBag)
                    DumpParameterBag(rtb, pair.Value.AsParameterBag, depth + 1);
                else if (pair.Value.IsParameterBagList)
                {
                    // Probably a list - let's try it
                    IList<ParameterBag> l = pair.Value.AsParameterBagList;
                    foreach (ParameterBag bag in l)
                    {
                        DumpParameterBag(rtb, bag, depth + 1);
                    }
                }
                else
                {
                    rtb.AppendText(pair.Value.Data.ToString());
                }
                rtb.AppendText("\n");
            }
        }

        private void frmScript_Load(object sender, EventArgs e)
        {
            cboLanguage.SelectedIndex = 0;
        }

        private void frmScript_Shown(object sender, EventArgs e)
        {
            rtbDoc.Focus();
        }

        public override IList<Pane> AvailablePanes
        {
            get
            {
                List<Pane> panes = new List<Pane> {((IForm) this).SelectedPane};
                return panes;
            }
        }

        public override Pane SelectedPane
        {
            get
            {
                return new Pane(Text, WindowInformation, 0);
            }
        }

        public override bool SelectPane(Pane pane)
        {
            // There's only ever one, do nothing
            return true;
        }

        internal override void EditCut()
        {
            rtbDoc.Cut();
        }

        internal override void EditCopy()
        {
            rtbDoc.Copy();
        }

        internal override void EditPaste()
        {
            rtbDoc.Paste();
        }

        internal override void Print()
        {
            PrintDialog1.Document = PrintDocument1;
            if (PrintDialog1.ShowDialog(SdApplication.SoleInstance.MainWindow) == DialogResult.OK)
                PrintDocument1.Print();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        internal override bool SaveContents()
        {
            // Nothing to save
            return true;
        }

        internal override bool SaveAsContents()
        {
            // Nothing to save
            return true;
        }
    }
}

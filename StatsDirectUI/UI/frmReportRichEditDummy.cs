using System;
using System.Windows.Forms;
using StatsDirect.Templates;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using DevExpress.XtraRichEdit.Commands;

namespace StatsDirect.UI
{
    public partial class frmReportRichEditDummy: Form
    {
        public frmReportRichEditDummy()
        {
            InitializeComponent();
        }

        #region Menu Methods

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                new SelectAllCommand(richEditControl1).Execute();
            }
            catch (Exception)
            {
                SDApplication.SoleInstance.msgbox_x("Unable to select all document content.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                new CopySelectionCommand(richEditControl1).Execute();
            }
            catch (Exception)
            {
                SDApplication.SoleInstance.msgbox_x("Unable to copy document content.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                new CutSelectionCommand(richEditControl1).Execute();
            }
            catch (Exception)
            {
                SDApplication.SoleInstance.msgbox_x("Unable to cut document content.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                new PasteSelectionCommand(richEditControl1).Execute();
            }
            catch (Exception)
            {
                SDApplication.SoleInstance.msgbox_x("Unable to copy clipboard content to document.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ShowFontFormCommand(richEditControl1).Execute();
        }

        private void BoldToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleFontBoldCommand(richEditControl1).Execute();
        }

        private void ItalicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleFontItalicCommand(richEditControl1).Execute();
        }

        private void UnderlineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleFontUnderlineCommand(richEditControl1).Execute();
        }

        private void NormalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ClearFormattingCommand(richEditControl1).Execute();
        }

        private void mnuUndo_Click(object sender, EventArgs e)
        {
            new UndoCommand(richEditControl1).Execute();
        }

        private void mnuRedo_Click(object sender, EventArgs e)
        {
            new RedoCommand(richEditControl1).Execute();
        }

        private void LeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleParagraphAlignmentLeftCommand(richEditControl1).Execute();
        }

        private void CenterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleParagraphAlignmentCenterCommand(richEditControl1).Execute();
        }

        private void RightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleParagraphAlignmentRightCommand(richEditControl1).Execute();
        }

        private void AddBulletsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleBulletedListCommand(richEditControl1).Execute();
        }

        private void RemoveBulletsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ToggleBulletedListCommand(richEditControl1).Execute();
        }

        private void FindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditFind();
        }

        private void EditFind()
        {
            new FindCommand(richEditControl1).Execute();
        }

        private void FindAndReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditReplace();
        }

        private void EditReplace()
        {
            new ReplaceCommand(richEditControl1).Execute();
        }

        private void PreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                new PrintPreviewCommand(richEditControl1).Execute();
            }
            catch
            {
                // Do nothing - errors in Print Preview and the like
            }
        }

        private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new PrintCommand(richEditControl1).Execute();
        }

        private void mnuPageSetup_Click(object sender, EventArgs e)
        {
            // TODO: Write me
        }

        private void InsertImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new InsertPictureCommand(richEditControl1).Execute();
        }
        #endregion

        private void tbrBold_Click(object sender, EventArgs e)
        {
            BoldToolStripMenuItem_Click(this, e);
        }

        private void tbrItalic_Click(object sender, EventArgs e){
            ItalicToolStripMenuItem_Click(this, e);
        }

        private void tbrUnderline_Click(object sender, EventArgs e)
        {
            UnderlineToolStripMenuItem_Click(this, e);
        }

        private void tbrFont_Click(object sender, EventArgs e)
        {
            SelectFontToolStripMenuItem_Click(this, e);
        }

        private void tbrLeft_Click(object sender, EventArgs e)
        {
            new ToggleParagraphAlignmentLeftCommand(richEditControl1).Execute();
        }

        private void tbrCenter_Click(object sender, EventArgs e)
        {
            new ToggleParagraphAlignmentCenterCommand(richEditControl1).Execute();
        }

        private void tbrRight_Click(object sender, EventArgs e)
        {
            new ToggleParagraphAlignmentRightCommand(richEditControl1).Execute();
        }

        private void tbrFind_Click(object sender, EventArgs e)
        {
            EditFind();
        }

        #region IReport Members

        public string RtfText
        {
            get { return richEditControl1.Document.RtfText; }
            set
            {
                Document document = richEditControl1.Document;
                document.BeginUpdate();
                richEditControl1.Document.RtfText = value;
                document.EndUpdate();
            }
        }

        public void AppendRtfText(string Rtf, int helpContextId, Operation operation, string redoInformation)
        {
            Document document = richEditControl1.Document;
            // DocumentPosition initialEnd = document.Range.End;
            document.BeginUpdate();

            // Append the text, surrounding it with the specified help context if required
            document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!help!-> " + helpContextId.ToString() + @" <-!help!! }}");
            // Add redo information if present
            if (!string.IsNullOrEmpty(redoInformation))
            {
                string safeXml = redoInformation.Replace(@"\", "&#92;");
                document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!redo!-> " + "\"" + operation.Name + "\" " + safeXml + @" <-!redo!! }}");
            }
            string[] splitInserts = Rtf.Split(new[] { "/split/" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string piece in splitInserts)
                if (piece.StartsWith(@"{\rtf"))
                    document.InsertRtfText(document.Range.End, piece);

            // TODO: Reset help at the end of the document.  For now (DXperience 10.2), this causes hidden text at the end, making insertion of text hard.
            // document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!help!-> " + AmbientHelpContextId.ToString() + @" <-!help!! }}");

            // Ensure the appended text is visible by scrolling the selection into view - the selection is the caret at the end of the old text
            // document.CaretPosition = initialEnd;
            document.CaretPosition = document.Range.End;

            document.EndUpdate();

            richEditControl1.ScrollToCaret();
        }

        #endregion

        private void EditToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            exportGraphicToolStripMenuItem.Enabled = IsImageSelected;
        }

        private bool IsImageSelected
        {
            get
            {
                string rtf = richEditControl1.Document.GetRtfText(richEditControl1.Document.Selection);
                if (rtf.Contains(@"{\pict"))
                {
                    string rtfFromPict = rtf.Substring(rtf.IndexOf(@"{\pict", StringComparison.Ordinal) + 6);
                    if (rtfFromPict.Contains("}"))
                    {
                        string trimmedRtf = rtfFromPict.Substring(0, rtfFromPict.IndexOf("}", StringComparison.Ordinal));
                        return trimmedRtf.Length > 0;
                    }
                }
                return false;
            }
        }

        private void tbrParagraph_Click(object sender, EventArgs e)
        {
            new ShowParagraphFormCommand(richEditControl1).Execute();
        }

        private void tbrTabs_Click(object sender, EventArgs e)
        {
            new ShowTabsFormCommand(richEditControl1).Execute();
        }

        private void tbrInsertSymbol_Click(object sender, EventArgs e)
        {
            new ShowSymbolFormCommand(richEditControl1).Execute();
        }

        private void tbrToggleBullets_Click(object sender, EventArgs e)
        {
            new ToggleBulletedListCommand(richEditControl1).Execute();
        }

        private void tbrToggleNumbers_Click(object sender, EventArgs e)
        {
            new ToggleSimpleNumberingListCommand(richEditControl1).Execute();
        }

        private void tbrSubscript_Click(object sender, EventArgs e)
        {
            new ToggleFontSubscriptCommand(richEditControl1).Execute();
        }

        private void tbrSuperscript_Click(object sender, EventArgs e)
        {
            new ToggleFontSuperscriptCommand(richEditControl1).Execute();
        }

        private void tbrDecreaseIndent_Click(object sender, EventArgs e)
        {
            new DecrementIndentCommand(richEditControl1).Execute();
        }

        private void tbrIncreaseIndent_Click(object sender, EventArgs e)
        {
            new IncrementIndentCommand(richEditControl1).Execute();
        }

        private void tbrZoomOut_Click(object sender, EventArgs e)
        {
            new ZoomOutCommand(richEditControl1).Execute();
        }

        private void tbrZoomIn_Click(object sender, EventArgs e)
        {
            new ZoomInCommand(richEditControl1).Execute();
        }

        private void tbrDraft_Click(object sender, EventArgs e)
        {
            richEditControl1.ActiveViewType = RichEditViewType.Draft;
        }

        private void tbrPrintLayout_Click(object sender, EventArgs e)
        {
            richEditControl1.ActiveViewType = RichEditViewType.PrintLayout;
        }

        private void tbrIndentedList_Click(object sender, EventArgs e)
        {
            new ToggleMultiLevelListCommand(richEditControl1).Execute();
        }

        private void tbrSimple_Click(object sender, EventArgs e)
        {
            richEditControl1.ActiveViewType = RichEditViewType.Simple;
        }

        private void frmReportRichEditDummy_Shown(object sender, EventArgs e)
        {
            Close();
        }
    }
}

using System.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using System.IO;
using StatsDirect.Charting;
using StatsDirect.Templates;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using DevExpress.XtraRichEdit.Commands;
using System.Drawing.Imaging;
using StatsDirect.Utilities;
using DevExpress.XtraRichEdit.Services;

namespace StatsDirect.UI
{
    public partial class frmReportRichEdit : StatsDirectForm, IReport
    {
        public frmReportRichEdit()
        {
            InitializeComponent();
            LoadTemplateFile();
            SdApplication.SoleInstance.MainWindow.EnsureBuiltInMenuItemsCanShowHelp(MenuStrip1);
            SetCustomCommandFactory(); // #1202: Paste in table form
        }

        private void SetCustomCommandFactory()
        {
            CustomRichEditCommandFactoryService commandFactory = new CustomRichEditCommandFactoryService(richEditControl1, richEditControl1.GetService<IRichEditCommandFactoryService>());
            richEditControl1.RemoveService(typeof(IRichEditCommandFactoryService));
            richEditControl1.AddService(typeof(IRichEditCommandFactoryService), commandFactory);
        }

        private string currentFile;

        private void frmReport_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AllowClose())
            {
                e.Cancel = true;
                return;
            }
            UnmergeToolStrip();
            SdApplication.SoleInstance.NoteFormClosing(this, e);
            Visible = false;
            MdiParent = null;
        }

        private void frmReport_TextChanged(object sender, EventArgs e)
        {
            dirty = true;
        }

        public override bool OpenFile(string filename, bool isTempFile, string nameToDisplay)
        {
            string strExt = System.IO.Path.GetExtension(filename) ?? string.Empty;
            strExt = strExt.ToLower(CultureInfo.InvariantCulture);
            if (".rtf".Equals(strExt))
                richEditControl1.LoadDocument(filename, DocumentFormat.Rtf);
            else if (".htm".Equals(strExt) || ".html".Equals(strExt))
                richEditControl1.LoadDocument(filename, DocumentFormat.Html);
            else if (".mht".Equals(strExt) || ".mhtml".Equals(strExt))
                richEditControl1.LoadDocument(filename, DocumentFormat.Mht);
            else
            {
                using (StreamReader txtReader = new StreamReader(filename))
                {
                    richEditControl1.Text = txtReader.ReadToEnd();
                }
            }
            if (!isTempFile)
            {
                currentFile = filename;
                Path = filename;
            }
            return true;
        }

        private void DoOrWarn(Action func, string explanation)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                func();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError(explanation, ex, false);
            }
#endif
        }

        private void DoOrSwallow(Action func)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                func();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception)
            {
                // TODO: It'd be nice to know that the exception happened for our diagnostic purposes.
            }
#endif
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(() => SaveContents(), "Couldn't save file");
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(() => SaveAsContents(), "Couldn't save file");
        }

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditSelectAll, "Unable to select all document content");
        }

        private void EditSelectAll()
        {
            new SelectAllCommand(richEditControl1).Execute();
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditCopy, "Unable to copy document content");
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditCut, "Unable to cut document content");
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditPaste, "Unable to copy clipboard content to document");
        }

        private void SelectFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ShowFontForm, "Unable to select font");
        }

        private void ShowFontForm()
        {
            new ShowFontFormCommand(richEditControl1).Execute();
        }

        private void BoldToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleBold, "Unable to set bold font");
        }

        private void ToggleBold()
        {
            new ToggleFontBoldCommand(richEditControl1).Execute();
        }

        private void ItalicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleItalic, "Unable to set italic font");
        }

        private void ToggleItalic()
        {
            new ToggleFontItalicCommand(richEditControl1).Execute();
        }

        private void UnderlineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleUnderline, "Unable to set underline font");
        }

        private void ToggleUnderline()
        {
            new ToggleFontUnderlineCommand(richEditControl1).Execute();
        }

        private void NormalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ClearFormatting, "Unable to clear formatting");
        }

        private void ClearFormatting()
        {
            new ClearFormattingCommand(richEditControl1).Execute();
        }

        private void mnuUndo_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditUndo, "Unable to undo");
        }

        private void mnuRedo_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditRedo, "Unable to redo");
        }

        private void LeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(AlignLeft, "Unable to set left alignment");
        }

        private void AlignLeft()
        {
            new ToggleParagraphAlignmentLeftCommand(richEditControl1).Execute();
        }

        private void CenterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(AlignCenter, "Unable to set centre alignment");
        }

        private void AlignCenter()
        {
            new ToggleParagraphAlignmentCenterCommand(richEditControl1).Execute();
        }

        private void RightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(AlignRight, "Unable to set right alignment");
        }

        private void AlignRight()
        {
            new ToggleParagraphAlignmentRightCommand(richEditControl1).Execute();
        }

        private void AddBulletsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleBullets, "Unable to set bullets");
        }

        private void ToggleBullets()
        {
            new ToggleBulletedListCommand(richEditControl1).Execute();
        }

        private void RemoveBulletsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleBullets, "Unable to set bullets");
        }

        private void FindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditFind, "Cannot start find");
        }

        private void EditFind()
        {
            new FindCommand(richEditControl1).Execute();
        }

        private void FindAndReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditReplace, "Cannot start replace");
        }

        private void EditReplace()
        {
            new ReplaceCommand(richEditControl1).Execute();
        }

        private void PreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(PrintPreview, "Couldn't print preview");
        }

        private void PrintPreview()
        {
            new PrintPreviewCommand(richEditControl1).Execute();
        }

        private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(Print, "Couldn't print");
        }

        private void mnuPageSetup_Click(object sender, EventArgs e)
        {
            // TODO: Write me
        }

        private void InsertImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(InsertPicture, "Couldn't insert image");
        }

        private void InsertPicture()
        {
            new InsertPictureCommand(richEditControl1).Execute();
        }

        private void tbrBold_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleBold, "Unable to set bold font");
        }

        private void tbrItalic_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleItalic, "Unable to set italic font");
        }

        private void tbrUnderline_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleUnderline, "Unable to set underline font");
        }

        private void tbrFont_Click(object sender, EventArgs e)
        {
            DoOrWarn(ShowFontForm, "Unable to select font");
        }

        private void tbrLeft_Click(object sender, EventArgs e)
        {
            DoOrWarn(AlignLeft, "Unable to set left alignment");
        }

        private void tbrCenter_Click(object sender, EventArgs e)
        {
            DoOrWarn(AlignCenter, "Unable to set centre alignment");
        }

        private void tbrRight_Click(object sender, EventArgs e)
        {
            DoOrWarn(AlignRight, "Unable to set right alignment");
        }

        private void tbrFind_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditFind, "Couldn't start find");
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

        public void AppendRtfText(string rtf, int helpContextId, Operation operation, string redoInformation)
        {
            Document document = richEditControl1.Document;
            int initialEnd = document.Range.End.ToInt();
            document.BeginUpdate();

            // Append the text, surrounding it with the specified help context if required
            document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!help!-> " + helpContextId + @" <-!help!! }}");
            // Add redo information if present
            if (!string.IsNullOrEmpty(redoInformation))
            {
                string safeXml = redoInformation.Replace(@"\", "&#92;");
                document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!redo!-> " + "\"" + operation.Name + "\" " + safeXml + @" <-!redo!! }}");
            }
            string[] splitInserts = rtf.Split(new[] { "/split/" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string piece in splitInserts)
                if (piece.StartsWith(@"{\rtf"))
                    document.InsertRtfText(document.Range.End, piece);

            // TODO: Reset help at the end of the document.  For now (DXperience 10.2), this causes hidden text at the end, making insertion of text hard.
            // document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!help!-> " + AmbientHelpContextId.ToString() + @" <-!help!! }}");

            // Ensure the appended text is visible by scrolling the selection into view - the selection is the caret at the end of the old text

            document.EndUpdate();

            FixupNewTables();

            // document.CaretPosition = document.CreatePosition(initialEnd);
            // richEditControl1.ScrollToCaret(0);
            document.CaretPosition = document.Range.End;
            richEditControl1.ScrollToCaret(1);
        }

        #endregion


        private string GetFreezeDriedData()
        {
            return GetHiddenTextEnclosedBy("redo");
        }

        private string GetHiddenTextEnclosedBy(string enclosure)
        {
            string enclosedEnclosureStart = "!!" + enclosure + "!->";
            string enclosedEnclosureEnd = "<-!" + enclosure + "!!";

            Document document = richEditControl1.Document;

            // Find the current caret...
            DocumentRange startToCaret = document.CreateRange(0, document.Selection.Start.ToInt());
            // ... search backwards for hidden text from that point.
            string textToSelectionPoint = document.GetRtfText(startToCaret);
            int firstBoundary = textToSelectionPoint.LastIndexOf(enclosedEnclosureStart, StringComparison.Ordinal);
            while (firstBoundary >= 0)
            {
                int secondBoundary = textToSelectionPoint.IndexOf(enclosedEnclosureEnd, firstBoundary + enclosedEnclosureStart.Length, StringComparison.Ordinal);
                if (secondBoundary >= 0)
                {
                    string middle = textToSelectionPoint.Substring(firstBoundary + enclosedEnclosureStart.Length, secondBoundary - (firstBoundary + enclosedEnclosureStart.Length));
                    return middle.Trim();
                }
                textToSelectionPoint = textToSelectionPoint.Substring(0, firstBoundary);
                firstBoundary = textToSelectionPoint.LastIndexOf(enclosedEnclosureStart, StringComparison.Ordinal);
            }

            return null;
        }

        internal override void ShowHelp()
        {
            string helpString = GetHiddenTextEnclosedBy("help");
            int helpId;
            if (int.TryParse(helpString, out helpId))
            {
                SdApplication.SoleInstance.ShowHelp(SdApplication.SoleInstance.MainWindow, helpId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                SdApplication.SoleInstance.ShowHelp(SdApplication.SoleInstance.MainWindow);
            }
        }

        private void frmReport_Shown(object sender, EventArgs e)
        {
            DoOrSwallow(() => { MergeToolStrip(); richEditControl1.Focus(); });
        }

        private void LoadTemplateFile()
        {
            string pathName = SdApplication.SoleInstance.TemplateFileForNewReports;
            if (null != pathName)
                richEditControl1.LoadDocument(pathName, DocumentFormat.Rtf);
        }

        private void frmReport_Activated(object sender, EventArgs e)
        {
            DoOrSwallow(() =>
            {
                MergeToolStrip();
                if (null != Tag)
                    SdApplication.SoleInstance.NoteFormActivated((WindowInformation)Tag);
                richEditControl1.Visible = true;
                richEditControl1.Focus();
            });
        }

        public override IList<Pane> AvailablePanes
        {
            get
            {
                return new List<Pane> {((IForm) this).SelectedPane};
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

        internal override bool SaveContents()
        {
            if (null == currentFile)
            {
                return SaveAsContents();
            }
            string strExt = System.IO.Path.GetExtension(currentFile);
            strExt = strExt.ToUpper(CultureInfo.InvariantCulture);
            richEditControl1.SaveDocument(currentFile, ".RTF".Equals(strExt) ? DocumentFormat.Rtf : DocumentFormat.PlainText);
            Text = "Editor: " + currentFile;
            richEditControl1.Modified = false;
            return true;
        }

        internal override bool SaveAsContents()
        {
            SaveFileDialog1.Title = "Save report";
            SaveFileDialog1.DefaultExt = "rtf";
            SaveFileDialog1.Filter = "Rich text files (*.rtf)|*.rtf|Text files (*.txt)|*.txt|HTML files (*.htm, *.html)|*.htm;*.html|MHTML files (*.mht, *.mhtml)|*.mht;*.mhtml|All files (*.*)|*.*";
            SaveFileDialog1.FilterIndex = 1;
            if (null == currentFile)
            {
                SaveFileDialog1.FileName = ((WindowInformation)Tag).FriendlyName;
            }
            else
            {
                SaveFileDialog1.InitialDirectory = System.IO.Path.GetDirectoryName(currentFile);
                SaveFileDialog1.FileName = System.IO.Path.GetFileName(currentFile);
            }
            DialogResult res = SaveFileDialog1.ShowDialog(SdApplication.SoleInstance.MainWindow);
            if (res != DialogResult.OK)
                return false;
            if (string.IsNullOrEmpty(SaveFileDialog1.FileName))
                return false;
            string strExt = System.IO.Path.GetExtension(SaveFileDialog1.FileName) ?? string.Empty;
            strExt = strExt.ToUpper(CultureInfo.InvariantCulture);
            if (".RTF".Equals(strExt))
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.Rtf);
            else if (".HTM".Equals(strExt) || ".HTML".Equals(strExt))
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.Html);
            else if (".MHT".Equals(strExt) || ".MHTML".Equals(strExt))
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.Mht);
            else
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.PlainText);
            currentFile = SaveFileDialog1.FileName;
            SdApplication.SoleInstance.NoteRecentFile(currentFile, true);
            Text = currentFile;
            ((WindowInformation)Tag).Path = currentFile;
            richEditControl1.Modified = false;
            return true;
        }

        internal override void Print()
        {
            new PrintCommand(richEditControl1).Execute();
        }

        private void showRuler_CheckedChanged(object sender, EventArgs e)
        {
            RichEditRulerVisibility vis = showRuler.Checked ? RichEditRulerVisibility.Visible : RichEditRulerVisibility.Hidden;
            richEditControl1.Options.HorizontalRuler.Visibility = vis;
            richEditControl1.Options.VerticalRuler.Visibility = vis;
        }

        private void frmReport_Deactivate(object sender, EventArgs e)
        {
            DoOrSwallow(UnmergeToolStrip);
        }

        private void MergeToolStrip()
        {
            IToolStripHost host = (IToolStripHost)ParentForm;
            if (null != host)
                host.AppendToolStrip(localToolStrip);
        }

        private void UnmergeToolStrip()
        {
            IToolStripHost host = (IToolStripHost)ParentForm;
            if (null != host)
                host.RemoveToolStrip(localToolStrip);
        }

        internal override void EditCut()
        {
            new CutSelectionCommand(richEditControl1).Execute();
        }

        internal override void EditCopy()
        {
            if (IsImageSelected)
            {
                byte[] bytes;
                Image img = GetSelectedImage(out bytes);
                if (null != bytes)
                {
                    Metafile mf = (Metafile)img;
                    ClipboardMetafileHelper.PutEnhMetafileOnClipboard(Handle, mf);
                }
            }
            else
                new CopySelectionCommand(richEditControl1).Execute();
        }

        internal override void EditPaste()
        {
            new PasteSelectionCommand(richEditControl1).Execute();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrSwallow(Close);
        }

        private void exportGraphicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ExportSelectedImage, "Couldn't export image");
        }

        private void ExportSelectedImage()
        {
            byte[] bytes;
            Image img = GetSelectedImage(out bytes);
            if (null != bytes)
            {
                using (frmExportGraphic f = new frmExportGraphic(img, bytes))
                {
                    f.ShowDialog(SdApplication.SoleInstance.MainWindow);
                }
            }
            else
                SdApplication.SoleInstance.MsgboxX("No chart is selected. Please select a chart to export.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Export graphic", true);
        }

        private Image GetSelectedImage(out byte[] rawBytes)
        {
            rawBytes = null;

            // TODO: Better approach
            string rtf = richEditControl1.Document.GetRtfText(richEditControl1.Document.Selection);
            if (!rtf.Contains(@"{\pict"))
                return null;

            string rtfFromPict = rtf.Substring(rtf.IndexOf(@"{\pict", StringComparison.Ordinal) + 6);
            if (!rtfFromPict.Contains("}"))
                return null;

            string trimmedRtf = rtfFromPict.Substring(0, rtfFromPict.IndexOf("}", StringComparison.Ordinal));
            if (0 == trimmedRtf.Length)
                return null;

            return RtfImageConverter.ParseRtfToImage(trimmedRtf, out rawBytes);
        }

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

        private void exportGraphicContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ExportSelectedImage, "Couldn't export image");
        }

        private void cutContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditCut, "Couldn't cut");
        }

        private void copyContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditCopy, "Couldn't copy");
        }

        private void pasteContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditPaste, "Couldn't paste");
        }

        private void undoContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditUndo, "Couldn't undo");
        }

        private void EditUndo()
        {
            new UndoCommand(richEditControl1).Execute();
        }

        private void EditRedo()
        {
            new RedoCommand(richEditControl1).Execute();
        }

        private void findContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditFind, "Couldn't start find");
        }

        private void replaceContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditReplace, "Couldn't start replace");
        }

        private void deleteContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditDelete, "Couldn't delete");
        }

        private void EditDelete()
        {
            new DeleteCommand(richEditControl1).Execute();
        }

        private void ReplayOperation(object sender, EventArgs e)
        {
            try
            {
                ReplayOperation();
            }
            catch (TemplateOperationCancelledException)
            {
                // The operation was cancelled during replay.  Do nothing.
            }
#if !WATCH_EXCEPTIONS
            catch (Exception ex)
            {
                if (SdApplication.SoleInstance.MainWindow.InOperation)
                {
                    SdApplication.SoleInstance.MainWindow.PuntThroughEventLoop(ex);
                }
                else
                {
                    // Normally we'd just throw the exception; in this case, we're the top of the stack and that would bring the application down.  So we report instead.
                    SdApplication.SoleInstance.FriendlyError("An internal error occurred while replaying the operation", ex, true);
                }
            }
#endif
        }

        private void ReplayOperation()
        {
            string freezeDriedData = GetFreezeDriedData();
            if (null != freezeDriedData)
            {
                // Operation is delimited by quotes and comes first
                int firstQuote = freezeDriedData.IndexOf('"');
                int secondQuote = freezeDriedData.IndexOf('"', firstQuote + 1);
                string operationName = freezeDriedData.Substring(firstQuote + 1, secondQuote - (firstQuote + 1));
                string freezeDriedParameters = freezeDriedData.Substring(secondQuote + 1).Trim();
                SdApplication.SoleInstance.ReplayWithCurrentData(operationName, freezeDriedParameters);
            }
        }

        private bool IsSelectionReplayable
        {
            get
            {
                return null != GetFreezeDriedData();
            }
        }

        private void richEditControl1_PopupMenuShowing(object sender, PopupMenuShowingEventArgs e)
        {
            DoOrSwallow(() =>
                {
                    if (IsSelectionReplayable)
                        e.Menu.Items.Add(new DevExpress.Utils.Menu.DXMenuItem("Replay Operation", ReplayOperation));
                    if (IsImageSelected)
                        e.Menu.Items.Add(new DevExpress.Utils.Menu.DXMenuItem("Export Graphic", ExportGraphic));
                });
        }

        private void ExportGraphic(object sender, EventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                ExportSelectedImage();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                if (SdApplication.SoleInstance.MainWindow.InOperation)
                {
                    SdApplication.SoleInstance.MainWindow.PuntThroughEventLoop(ex);
                }
                else
                {
                    // Normally we'd just throw the exception; in this case, we're the top of the stack and that would bring the application down.  So we report instead.
                    SdApplication.SoleInstance.FriendlyError("An internal error occurred while exporting the graphic", ex, true);
                }
            }
#endif
        }

        private void tbrParagraph_Click(object sender, EventArgs e)
        {
            DoOrWarn(ShowParagraphForm, "Couldn't show paragraph properties");
        }

        private void ShowParagraphForm()
        {
            new ShowParagraphFormCommand(richEditControl1).Execute();
        }

        private void tbrTabs_Click(object sender, EventArgs e)
        {
            DoOrWarn(ShowTabs, "Couldn't show tabs");
        }

        private void ShowTabs()
        {
            new ShowTabsFormCommand(richEditControl1).Execute();
        }

        private void tbrInsertSymbol_Click(object sender, EventArgs e)
        {
            DoOrWarn(ShowInsertSymbolForm, "Couldn't show Insert Symbol form");
        }

        private void ShowInsertSymbolForm()
        {
            new ShowSymbolFormCommand(richEditControl1).Execute();
        }

        private void tbrToggleBullets_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleBullets, "Couldn't set bullets");
        }

        private void tbrToggleNumbers_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleNumbers, "Couldn't set numbers");
        }

        private void ToggleNumbers()
        {
            new ToggleSimpleNumberingListCommand(richEditControl1).Execute();
        }

        private void tbrSubscript_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleSubscript, "Couldn't set subscript");
        }

        private void ToggleSubscript()
        {
            new ToggleFontSubscriptCommand(richEditControl1).Execute();
        }

        private void tbrSuperscript_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleSuperscript, "Couldn't set superscript");
        }

        private void ToggleSuperscript()
        {
            new ToggleFontSuperscriptCommand(richEditControl1).Execute();
        }

        private void tbrDecreaseIndent_Click(object sender, EventArgs e)
        {
            DoOrWarn(DecreaseIndent, "Couldn't decrease indent");
        }

        private void DecreaseIndent()
        {
            new DecrementIndentCommand(richEditControl1).Execute();
        }

        private void tbrIncreaseIndent_Click(object sender, EventArgs e)
        {
            DoOrWarn(IncreaseIndent, "Couldn't increase indent");
        }

        private void IncreaseIndent()
        {
            new IncrementIndentCommand(richEditControl1).Execute();
        }

        private void tbrZoomOut_Click(object sender, EventArgs e)
        {
            DoOrWarn(ZoomOut, "Couldn't set zoom");
        }

        private void ZoomOut()
        {
            new ZoomOutCommand(richEditControl1).Execute();
        }

        private void tbrZoomIn_Click(object sender, EventArgs e)
        {
            DoOrWarn(ZoomIn, "Couldn't zoom in");
        }

        private void ZoomIn()
        {
            new ZoomInCommand(richEditControl1).Execute();
        }

        private void tbrDraft_Click(object sender, EventArgs e)
        {
            DoOrWarn(SetDraftLayout, "Couldn't set draft layout");
        }

        private void SetDraftLayout()
        {
            richEditControl1.ActiveViewType = RichEditViewType.Draft;
        }

        private void tbrPrintLayout_Click(object sender, EventArgs e)
        {
            DoOrWarn(SetPrintLayout, "Couldn't set print layout");
        }

        private void SetPrintLayout()
        {
            richEditControl1.ActiveViewType = RichEditViewType.PrintLayout;
        }

        private void tbrIndentedList_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleMultiLevelList, "Couldn't set multi-level list");
        }

        private void ToggleMultiLevelList()
        {
            new ToggleMultiLevelListCommand(richEditControl1).Execute();
        }

        private void richEditControl1_ModifiedChanged(object sender, EventArgs e)
        {
            dirty = richEditControl1.Modified;
        }

        private void tbrSimple_Click(object sender, EventArgs e)
        {
            DoOrWarn(SetSimpleLayout, "Couldn't set simple layout");
        }

        private void SetSimpleLayout()
        {
            richEditControl1.ActiveViewType = RichEditViewType.Simple;
        }

        void richEditControl1_KeyUp(object sender, KeyEventArgs e)
        {
            DoOrSwallow(() =>
            {
                if (e.Control && e.KeyValue == 67)
                {
                    EditCopy();
                    e.Handled = true;
                }
            });
        }

        private void insertDateAndTimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(InsertDateAndTime, "Couldn't insert date and time");
        }

        private void InsertDateAndTime()
        {
            richEditControl1.Document.InsertText(richEditControl1.Document.CaretPosition, DateTime.Now.ToString("dd MMMM yyyy @ hh:MM:ss"));
        }

        private void FixupTableAt(DocumentRange rng)
        {
            Document doc = richEditControl1.Document;

            // Delete the found text - do this before we autofit!
            doc.Delete(rng);

            // Autofit
            DocumentPosition pos = rng.Start;
            TableCell cell = doc.Tables.GetTableCell(pos);
            if (null != cell)
                FixupTable(cell.Table);
        }

        private void FixupTable(Table table)
        {
            table.BeginUpdate();
            try
            {
                table.TableLayout = TableLayoutType.Autofit;
                table.ForEachCell(((cell, rowIndex, cellIndex) =>
                {
                    cell.PreferredWidthType = WidthType.Auto;
                }));
            }
            finally
            {
                table.EndUpdate();
            }
        }

        /// <summary>
        /// New tables that have been inserted by the Creole renderer have the string !!FIRSTCELLOFTABLE!! at the end of the first cell in the first row.  Use this as a marker, removing it.
        /// </summary>
        private void FixupNewTables()
        {
            Document doc = richEditControl1.Document;
            string txt = doc.GetText(doc.Range);
            string rtf = doc.GetRtfText(doc.Range);
            int startPos = doc.Range.Start.ToInt();
            int length = doc.Range.End.ToInt() - startPos;
            DocumentRange range = doc.CreateRange(startPos, length);
            ISearchResult searchResult = doc.StartSearch("!!FIRSTCELLOFTABLE!!" , SearchOptions.None, SearchDirection.Forward, range);
            // StartSearch merely sets up the search; use FindNext to find the first result.
            while (searchResult.FindNext())
                FixupTableAt(searchResult.CurrentResult);
        }
    }

    public class CustomRichEditCommandFactoryService : IRichEditCommandFactoryService
    {
        readonly IRichEditCommandFactoryService service;
        readonly RichEditControl control;

        public CustomRichEditCommandFactoryService(RichEditControl control, IRichEditCommandFactoryService service)
        {
            DevExpress.Utils.Guard.ArgumentNotNull(control, "control");
            DevExpress.Utils.Guard.ArgumentNotNull(service, "service");
            this.control = control;
            this.service = service;
        }

        public RichEditCommand CreateCommand(RichEditCommandId id)
        {
            if (id == RichEditCommandId.CopySelection)
                return new CustomCopySelectionCommand(control);
            return service.CreateCommand(id);
        }
    }
}

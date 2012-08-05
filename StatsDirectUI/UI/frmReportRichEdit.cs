// Comment for production
#define WATCH_EXCEPTIONS

using System.Drawing;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

using StatsDirect.Templates;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using DevExpress.XtraRichEdit.Commands;
using System.Drawing.Imaging;

namespace StatsDirect.UI
{
    public partial class frmReportRichEdit : StatsDirectForm, IReport
    {
        public frmReportRichEdit()
        {
            InitializeComponent();
            LoadTemplateFile();
            SDApplication.SoleInstance.MainWindow.EnsureBuiltInMenuItemsCanShowHelp(MenuStrip1);
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
            SDApplication.SoleInstance.NoteFormClosing(this, e);
        }

        private void frmReport_TextChanged(object sender, EventArgs e)
        {
            dirty = true;
        }

        public override bool OpenFile(string Filename)
        {
            string strExt = System.IO.Path.GetExtension(Filename) ?? "";
            strExt = strExt.ToLower();
            if (".rtf".Equals(strExt))
                richEditControl1.LoadDocument(Filename, DocumentFormat.Rtf);
            else if (".htm".Equals(strExt) || ".html".Equals(strExt))
                richEditControl1.LoadDocument(Filename, DocumentFormat.Html);
            else if (".mht".Equals(strExt) || ".mhtml".Equals(strExt))
                richEditControl1.LoadDocument(Filename, DocumentFormat.Mht);
            else
            {
                using (StreamReader txtReader = new StreamReader(Filename))
                {
                    richEditControl1.Text = txtReader.ReadToEnd();
                }
            }
            currentFile = Filename;
            Path = Filename;
            return true;
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveContents();
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAsContents();
        }

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                new SelectAllCommand(richEditControl1).Execute();
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to select all document content.", "RTE - Select", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                EditCopy();
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to copy document content.", "RTE - Copy", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                EditCut();
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to cut document content.", "RTE - Cut", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                EditPaste();
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to copy clipboard content to document.", "RTE - Paste", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        public void AppendRtfText(string rtf, int helpContextId, Operation operation, string redoInformation)
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
                document.InsertRtfText(document.Range.End, @"{\rtf1\ansi {\v !!redo!-> " + "\"" + operation.Names[0] + "\" " + safeXml + @" <-!redo!! }}");
            }
            string[] splitInserts = rtf.Split(new[] { "/split/" }, StringSplitOptions.RemoveEmptyEntries);
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
                SDApplication.SoleInstance.ShowHelp(SDApplication.SoleInstance.MainWindow, helpId.ToString());
            }
            else
            {
                SDApplication.SoleInstance.ShowHelp(SDApplication.SoleInstance.MainWindow);
            }
        }

        private void frmReport_Shown(object sender, EventArgs e)
        {
            MergeToolStrip();
            richEditControl1.Focus();
        }

        private void LoadTemplateFile()
        {
            string pathName = SDApplication.SoleInstance.TemplateFileForNewReports;
            if (null != pathName)
                richEditControl1.LoadDocument(pathName, DocumentFormat.Rtf);
        }

        public override bool ImplementsIReport
        {
            get { return true; }
        }

        private void frmReport_Activated(object sender, EventArgs e)
        {
            MergeToolStrip();
            if (null != Tag)
                SDApplication.SoleInstance.NoteFormActivated((WindowInformation)Tag);
            richEditControl1.Visible = true;
            richEditControl1.Focus();
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

        internal override bool SaveContents()
        {
            if (null == currentFile)
            {
                return SaveAsContents();
            }
            string strExt = System.IO.Path.GetExtension(currentFile) ?? "";
            strExt = strExt.ToUpper();
            richEditControl1.SaveDocument(currentFile,
                                          ".RTF".Equals(strExt) ? DocumentFormat.Rtf : DocumentFormat.PlainText);
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
            DialogResult res = SaveFileDialog1.ShowDialog();
            if (res != DialogResult.OK)
                return false;
            if (string.IsNullOrEmpty(SaveFileDialog1.FileName))
                return false;
            string strExt = System.IO.Path.GetExtension(SaveFileDialog1.FileName) ?? "";
            strExt = strExt.ToUpper();
            if (".RTF".Equals(strExt))
            {
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.Rtf);
            }
            else if (".HTM".Equals(strExt) || ".HTML".Equals(strExt))
            {
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.Html);
            }
            else if (".MHT".Equals(strExt) || ".MHTML".Equals(strExt))
            {
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.Mht);
            }
            else
            {
                richEditControl1.SaveDocument(SaveFileDialog1.FileName, DocumentFormat.PlainText);
            }
            currentFile = SaveFileDialog1.FileName;
            SDApplication.SoleInstance.NoteRecentFile(currentFile);
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
            UnmergeToolStrip();
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
            {
                new CopySelectionCommand(richEditControl1).Execute();
            }
        }

        internal override void EditPaste()
        {
            new PasteSelectionCommand(richEditControl1).Execute();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void exportGraphicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportSelectedImage();
        }

        private void ExportSelectedImage()
        {
            byte[] bytes;
            Image img = GetSelectedImage(out bytes);
            if (null != bytes)
            {
                using (frmExportGraphic f = new frmExportGraphic(img, bytes))
                {
                    f.ShowDialog(SDApplication.SoleInstance.MainWindow);
                }
            }
            else
            {
                SDApplication.SoleInstance.msgbox_x("No chart is selected. Please select a chart to export.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Export graphic", true);
            }
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

            return ParseRtfToImage(trimmedRtf, out rawBytes);
        }

        private Image ParseRtfToImage(string rtf, out byte[] rawBytes)
        {
            string[] parts = rtf.Split(new[] { '\\', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            ImageFormat imageFormat = null;
            using (MemoryStream bytes = new MemoryStream())
            {
                foreach (string s in parts)
                {
                    if (s.StartsWith("wmetafile"))
                    {
                        imageFormat = ImageFormat.Emf;
                        int wmetafileVersion;
                        int.TryParse(s.Substring(9), out wmetafileVersion);
                    }
                    else if (s.StartsWith("pngblip"))
                        imageFormat = ImageFormat.Png;
                    else if (s.StartsWith("picwgoal"))
                    {
                        int wGoal;
                        int.TryParse(s.Substring(8), out wGoal);
                    }
                    else if (s.StartsWith("pichgoal"))
                    {
                        int hGoal;
                        int.TryParse(s.Substring(8), out hGoal);
                    }
                    else if (s.StartsWith("picw"))
                    {
                        int w;
                        int.TryParse(s.Substring(4), out w);
                    }
                    else if (s.StartsWith("pich"))
                    {
                        int h;
                        int.TryParse(s.Substring(4), out h);
                    }
                    else if (s.StartsWith("emfblip"))
                    {
                        imageFormat = ImageFormat.Emf;
                    }
                    else if (s.StartsWith("picscale"))
                    {
                        // Do nothing
                    }
                    else if (s.Length < 16)
                    {
                        // Not a header value we know, and less than an 8-byte hex value - so a very small image!
                        // Assume another header value that we don't yet know about
                        throw new ArgumentException("Unexpected header '" + s + "' when importing image");
                    }
                    else
                    {
                        // Assume bytes encoded as hex
                        for (int i = 0; i < s.Length; i += 2)
                        {
                            int hiChar = s[i] - 48; // 48 is ASCII '0'
                            if (hiChar > 9) hiChar -= 7; // 65 is ASCII 'A' = 10.  48 already subtracted, so need to subtract (65 - 10 - 48) = 7.
                            if (hiChar > 15) hiChar -= 32; // 97 is ASCII 'a' = 10.  65 already subtracted, so need to subtract (97 - 65) = 32.
                            if (hiChar > 15 || hiChar < 0)
                                throw new ArgumentException("Unexpected non-hex char '" + s[i] + "' in hex string");

                            int loChar = s[i + 1] - 48; // 48 is ASCII '0'
                            if (loChar > 9) loChar -= 7; // 65 is ASCII 'A' = 10.  48 already subtracted, so need to subtract (65 - 10 - 48) = 7.
                            if (loChar > 15) loChar -= 32; // 97 is ASCII 'a' = 10.  65 already subtracted, so need to subtract (97 - 65) = 32.
                            if (loChar > 15 || loChar < 0)
                                throw new ArgumentException("Unexpected non-hex char '" + s[i + 1] + "' in hex string");
                            byte b = (byte) (hiChar * 16 + loChar);
                            bytes.WriteByte(b);
                        }
                    }
                }
                rawBytes = bytes.ToArray();
                bytes.Position = 0;
                Image img = null;
                if (imageFormat == ImageFormat.Emf)
                {
                    img = Image.FromStream(bytes);
                }
                else if (imageFormat == ImageFormat.Png)
                {
                    img = Image.FromStream(bytes);
                }
                return img;
            }
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
            ExportSelectedImage();
        }

        private void cutContextMenuItem_Click(object sender, EventArgs e)
        {
            EditCut();
        }

        private void copyContextMenuItem_Click(object sender, EventArgs e)
        {
            EditCopy();
        }

        private void pasteContextMenuItem_Click(object sender, EventArgs e)
        {
            EditPaste();
        }

        private void undoContextMenuItem_Click(object sender, EventArgs e)
        {
            EditUndo();
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
            EditFind();
        }

        private void replaceContextMenuItem_Click(object sender, EventArgs e)
        {
            EditReplace();
        }

        private void deleteContextMenuItem_Click(object sender, EventArgs e)
        {
            EditDelete();
        }

        private void EditDelete()
        {
            new DeleteCommand(richEditControl1).Execute();
        }

        private void ReplayOperation(object sender, EventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                ReplayOperation();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                if (SDApplication.SoleInstance.MainWindow.InOperation)
                {
                    SDApplication.SoleInstance.MainWindow.PuntThroughEventLoop(ex);
                }
                else
                {
                    // Normally we'd just throw the exception; in this case, we're the top of the stack and that would bring the application down.  So we report instead.
                    SDApplication.SoleInstance.FriendlyError("An internal error occurred while replaying the operation", ex, true);
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
                SDApplication.SoleInstance.ReplayWithCurrentData(operationName, freezeDriedParameters);
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
            if (IsSelectionReplayable)
            {
                e.Menu.Items.Add(new DevExpress.Utils.Menu.DXMenuItem("Replay operation", ReplayOperation));
            }
            if (IsImageSelected)
            {
                e.Menu.Items.Add(new DevExpress.Utils.Menu.DXMenuItem("Export graphic", ExportGraphic));
            }
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
                if (SDApplication.SoleInstance.MainWindow.InOperation)
                {
                    SDApplication.SoleInstance.MainWindow.PuntThroughEventLoop(ex);
                }
                else
                {
                    // Normally we'd just throw the exception; in this case, we're the top of the stack and that would bring the application down.  So we report instead.
                    SDApplication.SoleInstance.FriendlyError("An internal error occurred while exporting the graphic", ex, true);
                }
            }
#endif
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

        private void richEditControl1_ModifiedChanged(object sender, EventArgs e)
        {
            dirty = richEditControl1.Modified;
        }

        private void tbrSimple_Click(object sender, EventArgs e)
        {
            richEditControl1.ActiveViewType = RichEditViewType.Simple;
        }

        void richEditControl1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyValue == 67)
            {
                EditCopy();
                e.Handled = true;
            }
        }

        private void insertDateAndTimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richEditControl1.Document.InsertText(richEditControl1.Document.CaretPosition, DateTime.Now.ToString("dd MMMM yyyy @ hh:MM:ss"));
        }
    }
}

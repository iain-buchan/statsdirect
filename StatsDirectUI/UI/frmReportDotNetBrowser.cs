using System.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using System.IO;
using StatsDirect.Templates;
using System.Drawing.Imaging;
using StatsDirect.Utilities;
using System.Reflection;
using StatsDirect.TemplateProcessing;
using DotNetBrowser;
using DotNetBrowser.DOM;
using System.Web;
using System.Text;
using DevExpress.XtraBars.Docking;

namespace StatsDirect.UI
{
    public partial class frmReportDotNetBrowser : StatsDirectForm, IReport
    {
        const string HTML_REPORT_START = @"<html><head><style>body,h1,h2,th,td { font-family: Calibri,Arial; font-size: 10pt; } .ci { color: blue; } .grandtotal { color: #000080; } .model { color: #000080; } .pval { color: #008000; } .score { color: #008080; } .subtotal { color: #800000; } .warn {color: red; }</style></head><body>";
        const string HTML_REPORT_END = @"</body></html>";

        public frmReportDotNetBrowser()
        {
            // Lightweight (non-GPU accelerated) browsers are recommended for MDI applications as otherwise there can be some odd z-order behaviours.
            // These, in turn, are quite heavily optimised with the following options.
            BrowserPreferences.SetChromiumSwitches(
              "--disable-gpu",
              "--disable-gpu-compositing",
              "--enable-begin-frame-scheduling",
              "--software-rendering-fps=60"
            );

            InitializeComponent();
            SdApplication.SoleInstance.EnsureBuiltInMenuItemsCanShowHelp(MenuStrip1);
            Browser browser = dotNetBrowserView.Browser;
            string html = HTML_REPORT_START
                + "<p></p>"
                + HTML_REPORT_END;
            browser.LoadHTML(html);
            browser.ExecuteJavaScript("document.body.contentEditable='true'");
        }

        private string currentFile;

        private void frmReport_FormClosing(object sender, FormClosingEventArgs e)
        {
            DoOrWarn(() =>
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
            }, "Couldn't close form");
        }

        private void frmReport_TextChanged(object sender, EventArgs e)
        {
            Dirty = true;
        }

        public override bool OpenFile(string filename, bool isTempFile, string nameToDisplay)
        {
            string strExt = System.IO.Path.GetExtension(filename) ?? string.Empty;
            strExt = strExt.ToLower(CultureInfo.InvariantCulture);
            if (".htm".Equals(strExt) || ".html".Equals(strExt)
                || ".mht".Equals(strExt) || ".mhtml".Equals(strExt))
                dotNetBrowserView.Browser.LoadURL(FilePathToFileUrl(filename));
            else
            {
                using (StreamReader txtReader = new StreamReader(filename))
                    dotNetBrowserView.Browser.LoadHTML(HttpUtility.HtmlEncode(txtReader.ReadToEnd()));
            }
            if (!isTempFile)
            {
                currentFile = filename;
                Path = filename;
            }
            return true;
        }

        private static void DoOrWarn(Action func, string explanation)
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

        private static void DoOrSwallow(Action func)
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
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.SELECT_ALL);
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
            throw new NotImplementedException();
            // dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.FONT_NAME, someFontName);
        }

        private void BoldToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleBold, "Unable to set bold font");
        }

        private void ToggleBold()
        {
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.TOGGLE_BOLD);
        }

        private void ItalicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleItalic, "Unable to set italic font");
        }

        private void ToggleItalic()
        {
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.TOGGLE_ITALIC);
        }

        private void UnderlineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(ToggleUnderline, "Unable to set underline font");
        }

        private void ToggleUnderline()
        {
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.TOGGLE_UNDERLINE);
        }

        private void mnuUndo_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditUndo, "Unable to undo");
        }

        private void mnuRedo_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditRedo, "Unable to redo");
        }

        private void FindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditFind, "Cannot start find");
        }

        private void EditFind()
        {
            throw new NotImplementedException();
            // dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.FIND_STRING, someString);
        }

        private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(Print, "Couldn't print");
        }

        private void mnuPageSetup_Click(object sender, EventArgs e)
        {
            // TODO: Write me
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

        private void tbrFind_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditFind, "Couldn't start find");
        }

        #region IReport Members

        void IReport.AppendRenderable(IRenderable renderable, int helpContextId, Operation operation, string redoInformation)
        {
            string safeRedoInformation = string.IsNullOrWhiteSpace(redoInformation) ? string.Empty : redoInformation.Replace(@"\", "&#92;");
            string html = new HtmlRenderer(SdApplication.SoleInstance).Render(renderable);
            DOMDocument document = dotNetBrowserView.Browser.GetDocument();
            DOMElement body = document.GetElementByTagName("body");
            DOMElement wrappedRenderable = document.CreateElement("span");
            wrappedRenderable.Attributes.Add("class", "statsDirectResults");
            wrappedRenderable.Attributes.Add("operation", operation.Name);
            wrappedRenderable.Attributes.Add("helpContextId", helpContextId.ToString());
            wrappedRenderable.Attributes.Add("redoInformation", safeRedoInformation);
            wrappedRenderable.SetInnerHTML(html);
            body.AppendChild(wrappedRenderable);
        }

        #endregion


        private string GetFreezeDriedData()
        {
            // TODO: Do we need to htmlDecode this?
            return GetStatsDirectResultAttribute("redoInformation");
        }

        private string GetStatsDirectResultAttribute(string attributeName)
        {
            throw new NotImplementedException();
            /*
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
            */
        }

        internal override void ShowHelp()
        {
            string helpString = GetStatsDirectResultAttribute("helpContextId");
            if (int.TryParse(helpString, out int helpId))
            {
                SdApplication.SoleInstance.ShowHelp(SdApplication.SoleInstance.DialogOwner, helpId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                SdApplication.SoleInstance.ShowHelp(SdApplication.SoleInstance.DialogOwner);
            }
        }

        private void frmReport_Shown(object sender, EventArgs e)
        {
            DoOrSwallow(() => { MergeToolStrip(); dotNetBrowserView.Focus(); });
        }

        private void frmReport_Activated(object sender, EventArgs e)
        {
            DoOrSwallow(() =>
            {
                MergeToolStrip();
                if (null != Tag)
                    SdApplication.SoleInstance.NoteFormActivated((WindowInformation)Tag);
                dotNetBrowserView.Visible = true;
                dotNetBrowserView.Focus();
            });
        }

        public override IList<Pane> AvailablePanes => new List<Pane> { ((IForm)this).SelectedPane };

        public override Pane SelectedPane => new Pane(Text, WindowInformation, 0);

        public override bool SelectPane(Pane pane)
        {
            // There's only ever one, do nothing
            return true;
        }

        internal override bool SaveContents()
        {
            if (null == currentFile)
                return SaveAsContents();
            SaveFile(currentFile);
            Text = "Editor: " + currentFile;
            Dirty = false;
            return true;
        }

        internal override bool SaveAsContents()
        {
            SaveFileDialog1.Title = "Save report";
            SaveFileDialog1.DefaultExt = "html";
            SaveFileDialog1.Filter = "HTML files (*.htm, *.html)|*.htm;*.html|MHTML files (*.mht, *.mhtml)|*.mht;*.mhtml|All files (*.*)|*.*";
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
            DialogResult res = SaveFileDialog1.ShowDialog(SdApplication.SoleInstance.DialogOwner);
            if (res != DialogResult.OK)
                return false;
            if (string.IsNullOrEmpty(SaveFileDialog1.FileName))
                return false;
            currentFile = SaveFileDialog1.FileName;
            SaveFile(currentFile);
            SdApplication.SoleInstance.NoteRecentFile(currentFile, true);
            Text = currentFile;
            ((WindowInformation)Tag).Path = currentFile;
            Dirty = false;
            return true;
        }

        private void SaveFile(string path)
        {
            string strExt = System.IO.Path.GetExtension(path) ?? string.Empty;
            strExt = strExt.ToUpper(CultureInfo.InvariantCulture);
            string filesDirectory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + "_files");
            if (".MHT".Equals(strExt) || ".MHTML".Equals(strExt))
                dotNetBrowserView.Browser.SaveWebPage(path, filesDirectory, SavePageType.MHTML);
            else
                dotNetBrowserView.Browser.SaveWebPage(path, filesDirectory, SavePageType.COMPLETE_HTML);
        }

        internal override void Print()
        {
            dotNetBrowserView.Browser.Print();
        }

        private void frmReport_Deactivate(object sender, EventArgs e)
        {
            DoOrSwallow(UnmergeToolStrip);
        }

        private void MergeToolStrip()
        {
            IToolStripHost host = (IToolStripHost)ParentForm;
            host?.AppendToolStrip(localToolStrip);
        }

        private void UnmergeToolStrip()
        {
            IToolStripHost host = (IToolStripHost)ParentForm;
            host?.RemoveToolStrip(localToolStrip);
        }

        internal override void EditCut()
        {
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.CUT);
        }

        internal override void EditCopy()
        {
            if (IsImageSelected)
            {
                Image img = GetSelectedImage(out byte[] bytes);
                if (null != bytes)
                {
                    Metafile mf = (Metafile)img;
                    ClipboardMetafileHelper.PutEnhMetafileOnClipboard(Handle, mf);
                }
            }
            else
                dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.COPY);
        }

        internal override void EditPaste()
        {
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.PASTE);
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
            Image img = GetSelectedImage(out byte[] bytes);
            if (null != bytes)
            {
                using (frmExportGraphic f = new frmExportGraphic(img, bytes))
                {
                    f.ShowDialog(SdApplication.SoleInstance.DialogOwner);
                }
            }
            else
                SdApplication.SoleInstance.MsgboxX("No chart is selected. Please select a chart to export.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Export graphic", true);
        }

        private Image GetSelectedImage(out byte[] rawBytes)
        {
            throw new NotImplementedException();
            /*
            rawBytes = null;

            // TODO: Better approach
            string rtf = richEditControl1.Document.GetRtfText(richEditControl1.Document.Selection);
            if (!rtf.Contains(@"{\pict"))
                return null;

            string rtfFromPict = rtf.Substring(rtf.IndexOf(@"{\pict", StringComparison.Ordinal) + 6);
            if (!rtfFromPict.Contains("}"))
                return null;

            string trimmedRtf = rtfFromPict.Substring(0, rtfFromPict.IndexOf("}", StringComparison.Ordinal));
            return 0 == trimmedRtf.Length ? null : RtfImageConverter.ParseRtfToImage(trimmedRtf, out rawBytes);
            */
        }

        private void EditToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            exportGraphicToolStripMenuItem.Enabled = IsImageSelected;
        }

        private bool IsImageSelected
        {
            get
            {
                throw new NotImplementedException();
                dotNetBrowserView.Browser.Sel
                /*
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
                */
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
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.UNDO);
        }

        private void EditRedo()
        {
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.REDO);
        }

        private void findContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditFind, "Couldn't start find");
        }

        private void deleteContextMenuItem_Click(object sender, EventArgs e)
        {
            DoOrWarn(EditDelete, "Couldn't delete");
        }

        private void EditDelete()
        {
            dotNetBrowserView.Browser.ExecuteCommand(EditorCommand.DELETE);
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
                if (SdApplication.SoleInstance.InOperation)
                {
                    SdApplication.SoleInstance.PuntThroughEventLoop(ex);
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

        private bool IsSelectionReplayable => null != GetFreezeDriedData();

        private void richEditControl1_PopupMenuShowing(object sender, PopupMenuShowingEventArgs e)
        {
            DoOrSwallow(() =>
                {
                    foreach (DevExpress.Utils.Menu.DXMenuItem candidate in e.Menu.Items)
                        if ("Copy".Equals(candidate.Caption))
                            ClearEventAndSet(candidate, "Click", new EventHandler(ContextMenuCopy));
                    if (IsSelectionReplayable)
                        e.Menu.Items.Add(new DevExpress.Utils.Menu.DXMenuItem("Replay Operation", ReplayOperation));
                    if (IsImageSelected)
                        e.Menu.Items.Add(new DevExpress.Utils.Menu.DXMenuItem("Export Graphic", ExportGraphic));
                });
        }

        private void ContextMenuCopy(object sender, EventArgs e)
        {
            DoOrSwallow(EditCopy);
        }

        private static void ClearEventAndSet(object item, string eventName, Delegate handler)
        {
            FieldInfo fieldInfo = GetEventField(item.GetType(), eventName);
            if (null == fieldInfo)
                return;
            fieldInfo.SetValue(item, handler);
        }

        private static FieldInfo GetEventField(Type type, string eventName)
        {
            FieldInfo fieldInfo = null;
            while (type != null)
            {
                /* Find events defined as field */
                fieldInfo = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo != null && (fieldInfo.FieldType == typeof(MulticastDelegate) || fieldInfo.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
                    break;

                /* Find events defined as property { add; remove; } */
                fieldInfo = type.GetField("EVENT_" + eventName.ToUpper(), BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo != null)
                    break;
                type = type.BaseType;
            }
            return fieldInfo;
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
                if (SdApplication.SoleInstance.InOperation)
                {
                    SdApplication.SoleInstance.PuntThroughEventLoop(ex);
                }
                else
                {
                    // Normally we'd just throw the exception; in this case, we're the top of the stack and that would bring the application down.  So we report instead.
                    SdApplication.SoleInstance.FriendlyError("An internal error occurred while exporting the graphic", ex, true);
                }
            }
#endif
        }

        private void tbrZoomOut_Click(object sender, EventArgs e)
        {
            DoOrWarn(ZoomOut, "Couldn't set zoom");
        }

        private void ZoomOut()
        {
            if (dotNetBrowserView.ZoomLevel > -9)
                --dotNetBrowserView.ZoomLevel;
        }

        private void tbrZoomIn_Click(object sender, EventArgs e)
        {
            DoOrWarn(ZoomIn, "Couldn't zoom in");
        }

        private void ZoomIn()
        {
            if (dotNetBrowserView.ZoomLevel < 9)
                dotNetBrowserView.ZoomLevel++;
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

        private void Browser_DocumentLoadedInMainFrameEvent(object sender, DotNetBrowser.Events.LoadEventArgs e)
        {
            dotNetBrowserView.Browser.ExecuteJavaScript("document.body.contentEditable='true'");
        }

        // TODO: Add FormClosing handler that disposes BrowserView, then Browser

        // TODO: Add something that checks for Dirty

        /// <summary>
        /// Because System.Uri doesn't do the right thing with file paths that have % in them (and they're allowed in Windows)...
        /// </summary>
        /// <remarks>From https://stackoverflow.com/questions/1546419/convert-file-path-to-a-file-uri</remarks>
        public static string FilePathToFileUrl(string filePath)
        {
            StringBuilder uri = new StringBuilder();
            foreach (char v in filePath)
            {
                if ((v >= 'a' && v <= 'z') || (v >= 'A' && v <= 'Z') || (v >= '0' && v <= '9') ||
                  v == '+' || v == '/' || v == ':' || v == '.' || v == '-' || v == '_' || v == '~' ||
                  v > '\xFF')
                {
                    uri.Append(v);
                }
                else if (v == System.IO.Path.DirectorySeparatorChar || v == System.IO.Path.AltDirectorySeparatorChar)
                {
                    uri.Append('/');
                }
                else
                {
                    uri.Append(String.Format("%{0:X2}", (int)v));
                }
            }
            if (uri.Length >= 2 && uri[0] == '/' && uri[1] == '/') // UNC path
                uri.Insert(0, "file:");
            else
                uri.Insert(0, "file:///");
            return uri.ToString();
        }
    }
}

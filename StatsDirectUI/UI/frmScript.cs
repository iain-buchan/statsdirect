using StatsDirect.Configuration;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmScript : StatsDirectForm, IScriptWindow, IFindTarget
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
                frmScript.outputPane.AppendText(Message + "\n");
                Application.DoEvents(); // Force a display update
            }
        }

        public frmScript()
        {
            InitializeComponent();
            cboLanguage.SelectedIndexChanged += (_, _) => RefreshScriptPresentation();
            editTarget = rtbDoc;
            rtbDoc.Enter += (_, _) => editTarget = rtbDoc;
            outputPane.Enter += (_, _) => editTarget = outputPane;
            outputPane.Activated += () => editTarget = outputPane;
            outputPane.LinkClicked += OpenLink;
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
            // the window's own menu strip goes: the main window's File and Edit menus and the toolbar serve it, and the keyboard keeps its
            // Edit functions (Ctrl+Z undo, Ctrl+Y redo, Ctrl+A select all, Ctrl+F find, Ctrl+H find and replace)
            MenuStrip1.Visible = false;
            rtbDoc.KeyDown += ScriptKeyDown;
            rtbDoc.HideSelection = false;   // what Find selects stays highlighted while the Find dialog has the focus
            BuildScriptContextMenu();
            PrintDocument1.BeginPrint += (_, _) => printRemaining = rtbDoc.Text;
            PrintDocument1.PrintPage += PrintScriptPage;
            BuildFindBar();
            BuildPreview();
            // The same document command order as worksheets and the web report. File actions remain in File.
            var undo = DocumentToolbar.Button("Undo", "undo", mnuUndo_Click, "Ctrl+Z");
            var redo = DocumentToolbar.Button("Redo", "redo", mnuRedo_Click, "Ctrl+Y");
            var cut = DocumentToolbar.Button("Cut", "cut", CutToolStripMenuItem_Click, "Ctrl+X");
            var copy = DocumentToolbar.Button("Copy", "copy", CopyToolStripMenuItem_Click, "Ctrl+C");
            var paste = DocumentToolbar.Button("Paste", "paste", PasteToolStripMenuItem_Click, "Ctrl+V");
            // Keep the existing language selector and Run handler, but remove the second command row.
            cboLanguage.AccessibleName = "Script language";
            var language = new ToolStripControlHost(cboLanguage) { Name = "scriptLanguage", AccessibleName = "Script language", Overflow = ToolStripItemOverflow.Always };
            pnlActions.Visible = false;
            ToolStrip1.Items.Clear();
            ToolStrip1.Items.AddRange(new ToolStripItem[]
            {
                DocumentToolbar.Button("Save", "save", tbrSave_Click, "Ctrl+S"),
                new ToolStripSeparator(),
                undo, redo, new ToolStripSeparator(), cut, copy,
                paste,
                DocumentToolbar.Button("Find", "find", tbrFind_Click, "Ctrl+F"),
                new ToolStripSeparator(),
                DocumentToolbar.Button("Run", "run", cmdRun_Click, "Alt+R", labelled: true),
                DocumentToolbar.Button("Help", "help", (_, _) => ShowHelp(), "F1"),
                language,
            });
            DocumentToolbar.Configure(ToolStrip1, () => {
                undo.Enabled = CanUndo; redo.Enabled = CanRedo;
                copy.Enabled = HasSelection;
                cut.Enabled = copy.Enabled && Editable;
                paste.Enabled = Editable;
            });
        }

        private string currentFile;
        private Control editTarget; // the script editor or the output pane, retained when the toolbar or its More dropdown takes keyboard focus
        /// <summary>The editor when it is the edit target; the output pane, a page, is read and copied, not edited.</summary>
        private RichTextBox Editor => editTarget as RichTextBox;
        private bool Editable => Editor != null && !Editor.ReadOnly;
        private bool HasSelection => Editor != null ? Editor.SelectionLength > 0 : outputPane.HasSelection;
        private string printRemaining;   // what is left to print of the script, page by page

        /// <summary>Prints the script as the editor shows it, as many pages as its lines wrapped to the page take.</summary>
        private void PrintScriptPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
        {
            using System.Drawing.StringFormat format = new(System.Drawing.StringFormat.GenericTypographic) { Trimming = System.Drawing.StringTrimming.Word };
            string text = printRemaining ?? "";
            e.Graphics.MeasureString(text, rtbDoc.Font, e.MarginBounds.Size, format, out int characters, out _);
            e.Graphics.DrawString(text.Substring(0, characters), rtbDoc.Font, System.Drawing.Brushes.Black, e.MarginBounds, format);
            printRemaining = text.Substring(characters);
            e.HasMorePages = printRemaining.Trim().Length > 0 && characters > 0;
        }

        private void ScriptKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.Alt) return;
            switch (e.KeyCode)
            {
                case Keys.Y: rtbDoc.Redo(); break;
                case Keys.A: rtbDoc.SelectAll(); break;
                case Keys.F: FindToolStripMenuItem_Click(sender, e); break;
                case Keys.H: FindAndReplaceToolStripMenuItem_Click(sender, e); break;
                default: return;
            }
            e.SuppressKeyPress = true;
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            // Preserve the old lower Run button's access key after moving it into the toolbar.
            if (keyData == (Keys.Alt | Keys.R)) { cmdRun_Click(this, EventArgs.Empty); return true; }
            return base.ProcessCmdKey(ref message, keyData);
        }
        private string helpTopic;   // the help topic of the result the script continues, when it does

        private void frmScript_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsInteractiveR) { ClosingInteractiveR(e); return; }
            DoOrWarn(() =>
            {
                if (!AllowClose())
                {
                    e.Cancel = true;
                    return;
                }
                SdApplication.SoleInstance.NoteFormClosing(this, e);
                Visible = false;
                MdiParent = null;
            }, "Couldn't close form");

        }

        private void frmScript_TextChanged(object sender, EventArgs e)
        {
            Dirty = true;
            if (InteractiveConsole != null && !mirroringRSource) InteractiveConsole.Script = rtbDoc.Text;
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
            currentFile = string.Empty;
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
                strExt = strExt.ToLower(CultureInfo.InvariantCulture);
            if (".rtf".Equals(strExt))
                rtbDoc.LoadFile(filename, RichTextBoxStreamType.RichText);
            else
            {
                using (StreamReader txtReader = new(filename))
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
            Dirty = isTempFile;
            // Set the selected language according to the loaded file
            if (".vb".Equals(strExt))
                cboLanguage.SelectedIndex = 2;
            else if(".cs".Equals(strExt))
                cboLanguage.SelectedIndex = 0;
            else if (".r".Equals(strExt))
            {
                cboLanguage.SelectedIndex = 1;
                EnsureRTools();
                _ = GuardInteractiveAsync(() => InteractiveConsole.SetModeAsync("script"));
            }
            return true;
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsInteractiveR) { _ = GuardInteractiveAsync(async () => await SaveInteractiveAsync(false)); return; }
            if (null == currentFile)
            {
                SaveAsToolStripMenuItem_Click(this, e);
                return;
            }
            string strExt = System.IO.Path.GetExtension(currentFile);
            if (null != strExt)
                strExt = strExt.ToUpper(CultureInfo.InvariantCulture);
            if (".RTF".Equals(strExt))
                rtbDoc.SaveFile(currentFile);
            else
            {
                // to save as plain text
                using (StreamWriter txtWriter = new(currentFile))
                {
                    txtWriter.Write(rtbDoc.Text);
                }
                rtbDoc.SelectionStart = 0;
                rtbDoc.SelectionLength = 0;
                rtbDoc.Modified = false;
            }
            Path = currentFile;
            Dirty = false;
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsInteractiveR) { _ = GuardInteractiveAsync(async () => await SaveInteractiveAsync(true)); return; }
            SaveFileDialog1.Title = "Save File";
            bool r = cboLanguage.SelectedIndex == 1;   // an R script is saved as plain text with the extension R
            SaveFileDialog1.DefaultExt = r ? "R" : "rtf";
            SaveFileDialog1.Filter = r ? "R scripts|*.R|Script Files|*.cs;*.vb|All Files|*.*" : "Script Files|*.cs;*.vb|All Files|*.*";
            SaveFileDialog1.FilterIndex = 1;
            if (SaveFileDialog1.ShowDialog(SdApplication.SoleInstance.DialogOwner) != DialogResult.OK) return;
            if (SaveFileDialog1.FileName.Length == 0)
                return;
            string strExt = System.IO.Path.GetExtension(SaveFileDialog1.FileName);
            if (null != strExt)
                strExt = strExt.ToUpper(CultureInfo.InvariantCulture);
            if (".RTF".Equals(strExt))
            {
                rtbDoc.SaveFile(SaveFileDialog1.FileName, RichTextBoxStreamType.RichText);
            }
            else
            {
                using (StreamWriter txtWriter = new(SaveFileDialog1.FileName))
                {
                    txtWriter.Write(rtbDoc.Text);
                }
                rtbDoc.SelectionStart = 0;
                rtbDoc.SelectionLength = 0;
            }
            currentFile = SaveFileDialog1.FileName;
            rtbDoc.Modified = false;
            Path = currentFile;
            Dirty = false;
            SdApplication.SoleInstance.NoteRecentFile(currentFile, true);
        }

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (IsInteractiveR) { _ = InteractiveConsole.ExecuteAsync("window.StatsDirectRHost.selectAll()"); return; }
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
                EditCopy();
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
                EditCut();
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
                EditPaste();
            }
            catch (Exception)
            {
                SdApplication.SoleInstance.MsgboxX("Unable to copy clipboard content to document.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PageColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog1.Color = rtbDoc.BackColor;
            if (ColorDialog1.ShowDialog(SdApplication.SoleInstance.DialogOwner) == DialogResult.OK)
            {
                rtbDoc.BackColor = ColorDialog1.Color;
            }
        }

        private void mnuUndo_Click(object sender, EventArgs e)
        {
            EditUndo();
        }

        private void mnuRedo_Click(object sender, EventArgs e)
        {
            EditRedo();
        }

        private void FindToolStripMenuItem_Click(object sender, EventArgs e) => ShowFindBar(false);

        private void FindAndReplaceToolStripMenuItem_Click(object sender, EventArgs e) => ShowFindBar(true);

        #region Find bar
        // Find and replace as a bar at the top of the window, as the report's, in place of floating dialogs: hidden until asked
        private FindBar findBar;
        internal bool FindBarShown => findBar.Shown;

        private void BuildFindBar()
        {
            findBar = new FindBar(this);
            Controls.Add(findBar);
            Controls.SetChildIndex(findBar, Controls.GetChildIndex(ToolStrip1));   // laid out after the toolbar and before the script, so docked below the toolbar with the script below it
        }

        private void ShowFindBar(bool replace) => findBar.Open(replace);

        internal override void EditFind() => ShowFindBar(false);

        string IFindTarget.SelectedText => IsInteractiveR ? InteractiveConsole.SelectedText : rtbDoc.SelectionLength > 0 && !rtbDoc.SelectedText.Contains('\n') ? rtbDoc.SelectedText : null;

        void IFindTarget.FocusContent() { if (IsInteractiveR) InteractiveConsole.FocusInput(); else rtbDoc.Focus(); }

        async Task<bool> IFindTarget.FindNextAsync(string term, bool matchCase) => IsInteractiveR ? System.Text.Json.JsonSerializer.Deserialize<bool>(await InteractiveConsole.ExecuteAsync("window.StatsDirectRHost.findNext(" + System.Text.Json.JsonSerializer.Serialize(term) + "," + (matchCase ? "true" : "false") + ")")) : FindNext(term, matchCase);

        async Task<bool> IFindTarget.ReplaceSelectionAsync(string term, string replacement, bool matchCase)
        {
            if (IsInteractiveR) return System.Text.Json.JsonSerializer.Deserialize<bool>(await InteractiveConsole.ExecuteAsync("window.StatsDirectRHost.replaceSelection(" + System.Text.Json.JsonSerializer.Serialize(term) + "," + System.Text.Json.JsonSerializer.Serialize(replacement) + "," + (matchCase ? "true" : "false") + ")"));
            StringComparison comparison = matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            if (term.Length == 0 || !string.Equals(rtbDoc.SelectedText, term, comparison))
                return false;
            rtbDoc.SelectedText = replacement;
            return true;
        }

        async Task<int> IFindTarget.ReplaceAllAsync(string term, string replacement, bool matchCase) => IsInteractiveR ? System.Text.Json.JsonSerializer.Deserialize<int>(await InteractiveConsole.ExecuteAsync("window.StatsDirectRHost.replaceAll(" + System.Text.Json.JsonSerializer.Serialize(term) + "," + System.Text.Json.JsonSerializer.Serialize(replacement) + "," + (matchCase ? "true" : "false") + ")")) : ReplaceAll(term, replacement, matchCase);

        /// <summary>Selects the next match after the selection, going round to the start when there is none after it; false when there is none at all.</summary>
        internal bool FindNext(string term, bool matchCase)
        {
            if (string.IsNullOrEmpty(term))
                return false;
            StringComparison comparison = matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = rtbDoc.Text;
            int from = rtbDoc.SelectionStart + rtbDoc.SelectionLength;
            int at = from < text.Length ? text.IndexOf(term, from, comparison) : -1;
            if (at < 0)
                at = text.IndexOf(term, 0, comparison);
            if (at < 0)
                return false;
            rtbDoc.Select(at, term.Length);
            rtbDoc.ScrollToCaret();
            return true;
        }

        /// <summary>Replaces every match in the script; the number replaced.</summary>
        internal int ReplaceAll(string term, string replacement, bool matchCase)
        {
            if (string.IsNullOrEmpty(term))
                return 0;
            StringComparison comparison = matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            string text = rtbDoc.Text;
            System.Text.StringBuilder s = new();
            int count = 0, at = 0;
            while (at <= text.Length)
            {
                int next = at < text.Length ? text.IndexOf(term, at, comparison) : -1;
                if (next < 0) { s.Append(text, at, text.Length - at); break; }
                s.Append(text, at, next - at).Append(replacement);
                at = next + term.Length;
                count++;
            }
            if (count > 0)
                rtbDoc.Text = s.ToString();
            return count;
        }
        #endregion

        private void PreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                PrintPreviewDialog1.Document = PrintDocument1;
                PrintPreviewDialog1.ShowDialog(SdApplication.SoleInstance.DialogOwner);
            }
            catch
            {
                // Do nothing - errors in Print Preview and the like
            }
        }

        private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrintDialog1.Document = PrintDocument1;
            if (PrintDialog1.ShowDialog(SdApplication.SoleInstance.DialogOwner) == DialogResult.OK)
                PrintDocument1.Print();
        }

        private void mnuPageSetup_Click(object sender, EventArgs e)
        {
            PageSetupDialog1.Document = PrintDocument1;
            PageSetupDialog1.ShowDialog(SdApplication.SoleInstance.DialogOwner);
        }

        #endregion

        #region Toolbar Methods
        private void tbrSave_Click(object sender, EventArgs e)
        {
            SaveToolStripMenuItem_Click(this, e);
        }

        private void tbrFind_Click(object sender, EventArgs e) => ShowFindBar(false);

        #endregion


        #region IScriptWindow Members

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string RtfText
        {
            get => rtbDoc.Rtf;
            set => rtbDoc.Rtf = value;
        }

        /// <summary>
        /// Shows an R script, the one written for a result the user continues in R: the language set to R, so that Run runs it with the
        /// installed R and Save As offers an R file.
        /// </summary>
        internal void ShowR(string script, string title, string helpTopic = null, bool runImmediately = true)
        {
            this.helpTopic = helpTopic;
            rtbDoc.Text = script;
            rtbDoc.SelectionStart = 0;
            rtbDoc.SelectionLength = 0;
            rtbDoc.Modified = false;   // written from the report, not an unsaved document: closing the program must not wait on it
            Dirty = false;             // (setting the text marked the form changed)
            cboLanguage.SelectedIndex = 1;
            // the window keeps its number, so that the next script is Script 2, and names the analysis after it; the tab is named with it
            string name = Text + ": " + title;
            if (Tag is WindowInformation info && info.IsNew) info.FriendlyName = name; else Text = name;
            EnsureRTools();
            if (runImmediately) RunR();
            else _ = GuardInteractiveAsync(() => InteractiveConsole.SetModeAsync("script"));
        }

        private void EnsureRTools() => EnsureInteractiveConsole();

        internal string LearningScript => IsInteractiveR ? InteractiveConsole.Script : rtbDoc.Text;
        internal string LearningLanguage => ScriptLanguageForScript();
        internal string LearningOutput => IsInteractiveR ? InteractiveConsole.Transcript : outputPane.Text;
        internal bool LearningRunning => IsInteractiveR && InteractiveConsole.NativeState is "starting" or "busy" or "input" or "continuation" or "interrupting";

        /// <summary>The name of the file a script is saved as for R: the window's title, with what a file name cannot hold replaced.</summary>
        internal static string ScriptFileName(string title)
        {
            string name = string.Concat((title ?? "").Select(ch => System.IO.Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
            return (name.Length == 0 ? "script" : name) + ".R";
        }

        /// <summary>Saves the script as an R file in the R folder and opens it in RStudio when that is installed, else in whatever opens R
        /// files here: a user at home in R goes on from there, with the data in the script.</summary>
        private async void OpenInR()
        {
            string path = System.IO.Path.Combine(SDConfiguration.MyStatsDirectRFolder, ScriptFileName(Text));
            try
            {
                if (IsInteractiveR) await InteractiveConsole.GetScriptAsync();
                Directory.CreateDirectory(SDConfiguration.MyStatsDirectRFolder);
                File.WriteAllText(path, rtbDoc.Text.Replace("\r", "").Replace("\n", "\r\n"), new System.Text.UTF8Encoding(false));
                string rstudio = RController.FindRStudio();
                if (rstudio != null)
                {
                    Process.Start(new ProcessStartInfo(rstudio, "\"" + path + "\"") { UseShellExecute = false, WorkingDirectory = SDConfiguration.MyStatsDirectRFolder });
                    AppendScriptNotice("(the script is saved as " + path + " and opened in RStudio)\n");
                    return;
                }
                // R's own window: the script run in its console as a session shows it, and open in its editor
                RVersion version = RController.PreferredRVersion();
                while (version == null)
                {
                    if (!RController.UserMightHaveInstalledR())
                        return;
                    version = RController.PreferredRVersion();
                }
                RController.OpenInRGui(version, path);
                AppendScriptNotice("(the script is saved as " + path + ", run in R's own window and opened in its editor)\n");
            }
            catch (Exception ex)
            {
                AppendScriptNotice("Could not open the script in R: " + ex.Message + "\n(the script is saved as " + path + "; open it from there)\n");
            }
        }

        private static void OpenRFolder()
        {
            string folder = SDConfiguration.MyStatsDirectRFolder;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }

        /// <summary>A link in the output: a file the script wrote, opened with its own program, or a web address.</summary>
        private void OpenLink(string link)
        {
            try
            {
                string target = link.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(link).LocalPath : link;
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex) { outputPane.AppendText("Could not open " + link + ": " + ex.Message + "\n"); }
        }

        // These names are reserved for the controller's automatic charts and temporary scripts. Match the complete name so that
        // an explicitly named output such as Rplot-results.svg or script-results.r is still discovered normally.
        private static readonly System.Text.RegularExpressions.Regex AutomaticChartName = new(@"^Rplot-[0-9a-f]{32}-[0-9]+-[0-9]+\.(?:pdf|svg)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        private static readonly System.Text.RegularExpressions.Regex TemporaryRScriptName = new(@"^(?:continue|script)-[0-9a-f]{32}\.r$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>The files a run wrote to the R folder, charts among them, by name. Automatic charts belong only to their own run;
        /// explicitly named script outputs retain the working folder's timestamp-based discovery.</summary>
        private static List<FileInfo> FilesWritten(DateTime started, Guid runId)
        {
            string folder = SDConfiguration.MyStatsDirectRFolder;
            if (!Directory.Exists(folder))
                return new List<FileInfo>();
            string chartPrefix = "Rplot-" + runId.ToString("N") + "-";
            return new DirectoryInfo(folder).GetFiles()
                .Where(f => AutomaticChartName.IsMatch(f.Name)
                    ? f.Name.StartsWith(chartPrefix, StringComparison.OrdinalIgnoreCase)
                    : f.LastWriteTime >= started.AddSeconds(-1) && !TemporaryRScriptName.IsMatch(f.Name) && !f.Name.Equals("continue.r", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The files written as links the user can open; nothing when there are none.</summary>
        private static string FilesWrittenText(List<FileInfo> files)
        {
            if (files.Count == 0)
                return string.Empty;
            System.Text.StringBuilder s = new("\nFiles written, to open with a click:\n");
            foreach (FileInfo f in files)
                s.Append(new Uri(f.FullName).AbsoluteUri).Append('\n');
            return s.ToString();
        }

        /// <summary>The files a run writes that the output pane draws: SVG, as the scripts of the program draw, and the raster formats.</summary>
        private static readonly HashSet<string> ChartExtensions = new(StringComparer.OrdinalIgnoreCase) { ".svg", ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };

        /// <summary>Appends R's transcript to the output pane as an R session shows it: the commands after their prompts in a colour of their
        /// own, what R printed in the pane's colour.</summary>
        internal void AppendTranscript(string transcript) => outputPane.AppendTranscript(transcript);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TextInRtfBox
        {
            get => rtbDoc.Text;
            set => rtbDoc.Text = value;
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
            DoOrWarn(() =>
            {
                if (cboLanguage.SelectedIndex == 1)
                {
                    // R: the installed R runs the script and what it prints is shown below, as for a result continued in R; files it writes go to the R folder
                    RunR();
                    return;
                }
                outputPane.AppendText("\n======== Start of run ========\n");
                try
                {
                    string script = rtbDoc.Text;
                    IScriptEngine engine = new ScriptEngine();
                    string Language = ScriptLanguageForScript();
                    object output = engine.Run(Language, script, ScriptType.Method, SdApplication.SoleInstance, null, null, null);
                    if (null != output)
                    {
                        if (output is ParameterBag bag)
                        {
                            System.Text.StringBuilder dump = new();
                            DumpParameterBag(dump, bag, 0);
                            outputPane.AppendText(dump.ToString());
                        }
                        else
                        {
                            outputPane.AppendText(output.ToString());
                            outputPane.AppendText("\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    outputPane.AppendText("Error: " + ex.Message + "\n");
                }
                outputPane.AppendText("======== End of run ========\n");
            }, "Couldn't run script");
        }

        private static void DumpParameterBag(System.Text.StringBuilder rtb, ParameterBag output, int depth)
        {
            foreach (KeyValuePair<string, FilledParameter> pair in output.Pairs)
            {
                rtb.Append(new string(' ', depth * 3));
                rtb.Append(pair.Key);
                rtb.Append(" <- ");
                if (null == pair.Value || !pair.Value.HasData)
                {
                    rtb.Append("null");
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
                    rtb.Append(pair.Value.AsObject.ToString());
                }
                rtb.Append("\n");
            }
        }

        private void frmScript_Load(object sender, EventArgs e)
        {
            cboLanguage.SelectedIndex = 0;
        }

        private void frmScript_Shown(object sender, EventArgs e)
        {
            if (IsInteractiveR) InteractiveConsole.FocusInput(); else rtbDoc.Focus();
        }

        public override IList<Pane> AvailablePanes
        {
            get
            {
                List<Pane> panes = new() { ((IForm) this).SelectedPane};
                return panes;
            }
        }

        public override Pane SelectedPane => new(Text, WindowInformation, 0);

        public override bool SelectPane(Pane pane)
        {
            // There's only ever one, do nothing
            return true;
        }

        /// <summary>The help topic of this window: the topic of the result the script continues; the R script window's own topic for an
        /// empty window or a script of no result.</summary>
        internal string HelpTopic => rtbDoc.TextLength == 0 || string.IsNullOrEmpty(helpTopic) ? "1401" : helpTopic;

        internal override void ShowHelp() => SdApplication.SoleInstance.ShowHelp(this, HelpTopic);

        // the Edit commands act on the editor; the output pane, a page, only copies
        internal override bool CanUndo => IsInteractiveR ? InteractiveConsole.CanUndo : Editor != null && Editor.CanUndo;
        internal override bool CanRedo => IsInteractiveR ? InteractiveConsole.CanRedo : Editor != null && Editor.CanRedo;
        internal override void EditUndo() { if (IsInteractiveR) _ = InteractiveEditAsync("undo"); else if (CanUndo) Editor.Undo(); }
        internal override void EditRedo() { if (IsInteractiveR) _ = InteractiveEditAsync("redo"); else if (CanRedo) Editor.Redo(); }

        internal override void EditCut()
        {
            if (IsInteractiveR) _ = InteractiveEditAsync("cut"); else if (Editable) Editor.Cut();
        }

        internal override void EditCopy()
        {
            if (IsInteractiveR) _ = InteractiveEditAsync("copy");
            else if (Editor != null) Editor.Copy();
            else _ = outputPane.CopyAsync();
        }

        internal override void EditPaste()
        {
            if (IsInteractiveR) _ = InteractiveEditAsync("paste"); else if (Editable) Editor.Paste();
        }

        #region Print preview
        // the preview inside the window, as the report's print preview is, in place of a dialog of its own: it takes the script's place,
        // Print above it asks for the printer and sends the pages, and Close returns to the script
        private Panel previewPanel;
        private PrintPreviewControl previewControl;
        private bool previewShown;
        internal bool PreviewShown => previewShown;

        private void BuildPreview()
        {
            previewControl = new PrintPreviewControl { Dock = DockStyle.Fill, AutoZoom = true };
            Button Button(string text, EventHandler click) { Button b = new() { Text = text, AutoSize = true }; b.Click += click; return b; }
            FlowLayoutPanel row = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 2, 4, 2) };
            row.Controls.AddRange(new Control[] { Button("Print", (_, _) => PrintPages()), Button("Close", (_, _) => HidePreview()), new Label { Text = "Print preview", AutoSize = true, Margin = new Padding(12, 6, 3, 3), ForeColor = System.Drawing.SystemColors.GrayText } });
            previewPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
            previewPanel.Controls.Add(previewControl);
            previewPanel.Controls.Add(row);   // added after the preview, so laid out first: the row at the top, the pages below
            Controls.Add(previewPanel);
            Controls.SetChildIndex(previewPanel, 0);   // in front of the script, filling the same place, and shown only while the preview is wanted
        }

        internal override void Print()
        {
            PrintDocument1.DocumentName = Text;
            previewControl.Document = PrintDocument1;
            previewControl.InvalidatePreview();
            previewPanel.Visible = true;
            previewShown = true;
            previewPanel.BringToFront();
        }

        private void HidePreview()
        {
            previewPanel.Visible = false;
            previewShown = false;
            rtbDoc.Focus();
        }

        private void PrintPages()
        {
            // the plain print dialog for the printer: the extended one offered a preview of its own, which the printer's software answered
            // with a message that this program had none
            PrintDialog1.Document = PrintDocument1;
            PrintDialog1.UseEXDialog = false;
            if (PrintDialog1.ShowDialog(SdApplication.SoleInstance.DialogOwner) == DialogResult.OK)
                PrintDocument1.Print();
        }
        #endregion

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        internal override bool SaveContents()
        {
            if (IsInteractiveR) { _ = GuardInteractiveAsync(async () => await SaveInteractiveAsync(false)); return false; }
            SaveToolStripMenuItem_Click(this, EventArgs.Empty);
            return !Dirty;
        }

        internal override bool SaveAsContents()
        {
            if (IsInteractiveR) { _ = GuardInteractiveAsync(async () => await SaveInteractiveAsync(true)); return false; }
            SaveAsToolStripMenuItem_Click(this, EventArgs.Empty);
            return !Dirty;
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
    }
}

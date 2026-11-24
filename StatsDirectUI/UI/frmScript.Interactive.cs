using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI;

public partial class frmScript
{
    internal RConsoleView InteractiveConsole { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string WorksheetImportFolder { private get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool OfferROutputToReport { private get; set; }
    private bool mirroringRSource, savingInteractive, allowInteractiveClose, addingROutputToReport;
    internal Task<bool> PendingInteractiveClose { get; private set; }
    private bool IsInteractiveR => cboLanguage.SelectedIndex == 1 && InteractiveConsole != null;
    internal bool CanRunAISuggestion => IsInteractiveR;

    private void EnsureInteractiveConsole()
    {
        if (InteractiveConsole == null)
        {
            var view = InteractiveConsole = new RConsoleView { Dock = DockStyle.Fill, Script = rtbDoc.Text };
            view.ScriptChanged += () =>
            {
                if (mirroringRSource || IsDisposed) return;
                mirroringRSource = true;
                try { rtbDoc.Text = view.Script; rtbDoc.Modified = true; Dirty = true; }
                finally { mirroringRSource = false; }
                if (allowInteractiveClose) CancelInteractiveParentClose();
            };
            view.SaveRequested += () => _ = GuardInteractiveAsync(async () => await SaveInteractiveAsync(false));
            view.HelpRequested += ShowHelp;
            view.AIRequested += () => (MdiParent as frmMain)?.Learning.ShowScriptAI(this);
            view.OpenInRRequested += OpenInR;
            view.MoreRequested += ShowRMoreMenu;
            if (OfferROutputToReport) view.AddOutputToReportRequested += AddROutputToReportAsync;
            // Before the page is up nothing else can show a notice: a page that failed to load is reported here
            view.Notice += message => { if (!IsDisposed && !view.Ready.IsCompletedSuccessfully) MessageBox.Show(this, message, "R"); };
            view.CommandRequested += command =>
            {
                switch (command)
                {
                    case "save": _ = GuardInteractiveAsync(async () => await SaveInteractiveAsync(false)); break;
                    case "find": ShowFindBar(false); break;
                    case "replace": ShowFindBar(true); break;
                    case "print": Print(); break;
                    case "help": ShowHelp(); break;
                }
            };
            Controls.Add(view);
            Controls.SetChildIndex(view, Controls.GetChildIndex(splitContainer1));
            Disposed += (_, _) => view.Dispose();
        }
        InteractiveConsole.Script = rtbDoc.Text;
        RefreshScriptPresentation();
    }

    private void RefreshScriptPresentation()
    {
        bool interactive = cboLanguage.SelectedIndex == 1;
        if (interactive && InteractiveConsole == null) { EnsureInteractiveConsole(); return; }
        splitContainer1.Visible = !interactive;
        ToolStrip1.Visible = !interactive;
        if (InteractiveConsole != null) InteractiveConsole.Visible = interactive;
    }

    private void ShowRMoreMenu()
    {
        var menu = new ContextMenuStrip();
        void Item(string text, Action action) => menu.Items.Add(text, null, (_, _) => BeginInvoke(new Action(action)));
        Item("Output folder", OpenRFolder);
        Item("Set up R...", () => R.RController.UserMightHaveInstalledR());
        Item("Clear output", () => _ = GuardInteractiveAsync(() => InteractiveConsole.ClearOutputAsync()));
        Item("Reload R display", () => _ = GuardInteractiveAsync(() => InteractiveConsole.RecoverDisplayAsync()));
        menu.Items.Add(new ToolStripSeparator());
        Item("Find...", () => ShowFindBar(false));
        Item("Find and replace...", () => ShowFindBar(true));
        Item("Print script...", Print);
        var language = new ToolStripMenuItem("Script language");
        string[] names = { "C#", "R", "Visual Basic" };
        for (int i = 0; i < names.Length; i++)
        {
            int selected = i;
            var item = new ToolStripMenuItem(names[i]) { Checked = cboLanguage.SelectedIndex == i };
            item.Click += (_, _) => BeginInvoke(new Action(() => cboLanguage.SelectedIndex = selected));
            language.DropDownItems.Add(item);
        }
        menu.Items.Add(language);
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(InteractiveConsole, InteractiveConsole.PointToClient(Cursor.Position));
    }

    private void AppendScriptNotice(string message)
    {
        if (IsInteractiveR) InteractiveConsole.ReportNotice(message); else outputPane.AppendText(message);
    }

    private async Task GuardInteractiveAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (!IsDisposed && !Disposing) { InteractiveConsole?.ReportNotice(ex.Message); }
    }

    private void RunR() => _ = GuardInteractiveAsync(async () =>
    {
        EnsureInteractiveConsole();
        string source = await InteractiveConsole.GetScriptAsync();
        if (string.IsNullOrWhiteSpace(source)) await InteractiveConsole.StartAsync();
        else await InteractiveConsole.SubmitAsync(source, "script");
        await InteractiveConsole.SetModeAsync("console");
    });

    internal async Task RunAISuggestionAsync(string source)
    {
        if (IsDisposed || Disposing || !IsInteractiveR)
            throw new InvalidOperationException("The original R session is no longer open.");
        if (LearningRunning)
            throw new InvalidOperationException("R is busy or is waiting for an answer. Finish or stop that command first.");
        var console = InteractiveConsole;
        Activate();
        await console.SetModeAsync("console");
        if (IsDisposed || Disposing || !IsInteractiveR)
            throw new InvalidOperationException("The original R session is no longer open.");
        // Ordinary prompt submissions append to the editor; show this as a run without changing its text.
        await console.SubmitAsync(source, "script");
    }

    private async Task AddROutputToReportAsync()
    {
        if (addingROutputToReport || MdiParent is not frmMain main) return;
        addingROutputToReport = true;
        try
        {
            string output = await InteractiveConsole.ReportOutputAsync();
            if (IsDisposed || Disposing || MdiParent != main) return;
            var report = main.SelectedReportWindow as frmReportWebView
                ?? throw new InvalidOperationException("Open a report to receive the R output.");
            await report.AppendROutputAsync(output);
        }
        finally { addingROutputToReport = false; }
    }

    internal async Task<bool> SaveInteractiveAsync(bool choose)
    {
        if (savingInteractive) return false;
        savingInteractive = true;
        try
        {
            string source = await InteractiveConsole.GetScriptAsync();
            string destination = currentFile;
            bool replacing = false;
            if (choose || destination == null)
            {
                destination = ChooseRScriptFile(destination);
                if (destination == null) return false;
                // The dialog asked before replacing a file there, so the script's data file goes with it.
                replacing = File.Exists(destination);
            }
            var written = WriteRScript(destination, source, replacing);
            if (written.Notice != null) AppendScriptNotice(written.Notice);
            if (written.Cancelled) return false;
            currentFile = destination; Path = destination;
            knownDataFile = written.DataFile == null ? null : (written.DataFile, written.Hash);
            if (InteractiveConsole.Script == source) { rtbDoc.Modified = false; Dirty = false; }
            SdApplication.SoleInstance.NoteRecentFile(destination, true);
            return !Dirty;
        }
        finally { savingInteractive = false; }
    }

    // The file Save As saves the R script to, from the user, or null when they cancel.  The shell checks answer in the user's place.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<string, string> RScriptFileChooser { private get; set; }
    private string ChooseRScriptFile(string current)
    {
        if (RScriptFileChooser != null) return RScriptFileChooser(current);
        using var dialog = new SaveFileDialog
        {
            Title = "Save R script", Filter = "R scripts (*.R)|*.R|All files (*.*)|*.*",
            DefaultExt = "R", AddExtension = true, FileName = current ?? ScriptFileName(Text)
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    // Whether to replace a data file beside the script that does not hold this window's data and that this window did not write; the
    // shell checks answer in the user's place.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<string, bool> ReplaceDataConfirmer { private get; set; }
    private bool ConfirmReplaceData(string dataFile)
    {
        if (ReplaceDataConfirmer != null) return ReplaceDataConfirmer(dataFile);
        return MessageBox.Show(this,
            "The worksheet data file “" + System.IO.Path.GetFileName(dataFile) + "” beside this script does not hold this script's data. "
            + "Another script may have been saved under the same name, or another program may have changed the file.\n\n"
            + "Replace it with this script's own data?",
            "Save R script", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    // The window's own copy of its worksheet data, in a folder of its own (WorksheetImportFolder): the session's import, or, for a
    // script saved with its data and opened again, a copy of the data file beside it made as it opened.  The script in the editor reads
    // it, so nothing written later to a data file beside a script (another script saved under the same name, another program) changes
    // what the window runs, saves or hands to R (Codex's fourth review).  Save, Save As and R+ write a copy of it beside the script
    // they write, and that script reads the copy.
    private string OwnData => WorksheetImportFolder is string folder ? System.IO.Path.Combine(folder, WorksheetRData.CsvFileName) : null;
    private byte[] ownDataHash;
    // The data file beside the window's script as the window last wrote it or opened the script, with a hash of what it held.
    private (string Path, byte[] Hash)? knownDataFile;

    /// <summary>What writing the script did: a notice for the user about the data file, or null; the data file beside the written
    /// script and a hash of its data, or null when it has none; and whether the user chose not to replace a data file.</summary>
    private sealed record WrittenRScript(string Notice, string DataFile, byte[] Hash, bool Cancelled);

    /// <summary>
    /// Opened again, a script saved with its worksheet data works on a copy of its own, made now, as a session works on its import:
    /// the script in the editor reads the copy.  The script holds its data file through the import's own reader, or through the
    /// marker StatsDirect writes with each saved pair while its code still names the file (<see cref="WorksheetRImport.HoldsDataFile"/>;
    /// Codex's sixth and seventh reviews), so a script that writes a file of that name, a comment, a commented-out import or an
    /// operator of the user's own is left as it is.  Where it holds it, each literal in its code that names the file names the copy
    /// instead, and the marker is left out of the editor.  When no copy can be made, because the file is not there or another program holds it open,
    /// the user is asked to put it back or close it and retry, and otherwise the script is not opened
    /// (<see cref="WorksheetDataUnavailableException"/>): a window never works on the data file itself, which others can change (Codex's
    /// retest of 0b9c2282 and fifth review).  Returns the script for the editor.
    /// </summary>
    private string AdoptSavedWorksheetData(string scriptPath, string text)
    {
        if (WorksheetImportFolder != null) return text;
        string dataFile = DataFileBeside(scriptPath);
        if (!WorksheetRImport.HoldsDataFile(text, dataFile)) return text;
        while (true)
        {
            string folder = null;
            try
            {
                if (!File.Exists(dataFile)) throw new FileNotFoundException("The file is not there.", dataFile);
                folder = StatsDirect.R.WorksheetRLauncher.CreateImportFolder();
                string own = System.IO.Path.Combine(folder, WorksheetRData.CsvFileName);
                CopyData(dataFile, own);
                ownDataHash = DataHash(own);
                knownDataFile = HoldsData(dataFile, ownDataHash) ? (dataFile, ownDataHash) : null;
                WorksheetImportFolder = folder;
                string kept = folder;
                Disposed += (_, _) => StatsDirect.R.WorksheetRLauncher.DeleteImportFolder(kept);
                return WorksheetRImport.RenameInCode(WorksheetRImport.Unmarked(text, dataFile), dataFile, own);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                StatsDirect.R.WorksheetRLauncher.DeleteImportFolder(folder);
                WorksheetImportFolder = null;
                if (!RetryDataFile(dataFile, e))
                    throw new WorksheetDataUnavailableException("The script was not opened: StatsDirect could not copy its worksheet data file " + dataFile
                        + " (" + e.Message + ").  Put the file back, or close it in the program that has it open, then open the script again.", e);
            }
        }
    }

    // Whether to try again to copy a saved script's data file; the shell checks answer in the user's place, before the window opens.
    internal static Func<string, bool> DataFileRetrier { private get; set; }
    private static bool RetryDataFile(string dataFile, Exception error)
    {
        if (DataFileRetrier != null) return DataFileRetrier(dataFile);
        string name = System.IO.Path.GetFileName(dataFile);
        string problem = error is FileNotFoundException
            ? "“" + name + "”, the worksheet data file this script reads, is not there.\n\nIf you can put it back, do so and choose Retry."
            : "StatsDirect cannot copy “" + name + "”, the worksheet data file this script reads: " + error.Message
              + "\n\nIf another program has the file open, close it there and choose Retry.";
        return MessageBox.Show(SdApplication.SoleInstance.DialogOwner, problem + " Cancel leaves the script closed.",
            "Open R script", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) == DialogResult.Retry;
    }

    // A window that holds worksheet data stays an R script: saved in another language, its script would name the window's own copy,
    // which goes when the window closes (the independent review of 0b9c2282).
    private bool KeepRForWorksheetData()
    {
        if (WorksheetImportFolder == null || cboLanguage.SelectedIndex == 1) return false;
        cboLanguage.SelectedIndex = 1;   // raises the change again, which shows R
        AppendScriptNotice("(this script holds worksheet data, so it stays an R script)\n");
        return true;
    }

    /// <summary>
    /// Writes the script, for Save, Save As or R+.  A script that reads the window's own worksheet data carries a copy of them in a file
    /// beside it, named after the script so that saving again replaces it (RULES.md 6.3), and the written script reads that file.  The
    /// editor's script is left as it is.  A data file the script refers to in any other way is an ordinary file of the user's.  A data
    /// file already beside the script that holds other data, and that this window did not write, is replaced only when the user agreed
    /// to replace the script there, or agrees now.  The script and its data file change together: a data file this write replaced is put
    /// back when the write fails.  A data file the user has made read-only is not replaced.  Nothing about the window changes here.
    /// </summary>
    private WrittenRScript WriteRScript(string destination, string source, bool replacing)
    {
        string full = System.IO.Path.GetFullPath(destination);
        string folder = System.IO.Path.GetDirectoryName(full);
        // Working files have short names of their own, so that a long script name does not make theirs too long for Windows.
        string token = "statsdirect-" + Guid.NewGuid().ToString("N");
        string temporary = System.IO.Path.Combine(folder, token + ".tmp");
        string temporaryCsv = System.IO.Path.Combine(folder, token + ".worksheet-data.tmp");
        string notice = null, companion = null, previous = null, dataFile = null;
        byte[] hash = null;
        bool copyData = false, placed = false;
        try
        {
            string own = OwnData;
            if (own != null && WorksheetRImport.NamesInCode(source, own))
            {
                // The data come from the window's own copy, or, should that have gone, from the data file the window last wrote while
                // it still holds the same data.
                if (File.Exists(own))
                    (dataFile, hash) = (own, ownDataHash ??= DataHash(own));
                else if (ownDataHash != null && knownDataFile is { } last && HoldsData(last.Path, ownDataHash))
                    (dataFile, hash) = (last.Path, ownDataHash);
                if (dataFile != null)
                {
                    companion = DataFileBeside(full);
                    // An unchanged data file is left alone, so that one open in Excel does not stop the save.
                    copyData = !SameFile(dataFile, companion) && !HoldsData(companion, hash);
                    if (copyData && File.Exists(companion))
                    {
                        if (File.GetAttributes(companion).HasFlag(FileAttributes.ReadOnly))
                            throw new UnauthorizedAccessException("The worksheet data file " + companion + " is read-only, so the script is not saved over it.");
                        bool written = knownDataFile is { } mine && SameFile(mine.Path, companion) && HoldsData(companion, mine.Hash);
                        // A script being replaced takes its own data file with it; the dialog asked about the script, so a data file
                        // that the script there does not read is asked about now (Codex's retest of 0b9c2282).
                        bool goesWithScript = replacing && ReadsDataFile(full, companion);
                        if (!written && !goesWithScript && !ConfirmReplaceData(companion))
                            return new WrittenRScript("(the script is not saved, and " + System.IO.Path.GetFileName(companion) + " is left as it is)\n", null, null, true);
                    }
                    if (copyData)
                    {
                        CopyData(dataFile, temporaryCsv);
                        // The data are those copied: the window's own copy as it now is, or the data file the window last wrote, which
                        // must still hold the same data.
                        byte[] copied = DataHash(temporaryCsv);
                        if (copied.AsSpan().SequenceEqual(hash)) { }
                        else if (SameFile(dataFile, own)) hash = ownDataHash = copied;
                        else (companion, copyData) = (null, false);
                    }
                }
                if (companion != null)
                {
                    source = WorksheetRImport.Marked(WorksheetRImport.RenameInCode(source, own, companion), System.IO.Path.GetFileName(companion));
                    notice = "(the worksheet data is saved beside the script, as " + System.IO.Path.GetFileName(companion) + ")\n";
                }
                else
                    notice = "(the script is saved without its worksheet data: the data it reads are no longer there)\n";
            }

            File.WriteAllText(temporary, source, new UTF8Encoding(false));
            try
            {
                if (copyData)
                {
                    if (File.Exists(companion))
                    {
                        previous = System.IO.Path.Combine(folder, token + ".previous.worksheet-data.csv");
                        File.Move(companion, previous);
                    }
                    File.Move(temporaryCsv, companion);
                    placed = true;
                }
                File.Move(temporary, destination, true);
            }
            catch
            {
                if (copyData)
                    try
                    {
                        if (placed) File.Delete(companion);
                        if (previous != null) File.Move(previous, companion);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    finally { previous = null; }   // put back, or left beside it, under its working name, when it could not be
                throw;
            }
            return new WrittenRScript(notice, companion, companion == null ? null : hash, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(temporaryCsv)) File.Delete(temporaryCsv);
            if (previous != null)
                try { File.Delete(previous); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// A script's data file, beside it and named after it, so that no two scripts' names give one data file (Codex's fourth review):
    /// analysis.R has analysis.worksheet-data.csv, as the help says, and a script of any other name has its whole name followed by
    /// -worksheet-data.csv (notes.txt has notes.txt-worksheet-data.csv), which is never the name of a .R script's data file.
    /// </summary>
    internal static string DataFileBeside(string script)
    {
        string full = System.IO.Path.GetFullPath(script), name = System.IO.Path.GetFileName(full);
        string file = string.Equals(System.IO.Path.GetExtension(name), ".R", StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.GetFileNameWithoutExtension(name) + ".worksheet-data.csv"
            : name + "-worksheet-data.csv";
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full), file);
    }

    // Whether the script at a path holds a data file (WorksheetRImport.HoldsDataFile): a script saved with its data, whose data file
    // goes with it when the script is replaced.  An ordinary script there, one that writes a file of that name among them, owns no data
    // file (Codex's sixth review).
    private static bool ReadsDataFile(string script, string dataFile)
    {
        try
        {
            using var stream = new FileStream(script, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 64 << 20) return false;   // no script StatsDirect writes is so large
            using var reader = new StreamReader(stream);
            return WorksheetRImport.HoldsDataFile(reader.ReadToEnd(), dataFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool SameFile(string a, string b) =>
        string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    // A copy of a data file, which another program may have open, as Excel opens a CSV file.
    private static void CopyData(string from, string to)
    {
        using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(to, FileMode.CreateNew, FileAccess.Write);
        input.CopyTo(output);
    }

    private static byte[] DataHash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return System.Security.Cryptography.SHA256.HashData(stream);
    }

    // Whether a file holds the data with this hash; a file another program has open, as Excel opens a CSV file, may still be read.
    private static bool HoldsData(string path, byte[] hash)
    {
        if (hash == null || !File.Exists(path)) return false;
        try { return DataHash(path).AsSpan().SequenceEqual(hash); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private async void ClosingInteractiveR(FormClosingEventArgs e)
    {
        if (allowInteractiveClose)
        {
            SdApplication.SoleInstance.NoteFormClosing(this, e);
            return;
        }
        e.Cancel = true;
        if (PendingInteractiveClose != null) return;
        bool parentClosing = e.CloseReason == CloseReason.MdiFormClosing;
        PendingInteractiveClose = ConfirmInteractiveCloseAsync();
        bool accepted = await PendingInteractiveClose;
        if (parentClosing) return;
        PendingInteractiveClose = null;
        // The close is certain now, no other window can veto it: R ends its session and removes its temporary folder before the window goes.
        // (When the main window closes, the window's disposal asks the worker to stop in the same way.)
        if (accepted && !IsDisposed) { await InteractiveConsole.ShutdownAsync(); if (!IsDisposed) BeginInvoke(new Action(Close)); }
    }

    private async Task<bool> ConfirmInteractiveCloseAsync()
    {
        try
        {
            if (InteractiveConsole.Ready.IsCompletedSuccessfully) await InteractiveConsole.GetScriptAsync();
            bool accepted = !Dirty || dirtyButSafeToClose;
            if (!accepted)
            {
                DialogResult answer = AskToSaveChanges();
                accepted = answer == DialogResult.No || answer == DialogResult.Yes && await SaveInteractiveAsync(false);
            }
            allowInteractiveClose = accepted; dirtyButSafeToClose = accepted;
            if (!accepted) SdApplication.SoleInstance.NoteASubformCloseIsCancelled();
            return accepted;
        }
        catch (Exception ex)
        {
            InteractiveConsole.ReportNotice(ex.Message);
            SdApplication.SoleInstance.NoteASubformCloseIsCancelled();
            return false;
        }
    }
    internal void CancelInteractiveParentClose() { allowInteractiveClose = false; PendingInteractiveClose = null; NoteNonClosure(); }
    internal void CompleteInteractiveParentClosePreparation() => PendingInteractiveClose = null;

    private async Task InteractiveEditAsync(string command)
    {
        try
        {
            string selected = JsonSerializer.Deserialize<string>(await InteractiveConsole.ExecuteAsync("window.StatsDirectRHost.selectedText()")) ?? "";
            if (command is "copy" or "cut") { if (selected.Length == 0) return; Clipboard.SetText(selected); }
            if (command == "copy") return;
            if (command == "paste" && !Clipboard.ContainsText()) return;
            string text = command == "paste" && Clipboard.ContainsText() ? Clipboard.GetText() : "";
            await InteractiveConsole.ExecuteAsync("window.StatsDirectRHost.edit(" + JsonSerializer.Serialize(command) + "," + JsonSerializer.Serialize(text) + ")");
        }
        catch (Exception ex) { InteractiveConsole.ReportNotice(ex.Message); }
    }
}

/// <summary>A saved R script that was not opened because no copy of its worksheet data file could be made, the user having chosen not to
/// retry: a window never works on the data file itself (frmScript.AdoptSavedWorksheetData).</summary>
internal sealed class WorksheetDataUnavailableException : IOException
{
    internal WorksheetDataUnavailableException(string message, Exception inner) : base(message, inner) { }
}

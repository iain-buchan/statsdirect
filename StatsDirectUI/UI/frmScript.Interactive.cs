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
    private bool mirroringRSource, savingInteractive, allowInteractiveClose;
    internal Task<bool> PendingInteractiveClose { get; private set; }
    private bool IsInteractiveR => cboLanguage.SelectedIndex == 1 && InteractiveConsole != null;

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

    internal async Task<bool> SaveInteractiveAsync(bool choose)
    {
        if (savingInteractive) return false;
        savingInteractive = true;
        try
        {
            string source = await InteractiveConsole.GetScriptAsync();
            string destination = currentFile;
            if (choose || destination == null)
            {
                destination = ChooseRScriptFile(destination);
                if (destination == null) return false;
            }
            var written = WriteRScript(destination, source);
            if (written.Notice != null) AppendScriptNotice(written.Notice);
            currentFile = destination; Path = destination;
            if (written.Companion != null)
            {
                savedWorksheetData = written.Fallback;
                // The script now reads the data saved beside it, so that Run, Save, Save As and R+ all take them from there, and
                // the file the data came from, which may now be another script's, is no longer this one's (Codex's third review).
                if (written.DataInR != written.CompanionInR)
                    await ReplaceInScriptAsync(written.DataInR, written.CompanionInR);
            }
            if (EditorLines(InteractiveConsole.Script) == EditorLines(written.Text)) { rtbDoc.Modified = false; Dirty = false; }
            SdApplication.SoleInstance.NoteRecentFile(destination, true);
            return !Dirty;
        }
        finally { savingInteractive = false; }
    }

    // A script as the editor holds it, each line ending in LF alone: the script written, with CR LF where the console added it, is the same.
    private static string EditorLines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

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

    // The program's own change to the script, made in the editor as it is now, so that what the user types meanwhile stays.
    private async Task ReplaceInScriptAsync(string text, string replacement)
    {
        string script = await InteractiveConsole.ReplaceInScriptAsync(text, replacement);
        mirroringRSource = true;
        try { rtbDoc.Text = script; }
        finally { mirroringRSource = false; }
    }

    // A copy that still holds a saved script's data, for the R text the script refers to its data file by: when that file has gone, a
    // later save takes the data from the copy while it still holds them.  A reference the user has changed has no copy.
    private (string ForInR, string Path, byte[] Hash)? savedWorksheetData;

    /// <summary>What writing the script did.</summary>
    /// <param name="Text">The script as written.</param>
    /// <param name="Notice">A notice for the user about the data file, or null.</param>
    /// <param name="DataInR">The R text by which the script referred to its worksheet data, or null when it reads none.</param>
    /// <param name="Companion">The data file beside the script, or null when it has none.</param>
    /// <param name="CompanionInR">The R text by which the written script refers to that file.</param>
    /// <param name="Fallback">A copy that still holds the data, for that text.</param>
    private sealed record WrittenRScript(string Text, string Notice, string DataInR, string Companion, string CompanionInR,
        (string ForInR, string Path, byte[] Hash)? Fallback);

    /// <summary>
    /// Writes the script, for Save, Save As or R+.  A script that reads worksheet data carries the data in a file beside it, named after
    /// the script so that saving again replaces it (RULES.md 6.3).  The data are those of the window's script: the session's import,
    /// which the running session keeps until the window closes, or the data file beside the file the window holds, which a save moves
    /// beside the new file.  A data file the script refers to in any other way is an ordinary file of the user's, and stays as it is.
    /// The script and its data file change together: a data file this write replaced is put back when the write fails.  A data file
    /// the user has made read-only is not replaced.  Nothing about the window changes here: Save and Save As take the result.
    /// </summary>
    private WrittenRScript WriteRScript(string destination, string source)
    {
        string full = System.IO.Path.GetFullPath(destination);
        string folder = System.IO.Path.GetDirectoryName(full);
        // Working files have short names of their own, so that a long script name does not make theirs too long for Windows.
        string token = "statsdirect-" + Guid.NewGuid().ToString("N");
        string temporary = System.IO.Path.Combine(folder, token + ".tmp");
        string temporaryCsv = System.IO.Path.Combine(folder, token + ".worksheet-data.tmp");
        string notice = null, companion = null, companionInR = null, previous = null, dataFile = null;
        byte[] hash = null;
        bool copyData = false, placed = false;
        try
        {
            string original = null;
            if (WorksheetImportFolder is string importFolder && source.Contains(WorksheetRData.QuotePath(System.IO.Path.Combine(importFolder, WorksheetRData.CsvFileName)), StringComparison.Ordinal))
                original = System.IO.Path.Combine(importFolder, WorksheetRData.CsvFileName);
            else if (!string.IsNullOrEmpty(currentFile) && source.Contains(WorksheetRData.QuotePath(DataFileBeside(currentFile)), StringComparison.Ordinal))
                original = DataFileBeside(currentFile);
            string originalInR = original == null ? null : WorksheetRData.QuotePath(original);

            if (original != null)
            {
                // The data come from the file the script refers to, or, when that has gone, from a copy that still holds them.
                byte[] required = null;
                if (File.Exists(original))
                    (dataFile, hash) = (original, DataHash(original));
                else if (savedWorksheetData is { } saved && saved.ForInR == originalInR && HoldsData(saved.Path, saved.Hash))
                    (dataFile, hash, required) = (saved.Path, saved.Hash, saved.Hash);
                if (dataFile != null)
                {
                    companion = DataFileBeside(full);
                    // An unchanged data file is left alone, so that one open in Excel does not stop the save.
                    copyData = !SameFile(dataFile, companion) && !HoldsData(companion, hash);
                    if (copyData)
                    {
                        if (File.Exists(companion) && File.GetAttributes(companion).HasFlag(FileAttributes.ReadOnly))
                            throw new UnauthorizedAccessException("The worksheet data file " + companion + " is read-only, so the script is not saved over it.");
                        File.Copy(dataFile, temporaryCsv);
                        // The data are those copied, whatever wrote to the file meanwhile; a copy kept for its data must still hold them.
                        byte[] copied = DataHash(temporaryCsv);
                        if (required != null && !copied.AsSpan().SequenceEqual(required)) (companion, copyData) = (null, false);
                        else hash = copied;
                    }
                }
                if (companion != null)
                {
                    companionInR = WorksheetRData.QuotePath(companion);
                    source = source.Replace(originalInR, companionInR, StringComparison.Ordinal);
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
            // The copy for a later save: the file the data came from, while it holds them, unless that is the data file itself, when the
            // copy kept before stays if it holds the same data.
            (string ForInR, string Path, byte[] Hash)? fallback = null;
            if (companion != null)
                fallback = !SameFile(dataFile, companion) ? (companionInR, dataFile, hash)
                    : savedWorksheetData is { } kept && kept.ForInR == companionInR && kept.Hash.AsSpan().SequenceEqual(hash) ? kept : null;
            return new WrittenRScript(source, notice, originalInR, companion, companionInR, fallback);
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

    // A script's data file: beside it, named after it.
    private static string DataFileBeside(string script)
    {
        string full = System.IO.Path.GetFullPath(script);
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full), System.IO.Path.GetFileNameWithoutExtension(full) + ".worksheet-data.csv");
    }

    private static bool SameFile(string a, string b) =>
        string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

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

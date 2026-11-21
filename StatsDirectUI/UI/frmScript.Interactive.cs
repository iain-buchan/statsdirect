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
                using var dialog = new SaveFileDialog
                {
                    Title = "Save R script", Filter = "R scripts (*.R)|*.R|All files (*.*)|*.*",
                    DefaultExt = "R", AddExtension = true, FileName = destination ?? ScriptFileName(Text)
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                destination = dialog.FileName;
            }
            string data = WriteRScript(destination, source);
            if (data != null) AppendScriptNotice(data);
            currentFile = destination; Path = destination;
            if (InteractiveConsole.Script == source) { rtbDoc.Modified = false; Dirty = false; }
            SdApplication.SoleInstance.NoteRecentFile(destination, true);
            return !Dirty;
        }
        finally { savingInteractive = false; }
    }

    // The data files this window has put beside its saved scripts, by the script's full path, with a hash of the data written, so that a
    // later save of the same script can still point at its data when the session's own copy has gone, while that file still holds them.
    private readonly System.Collections.Generic.Dictionary<string, (string Path, byte[] Hash)> savedWorksheetData = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Saves the script.  A script that reads its session's worksheet data carries that data in a file beside it, named after the
    /// script so that saving again replaces it (RULES.md 6.3).  Returns a notice for the user about the data file, or null.
    /// The script and its data file change together: a data file this save replaced is put back when the save fails.  A data file the
    /// user has made read-only is not replaced.
    /// </summary>
    private string WriteRScript(string destination, string source)
    {
        string full = System.IO.Path.GetFullPath(destination);
        string folder = System.IO.Path.GetDirectoryName(full);
        // Working files have short names of their own, so that a long script name does not make theirs too long for Windows.
        string token = "statsdirect-" + Guid.NewGuid().ToString("N");
        string temporary = System.IO.Path.Combine(folder, token + ".tmp");
        string temporaryCsv = System.IO.Path.Combine(folder, token + ".worksheet-data.tmp");
        string notice = null, companion = null, previous = null;
        byte[] hash = null;
        bool copyData = false, placed = false;
        try
        {
            // Only the approved worksheet snapshot accompanies a saved script.
            // The running session keeps its temporary import until the window closes.
            if (WorksheetImportFolder is string importFolder)
            {
                string original = System.IO.Path.Combine(importFolder, WorksheetRData.CsvFileName);
                string originalInR = WorksheetRData.QuotePath(original);
                if (source.Contains(originalInR, StringComparison.Ordinal))
                {
                    string beside = System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(full) + ".worksheet-data.csv");
                    if (File.Exists(original))
                    {
                        companion = beside;
                        hash = DataHash(original);
                        // An unchanged data file is left alone, so that one open in Excel does not stop the save.
                        copyData = !HoldsData(companion, hash);
                        if (copyData)
                        {
                            if (File.Exists(companion) && File.GetAttributes(companion).HasFlag(FileAttributes.ReadOnly))
                                throw new UnauthorizedAccessException("The worksheet data file " + companion + " is read-only, so the script is not saved over it.");
                            File.Copy(original, temporaryCsv);
                        }
                    }
                    else if (savedWorksheetData.TryGetValue(full, out var saved) && HoldsData(saved.Path, saved.Hash))
                        (companion, hash) = saved;   // the session's copy has gone, but this window's data are still beside this script
                    if (companion != null)
                    {
                        source = source.Replace(originalInR, WorksheetRData.QuotePath(companion), StringComparison.Ordinal);
                        notice = "(the worksheet data is saved beside the script, as " + System.IO.Path.GetFileName(companion) + ")\n";
                    }
                    else
                        notice = "(the script is saved without its worksheet data: the session's copy of the data is no longer there)\n";
                }
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
            if (companion != null) savedWorksheetData[full] = (companion, hash);
            return notice;
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

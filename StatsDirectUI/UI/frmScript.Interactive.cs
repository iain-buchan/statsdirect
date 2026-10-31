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
            view.MoreRequested += ShowRMoreMenu;
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
        Item("Open in R / RStudio", OpenInR);
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
        await InteractiveConsole.SubmitAsync(source, "script");
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
            // Saving an R script never serialises the live workspace or executes it.
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, source, new UTF8Encoding(false));
                File.Move(temporary, destination, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            currentFile = destination; Path = destination;
            if (InteractiveConsole.Script == source) { rtbDoc.Modified = false; Dirty = false; }
            SdApplication.SoleInstance.NoteRecentFile(destination, true);
            return !Dirty;
        }
        finally { savingInteractive = false; }
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
        if (accepted && !IsDisposed) BeginInvoke(new Action(Close));
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

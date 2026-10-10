using StatsDirect.Configuration;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using StatsDirect.UI.WebReports;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI;

/// <summary>HTML/SVG report editor for generated and saved reports.</summary>
internal sealed class frmReportWebView : StatsDirectForm, IReport
{
    private readonly ReportView view;
    private readonly Label status = new() { Dock = DockStyle.Bottom, AutoSize = true, Text = "Loading the report editor…" };   // sized by its text: a fixed height cut it off at a display scaling above 100%
    private bool allowClosing, saving, opening;
    private static readonly System.Threading.SemaphoreSlim closePrompts = new(1, 1);
    internal Task<bool> PendingClose { get; private set; }

    internal frmReportWebView()
    {
        Text = "Report";
        Width = 1000; Height = 750;
        view = new ReportView(System.IO.Path.Combine(SDConfiguration.InstallationDirectory, "ReportEditor")) { Dock = DockStyle.Fill };
        Controls.Add(view); Controls.Add(status);
        status.Padding = new Padding((int)Math.Round(6 * DeviceDpi / 96.0));   // the padding scaled with the display
        CreateFileMenu();
        view.Changed += () => { if (allowClosing) CancelParentClose(); Dirty = true; status.Text = "Unsaved changes"; };
        view.Notice += text => status.Text = text;
        view.TopicRequested += ShowTopic;
        view.RRequested += id => _ = Guard(() => ContinueInRAsync(id));
        view.CommandRequested += command => _ = Guard(async () =>
        {
            if (command == "open") await OpenDialogAsync();
            else if (command == "print") Print();
            else await SaveHtmlAsync(command == "saveAs");
        });
        Shown += async (_, _) => await Guard(async () => { await view.Ready; status.Text = "Ready"; });
        Activated += (_, _) =>
        {
            if (Tag is WindowInformation info) SdApplication.SoleInstance.NoteFormActivated(info);
        };
        FormClosing += ClosingReport;
    }

    private void CreateFileMenu()
    {
        // Use the same automatic MDI menu merging as the data grid.
        // Each report owns its commands; switching tabs restores the other
        // document's File menu without leaving a report toolbar behind.
        var menu = new MenuStrip { Name = "webReportFileMenu", Visible = false };
        var file = new ToolStripMenuItem("&File") { MergeAction = MergeAction.MatchOnly };
        menu.Items.Add(file);
        // Let the merged dropdown finish closing before opening a modal dialog
        // or closing its owning MDI child. Re-entering menu mode from Click can
        // otherwise modify WinForms' toolstrip collection during enumeration.
        ToolStripMenuItem Command(string name, string text, Func<Task> action, Keys shortcut = Keys.None) =>
            new(text, null, (_, _) => BeginInvoke(new Action(async () =>
            {
                if (!IsDisposed && !Disposing) await Guard(action);
            })))
            {
                Name = name, MergeAction = MergeAction.Replace, ShortcutKeys = shortcut
            };
        file.DropDownItems.Add(Command("webReportSave", "&Save", async () => await SaveHtmlAsync(false), Keys.Control | Keys.S));
        file.DropDownItems.Add(Command("webReportSaveAs", "S&ave As...", async () => await SaveHtmlAsync(true), Keys.Control | Keys.Shift | Keys.S));
        file.DropDownItems.Add(Command("webReportClose", "&Close", () => { Close(); return Task.CompletedTask; }));
        file.DropDownItems.Add(Command("webReportPrintPreview", "Print Preview", () => { Print(); return Task.CompletedTask; }));
        file.DropDownItems.Add(Command("webReportPrint", "&Print...", () => { Print(); return Task.CompletedTask; }, Keys.Control | Keys.P));
        // Insert beside the existing Open and Save As entries in frmMain's
        // File menu (which includes New Web Report at index 2).
        var open = Command("webReportOpenHtml", "Open HTML...", OpenDialogAsync);
        open.MergeAction = MergeAction.Insert; open.MergeIndex = 5;
        file.DropDownItems.Add(open);
        var export = new ToolStripMenuItem("Export report") { Name = "webReportExport", MergeAction = MergeAction.Insert, MergeIndex = 8 };
        export.DropDownItems.Add(Command("webReportExportWord", "Word document...", () => ExportAsync("docx")));
        export.DropDownItems.Add(Command("webReportExportPdf", "PDF...", () => ExportAsync("pdf")));
        file.DropDownItems.Add(export);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }
    private async Task Guard(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { status.Text = ex.Message; SdApplication.SoleInstance.FriendlyError("The Web Report command could not be completed", ex, false); }
    }

    private object lastRun;            // the run of the operation whose item was appended last
    private Task<string> lastItem;     // that item's id, once appended

    void IReport.AppendRenderable(IRenderable renderable, int helpContextId, Operation operation, object run)
    {
        string html = new HtmlRenderer(SdApplication.SoleInstance).Render(renderable);
        if (allowClosing) CancelParentClose();
        Dirty = true;
        string record = (run as OperationRun)?.Record;   // the record of the run, kept with the item for the R script of the result
        if (run != null && run == lastRun && lastItem != null)
        {
            // another output of the same run, a chart after its analysis: into that item, at whose start the window stays
            Task<string> item = lastItem;
            _ = Guard(async () => await view.ExtendAsync(await item, html, record));
        }
        else
        {
            lastRun = run;
            lastItem = view.AppendAsync(html, operation?.FriendlyName ?? "Analysis", operation?.Name ?? "", helpContextId, record);
            _ = Guard(() => lastItem);
        }
    }

    /// <summary>
    /// The result the user chose to continue in R: its script, written from the record of its run and the workbook the record points
    /// to, opens in a script window set to R, where Run runs it with the installed R and Save As keeps it as an R file.
    /// </summary>
    private async Task ContinueInRAsync(string id)
    {
        JsonElement entry = await view.RecordAsync(id);
        JsonElement record = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("record", out JsonElement r) ? r : default;
        if (record.ValueKind != JsonValueKind.Object)
        {
            status.Text = "This result has no record of its run, so there is nothing to continue in R.";
            return;
        }
        string title = entry.TryGetProperty("title", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString() : "Analysis";
        RScript script = RScriptWriter.Write(record, title, FrameReader.Read);
        if (SdApplication.SoleInstance.CreateScriptWindow() is frmScript window)
            window.ShowR(script.Text, "R: " + title);
        status.Text = script.DataUnavailable ? "R script opened, but its data could not be read from the workbook: see the head of the script"
                    : script.HasRecipe ? "R script of the analysis opened" : "R script with the data and settings opened";
    }

    public override IList<Pane> AvailablePanes => new List<Pane> { SelectedPane };
    public override Pane SelectedPane => new(Text, WindowInformation, 0);
    public override bool SelectPane(Pane pane) => true;
    internal override void EditCopy() => _ = view.ClipboardCommandAsync("copy");
    internal override void EditCut() => _ = view.ClipboardCommandAsync("cut");
    internal override void EditPaste() => _ = view.ClipboardCommandAsync("paste");
    internal override void Print() => _ = Guard(async () => { await view.Ready; view.Browser.CoreWebView2.ShowPrintUI(); });
    internal override void ShowHelp() => ShowTopic(view.HelpContext);
    private void ShowTopic(int context)
    {
        if (context > 0) SdApplication.SoleInstance.ShowHelp(this, context.ToString(System.Globalization.CultureInfo.InvariantCulture));
        else SdApplication.SoleInstance.ShowHelp(this);
    }

    // The existing File/Save hooks are synchronous. Do not block WebView2's UI
    // thread waiting for script replies. Closing uses the awaited path below.
    internal override bool SaveContents() { _ = Guard(async () => await SaveHtmlAsync(false)); return false; }
    internal override bool SaveAsContents() { _ = Guard(async () => await SaveHtmlAsync(true)); return false; }

    private async Task<bool> SaveHtmlAsync(bool choose)
    {
        if (saving || opening) return false;
        saving = true;
        try
        {
            string destination = path;
            if (choose || string.IsNullOrEmpty(destination))
            {
                using var dialog = new SaveFileDialog { Filter = "Editable HTML report (*.html)|*.html", DefaultExt = "html", FileName = destination ?? "StatsDirect report.html", AddExtension = true };
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                destination = dialog.FileName;
            }
            long revision = await view.SaveAsync(destination, Text, "html");
            Path = destination; Text = System.IO.Path.GetFileName(destination);
            SdApplication.SoleInstance.NoteRecentFile(destination, true);
            // A user may continue typing while export awaits image conversion.
            // Such edits must not be marked as saved by an earlier snapshot.
            Dirty = view.Revision != revision;
            status.Text = Dirty ? "Saved; newer edits remain unsaved." : "Saved " + destination;
            return !Dirty;
        }
        finally { saving = false; }
    }

    private async Task ExportAsync(string format)
    {
        using var dialog = new SaveFileDialog { Filter = format == "pdf" ? "PDF report (*.pdf)|*.pdf" : "Word document (*.docx)|*.docx", DefaultExt = format, FileName = "StatsDirect report." + format };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await view.SaveAsync(dialog.FileName, Text, format);
            status.Text = "Exported " + dialog.FileName + ". Use Save to retain an editable report.";
        }
    }

    private async Task OpenDialogAsync()
    {
        if (opening || !await ConfirmDiscardAsync()) return;
        using var dialog = new OpenFileDialog { Filter = "HTML reports (*.html;*.htm)|*.html;*.htm" };
        if (dialog.ShowDialog(this) == DialogResult.OK) await LoadHtmlAsync(dialog.FileName, false);
    }

    public override bool OpenFile(string filename, bool isTempFile, string nameToDisplay)
    {
        if (new FileInfo(filename).Length > 30_000_000) throw new IOException("This report exceeds the 30 MB report limit.");
        _ = Guard(() => LoadHtmlAsync(filename, isTempFile));
        return true;
    }

    private async Task LoadHtmlAsync(string filename, bool isTempFile)
    {
        if (opening || saving) return;
        opening = true;
        try
        {
            string html = await File.ReadAllTextAsync(filename);
            if (System.IO.Path.GetExtension(filename).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                html = "<pre>" + System.Net.WebUtility.HtmlEncode(html) + "</pre>";
            await view.OpenHtmlAsync(html, System.IO.Path.GetFileNameWithoutExtension(filename));
            bool importedText = System.IO.Path.GetExtension(filename).Equals(".txt", StringComparison.OrdinalIgnoreCase);
            Path = isTempFile || importedText ? null : filename; Text = System.IO.Path.GetFileName(filename);
            Dirty = false; status.Text = "Opened report.";
        }
        finally { opening = false; }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (saving || opening) return false;
        // Fence pending editor messages, including the last keystroke, before
        // deciding whether a close/open operation needs a save prompt.
        try { await view.CallAsync("snapshot"); } catch { if (!Dirty) return true; }
        if (!Dirty) return true;
        await closePrompts.WaitAsync();
        try
        {
            var answer = MessageBox.Show(this, Text + " has unsaved changes. Save the editable HTML report?", "StatsDirect", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
            return answer == DialogResult.No || (answer == DialogResult.Yes && await SaveHtmlAsync(false));
        }
        finally { closePrompts.Release(); }
    }

    private async void ClosingReport(object sender, FormClosingEventArgs e)
    {
        if (allowClosing)
        {
            SdApplication.SoleInstance.NoteFormClosing(this, e);
            return;
        }
        e.Cancel = true;
        if (PendingClose != null) return;
        bool parentClosing = e.CloseReason == CloseReason.MdiFormClosing;
        PendingClose = ConfirmCloseAsync();
        bool accepted = await PendingClose;
        if (parentClosing) return; // frmMain awaits and retries the whole close.
        PendingClose = null;
        if (accepted) Close();
    }

    private async Task<bool> ConfirmCloseAsync()
    {
        try
        {
            bool accepted = await ConfirmDiscardAsync();
            allowClosing = accepted; dirtyButSafeToClose = accepted;
            if (!accepted) SdApplication.SoleInstance.NoteASubformCloseIsCancelled();
            return accepted;
        }
        catch (Exception ex) { status.Text = ex.Message; return false; }
    }

    internal void CancelParentClose()
    {
        allowClosing = false; PendingClose = null; NoteNonClosure();
    }

    internal void CompleteParentClosePreparation() => PendingClose = null;
}

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
internal sealed class frmReportWebView : StatsDirectForm, IReport, IFindTarget
{
    private readonly ReportView view;
    internal ReportView LearningReport => view;
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
        findBar = new FindBar(this);
        Controls.Add(findBar);   // added last, so laid out first: a bar at the top, the report below it
        status.Padding = new Padding((int)Math.Round(6 * DeviceDpi / 96.0));   // the padding scaled with the display
        CreateFileMenu();
        view.Changed += () => { if (allowClosing) CancelParentClose(); Dirty = true; status.Text = "Unsaved changes"; };
        view.Notice += text => status.Text = text;
        view.TopicRequested += ShowTopic;
        view.RRequested += id => _ = Guard(() => ContinueInRAsync(id));
        view.AIRequested += id => (MdiParent as frmMain)?.Learning.ShowReportAI(this, id);
        view.ChartSaveRequested += format => _ = Guard(() => SaveChartAsync(format));
        view.CommandRequested += command => _ = Guard(async () =>
        {
            if (command == "open") await OpenDialogAsync();
            else if (command == "print") Print();
            else if (command == "find") EditFind();
            else if (command == "continueInR") await ContinueCurrentInRAsync();
            else if (command == "ai") (MdiParent as frmMain)?.Learning.ShowReportAI(this);
            else if (command is "save" or "saveAs") await SaveReportAsync(command == "saveAs");
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
        file.DropDownItems.Add(Command("webReportSave", "&Save", async () => await SaveReportAsync(false), Keys.Control | Keys.S));
        file.DropDownItems.Add(Command("webReportSaveAs", "S&ave As...", async () => await SaveReportAsync(true), Keys.Control | Keys.Shift | Keys.S));
        file.DropDownItems.Add(Command("webReportClose", "&Close", () => { Close(); return Task.CompletedTask; }));
        file.DropDownItems.Add(Command("webReportPrintPreview", "Print Preview", () => { Print(); return Task.CompletedTask; }));
        file.DropDownItems.Add(Command("webReportPrint", "&Print...", () => { Print(); return Task.CompletedTask; }, Keys.Control | Keys.P));
        // Insert beside the existing Open and Save As entries in frmMain's
        // File menu (which includes New Web Report at index 2).
        var open = Command("webReportOpenHtml", "Open HTML...", OpenDialogAsync);
        open.MergeAction = MergeAction.Insert; open.MergeIndex = 5;
        file.DropDownItems.Add(open);
        var export = Command("webReportExportNotebook", "Export Jupyter notebook...", ExportNotebookAsync);
        export.MergeAction = MergeAction.Insert; export.MergeIndex = 8;
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

    internal async Task<string> AppendLearningResultAsync(string html, string operationName, string record, Action check)
    {
        await view.Ready;
        var operation = TemplateFactory.Operations[operationName];
        string item = await view.AppendAsync(html, operation.FriendlyName, operationName, operation.HelpContext?.ChmId ?? 0, record, () =>
        {
            check();
            if (IsDisposed) throw new InvalidOperationException("The report closed before the calculation finished.");
            if (allowClosing) CancelParentClose();
            Dirty = true;
        });
        Activate(); return item;
    }

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
    /// The chart under the pointer saved as a file: an SVG file of its own markup, or a PNG drawn from it at twice its size on the page.
    /// </summary>
    private async Task SaveChartAsync(string format)
    {
        bool png = format == "png";
        using SaveFileDialog dialog = new()
        {
            Title = png ? "Save chart as PNG" : "Save chart as SVG",
            Filter = png ? "PNG picture|*.png|All files|*.*" : "SVG picture|*.svg|All files|*.*",
            DefaultExt = png ? "png" : "svg",
            FileName = png ? "chart.png" : "chart.svg",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        JsonElement image = await view.CallAsync("chartImage", new { format, scale = 2 });
        string data = image.ValueKind == JsonValueKind.String ? image.GetString() : null;
        if (string.IsNullOrEmpty(data))
            throw new InvalidOperationException("The chart gave no picture.");
        if (png)
        {
            int comma = data.IndexOf(',');
            if (!data.StartsWith("data:image/png;base64,", StringComparison.Ordinal) || comma < 0)
                throw new InvalidOperationException("The picture is not a PNG.");
            await File.WriteAllBytesAsync(dialog.FileName, Convert.FromBase64String(data[(comma + 1)..]));
        }
        else
            await File.WriteAllTextAsync(dialog.FileName, data, new System.Text.UTF8Encoding(false));
        status.Text = "Chart saved as " + dialog.FileName;
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
        {
            // the help of the script window is the result's topic: the operation's help context, as the menu shows it
            string operationName = entry.TryGetProperty("operation", out JsonElement o) && o.ValueKind == JsonValueKind.String ? o.GetString() : null;
            string helpTopic = !string.IsNullOrEmpty(operationName) && TemplateFactory.Operations.TryGetValue(operationName, out Operation operation) && operation.HelpContext != null
                               ? operation.HelpContext.Url ?? operation.HelpContext.ChmId.ToString() : null;
            window.ShowR(script.Text, title, helpTopic);
        }
        status.Text = script.DataUnavailable ? "R script opened, but its data could not be read from the workbook: see the head of the script"
                    : script.HasRecipe ? "R script of the analysis opened" : "R script with the data and settings opened";
    }

    /// <summary>
    /// The R button of the toolbar: the result the cursor or selection is in, else the latest result that has a record of its run,
    /// continued in R as the right-click menu's Continue in R continues the result under the pointer.
    /// </summary>
    internal void ContinueCurrentInR() => _ = Guard(ContinueCurrentInRAsync);
    private async Task ContinueCurrentInRAsync()
    {
        JsonElement target = await view.CurrentResultAsync();
        string id = target.ValueKind == JsonValueKind.Object && target.TryGetProperty("resultId", out JsonElement r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        if (id == null) { status.Text = "No result to continue in R: run an analysis into this report first."; return; }
        await ContinueInRAsync(id);
    }

    // Find on the toolbar: the bar at the top of the window, as the script window's, whose searches the view answers
    private readonly FindBar findBar;
    internal override void EditFind() => findBar.Open(false);
    string IFindTarget.SelectedText => null;
    void IFindTarget.FocusContent() => view.Browser.Focus();
    Task<bool> IFindTarget.FindNextAsync(string term, bool matchCase) => view.FindNextAsync(term, matchCase);
    Task<bool> IFindTarget.ReplaceSelectionAsync(string term, string replacement, bool matchCase) => view.ReplaceSelectionAsync(term, replacement, matchCase);
    Task<int> IFindTarget.ReplaceAllAsync(string term, string replacement, bool matchCase) => view.ReplaceAllAsync(term, replacement, matchCase);

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
        else SdApplication.SoleInstance.ShowHelp(this, "1405");   // the Report window topic, when the cursor is in no result
    }

    // The existing File/Save hooks are synchronous. Do not block WebView2's UI
    // thread waiting for script replies. Closing uses the awaited path below.
    internal override bool SaveContents() { _ = Guard(async () => await SaveReportAsync(false)); return false; }
    internal override bool SaveAsContents() { _ = Guard(async () => await SaveReportAsync(true)); return false; }

    private SaveFileDialog CreateSaveReportDialog(bool allowExports) => new()
    {
        Filter = "Editable HTML report (*.html)|*.html" + (allowExports ? "|Word document (*.docx)|*.docx|PDF document (*.pdf)|*.pdf" : ""),
        FilterIndex = 1, DefaultExt = "html", FileName = path ?? "StatsDirect report.html", AddExtension = true
    };

    private async Task<bool> SaveReportAsync(bool choose)
    {
        if (saving || opening) return false;
        saving = true;
        try
        {
            string destination = path;
            int formatIndex = 1;
            if (choose || string.IsNullOrEmpty(destination))
            {
                // Ordinary Save (including the close prompt) retains the editable
                // HTML report. Explicit Save As also offers Word and PDF copies.
                using var dialog = CreateSaveReportDialog(choose);
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                destination = dialog.FileName;
                formatIndex = dialog.FilterIndex;
            }
            return await SaveReportFileAsync(destination, formatIndex);
        }
        finally { saving = false; }
    }

    private async Task<bool> SaveReportFileAsync(string destination, int formatIndex)
    {
        string format = formatIndex switch { 1 => "html", 2 => "docx", 3 => "pdf", _ => throw new ArgumentOutOfRangeException(nameof(formatIndex)) };
        long revision = await view.SaveAsync(destination, Text, format);
        if (format != "html")
        {
            // Exporting must not redirect Ctrl+S to a Word/PDF file or discard
            // unsaved edits and analysis records from the editable HTML report.
            status.Text = "Exported " + destination + ". Use Save to retain an editable report.";
            return false;
        }
        Path = destination; Text = System.IO.Path.GetFileName(destination);
        SdApplication.SoleInstance.NoteRecentFile(destination, true);
        // A user may continue typing while export awaits image conversion.
        // Such edits must not be marked as saved by an earlier snapshot.
        Dirty = view.Revision != revision;
        status.Text = Dirty ? "Saved; newer edits remain unsaved." : "Saved " + destination;
        return !Dirty;
    }

    private SaveFileDialog CreateNotebookDialog() => new()
    {
        Title = "Export Jupyter notebook", Filter = "Jupyter notebook (*.ipynb)|*.ipynb",
        DefaultExt = "ipynb", FileName = System.IO.Path.GetFileNameWithoutExtension(path ?? Text) + ".ipynb", AddExtension = true
    };

    private async Task ExportNotebookAsync()
    {
        if (saving || opening) return;
        saving = true;
        try
        {
            using var dialog = CreateNotebookDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK) await ExportNotebookFileAsync(dialog.FileName);
        }
        finally { saving = false; }
    }

    private async Task ExportNotebookFileAsync(string destination)
    {
        if (!string.IsNullOrEmpty(path) && string.Equals(System.IO.Path.GetFullPath(path), System.IO.Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a separate .ipynb file so the editable HTML report is retained.");
        JsonElement snapshot = await view.NotebookSnapshotAsync(Text);
        string notebook = ReportNotebook.Write(snapshot, Text, FrameReader.Read, Application.ProductVersion);
        string temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(destination)), ".statsdirect-" + Guid.NewGuid().ToString("N") + ".ipynb");
        try { await File.WriteAllTextAsync(temporary, notebook, new System.Text.UTF8Encoding(false)); File.Move(temporary, destination, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        status.Text = "Exported notebook with report, analysis data and R code: " + destination;
    }

    private async Task OpenDialogAsync()
    {
        if (opening || !await ConfirmDiscardAsync()) return;
        using var dialog = new OpenFileDialog { Filter = "Reports (*.html;*.htm;*.rtf)|*.html;*.htm;*.rtf" };
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
            string extension = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            status.Text = "Opened report.";
            if (extension == ".rtf")
                await view.OpenLegacyAsync(await Task.Run(() => LegacyReport.Read(filename)), System.IO.Path.GetFileNameWithoutExtension(filename));
            else
            {
                string html = await File.ReadAllTextAsync(filename);
                if (extension == ".txt") html = "<pre>" + System.Net.WebUtility.HtmlEncode(html) + "</pre>";
                await view.OpenHtmlAsync(html, System.IO.Path.GetFileNameWithoutExtension(filename));
            }
            bool importedText = extension is ".txt" or ".rtf";
            Path = isTempFile || importedText ? null : filename; Text = System.IO.Path.GetFileName(filename);
            Dirty = false;
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
            // the same question, in the program's message banner, as a workbook or a script asks on closing
            DialogResult answer = AskToSaveChanges();
            return answer == DialogResult.No || (answer == DialogResult.Yes && await SaveReportAsync(false));
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

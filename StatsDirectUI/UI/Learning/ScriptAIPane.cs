using StatsDirect.UI.ToolWindows;
using StatsDirect.UI.WebReports;
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

/// <summary>A dockable assistant for the active script or report. Each question has a fixed, read-only document scope.</summary>
internal sealed class ScriptAIPane : IDisposable
{
    private readonly LearningService service;
    private readonly UserControl view = new() { Dock = DockStyle.Fill, Font = new Font("Arial", 10) };
    private readonly ToolPaneSession session;
    private readonly Panel viewport = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 10, Padding = new Padding(8) };
    private readonly FlowLayoutPanel sharing = new() { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    internal readonly ConversationView Transcript;   // the conversation as a page: Markdown rendered, code blocks with their buttons
    internal readonly TextBox Composer = new() { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, WordWrap = true, MaxLength = 4000, AccessibleName = "Question for the script assistant" };
    internal readonly CheckBox ShareScript = new() { AutoSize = true, Text = "Share this script and its R output" };
    internal readonly CheckBox ShareWorkbook = new() { AutoSize = true, Text = "Share open workbook data" };
    internal readonly CheckBox Acknowledge = new() { AutoSize = true, Text = "I have checked the shared content contains no person identifiers. Remember this for this session, until the open workbooks change." };
    internal readonly Button Send = new() { AutoSize = true, Text = "Send" };
    private readonly Button stop = new() { AutoSize = true, Text = "Stop", Enabled = false }, connection = new() { AutoSize = true, Text = "Use my ChatGPT" };
    internal readonly LinkLabel SharingOptions = new() { AutoSize = true, Text = "Sharing for this session ▾", Dock = DockStyle.Fill };
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, AccessibleName = "AI status" };
    private readonly Dictionary<string, Conversation> conversations = new();
    private StatsDirectForm script;
    private readonly ToolStripLabel title = new("Script AI");
    private string focusedResultId;
    private Conversation chat;
    private Conversation renderedChat;
    private CancellationTokenSource pending;
    private bool refreshing, disposed;
    private HashSet<string> openWorkbookIds;
    private string partial = "";
    internal bool IsOpen => session.IsVisible;
    private sealed class Conversation
    { internal JsonArray Messages = new(); internal string Draft = ""; internal bool ShareScript, ShareWorkbook; }
    internal frmSpreadsheetGear[] SharedWorkbooks => ShareWorkbook.Checked ? service.Documents.OfType<frmSpreadsheetGear>().Where(g => !g.IsDisposed && !g.Disposing && g.MdiParent != null).ToArray() : Array.Empty<frmSpreadsheetGear>();
    private IEnumerable<string> SharedDataIds => SharedWorkbooks.Select(g => g.Id());
    private bool ReportMode => script is frmReportWebView;
    internal ScriptAIPane(frmMain main, LearningService service)
    {
        this.service = service;
        openWorkbookIds = service.Documents.OfType<frmSpreadsheetGear>().Select(g => g.Id()).ToHashSet();
        Transcript = new ConversationView(Path.Combine(Configuration.SDConfiguration.InstallationDirectory, "Tutor"), Path.Combine(service.Storage, "WebView")) { Dock = DockStyle.Fill };
        Transcript.CodeRequested += (what, code, messageIndex, blockIndex) => { if (what == "copy") Clipboard.SetText(code); else OpenCode(messageIndex, blockIndex); };
        Transcript.Notice += text => { if (!disposed) status.Text = text; };
        var heading = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        var pop = new ToolStripButton("Pop out"); var close = new ToolStripButton("Close");
        heading.Items.AddRange([title, new ToolStripButton("New conversation", null, (_, _) => NewConversation()), pop, close]);
        for (int row = 0; row < 10; row++) layout.RowStyles.Add(new RowStyle(row == 8 ? SizeType.Percent : row == 4 ? SizeType.Absolute : SizeType.AutoSize, row == 8 ? 100 : row == 4 ? 100 : 0));
        layout.Controls.Add(connection, 0, 0); layout.Controls.Add(SharingOptions, 0, 1);
        var warning = new Label { AutoSize = true, Text = "Workbook sharing includes every open workbook. Do not share person-identifiable data: remove names, dates of birth, patient IDs and identifying free text." };
        sharing.Controls.AddRange([warning, ShareScript, ShareWorkbook, Acknowledge]); layout.Controls.Add(sharing, 0, 2);
        layout.Controls.Add(new Label { AutoSize = true, Text = "Your question (Enter sends; Shift+Enter adds a line)", Dock = DockStyle.Fill }, 0, 3);
        layout.Controls.Add(Composer, 0, 4);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var copyCode = new Button { AutoSize = true, Text = "Copy R code" }; var openCode = new Button { AutoSize = true, Text = "Open R code" };
        actions.Controls.AddRange([Send, stop]); layout.Controls.Add(actions, 0, 5); layout.Controls.Add(status, 0, 6);
        layout.Controls.Add(new Label { AutoSize = true, Text = "Conversation", Dock = DockStyle.Fill }, 0, 7);
        Transcript.AccessibleName = "AI conversation"; layout.Controls.Add(Transcript, 0, 8);
        var replyActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        replyActions.Controls.AddRange([copyCode, openCode]); layout.Controls.Add(replyActions, 0, 9);
        viewport.Controls.Add(layout); view.Controls.Add(viewport); view.Controls.Add(heading);
        session = new ToolPaneSession(main, view, "Script AI", DockStyle.Right, 440, new Size(500, 760),
            (floating, canDock) => { pop.Text = floating ? "Dock" : "Pop out"; pop.Enabled = !floating || canDock; }, () => Composer.Focus(), text => status.Text = text, minimumDockLength: 330);
        pop.Click += async (_, _) => await session.ToggleModeAsync(); close.Click += (_, _) => Hide();
        connection.Click += async (_, _) => { try { await service.ConfigureTutorAsync(view); } catch (Exception ex) { status.Text = ex.Message; } };
        ShareScript.CheckedChanged += (_, _) => ScopeChanged(); ShareWorkbook.CheckedChanged += (_, _) => ScopeChanged();
        Acknowledge.CheckedChanged += (_, _) => {
            if (refreshing) return;
            if (Acknowledge.Checked) {
                service.Sharing.Approve(SharedDataIds); sharing.Visible = false;
                if (status.Text == "Review the selected content and confirm sharing for this session." || status.Text == "The open workbooks changed. Review their data and confirm sharing again.") status.Text = "";
                Composer.Focus();
            } else service.RevokeAISharing();
            UpdateControls();
        };
        SharingOptions.LinkClicked += (_, _) => { sharing.Visible = !sharing.Visible; UpdateControls(); };
        Send.Click += async (_, _) => await SendAsync(); stop.Click += (_, _) => Stop();
        Composer.KeyDown += (_, e) => { if (ComposerKey(e.KeyData)) { e.SuppressKeyPress = true; e.Handled = true; } };
        copyCode.Click += (_, _) => { string code = LastCode(); if (code.Length > 0) Clipboard.SetText(code); else status.Text = "The last reply contains no R code block."; };
        openCode.Click += (_, _) => OpenCode(LastReply());
        void FitContent()
        {
            int width = Math.Max(120, viewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - layout.Padding.Horizontal - 12);
            warning.MaximumSize = status.MaximumSize = new Size(width, 0);
            // AutoSize checkboxes can clip wrapped text at high DPI. Measure the full label
            // and keep a scrollable minimum height for the conversation in small panes.
            foreach (var box in new[] { ShareScript, ShareWorkbook, Acknowledge })
            {
                box.AutoSize = false;
                int glyph = Math.Max(24, box.Font.Height);
                box.Size = new Size(width, Math.Max(glyph, TextRenderer.MeasureText(box.Text, box.Font, new Size(Math.Max(1, width - glyph), int.MaxValue), TextFormatFlags.WordBreak).Height) + 8);
            }
            layout.RowStyles[4].Height = Composer.Font.Height * 3 + 12;
            Transcript.MinimumSize = new Size(0, Transcript.Font.Height * 4 + 12);
            layout.MinimumSize = new Size(0, viewport.ClientSize.Height);
        }
        viewport.ClientSizeChanged += (_, _) => FitContent(); view.FontChanged += (_, _) => FitContent();
        FitContent();
    }
    internal void Open(StatsDirectForm document, string resultId = null)
    {
        if (script != document)
        {
            SaveDraft(); Stop();
            if (script != null) script.FormClosed -= ScriptClosed;
            script = document; script.FormClosed += ScriptClosed;
            if (!conversations.TryGetValue(document.Id(), out chat)) conversations[document.Id()] = chat = new Conversation { ShareScript = service.ShareAIDocument, ShareWorkbook = service.ShareAIWorkbook };
            refreshing = true; Composer.Text = chat.Draft; ShareScript.Checked = chat.ShareScript; ShareWorkbook.Checked = chat.ShareWorkbook; refreshing = false;
            service.ShareAIDocument = ShareScript.Checked; service.ShareAIWorkbook = ShareWorkbook.Checked;
            sharing.Visible = true; partial = "";
        }
        focusedResultId = resultId;
        title.Text = ReportMode ? "Report AI" : "Script AI";
        session.SetTitle(title.Text);
        ShareScript.Text = ReportMode ? "Share this report and its displayed results" : "Share this script and its output";
        Composer.AccessibleName = ReportMode ? "Question for the report assistant" : "Question for the script assistant";
        RefreshWorkbooks(); SyncAcceptance(); Render(); session.Show(true); UpdateConnection();
        _ = Transcript.ReopenAsync();
        _ = RefreshAccountAsync();
    }
    private async Task RefreshAccountAsync() { try { if (!ChatGPTTutor.Disabled) await service.Tutor.RefreshAccountAsync(); } catch (Exception ex) { if (!disposed) status.Text = ex.Message; } }
    internal void RefreshWorkbooks()
    {
        if (disposed || chat == null) return;
        var ids = service.Documents.OfType<frmSpreadsheetGear>().Where(g => !g.IsDisposed && !g.Disposing && g.MdiParent != null).Select(g => g.Id()).ToHashSet();
        bool changed = !openWorkbookIds.SetEquals(ids);
        openWorkbookIds = ids;
        if (changed && ShareWorkbook.Checked)
        {
            service.RevokeAISharing();
            status.Text = "The open workbooks changed. Review their data and confirm sharing again.";
            return;
        }
        SyncAcceptance(); UpdateControls();
    }
    internal void SessionSharingChanged() { Stop(); SyncAcceptance(); sharing.Visible = true; UpdateControls(); }
    private void SyncAcceptance() { refreshing = true; Acknowledge.Checked = service.Sharing.Accepts(SharedDataIds); refreshing = false; }
    private void ScopeChanged()
    {
        if (refreshing) return;
        service.ShareAIDocument = ShareScript.Checked; service.ShareAIWorkbook = ShareWorkbook.Checked;
        SyncAcceptance(); sharing.Visible = !Acknowledge.Checked;
        if (chat != null) { chat.ShareScript = ShareScript.Checked; chat.ShareWorkbook = ShareWorkbook.Checked; }
        UpdateControls();
    }
    internal void UpdateConnection()
    {
        if (disposed) return;
        connection.Text = ChatGPTTutor.Disabled ? "AI disabled by policy" : service.Tutor.Email != null ? "ChatGPT connection…" : service.Tutor.LoginUrl != null ? "Finish ChatGPT sign-in…" : "Use my ChatGPT";
        UpdateControls();
    }
    private void UpdateControls()
    {
        bool busy = pending != null, share = ShareScript.Checked || ShareWorkbook.Checked;
        Send.Enabled = !busy && !ChatGPTTutor.Disabled && (!share || Acknowledge.Checked);
        Send.Text = service.Tutor.Email == null ? "Use my ChatGPT" : "Send"; stop.Enabled = busy;
        connection.Enabled = !busy && !ChatGPTTutor.Disabled;
        ShareScript.Enabled = ShareWorkbook.Enabled = Acknowledge.Enabled = !busy;
        SharingOptions.Enabled = !busy;
        SharingOptions.Text = sharing.Visible ? "Sharing for this session ▾" : share ? (Acknowledge.Checked ? "Confirmed: " + (ShareScript.Checked ? (ReportMode ? "report " : "script/output ") : "") + (ShareWorkbook.Checked ? "+ all open workbooks " : "") + "· Review sharing ▸" : "Review sharing ▸") : "Question only · Sharing options ▸";
    }
    private void SaveDraft() { if (chat != null) chat.Draft = Composer.Text; }
    private void Render()
    {
        if (chat == null || disposed) return;
        Transcript.Update(chat.Messages, partial, renderedChat != chat);
        renderedChat = chat;
    }
    /// <summary>Enter in the question box sends, as the ChatGPT prompt does; Shift+Enter adds a line, as in the calculator.  True when the key was taken as Send.</summary>
    internal bool ComposerKey(Keys key)
    {
        if ((key & Keys.KeyCode) != Keys.Enter || (key & Keys.Shift) != 0) return false;
        _ = SendAsync();
        return true;
    }
    internal async Task SendAsync()
    {
        if (pending != null || chat == null || script?.IsDisposed != false) return;
        if (service.Tutor.Email == null)
        {
            // a dropped connection comes back by itself while the sign-in is held; the set-up and sign-in only when it is not
            try { await service.Tutor.RefreshAccountAsync(); } catch (Exception ex) { status.Text = ex.Message; }
            UpdateConnection();
            if (service.Tutor.Email == null) { try { await service.ConfigureTutorAsync(view); } catch (Exception ex) { status.Text = ex.Message; } return; }
        }
        string question = Composer.Text.Trim(); if (question.Length == 0) { status.Text = "Enter a question."; return; }
        RefreshWorkbooks(); SyncAcceptance();
        if ((ShareScript.Checked || ShareWorkbook.Checked) && !Acknowledge.Checked) { status.Text = "Review the selected content and confirm sharing for this session."; return; }
        var approved = new List<StatsDirectForm>();
        if (ShareScript.Checked) approved.Add(script);
        approved.AddRange(SharedWorkbooks);
        using var request = new CancellationTokenSource(); pending = request; var current = chat; bool reportMode = ReportMode;
        var context = new QuestionContext(approved, true, request.Token, service.Sharing);
        sharing.Visible = false; UpdateControls();
        try
        {
            await context.CaptureReportsAsync(focusedResultId);
            string reference = context.InitialContext().ToJsonString();
            if (reference.Length > 28000) throw new InvalidOperationException("The shared context is too large. Close some workbooks or turn off workbook sharing for this question.");
            current.Messages.Add(new JsonObject { ["role"] = "user", ["text"] = question, ["rSource"] = context.RSource }); Composer.Clear(); current.Draft = ""; partial = ""; Render();
            status.Text = approved.Count == 0 ? "Sending the question and conversation only…" : "Sending the confirmed context…";
            var reply = await service.Tutor.ConverseAsync(reference, current.Messages, context.Tools, true, text => { if (!disposed && pending == request && chat == current) { partial = text; Render(); } }, context.CallAsync, request.Token, report: reportMode);
            context.Check(); current.Messages.Add(new JsonObject { ["role"] = "assistant", ["text"] = reply.Text, ["rData"] = context.RData }); partial = "";
            if (chat == current) { Render(); status.Text = "Reply received. Review suggested code before running. Earlier shared content remains in this conversation."; }
        }
        catch (Exception ex)
        {
            if (chat == current && pending == request)
            { if (partial.Length > 0) current.Messages.Add(new JsonObject { ["role"] = "assistant", ["text"] = partial + "\n[Reply stopped before completion.]", ["rData"] = context.RData }); partial = ""; if (Composer.Text.Length == 0) Composer.Text = question; Render(); status.Text = ex is OperationCanceledException ? "Reply stopped. Your question is kept here." : ex.Message; }
        }
        finally { if (pending == request) { pending = null; if (!disposed) { SyncAcceptance(); UpdateControls(); } } }
    }
    internal void Stop() { pending?.Cancel(); }
    private int LastReply()
    { for (int i = (chat?.Messages.Count ?? 0) - 1; i >= 0; i--) if (chat.Messages[i].S("role") == "assistant") return i; return -1; }
    private void OpenCode(int messageIndex, int blockIndex = -1)
    {
        try
        {
            if (chat == null) return;
            string code = ConversationRScript.Build(chat.Messages, messageIndex, blockIndex);
            service.OpenScript(code, "AI suggestion — review before running", false);
        }
        catch (InvalidOperationException ex) { status.Text = ex.Message; }
    }
    private string LastCode()
    { return string.Join("\n\n", ConversationRScript.Blocks(chat?.Messages.LastOrDefault(m => m.S("role") == "assistant").S("text") ?? "")).Limit(100000); }
    internal void Hide() { SaveDraft(); Stop(); session.Hide(); script?.Activate(); }
    internal void NewConversation() { Stop(); if (script == null) return; conversations[script.Id()] = chat = new() { ShareScript = ShareScript.Checked, ShareWorkbook = ShareWorkbook.Checked }; Composer.Clear(); partial = ""; SyncAcceptance(); sharing.Visible = !Acknowledge.Checked; Render(); UpdateControls(); }
    private void ScriptClosed(object sender, FormClosedEventArgs e)
    {
        if (sender != script) return;
        Stop(); conversations.Remove(script.Id()); script.FormClosed -= ScriptClosed;
        script = null; chat = null; session.Hide();
    }
    public void Dispose() { if (disposed) return; disposed = true; Stop(); if (script != null) script.FormClosed -= ScriptClosed; session.Dispose(); view.Dispose(); }
}

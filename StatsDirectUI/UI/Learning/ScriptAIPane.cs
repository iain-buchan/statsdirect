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

/// <summary>A dockable assistant for the active R script. Document access is read-only and renewed for every question.</summary>
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
    internal readonly CheckBox ShareWorkbook = new() { AutoSize = true, Text = "Share the chosen workbook" };
    internal readonly CheckBox Acknowledge = new() { AutoSize = true, Text = "I have checked the selected content contains no person identifiers." };
    internal readonly ComboBox Workbook = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 290 };
    internal readonly Button Send = new() { AutoSize = true, Text = "Send" };
    private readonly Button stop = new() { AutoSize = true, Text = "Stop", Enabled = false }, connection = new() { AutoSize = true, Text = "Use my ChatGPT" };
    internal readonly LinkLabel SharingOptions = new() { AutoSize = true, Text = "Sharing for this question ▾", Dock = DockStyle.Fill };
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, AccessibleName = "AI status" };
    private readonly Dictionary<string, Conversation> conversations = new();
    private frmScript script;
    private Conversation chat;
    private Conversation renderedChat;
    private CancellationTokenSource pending;
    private bool refreshing, disposed;
    private string partial = "";
    internal bool IsOpen => session.IsVisible;
    private sealed class Conversation
    { internal JsonArray Messages = new(); internal string Draft = "", WorkbookId; internal bool ShareScript, ShareWorkbook; }
    private sealed record WorkbookChoice(string Id, string Title) { public override string ToString() => Title; }
    internal ScriptAIPane(frmMain main, LearningService service)
    {
        this.service = service;
        Transcript = new ConversationView(Path.Combine(Configuration.SDConfiguration.InstallationDirectory, "Tutor"), Path.Combine(service.Storage, "WebView")) { Dock = DockStyle.Fill };
        Transcript.CodeRequested += (what, code, messageIndex, blockIndex) => { if (what == "copy") Clipboard.SetText(code); else OpenCode(messageIndex, blockIndex); };
        Transcript.Notice += text => { if (!disposed) status.Text = text; };
        var heading = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        var title = new ToolStripLabel("Script AI"); var pop = new ToolStripButton("Pop out"); var close = new ToolStripButton("Close");
        heading.Items.AddRange([title, new ToolStripButton("New conversation", null, (_, _) => NewConversation()), pop, close]);
        for (int row = 0; row < 10; row++) layout.RowStyles.Add(new RowStyle(row == 8 ? SizeType.Percent : row == 4 ? SizeType.Absolute : SizeType.AutoSize, row == 8 ? 100 : row == 4 ? 100 : 0));
        layout.Controls.Add(connection, 0, 0); layout.Controls.Add(SharingOptions, 0, 1);
        var warning = new Label { AutoSize = true, Text = "Share workbook data or scripts with ChatGPT? Do not share person-identifiable data: remove names, dates of birth, patient IDs and identifying free text." };
        sharing.Controls.AddRange([warning, ShareScript, ShareWorkbook, Workbook, Acknowledge]); layout.Controls.Add(sharing, 0, 2);
        layout.Controls.Add(new Label { AutoSize = true, Text = "Your question", Dock = DockStyle.Fill }, 0, 3);
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
        ShareScript.CheckedChanged += (_, _) => ScopeChanged(); ShareWorkbook.CheckedChanged += (_, _) => ScopeChanged(); Workbook.SelectedIndexChanged += (_, _) => ScopeChanged();
        Acknowledge.CheckedChanged += (_, _) => { if (Acknowledge.Checked && (ShareScript.Checked || ShareWorkbook.Checked)) { sharing.Visible = false; if (status.Text == "Review the selected content and confirm sharing for this question.") status.Text = ""; Composer.Focus(); } UpdateControls(); };
        SharingOptions.LinkClicked += (_, _) => { sharing.Visible = !sharing.Visible; UpdateControls(); };
        Send.Click += async (_, _) => await SendAsync(); stop.Click += (_, _) => Stop();
        copyCode.Click += (_, _) => { string code = LastCode(); if (code.Length > 0) Clipboard.SetText(code); else status.Text = "The last reply contains no R code block."; };
        openCode.Click += (_, _) => OpenCode(LastReply());
        void FitContent()
        {
            int width = Math.Max(120, viewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - layout.Padding.Horizontal - 12);
            warning.MaximumSize = status.MaximumSize = new Size(width, 0); Workbook.Width = width;
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
    internal void Open(frmScript document)
    {
        if (script != document)
        {
            SaveDraft(); Stop();
            if (script != null) script.FormClosed -= ScriptClosed;
            script = document; script.FormClosed += ScriptClosed;
            if (!conversations.TryGetValue(document.Id(), out chat)) conversations[document.Id()] = chat = new Conversation { WorkbookId = service.Documents.OfType<frmSpreadsheetGear>().LastOrDefault()?.Id() };
            refreshing = true; Composer.Text = chat.Draft; ShareScript.Checked = chat.ShareScript; ShareWorkbook.Checked = chat.ShareWorkbook; refreshing = false;
            Acknowledge.Checked = false; sharing.Visible = true; partial = "";
        }
        RefreshWorkbooks(); Render(); session.Show(true); UpdateConnection();
        _ = Transcript.ReopenAsync();
        _ = RefreshAccountAsync();
    }
    private async Task RefreshAccountAsync() { try { if (!ChatGPTTutor.Disabled) await service.Tutor.RefreshAccountAsync(); } catch (Exception ex) { if (!disposed) status.Text = ex.Message; } }
    internal void RefreshWorkbooks()
    {
        if (disposed || chat == null) return;
        var choices = service.Documents.OfType<frmSpreadsheetGear>().Select(d => new WorkbookChoice(d.Id(), d.Text)).ToArray();
        if (choices.SequenceEqual(Workbook.Items.Cast<WorkbookChoice>())) return;
        string id = (Workbook.SelectedItem as WorkbookChoice)?.Id ?? chat.WorkbookId;
        refreshing = true; Workbook.Items.Clear(); Workbook.Items.AddRange(choices);
        var selected = choices.FirstOrDefault(c => c.Id == id); Workbook.SelectedItem = selected;
        if (selected == null) { ShareWorkbook.Checked = false; Acknowledge.Checked = false; chat.ShareWorkbook = false; }
        refreshing = false; UpdateControls();
    }
    private void ScopeChanged()
    {
        if (refreshing) return;
        Acknowledge.Checked = false; sharing.Visible = true;
        if (chat != null) { chat.ShareScript = ShareScript.Checked; chat.ShareWorkbook = ShareWorkbook.Checked; chat.WorkbookId = (Workbook.SelectedItem as WorkbookChoice)?.Id; }
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
        ShareScript.Enabled = ShareWorkbook.Enabled = Workbook.Enabled = Acknowledge.Enabled = !busy;
        SharingOptions.Enabled = !busy;
        SharingOptions.Text = sharing.Visible ? "Sharing for this question ▾" : share ? (Acknowledge.Checked ? "Confirmed: " + (ShareScript.Checked ? "script/output " : "") + (ShareWorkbook.Checked ? "+ workbook " : "") + "· Review sharing ▸" : "Review sharing for the next question ▸") : "Question only · Sharing options ▸";
    }
    private void SaveDraft() { if (chat != null) chat.Draft = Composer.Text; }
    private void Render()
    {
        if (chat == null || disposed) return;
        Transcript.Update(chat.Messages, partial, renderedChat != chat);
        renderedChat = chat;
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
        if ((ShareScript.Checked || ShareWorkbook.Checked) && !Acknowledge.Checked) { status.Text = "Review the selected content and confirm sharing for this question."; return; }
        var approved = new List<StatsDirectForm>();
        if (ShareScript.Checked) approved.Add(script);
        if (ShareWorkbook.Checked)
        {
            var grid = service.Documents.OfType<frmSpreadsheetGear>().FirstOrDefault(d => d.Id() == (Workbook.SelectedItem as WorkbookChoice)?.Id);
            if (grid == null) { status.Text = "Choose the workbook again and confirm sharing."; ScopeChanged(); return; }
            approved.Add(grid);
        }
        using var request = new CancellationTokenSource(); pending = request; var current = chat;
        var context = new QuestionContext(approved, true, request.Token);
        Acknowledge.Checked = false; sharing.Visible = false; UpdateControls();
        try
        {
            string reference = context.InitialContext().ToJsonString();
            if (reference.Length > 28000) throw new InvalidOperationException("The selected context is too large. Choose less content for this question.");
            current.Messages.Add(new JsonObject { ["role"] = "user", ["text"] = question, ["rSource"] = context.RSource }); Composer.Clear(); current.Draft = ""; partial = ""; Render();
            status.Text = approved.Count == 0 ? "Sending the question and conversation only…" : "Sending the confirmed context…";
            var reply = await service.Tutor.ConverseAsync(reference, current.Messages, context.Tools, true, text => { if (!disposed && pending == request && chat == current) { partial = text; Render(); } }, context.CallAsync, request.Token);
            context.Check(); current.Messages.Add(new JsonObject { ["role"] = "assistant", ["text"] = reply.Text, ["rData"] = context.RData }); partial = "";
            if (chat == current) { Render(); status.Text = "Reply received. Review suggested code before running. Earlier shared content remains in this conversation."; }
        }
        catch (Exception ex)
        {
            if (chat == current && pending == request)
            { if (partial.Length > 0) current.Messages.Add(new JsonObject { ["role"] = "assistant", ["text"] = partial + "\n[Reply stopped before completion.]", ["rData"] = context.RData }); partial = ""; if (Composer.Text.Length == 0) Composer.Text = question; Render(); status.Text = ex is OperationCanceledException ? "Reply stopped. Your question is kept here." : ex.Message; }
        }
        finally { if (pending == request) { pending = null; if (!disposed) { Acknowledge.Checked = false; UpdateControls(); } } }
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
    internal void NewConversation() { Stop(); if (script == null) return; conversations[script.Id()] = chat = new(); Composer.Clear(); partial = ""; ShareScript.Checked = ShareWorkbook.Checked = Acknowledge.Checked = false; sharing.Visible = true; Render(); UpdateControls(); }
    private void ScriptClosed(object sender, FormClosedEventArgs e)
    {
        if (sender != script) return;
        Stop(); conversations.Remove(script.Id()); script.FormClosed -= ScriptClosed;
        script = null; chat = null; session.Hide();
    }
    public void Dispose() { if (disposed) return; disposed = true; Stop(); if (script != null) script.FormClosed -= ScriptClosed; session.Dispose(); view.Dispose(); }
}

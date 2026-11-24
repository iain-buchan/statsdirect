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

/// <summary>A dockable assistant with a fixed document scope and locally reviewed worksheet proposals.</summary>
internal sealed partial class ScriptAIPane : IDisposable
{
    private const string WorksheetOpeningQuestion = "Do you want me to help you analyse your data?";
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
    internal readonly CheckBox Acknowledge = new() { AutoSize = true };
    internal readonly Button Send = new() { AutoSize = true, Text = "Send" };
    private readonly Button stop = new() { AutoSize = true, Text = "Stop", Enabled = false }, connection = new() { AutoSize = true, Text = "Use my ChatGPT" };
    internal readonly LinkLabel SharingOptions = new() { AutoSize = true, Text = "Sharing for this session ▾", Dock = DockStyle.Fill };
    private readonly Label status = new() { Name = "aiStatus", AutoSize = true, Dock = DockStyle.Fill, AccessibleName = "AI status" };
    private readonly Label conversationCaption = new() { Name = "conversationCaption", AutoSize = true, Text = "Conversation", Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel replyCodeActions = new() { Name = "replyRCodeActions", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Label warning = new() { AutoSize = true };
    private readonly Label questionCaption = new() { AutoSize = true, Dock = DockStyle.Fill };
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
    { internal JsonArray Messages = new(); internal string Draft = ""; internal bool ShareScript, ShareWorkbook, WorksheetHistoryShared; }
    internal frmSpreadsheetGear[] SharedWorkbooks => ShareWorkbook.Checked && !service.Tutor.DataSharingDisabled
        ? service.Documents.OfType<frmSpreadsheetGear>().Where(g => (!WorksheetMode || g == script) && !g.IsDisposed && !g.Disposing && g.MdiParent != null).ToArray()
        : Array.Empty<frmSpreadsheetGear>();
    private StatsDirectForm[] SharedDocuments
    {
        get
        {
            var workbooks = SharedWorkbooks;
            return !WorksheetMode && ShareScript.Checked && !service.Tutor.DataSharingDisabled
                && script is { IsDisposed: false, Disposing: false, MdiParent: not null }
                ? [script, .. workbooks] : workbooks;
        }
    }
    private bool ReportMode => script is frmReportWebView;
    internal bool WorksheetMode => script is frmSpreadsheetGear;
    private string WorksheetName => string.IsNullOrWhiteSpace(script?.Text) ? "Worksheet" : script.Text.Trim().Limit(32);
    internal ScriptAIPane(frmMain main, LearningService service)
    {
        this.service = service;
        openWorkbookIds = service.Documents.OfType<frmSpreadsheetGear>().Select(g => g.Id()).ToHashSet();
        Transcript = new ConversationView(Path.Combine(Configuration.SDConfiguration.InstallationDirectory, "Tutor"), Path.Combine(service.Storage, "WebView")) { Dock = DockStyle.Fill };
        Transcript.CodeRequested += (what, code, messageIndex, blockIndex) => { if (what == "copy") Clipboard.SetText(code); else OpenCode(messageIndex, blockIndex); };
        Transcript.Notice += text => { if (!disposed) status.Text = text; };
        Transcript.WorksheetActionRequested += ChooseWorksheetAction;
        var heading = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        var pop = new ToolStripButton("Pop out"); var close = new ToolStripButton("Close");
        heading.Items.AddRange([title, new ToolStripButton("New conversation", null, (_, _) => NewConversation()), pop, close]);
        for (int row = 0; row < 10; row++) layout.RowStyles.Add(new RowStyle(row == 8 ? SizeType.Percent : row == 4 ? SizeType.Absolute : SizeType.AutoSize, row == 8 ? 100 : row == 4 ? 100 : 0));
        layout.Controls.Add(connection, 0, 0); layout.Controls.Add(SharingOptions, 0, 1);
        sharing.Controls.AddRange([warning, ShareScript, ShareWorkbook, Acknowledge]); layout.Controls.Add(sharing, 0, 2);
        layout.Controls.Add(questionCaption, 0, 3);
        layout.Controls.Add(Composer, 0, 4);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var copyCode = new Button { AutoSize = true, Text = "Copy R code" }; var openCode = new Button { AutoSize = true, Text = "Open R code" };
        actions.Controls.AddRange([Send, stop]); layout.Controls.Add(actions, 0, 5); layout.Controls.Add(status, 0, 6);
        layout.Controls.Add(conversationCaption, 0, 7);
        Transcript.AccessibleName = "AI conversation"; layout.Controls.Add(Transcript, 0, 8);
        replyCodeActions.Controls.AddRange([copyCode, openCode]); layout.Controls.Add(replyCodeActions, 0, 9);
        viewport.Controls.Add(layout); view.Controls.Add(viewport); view.Controls.Add(heading);
        session = new ToolPaneSession(main, view, "Script AI", DockStyle.Right, 440, new Size(500, 760),
            (floating, canDock) => { pop.Text = floating ? "Dock" : "Pop out"; pop.Enabled = !floating || canDock; }, () => Composer.Focus(), text => status.Text = text, minimumDockLength: 330);
        pop.Click += async (_, _) => await session.ToggleModeAsync(); close.Click += (_, _) => Hide();
        connection.Click += async (_, _) => { try { await service.ConfigureTutorAsync(view); } catch (Exception ex) { status.Text = ex.Message; } };
        ShareScript.CheckedChanged += (_, _) => ScopeChanged(); ShareWorkbook.CheckedChanged += (_, _) => ScopeChanged();
        Acknowledge.CheckedChanged += (_, _) => {
            if (refreshing || WorksheetMode) return;
            var documents = SharedDocuments;
            if (documents.Length == 0)
            {
                SyncAcceptance();
                return;
            }
            if (Acknowledge.Checked) {
                service.Sharing.Approve(documents.Select(d => d.Id())); sharing.Visible = false;
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
            warning.MaximumSize = status.MaximumSize = questionCaption.MaximumSize = SharingOptions.MaximumSize = new Size(width, 0);
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
        foreach (var box in new[] { ShareScript, ShareWorkbook, Acknowledge }) box.TextChanged += (_, _) => FitContent();
        FitContent();
    }
    internal void Open(StatsDirectForm document, string resultId = null)
    {
        if (script != document)
        {
            SaveDraft(); Stop(); ClearWorksheetAnalysis(); status.Text = "";
            if (script != null) { script.FormClosed -= ScriptClosed; script.TextChanged -= WorksheetTitleChanged; }
            script = document; script.FormClosed += ScriptClosed;
            if (WorksheetMode) script.TextChanged += WorksheetTitleChanged;
            if (!conversations.TryGetValue(document.Id(), out chat))
                conversations[document.Id()] = chat = new Conversation { ShareScript = !WorksheetMode && service.ShareAIDocument, ShareWorkbook = !WorksheetMode && service.ShareAIWorkbook };
            if (WorksheetMode && !service.Sharing.Accepts([document.Id()])) chat.ShareWorkbook = false;
            refreshing = true; Composer.Text = chat.Draft; ShareScript.Checked = chat.ShareScript; ShareWorkbook.Checked = chat.ShareWorkbook; refreshing = false;
            if (!WorksheetMode) { service.ShareAIDocument = ShareScript.Checked; service.ShareAIWorkbook = ShareWorkbook.Checked; }
            sharing.Visible = !WorksheetMode; partial = "";
        }
        focusedResultId = resultId;
        UpdateScopeTitle();
        ShareScript.Visible = !WorksheetMode;
        conversationCaption.Visible = !WorksheetMode;
        ShareScript.Text = ReportMode ? "Share this report and its displayed results" : "Share this script and its output";
        warning.Text = "Workbook sharing includes every open workbook. Do not share person-identifiable data: remove names, dates of birth, patient IDs and identifying free text.";
        Acknowledge.Text = "I have checked the documents selected above contain no person identifiers. Remember this confirmation for those documents during this session.";
        questionCaption.Text = WorksheetMode ? "Ask a question about your data" : "Your question (Enter sends; Shift+Enter adds a line)";
        Composer.AccessibleName = WorksheetMode ? questionCaption.Text : ReportMode ? "Question for the report assistant" : "Question for the script assistant";
        RefreshWorkbooks(); SyncAcceptance(); Render(); session.Show(true); UpdateConnection();
        _ = Transcript.ReopenAsync();
        _ = RefreshAccountAsync();
    }
    private void UpdateScopeTitle()
    {
        title.Text = WorksheetMode ? "Worksheet AI — " + WorksheetName.Limit(24) : ReportMode ? "Report AI" : "Script AI";
        session.SetTitle(WorksheetMode ? "Worksheet AI — " + WorksheetName : title.Text);
        ShareWorkbook.Text = WorksheetMode ? "Share workbook data with AI (“" + WorksheetName + "”)" : "Share open workbook data";
    }

    private void WorksheetTitleChanged(object sender, EventArgs e) { UpdateScopeTitle(); UpdateControls(); }

    private async Task RefreshAccountAsync() { try { if (!service.Tutor.Disabled) await service.Tutor.RefreshAccountAsync(); } catch (Exception ex) { if (!disposed) status.Text = ex.Message; } }
    internal void RefreshWorkbooks()
    {
        if (disposed || chat == null) return;
        var ids = service.Documents.OfType<frmSpreadsheetGear>().Where(g => !g.IsDisposed && !g.Disposing && g.MdiParent != null).Select(g => g.Id()).ToHashSet();
        bool changed = !openWorkbookIds.SetEquals(ids);
        openWorkbookIds = ids;
        if (changed && !WorksheetMode && ShareWorkbook.Checked && !service.Tutor.DataSharingDisabled)
        {
            service.RevokeAISharing();
            status.Text = "The open workbooks changed. Review their data and confirm sharing again.";
            return;
        }
        SyncAcceptance(); UpdateControls();
    }
    internal void SessionSharingChanged()
    {
        Stop();
        if (ClearWorksheetAnalysis()) Render();
        if (WorksheetMode) TurnOffWorksheetSharing();
        SyncAcceptance();
        sharing.Visible = !WorksheetMode;
        UpdateControls();
    }

    private void TurnOffWorksheetSharing()
    {
        refreshing = true;
        ShareWorkbook.Checked = false;
        refreshing = false;
        if (chat != null) chat.ShareWorkbook = false;
    }
    private void SyncAcceptance()
    {
        refreshing = true;
        Acknowledge.Checked = service.Sharing.Accepts(SharedDocuments.Select(d => d.Id()));
        refreshing = false;
    }
    private void ScopeChanged()
    {
        if (refreshing || service.Tutor.DataSharingDisabled) return;
        if (WorksheetMode)
        {
            Stop();
            if (ClearWorksheetAnalysis()) Render();
            if (SharedWorkbooks is [var workbook] && !service.Sharing.Accepts([workbook.Id()]))
            {
                var conversation = chat;
                UpdateControls();
                bool approved = service.ConfirmSharing is { } confirm
                    ? confirm([workbook]) == LearningService.SharingDecision.Share
                    : session.ShowOwnedModal(owner => MessageBox.Show(owner,
                        "AI can read “" + workbook.Text + "” when answering your questions. Confirm that the workbook contains no person identifiers before sharing. This confirmation lasts for the current StatsDirect session.",
                        "Share workbook data with AI?", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
                if (disposed || chat != conversation || script != workbook) return;
                if (approved && !service.Tutor.DataSharingDisabled && SharedWorkbooks.Contains(workbook))
                    service.Sharing.Approve([workbook.Id()]);
                else
                    TurnOffWorksheetSharing();
            }
        }
        else
        {
            service.ShareAIDocument = ShareScript.Checked;
            service.ShareAIWorkbook = ShareWorkbook.Checked;
        }
        SyncAcceptance();
        if (!WorksheetMode) sharing.Visible = !Acknowledge.Checked;
        if (chat != null) { chat.ShareScript = ShareScript.Checked; chat.ShareWorkbook = ShareWorkbook.Checked; }
        UpdateControls();
    }
    internal void UpdateConnection()
    {
        if (disposed) return;
        SyncAcceptance();
        UpdateControls();
    }
    private void UpdateControls()
    {
        var securityLevel = service.Tutor.SecurityLevel;
        bool busy = pending != null, disabled = securityLevel == AISecurityLevel.AIRestricted, sharingRestricted = securityLevel != AISecurityLevel.Unrestricted;
        var documents = SharedDocuments;
        bool share = !sharingRestricted && documents.Length > 0;
        Send.Enabled = !busy && !disabled && (!share || service.Sharing.Accepts(documents.Select(d => d.Id())));
        Send.Text = !disabled && service.Tutor.Email == null ? "Use my ChatGPT" : "Send"; stop.Enabled = busy;
        connection.Text = disabled ? ChatGPTTutor.RestrictedLabel : service.Tutor.Email != null ? "ChatGPT connection…" : service.Tutor.LoginUrl != null ? "Finish ChatGPT sign-in…" : "Use my ChatGPT";
        connection.Enabled = !busy && !disabled;
        ShareWorkbook.Enabled = !busy && !sharingRestricted;
        Acknowledge.Enabled = !WorksheetMode && !busy && share;
        ShareScript.Enabled = !WorksheetMode && ShareWorkbook.Enabled;
        if (disabled && (ClearWorksheetAnalysis() || WorksheetMode)) Render();
        replyCodeActions.Visible = !WorksheetMode;
        SharingOptions.Enabled = !busy && !sharingRestricted;
        SharingOptions.Visible = !WorksheetMode || sharingRestricted;
        warning.Visible = Acknowledge.Visible = !WorksheetMode;
        if (WorksheetMode || sharingRestricted) sharing.Visible = !sharingRestricted;
        if (disabled) SharingOptions.Text = ChatGPTTutor.PolicyMessage;
        else if (sharingRestricted) SharingOptions.Text = ChatGPTTutor.DataSharingPolicyMessage;
        else if (!WorksheetMode)
            SharingOptions.Text = sharing.Visible ? "Sharing for this session ▾" : share
                ? Acknowledge.Checked ? "Confirmed: " + (ShareScript.Checked ? (ReportMode ? "report " : "script/output ") : "") + (ShareWorkbook.Checked ? "+ all open workbooks " : "") + "· Review sharing ▸" : "Review sharing ▸"
                : "Question only · Sharing options ▸";
    }
    private void SaveDraft() { if (chat != null) chat.Draft = Composer.Text; }
    private void Render()
    {
        if (chat == null || disposed) return;
        Transcript.Update(chat.Messages, partial, renderedChat != chat, collapseRCode: WorksheetMode,
            welcome: WorksheetMode && !service.Tutor.Disabled ? WorksheetOpeningQuestion : "",
            worksheetAction: VisibleWorksheetAction?.Display());
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
        if (service.Tutor.Disabled) { status.Text = ChatGPTTutor.PolicyMessage; UpdateControls(); return; }
        var origin = script;
        var current = chat;
        if (service.Tutor.Email == null)
        {
            // a dropped connection comes back by itself while the sign-in is held; the set-up and sign-in only when it is not
            try { await service.Tutor.RefreshAccountAsync(); } catch (Exception ex) { status.Text = ex.Message; }
            if (disposed || origin != script || current != chat || pending != null || !IsOpen) return;
            UpdateConnection();
            if (service.Tutor.Email == null) { try { await service.ConfigureTutorAsync(view); } catch (Exception ex) { status.Text = ex.Message; } return; }
        }
        string question = Composer.Text.Trim(); if (question.Length == 0) { status.Text = "Enter a question."; return; }
        RefreshWorkbooks(); SyncAcceptance();
        bool guidanceOnly = service.Tutor.DataSharingDisabled;
        bool worksheetMode = WorksheetMode;
        StatsDirectForm[] approved = guidanceOnly ? [] : SharedDocuments;
        if (approved.Length > 0 && !service.Sharing.Accepts(approved.Select(d => d.Id())))
        {
            // the reason the confirmation lapsed, when the open workbooks changed, stands; the general reminder otherwise
            if (status.Text != "The open workbooks changed. Review their data and confirm sharing again.") status.Text = "Review the selected content and confirm sharing for this session.";
            return;
        }
        using var request = new CancellationTokenSource(); pending = request;
        bool reportMode = ReportMode;
        ClearWorksheetAnalysis();
        QuestionContext context = null;
        if (!worksheetMode) sharing.Visible = false;
        UpdateControls();
        try
        {
            context = new QuestionContext(approved, true, request.Token, service.Sharing, service.Tutor, guidanceOnly);
            await context.CaptureReportsAsync(focusedResultId);
            var initial = guidanceOnly ? new JsonObject() : context.InitialContext();
            var tools = context.Tools;
            WorksheetAnalysisTarget analysisTarget = null;
            WorksheetCommand[] analysisCommands = [];
            WorksheetAction stagedAnalysis = null;
            bool worksheetShared = worksheetMode && approved.Contains(origin);
            string helpDirectory = Configuration.SDConfiguration.HelpDirectory;
            if (worksheetMode)
            {
                if (!current.Messages.Any(message => message.S("role") == "assistant"))
                {
                    initial["openingQuestion"] = WorksheetOpeningQuestion;
                    initial["methodSelection"] = WorksheetAIHelp.MethodSelectionGuide();
                }
                tools.Add(QuestionContext.Tool(WorksheetAIHelp.SearchTool,
                    "Search the full packaged StatsDirect Help using keywords. Returns excerpts, links and topic IDs.",
                    new() { ["query"] = new JsonObject { ["type"] = "string", ["maxLength"] = 200 } }, "query"));
                tools.Add(QuestionContext.Tool(WorksheetAIHelp.ReadTopicTool,
                    "Read a full packaged StatsDirect Help topic by its topic ID from search. If nextOffset is present, use it to continue reading.",
                    new()
                    {
                        ["topic"] = new JsonObject { ["type"] = "string", ["maxLength"] = 200 },
                        ["offset"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 }
                    }, "topic"));
                var grid = (frmSpreadsheetGear)origin;
                if (grid.MdiParent is frmMain main && ReferenceEquals(main.ActiveMdiChild, grid)
                    && !main.InOperation && !main.LearningCalculationRunning)
                {
                    try { analysisTarget = grid.CaptureWorksheetAnalysisTarget(worksheetShared); }
                    catch (InvalidOperationException) { /* The question can proceed without a local analysis target. */ }
                    if (analysisTarget != null)
                    {
                        analysisCommands = main.WorksheetCommands();
                        tools.Add(QuestionContext.Tool(ChatGPTTutor.WorksheetMethodSearchTool,
                            "Find native methods in the Analyse picker by name, menu path or operation. Returns up to eight public method descriptions and IDs. It does not inspect the worksheet.",
                            new() { ["query"] = new JsonObject { ["type"] = "string", ["maxLength"] = 120 } }, "query"));
                        tools.Add(WorksheetAnalysisToolDefinition());
                        tools.Add(QuestionContext.Tool(ChatGPTTutor.WorksheetTTestChoiceTool,
                            "Ask whether selected measurements are matched or independent using clickable choices in chat. Use for a comparison of quantitative means when pairing is the remaining question. Does not share data or start an analysis.", new JsonObject()));
                    }
                }
            }
            string reference = guidanceOnly && !worksheetMode ? "" : initial.ToJsonString();
            if (reference.Length > 28000) throw new InvalidOperationException("The shared context is too large. Close some workbooks or turn off workbook sharing for this question.");
            current.Messages.Add(new JsonObject { ["role"] = "user", ["text"] = question, ["rSource"] = context.RSource }); Composer.Clear(); current.Draft = ""; partial = ""; Render();
            status.Text = guidanceOnly ? "Sending your question…" : approved.Length == 0 ? "Sending the question and conversation only…" : "Sending the confirmed context…";

            Task<JsonNode> CallTool(string name, JsonObject args)
            {
                if (worksheetMode && name == WorksheetAIHelp.SearchTool)
                {
                    context.Check();
                    if (args.Count != 1 || args["query"] is not JsonValue queryValue || !queryValue.TryGetValue<string>(out var query) || query.Length is < 1 or > 200)
                        throw new ArgumentException("Provide one search query of up to 200 characters.");
                    return Task.FromResult<JsonNode>(new JsonObject { ["topics"] = WorksheetAIHelp.Search(helpDirectory, query) });
                }
                if (worksheetMode && name == WorksheetAIHelp.ReadTopicTool)
                {
                    context.Check();
                    if (args.Any(pair => pair.Key is not ("topic" or "offset")) || args["topic"] is not JsonValue topicValue
                        || !topicValue.TryGetValue<string>(out var topic) || topic.Length is < 1 or > 200)
                        throw new ArgumentException("Choose a topic ID returned by Help search.");
                    int offset = 0;
                    if (args.TryGetPropertyValue("offset", out var offsetNode)
                        && (offsetNode is not JsonValue value || !value.TryGetValue(out offset) || offset < 0))
                        throw new ArgumentException("Use a non-negative Help text offset.");
                    return Task.FromResult<JsonNode>(WorksheetAIHelp.ReadTopic(helpDirectory, topic, offset));
                }
                if (worksheetMode && name == ChatGPTTutor.WorksheetMethodSearchTool)
                {
                    context.Check();
                    if (analysisTarget == null || args.Count != 1 || args["query"] is not JsonValue queryValue
                        || !queryValue.TryGetValue<string>(out var query) || query.Length is < 1 or > 120 || string.IsNullOrWhiteSpace(query))
                        throw new ArgumentException("Provide one method name or search term of up to 120 characters.");
                    var matches = WorksheetCommandPicker.Search(analysisCommands, query).Where(command => command.CanRun).Take(8);
                    return Task.FromResult<JsonNode>(new JsonObject
                    {
                        ["methods"] = new JsonArray(matches.Select(command => (JsonNode)new JsonObject
                        {
                            ["id"] = command.Id, ["name"] = command.Name, ["menuPath"] = command.MenuPath, ["operation"] = command.Operation
                        }).ToArray())
                    });
                }
                if (worksheetMode && name == ChatGPTTutor.WorksheetTTestChoiceTool)
                {
                    context.Check();
                    if (analysisTarget == null || service.Tutor.Disabled || args.Count != 0)
                        throw new InvalidOperationException("Choose an active worksheet before offering a study-design choice.");
                    if (stagedAnalysis != null) throw new InvalidOperationException("A worksheet action is already waiting.");
                    analysisTarget.Grid.ResolveWorksheetAnalysisTarget(analysisTarget, allowSelectionChanges: false);
                    stagedAnalysis = new WorksheetAction("choose", analysisTarget);
                    return Task.FromResult<JsonNode>(new JsonObject
                    {
                        ["state"] = "awaiting_design",
                        ["message"] = "Ask the user to choose “Same people or units” or “Separate groups” in the chat."
                    });
                }
                if (worksheetMode && name == ChatGPTTutor.WorksheetAnalysisTool)
                {
                    context.Check();
                    if (analysisTarget == null || service.Tutor.Disabled)
                        throw new InvalidOperationException("Choose an active worksheet before asking for a local analysis proposal.");
                    if (stagedAnalysis != null) throw new InvalidOperationException("An analysis is already waiting for the user to review.");
                    if (args.Count != 1 || args["methodId"] is not JsonValue idValue
                        || !idValue.TryGetValue<string>(out var methodId) || methodId.Length is < 1 or > 600)
                        throw new ArgumentException("Choose one method ID returned by the Analyse search.");
                    var method = analysisCommands.FirstOrDefault(command => command.Id == methodId && command.CanRun);
                    if (method == null)
                        throw new ArgumentException("Find a method in the Analyse picker before proposing it.");
                    try { stagedAnalysis = ConfirmWorksheetAnalysis(analysisTarget, method); }
                    catch (InvalidOperationException)
                    {
                        // Keep errors about the captured workbook or selection on the user's side of the connection.
                        throw new InvalidOperationException("Review the worksheet in StatsDirect and send the question again.");
                    }
                    return Task.FromResult<JsonNode>(new JsonObject
                    {
                        ["state"] = "pending_review",
                        ["operation"] = method.Operation,
                        ["method"] = stagedAnalysis.Proposal.MethodName,
                        ["message"] = "The proposal is shown in this chat. Ask the user to check it and choose Continue to analysis. Nothing has run."
                    });
                }
                return context.CallAsync(name, args);
            }

            // Turning off workbook sharing also excludes earlier replies that may quote its cells.
            var outgoing = worksheetMode && !worksheetShared && current.WorksheetHistoryShared
                ? new JsonArray(current.Messages[^1].DeepClone()) : current.Messages;
            current.WorksheetHistoryShared |= worksheetShared;
            var reply = await service.Tutor.ConverseAsync(reference, outgoing, tools, true, text => { if (!disposed && pending == request && chat == current) { partial = text; Render(); } }, CallTool, request.Token, report: reportMode, guidanceOnly: guidanceOnly, worksheet: worksheetMode);
            context.Check(); current.Messages.Add(new JsonObject { ["role"] = "assistant", ["text"] = reply.Text, ["rData"] = context.RData }); partial = "";
            if (chat == current && script == origin && IsOpen)
            {
                worksheetAction = stagedAnalysis;
                Render();
                status.Text = worksheetMode
                    ? ""
                    : guidanceOnly ? "Reply received. Review suggested code before running." : "Reply received. Review suggested code before running. Earlier shared content remains in this conversation.";
            }
        }
        catch (Exception ex)
        {
            if (chat == current && pending == request)
            { if (partial.Length > 0) current.Messages.Add(new JsonObject { ["role"] = "assistant", ["text"] = partial + "\n[Reply stopped before completion.]", ["rData"] = context?.RData }); partial = ""; if (Composer.Text.Length == 0) Composer.Text = question; Render(); status.Text = ex is OperationCanceledException ? "Reply stopped. Your question is kept here." : ex.Message; }
        }
        finally { if (pending == request) { pending = null; if (!disposed) { SyncAcceptance(); UpdateControls(); if (WorksheetMode) Render(); } } }
    }
    private static JsonObject WorksheetAnalysisToolDefinition() => QuestionContext.Tool(ChatGPTTutor.WorksheetAnalysisTool,
        "Stage one requested native method using an exact id from statsdirect_find_worksheet_methods. Use after any clarifications needed to choose the method, or when the user explicitly requests the method by name. It posts a local confirmation with the current worksheet selection. Staging does not run the method or return data or results.",
        new() { ["methodId"] = new JsonObject { ["type"] = "string", ["maxLength"] = 600 } }, "methodId");

    internal void Stop() { pending?.Cancel(); }
    private int LastReply()
    { for (int i = (chat?.Messages.Count ?? 0) - 1; i >= 0; i--) if (chat.Messages[i].S("role") == "assistant") return i; return -1; }
    private void OpenCode(int messageIndex, int blockIndex = -1)
    {
        try
        {
            if (chat == null) return;
            string code = ConversationRScript.Build(chat.Messages, messageIndex, blockIndex, currentReplyOnly: WorksheetMode);
            service.OpenScript(code, "AI suggestion — review before running", false, reportOutput: WorksheetMode);
        }
        catch (InvalidOperationException ex) { status.Text = ex.Message; }
    }
    private string LastCode()
    { return string.Join("\n\n", ConversationRScript.Blocks(chat?.Messages.LastOrDefault(m => m.S("role") == "assistant").S("text") ?? "")).Limit(100000); }
    internal void Hide() { SaveDraft(); Stop(); ClearWorksheetAnalysis(); session.Hide(); script?.Activate(); }
    internal void NewConversation()
    {
        Stop(); ClearWorksheetAnalysis();
        if (script == null) return;
        conversations[script.Id()] = chat = new() { ShareScript = !WorksheetMode && ShareScript.Checked, ShareWorkbook = !WorksheetMode && ShareWorkbook.Checked };
        refreshing = true;
        Composer.Clear(); ShareScript.Checked = chat.ShareScript; ShareWorkbook.Checked = chat.ShareWorkbook;
        refreshing = false;
        partial = ""; SyncAcceptance(); sharing.Visible = !WorksheetMode && !Acknowledge.Checked; Render(); UpdateControls();
    }
    private void ScriptClosed(object sender, FormClosedEventArgs e)
    {
        if (sender != script) return;
        Stop(); ClearWorksheetAnalysis(); conversations.Remove(script.Id()); script.FormClosed -= ScriptClosed; script.TextChanged -= WorksheetTitleChanged;
        script = null; chat = null; session.Hide();
    }
    public void Dispose() { if (disposed) return; disposed = true; Stop(); if (script != null) { script.FormClosed -= ScriptClosed; script.TextChanged -= WorksheetTitleChanged; } session.Dispose(); view.Dispose(); }
}

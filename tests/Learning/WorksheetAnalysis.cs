using SpreadsheetGear.Windows.Forms;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private const string WorksheetOpeningText = "Do you want me to help you analyse your data?";
    private const string PrivateWorksheet = "TRE_PRIVATE_ANALYSIS_WORKBOOK";
    private const string PairedSelection = "A1:B6";
    private const string InitialSelection = "A1:A6";
    private const string PrivateCell = "TRE_PRIVATE_ANALYSIS_CELL";

    private static frmSpreadsheetGear PairedWorksheet(frmMain main, string title, bool privateFixture)
    {
        var grid = (frmSpreadsheetGear)main.CreateGrid(title);
        grid.LoadLearningExample(JsonNode.Parse("""
            [{"title":"Before","values":[11,14,17,22,23]},
             {"title":"After","values":[10,11,15,20,19]}]
            """)!.AsArray());
        var view = (WorkbookView)Field(grid, "workbookView");
        WithWorksheet(view, sheet =>
        {
            if (privateFixture) sheet.Cells["C1"].Value = PrivateCell;
            view.RangeSelection = sheet.Cells[PairedSelection];
            return true;
        });
        return grid;
    }

    private static async Task RunWorksheetAnalysisFixture(frmMain main)
    {
        string trace = Path.Combine(root, "worksheet-analysis-wire.jsonl");
        using var tutor = WorksheetMockTutor(Path.Combine(root, "worksheet-analysis-account"), () => AISecurityLevel.DataSharingRestricted, trace);
        using var service = new LearningService(main, Path.Combine(root, "Worksheet Analysis Learning"), tutor);
        typeof(frmMain).GetField("learning", Members)!.SetValue(main, service);
        var otherWorkbook = (frmSpreadsheetGear)main.CreateGrid("Other test workbook");
        var grid = PairedWorksheet(main, PrivateWorksheet, privateFixture: true);
        var prefs = SdApplication.SoleInstance.Preferences;
        bool originalDefault = prefs.CanDefaultConfidenceInterval;
        double originalLevel = prefs.DefaultConfidenceInterval;
        prefs.CanDefaultConfidenceInterval = true;
        prefs.DefaultConfidenceInterval = .95;
        ScriptAIPane pane = null;
        try
        {
            await tutor.SignInAsync();
            await tutor.RefreshAccountAsync();
            await Wait(() => tutor.Email == "mock@example.invalid", "the restricted worksheet assistant uses the local mock account");
            pane = await OpenWorksheetAssistant(main, service, grid);
            Check(tutor.DataSharingDisabled && !pane.ShareWorkbook.Enabled && !pane.Acknowledge.Visible && pane.Send.Enabled,
                "level 2 locks worksheet sharing and hides confirmation while leaving Send available");

            var review = FindWorksheetAnalysisReview(pane);
            var paneView = (Control)Field(pane, "view");
            var reviewActions = paneView.Controls.Find("worksheetReplyActions", true).Single();
            var aiStatus = paneView.Controls.Find("aiStatus", true).Single();
            var viewport = (Control)Field(pane, "viewport");
            Check(!reviewActions.Visible, "worksheet reply actions begin hidden");
            var workbook = (WorkbookView)Field(grid, "workbookView");
            void ChooseRange(string range) => WithWorksheet(workbook, sheet =>
            {
                workbook.RangeSelection = sheet.Cells[range];
                return true;
            });
            ChooseRange(InitialSelection);
            const string initialQuestion = "TRE_WORKSHEET_ANALYSIS_NEW_REPORT";
            var response = await WorksheetReply(pane, initialQuestion);
            var tools = response.A("tools").Select(tool => tool.GetValue<string>()).ToHashSet();
            Check(tools.SetEquals(["statsdirect_method_help", "statsdirect_search_help", "statsdirect_read_help_topic", "statsdirect_propose_worksheet_analysis"])
                && response.S("help").Contains("paired", StringComparison.OrdinalIgnoreCase),
                "level 2 exposes bundled help and the worksheet method proposal only");
            Check(!response.B("workspace") && !response.B("read") && !response.B("calculation") && !response.B("invalid"),
                "the restricted transport refuses workbook scope, cell reads, direct calculations and AI-selected columns");
            var receipt = response["pending"] as JsonObject;
            Check(receipt?.S("state") == "pending_review" && receipt.Count == 2 && receipt.ContainsKey("message"),
                "the model receives only a generic proposal receipt");
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any() && review.Visible && review.Enabled,
                "a method proposal neither runs a calculation nor creates a report");
            await Task.Yield();
            Check(reviewActions.Visible && ReferenceEquals(review.Parent, reviewActions)
                && aiStatus.Parent is TableLayoutPanel layout && ReferenceEquals(layout, reviewActions.Parent)
                && layout.GetRow(reviewActions) == layout.GetRow(aiStatus) + 1
                && viewport.RectangleToScreen(viewport.ClientRectangle).Contains(review.RectangleToScreen(review.ClientRectangle)),
                "the plain reply puts Review analysis next to its status without scrolling");
            CheckWorksheetAnalysisQuestion(trace, initialQuestion, openingQuestion: true);
            string[] beforeApproval = WorksheetAnalysisDataTraffic(trace);
            var stagedMethod = Field(pane, "worksheetAnalysis");
            otherWorkbook.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, otherWorkbook), "the user switches to another workbook with an analysis staged");
            grid.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, grid), "the user returns to the original worksheet");
            Check(stagedMethod != null && ReferenceEquals(Field(pane, "worksheetAnalysis"), stagedMethod) && review.Visible && review.Enabled,
                "the reviewed worksheet method survives visiting another workbook");
            ReviewWorksheetAnalysis(main, review, approve: false, InitialSelection, "A new report");
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any() && review.Visible && review.Enabled,
                "cancelling review leaves the workbook and reports alone and retains the proposed method");

            // The workbook remains private. A different range can be chosen and used in the same local proposal.
            ChooseRange(PairedSelection);
            ReviewWorksheetAnalysis(main, review, approve: true, PairedSelection, "A new report");
            await Wait(() => main.MdiChildren.OfType<frmReportWebView>().Count() == 1,
                "approving the reopened method review creates a report when none was open");
            var chosen = main.MdiChildren.OfType<frmReportWebView>().Single();
            await CheckWorksheetPairedResult(chosen, 1);
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, chosen) && pane.WorksheetMode,
                "opening the new result leaves the source worksheet conversation visible");
            Check(beforeApproval.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "reselecting, reopening and running on local data send no additional question or result");

            // With two reports open, deliberately select the older one as the destination.
            chosen.Text = "Local analysis report";
            var other = (frmReportWebView)main.CreateReport();
            await Task.WhenAll(chosen.LearningReport.Ready, other.LearningReport.Ready).WaitAsync(TimeSpan.FromSeconds(40));
            chosen.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, chosen), "the user selects an existing destination report");
            pane = await OpenWorksheetAssistant(main, service, grid);
            review = FindWorksheetAnalysisReview(pane);
            const string existingQuestion = "TRE_WORKSHEET_ANALYSIS_EXISTING_REPORT";
            await WorksheetReply(pane, existingQuestion);
            CheckWorksheetAnalysisQuestion(trace, existingQuestion);
            Check(await WorksheetAnalysisResultCount(chosen) == 1 && await WorksheetAnalysisResultCount(other) == 0,
                "the pending second proposal leaves both reports unchanged");
            beforeApproval = WorksheetAnalysisDataTraffic(trace);
            ReviewWorksheetAnalysis(main, review, approve: true, PairedSelection, chosen.Text);
            await CheckWorksheetPairedResult(chosen, 2);
            Check(await WorksheetAnalysisResultCount(other) == 0 && ReferenceEquals(main.SelectedReportWindow, chosen),
                "the approved native analysis writes only to the report last selected by the user");
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, chosen) && pane.WorksheetMode,
                "activating the selected report keeps the worksheet conversation open");
            Check(beforeApproval.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "running the native analysis adds no question, worksheet data or calculated result to the mock server traffic");
            service.ShowReportAI(chosen);
            Check(!pane.WorksheetMode && ReferenceEquals(Field(pane, "script"), chosen),
                "the user can explicitly switch from worksheet AI to the selected report's assistant");

            pane = await OpenWorksheetAssistant(main, service, grid);
            var sharedSelection = grid.CaptureWorksheetAnalysisTarget(workbookShared: true);
            ChooseRange(InitialSelection);
            Check(SharedWorksheetAnalysisRequiresNewSuggestion(grid, sharedSelection),
                "a shared worksheet analysis cannot use a changed selection without a new suggestion");
            ChooseRange(PairedSelection);
            var sharedVersion = grid.CaptureWorksheetAnalysisTarget(workbookShared: true);
            WithWorksheet(workbook, sheet => { sheet.Cells["C1"].Value = PrivateCell + "_EDITED"; return true; });
            Check(SharedWorksheetAnalysisRequiresNewSuggestion(grid, sharedVersion),
                "a shared worksheet analysis cannot use changed workbook data without a new suggestion");

            pane.Composer.Text = "TRE_WORKSHEET_ANALYSIS_R_EXAMPLE";
            await pane.SendAsync();
            await pane.Transcript.SettledAsync();
            var browser = pane.Transcript.Browser.CoreWebView2;
            Check(!reviewActions.Visible && await browser.ExecuteScriptAsync("""
                (() => {
                    const section = document.querySelector('.message.assistant:last-child details.r-code-disclosure');
                    return !!section && !section.open && !!section.querySelector('[data-code=copy]') && !!section.querySelector('[data-code=open]');
                })()
                """) == "true", "an R reply keeps Copy and Open in R inside its own collapsed section");
            const string followUp = "TRE_WORKSHEET_ANALYSIS_FOLLOWUP";
            await WorksheetReply(pane, followUp);
            await pane.Transcript.SettledAsync();
            Check(await browser.ExecuteScriptAsync("""
                document.querySelectorAll('details.r-code-disclosure').length === 1 &&
                    !document.querySelector('.message.assistant:last-child details.r-code-disclosure')
                """) == "true", "a later plain reply has no R actions and keeps the earlier reply's code available");
            CheckWorksheetAnalysisQuestion(trace, followUp);
            string wire = ReadWorksheetTrace(trace);
            Check(!wire.Contains(PrivateWorksheet, StringComparison.Ordinal) && !wire.Contains(PrivateCell, StringComparison.Ordinal)
                && !wire.Contains("4.706787", StringComparison.Ordinal) && !wire.Contains("0.009261696", StringComparison.Ordinal)
                && !wire.Contains("differences between Before and After", StringComparison.Ordinal),
                "neither workbook metadata and values nor native result content appear in the wire trace, including the next question");
        }
        finally
        {
            prefs.CanDefaultConfidenceInterval = originalDefault;
            prefs.DefaultConfidenceInterval = originalLevel;
            pane?.Hide();
            foreach (var report in main.MdiChildren.OfType<frmReportWebView>().ToArray()) CloseTestDocument(report);
            CloseTestDocument(grid);
            CloseTestDocument(otherWorkbook);
            typeof(frmMain).GetField("learning", Members)!.SetValue(main, null);
        }
    }

    private static async Task ShowWorksheetAnalysisDemo(frmMain main)
    {
        var tutor = WorksheetMockTutor(Path.Combine(root, "preview-worksheet-analysis-account"), () => AISecurityLevel.DataSharingRestricted);
        var service = new LearningService(main, Path.Combine(root, "Preview Worksheet Analysis"), tutor);
        typeof(frmMain).GetField("learning", Members)!.SetValue(main, service);
        main.FormClosed += (_, _) => { service.Dispose(); tutor.Dispose(); };
        var grid = PairedWorksheet(main, "Worksheet AI analysis prototype (simulated replies)", privateFixture: false);
        await tutor.SignInAsync();
        await Wait(() => tutor.Email == "mock@example.invalid", "the prototype uses a local simulated AI account");
        var pane = await OpenWorksheetAssistant(main, service, grid);
        pane.Composer.Text = "Compare my two paired measurements using a paired t test";
        await pane.SendAsync();
        var review = FindWorksheetAnalysisReview(pane);
        await Wait(() => review.Enabled && review.Visible, "the recommended paired t test is ready for local review");
    }

    private static bool SharedWorksheetAnalysisRequiresNewSuggestion(frmSpreadsheetGear grid, WorksheetAnalysisTarget target)
    {
        try { grid.ResolveWorksheetAnalysisTarget(target, allowSelectionChanges: true); }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("Ask for a new analysis suggestion", StringComparison.Ordinal);
        }
        return false;
    }

    private static Button FindWorksheetAnalysisReview(ScriptAIPane pane) =>
        ((Control)Field(pane, "view")).Controls.Find("reviewWorksheetAnalysis", true).OfType<Button>().Single();

    private static string ReadWorksheetTrace(string trace)
    {
        // The mock can append transport housekeeping while the fixture reads its trace.
        using var file = new FileStream(trace, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(file);
        string text = reader.ReadToEnd();
        return text[..(text.LastIndexOf('\n') + 1)];
    }

    private static string[] WorksheetAnalysisDataTraffic(string trace) =>
        ReadWorksheetTrace(trace).Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line =>
        {
            var request = JsonNode.Parse(line);
            return request.S("method") == "turn/start" || request["result"] != null;
        }).ToArray();

    private static JsonNode LastWorksheetRequest(string trace)
    {
        var turn = WorksheetAnalysisDataTraffic(trace).Select(line => JsonNode.Parse(line))
            .Last(request => request.S("method") == "turn/start");
        string message = turn["params"].A("input")[0].S("text");
        return JsonNode.Parse(message[(message.IndexOf('\n') + 1)..])!;
    }

    private static void CheckWorksheetMethodSelectionContext(string trace, bool openingQuestion, params string[] privateTexts)
    {
        var requests = ReadWorksheetTrace(trace).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)).ToArray();
        int lastTurn = Array.FindLastIndex(requests, request => request.S("method") == "turn/start");
        var parameters = requests.Take(lastTurn).LastOrDefault(request => request.S("method") == "thread/start")?["params"];
        string prompt = parameters.S("baseInstructions");
        const string helpUrl = "https://www.statsdirect.com/help/methods/method_selection.htm";
        Check(lastTurn > 0 && parameters.S("serviceName") == "statsdirect-worksheet-assistant",
            "the question starts after the assistant thread is configured");

        string rawContext = LastWorksheetRequest(trace).S("referenceContext");
        var reference = JsonNode.Parse(rawContext)!.AsObject();
        string[] landmarks =
        [
            "WHAT SORT OF INVESTIGATION ARE YOU TACKLING?",
            "Select the appropriate combination of measurement scales",
            "Only the simpler experimental designs are dealt with here",
        ];
        Check(!reference.ContainsKey("statsDirectHelp") && !prompt.Contains(helpUrl, StringComparison.Ordinal)
            && landmarks.All(text => !prompt.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !rawContext.Contains(text, StringComparison.OrdinalIgnoreCase)),
            "requests carry neither bulk Method Selection pages nor automatic Help excerpts");

        if (openingQuestion)
        {
            var guide = reference["methodSelection"] as JsonObject;
            string summary = guide.S("text");
            Check(reference.S("openingQuestion") == WorksheetOpeningText
                && guide.S("title") == "Statistical Method Selection" && guide.S("url") == helpUrl
                && summary.Length <= 700
                && prompt.Contains("This opening question needs no tool calls.", StringComparison.Ordinal)
                && new[] { "goal", "scale", "independent", "paired", "association" }
                    .All(term => summary.Contains(term, StringComparison.OrdinalIgnoreCase)),
                "the first question carries the opening offer and a short Method Selection guide");
        }
        else
        {
            Check(!reference.ContainsKey("openingQuestion") && !reference.ContainsKey("methodSelection"),
                "later questions omit the opening offer and Method Selection guide");
        }
        Check(prompt.Contains("Ground instructions about StatsDirect in the manual.", StringComparison.Ordinal)
            && prompt.Contains("Draw on general statistical and R knowledge", StringComparison.Ordinal),
            "the prompt grounds StatsDirect guidance in its manual and uses general statistics and R knowledge when useful");
        Check(privateTexts.All(text => !prompt.Contains(text, StringComparison.Ordinal)
            && !rawContext.Contains(text, StringComparison.Ordinal)),
            "the worksheet guidance passes no workbook information without consent");
    }

    private static void CheckWorksheetAnalysisQuestion(string trace, string question, bool openingQuestion = false)
    {
        var request = LastWorksheetRequest(trace);
        var conversation = request.A("conversation");
        var reference = JsonNode.Parse(request.S("referenceContext"))!.AsObject();
        Check(conversation.Count == 1 && conversation[0].S("role") == "user" && conversation[0].S("text") == question
            && reference.Count == (openingQuestion ? 2 : 0),
            "the restricted request supplies the current question without workbook data");
        CheckWorksheetMethodSelectionContext(trace, openingQuestion, PrivateWorksheet, PrivateCell);
    }

    private static void ReviewWorksheetAnalysis(frmMain main, Button review, bool approve, string range, string destination)
    {
        bool sawReview = false, correctReview = false, sawNativeOptions = false;
        int prompts = 0;
        string failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 80 };
        timer.Tick += (_, _) =>
        {
            var modal = Application.OpenForms.OfType<Form>().FirstOrDefault(form => form.Modal);
            if (modal != null)
            {
                sawReview = modal.Name == "worksheetAnalysisReview";
                string TextOf(string name) => modal.Controls.Find(name, true).SingleOrDefault()?.Text ?? "";
                correctReview = sawReview && !main.InOperation
                    && TextOf("worksheetAnalysisMethod").Contains("Paired Student t test", StringComparison.OrdinalIgnoreCase)
                    && TextOf("worksheetAnalysisSource").Contains(PrivateWorksheet, StringComparison.Ordinal)
                    && TextOf("worksheetAnalysisSelection").Replace("$", "", StringComparison.Ordinal).Contains(range, StringComparison.Ordinal)
                    && TextOf("worksheetAnalysisDestination").Contains(destination, StringComparison.OrdinalIgnoreCase);
                var area = modal.RectangleToScreen(modal.ClientRectangle);
                correctReview = correctReview &&
                    modal.Controls.Find("openWorksheetAnalysis", true).OfType<Button>().SingleOrDefault() is { Text: "Run and add to report" } open
                    && area.Contains(open.RectangleToScreen(open.ClientRectangle))
                    && modal.Controls.Find("cancelWorksheetAnalysis", true).OfType<Button>().SingleOrDefault() is { } cancel
                    && area.Contains(cancel.RectangleToScreen(cancel.ClientRectangle))
                    && (cancel.Focused || ReferenceEquals(modal.ActiveControl, cancel));
                if (!correctReview)
                    Console.WriteLine($"Expected {range} → {destination}; actual {TextOf("worksheetAnalysisSelection")} → {TextOf("worksheetAnalysisDestination")}");
                string action = approve ? "openWorksheetAnalysis" : "cancelWorksheetAnalysis";
                if (correctReview && modal.Controls.Find(action, true).OfType<Button>().SingleOrDefault() is { Enabled: true } button)
                    button.PerformClick();
                else
                    modal.DialogResult = DialogResult.Cancel;
                return;
            }

            if (!approve || !sawReview || !main.InOperation) return;
            bool selecting = (bool)Field(main, "selectingData"), inputting = (bool)Field(main, "inputtingData");
            if ((bool)Field(main, "waitingForModalMessage"))
            {
                failure = ((Label)Field(main, "lblModalMessageText")).Text;
                ((Button)Field(main, "cmdModalMessage1")).PerformClick();
                return;
            }
            if (!selecting && !inputting) return;
            if (selecting || failure != null || ++prompts > 8)
            {
                failure ??= selecting ? "the preselected matched columns were requested again" : "too many native option requests";
                ((Button)Field(main, selecting ? "cmdCancel" : "cmdClose")).PerformClick();
                return;
            }
            if (!main.Text.Contains("Paired Student t test", StringComparison.OrdinalIgnoreCase))
            {
                ((Button)Field(main, "cmdClose")).PerformClick();
                return;
            }
            sawNativeOptions = true;
            ((Button)Field(main, "cmdCalculate")).PerformClick();
        };
        timer.Start();
        review.PerformClick();
        timer.Stop();
        Check(sawReview && correctReview, $"Review analysis shows the method, {range}, destination {destination} and both buttons, with focus on Cancel");
        Check(failure == null && (!approve || sawNativeOptions),
            failure == null ? "the explicit approval path uses the native paired t options and Run command" : "native paired t setup failed: " + failure);
    }

    private static async Task<int> WorksheetAnalysisResultCount(frmReportWebView report) =>
        (await report.LearningReport.CallAsync("snapshot")).GetProperty("entries").GetArrayLength();

    private static async Task CheckWorksheetPairedResult(frmReportWebView report, int expectedCount)
    {
        await report.LearningReport.Ready.WaitAsync(TimeSpan.FromSeconds(40));
        JsonElement entries = default;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            entries = (await report.LearningReport.CallAsync("snapshot")).GetProperty("entries");
            if (entries.GetArrayLength() >= expectedCount) break;
            await Task.Delay(50);
        }
        Check(entries.GetArrayLength() == expectedCount && entries[expectedCount - 1].GetProperty("operation").GetString() == "TPaired",
            $"the report contains {expectedCount} entries with the new native paired t result last");
        string itemId = entries[expectedCount - 1].GetProperty("id").GetString()!;
        var values = (await report.LearningReport.RecordAsync(itemId)).GetProperty("record").GetProperty("values");
        bool Is(string name, double expected) => Math.Abs(values.GetProperty(name).GetDouble() - expected) < 1e-9;
        Check(Is("n", 5) && Is("df", 4) && Is("mean", 2.4) && Is("t", 4.706787243316)
            && Is("tail_2", 0.009261696760) && Is("from", 0.984285223018) && Is("to", 3.815714776982),
            "the native report's paired t run record agrees with the independent base R calculation on five matched observations");
    }
}

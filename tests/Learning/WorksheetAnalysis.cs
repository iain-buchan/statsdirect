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
        EnsureWorksheetMethodMenus(main);
        var prefs = SdApplication.SoleInstance.Preferences;
        bool originalDefault = prefs.CanDefaultConfidenceInterval;
        double originalLevel = prefs.DefaultConfidenceInterval;
        bool originalGrouping = prefs.SelectGroupsByIdentifier;
        prefs.CanDefaultConfidenceInterval = true;
        prefs.DefaultConfidenceInterval = .95;
        prefs.SelectGroupsByIdentifier = false;
        ScriptAIPane pane = null;
        try
        {
            await tutor.SignInAsync();
            await tutor.RefreshAccountAsync();
            await Wait(() => tutor.Email == "mock@example.invalid", "the restricted worksheet assistant uses the local mock account");
            pane = await OpenWorksheetAssistant(main, service, grid);
            Check(tutor.DataSharingDisabled && !pane.ShareWorkbook.Enabled && !pane.Acknowledge.Visible && pane.Send.Enabled,
                "level 2 locks worksheet sharing and hides confirmation while leaving Send available");

            var paneView = (Control)Field(pane, "view");
            var aiStatus = paneView.Controls.Find("aiStatus", true).Single();
            var browser = pane.Transcript.Browser.CoreWebView2;
            var workbook = (WorkbookView)Field(grid, "workbookView");
            void ChooseRange(string range) => WithWorksheet(workbook, sheet =>
            {
                workbook.RangeSelection = sheet.Cells[range];
                return true;
            });

            string[] initialTraffic = WorksheetAnalysisDataTraffic(trace);
            CheckWorksheetCatalog(main, grid);
            await CheckWorksheetPickerConfirmation(main, pane, "normal random", "RndNormal", reportExpected: false);
            pane.NewConversation();
            await CheckWorksheetPickerConfirmation(main, pane, "graphics histogram", "HistogramPlot", reportExpected: true);
            pane.NewConversation();
            await CheckWorksheetPickerConfirmation(main, pane, "calculator", "", reportExpected: false);
            pane.NewConversation();
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any()
                && initialTraffic.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "browsing and staging commands from three Analyse picker categories run nothing and send nothing");
            await WaitForWorksheetAction(pane, "compare");
            await ClickWorksheetAction(pane, "compare");
            var designChoices = await WaitForWorksheetAction(pane, "choose");
            Check(designChoices.A("choices").Select(item => item.GetValue<string>()).SequenceEqual(["paired", "independent"]),
                "the worksheet conversation offers two explicit study-design choices");
            await ClickWorksheetAction(pane, "paired");
            var local = await CheckWorksheetConfirmation(pane, "Paired Student t test", PairedSelection, "A new report");
            await ClickWorksheetAction(pane, "change");
            await WaitForWorksheetAction(pane, "choose");
            await ClickWorksheetAction(pane, "independent");
            await CheckWorksheetConfirmation(pane, "Unpaired Student t test", PairedSelection, "A new report");
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any()
                && initialTraffic.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "choosing and changing the method changes only the local confirmation");
            await browser.ExecuteScriptAsync("window.chrome.webview.postMessage(" + JsonSerializer.Serialize(new
            {
                action = "worksheetAnalysis", token = local.S("token"), choice = "continue", context = (int)Field(pane.Transcript, "context")
            }) + ")");
            await Task.Delay(80);
            Check(!main.InOperation && (await WorksheetActionSnapshot(pane)).S("kind") == "confirm",
                "a stale Continue button cannot confirm a different method");

            ChooseRange(InitialSelection);
            await ClickWorksheetAction(pane, "continue");
            await Wait(() => aiStatus.Text.Contains("changed", StringComparison.OrdinalIgnoreCase),
                "Continue refuses a range changed since the confirmation was shown");
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any(),
                "a changed range does not launch an analysis or create a report");

            ChooseRange(PairedSelection);
            const string initialQuestion = "TRE_WORKSHEET_ANALYSIS_NEW_REPORT";
            var response = await WorksheetReply(pane, initialQuestion);
            var tools = response.A("tools").Select(tool => tool.GetValue<string>()).ToHashSet();
            Check(tools.SetEquals(["statsdirect_method_help", "statsdirect_search_help", "statsdirect_read_help_topic",
                    "statsdirect_find_worksheet_methods", "statsdirect_propose_worksheet_analysis", "statsdirect_choose_worksheet_t_test"])
                && response.S("help").Contains("paired", StringComparison.OrdinalIgnoreCase),
                "level 2 exposes bundled help, method lookup and proposals, and a study-design question");
            Check(response.I("broadMatches") is > 0 and <= 8
                && response.A("matches").Any(method => method.S("operation") == "TPaired"
                    && method.S("id").Length > 0 && method.S("menuPath").Contains("Analysis", StringComparison.Ordinal)),
                "restricted lookup returns compact, bounded native menu identities");
            Check(!response.B("workspace") && !response.B("read") && !response.B("calculation")
                && !response.B("invalid") && !response.B("unknownMethod"),
                "the restricted transport refuses workbook scope, cell reads, direct calculations, arbitrary methods and AI-selected columns");
            var receipt = response["pending"] as JsonObject;
            Check(receipt.S("state") == "pending_review" && receipt.S("operation") == "TPaired"
                && receipt.S("method") == "Paired Student t test" && receipt.ContainsKey("message")
                && receipt.All(property => property.Key is "state" or "message" or "method" or "operation"),
                "the model receives a proposal receipt without workbook values or calculated results");
            await CheckWorksheetConfirmation(pane, "Paired Student t test", PairedSelection, "A new report");
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any(),
                "a model-proposed method neither runs a calculation nor creates a report");
            CheckWorksheetAnalysisQuestion(trace, initialQuestion, openingQuestion: true);
            string[] beforeApproval = WorksheetAnalysisDataTraffic(trace);
            string stagedToken = (await WorksheetActionSnapshot(pane)).S("token");
            otherWorkbook.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, otherWorkbook), "the user switches to another workbook with an analysis staged");
            grid.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, grid), "the user returns to the original worksheet");
            Check(stagedToken.Length > 0 && (await WorksheetActionSnapshot(pane)).S("token") == stagedToken,
                "the proposed worksheet method survives visiting another workbook");
            await ContinueWorksheetAnalysis(main, pane, "Paired Student t test");
            await Wait(() => main.MdiChildren.OfType<frmReportWebView>().Count() == 1,
                "confirming the inline proposal creates a report when none was open");
            var chosen = main.MdiChildren.OfType<frmReportWebView>().Single();
            await CheckWorksheetPairedResult(chosen, 1);
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, chosen) && pane.WorksheetMode,
                "opening the new result leaves the source worksheet conversation visible");
            Check(beforeApproval.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "confirming and running on local data sends no additional question or result");

            // The workbook conversation remains open even while its destination report has focus.
            await CheckWorksheetPickerConfirmation(main, pane, "paired t", "TPaired", reportExpected: true, reportDestination: chosen);
            await CheckWorksheetConfirmation(pane, "Paired Student t test", PairedSelection, chosen.Text);
            Check(ReferenceEquals(main.ActiveMdiChild, grid) && ReferenceEquals(main.SelectedReportWindow, chosen)
                && await WorksheetAnalysisResultCount(chosen) == 1 && beforeApproval.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "choosing a method from the report returns to the chat's worksheet, keeps the selected report, and creates no additional result or AI traffic");

            // With two reports open, select the older one as the destination.
            chosen.Text = "Local analysis report";
            var other = (frmReportWebView)main.CreateReport();
            await Task.WhenAll(chosen.LearningReport.Ready, other.LearningReport.Ready).WaitAsync(TimeSpan.FromSeconds(40));
            chosen.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, chosen), "the user selects an existing destination report");
            pane = await OpenWorksheetAssistant(main, service, grid);
            const string existingQuestion = "TRE_WORKSHEET_ANALYSIS_EXISTING_REPORT";
            await WorksheetReply(pane, existingQuestion);
            CheckWorksheetAnalysisQuestion(trace, existingQuestion);
            await CheckWorksheetConfirmation(pane, "Paired Student t test", PairedSelection, chosen.Text);
            other.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, other), "the user changes the report after staging the analysis");
            grid.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, grid), "the source worksheet is active again");
            await ClickWorksheetAction(pane, "continue");
            await Wait(() => aiStatus.Text.Contains("destination", StringComparison.OrdinalIgnoreCase),
                "Continue refuses a report destination changed since the confirmation was shown");
            Check(await WorksheetAnalysisResultCount(chosen) == 1 && await WorksheetAnalysisResultCount(other) == 0,
                "a changed destination leaves both reports unchanged");

            chosen.Activate();
            pane = await OpenWorksheetAssistant(main, service, grid);
            const string designQuestion = "TRE_WORKSHEET_ANALYSIS_DESIGN";
            response = await WorksheetReply(pane, designQuestion);
            Check(response["pending"].S("state") == "awaiting_design",
                "the model can ask for clickable study-design choices");
            CheckWorksheetAnalysisQuestion(trace, designQuestion);
            await WaitForWorksheetAction(pane, "choose");
            beforeApproval = WorksheetAnalysisDataTraffic(trace);
            await ClickWorksheetAction(pane, "independent");
            await CheckWorksheetConfirmation(pane, "Unpaired Student t test", PairedSelection, chosen.Text);
            await ContinueWorksheetAnalysis(main, pane, "Unpaired Student t test", () => CheckWorksheetUnpairedResult(chosen, 2));
            Check(await WorksheetAnalysisResultCount(other) == 0 && ReferenceEquals(main.SelectedReportWindow, chosen),
                "the confirmed native analysis writes only to the report last selected by the user");
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, chosen) && pane.WorksheetMode,
                "activating the selected report keeps the worksheet conversation open");
            Check(beforeApproval.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "the design choice and native run send no question, worksheet data or calculated result");
            pane = await OpenWorksheetAssistant(main, service, grid);
            WithWorksheet(workbook, sheet =>
            {
                sheet.Cells["D1"].Value = "Independent sample x";
                sheet.Cells["E1"].Value = "Independent sample y";
                int[] x = [2, 5, 7, 11, 13], y = [1, 3, 4, 8, 12];
                for (int i = 0; i < x.Length; i++)
                {
                    sheet.Cells[i + 1, 3].Value = x[i];
                    sheet.Cells[i + 1, 4].Value = y[i];
                }
                workbook.RangeSelection = sheet.Cells["D1:E6"];
                return true;
            });
            const string mannWhitneyQuestion = "TRE_WORKSHEET_ANALYSIS_MANN_WHITNEY";
            response = await WorksheetReply(pane, mannWhitneyQuestion);
            var mannWhitney = response["pending"] as JsonObject;
            Check(mannWhitney.S("state") == "pending_review" && mannWhitney.S("operation") == "MannWhitney"
                && response.A("matches").Any(method => method.S("operation") == "MannWhitney"
                    && method.S("menuPath").Contains("Nonparametric", StringComparison.Ordinal)),
                "the model finds and stages Mann–Whitney by its native picker identity");
            await CheckWorksheetConfirmation(pane, "Mann-Whitney U (two-sample Wilcoxon) test", "D1:E6", chosen.Text);
            Check(!main.InOperation && await WorksheetAnalysisResultCount(chosen) == 2,
                "the staged Mann–Whitney proposal has not yet run");
            CheckWorksheetAnalysisQuestion(trace, mannWhitneyQuestion);
            beforeApproval = WorksheetAnalysisDataTraffic(trace);
            await ContinueWorksheetAnalysis(main, pane, "Mann-Whitney U (two-sample Wilcoxon) test", () => CheckWorksheetMannWhitneyResult(chosen, 3));
            Check(beforeApproval.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "the confirmed Mann–Whitney run keeps the data and result local");
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
            var browseOnly = await WaitForWorksheetAction(pane, "compare");
            Check(browseOnly.A("choices").Select(choice => choice.GetValue<string>()).SequenceEqual(["browse"])
                && await browser.ExecuteScriptAsync("""
                (() => {
                    const section = [...document.querySelectorAll('.message.assistant:not(.worksheet-analysis)')].at(-1)?.querySelector('details.r-code-disclosure');
                    return !!section && !section.open && !!section.querySelector('[data-code=copy]') && !!section.querySelector('[data-code=open]');
                })()
                """) == "true", "an R reply keeps Copy and Open in R inside its own collapsed section");
            const string followUp = "TRE_WORKSHEET_ANALYSIS_FOLLOWUP";
            await WorksheetReply(pane, followUp);
            await pane.Transcript.SettledAsync();
            Check(await browser.ExecuteScriptAsync("""
                document.querySelectorAll('details.r-code-disclosure').length === 1 &&
                    ![...document.querySelectorAll('.message.assistant:not(.worksheet-analysis)')].at(-1)?.querySelector('details.r-code-disclosure')
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
            prefs.SelectGroupsByIdentifier = originalGrouping;
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
        EnsureWorksheetMethodMenus(main);
        await tutor.SignInAsync();
        await Wait(() => tutor.Email == "mock@example.invalid", "the prototype uses a local simulated AI account");
        var pane = await OpenWorksheetAssistant(main, service, grid);
        await WaitForWorksheetAction(pane, "compare");
        await ClickWorksheetAction(pane, "compare");
        await WaitForWorksheetAction(pane, "choose");
    }

    private static void EnsureWorksheetMethodMenus(frmMain main)
    {
        if (main.WorksheetCommands().Length == 0)
            typeof(frmMain).GetMethod("AddTemplates", Members)!.Invoke(main, null);
    }

    private static void CheckWorksheetCatalog(frmMain main, frmSpreadsheetGear grid)
    {
        var menu = (MenuStrip)Field(main, "mnuMain");
        var leaves = menu.Items.OfType<ToolStripMenuItem>()
            .Where(item => item.Available && item.Text.Replace("&", "") is "Analysis" or "Data" or "Graphics")
            .SelectMany(MenuLeaves)
            .Where(item => item.Name == "calculatorMenuItem" || item.Tag is string tag && tag.Contains("operation=", StringComparison.Ordinal))
            .ToHashSet();
        var catalog = main.WorksheetCommands();
        Check(catalog.Length > 100 && catalog.Length == leaves.Count && catalog.Select(command => command.Item).ToHashSet().SetEquals(leaves),
            $"the analysis catalog has exactly the {leaves.Count} real Analyse picker entries from Analysis, Data and Graphics");
        var repeatedCatalog = main.WorksheetCommands();
        Check(catalog.All(command => command.Id.Length != 0)
            && catalog.Select(command => command.Id).Distinct(StringComparer.Ordinal).Count() == catalog.Length
            && catalog.Select(command => command.Id).SequenceEqual(repeatedCatalog.Select(command => command.Id)),
            "each entry, including separately named menu paths for like methods, has a unique deterministic confirmation identity");
        var target = grid.CaptureWorksheetAnalysisTarget();
        var available = catalog.Where(command => command.CanRun).ToArray();
        foreach (var command in available)
        {
            var proposal = target.Propose(command);
            if (proposal.MethodName.Length == 0 || proposal.MenuPath != command.MenuPath)
                throw new Exception("The confirmation lost the native method/menu for " + command.Id);
        }
        Check(available.Length > 100 && !main.InOperation,
            $"all {available.Length} available Analyse entries can create their own confirmation without running a command");

        var calculator = available.Single(command => command.Item.Name == "calculatorMenuItem");
        var stagedCalculator = target.Propose(calculator);
        calculator.Item.Enabled = false;
        try
        {
            bool rejectedStaging = false, rejectedOpening = false;
            try { target.Propose(calculator); }
            catch (InvalidOperationException) { rejectedStaging = true; }
            try { stagedCalculator.Open(); }
            catch (InvalidOperationException) { rejectedOpening = true; }
            Check(rejectedStaging && rejectedOpening && !main.InOperation,
                "a disabled native command cannot be staged or opened, even with its original confirmation");
        }
        finally { calculator.Item.Enabled = true; }

        static IEnumerable<ToolStripMenuItem> MenuLeaves(ToolStripMenuItem item) =>
            item.HasDropDownItems
                ? item.DropDownItems.OfType<ToolStripMenuItem>().Where(child => child.Available).SelectMany(MenuLeaves)
                : [item];
    }

    private static async Task CheckWorksheetPickerConfirmation(frmMain main, ScriptAIPane pane, string query, string operation, bool reportExpected,
        frmReportWebView reportDestination = null)
    {
        await WaitForWorksheetAction(pane, "compare");
        var toolbar = (ToolStrip)Field(main, "toolStrip");
        var analyse = (ToolStripDropDownButton)toolbar.Items["analyseToolStripButton"];
        var picker = analyse.DropDown;
        var content = picker.Items.OfType<ToolStripControlHost>().Single().Control;
        var search = (TextBox)content.Controls.Find("worksheetCommandSearch", true).Single();
        var results = (TreeView)content.Controls.Find("worksheetCommandResults", true).Single();
        var source = (frmSpreadsheetGear)Field(pane, "script");
        await ClickWorksheetAction(pane, "browse");
        await Wait(() => picker.Visible && ReferenceEquals(main.ActiveMdiChild, source),
            "Choose a method activates the conversation's worksheet and opens its Analyse picker");
        search.Text = query;
        var command = results.SelectedNode?.Tag as WorksheetCommand;
        Check(command != null && command.Operation == operation && command.CanRun,
            "the shared picker selects the expected native command for " + query);
        picker.GetType().GetMethod("HandleKey", Members)!.Invoke(picker, [Keys.Enter]);
        var confirmation = await WaitForWorksheetAction(pane, "confirm");
        string cardText = confirmation.S("text");
        Check(!picker.Visible && cardText.Contains(command.MenuPath, StringComparison.Ordinal)
            && cardText.Contains("Continue to analysis", StringComparison.Ordinal)
            && cardText.Contains(reportDestination?.Text ?? "A new report", StringComparison.Ordinal) == reportExpected
            && !main.InOperation,
            "the same picker command shows its own inline review before running with the applicable report destination");
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

    private static async Task<JsonNode> WorksheetActionSnapshot(ScriptAIPane pane)
    {
        await pane.Transcript.SettledAsync();
        return JsonNode.Parse(await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("""
            (() => {
                const card = document.querySelector('#conversation [data-worksheet-action]');
                return card ? {
                    kind: card.dataset.worksheetAction,
                    token: card.dataset.token,
                    text: card.innerText,
                    choices: [...card.querySelectorAll('button[data-action="worksheet-analysis"]')].map(button => button.dataset.choice)
                } : null;
            })()
            """));
    }

    private static async Task<JsonNode> WaitForWorksheetAction(ScriptAIPane pane, string kind)
    {
        JsonNode action = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            action = await WorksheetActionSnapshot(pane);
            if (action.S("kind") == kind) break;
            await Task.Delay(50);
        }
        Check(action.S("kind") == kind && !string.IsNullOrEmpty(action.S("token")), $"the conversation shows the {kind} action");
        return action;
    }

    private static async Task ClickWorksheetAction(ScriptAIPane pane, string choice)
    {
        string selector = JsonSerializer.Serialize($"button[data-action=\"worksheet-analysis\"][data-choice=\"{choice}\"]");
        string result = await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync($$"""
            (() => {
                const button = document.querySelector({{selector}});
                if (!button || button.disabled) return false;
                button.click();
                return true;
            })()
            """);
        Check(result == "true", $"the user clicks the {choice} chat button");
    }

    private static async Task<JsonNode> CheckWorksheetConfirmation(ScriptAIPane pane, string method, string range, string destination)
    {
        var action = await WaitForWorksheetAction(pane, "confirm");
        string text = action.S("text").Replace("$", "", StringComparison.Ordinal);
        Check(text.Contains(method, StringComparison.OrdinalIgnoreCase)
            && text.Contains(PrivateWorksheet, StringComparison.Ordinal)
            && text.Contains(range, StringComparison.Ordinal)
            && text.Contains(destination, StringComparison.OrdinalIgnoreCase)
            && text.Contains("Continue to analysis", StringComparison.Ordinal)
            && action.A("choices").Any(choice => choice.GetValue<string>() == "continue"),
            $"the inline confirmation shows {method}, {range}, destination {destination}, and Continue to analysis");
        return action;
    }
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

        string turnMessage = requests[lastTurn]["params"].A("input")[0].S("text");
        int firstLineEnd = turnMessage.IndexOf('\n');
        Check(firstLineEnd > 0 && turnMessage[..firstLineEnd].Contains(
                "Ask the most important unanswered question before naming or staging a test.", StringComparison.Ordinal),
            "the first line of the worksheet turn repeats the instruction to clarify before naming or staging a test");

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
        string[] clarification =
        [
            "ask before recommending or staging a specific method",
            "what the outcome measures and its scale",
            "what the user wants to compare",
            "how the observations were obtained and related",
            "Sample size alone does not choose the test."
        ];
        Check(clarification.All(instruction => prompt.Contains(instruction, StringComparison.Ordinal)),
            "the worksheet prompt asks about scale, comparison and study design before proposing a method and explains the role of sample size");
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

    private static async Task ContinueWorksheetAnalysis(frmMain main, ScriptAIPane pane, string method, Func<Task> verifyImmediateResult = null)
    {
        bool sawNativeOptions = false;
        int prompts = 0;
        string failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 80 };
        timer.Tick += (_, _) =>
        {
            if (!main.InOperation) return;
            bool selecting = (bool)Field(main, "selectingData"), inputting = (bool)Field(main, "inputtingData");
            if ((bool)Field(main, "waitingForModalMessage"))
            {
                failure = ((Label)Field(main, "lblModalMessageText")).Text;
                ((Button)Field(main, "cmdModalMessage1")).PerformClick();
                return;
            }
            if (!selecting && !inputting) return;
            if (selecting || failure != null || ++prompts > 8 || !main.Text.Contains(method, StringComparison.OrdinalIgnoreCase))
            {
                failure ??= selecting ? "the preselected columns were requested again" : "unexpected native test options";
                ((Button)Field(main, selecting ? "cmdCancel" : "cmdClose")).PerformClick();
                return;
            }
            sawNativeOptions = true;
            ((Button)Field(main, "cmdCalculate")).PerformClick();
        };
        timer.Start();
        try
        {
            await ClickWorksheetAction(pane, "continue");
            // The unpaired t-test can use the default confidence interval and go straight to its report.
            if (verifyImmediateResult != null) await verifyImmediateResult();
            else await Wait(() => (sawNativeOptions || failure != null) && !main.InOperation,
                "the native analysis finishes after the user continues", 30000);
        }
        finally { timer.Stop(); }
        Check(failure == null && (sawNativeOptions || verifyImmediateResult != null),
            failure == null ? $"the chat confirmation continues with the native {method}" : "native analysis setup failed: " + failure);
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

    private static async Task CheckWorksheetMannWhitneyResult(frmReportWebView report, int expectedCount)
    {
        JsonElement entries = default;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            entries = (await report.LearningReport.CallAsync("snapshot")).GetProperty("entries");
            if (entries.GetArrayLength() >= expectedCount) break;
            await Task.Delay(50);
        }
        Check(entries.GetArrayLength() == expectedCount && entries[expectedCount - 1].GetProperty("operation").GetString() == "MannWhitney",
            "the native report records Mann–Whitney on the independent samples");
        string itemId = entries[expectedCount - 1].GetProperty("id").GetString()!;
        var values = (await report.LearningReport.RecordAsync(itemId)).GetProperty("record").GetProperty("values");
        bool Is(string name, double expected) => Math.Abs(values.GetProperty(name).GetDouble() - expected) < 1e-10;
        Check(values.GetProperty("sample_1").GetString() == "Independent sample x"
            && values.GetProperty("sample_2").GetString() == "Independent sample y"
            && Is("obs_1", 5) && Is("obs_2", 5) && Is("ranksum", 31) && Is("u", 16) && Is("u_prime", 9)
            && Is("p_l", 0.7896825396825397) && Is("p_u", 0.2738095238095238) && Is("p_2", 0.5476190476190477),
            "Mann–Whitney sample sizes, U and exact tail probabilities match base R on the selected columns");
    }

    private static async Task CheckWorksheetUnpairedResult(frmReportWebView report, int expectedCount)
    {
        JsonElement entries = default;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            entries = (await report.LearningReport.CallAsync("snapshot")).GetProperty("entries");
            if (entries.GetArrayLength() >= expectedCount) break;
            await Task.Delay(50);
        }
        Check(entries.GetArrayLength() == expectedCount && entries[expectedCount - 1].GetProperty("operation").GetString() == "TUnpaired",
            "the report records the independent-samples analysis as the unpaired t-test");
        string itemId = entries[expectedCount - 1].GetProperty("id").GetString()!;
        var values = (await report.LearningReport.RecordAsync(itemId)).GetProperty("record").GetProperty("values");
        bool Is(string name, double expected) => Math.Abs(values.GetProperty(name).GetDouble() - expected) < 1e-9;
        Check(Is("df", 8) && Is("t", 0.784464540552736) && Is("p_2", 0.455366343552690)
            && Is("df_unequal", 7.878986276237927) && Is("p_2_unequal", 0.455703230416979),
            "the unpaired run agrees with base R for equal-variance and Welch tests");
    }
}

using SpreadsheetGear.Windows.Forms;
using StatsDirect.UI;
using StatsDirect.UI.HtmlHelp;
using StatsDirect.UI.Learning;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private const string WorksheetOpeningText = "Do you want me to help you analyse your data?";
    private const string PrivateWorksheet = "TRE_PRIVATE_ANALYSIS_WORKBOOK";
    private const string PairedSelection = "A1:B6";
    private const string KnownPairedQuestion = "TRE_WORKSHEET_ANALYSIS_PAIRED_INTERVAL";
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
        var securityLevel = AISecurityLevel.DataSharingRestricted;
        using var tutor = WorksheetMockTutor(Path.Combine(root, "worksheet-analysis-account"), () => securityLevel, trace);
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
            await CheckChooseMethodOpening(pane);
            var designReply = await WorksheetReply(pane, "TRE_WORKSHEET_ANALYSIS_DESIGN");
            Check(designReply["pending"].S("state") == "awaiting_design",
                "the model offers study-design choices when comparing quantitative means");
            string[] designTraffic = WorksheetAnalysisDataTraffic(trace);
            var designChoices = await WaitForWorksheetAction(pane, "choose");
            Check(designChoices.A("choices").Select(item => item.GetValue<string>()).SequenceEqual(["paired", "independent"])
                && designChoices.S("text").Contains("Same people or units", StringComparison.Ordinal),
                "the worksheet conversation offers two explicit study-design choices");
            await ClickWorksheetAction(pane, "paired");
            var pairedChoices = await WaitForWorksheetAction(pane, "paired-interval");
            Check(pairedChoices.A("choices").Select(item => item.GetValue<string>()).ToHashSet()
                    .SetEquals(["large", "normal", "non-normal", "back"]),
                "matched measurements ask about sample size and the distribution of differences before offering a method");
            Check(pairedChoices.S("text").Contains("5 complete pairs", StringComparison.OrdinalIgnoreCase)
                && !pairedChoices.S("text").Contains("How many complete pairs", StringComparison.OrdinalIgnoreCase)
                && !pairedChoices.S("text").Contains("How are the within-pair differences distributed?", StringComparison.OrdinalIgnoreCase),
                "a clear paired selection supplies its complete-pair count and replaces the generic distribution question");
            await CheckPairedDifferenceCard(pane, 5, expectShapiroWilk: true);
            await CheckPairedRecommendation(pane, 5, "normal", "Shapiro–Wilk", "limited evidence");
            var diagnostics = grid.CheckSelectedPairedDifferences();
            // R 4.6.2: shapiro.test(c(11,14,17,22,23) - c(10,11,15,20,19)).
            Check(diagnostics.Count == 5 && diagnostics.ShapiroWilkW is double w && diagnostics.ShapiroWilkP is double p
                && Math.Abs(w - 0.96085899914851) < 0.000005 && Math.Abs(p - 0.81395212735703) < 0.000005,
                "the local normality check agrees with base R on the within-pair differences");
            await ClickWorksheetAction(pane, "large");
            await WaitForWorksheetEvidence(pane, "paired-interval", "at most 100 complete pairs");
            await ClickWorksheetAction(pane, "normal");
            var local = await CheckWorksheetConfirmation(pane, "Paired Student t test", PairedSelection, "A new report");
            await ClickWorksheetAction(pane, "sample");
            await WaitForWorksheetAction(pane, "paired-interval");
            await ClickWorksheetAction(pane, "back");
            await WaitForWorksheetAction(pane, "choose");
            await ClickWorksheetAction(pane, "independent");
            await WaitForWorksheetAction(pane, "independent-interval");
            await ClickWorksheetAction(pane, "normal");
            await CheckWorksheetConfirmation(pane, "Unpaired Student t test", PairedSelection, "A new report");
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any()
                && designTraffic.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "choosing and changing the method changes only the local confirmation");
            await PostWorksheetAction(pane, local.S("token"), "continue");
            await Task.Delay(80);
            Check(!main.InOperation && (await WorksheetActionSnapshot(pane)).S("kind") == "confirm",
                "a stale Continue button cannot confirm a different method");

            ChooseRange(InitialSelection);
            await ClickWorksheetAction(pane, "continue");
            await Wait(() => aiStatus.Text.Contains("Choose the method again.", StringComparison.Ordinal),
                "Continue refuses a range changed since the confirmation was shown");
            Check(!main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any(),
                "a changed range does not launch an analysis or create a report");

            ChooseRange(PairedSelection);
            await CheckOtherPairedIntervalBranches(main, pane, grid, workbook, trace);
            await CheckOtherIndependentIntervalBranches(main, pane, workbook, trace);
            CheckWorksheetLocaleNumberAcquisition();
            await CheckPairedLocaleGuidance(main, pane, workbook, trace);
            pane.NewConversation();
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
                && !response.B("invalid") && !response.B("unknownMethod") && !response.B("clarification"),
                "the restricted transport refuses workbook scope, cell reads, direct calculations, arbitrary methods, AI-selected columns and multi-turn clarification");
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
            await CheckWorksheetPaneBinding(main, pane, otherWorkbook, shared: false);
            await WaitForWorksheetAction(pane, "empty", ["example", "help"]);
            await PostWorksheetAction(pane, stagedToken, "continue");
            await Task.Delay(80);
            Check(stagedToken.Length > 0 && !main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any()
                && beforeApproval.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "switching workbooks dismisses the staged method and rejects Continue with its old token");
            grid.Activate();
            await CheckWorksheetPaneBinding(main, pane, grid, shared: false);
            await WaitForWorksheetAction(pane, "compare");
            await WorksheetReply(pane, "TRE_WORKSHEET_ANALYSIS_RESTAGED");
            await CheckWorksheetConfirmation(pane, "Paired Student t test", PairedSelection, "A new report");
            beforeApproval = WorksheetAnalysisDataTraffic(trace);
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
            await ClickWorksheetAction(pane, "continue");
            await Wait(() => aiStatus.Text.Contains("Activate", StringComparison.OrdinalIgnoreCase)
                && aiStatus.Text.Contains(grid.Text, StringComparison.Ordinal),
                "Continue still requires the proposal's source worksheet in front when the chat stays open over a report");
            Check(!main.InOperation && (await WorksheetActionSnapshot(pane)).S("kind") == "confirm",
                "refusing Continue leaves the confirmation available without starting an analysis");
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
            await WaitForWorksheetAction(pane, "independent-interval");
            await ClickWorksheetAction(pane, "normal");
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
            await CheckChooseMethodOpening(pane);
            Check(await browser.ExecuteScriptAsync("""
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

            securityLevel = AISecurityLevel.Unrestricted;
            await CheckWorksheetClarification(main, pane, trace, chosen, ChooseRange);

            securityLevel = AISecurityLevel.DataSharingRestricted;
            await CheckWorksheetOpening(main, service, otherWorkbook, trace);
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

    private static Task CheckWorksheetSelectionLabel(ScriptAIPane pane, string expected)
    {
        var label = ((Control)Field(pane, "view")).Controls.Find("worksheetSelection", true).Single();
        return Wait(() => label.Visible && label.Text.Replace("$", "", StringComparison.Ordinal) == expected,
            "worksheet AI displays " + expected);
    }

    private static async Task<JsonNode> WaitForWorksheetEvidence(ScriptAIPane pane, string kind, params string[] evidence)
    {
        JsonNode action = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            action = await WorksheetActionSnapshot(pane);
            if (Matches(action)) break;
            await Task.Delay(50);
        }
        Check(Matches(action), $"the {kind} card shows {string.Join(", ", evidence)}");
        return action;

        bool Matches(JsonNode card) => card.S("kind") == kind
            && evidence.All(text => card.S("text").Contains(text, StringComparison.Ordinal));
    }

    private static async Task CheckPairedDifferenceCard(ScriptAIPane pane, int n, bool expectShapiroWilk)
    {
        string count = $"{n} complete pairs";
        var card = await WaitForWorksheetEvidence(pane, "paired-interval", count);
        string text = card.S("text");
        bool hasShapiroWilk = text.Contains("W =", StringComparison.Ordinal) && text.Contains("P =", StringComparison.Ordinal);
        Check(hasShapiroWilk == expectShapiroWilk && !card.A("choices").Any(choice => choice.GetValue<string>() == "check"),
            $"the automatic local check displays {count} and removes the check button"
                + (expectShapiroWilk ? " while showing Shapiro–Wilk W and P" : ""));
    }

    private static async Task CheckPairedRecommendation(ScriptAIPane pane, int n, string expectedBranch, params string[] reasons)
    {
        string heading = expectedBranch == null ? "Review the differences" : "Suggested branch";
        var card = await WaitForWorksheetEvidence(pane, "paired-interval", $"{n} complete pairs", heading);
        var recommendations = card.A("recommended");
        bool correctBranch = expectedBranch == null ? recommendations.Count == 0
            : recommendations.Count == 1 && recommendations[0].S("choice") == expectedBranch
                && recommendations[0].S("label").StartsWith("Suggested · ", StringComparison.Ordinal);
        string text = card.S("text");
        Check(correctBranch && reasons.All(reason => text.Contains(reason, StringComparison.OrdinalIgnoreCase))
            && !card.A("choices").Any(choice => choice.GetValue<string>() == "continue"),
            expectedBranch == null ? $"the {n}-pair decision explains why it needs review before suggesting a test"
                : $"the {n}-pair decision explains and highlights the {expectedBranch} option while leaving the decision open");
    }

    private static async Task CheckOtherPairedIntervalBranches(frmMain main, ScriptAIPane pane, frmSpreadsheetGear grid, WorkbookView workbook, string trace)
    {
        async Task StartKnownPairing()
        {
            var reply = await WorksheetReply(pane, KnownPairedQuestion);
            Check(reply["pending"].S("state") == "awaiting_distribution"
                && !reply.B("invalidDesign") && !reply.B("extraDesign"),
                "the model can open the distribution decision directly for an already-matched design");
            await WaitForWorksheetAction(pane, "paired-interval");
        }

        const string alternateSelection = "D1:E13";
        WithWorksheet(workbook, sheet =>
        {
            int[] differences = [-5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 1000];
            object[,] values = new object[13, 2];
            values[0, 0] = "Other Before";
            values[0, 1] = "Other After";
            for (int i = 0; i < differences.Length; i++)
            {
                values[i + 1, 0] = 20 + differences[i];
                values[i + 1, 1] = 20;
            }
            sheet.Cells[alternateSelection].Value = values;
            workbook.RangeSelection = sheet.Cells["A1"];
            return true;
        });
        await StartKnownPairing();
        await CheckWorksheetSelectionLabel(pane, "Current cell: Fictional data!A1");
        var undecided = await WorksheetActionSnapshot(pane);
        Check(undecided.S("text").Contains("How many complete pairs", StringComparison.OrdinalIgnoreCase)
            && !undecided.S("text").Contains("Local check found", StringComparison.Ordinal)
            && undecided.A("choices").Any(choice => choice.GetValue<string>() == "check"),
            "the paired card offers the local check when only one cell is current");
        string[] before = WorksheetAnalysisDataTraffic(trace);
        await ClickWorksheetAction(pane, "check");
        var invalid = await WaitForWorksheetEvidence(pane, "paired-interval", "Select two adjacent");
        Check(invalid.A("choices").Any(choice => choice.GetValue<string>() == "check")
            && !invalid.S("text").Contains("Local check found", StringComparison.Ordinal),
            "checking a single cell explains the required selection in the paired card");

        WithWorksheet(workbook, sheet =>
        {
            workbook.RangeSelection = sheet.Cells[PairedSelection];
            return true;
        });
        await CheckWorksheetSelectionLabel(pane, "Selection: Fictional data!" + PairedSelection);
        var selected = await WaitForWorksheetEvidence(pane, "paired-interval", "5 complete pairs");
        Check(!selected.S("text").Contains("How many complete pairs", StringComparison.OrdinalIgnoreCase)
            && !selected.S("text").Contains("Select two adjacent", StringComparison.Ordinal)
            && !selected.A("choices").Any(choice => choice.GetValue<string>() == "check")
            && before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
            "selecting paired data clears the earlier message and shows the evidence without an AI turn");

        string previousToken = selected.S("token");
        Check(Equals(typeof(ScriptAIPane).GetMethod("RefreshWorksheetGuidance", Members)!.Invoke(pane, null), false),
            "an unchanged worksheet selection and revision keep the current guidance card");
        var chooseAction = typeof(ScriptAIPane).GetMethod("ChooseWorksheetAction", Members)!
            .CreateDelegate<Action<string, string>>(pane);
        WithWorksheet(workbook, sheet => { workbook.RangeSelection = sheet.Cells[alternateSelection]; return true; });
        // Deliver the earlier click before the selection change posted to the UI loop can run.
        chooseAction(previousToken, "normal");
        var warning = await WaitForWorksheetEvidence(pane, "paired-interval", "12 complete pairs", "box-plot");
        Check(warning.S("token") != previousToken, "a stale branch click cannot skip the warning for a different selection");

        string warningToken = warning.S("token");
        WithWorksheet(workbook, sheet => { sheet.Cells["D13"].Value = 26; return true; });
        chooseAction(warningToken, "normal");
        var updated = await WaitForWorksheetEvidence(pane, "paired-interval", "12 complete pairs", "Suggested branch");
        Check(updated.S("token") != warningToken && updated.A("recommended").Single().S("choice") == "normal",
            "a same-range edit refreshes the evidence and retires the older branch click");
        await ClickWorksheetAction(pane, "normal");
        await CheckWorksheetConfirmation(pane, "Paired Student t test", alternateSelection, "A new report");
        await ClickWorksheetAction(pane, "sample");
        WithWorksheet(workbook, sheet =>
        {
            sheet.Cells[alternateSelection].ClearContents();
            workbook.RangeSelection = sheet.Cells[PairedSelection];
            return true;
        });
        await WaitForWorksheetEvidence(pane, "paired-interval", "5 complete pairs");
        await ClickWorksheetAction(pane, "non-normal");
        await CheckWorksheetConfirmation(pane, "Wilcoxon signed ranks test", PairedSelection, "A new report");
        Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
            "the non-normal branch prepares Wilcoxon signed ranks locally");

        const string largeSelection = "H1:I103";
        WithWorksheet(workbook, sheet =>
        {
            object[,] values = new object[103, 2];
            values[0, 0] = "Large Before";
            values[0, 1] = "Large After";
            for (int row = 1; row <= 101; row++)
            {
                values[row, 0] = row + row % 5;
                values[row, 1] = row;
            }
            values[102, 0] = 102; // One unmatched observation must not count as a pair.
            sheet.Cells[largeSelection].Value = values;
            workbook.RangeSelection = sheet.Cells["H1:I101"];
            return true;
        });
        try
        {
            var oracle = await PairedShapeOracles();
            await StartKnownPairing();
            before = WorksheetAnalysisDataTraffic(trace);
            await CheckPairedDifferenceCard(pane, 100, expectShapiroWilk: true);
            var boundary = grid.CheckSelectedPairedDifferences();
            var expected = oracle["boundary100"];
            Check(boundary.ShapiroWilkW is double w && Math.Abs(w - expected.W) < 5e-6
                && boundary.ShapiroWilkP is double p && Math.Abs(p - expected.P) < 5e-6,
                "at 100 pairs the retained Shapiro–Wilk check agrees with R");
            WithWorksheet(workbook, sheet =>
            {
                workbook.RangeSelection = sheet.Cells[largeSelection];
                return true;
            });
            await CheckPairedDifferenceCard(pane, 101, expectShapiroWilk: false);
            await CheckPairedRecommendation(pane, 101, "large");
            var largeCard = await WaitForWorksheetEvidence(pane, "paired-interval", "Shape check", "Skewness", "Excess kurtosis", "3× IQR");
            Check(!largeCard.S("text").Contains("Small or medium · Non-normal differences", StringComparison.Ordinal)
                && largeCard.S("text").Contains("Non-normal differences", StringComparison.Ordinal),
                "the non-normal button has no small-sample qualifier at the 101-pair boundary");
            CheckPairedShapeOracle(grid.CheckSelectedPairedDifferences(), oracle["boundary101"], "101-pair boundary");
            await ClickWorksheetAction(pane, "normal");
            await WaitForWorksheetEvidence(pane, "paired-interval", "more than 100 complete pairs", "large-sample", "non-normal");
            await ClickWorksheetAction(pane, "non-normal");
            await CheckWorksheetConfirmation(pane, "Wilcoxon signed ranks test", largeSelection, "A new report");
            await ClickWorksheetAction(pane, "sample");
            await WaitForWorksheetAction(pane, "paired-interval");
            await ClickWorksheetAction(pane, "large");
            await CheckWorksheetConfirmation(pane, "Paired Student t test", largeSelection, "A new report");
            Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace))
                && !main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any(),
                "the large-sample branch prepares a paired t test without sending data or running an analysis");

            await StartKnownPairing();
            before = WorksheetAnalysisDataTraffic(trace);
            await CheckFenceWarning([-4.4, -1, -1, 0, 0, 0, 0, 1, 1, 4.4], false,
                "ten differences beyond the old 1.5× IQR fence but inside the calibrated fence give no box-plot warning");
            await CheckFenceWarning([-5.5, -1, -1, 0, 0, 0, 0, 1, 1, 5.5], true,
                "ten differences beyond the calibrated fence give a box-plot warning");
            await CheckFenceWarning([-10, -1, -1, 0, 1, 1, 10], false,
                "seven pairs do not trigger a box-plot warning");
            await CheckFenceWarning([-10, -1, -1, 0, 0, 1, 1, 10], true,
                "eight pairs use the calibrated fence");
            CheckPairedFenceTable();
            await CheckRecommendationForDifferences(
                [-109, -108, -107, -106, -105, -104, -103, -102, -101, -100, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109],
                "non-normal", "Shapiro–Wilk", "symmetric");
            await CheckRecommendationForDifferences([-5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 1000], null, "box-plot");
            await CheckRecommendationForDifferences([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 5, 6, 7, 8], null, "skewness");
            await CheckRecommendationForDifferences([3, 3, 3, 3, 3, 3], null, "variation");
            await CheckRecommendationForDifferences(Enumerable.Repeat(3.0, 101).ToArray(), null, "variation");
            await CheckRecommendationForDifferences([1, 2], null);
            await CheckLargePairedShapes(main, pane, grid, workbook, trace, oracle);
            Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)) && !main.InOperation,
                "updating the paired recommendation from new local data sends no AI request and runs no analysis");

            async Task CheckFenceWarning(double[] differences, bool expected, string label)
            {
                const string warning = "The box-plot check flagged";
                SelectPairedDifferences(differences);
                string count = $"{differences.Length} complete pairs";
                var card = expected
                    ? await WaitForWorksheetEvidence(pane, "paired-interval", count, warning)
                    : await WaitForWorksheetEvidence(pane, "paired-interval", count);
                var actual = grid.CheckSelectedPairedDifferences();
                bool visible = card.S("text").Contains(warning, StringComparison.Ordinal);
                Check(actual.HasPotentialOutlier == expected && visible == expected, label);
            }

            async Task CheckRecommendationForDifferences(double[] differences, string branch, params string[] reasons)
            {
                SelectPairedDifferences(differences);
                await CheckPairedRecommendation(pane, differences.Length, branch, reasons);
            }

            void SelectPairedDifferences(double[] differences)
            {
                WithWorksheet(workbook, sheet =>
                {
                    var values = new object[differences.Length + 1, 2];
                    values[0, 0] = "Before";
                    values[0, 1] = "After";
                    for (int i = 0; i < differences.Length; i++)
                    {
                        values[i + 1, 0] = 20 + differences[i];
                        values[i + 1, 1] = 20;
                    }
                    sheet.Cells[largeSelection].ClearContents();
                    var pairs = sheet.Cells[$"H1:I{differences.Length + 1}"];
                    pairs.Value = values;
                    workbook.RangeSelection = pairs;
                    return true;
                });
            }
        }
        finally
        {
            WithWorksheet(workbook, sheet =>
            {
                sheet.Cells[largeSelection].ClearContents();
                workbook.RangeSelection = sheet.Cells[PairedSelection];
                return true;
            });
        }
    }

    private static void CheckPairedFenceTable()
    {
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "paired-outlier-fence.csv"));
        Check(rows.Length == 94 && rows[0] == "pairs,multiplier", "the calibration records a multiplier for every size from 8 to 100");
        for (int pairs = 8; pairs <= 100; pairs++)
        {
            var values = rows[pairs - 7].Split(',');
            Check(int.Parse(values[0], CultureInfo.InvariantCulture) == pairs
                && double.Parse(values[1], CultureInfo.InvariantCulture) == PairedOutlierFence.Multiplier(pairs),
                $"the {pairs}-pair fence equals the recorded R calibration");
        }
    }

    private sealed record PairedShapeOracle(double[] Differences, double Lower, double Upper,
        double Skewness, double ExcessKurtosis, int FarOutCount, double W, double P);

    private static async Task<Dictionary<string, PairedShapeOracle>> PairedShapeOracles()
    {
        const string name = "worksheet-paired-shape-oracle";
        await RunConversationR("", name, """
            set.seed(5136)
            centre <- qnorm(ppoints(101))
            boundary <- rep(c(1, 2, 3, 4, 0), 20)
            cases <- list(
              boundary100 = boundary, boundary101 = c(boundary, 1),
              normal101 = rnorm(101), normal500 = rnorm(500), normal5000 = rnorm(5000),
              heavy = c(centre, -4.5, 4.5),
              outlier = c(centre, 30),
              skew = c(rep(0, 40), rep(1, 65), rep(3.5, 10))
            )
            lines <- vapply(names(cases), function(name) {
              x <- cases[[name]]
              q <- quantile(x, c(.25, .75), type = 6)
              z <- x - mean(x)
              m2 <- mean(z^2)
              far <- sum(x < q[1] - 3*diff(q) | x > q[2] + 3*diff(q))
              sw <- if (length(x) <= 100) {
                with(shapiro.test(x), c(statistic, p.value))
              } else {
                c(NA_real_, NA_real_)
              }
              results <- c(q, mean(z^3)/m2^1.5, mean(z^4)/m2^2 - 3, far, sw, x)
              paste(c(name, sprintf("%.17g", results)), collapse = ",")
            }, "")
            writeLines(lines, "paired-shape.csv")
            cat("PAIRED_SHAPE_ORACLE_OK\n")
            """, "PAIRED_SHAPE_ORACLE_OK");
        var oracle = new Dictionary<string, PairedShapeOracle>();
        foreach (var line in File.ReadLines(Path.Combine(artifacts, name, "paired-shape.csv")))
        {
            var fields = line.Split(',');
            var values = fields.Skip(1)
                .Select(value => value == "NA" ? double.NaN : double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            oracle.Add(fields[0], new PairedShapeOracle(values[7..], values[0], values[1], values[2], values[3], (int)values[4], values[5], values[6]));
        }
        return oracle;
    }

    private static void CheckPairedShapeOracle(PairedDifferenceDiagnostics diagnostics, PairedShapeOracle expected, string label)
    {
        Check(diagnostics.Count == expected.Differences.Length && diagnostics.Shape is { } shape
            && Math.Abs(shape.LowerQuartile - expected.Lower) < 1e-10
            && Math.Abs(shape.UpperQuartile - expected.Upper) < 1e-10
            && shape.Skewness is double skewness && Math.Abs(skewness - expected.Skewness) < 1e-10
            && shape.ExcessKurtosis is double kurtosis && Math.Abs(kurtosis - expected.ExcessKurtosis) < 1e-10
            && shape.FarOutCount == expected.FarOutCount,
            $"{label}: quartiles, skewness, excess kurtosis and far-out count agree with base R");
    }

    private static async Task CheckLargePairedShapes(frmMain main, ScriptAIPane pane, frmSpreadsheetGear grid,
        WorkbookView workbook, string trace, Dictionary<string, PairedShapeOracle> oracle)
    {
        string[] before = WorksheetAnalysisDataTraffic(trace);
        string previousRange = null;
        foreach (string name in new[] { "heavy", "outlier", "skew", "normal101", "normal500", "normal5000" })
        {
            var expected = oracle[name];
            int n = expected.Differences.Length;
            string range = $"H1:I{n + 1}";
            WithWorksheet(workbook, sheet =>
            {
                if (previousRange != null) sheet.Cells[previousRange].ClearContents();
                var cells = new object[n + 1, 2];
                cells[0, 0] = "Before";
                cells[0, 1] = "After";
                for (int index = 0; index < n; index++)
                {
                    cells[index + 1, 0] = expected.Differences[index];
                    cells[index + 1, 1] = 0.0;
                }
                sheet.Cells[range].Value = cells;
                workbook.RangeSelection = sheet.Cells[range];
                return true;
            });
            previousRange = range;

            await CheckPairedDifferenceCard(pane, n, expectShapiroWilk: false);
            await WaitForWorksheetEvidence(pane, "paired-interval", "Shape check", "Skewness", "Excess kurtosis", "3× IQR");
            var actual = grid.CheckSelectedPairedDifferences();
            CheckPairedShapeOracle(actual, expected, name);
            bool normal = name.StartsWith("normal", StringComparison.Ordinal);
            if (normal)
            {
                await CheckPairedRecommendation(pane, n, "large");
                Check(!actual.HasPotentialOutlier && !actual.HasSkewSignal && !actual.Shape.HasHeavyTails,
                    $"{n} seeded normal differences have no substantial-departure warning");
            }
            else
            {
                await CheckPairedRecommendation(pane, n, null, "Wilcoxon signed ranks", "symmetric", "mean difference");
                Check(name switch
                {
                    "heavy" => actual.Shape.HasHeavyTails && !actual.HasPotentialOutlier && !actual.HasSkewSignal,
                    "outlier" => actual.HasPotentialOutlier && actual.Shape.FarOutCount == 1,
                    "skew" => actual.HasSkewSignal && !actual.HasPotentialOutlier,
                    _ => false
                }, $"{name}: the shape check flags the intended departure using all pairs");
                await ClickWorksheetAction(pane, "non-normal");
                await CheckWorksheetConfirmation(pane, "Wilcoxon signed ranks test", range, "A new report");
                await ClickWorksheetAction(pane, "sample");
                await WaitForWorksheetAction(pane, "paired-interval");
            }
        }
        WithWorksheet(workbook, sheet =>
        {
            sheet.Cells[previousRange].ClearContents();
            workbook.RangeSelection = sheet.Cells[PairedSelection];
            return true;
        });
        Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)) && !main.InOperation
            && !main.MdiChildren.OfType<frmReportWebView>().Any(),
            "all large-pair shape checks and Wilcoxon confirmations stay local and run no analysis");
    }

    private static async Task CheckPairedLocaleGuidance(frmMain main, ScriptAIPane pane, WorkbookView workbook, string trace)
    {
        const string range = "D17:E22";
        const string warning = "Cell D20 has a misplaced number separator.";
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            pane.NewConversation();
            WithWorksheet(workbook, sheet =>
            {
                sheet.Cells["D18:D20"].NumberFormat = "@";
                sheet.Cells[range].Value = new object[,]
                {
                    { "Before", "After" },
                    { "1,002", 1000 },
                    { "9,6", 6 },
                    { "4,5", 3 },
                    { 14, 9 },
                    { 17, 10 }
                };
                sheet.Cells["D19"].EntireRow.Hidden = true;
                workbook.RangeSelection = sheet.Cells[range];
                return true;
            });

            await WorksheetReply(pane, KnownPairedQuestion);
            var malformed = await WaitForWorksheetEvidence(pane, "paired-interval", warning);
            Check(!malformed.S("text").Contains("Cell D19", StringComparison.Ordinal)
                && !malformed.S("text").Contains("Local check found", StringComparison.Ordinal)
                && malformed.A("choices").Any(choice => choice.GetValue<string>() == "check"),
                "the initial paired decision names the visible text cell rather than the hidden physical row");

            string[] before = WorksheetAnalysisDataTraffic(trace);
            object beforeCheck = Field(pane, "worksheetAction");
            await ClickWorksheetAction(pane, "check");
            await Wait(() => !ReferenceEquals(beforeCheck, Field(pane, "worksheetAction")),
                "the paired decision processes the explicit local check");
            await WaitForWorksheetEvidence(pane, "paired-interval", warning);

            WithWorksheet(workbook, sheet =>
            {
                sheet.Cells["D20"].Value = "4.5";
                return true;
            });
            await CheckPairedDifferenceCard(pane, 4, expectShapiroWilk: true);
            var corrected = await WorksheetActionSnapshot(pane);
            Check(corrected.S("token") != malformed.S("token")
                && !corrected.S("text").Contains("misplaced number separator", StringComparison.Ordinal)
                && before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)) && !main.InOperation
                && !main.MdiChildren.OfType<frmReportWebView>().Any(),
                "correcting the number restores local paired diagnostics without another AI request or an analysis");
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = originalDefault;
            CultureInfo.CurrentCulture = originalCulture;
            WithWorksheet(workbook, sheet =>
            {
                sheet.Cells["D19"].EntireRow.Hidden = false;
                sheet.Cells[range].ClearContents();
                sheet.Cells["D18:D20"].NumberFormat = "General";
                workbook.RangeSelection = sheet.Cells[PairedSelection];
                return true;
            });
        }
    }

    private static async Task CheckOtherIndependentIntervalBranches(frmMain main, ScriptAIPane pane, WorkbookView workbook, string trace)
    {
        const string knownIndependentQuestion = "TRE_WORKSHEET_ANALYSIS_INDEPENDENT_INTERVAL";
        const string testCells = "J1:K103";

        void SetSamples(int n1, int n2, string selection)
        {
            WithWorksheet(workbook, sheet =>
            {
                object[,] values = new object[103, 2];
                values[0, 0] = "Independent sample x";
                values[0, 1] = "Independent sample y";
                for (int i = 0; i < n1; i++) values[i + 1, 0] = i + 2;
                for (int i = 0; i < n2; i++) values[i + 1, 1] = 2 * i + 1;
                sheet.Cells[testCells].Value = values;
                workbook.RangeSelection = sheet.Cells[selection];
                return true;
            });
        }

        async Task<JsonNode> StartKnownIndependence()
        {
            var reply = await WorksheetReply(pane, knownIndependentQuestion);
            Check(reply["pending"].S("state") == "awaiting_distribution"
                && !reply.B("invalidDesign") && !reply.B("extraDesign"),
                "a known independent design opens the sample-size and distribution card without staging a test");
            return await WaitForWorksheetAction(pane, "independent-interval");
        }

        async Task CheckIndependentCounts(int n1, int n2, bool expectShapiroWilk)
        {
            string previousToken = (await WorksheetActionSnapshot(pane)).S("token");
            await ClickWorksheetAction(pane, "check");
            string first = $"Column 1: n = {n1}", second = $"Column 2: n = {n2}";
            JsonNode checkedCard = null;
            for (int i = 0; i < 100; i++)
            {
                checkedCard = await WorksheetActionSnapshot(pane);
                if (Confirmed(checkedCard)) break;
                await Task.Delay(50);
            }
            string current = checkedCard.S("text");
            bool hasShapiroWilk = current.Contains("W =", StringComparison.Ordinal) && current.Contains("P =", StringComparison.Ordinal);
            Check(Confirmed(checkedCard) && hasShapiroWilk == expectShapiroWilk
                && !current.Contains("Selected columns, if they hold the two samples separately:", StringComparison.Ordinal),
                $"confirming the two sample columns displays counts {n1} and {n2} and removes the check button");

            bool Confirmed(JsonNode card) => card.S("kind") == "independent-interval" && card.S("token") != previousToken
                && card.S("text").Contains(first, StringComparison.Ordinal) && card.S("text").Contains(second, StringComparison.Ordinal)
                && !card.A("choices").Any(choice => choice.GetValue<string>() == "check");
        }

        try
        {
            SetSamples(7, 4, "J1");
            var ambiguous = await StartKnownIndependence();
            await CheckWorksheetSelectionLabel(pane, "Current cell: Fictional data!J1");
            Check(ambiguous.S("text").Contains("How many observations are there in each group?", StringComparison.Ordinal)
                && !ambiguous.S("text").Contains("Column 1: n =", StringComparison.Ordinal),
                "an ambiguous independent selection asks for group sizes");

            string[] before = WorksheetAnalysisDataTraffic(trace);
            const string smallSelection = "J1:K8";
            WithWorksheet(workbook, sheet =>
            {
                workbook.RangeSelection = sheet.Cells[smallSelection];
                return true;
            });
            await CheckWorksheetSelectionLabel(pane, "Selection: Fictional data!" + smallSelection);
            var smallCard = await WaitForWorksheetEvidence(pane, "independent-interval", "Column 1: n = 7", "Column 2: n = 4");
            Check(smallCard.A("choices").Select(value => value.GetValue<string>()).ToHashSet()
                .SetEquals(["large", "normal", "non-normal", "check", "back"]),
                "the independent numeric route offers sample-size and distribution choices");
            Check(smallCard.S("text").Contains("Selected columns, if they hold the two samples separately:", StringComparison.Ordinal)
                && smallCard.S("text").Contains("Use these as separate samples", StringComparison.Ordinal)
                && smallCard.S("text").Contains("Column 1: n = 7", StringComparison.Ordinal)
                && before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "selecting the two columns during the question updates local counts without sending an AI turn");
            await CheckIndependentCounts(7, 4, expectShapiroWilk: true);
            var diagnostics = ((frmSpreadsheetGear)main.ActiveMdiChild).CheckSelectedIndependentSamples();
            // R 4.6.2: shapiro.test(2:8) and shapiro.test(c(1, 3, 5, 7)).
            Check(diagnostics.First.Count == 7 && diagnostics.Second.Count == 4
                && diagnostics.First.ShapiroWilkW is double w1 && diagnostics.First.ShapiroWilkP is double p1
                && diagnostics.Second.ShapiroWilkW is double w2 && diagnostics.Second.ShapiroWilkP is double p2
                && Math.Abs(w1 - 0.978001629412) < 0.000005 && Math.Abs(p1 - 0.949288562354) < 0.000005
                && Math.Abs(w2 - 0.992912006801) < 0.000005 && Math.Abs(p2 - 0.971877057621) < 0.000005,
                "the two independent normality checks agree with R even when their usable counts differ");
            var checkedSmall = await WorksheetActionSnapshot(pane);
            await PostWorksheetAction(pane, checkedSmall.S("token"), "large");
            await WaitForWorksheetEvidence(pane, "independent-interval", "at most 100 observations in at least one sample");
            await ClickWorksheetAction(pane, "non-normal");
            await CheckWorksheetConfirmation(pane, "Mann-Whitney U (two-sample Wilcoxon) test", smallSelection, "A new report");
            Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "the non-normal independent branch stages Mann–Whitney without sending either sample");

            SetSamples(101, 4, testCells);
            await StartKnownIndependence();
            before = WorksheetAnalysisDataTraffic(trace);
            await CheckIndependentCounts(101, 4, expectShapiroWilk: true);
            var mixedSize = await WorksheetActionSnapshot(pane);
            await PostWorksheetAction(pane, mixedSize.S("token"), "large");
            await WaitForWorksheetEvidence(pane, "independent-interval", "at most 100 observations in at least one sample");
            Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "one independent group above 100 does not make both groups large or send an AI request");

            SetSamples(101, 102, testCells);
            await StartKnownIndependence();
            before = WorksheetAnalysisDataTraffic(trace);
            await CheckIndependentCounts(101, 102, expectShapiroWilk: false);
            await ClickWorksheetAction(pane, "large");
            await CheckWorksheetConfirmation(pane, "Unpaired two-sample z(normal distribution) test", testCells, "A new report");
            Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace))
                && !main.InOperation && !main.MdiChildren.OfType<frmReportWebView>().Any(),
                "the large independent branch stages the native Z test without sending data or running the test");
        }
        finally
        {
            WithWorksheet(workbook, sheet =>
            {
                sheet.Cells[testCells].ClearContents();
                workbook.RangeSelection = sheet.Cells[PairedSelection];
                return true;
            });
        }
    }

    private static async Task CheckWorksheetOpening(frmMain main, LearningService service, frmSpreadsheetGear grid, string trace)
    {
        string[] before = WorksheetAnalysisDataTraffic(trace);
        var pane = await OpenWorksheetAssistant(main, service, grid);
        var empty = await WaitForWorksheetAction(pane, "empty", ["example", "help"]);
        Check(empty.S("text").Contains("example workbook", StringComparison.OrdinalIgnoreCase)
            && empty.S("text").Contains("Workbook help", StringComparison.OrdinalIgnoreCase)
            && (await pane.Transcript.TextAsync()).Contains("This workbook is empty.", StringComparison.Ordinal),
            "an empty workbook offers the example and workbook help");

        var workbook = (WorkbookView)Field(grid, "workbookView");
        WithWorksheet(workbook, sheet => { sheet.Cells["H12"].NumberFormat = "0.00"; return true; });
        await WaitForWorksheetAction(pane, "empty", ["example", "help"]);
        WithWorksheet(workbook, sheet => { sheet.Cells["H12"].Value = 0; return true; });
        await CheckChooseMethodOpening(pane);
        await PostWorksheetAction(pane, empty.S("token"), "example");
        await Task.Delay(80);
        Check(ReferenceEquals(main.ActiveMdiChild, grid), "a stale empty-workbook choice cannot open the example after data is entered");

        WithWorksheet(workbook, sheet => { sheet.Cells["H12"].ClearContents(); return true; });
        await WaitForWorksheetAction(pane, "empty", ["example", "help"]);
        var otherSheet = WithWorksheet(workbook, sheet =>
        {
            var another = sheet.Workbook.Worksheets.Add();
            another.Cells["E3"].Value = "Other worksheet value";
            workbook.ActiveWorksheet = sheet;
            return another;
        });
        await CheckChooseMethodOpening(pane);
        WithWorksheet(workbook, _ => { otherSheet.Cells["E3"].ClearContents(); return true; });
        await WaitForWorksheetAction(pane, "empty", ["example", "help"]);
        var help = (HelpService)Field(SdApplication.SoleInstance, "htmlHelp");
        await ClickWorksheetAction(pane, "help");
        try
        {
            await Wait(() => help.IsVisible && help.View?.Browser.Source is Uri source
                && source.AbsolutePath.EndsWith("/basics/worksheet.htm", StringComparison.Ordinal),
                "the workbook help choice opens the local Workbook help topic", 20000);
        }
        finally { help.Hide(); }
        await ClickWorksheetAction(pane, "example");
        await Wait(() => main.ActiveMdiChild is frmSpreadsheetGear { Text: "Example workbook" },
            "the example choice opens the bundled workbook");
        var example = (frmSpreadsheetGear)main.ActiveMdiChild;
        try
        {
            await CheckWorksheetPaneBinding(main, pane, example, shared: false);
            await CheckChooseMethodOpening(pane);
            Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "opening workbook help and the example works without sending a question or workbook data");
            await ClickWorksheetAction(pane, "guide");
            const string guidance = "Help me choose a statistical method.";
            await Wait(() => Field(pane, "pending") == null && WorksheetAnalysisDataTraffic(trace).Length > before.Length,
                "the method guidance choice starts a conversation");
            var request = LastWorksheetRequest(trace);
            var messages = request.A("conversation");
            Check(messages.Count == 1 && messages[0].S("text") == guidance
                && !request.S("referenceContext").Contains(example.Text, StringComparison.Ordinal) && !main.InOperation,
                "the method guidance choice sends a general question and leaves the workbook private under level 2");
            await CheckWorksheetPickerConfirmation(main, pane, "calculator", "", reportExpected: false);
            var exampleView = (WorkbookView)Field(example, "workbookView");
            WithWorksheet(exampleView, sheet =>
            {
                var parametric = sheet.Workbook.Worksheets["Parametric"];
                exampleView.ActiveWorksheet = parametric;
                exampleView.RangeSelection = parametric.Cells["A:B"];
                return true;
            });
            await WorksheetReply(pane, KnownPairedQuestion);
            before = WorksheetAnalysisDataTraffic(trace);
            await WaitForWorksheetAction(pane, "paired-interval");
            await CheckPairedDifferenceCard(pane, 9, expectShapiroWilk: true);
            await CheckPairedRecommendation(pane, 9, "normal", "Shapiro–Wilk", "limited evidence");
            var pefr = example.CheckSelectedPairedDifferences();
            // R 4.6.2 shapiro.test() on the nine bundled PEFR Before minus PEFR After values.
            Check(pefr.Count == 9 && pefr.ShapiroWilkW is double w && pefr.ShapiroWilkP is double p
                && Math.Abs(w - 0.9393052) < 0.000005 && Math.Abs(p - 0.5746038) < 0.000005
                && before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "checking whole columns A:B of the bundled PEFR example agrees with R and sends nothing to the AI");
        }
        finally { CloseTestDocument(example); }
    }

    private static async Task CheckChooseMethodOpening(ScriptAIPane pane)
    {
        var offer = await WaitForWorksheetAction(pane, "compare", ["guide", "browse"]);
        Check(offer.S("text").Contains("Help me choose a method", StringComparison.OrdinalIgnoreCase)
            && offer.S("text").Contains("Browse methods", StringComparison.OrdinalIgnoreCase)
            && !offer.S("text").Contains("Compare two means", StringComparison.OrdinalIgnoreCase),
            "a populated workbook offers help choosing a method without singling out means");
    }

    private static async Task CheckWorksheetClarification(frmMain main, ScriptAIPane pane, string trace,
        frmReportWebView report, Action<string> chooseRange)
    {
        const string question = "TRE_WORKSHEET_ANALYSIS_CLARIFY";
        string[] goals = ["means", "ordered", "proportions", "association", "agreement", "unsure"];
        string[] scales = ["quantitative", "ordinal", "binary", "nominal", "unsure"];
        string[] designs = ["paired", "independent", "repeated", "unsure"];
        int firstNewLine = WorksheetAnalysisDataTraffic(trace).Length;
        int beforeResults = await WorksheetAnalysisResultCount(report);

        pane.NewConversation();
        var reply = await WorksheetReply(pane, question);
        Check(reply.B("available") && reply.B("accepted") && !reply.B("invalid") && !reply.B("extra"),
            "ordinary AI can ask clarifying questions through the typed tool without choosing workbook content");
        var goal = await WaitForWorksheetAction(pane, "clarify", goals);
        Check(goal.S("text").Contains("agreement", StringComparison.OrdinalIgnoreCase)
            && !goal.S("text").Contains("Continue to analysis", StringComparison.OrdinalIgnoreCase),
            "an unclear goal shows meaningful choices before any method confirmation");

        await ClickWorksheetAction(pane, "agreement");
        await WaitForWorksheetAction(pane, "clarify", scales);
        Check(LastWorksheetRequest(trace).A("conversation").Last().S("text") == "I want to assess agreement between measurements.",
            "the goal click answers the question in the same conversation");
        int beforeOldClick = WorksheetAnalysisDataTraffic(trace).Count(line => JsonNode.Parse(line).S("method") == "turn/start");
        await PostWorksheetAction(pane, goal.S("token"), "means");
        await WaitForWorksheetAction(pane, "clarify", scales);
        Check(WorksheetAnalysisDataTraffic(trace).Count(line => JsonNode.Parse(line).S("method") == "turn/start") == beforeOldClick,
            "an old clarification button cannot replace the current question or send another answer");

        await ClickWorksheetAction(pane, "ordinal");
        await WaitForWorksheetAction(pane, "clarify", designs);
        Check(LastWorksheetRequest(trace).A("conversation").Last().S("text") == "My outcome is an ordered score or category.",
            "the scale click answers the next question without assuming means");

        await ClickWorksheetAction(pane, "paired");
        var confirmation = await WaitForWorksheetAction(pane, "confirm");
        var request = LastWorksheetRequest(trace);
        Check(request.A("conversation").Last().S("text") == "The measurements are matched on the same people or units."
            && confirmation.S("text").Contains("Kappa", StringComparison.OrdinalIgnoreCase)
            && !confirmation.S("text").Contains("Student t test", StringComparison.OrdinalIgnoreCase),
            "matched ordinal agreement reaches a native Kappa proposal after all three choices");
        Check(!main.InOperation && await WorksheetAnalysisResultCount(report) == beforeResults,
            "answering clarification questions and staging a method do not start or publish an analysis");
        string[] traffic = WorksheetAnalysisDataTraffic(trace).Skip(firstNewLine).ToArray();
        Check(traffic.Count(line => JsonNode.Parse(line).S("method") == "turn/start") == 4
            && traffic.All(line => !line.Contains(PrivateWorksheet, StringComparison.Ordinal) && !line.Contains(PrivateCell, StringComparison.Ordinal)),
            "the three clicks send one answer each without sharing the unapproved worksheet");

        pane.NewConversation();
        chooseRange("A1");
        pane.ShareWorkbook.Checked = true;
        pane.Acknowledge.Checked = true;
        try
        {
            await WorksheetReply(pane, question);
            await WaitForWorksheetAction(pane, "clarify", goals);
            await CheckWorksheetSelectionLabel(pane, "Current cell: Fictional data!A1");
            Check(SharedSelection(LastWorksheetRequest(trace)) == "A1",
                "the initial approved clarification uses the current cell");
            string[] beforeRangeChange = WorksheetAnalysisDataTraffic(trace);
            chooseRange(PairedSelection);
            await CheckWorksheetSelectionLabel(pane, "Selection: Fictional data!" + PairedSelection);
            await WaitForWorksheetAction(pane, "clarify", goals);
            Check(beforeRangeChange.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
                "selecting data during clarification sends nothing even when workbook sharing is approved");

            await ClickWorksheetAction(pane, "agreement");
            await WaitForWorksheetAction(pane, "clarify", scales);
            var latest = LastWorksheetRequest(trace);
            Check(WorksheetAnalysisDataTraffic(trace).Skip(beforeRangeChange.Length)
                    .Count(line => JsonNode.Parse(line).S("method") == "turn/start") == 1
                && latest.A("conversation").Last().S("text") == "I want to assess agreement between measurements."
                && SharedSelection(latest) == PairedSelection && !main.InOperation
                && await WorksheetAnalysisResultCount(report) == beforeResults,
                "an unanswered clarification sends one answer with the current approved selection and starts no analysis");
        }
        finally
        {
            pane.ShareWorkbook.Checked = false;
        }

        static string SharedSelection(JsonNode request)
        {
            var documents = JsonNode.Parse(request.S("referenceContext")).A("documents");
            return documents.Count == 1 && documents[0].S("title") == PrivateWorksheet
                ? documents[0]["worksheet"].S("selection").Replace("$", "", StringComparison.Ordinal)
                : "";
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
        await CheckChooseMethodOpening(pane);
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

        // Cochran Q reports from a conditional branch. Univariate Summary can write to the worksheet from a branch.
        var cochranQ = target.Propose(available.Single(command => command.Operation == "CochranQ"));
        Check(cochranQ.DestinationTitle == "A new report",
            "the Cochran Q confirmation names a report destination for its nested report step");
        var summary = target.Propose(available.Single(command => command.Operation == "UnivariateSummary" && command.Groups[0] == "Analysis"));
        Check(summary.Effect.Contains("change worksheet data", StringComparison.OrdinalIgnoreCase),
            "the Univariate Summary confirmation warns about worksheet changes for its nested output step");

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
                    choices: [...card.querySelectorAll('button[data-action="worksheet-analysis"]')].map(button => button.dataset.choice),
                    recommended: [...card.querySelectorAll('button[data-recommended="true"]')]
                        .map(button => ({ choice: button.dataset.choice, label: button.innerText }))
                } : null;
            })()
            """));
    }

    private static async Task<JsonNode> WaitForWorksheetAction(ScriptAIPane pane, string kind, string[] choices = null)
    {
        JsonNode action = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            action = await WorksheetActionSnapshot(pane);
            if (Matches(action)) break;
            await Task.Delay(50);
        }
        Check(Matches(action), choices == null ? $"the conversation shows the {kind} action"
            : $"the {kind} action offers {string.Join(", ", choices)}");
        return action;

        bool Matches(JsonNode card) => card.S("kind") == kind && card.S("token").Length > 0
            && (choices == null || card.A("choices").Select(choice => choice.GetValue<string>()).SequenceEqual(choices));
    }

    private static Task PostWorksheetAction(ScriptAIPane pane, string token, string choice) =>
        pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("window.chrome.webview.postMessage(" + JsonSerializer.Serialize(new
        {
            action = "worksheetAnalysis", token, choice, context = (int)Field(pane.Transcript, "context")
        }) + ")");

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
        string text = action.S("text");
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

    private static void CheckWorksheetMethodSelectionContext(string trace, bool openingQuestion, bool guidanceOnly, params string[] privateTexts)
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
        string turnHeader = firstLineEnd > 0 ? turnMessage[..firstLineEnd] : "";
        const string askOne = "Ask the most important unanswered question before naming or staging a test.";
        string[] askTogether =
        [
            "cannot see earlier messages",
            "ask for all missing non-confidential details together",
            "restate the full question with those details in one message",
            "pairing is unclear",
            "pass design paired to the same tool",
            "pass design independent",
            "buttons collect the next details locally"
        ];
        bool includesAllTogether(string text) => askTogether.All(rule => text.Contains(rule, StringComparison.Ordinal));
        Check(guidanceOnly
                ? includesAllTogether(prompt) && includesAllTogether(turnHeader)
                    && !prompt.Contains(askOne, StringComparison.Ordinal) && !turnHeader.Contains(askOne, StringComparison.Ordinal)
                : prompt.Contains(askOne, StringComparison.Ordinal) && turnHeader.Contains(askOne, StringComparison.Ordinal)
                    && !includesAllTogether(prompt) && !includesAllTogether(turnHeader),
            guidanceOnly
                ? "restricted worksheet instructions ask for all missing details in a single reply and a self-contained follow-up"
                : "ordinary worksheet instructions ask the most important unanswered question");

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
                && prompt.Contains("Use the clickable clarification if available. The opening exchange does not need worksheet reads or Help searches.", StringComparison.Ordinal)
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
        Check(prompt.Contains("only the workbook named in the Share open workbook data checkbox", StringComparison.Ordinal)
            && prompt.Contains("the user brings it to the front and the chat follows", StringComparison.Ordinal)
            && prompt.Contains("without asking the user to tick or confirm the checkbox again", StringComparison.Ordinal)
            && !prompt.Contains("Share workbook data with AI", StringComparison.Ordinal),
            "the assistant explains the active-workbook scope and recognizes an already confirmed workbook");
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
        string[] evidenceRules =
        [
            "Ask only for the remaining details before recommending or staging a method.",
            "Selection dimensions and partial reads cannot establish the whole-sample counts or distribution.",
            "For three or more independent groups, establish per-group sizes",
            "the shape and linearity of the relationship",
            "the residuals and variance where they affect inference",
            "sparse cells, expected counts or discordant pairs"
        ];
        Check(turnHeader.Contains("Check sample size, distribution or other conditions when they could change the method.", StringComparison.Ordinal)
            && evidenceRules.All(rule => prompt.Contains(rule, StringComparison.Ordinal)),
            "the worksheet prompts cover method-dependent sample size, distribution and other conditions beyond paired data");
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
        CheckWorksheetMethodSelectionContext(trace, openingQuestion, guidanceOnly: true, PrivateWorksheet, PrivateCell);
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

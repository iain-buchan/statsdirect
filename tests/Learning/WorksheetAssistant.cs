using StatsDirect.UI;
using StatsDirect.UI.Learning;
using SpreadsheetGear.Windows.Forms;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static ToolStrip WorksheetRibbon(frmMain main) => (ToolStrip)Field(main, "toolStrip");

    private static async Task<ScriptAIPane> OpenWorksheetAssistant(frmMain main, LearningService service, frmSpreadsheetGear grid)
    {
        grid.Activate();
        await Wait(() => main.ActiveMdiChild == grid && WorksheetRibbon(main).Items["aiToolStripButton"] is { Available: true, Enabled: true }, "the worksheet ribbon exposes Ask AI");
        WorksheetRibbon(main).Items["aiToolStripButton"]!.PerformClick();
        await Wait(() => Field(service, "ai") is ScriptAIPane { IsOpen: true }, "Ask AI opens the existing assistant");
        var pane = (ScriptAIPane)Field(service, "ai");
        await pane.Transcript.SettledAsync().WaitAsync(TimeSpan.FromSeconds(40));
        if (main.ActiveMdiChild != grid) grid.Activate();
        await Wait(() => main.ActiveMdiChild == grid,
            "the worksheet and its sharing context are active before the test sends a question");
        return pane;
    }

    private static ChatGPTTutor WorksheetMockTutor(string directory, Func<AISecurityLevel> securityLevel, string analysisTrace = null)
    {
        string node = Environment.GetEnvironmentVariable("STATSDIRECT_TEST_NODE")
            ?? throw new InvalidOperationException("Set STATSDIRECT_TEST_NODE to the Node executable.");
        string server = Path.Combine(AppContext.BaseDirectory, "mock-server.mjs");
        return new ChatGPTTutor(node, directory, Path.Combine(AppContext.BaseDirectory, "Tutor"),
            analysisTrace == null ? [server] : [server, "--worksheet-analysis-probe", analysisTrace])
        {
            ReadSecurityLevel = securityLevel,
            ReadWorkspaceId = () => null
        };
    }

    private static async Task RunWorksheetAssistantFixture(frmMain main)
    {
        var policy = AISecurityLevel.Unrestricted;
        string trace = Path.Combine(root, "worksheet-consent-wire.jsonl");
        using var tutor = WorksheetMockTutor(Path.Combine(root, "worksheet-account"), () => policy, trace);
        using var service = new LearningService(main, Path.Combine(root, "Worksheet Learning"), tutor);
        typeof(frmMain).GetField("learning", Members)!.SetValue(main, service);
        try
        {
            await TestWorksheetAssistant(main, service, level => policy = level, trace);
            await TestWorksheetConsentAcrossPanes(main, service, trace);
            await TestWorksheetActivationFromCommands(main, service, trace);
        }
        finally { typeof(frmMain).GetField("learning", Members)!.SetValue(main, null); }
    }

    private static async Task ShowWorksheetAssistantDemo(frmMain main)
    {
        var tutor = WorksheetMockTutor(Path.Combine(root, "preview-worksheet-account"), () => AISecurityLevel.Unrestricted);
        var service = new LearningService(main, Path.Combine(root, "Preview Learning"), tutor);
        typeof(frmMain).GetField("learning", Members)!.SetValue(main, service);
        main.FormClosed += (_, _) => { service.Dispose(); tutor.Dispose(); };
        var grid = (frmSpreadsheetGear)main.CreateGrid("Worksheet AI prototype (simulated replies)");
        grid.LoadLearningExample(new JsonArray(new JsonObject { ["title"] = "Age", ["values"] = new JsonArray(26, 53, 68) }));
        await tutor.SignInAsync();
        await Wait(() => tutor.Email == "mock@example.invalid", "the prototype uses a local simulated AI account");
        var pane = await OpenWorksheetAssistant(main, service, grid);
        pane.ShareWorkbook.Checked = true;
        pane.Acknowledge.Checked = true;
        pane.Composer.Text = "What is the mean age?";
        await pane.SendAsync();
        await pane.Transcript.SettledAsync();
        Check((await pane.Transcript.TextAsync()).Contains("The mean age is 49"),
            "the preview explains the result from the shared sample ages");
    }

    private static async Task<JsonNode> WorksheetReply(ScriptAIPane pane, string prompt)
    {
        var messages = (JsonArray)Field(Field(pane, "chat"), "Messages");
        int before = messages.Count(message => message.S("role") == "assistant");
        pane.Composer.Text = prompt;
        await pane.SendAsync();
        if (messages.Count(message => message.S("role") == "assistant") != before + 1)
            throw new InvalidOperationException($"The mock reply to {prompt} did not arrive. {((Label)Field(pane, "status")).Text}");
        return JsonNode.Parse(messages.Last(message => message.S("role") == "assistant").S("text"));
    }

    private static bool HasWorksheetWriteTool(JsonNode response) =>
        response.A("toolNames").Any(tool => tool.GetValue<string>() == "statsdirect_propose_worksheet_edit");

    private static bool ReadFullPackagedHelp(JsonNode response)
    {
        var fullHelp = response["fullHelp"];
        return fullHelp.S("topic") == "references/reference_list.htm"
            && fullHelp.S("url") == "https://www.statsdirect.com/help/references/reference_list.htm"
            && fullHelp.B("introductoryText") && fullHelp.B("continued") && fullHelp.B("invalidRejected");
    }

    private static async Task TestWorksheetRAnswer(frmMain main, LearningService service, frmSpreadsheetGear grid, ScriptAIPane pane, string trace)
    {
        pane.Composer.Text = "WORKSHEET_AI_R_PRIOR_ANSWER";
        await pane.SendAsync();
        pane.Composer.Text = "WORKSHEET_AI_R_ANSWER";
        await pane.SendAsync();
        await pane.Transcript.SettledAsync();
        string transcript = await pane.Transcript.TextAsync();
        Check(transcript.Contains("The median age") && transcript.Contains("The mean age is 49"),
            "the assistant answers successive questions about the approved values in plain language");

        var browser = pane.Transcript.Browser.CoreWebView2;
        string check = """
            (() => {
                const sections = [...document.querySelectorAll('.message.assistant details.r-code-disclosure')];
                return sections.length === 2 && sections.every(section => !section.open && section.querySelector('[data-code=open]'));
            })()
            """;
        Check(await browser.ExecuteScriptAsync(check) == "true",
            "the answer keeps R code and its Open action collapsed by default");

        string evidence = await browser.ExecuteScriptAsync("""
            (() => {
                const sections = [...document.querySelectorAll('.message.assistant details.worksheet-evidence')].slice(-2);
                return sections.length === 2 && sections.every(section => !section.open &&
                    section.querySelector('summary')?.textContent === 'Data used' &&
                    section.querySelector('li')?.textContent === 'Worksheet assistant test · Fictional data · A2:A4 (part of the worksheet’s used range)');
            })()
            """);
        Check(evidence == "true", "both data-based replies display their own app-generated, collapsed evidence");
        var wire = LastWorksheetRequest(trace).A("conversation");
        Check(wire.Any(row => row.S("role") == "assistant" && row.S("text").StartsWith("The median age", StringComparison.Ordinal))
            && wire.All(row => row["worksheetReads"] == null),
            "a later question can carry the preceding reply without resending its local evidence metadata");

        var view = (WorkbookView)Field(grid, "workbookView");
        WithWorksheet(view, sheet => { sheet.Cells["A2"].Value = 999; return true; });
        int before = service.Documents.OfType<frmScript>().Count();
        await browser.ExecuteScriptAsync("""
            (() => {
                const section = [...document.querySelectorAll('.message.assistant details.r-code-disclosure')].at(-1);
                section.open = true;
                section.querySelector('[data-code=open]').click();
            })()
            """);
        await Wait(() => service.Documents.OfType<frmScript>().Count() == before + 1,
            "opening R code from the disclosure creates a script for review");

        var script = (frmScript)main.ActiveMdiChild;
        try
        {
            Check(!script.LearningRunning && script.LearningOutput.Length == 0,
                "opening an assistant's R script does not execute it");
            await RunConversationR(script.LearningScript, "worksheet-ai-answer",
                "stopifnot(identical(age, c(26, 53, 68)), mean(age) == 49, !exists('earlier_answer_only'))\ncat('WORKSHEET_AI_R_OK\\n')",
                "WORKSHEET_AI_R_OK");
            await TestWorksheetRReport(main, script);
        }
        finally
        {
            CloseTestDocument(script);
            await Wait(() => script.IsDisposed, "the reviewed worksheet R script closes");
            await Wait(() => !((System.Windows.Forms.Timer)Field(main, "postTabTimer")).Enabled,
                "closing the script finishes the deferred window-tab activation");
            WithWorksheet(view, sheet => { sheet.Cells["A2"].Value = 26; return true; });
            grid.Activate();
        }
    }

    private static T WithWorksheet<T>(WorkbookView view, Func<SpreadsheetGear.IWorksheet, T> action)
    {
        view.GetLock();
        try { return action(view.ActiveWorksheet); }
        finally { view.ReleaseLock(); }
    }

    private static async Task TestWorksheetReadEvidence(frmSpreadsheetGear grid)
    {
        var context = new QuestionContext([grid], true, CancellationToken.None, trackWorksheetReads: true);
        var separated = new JsonObject
        {
            ["documentId"] = grid.Id(), ["firstRow"] = 2, ["lastRow"] = 4, ["columns"] = new JsonArray(2, 0)
        };
        var data = await context.CallAsync("statsdirect_read_data", separated);
        Check(data["worksheetReads"] == null && data["ranges"] == null && data["partial"] == null,
            "worksheet evidence is kept locally and outside the provider's tool response");
        var reads = context.WorksheetReads;
        Check(reads.Count == 1 && reads[0].B("partial") && reads[0].A("ranges").Select(range => range.GetValue<string>())
            .SequenceEqual(["A2:A4", "C2:C4"]), "disjoint evidence describes only the requested columns");
        await context.CallAsync("statsdirect_read_data", separated);
        Check(context.WorksheetReads.Count == 1, "repeating a read keeps one evidence entry");

        var invalid = (JsonObject)separated.DeepClone();
        invalid["lastRow"] = 4002;
        await Reject(() => context.CallAsync("statsdirect_read_data", invalid),
            "an oversize read cannot add worksheet evidence");
        Check(context.WorksheetReads.Count == 1, "a failed read adds no worksheet evidence");
        await context.CallAsync("statsdirect_read_data", new JsonObject
        {
            ["documentId"] = grid.Id(), ["firstRow"] = 1, ["lastRow"] = 4, ["columns"] = new JsonArray(0, 1)
        });
        reads = context.WorksheetReads;
        Check(reads.Count == 2 && !reads[1].B("partial") && reads[1].A("ranges")[0]?.GetValue<string>() == "A1:B4",
            "a read of the whole used worksheet range is not marked as partial");
    }

    private static async Task TestWorksheetAssistant(frmMain main, LearningService service, Action<AISecurityLevel> setSecurity, string trace)
    {
        var previous = main.ActiveMdiChild;
        var other = (frmSpreadsheetGear)main.CreateGrid("Other private test workbook");
        var grid = (frmSpreadsheetGear)main.CreateGrid("Worksheet assistant test");
        var view = (WorkbookView)Field(grid, "workbookView");
        grid.LoadLearningExample(new JsonArray(new JsonObject { ["title"] = "Age", ["values"] = new JsonArray(26, 53, 68) }));
        WithWorksheet(view, sheet =>
        {
            sheet.Cells["B1"].Value = "Original heading";
            sheet.Cells["B2"].Formula = "=1+1";
            sheet.Cells["B3"].Value = "Existing cell";
            return true;
        });
        ScriptAIPane pane = null;
        try
        {
            string help = Path.Combine(root, "oversized-worksheet-help");
            Directory.CreateDirectory(help);
            using (var index = File.Create(Path.Combine(help, "search-index.js"))) index.SetLength(2_000_001);
            Check(WorksheetAIHelp.Search(help, "worksheet").Count == 0,
                "an oversized packaged Help index leaves worksheet questions available");

            await TestWorksheetReadEvidence(grid);

            var toolbar = WorksheetRibbon(main);
            await Wait(() => main.ActiveMdiChild == grid && toolbar.Items["learnToolStripButton"] is { Available: true, Enabled: true }, "the worksheet ribbon exposes Learn");
            toolbar.Items["learnToolStripButton"]!.PerformClick();
            await Wait(() => service.View is { IsDisposed: false } && main.ActiveMdiChild == service.View, "Learn opens the existing course from the worksheet ribbon");
            await service.View.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40));
            service.View.Close();

            if (service.Tutor.Email == null) await service.Tutor.SignInAsync();
            await service.Tutor.RefreshAccountAsync();
            await Wait(() => service.Tutor.Email == "mock@example.invalid", "the worksheet assistant test is connected to a mock account");
            pane = await OpenWorksheetAssistant(main, service, grid);
            Check(pane.Composer.Text.Length == 0 && pane.ShareWorkbook.Visible && pane.ShareWorkbook.Enabled
                && pane.ShareWorkbook.Text == $"Share open workbook data (“{grid.Text}”)" && !pane.ShareWorkbook.Checked
                && pane.Acknowledge.Visible && !pane.Acknowledge.Enabled && !pane.Acknowledge.Checked,
                "the worksheet pane names its workbook and displays the same sharing and confirmation controls as Script AI");
            await pane.Transcript.SettledAsync();
            Check((await pane.Transcript.TextAsync()).Contains(WorksheetOpeningText, StringComparison.Ordinal)
                && WorksheetAnalysisDataTraffic(trace).Select(line => JsonNode.Parse(line)).All(request => request.S("method") != "turn/start"),
                "the welcome appears locally without sending a question");

            const string acceptance = "yes please";
            pane.Composer.Text = acceptance;
            await pane.SendAsync();
            await pane.Transcript.SettledAsync();
            var firstRequest = LastWorksheetRequest(trace);
            var firstConversation = firstRequest.A("conversation");
            var firstReference = JsonNode.Parse(firstRequest.S("referenceContext"));
            Check(firstConversation.Count == 1 && firstConversation[0].S("role") == "user"
                && firstConversation[0].S("text") == acceptance && firstReference.A("documents").Count == 0,
                "the first affirmative reply is sent without workbook details");
            CheckWorksheetMethodSelectionContext(trace, true, guidanceOnly: false, grid.Text, other.Text, "Existing cell");
            Check((await pane.Transcript.TextAsync()).Contains("What would you like to find out from your data?", StringComparison.Ordinal),
                "the welcome acceptance can lead straight to a question about the analysis goal");
            Check((await pane.Transcript.TextAsync()).Contains("General guidance · No worksheet cells read for this reply", StringComparison.Ordinal),
                "the welcome's answer explains when it has read no worksheet cells");

            var questionOnly = await WorksheetReply(pane, "WORKSHEET_AI_WITHOUT_SHARING");
            var nextRequest = LastWorksheetRequest(trace);
            Check(nextRequest.A("conversation").Any(message => message.S("role") == "user" && message.S("text") == acceptance),
                "later questions continue the conversation from the opening offer");
            CheckWorksheetMethodSelectionContext(trace, false, guidanceOnly: false, grid.Text, other.Text, "Existing cell");
            Check(questionOnly.S("help").Contains("worksheet", StringComparison.OrdinalIgnoreCase)
                && !questionOnly.B("read") && !HasWorksheetWriteTool(questionOnly),
                "question-only mode retrieves packaged Help and exposes neither workbook reads nor workbook edits");
            Check(ReadFullPackagedHelp(questionOnly),
                "question-only AI can continue beyond a Help excerpt on a non-method topic and rejects unknown topics");
            service.RevokeAISharing();
            CheckWorksheetSharing(pane, service, grid, trace, allow: false);
            var unconfirmed = await WorksheetReply(pane, "WORKSHEET_AI_AFTER_UNCONFIRMED");
            Check(unconfirmed.A("ids").Count == 0 && !unconfirmed.B("read") && !service.Sharing.Accepts([other.Id()]),
                "the next question has no workbook scope when the sharing checkbox was left unconfirmed");
            CheckWorksheetSharing(pane, service, grid, trace);
            Check(!service.Sharing.Accepts([other.Id()]),
                "confirmation approves only the bound workbook when another workbook is open");
            pane.ShareWorkbook.Checked = false;
            Check(!pane.Acknowledge.Enabled && service.Sharing.Accepts([grid.Id()]),
                "turning sharing off suspends the scope while preserving session consent");
            CheckWorksheetSharing(pane, service, grid, trace, reuse: true);

            grid.Activate();
            await Wait(() => main.ActiveMdiChild == grid, "the worksheet assistant returns to its source worksheet");
            int version = grid.LearningVersion;
            var result = await WorksheetReply(pane, "WORKSHEET_AI_DEPRECATED_EDIT");
            Check(result.A("ids").Select(id => id.GetValue<string>()).SequenceEqual([grid.Id()]) && result.B("read"),
                "the assistant reads only the named workbook after confirmation");
            Check(!HasWorksheetWriteTool(result) && result.B("editAttempted") && !result.B("editAccepted"),
                "the former worksheet write tool is absent and the mock cannot call it");
            Check(grid.LearningVersion == version && WithWorksheet(view, sheet =>
                Equals(sheet.Cells["B1"].Value, "Original heading") && sheet.Cells["B2"].Formula == "=1+1"
                && Equals(sheet.Cells["B3"].Value, "Existing cell") && Equals(sheet.Cells["A2"].Value, 26d)),
                "the attempted AI write leaves cell values, formula and workbook revision intact");

            await TestWorksheetRAnswer(main, service, grid, pane, trace);
            await CheckWorksheetPaneBinding(main, pane, grid, shared: true);

            pane.ShareWorkbook.Checked = false;
            const string afterSharing = "WORKSHEET_AI_HISTORY_OFF";
            var noSharing = await WorksheetReply(pane, afterSharing);
            var turn = WorksheetAnalysisDataTraffic(trace).Select(line => JsonNode.Parse(line))
                .Last(request => request.S("method") == "turn/start");
            string payload = turn["params"].A("input")[0].S("text");
            var conversation = JsonNode.Parse(payload[(payload.IndexOf('\n') + 1)..]).A("conversation");
            Check(conversation.Count == 1 && conversation[0].S("role") == "user" && conversation[0].S("text") == afterSharing
                && noSharing.A("ids").Count == 0 && !noSharing.B("read") && !HasWorksheetWriteTool(noSharing),
                "turning off sharing sends only the new question and excludes earlier replies and worksheet tools");
            CheckWorksheetSharing(pane, service, grid, trace, reuse: true);

            setSecurity(AISecurityLevel.DataSharingRestricted);
            pane.UpdateConnection();
            Check(!pane.ShareWorkbook.Enabled && !pane.Acknowledge.Visible && !pane.Acknowledge.Enabled,
                "restricted mode blocks workbook sharing and hides consent");
            var restricted = await WorksheetReply(pane, "WORKSHEET_AI_RESTRICTED");
            Check(restricted.S("help").Contains("worksheet", StringComparison.OrdinalIgnoreCase) && !restricted.B("read")
                && !HasWorksheetWriteTool(restricted), "data-sharing policy keeps Help available without sending worksheet data");
            Check(ReadFullPackagedHelp(restricted),
                "guidance-only AI can read the full packaged Help without gaining workbook access");
            await pane.Transcript.SettledAsync();
            Check(await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync(
                "[...document.querySelectorAll('.message.assistant .worksheet-evidence')].at(-1)?.textContent.includes('General guidance')") == "true",
                "the restricted reply does not display worksheet evidence from previous replies");

            setSecurity(AISecurityLevel.AIRestricted);
            pane.UpdateConnection();
            Check(!pane.Send.Enabled, "organisation AI policy disables AI submission");
        }
        finally
        {
            setSecurity(AISecurityLevel.Unrestricted);
            if (pane != null)
            {
                pane.ShareWorkbook.Checked = false;
                pane.Hide();
            }
            CloseTestDocument(grid);
            CloseTestDocument(other);
            previous?.Activate();
        }
    }
}

using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static async Task TestWorksheetConsentAcrossPanes(frmMain main, LearningService service, string trace)
    {
        const string workbookValue = "CONSENT_WORKBOOK_CELL";
        const string scriptValue = "CONSENT_SCRIPT_CONTENT";
        const string reportValue = "CONSENT_REPORT_CONTENT";
        var workbook = (frmSpreadsheetGear)main.CreateGrid("Consent test workbook");
        workbook.LoadLearningExample(new JsonArray(new JsonObject
        {
            ["title"] = "Test data", ["values"] = new JsonArray(12, 24)
        }));
        WithWorksheet((SpreadsheetGear.Windows.Forms.WorkbookView)Field(workbook, "workbookView"), sheet =>
        {
            sheet.Cells["A2"].Value = workbookValue;
            return true;
        });
        var script = service.OpenScript($"x <- '{scriptValue}'\n", "Consent test script", false);
        var report = (frmReportWebView)main.CreateReport();
        var previousConfirm = service.ConfirmSharing;
        ScriptAIPane pane = null;
        try
        {
            await report.LearningReport.Ready.WaitAsync(TimeSpan.FromSeconds(40));
            string result = await report.LearningReport.AppendAsync("<p>" + reportValue + "</p>", "Consent test result", "", 0);
            var sources = new (string Name, StatsDirectForm Document, string Value, Action Open)[]
            {
                ("Script", script, scriptValue, () => service.ShowScriptAI(script)),
                ("Report", report, reportValue, () => service.ShowReportAI(report, result))
            };

            foreach (var source in sources)
            {
                async Task OpenSource()
                {
                    source.Document.Activate();
                    await Wait(() => ReferenceEquals(main.ActiveMdiChild, source.Document), source.Name + " is the active document");
                    source.Open();
                    pane = (ScriptAIPane)Field(service, "ai");
                }

                service.RevokeAISharing();
                await OpenWorksheetAssistant(main, service, workbook);
                await OpenSource();
                string acknowledgment = pane.Acknowledge.Text;
                pane.ShareWorkbook.Checked = false;
                pane.ShareScript.Checked = true;
                pane.Acknowledge.Checked = true;
                Check(service.Sharing.Accepts([source.Document.Id()]) && !service.Sharing.Accepts([workbook.Id()]),
                    source.Name + " confirmation grants only that document even when the workbook was last active");
                pane = await OpenWorksheetAssistant(main, service, workbook);
                Check(pane.Acknowledge.Text == acknowledgment,
                    "worksheet and " + source.Name + " assistance use the same sharing acknowledgment");
                CheckWorksheetSharing(pane, service, workbook, trace, allow: false);
                Check(service.Sharing.Accepts([source.Document.Id()]),
                    "leaving worksheet sharing unconfirmed preserves the earlier " + source.Name + " approval");
                CheckWorksheetSharing(pane, service, workbook, trace);
                await CheckConfirmedWorkbookRead(pane, workbook, workbookValue, trace, source.Name + "_THEN_CONFIRMED_WORKBOOK");

                service.RevokeAISharing();
                Check(pane.ShareWorkbook.Visible && !pane.ShareWorkbook.Checked && pane.Send.Enabled
                    && !service.Sharing.Accepts([workbook.Id()]),
                    "revoking consent in the worksheet unchecks sharing and leaves questions enabled");
                CheckWorksheetSharing(pane, service, workbook, trace);
                Check(service.Sharing.Accepts([workbook.Id()]) && !service.Sharing.Accepts([source.Document.Id()]),
                    "worksheet confirmation grants only its named workbook");
                await OpenSource();
                pane.ShareWorkbook.Checked = false;
                pane.ShareScript.Checked = true;
                await CheckConsentBlocksTurn(pane, trace, "WORKBOOK_THEN_UNCONFIRMED_" + source.Name);
                pane.Acknowledge.Checked = true;
                int before = WorksheetAnalysisDataTraffic(trace).Length;
                string question = "WORKBOOK_THEN_CONFIRMED_" + source.Name;
                pane.Composer.Text = question;
                await pane.SendAsync();
                string[] currentTraffic = WorksheetAnalysisDataTraffic(trace).Skip(before).ToArray();
                Check(currentTraffic.Any(line => line.Contains(question) && line.Contains(source.Value)) &&
                    currentTraffic.All(line => !line.Contains(workbookValue)),
                    source.Name + " content reaches the tutor only after its own confirmation and with workbook sharing off");

                service.RevokeAISharing();
                pane.ShareScript.Checked = false;
                pane.ShareWorkbook.Checked = true;
                await CheckConsentBlocksTurn(pane, trace, source.Name + "_UNCONFIRMED_WORKBOOK_SCOPE");
                pane.Acknowledge.Checked = true;
                Check(service.Sharing.Accepts([workbook.Id()]) && !service.Sharing.Accepts([source.Document.Id()]),
                    "explicit " + source.Name + " workbook sharing approves the workbook but excludes the document checkbox left off");
                pane = await OpenWorksheetAssistant(main, service, workbook);
                CheckWorksheetSharing(pane, service, workbook, trace, reuse: true);
                await CheckConfirmedWorkbookRead(pane, workbook, workbookValue, trace, source.Name + "_REUSED_WORKBOOK");

                await OpenSource();
                service.RevokeAISharing();
                pane = await OpenWorksheetAssistant(main, service, workbook);
                Check(pane.ShareWorkbook.Visible && !pane.ShareWorkbook.Checked && pane.Send.Enabled
                    && !service.Sharing.Accepts([workbook.Id()]),
                    "returning from " + source.Name + " after revocation unchecks worksheet sharing and leaves questions enabled");
            }

            await TestWorksheetConsentAcrossFocus(main, service, workbook, script, report, workbookValue, trace);

            service.RevokeAISharing();
            service.Show(false);
            await Wait(() => service.View is { IsDisposed: false }, "Learning opens for the document consent check");
            var learning = service.View;
            await learning.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40));
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, learning), "the learning document is in front");
            Check(ReferenceEquals(Field(pane, "script"), workbook) && pane.WorksheetMode,
                "opening Learning leaves the worksheet conversation in place");
            await JS(learning, "document.querySelector('[data-lesson=precision]').click()");
            learning.SourceId = script.Id();
            service.RefreshWorkspace();
            int learningConsents = 0;
            service.ConfirmSharing = documents =>
            {
                Check(documents.Select(document => document.Id()).SequenceEqual([script.Id()]),
                    "Learning confirmation names only the selected script");
                learningConsents++;
                return LearningService.SharingDecision.Share;
            };
            await LearningAsk(service, learning, "LEARNING_CONFIRMED_SCRIPT");
            Check(learningConsents == 1 && service.Sharing.Accepts([script.Id()]) && !service.Sharing.Accepts([workbook.Id()]),
                "Learning's script-only confirmation does not approve the active workbook");
            await service.HandleAsync(learning, new JsonObject { ["action"] = "workspace", ["choice"] = workbook.Id() });
            Check(learning.SourceId == workbook.Id() && service.Sharing.Accepts([script.Id()]) && !service.Sharing.Accepts([workbook.Id()]),
                "changing the Learning source preserves explicit script approval and gives no approval to the newly selected workbook");
            pane = await OpenWorksheetAssistant(main, service, workbook);
            CheckWorksheetSharing(pane, service, workbook, trace, allow: false);
        }
        finally
        {
            service.ConfirmSharing = previousConfirm;
            pane?.Hide();
            if (service.View is { IsDisposed: false } learning) learning.Close();
            CloseTestDocument(report);
            CloseTestDocument(script);
            await Wait(() => script.IsDisposed, "the consent fixture waits for the interactive R window to close");
            CloseTestDocument(workbook);
        }
    }

    private static async Task TestWorksheetConsentAcrossFocus(frmMain main, LearningService service, frmSpreadsheetGear workbook,
        frmScript script, frmReportWebView report, string workbookValue, string trace)
    {
        const string otherValue = "CONSENT_UNAPPROVED_OTHER_WORKBOOK";
        var other = (frmSpreadsheetGear)main.CreateGrid("Another consent test workbook");
        var otherView = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(other, "workbookView");

        try
        {
            WithWorksheet(otherView, sheet =>
            {
                sheet.Cells["A2"].Value = otherValue;
                sheet.Cells["B1"].Value = otherValue;
                return true;
            });
            service.RevokeAISharing();
            script.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, script), "the script is active before its explicit confirmation");
            service.ShowScriptAI(script);
            var pane = (ScriptAIPane)Field(service, "ai");
            pane.ShareWorkbook.Checked = false;
            pane.ShareScript.Checked = true;
            pane.Acknowledge.Checked = true;
            Check(service.Sharing.Accepts([script.Id()]), "the script has its own approval before worksheet activation");

            pane = await OpenWorksheetAssistant(main, service, workbook);
            CheckWorksheetSharing(pane, service, workbook, trace);
            int revision = service.Sharing.Revision;
            await CheckConfirmedWorkbookRead(pane, workbook, workbookValue, trace, "FOCUS_BEFORE_SWITCH");

            other.Activate();
            await CheckWorksheetPaneBinding(main, pane, other, shared: false);
            Check(!service.Sharing.Accepts([other.Id()]) &&
                service.Sharing.Accepts([workbook.Id(), script.Id()]) && service.Sharing.Revision == revision,
                "viewing a different workbook preserves existing approvals without approving that workbook");
            await pane.Transcript.SettledAsync();
            Check(!(await pane.Transcript.TextAsync()).Contains("FOCUS_BEFORE_SWITCH", StringComparison.Ordinal),
                "the first workbook's conversation is hidden when the other workbook is in front");
            pane.ShareWorkbook.Checked = true;
            await CheckConsentBlocksTurn(pane, trace, "WORKSHEET_AI_FOCUS_OTHER_UNCONFIRMED");
            pane.ShareWorkbook.Checked = false;
            await CheckUnsharedWorksheetTurn(pane, trace, "WORKSHEET_AI_FOCUS_OTHER_UNAPPROVED",
                workbook.Id(), workbookValue, workbook.Text, other.Id(), otherValue, other.Text);

            CheckWorksheetSharing(pane, service, other, trace);
            await CheckConfirmedWorkbookRead(pane, other, otherValue, trace, "FOCUS_OTHER_CONFIRMED",
                workbook.Id(), workbookValue, workbook.Text);
            pane.Acknowledge.Checked = false;
            Check(!service.Sharing.Accepts([other.Id()]) && service.Sharing.Accepts([workbook.Id(), script.Id()]),
                "withdrawing the other workbook's acknowledgment preserves the first workbook and script approvals");
            await CheckConsentBlocksTurn(pane, trace, "WORKSHEET_AI_FOCUS_OTHER_WITHDRAWN");
            pane.Acknowledge.Checked = true;

            workbook.Activate();
            await CheckWorksheetPaneBinding(main, pane, workbook, shared: true);
            await pane.Transcript.SettledAsync();
            string conversation = await pane.Transcript.TextAsync();
            Check(conversation.Contains("FOCUS_BEFORE_SWITCH", StringComparison.Ordinal)
                && !conversation.Contains("FOCUS_OTHER_CONFIRMED", StringComparison.Ordinal),
                "returning to the first worksheet restores only its own conversation and sharing choice");
            await CheckConfirmedWorkbookRead(pane, workbook, workbookValue, trace, "FOCUS_RETURNED",
                other.Id(), otherValue, other.Text);
            Check(LastWorksheetRequest(trace).A("conversation").Any(message =>
                    message.S("text") == "WORKSHEET_AI_CONSENT_FOCUS_BEFORE_SWITCH"),
                "the resumed conversation sends its earlier worksheet question");

            foreach (var document in new StatsDirectForm[] { script, report })
            {
                document.Activate();
                await Wait(() => ReferenceEquals(main.ActiveMdiChild, document), "the non-workbook document is in front");
                Check(pane.WorksheetMode && ReferenceEquals(Field(pane, "script"), workbook) && pane.ShareWorkbook.Checked,
                    "activating " + document.GetType().Name + " leaves the worksheet conversation and its confirmation in place");
            }

            other.Activate();
            await CheckWorksheetPaneBinding(main, pane, other, shared: true);
            await pane.Transcript.SettledAsync();
            Check((await pane.Transcript.TextAsync()).Contains("FOCUS_OTHER_CONFIRMED", StringComparison.Ordinal),
                "returning to the other worksheet restores its confirmed conversation too");
            workbook.Activate();
            await CheckWorksheetPaneBinding(main, pane, workbook, shared: true);
        }
        finally { CloseTestDocument(other); }
    }

    private static void CheckWorksheetSharing(ScriptAIPane pane, LearningService service, frmSpreadsheetGear workbook,
        string trace, bool allow = true, bool reuse = false)
    {
        pane.ShareWorkbook.Checked = false;
        string[] before = WorksheetAnalysisDataTraffic(trace);
        pane.ShareWorkbook.Checked = true;
        Check(ReferenceEquals(Field(pane, "script"), workbook)
            && pane.ShareWorkbook.Visible && pane.Acknowledge.Visible && pane.Acknowledge.Enabled
            && pane.Acknowledge.Checked == reuse && pane.Send.Enabled == reuse
            && service.Sharing.Accepts([workbook.Id()]) == reuse,
            reuse ? "the sharing checkbox restores the named workbook's existing confirmation"
                : "the sharing checkbox alone does not authorize the named workbook or enable Send");
        if (allow && !reuse) pane.Acknowledge.Checked = true;
        if (!allow) pane.ShareWorkbook.Checked = false;
        Check(pane.ShareWorkbook.Checked == allow && pane.Acknowledge.Enabled == allow && pane.Send.Enabled
            && service.Sharing.Accepts([workbook.Id()]) == allow,
            allow ? "the acknowledgment enables questions sharing the named workbook"
                : "turning off unconfirmed sharing leaves questions enabled without workbook approval");
        Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)),
            "changing the sharing choice by itself does not send a question or workbook data");
    }

    private static async Task CheckConsentBlocksTurn(ScriptAIPane pane, string trace, string prompt)
    {
        Check(pane.Acknowledge.Enabled && !pane.Acknowledge.Checked && !pane.Send.Enabled,
            prompt + " requires its own acknowledgement before Send");
        string[] before = WorksheetAnalysisDataTraffic(trace);
        pane.Composer.Text = prompt;
        await pane.SendAsync();
        Check(before.SequenceEqual(WorksheetAnalysisDataTraffic(trace)) &&
            ((Label)Field(pane, "status")).Text.Contains("confirm sharing", StringComparison.OrdinalIgnoreCase),
            prompt + " sends no request or document data before acknowledgement");
    }

    private static async Task CheckConfirmedWorkbookRead(
        ScriptAIPane pane, frmSpreadsheetGear workbook, string sample, string trace, string check, params string[] excluded)
    {
        int before = WorksheetAnalysisDataTraffic(trace).Length;
        var reply = await WorksheetReply(pane, "WORKSHEET_AI_CONSENT_" + check);
        string[] traffic = WorksheetAnalysisDataTraffic(trace).Skip(before).ToArray();
        Check(reply.A("ids").Select(id => id.GetValue<string>()).SequenceEqual([workbook.Id()]) && reply.B("read") &&
            traffic.Any(line => line.Contains(sample, StringComparison.Ordinal)) &&
            traffic.All(line => excluded.All(value => !line.Contains(value, StringComparison.Ordinal))),
            check + " reads a real cell from exactly the named workbook after its confirmation");
    }
}

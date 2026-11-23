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
                pane.ShareWorkbook.Checked = false;
                pane.ShareScript.Checked = true;
                pane.Acknowledge.Checked = true;
                Check(service.Sharing.Accepts([source.Document.Id()]) && !service.Sharing.Accepts([workbook.Id()]),
                    source.Name + " confirmation grants only that document even when the workbook was last active");
                pane = await OpenWorksheetAssistant(main, service, workbook);
                pane.ShareWorkbook.Checked = true;
                await CheckConsentBlocksTurn(pane, trace, source.Name + "_THEN_UNCONFIRMED_WORKBOOK");
                pane.Acknowledge.Checked = true;
                await CheckConfirmedWorkbookRead(pane, workbook, workbookValue, trace, source.Name + "_THEN_CONFIRMED_WORKBOOK");

                service.RevokeAISharing();
                pane.Acknowledge.Checked = true;
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
                pane.ShareWorkbook.Checked = true;
                Check(pane.Acknowledge.Checked && pane.Send.Enabled, "worksheet reuses explicit workbook confirmation from " + source.Name + " AI");
                await CheckConfirmedWorkbookRead(pane, workbook, workbookValue, trace, source.Name + "_REUSED_WORKBOOK");
            }

            service.RevokeAISharing();
            service.Show(false);
            await Wait(() => service.View is { IsDisposed: false }, "Learning opens for the document consent check");
            var learning = service.View;
            await learning.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40));
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
            pane = await OpenWorksheetAssistant(main, service, workbook);
            pane.ShareWorkbook.Checked = true;
            await CheckConsentBlocksTurn(pane, trace, "LEARNING_THEN_UNCONFIRMED_WORKBOOK");
            pane.Composer.Clear();
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
        ScriptAIPane pane, frmSpreadsheetGear workbook, string sample, string trace, string check)
    {
        int before = WorksheetAnalysisDataTraffic(trace).Length;
        var reply = await WorksheetReply(pane, "WORKSHEET_AI_CONSENT_" + check);
        Check(reply.A("ids").Select(id => id.GetValue<string>()).SequenceEqual([workbook.Id()]) && reply.B("read") &&
            WorksheetAnalysisDataTraffic(trace).Skip(before).Any(line => line.Contains(sample, StringComparison.Ordinal)),
            check + " reads a real cell from exactly the named workbook after its confirmation");
    }
}

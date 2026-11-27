using StatsDirect.UI;
using StatsDirect.UI.Learning;
using StatsDirect.UI.ToolWindows;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static async Task CheckWorksheetPaneBinding(frmMain main, ScriptAIPane pane, frmSpreadsheetGear workbook, bool shared)
    {
        await Wait(() => ReferenceEquals(main.ActiveMdiChild, workbook) && ReferenceEquals(Field(pane, "script"), workbook),
            "the open worksheet assistant follows " + workbook.Text);
        Check(pane.IsOpen && pane.WorksheetMode && pane.ShareWorkbook.Checked == shared
            && pane.ShareWorkbook.Text == $"Share open workbook data (“{workbook.Text}”)"
            && ((ToolStripLabel)Field(pane, "title")).Text == "Worksheet AI — " + workbook.Text.Limit(24),
            "the assistant title, sharing choice and workbook name agree");
    }

    private static async Task CheckUnsharedWorksheetTurn(ScriptAIPane pane, string trace, string question, params string[] excluded)
    {
        int before = WorksheetAnalysisDataTraffic(trace).Length;
        var response = await WorksheetReply(pane, question);
        var traffic = WorksheetAnalysisDataTraffic(trace).Skip(before).ToArray();
        Check(response.A("ids").Count == 0 && !response.B("read") && traffic.Length > 0
            && traffic.All(line => excluded.All(value => !line.Contains(value, StringComparison.Ordinal))),
            question + " sends no worksheet data, even with another workbook's approval still held");
    }

    private static async Task TestWorksheetActivationFromCommands(frmMain main, LearningService service, string trace)
    {
        const string sample = "ORIGINAL_WORKSHEET_VALUE";
        var original = (frmSpreadsheetGear)main.CreateGrid("Original worksheet");
        var view = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(original, "workbookView");
        WithWorksheet(view, sheet => { sheet.Cells["A2"].Value = sample; return true; });
        var pane = await OpenWorksheetAssistant(main, service, original);
        var session = (ToolPaneSession)Field(pane, "session");
        frmSpreadsheetGear example = null, imported = null, created = null;
        try
        {
            const string initial = "FOCUS_ORIGINAL_CONVERSATION";
            CheckWorksheetSharing(pane, service, original, trace);
            await CheckConfirmedWorkbookRead(pane, original, sample, trace, initial);
            Check(session.IsDocked, "the assistant starts docked for the example opening test");
            var help = ((MenuStrip)Field(main, "mnuMain")).Items.OfType<ToolStripMenuItem>()
                .Single(item => item.Text.Replace("&", "") == "Help");
            help.DropDownItems["exampleWorkbookMenuItem"]!.PerformClick();
            await Wait(() => main.ActiveMdiChild is frmSpreadsheetGear { Text: "Example workbook" },
                "Help > Example workbook opens the supplied workbook while the assistant is visible");
            example = (frmSpreadsheetGear)main.ActiveMdiChild;
            await CheckWorksheetPaneBinding(main, pane, example, shared: false);
            await pane.Transcript.SettledAsync();
            var conversation = await pane.Transcript.TextAsync();
            Check(conversation.Contains(WorksheetOpeningText, StringComparison.Ordinal) && !conversation.Contains(initial, StringComparison.Ordinal),
                "the example workbook starts its own conversation with the opening offer");
            await CheckUnsharedWorksheetTurn(pane, trace, "WORKSHEET_AI_FOCUS_EXAMPLE_UNSHARED",
                original.Id(), original.Text, sample, example.Id(), example.Text);

            var heading = ((UserControl)Field(pane, "view")).Controls.OfType<ToolStrip>().Single();
            heading.Items.OfType<ToolStripButton>().Single(item => item.Text == "Pop out").PerformClick();
            await Wait(() => session.Window is { Visible: true }, "the user pops out the worksheet assistant");
            var floating = session.Window;
            string path = Path.Combine(root, "focus-import.csv");
            File.WriteAllText(path, "Value\r\nIMPORTED_WORKSHEET_CELL\r\n");
            Check(main.OpenFile(path, false), "File > Open imports a separate workbook while the assistant is popped out");
            await Wait(() => main.ActiveMdiChild is frmSpreadsheetGear grid && grid != example && grid != original,
                "the imported worksheet becomes active");
            imported = (frmSpreadsheetGear)main.ActiveMdiChild;
            await CheckWorksheetPaneBinding(main, pane, imported, shared: false);
            Check(ReferenceEquals(session.Window, floating) && floating.Text == "Worksheet AI — " + imported.Text,
                "the popped-out assistant stays open and its window title follows the imported workbook");
            await pane.Transcript.SettledAsync();
            Check(!(await pane.Transcript.TextAsync()).Contains("WORKSHEET_AI_FOCUS_EXAMPLE_UNSHARED", StringComparison.Ordinal),
                "the imported workbook does not inherit the example conversation");
            await CheckUnsharedWorksheetTurn(pane, trace, "WORKSHEET_AI_FOCUS_IMPORTED_UNSHARED",
                original.Id(), original.Text, sample, imported.Id(), "IMPORTED_WORKSHEET_CELL");

            heading.Items.OfType<ToolStripButton>().Single(item => item.Text == "Dock").PerformClick();
            await Wait(() => session.IsDocked, "the user docks the worksheet assistant again");
            original.Activate();
            await CheckWorksheetPaneBinding(main, pane, original, shared: true);
            var activity = ((Control)Field(pane, "view")).Controls.Find("aiReplyActivity", true).Single();
            Check(!activity.Visible, "the worksheet assistant starts without a reply indicator");
            const string delayed = "WORKSHEET_AI_FOCUS_GATED_REPLY";
            var reply = WorksheetReply(pane, delayed);
            await WaitForWorksheetTurn(trace, delayed);
            Check(activity.Visible && activity.Text == "Reply in progress…",
                "the worksheet assistant shows a reply in progress while waiting for the first response");
            imported.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, imported), "an imported workbook is selected during the reply");
            created = (frmSpreadsheetGear)main.CreateGrid("New active worksheet");
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, created), "a new worksheet is selected before the reply finishes");
            Check(ReferenceEquals(Field(pane, "script"), original) && !reply.IsCompleted && !pane.Send.Enabled,
                "a running reply keeps its original worksheet and does not move the assistant early");
            int beforeRelease = WorksheetAnalysisDataTraffic(trace).Length;
            File.WriteAllText(trace + "." + delayed, "");
            var response = await reply.WaitAsync(TimeSpan.FromSeconds(15));
            Check(response.B("read") && response.A("ids").Select(id => id.GetValue<string>()).SequenceEqual([original.Id()])
                && WorksheetAnalysisDataTraffic(trace).Skip(beforeRelease).Any(line => line.Contains(sample, StringComparison.Ordinal)),
                "the pending turn reads the original worksheet even after new workbooks are activated");
            await CheckWorksheetPaneBinding(main, pane, created, shared: false);
            Check(!activity.Visible && pane.Send.Enabled,
                "the reply indicator goes away when the reply finishes");
            await pane.Transcript.SettledAsync();
            Check(!(await pane.Transcript.TextAsync()).Contains(delayed, StringComparison.Ordinal),
                "the finished question and reply are not copied to the latest active worksheet");

            original.Activate();
            await CheckWorksheetPaneBinding(main, pane, original, shared: true);
            await pane.Transcript.SettledAsync();
            Check((await pane.Transcript.TextAsync()).Contains(delayed, StringComparison.Ordinal),
                "the original conversation reopens with its question and the completed reply");
            await TestWorksheetReplyStop(main, pane, original, example, trace, returnBeforeStop: false);
            await TestWorksheetReplyStop(main, pane, original, example, trace, returnBeforeStop: true);
        }
        finally
        {
            pane.Hide();
            foreach (var workbook in new[] { created, imported, example, original })
                if (workbook != null && !workbook.IsDisposed) CloseTestDocument(workbook);
        }
    }

    private static async Task WaitForWorksheetTurn(string trace, string question)
    {
        await Wait(() => WorksheetAnalysisDataTraffic(trace).Any(line =>
                line.Contains(question, StringComparison.Ordinal) && JsonNode.Parse(line).S("method") == "turn/start"),
            "the mock received the worksheet question before switching workbooks");
    }

    private static async Task TestWorksheetReplyStop(frmMain main, ScriptAIPane pane, frmSpreadsheetGear original,
        frmSpreadsheetGear next, string trace, bool returnBeforeStop)
    {
        string question = returnBeforeStop ? "WORKSHEET_AI_FOCUS_GATED_STOP_AFTER_RETURN" : "WORKSHEET_AI_FOCUS_GATED_STOP";
        pane.Composer.Text = question;
        var stopped = pane.SendAsync();
        await WaitForWorksheetTurn(trace, question);
        next.Activate();
        await Wait(() => ReferenceEquals(main.ActiveMdiChild, next), "the example workbook is selected while the reply waits");
        var stop = (Button)Field(pane, "stop");
        var activity = ((Control)Field(pane, "view")).Controls.Find("aiReplyActivity", true).Single();
        Check(stop.Enabled && activity.Visible && ReferenceEquals(Field(pane, "script"), original),
            "Stop remains available in the original chat while another workbook is in front");
        if (returnBeforeStop)
        {
            original.Activate();
            await Wait(() => ReferenceEquals(main.ActiveMdiChild, original), "the user returns to the source before stopping the reply");
        }
        stop.PerformClick();
        await stopped.WaitAsync(TimeSpan.FromSeconds(15));
        if (!returnBeforeStop)
        {
            await CheckWorksheetPaneBinding(main, pane, next, shared: false);
            original.Activate();
        }
        await CheckWorksheetPaneBinding(main, pane, original, shared: true);
        Check(!activity.Visible && !stop.Enabled, "stopping the reply clears the reply indicator");
        Check(pane.Composer.Text == question, "Stop preserves the question in the workbook where it was asked");
    }
}

using StatsDirect.UI;
using StatsDirect.UI.Learning;
using StatsDirect.UI.ToolWindows;
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
        return (ScriptAIPane)Field(service, "ai");
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
        using var tutor = WorksheetMockTutor(Path.Combine(root, "worksheet-account"), () => policy);
        using var service = new LearningService(main, Path.Combine(root, "Worksheet Learning"), tutor);
        typeof(frmMain).GetField("learning", Members)!.SetValue(main, service);
        try
        {
            await TestWorksheetAssistant(main, service, level => policy = level);
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
        pane.Composer.Text = "Add an Age band column";
        await pane.SendAsync();
        var review = FindWorksheetReview(pane);
        await Wait(() => review.Enabled && review.Visible, "the example edit is ready to preview");
    }

    private static Button FindWorksheetReview(ScriptAIPane pane) =>
        ((Control)Field(pane, "view")).Controls.Find("reviewWorksheetEdit", true).OfType<Button>().Single();

    private static async Task<JsonNode> WorksheetReply(ScriptAIPane pane, string prompt)
    {
        pane.Composer.Text = prompt;
        await pane.SendAsync();
        var messages = (JsonArray)Field(Field(pane, "chat"), "Messages");
        return JsonNode.Parse(messages.Last(message => message.S("role") == "assistant").S("text"));
    }

    private static bool OpenWorksheetReview(ScriptAIPane pane, Button review, string action)
    {
        var session = (ToolPaneSession)Field(pane, "session");
        var originalFrame = session.Window;
        bool seen = false, handled = false, completePreview = false, stablePresentation = false, controlsFit = false;
        using var timer = new System.Windows.Forms.Timer { Interval = 150 };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<Form>().FirstOrDefault(form => form.Modal);
            if (dialog == null) return;
            seen = dialog.Name == "worksheetEditReview";
            stablePresentation = !session.IsTemporary && session.Window == originalFrame;
            var values = dialog.Controls.Find("worksheetEditPreview", true).OfType<DataGridView>().SingleOrDefault();
            completePreview = values?.Rows.Count == 3
                && values.Rows[0].Cells[0].Value?.ToString()?.Replace("$", "", StringComparison.Ordinal).EndsWith("B1", StringComparison.Ordinal) == true
                && values.Rows[0].Cells[1].Value?.ToString()?.Contains("Original heading", StringComparison.Ordinal) == true
                && values.Rows[0].Cells[2].Value?.ToString()?.Contains("Age band", StringComparison.Ordinal) == true;
            var area = dialog.RectangleToScreen(dialog.ClientRectangle);
            controlsFit = values?.DisplayedRowCount(false) >= 3
                && new[] { "applyWorksheetEdit", "cancelWorksheetEdit" }.All(name =>
                    dialog.Controls.Find(name, true).OfType<Button>().SingleOrDefault() is { Visible: true, Enabled: true } button
                    && area.Contains(button.RectangleToScreen(button.ClientRectangle))
                    && button.Parent.RectangleToScreen(button.Parent.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)));
            if (!controlsFit) Console.WriteLine($"Preview layout: dialog {dialog.ClientSize}, visible rows {values?.DisplayedRowCount(false)}, buttons {string.Join(", ", dialog.Controls.Find("applyWorksheetEdit", true).Concat(dialog.Controls.Find("cancelWorksheetEdit", true)).Select(control => $"{control.Text} {control.Bounds} in {control.Parent.ClientSize}"))}");
            var button = dialog.Controls.Find(action, true).OfType<Button>().SingleOrDefault();
            if (seen && button is { Enabled: true })
            {
                handled = true;
                button.PerformClick();
            }
            else dialog.DialogResult = DialogResult.Cancel;
        };
        timer.Start();
        review.PerformClick();
        timer.Stop();
        if (seen)
        {
            Check(handled && completePreview, "the worksheet preview shows the proposed range, original values, replacements and approval controls");
            Check(stablePresentation, "the assistant keeps its dock or floating frame while worksheet review is modal");
            Check(controlsFit, "the proposed rows and both approval buttons fit within the native dialog");
        }
        return seen;
    }

    private static T WithWorksheet<T>(WorkbookView view, Func<SpreadsheetGear.IWorksheet, T> action)
    {
        view.GetLock();
        try { return action(view.ActiveWorksheet); }
        finally { view.ReleaseLock(); }
    }

    private static async Task TestWorksheetAssistant(frmMain main, LearningService service, Action<AISecurityLevel> setSecurity)
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
            var toolbar = WorksheetRibbon(main);
            await Wait(() => main.ActiveMdiChild == grid && toolbar.Items["learnToolStripButton"] is { Available: true, Enabled: true }, "the worksheet ribbon exposes Learn");
            toolbar.Items["learnToolStripButton"]!.PerformClick();
            await Wait(() => service.View is { IsDisposed: false } && main.ActiveMdiChild == service.View, "Learn opens the existing course from the worksheet ribbon");
            await service.View.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40));
            service.View.Close();

            if (service.Tutor.Email == null) await service.Tutor.SignInAsync();
            await Wait(() => service.Tutor.Email == "mock@example.invalid", "the worksheet assistant test is connected to a mock account");
            pane = await OpenWorksheetAssistant(main, service, grid);
            Check(pane.Composer.Text.Length == 0 && !pane.ShareWorkbook.Checked, "Ask AI opens a question-only worksheet conversation without sending");
            var review = FindWorksheetReview(pane);
            var readOnly = await WorksheetReply(pane, "WORKSHEET_AI_WITHOUT_SHARING");
            Check(readOnly.S("help").Contains("worksheet", StringComparison.OrdinalIgnoreCase)
                && !readOnly.B("read") && !readOnly.B("proposed") && !review.Visible,
                "question-only mode retrieves packaged worksheet help and exposes no workbook read or edit");
            service.RevokeAISharing();
            other.Activate();
            await Wait(() => main.ActiveMdiChild == other && service.Sharing.DataDocumentId == other.Id(),
                "a different active workbook leaves the open assistant bound to its original workbook");
            pane.Acknowledge.Checked = true;
            Check(!pane.Acknowledge.Enabled && !pane.Acknowledge.Checked && !service.Sharing.Accepted,
                "question-only mode cannot grant workbook consent even if acknowledgement is toggled");
            pane.ShareWorkbook.Checked = true;
            Check(pane.Acknowledge.Enabled && !pane.Acknowledge.Checked && !pane.Send.Enabled,
                "switching on workbook sharing requires confirmation when the bound workbook is unapproved");
            pane.Acknowledge.Checked = true;
            Check(service.Sharing.Accepts([grid.Id()]) && !service.Sharing.Accepts([other.Id()]),
                "worksheet acknowledgement approves only the bound workbook, even while another workbook is active");
            pane.ShareWorkbook.Checked = false;
            Check(!pane.Acknowledge.Enabled && !pane.Acknowledge.Checked && service.Sharing.Accepts([grid.Id()]),
                "turning sharing off suspends the scope while preserving session acceptance for the approved workbook");
            pane.ShareWorkbook.Checked = true;
            Check(pane.Acknowledge.Checked && pane.Send.Enabled && !service.Sharing.Accepts([other.Id()]),
                "enabling sharing again reuses acceptance for the bound workbook only");

            grid.Activate();
            await Wait(() => main.ActiveMdiChild == grid, "the worksheet assistant returns to its source worksheet");
            pane.Acknowledge.Checked = true;
            var result = await WorksheetReply(pane, "WORKSHEET_AI_REVIEW");
            Check(result.A("ids").Count == 1 && result.A("ids")[0].GetValue<string>() == grid.Id()
                && result.B("read") && result.B("proposed"), "approved worksheet AI reads and stages an edit for only the selected workbook");
            Check(WithWorksheet(view, sheet => (string)sheet.Cells["B1"].Value == "Original heading" && sheet.Cells["B2"].HasFormula),
                "the tool call leaves the worksheet unchanged before approval");
            Check(OpenWorksheetReview(pane, review, "cancelWorksheetEdit"), "Review opens the native preview and Cancel dismisses it");
            Check(WithWorksheet(view, sheet => (string)sheet.Cells["B3"].Value == "Existing cell"), "cancelling preview leaves the original cells intact");
            await WorksheetReply(pane, "WORKSHEET_AI_REVIEW_AGAIN");
            Check(OpenWorksheetReview(pane, review, "applyWorksheetEdit"), "the user can approve the reviewed edit");
            Check(WithWorksheet(view, sheet => (string)sheet.Cells["B1"].Value == "Age band"
                && (string)sheet.Cells["B2"].Value == "=2+2" && !sheet.Cells["B2"].HasFormula
                && sheet.Cells["B3"].Value == null && (double)sheet.Cells["A2"].Value == 26),
                "approval edits exactly the target range, preserving text literals and unrelated cells");
            grid.EditUndo();
            Check(WithWorksheet(view, sheet => (string)sheet.Cells["B1"].Value == "Original heading"
                && sheet.Cells["B2"].Formula == "=1+1" && (string)sheet.Cells["B3"].Value == "Existing cell"),
                "one normal worksheet Undo restores the whole edit including its original formula");
            grid.EditRedo();
            Check(WithWorksheet(view, sheet => (string)sheet.Cells["B1"].Value == "Age band" && !sheet.Cells["B2"].HasFormula),
                "one normal worksheet Redo replays the approved literal edit");
            grid.EditUndo();

            var target = grid.CaptureWorksheetEditTarget();
            await Reject(() =>
            {
                target.Propose(new JsonObject { ["range"] = "OtherSheet!B2", ["values"] = new JsonArray(new JsonArray(5)) });
                return Task.CompletedTask;
            }, "worksheet proposals cannot name a different sheet");
            await Reject(() =>
            {
                target.Propose(new JsonObject { ["range"] = "B1:C1", ["values"] = new JsonArray(new JsonArray(5)) });
                return Task.CompletedTask;
            }, "worksheet proposals reject mismatched range dimensions");

            await WorksheetReply(pane, "WORKSHEET_AI_STALE");
            WithWorksheet(view, sheet => { sheet.Cells["B3"].Value = "User changed this"; return true; });
            if (review.Visible && review.Enabled)
                Check(!OpenWorksheetReview(pane, review, "cancelWorksheetEdit"), "a stale worksheet edit is refused before preview");
            Check(WithWorksheet(view, sheet => (string)sheet.Cells["B3"].Value == "User changed this"), "stale proposals preserve newer cell contents");

            setSecurity(AISecurityLevel.DataSharingRestricted);
            pane.UpdateConnection();
            pane.Acknowledge.Checked = true;
            Check(!pane.ShareWorkbook.Enabled && !pane.Acknowledge.Enabled && !pane.Acknowledge.Checked,
                "restricted mode keeps workbook consent disabled even if acknowledgement is toggled");
            var restricted = await WorksheetReply(pane, "WORKSHEET_AI_RESTRICTED");
            Check(restricted.S("help").Contains("worksheet", StringComparison.OrdinalIgnoreCase) && !restricted.B("read")
                && !restricted.B("proposed") && !review.Visible, "data-sharing policy keeps help available and disables worksheet reads and edits");

            setSecurity(AISecurityLevel.AIRestricted);
            pane.UpdateConnection();
            Check(!pane.Send.Enabled && !review.Visible, "organisation AI policy disables AI submission and worksheet proposals");
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

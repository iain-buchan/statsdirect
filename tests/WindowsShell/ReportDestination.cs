using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.Templates;
using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckReportDestination(frmMain main)
    {
        var originalDocument = main.ActiveMdiChild;
        var originalReports = main.MdiChildren.Where(child => child is IReport).ToHashSet();
        using var grid = (Form)Call(main, "CreateGrid");
        var toolbar = (ToolStrip)Field(main, "toolStrip");
        var tabs = (TabControl)Field(main, "tabWindows");

        object Destination() => Get(main, "SelectedReportWindow");
        async Task SelectTab(Form child)
        {
            tabs.SelectedTab = (TabPage)Get(child.Tag, "TabPage");
            for (int attempt = 0; attempt < 80 && main.ActiveMdiChild != child; attempt++) await Task.Delay(25);
            Check(main.ActiveMdiChild == child, "the document tab activates " + child.Text);
        }

        try
        {
            await SelectTab(grid);
            Check(toolbar.Visible && !toolbar.Items.ContainsKey("lblActiveReport") && !toolbar.Items.ContainsKey("cboActiveReport")
                && !toolbar.Items.OfType<ToolStripComboBox>().Any(), "the worksheet toolbar has no Results to label or selector");
            int rPlus = toolbar.Items.IndexOfKey("rPlusToolStripButton");
            int help = toolbar.Items.IndexOfKey("helpToolStripButton");
            Check(rPlus >= 0 && help == rPlus + 2 && toolbar.Items[rPlus + 1] is ToolStripSeparator,
                "one separator divides worksheet Help from the analysis and R buttons");

            async Task<Form> NewReport()
            {
                var before = main.MdiChildren.ToHashSet();
                ((ToolStripMenuItem)Field(main, "newReportToolStripMenuItem")).PerformClick();
                var report = main.MdiChildren.Single(child => child is IReport && !before.Contains(child));
                await ((Task)Get(Field(report, "view"), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
                Check(ReferenceEquals(main.ActiveMdiChild, report) && ReferenceEquals(Destination(), report),
                    "File > New Report makes the new report the output destination immediately");
                return report;
            }

            var older = await NewReport();
            var middle = await NewReport();
            var last = await NewReport();
            await SelectTab(middle);
            await SelectTab(grid);
            Check(ReferenceEquals(Destination(), middle), "returning to the worksheet keeps the report last selected by tab");
            await SelectTab(older);
            await SelectTab(grid);
            Check(ReferenceEquals(Destination(), older), "an older report tab becomes the worksheet's new destination");

            var workbook = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
            workbook.GetLock();
            try
            {
                var sheet = workbook.ActiveWorksheet;
                sheet.Cells["A1"].Value = "Destination before";
                sheet.Cells["B1"].Value = "Destination after";
                for (int row = 1; row <= 4; row++)
                {
                    sheet.Cells[row, 0].Value = 10 * row;
                    sheet.Cells[row, 1].Value = 9 * row;
                }
            }
            finally { workbook.ReleaseLock(); }

            var menu = (MenuStrip)Field(main, "mnuMain");
            var analysis = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text.Replace("&", "") == "Analysis");
            var paired = MenuDescendants(analysis.DropDownItems).OfType<ToolStripMenuItem>()
                .Single(item => item.Tag is string tag && (tag.Contains("operation=TPaired|", StringComparison.Ordinal)
                    || tag.EndsWith("operation=TPaired}", StringComparison.Ordinal)));
            async Task RunPairedT()
            {
                workbook.GetLock();
                try { workbook.RangeSelection = workbook.ActiveWorksheet.Cells["A1:B5"]; }
                finally { workbook.ReleaseLock(); }
                var outcome = await RunAsTheMenuDoes(main, workbook, TemplateFactory.Operations["TPaired"],
                    new Queue<string>(), () => Task.CompletedTask, () => paired.PerformClick());
                Check(outcome.completed && outcome.failure == null, "the native Analysis menu completes Paired t");
            }

            await RunPairedT();
            await VerifyPairedResult(older);
            Check(!HasPairedResult(await ReportDestinationText(middle)) && !HasPairedResult(await ReportDestinationText(last)),
                "Paired t does not append to either of the newer reports");
            await SelectTab(grid);

            Call(older, "DiscardNotebookImport");
            Check(older.IsDisposed && ReferenceEquals(Destination(), last),
                "closing the selected report falls back to the last open report in document order");
            Call(last, "DiscardNotebookImport");
            Check(last.IsDisposed && ReferenceEquals(Destination(), middle), "closing that report falls back to the remaining report");
            Call(middle, "DiscardNotebookImport");

            if (originalReports.Count == 0)
            {
                await SelectTab(grid);
                Check(!main.MdiChildren.Any(child => child is IReport), "all reports are closed before the next analysis");
                await RunPairedT();
                var created = main.MdiChildren.Single(child => child is IReport);
                Check(ReferenceEquals(Destination(), created), "running an analysis with no open reports creates one output report");
                await VerifyPairedResult(created);
                await SelectTab(grid);
            }
        }
        finally
        {
            // Let the WebView script callback return before closing its report.
            await Task.Yield();
            Call(main, "CloseCurrentOperation");
            foreach (var child in main.MdiChildren.Where(child => child is IReport && !originalReports.Contains(child)))
                Call(child, "DiscardNotebookImport");
            grid.Dispose();
            if (originalDocument is { IsDisposed: false }) originalDocument.Activate();
        }
    }

    private static bool HasPairedResult(string text) => text.Contains("Paired t test", StringComparison.Ordinal)
        && text.Contains("Destination before", StringComparison.Ordinal) && text.Contains("Destination after", StringComparison.Ordinal);

    private static async Task VerifyPairedResult(Form report)
    {
        string text = await ReportDestinationText(report);
        for (int attempt = 0; attempt < 100 && !HasPairedResult(text); attempt++)
        {
            await Task.Delay(50);
            text = await ReportDestinationText(report);
        }
        if (!HasPairedResult(text)) Console.WriteLine("Rendered report text: " + text);
        Check(HasPairedResult(text), "the selected report displays Paired t output for the worksheet data");
    }

    private static async Task<string> ReportDestinationText(Form report)
    {
        var view = Field(report, "view");
        await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        var browser = (WebView2)Field(view, "Browser");
        string rendered = await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('#results')?.innerText ?? ''");
        return JsonSerializer.Deserialize<string>(rendered)!;
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Templates;
using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckWorksheetCommandPicker(frmMain main)
    {
        var originalDocument = main.ActiveMdiChild;
        var toolbar = (ToolStrip)Field(main, "toolStrip");
        var analyse = (ToolStripDropDownButton)toolbar.Items["analyseToolStripButton"]!;
        Check(analyse != null && analyse.Text == "Analyse" && analyse.AccessibleName == "Analyse"
            && analyse.DisplayStyle == ToolStripItemDisplayStyle.ImageAndText, "worksheet toolbar has a labelled Analyse icon");

        var picker = analyse.DropDown;
        var content = picker.Items.OfType<ToolStripControlHost>().Single().Control;
        var search = (TextBox)content.Controls.Find("worksheetCommandSearch", true).Single();
        var results = (TreeView)content.Controls.Find("worksheetCommandResults", true).Single();

        async Task OpenPicker()
        {
            analyse.ShowDropDown();
            for (int attempt = 0; attempt < 40 && (!picker.Visible || !search.Focused); attempt++)
                await Task.Delay(25);
            Check(picker.Visible && search.Focused && ReferenceEquals(analyse.DropDown, picker),
                "the worksheet button opens its integrated picker and focuses search");
        }

        using var grid = (Form)Call(main, "CreateGrid");
        var workbook = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
        (int Before, int After)[] readings = [(312, 300), (242, 201), (340, 232), (388, 312), (296, 220),
            (254, 256), (391, 328), (402, 330), (290, 231)];
        string SelectionAddress()
        {
            workbook.GetLock();
            try { return workbook.RangeSelection.Address; }
            finally { workbook.ReleaseLock(); }
        }

        workbook.GetLock();
        try
        {
            var sheet = workbook.ActiveWorksheet;
            sheet.Cells["A1"].Value = "PEFR Before";
            sheet.Cells["B1"].Value = "PEFR After";
            for (int i = 0; i < readings.Length; i++)
            {
                sheet.Cells[i + 1, 0].Value = readings[i].Before;
                sheet.Cells[i + 1, 1].Value = readings[i].After;
            }
            workbook.RangeSelection = sheet.Cells["A1:B10"];
        }
        finally { workbook.ReleaseLock(); }
        string originalSelection = SelectionAddress();

        var report = (Form)Call(main, "CreateReport");
        try
        {
            var view = Field(report, "view");
            await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
            await Task.Delay(150);
            Check(main.ActiveMdiChild == report && !toolbar.Visible, "Analyse is confined to the worksheet toolbar");
            grid.Activate();
            await Task.Delay(150);
            Check(main.ActiveMdiChild == grid && analyse.Available && toolbar.Visible, "Analyse is available in the active workbook");

            var menu = (MenuStrip)Field(main, "mnuMain");
            var rootLabels = new[] { "Analysis", "Data", "Graphics" };
            var menuRoots = menu.Items.OfType<ToolStripMenuItem>()
                .Where(item => rootLabels.Contains(item.Text.Replace("&", ""))).ToArray();
            var leaves = menuRoots.SelectMany(root => MenuDescendants(root.DropDownItems)).OfType<ToolStripMenuItem>()
                .Where(item => item.Available && !item.HasDropDownItems &&
                    (item.Tag is string tag && tag.Contains("operation=", StringComparison.Ordinal) || item.Name == "calculatorMenuItem"))
                .ToHashSet();
            ToolStripMenuItem OperationItem(string operation) => leaves.Single(item => item.Tag is string tag
                && (tag.Contains("operation=" + operation + "|", StringComparison.Ordinal)
                    || tag.EndsWith("operation=" + operation + "}", StringComparison.Ordinal)));
            var paired = OperationItem("TPaired");
            var boxPlot = OperationItem("BoxWhiskerPlot");

            await OpenPicker();
            Check(search.AccessibleName == "Search methods" && ((Label)Field(picker, "searchCaption")).Text == "Search methods"
                && analyse.DropDownDirection == ToolStripDropDownDirection.BelowRight,
                "the button opens below the toolbar and Search methods stays labelled while focused");
            Check(results.Nodes.Cast<TreeNode>().Select(node => node.Text).SequenceEqual(rootLabels),
                "the picker browses Analysis, Data and Graphics");
            var mapped = WorksheetPickerLeaves(results.Nodes).Select(node => (ToolStripMenuItem)Get(node.Tag!, "Item")).ToHashSet();
            Check(mapped.SetEquals(leaves), "every picker command is the actual available native menu item from those three menus");
            var workArea = Screen.FromControl(main).WorkingArea;
            Check(workArea.Contains(picker.Bounds), "the integrated picker fits the workbook monitor's working area");

            TreeNode FirstMatch() => WorksheetPickerLeaves(results.Nodes).First();
            search.Text = "paired t";
            Check(results.Nodes[0].Text == "Exact matches" && ReferenceEquals(Get(FirstMatch().Tag!, "Item"), paired)
                && ReferenceEquals(results.SelectedNode, FirstMatch()), "exact Paired t ranks first and is ready to open");
            search.Text = "mann whitney";
            Check(ReferenceEquals(Get(FirstMatch().Tag!, "Item"), OperationItem("MannWhitney")),
                "search treats the hyphen in Mann-Whitney as a word boundary");
            search.Text = "normal random";
            Check(ReferenceEquals(Get(FirstMatch().Tag!, "Item"), OperationItem("RndNormal")),
                "search finds a Data command by its name and menu path");
            search.Text = "graphics histogram";
            var histogram = FirstMatch();
            Check(ReferenceEquals(Get(histogram.Tag!, "Item"), OperationItem("HistogramPlot"))
                && histogram.Parent != null && histogram.Parent.IsExpanded,
                "search shows the graphical Histogram in an expanded matching branch");
            search.Text = "unpaired t";
            var duplicate = WorksheetPickerLeaves(results.Nodes).Where(node => (string)Get(node.Tag!, "Name") == "Unpaired t").ToArray();
            Check(duplicate.Length == 2 && duplicate.Select(node => node.ToolTipText).Distinct().Count() == 2
                && duplicate.Select(node => node.FullPath).Distinct().Count() == 2,
                "the two Unpaired t matches show distinct native menu paths");
            Check((bool)Call(picker, "HandleKey", Keys.Down) && search.Focused && ReferenceEquals(results.SelectedNode, duplicate[1])
                && (bool)Call(picker, "HandleKey", Keys.Up) && ReferenceEquals(results.SelectedNode, duplicate[0]),
                "the arrow keys select search results while the search field keeps focus");

            search.Text = "no such worksheet command xyz";
            Call(picker, "HandleKey", Keys.Enter);
            Check(!WorksheetPickerLeaves(results.Nodes).Any() && picker.Visible && !(bool)Get(main, "InOperation"),
                "Enter on an empty search runs no analysis");
            Check((bool)Call(picker, "HandleKey", Keys.Escape), "Escape dismisses the integrated picker");
            await Task.Yield();
            Check(!picker.Visible && analyse.Selected && SelectionAddress() == originalSelection && main.ActiveMdiChild == grid
                && !(bool)Get(main, "InOperation"), "Escape returns to Analyse and preserves the selected cells");

            await OpenPicker();
            search.Text = "paired t";
            report.Activate();
            await Task.Delay(150);
            Check(main.ActiveMdiChild == report && !picker.Visible && !toolbar.Visible && !(bool)Get(main, "InOperation"),
                "switching to a report dismisses the picker without dispatching its highlighted method");
            grid.Activate();
            await Task.Delay(150);

            try
            {
                paired.Enabled = false;
                boxPlot.Available = false;
                await OpenPicker();
                search.Clear();
                Check(!WorksheetPickerLeaves(results.Nodes).Any(node => ReferenceEquals(Get(node.Tag!, "Item"), boxPlot)),
                    "an unavailable command is absent from the picker");
                search.Text = "paired t";
                var disabled = WorksheetPickerLeaves(results.Nodes).First(node => ReferenceEquals(Get(node.Tag!, "Item"), paired));
                Check(!ReferenceEquals(results.SelectedNode, disabled), "a disabled exact match is not the default result");
                results.SelectedNode = disabled;
                Call(picker, "ChooseSelection");
                Check(picker.Visible && !(bool)Get(main, "InOperation"), "attempting to open a disabled method keeps the picker open");
            }
            finally
            {
                if (picker.Visible) picker.Close();
                paired.Enabled = true;
                boxPlot.Available = true;
            }

            int started = 0;
            EventHandler observed = (_, _) => started++;
            paired.Click += observed;
            try
            {
                await OpenPicker();
                search.Text = "paired t";
                Check(ReferenceEquals(Get(results.SelectedNode!.Tag!, "Item"), paired),
                    "the picker selects the enabled Paired t command");
                var operation = TemplateFactory.Operations["TPaired"];
                var outcome = await RunAsTheMenuDoes(main, workbook, operation, new Queue<string>(), () => Task.CompletedTask, () =>
                {
                    Check((bool)Call(picker, "HandleKey", Keys.Enter), "Enter opens the highlighted method");
                    Check(!picker.Visible && started == 0, "the picker closes before the native method is dispatched");
                    // The picker enqueues the existing menu click once its own window has closed.
                    // Pump that callback while the normal operation driver answers any options.
                    Application.DoEvents();
                });
                Check(outcome.completed && outcome.failure == null && started == 1,
                    "the toolbar picker runs the original Paired t menu command exactly once");
            }
            finally { paired.Click -= observed; }

            var browser = (Microsoft.Web.WebView2.WinForms.WebView2)Field(view, "Browser");
            string reportText = "";
            for (int attempt = 0; attempt < 30; attempt++)
            {
                reportText = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync(
                    "document.querySelector('#results')?.innerText ?? ''"))!;
                if (reportText.Contains("Mean of differences", StringComparison.Ordinal)) break;
                await Task.Delay(100);
            }
            Check(reportText.Contains("Paired t test", StringComparison.Ordinal)
                && reportText.Contains("PEFR Before", StringComparison.Ordinal) && reportText.Contains("PEFR After", StringComparison.Ordinal)
                && Regex.IsMatch(reportText, @"Mean of differences\s*=\s*56[.,]1\d*\s*\(n = 9\)"),
                "the existing paired t engine uses the original selection and reports the expected nine-pair mean");
        }
        finally
        {
            if (picker.Visible) picker.Close();
            // Let the WebView script callback return before closing its report, as the report-destination checks do: closing the report
            // inside the callback can stall the suite (Codex's second review).
            await Task.Yield();
            Call(main, "CloseCurrentOperation");
            if (!report.IsDisposed) Call(report, "DiscardNotebookImport");
            grid.Dispose();
            originalDocument?.Activate();
        }
    }

    static IEnumerable<TreeNode> WorksheetPickerLeaves(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag != null) yield return node;
            foreach (var leaf in WorksheetPickerLeaves(node.Nodes)) yield return leaf;
        }
    }
}

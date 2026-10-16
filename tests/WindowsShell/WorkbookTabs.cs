using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckWorkbookTabs(frmMain main, Form original, SpreadsheetGear.Windows.Forms.WorkbookView workbook)
    {
        var tabs = (TabControl)Field(main, "tabWindows");
        TabPage Tab(Form window) => tabs.TabPages.Cast<TabPage>().Single(t => t.Tag != null && ReferenceEquals(Get(t.Tag, "Window"), window));
        string originalName = original.Text;
        string examplePath = Path.Combine(AppContext.BaseDirectory, "Data", "test.xlsx");
        byte[] exampleBytes = File.ReadAllBytes(examplePath);
        var opened = new List<Form>();
        string csv = Path.Combine(Path.GetTempPath(), "StatsDirect-tab-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            var help = (ToolStripMenuItem)Field(main, "helpToolStripMenuItem");
            help.DropDownItems["exampleWorkbookMenuItem"]!.PerformClick();
            var until = DateTime.UtcNow.AddSeconds(10);
            while (main.ActiveMdiChild == original && DateTime.UtcNow < until) await Task.Delay(30);
            Check(main.ActiveMdiChild != null && main.ActiveMdiChild != original, "Help example opens beside an edited blank workbook");
            var example = main.ActiveMdiChild!; opened.Add(example);
            var exampleView = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(example, "workbookView");
            Check(example.Text == "Example workbook" && Tab(example).Text == example.Text && Tab(example).ToolTipText == example.Text,
                $"example window, tab and tooltip all show its name (tab: '{Tab(example).Text}')");
            Check(Get(example, "Path") == null && (bool)Get(example.Tag!, "IsNew"), "named example remains an unsaved working copy");

            // Run a real paired analysis from the installed example into a report.
            var data = new DataFrame();
            exampleView.GetLock();
            try
            {
                var sheet = exampleView.ActiveWorkbook.Worksheets["Agreement"];
                for (int c = 0; c < 2; c++)
                    data.Variables.Add(new DoubleVariable(Enumerable.Range(1, 20).Select(r => Convert.ToDouble(sheet.Cells[r, c].Value)).ToArray(), sheet.Cells[0, c].Text));
            }
            finally { exampleView.ReleaseLock(); }
            var inputs = new ParameterBag();
            inputs.AddInput("data", data); inputs.AddInput("gamma", .95); inputs.AddInput("doAgreement", false);
            var results = Parametric.RptTPaired(inputs).ParameterBag;
            var report = (Form)Call(main, "CreateReport"); opened.Add(report);
            var template = new ReportTemplate(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Template", "m_paired.creole")), "application/x-statsdirect-creole");
            ((IReport)report).AppendRenderable(new ReportTemplateAndParameters(template, results), 0, null, null);
            await ((Task)Field(report, "lastItem")).WaitAsync(TimeSpan.FromSeconds(45));
            Check(main.ActiveMdiChild == report && Tab(example).Text == "Example workbook" && Tab(original).Text == originalName,
                "running the paired report retains both workbook tab names");
            workbook.GetLock();
            try { Check(workbook.ActiveWorksheet.Cells["A1"].Value?.ToString() == "123", "opening example and report preserves the original entered data"); }
            finally { workbook.ReleaseLock(); }
            example.Activate(); await Task.Delay(150);
            Check(tabs.SelectedTab == Tab(example) && Tab(example).Text == "Example workbook", "returning from report selects the named example tab");
            report.Text = "Renamed report";
            Check(Tab(report).Text == report.Text && Tab(report).ToolTipText == report.Text, "report title changes also update its tab and tooltip");

            File.WriteAllText(csv, "id,value\n0012,42\n");
            var imported = (Form)Call(main, "CreateGrid", csv, false, null); opened.Add(imported);
            Check(imported.Text == Path.GetFileName(csv) && Tab(imported).Text == imported.Text && Tab(imported).ToolTipText == imported.Text && Get(imported, "Path") == null,
                "CSV import has a named tab while retaining Save As semantics");
            var temporary = (Form)Call(main, "CreateGrid", examplePath, true, null); opened.Add(temporary);
            Check(!string.IsNullOrWhiteSpace(temporary.Text) && Tab(temporary).Text == temporary.Text && Get(temporary, "Path") == null,
                "temporary workbook without a supplied name keeps its initial tab title");
            var saved = (Form)Call(main, "CreateGrid", examplePath, false, null); opened.Add(saved);
            Check(saved.Text == "test" && Tab(saved).Text == "test" && Tab(saved).ToolTipText == examplePath && (string)Get(saved, "Path") == examplePath,
                "normal workbook keeps its filename and full-path tooltip");
            Check(File.ReadAllBytes(examplePath).SequenceEqual(exampleBytes), "tab regression never changes the installed example file");
        }
        finally
        {
            foreach (var window in opened.AsEnumerable().Reverse())
            {
                window.GetType().GetProperty("Dirty", Internal)!.SetValue(window, false);
                window.Close(); window.Dispose();
            }
            if (File.Exists(csv)) File.Delete(csv);
            original.Activate();
            await Task.Delay(150);
        }
    }
}

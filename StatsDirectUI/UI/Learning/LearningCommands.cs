using StatsDirect.Templates;
using StatsDirect.UI.Learning;
using System;
using System.IO;
using System.Windows.Forms;

namespace StatsDirect.UI;

public partial class frmMain
{
    private LearningService learning;
    internal bool LearningCalculationRunning;
    internal LearningService Learning => learning ??= new LearningService(this);
    private void InitializeLearningCommands()
    {
        helpToolStripMenuItem.DropDownItems.Insert(0, new ToolStripMenuItem("&Learning", null, (_, _) => BeginInvoke(new Action(() => Learning.Show(false)))) { Name = "learningMenuItem" });
        helpToolStripMenuItem.DropDownItems.Insert(1, new ToolStripMenuItem("Learning &Options…", null, (_, _) => BeginInvoke(new Action(() => Learning.Show(true)))) { Name = "learningOptionsMenuItem" });
        helpToolStripMenuItem.DropDownItems.Insert(2, new ToolStripSeparator());
        int updatesIndex = helpToolStripMenuItem.DropDownItems.IndexOf(aboutsStatsDirectToolStripMenuItem);
        toolsToolStripMenuItem.DropDownItems.Remove(checkForUpdatesToolStripMenuItem);
        helpToolStripMenuItem.DropDownItems.Insert(updatesIndex++, new ToolStripMenuItem("&Example workbook", null, (_, _) => BeginInvoke(new Action(OpenExampleWorkbook))) { Name = "exampleWorkbookMenuItem" });
        helpToolStripMenuItem.DropDownItems.Insert(updatesIndex++, new ToolStripSeparator());
        helpToolStripMenuItem.DropDownItems.Insert(updatesIndex++, checkForUpdatesToolStripMenuItem);
        helpToolStripMenuItem.DropDownItems.Insert(updatesIndex, new ToolStripSeparator());
        MdiChildActivate += (_, _) =>
        {
            if (ActiveMdiChild is frmSpreadsheetGear grid) learning?.SelectAIDataDocument(grid.Id());
            learning?.RefreshWorkspace();
            if (ActiveMdiChild is frmScript script) learning?.ActiveScriptChanged(script);
            if (ActiveMdiChild is frmReportWebView report) learning?.ActiveReportChanged(report);
        };
        FormClosed += (_, _) => learning?.Dispose();
    }
    private void OpenExampleWorkbook()
    {
        // An unsaved working copy uses the normal Save As flow, preserving the installed example.
        string path = Path.Combine(AppContext.BaseDirectory, "Data", Properties.Settings.Default.DefaultRecentlyUsedFile);
        CreateGrid(path, true, "Example workbook");
    }
    internal void AddLearningDocument(LearningView child) => SetUpForm(child);
    internal bool OpenLearningAnalysis(string operationName, StatsDirectForm source = null)
    {
        if (InOperation || LearningCalculationRunning) throw new InvalidOperationException("Finish or cancel the current analysis before opening another.");
        if (operationName == "AnalysisOptions" || !TemplateFactory.Operations.TryGetValue(operationName, out var operation))
            throw new System.IO.InvalidDataException("This course refers to a method that is not available in this build.");
        source?.Activate();
        // Defer entry into the existing prompted operation loop until the web-message reply has returned.
        BeginInvoke(new Action(() => DoOperationWithPossibleBatching(operation)));
        return true;
    }
}

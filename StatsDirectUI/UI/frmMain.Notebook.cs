using StatsDirect.UI.WebReports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace StatsDirect.UI;

public partial class frmMain
{
    internal Task<bool> PendingNotebookOpen { get; private set; }

    private async Task<bool> OpenNotebookSafelyAsync(string filename)
    {
        try { await ImportNotebookAsync(filename); return true; }
        catch (Exception ex)
        {
            SdApplication.SoleInstance.FriendlyError("Couldn't open Jupyter notebook", ex, true);
            return false;
        }
    }

    internal async Task ImportNotebookAsync(string filename)
    {
        ReportNotebookImport.ImportPlan plan;
        try
        {
            if (new FileInfo(filename).Length > 90_000_000) throw new IOException("This notebook exceeds the 90 MB notebook limit.");
            plan = ReportNotebookImport.Read(await File.ReadAllTextAsync(filename, new UTF8Encoding(false, true)), System.IO.Path.GetFileNameWithoutExtension(filename));
        }
        catch
        {
            // The file itself cannot be opened, so it leaves the recent files, as a report or workbook that cannot be read does.  A failure
            // of the report editor below says nothing about the file, which stays listed, as a report does in the same failure.
            SdApplication.SoleInstance.NoteRecentFile(filename, false);
            throw;
        }
        string displayName = System.IO.Path.GetFileNameWithoutExtension(filename);
        if (!string.IsNullOrWhiteSpace(displayName)) plan.Title = displayName;
        var created = new List<StatsDirectForm>();
        var previous = ActiveMdiChild;
        try
        {
            var report = (frmReportWebView)CreateReport();
            created.Add(report);
            // Complete sanitization before opening the workbook or script.
            await report.LoadNotebookAsync(plan);
            if (plan.Tables.Length > 0)
            {
                var grid = (frmSpreadsheetGear)CreateGrid(plan.Title + " data");
                created.Add(grid);
                grid.LoadNotebookTables(plan.Tables, plan.Title);
            }
            if (!string.IsNullOrWhiteSpace(plan.Script))
            {
                var script = (frmScript)CreateScriptWindow();
                created.Add(script);
                script.LoadNotebookScript(plan.Script, plan.Title);
            }
            report.Activate();
            SdApplication.SoleInstance.NoteRecentFile(filename, true);
        }
        catch
        {
            for (int i = created.Count - 1; i >= 0; i--) created[i].DiscardNotebookImport();
            if (previous != null && !previous.IsDisposed) previous.Activate();
            throw;
        }
    }
}

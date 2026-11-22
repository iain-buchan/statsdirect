using SpreadsheetGear;
using StatsDirect.Templates;
using StatsDirect.UI.Learning;
using System;

namespace StatsDirect.UI;

internal partial class frmSpreadsheetGear
{
    internal WorksheetAnalysisTarget CaptureWorksheetAnalysisTarget(bool workbookShared = false) => workbookView.WithLock(() =>
    {
        CheckWorksheetAnalysisReady();
        if (workbookView.ActiveWorkbook is not IWorkbook book || workbookView.ActiveWorksheet is not IWorksheet sheet)
            throw new InvalidOperationException("Select a worksheet before asking for an analysis.");
        return new WorksheetAnalysisTarget(this, book, sheet, Text, sheet.Name,
            workbookView.RangeSelection.Address, LearningVersion, workbookShared);
    });

    internal (frmMain Main, WorksheetAnalysisTarget Source) ResolveWorksheetAnalysisTarget(
        WorksheetAnalysisTarget target, bool allowSelectionChanges) => workbookView.WithLock(() =>
    {
        var main = CheckWorksheetAnalysisReady();
        if (target.Grid != this || target.Workbook != workbookView.ActiveWorkbook ||
            target.Worksheet != workbookView.ActiveWorksheet || target.WorksheetName != target.Worksheet.Name ||
            target.WorkbookName != Text)
            throw new InvalidOperationException("Open the original worksheet to review this analysis.");

        string selection = workbookView.RangeSelection.Address;
        if (target.Version == LearningVersion && target.Selection == selection) return (main, target);
        if (allowSelectionChanges && !target.WorkbookShared)
            return (main, target with { Selection = selection, Version = LearningVersion });

        string reason = target.WorkbookShared
            ? "The shared worksheet or selection changed. Ask for a new analysis suggestion."
            : "The worksheet or selection changed during review. Review the analysis again.";
        throw new InvalidOperationException(reason);
    });

    private frmMain CheckWorksheetAnalysisReady()
    {
        if (IsDisposed || Disposing || MdiParent is not frmMain main || main.ActiveMdiChild != this)
            throw new InvalidOperationException("Open the worksheet you asked about before starting an analysis.");
        if (main.InOperation || main.LearningCalculationRunning || workbookView.IsEditing)
            throw new InvalidOperationException("Finish the current analysis or cell edit before starting an analysis.");
        return main;
    }
}

public partial class frmMain
{
    internal void OpenReviewedWorksheetAnalysis(Operation operation)
    {
        if (!mnuMain.Enabled)
            throw new InvalidOperationException("Finish the current task before starting an analysis.");

        // Use the same selection, parameters, output routing and operation record as the Analysis menu.
        DoOperationWithPossibleBatching(operation);
    }
}

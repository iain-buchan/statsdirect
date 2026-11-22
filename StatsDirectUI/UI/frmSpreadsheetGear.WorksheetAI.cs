using SpreadsheetGear;
using SpreadsheetGear.Advanced.Cells;
using StatsDirect.UI.Learning;
using System;
using System.Collections.Generic;

namespace StatsDirect.UI;

internal partial class frmSpreadsheetGear
{
    internal WorksheetEditTarget CaptureWorksheetEditTarget() => workbookView.WithLock(() =>
    {
        if (workbookView.IsEditing || workbookView.ActiveWorkbook is not IWorkbook book || workbookView.ActiveWorksheet is not IWorksheet sheet)
            throw new InvalidOperationException("Finish editing the worksheet before asking for an edit.");
        return new WorksheetEditTarget(this, book, sheet, sheet.Name, LearningVersion);
    });

    internal WorksheetEditProposal StageWorksheetEdit(WorksheetEditTarget target, WorksheetEditRequest request) => workbookView.WithLock(() =>
    {
        CheckWorksheetEditTarget(target);
        var sheet = target.Worksheet;
        if (request.Bottom >= sheet.Workbook.WorkbookSet.MaxRows || request.Right >= sheet.Workbook.WorkbookSet.MaxColumns)
            throw new ArgumentException("The suggestions must fit this workbook's worksheet limits.");

        var changes = new List<WorksheetCellEdit>();
        var cells = (IValues)sheet;
        for (int row = request.Top; row <= request.Bottom; row++)
            for (int column = request.Left; column <= request.Right; column++)
            {
                var cell = sheet.Cells[row, column];
                CheckEditableCell(cell);
                object after = request.Values[row - request.Top, column - request.Left];
                if (after is double number && !cells.IsValidNumber(number))
                    throw new ArgumentException("A proposed number is outside the range supported by the worksheet.");

                string previousFormula = cell.HasFormula ? (string)cell.Formula : null;
                object before = previousFormula == null ? cell.Value : null;
                if (previousFormula == null && Equals(before, after)) continue;
                changes.Add(new WorksheetCellEdit(row, column, cell.Address, before, previousFormula, after));
            }
        if (changes.Count == 0) throw new InvalidOperationException("This proposal leaves all the cells as they are.");
        return new WorksheetEditProposal(target, request, Text, changes);
    });

    internal void ValidateWorksheetEdit(WorksheetEditProposal proposal) => workbookView.WithLock(() => CheckWorksheetEdit(proposal));

    internal void ApplyWorksheetEdit(WorksheetEditProposal proposal) => workbookView.WithLock(() =>
    {
        CheckWorksheetEdit(proposal);
        var sheet = proposal.Target.Worksheet;
        var request = proposal.Request;
        var affected = sheet.Cells[request.Top, request.Left, request.Bottom, request.Right];
        int version = LearningVersion;

        workbookView.ActiveCommandManager.Execute(new UndoWrapper(affected, "Apply worksheet edits", () =>
        {
            var values = (IValues)sheet;
            int attempted = 0;
            try
            {
                foreach (var cell in proposal.PreviewCells)
                {
                    attempted++;
                    WriteWorksheetEditValue(sheet, values, cell.Row, cell.Column, cell.After);
                }
            }
            catch
            {
                // Restore any cells already touched if the worksheet engine rejects a value mid-command.
                while (attempted > 0)
                {
                    var cell = proposal.PreviewCells[--attempted];
                    if (cell.PreviousFormula != null) values.SetFormula(cell.Row, cell.Column, cell.PreviousFormula);
                    else WriteWorksheetEditValue(sheet, values, cell.Row, cell.Column, cell.Before);
                }
                throw;
            }
            return true;
        }));
        if (LearningVersion == version) LearningVersion++;
        Dirty = true;
    });

    private void CheckWorksheetEdit(WorksheetEditProposal proposal)
    {
        CheckWorksheetEditTarget(proposal.Target);
        var sheet = proposal.Target.Worksheet;
        foreach (var edit in proposal.PreviewCells)
        {
            var cell = sheet.Cells[edit.Row, edit.Column];
            CheckEditableCell(cell);
            string formula = cell.HasFormula ? (string)cell.Formula : null;
            if (formula != edit.PreviousFormula || (formula == null && !Equals(cell.Value, edit.Before)))
                throw new InvalidOperationException("The cells changed since the proposal was prepared. Ask for a new edit.");
        }
    }

    private void CheckWorksheetEditTarget(WorksheetEditTarget target)
    {
        if (IsDisposed || Disposing || MdiParent is not frmMain main ||
            target.Grid != this || target.Workbook != workbookView.ActiveWorkbook || target.Worksheet != workbookView.ActiveWorksheet ||
            LearningVersion != target.Version || target.SheetName != target.Worksheet.Name)
            throw new InvalidOperationException("The worksheet changed since you asked. Open the intended sheet and ask for a new edit.");
        if (main.InOperation || main.LearningCalculationRunning || workbookView.IsEditing)
            throw new InvalidOperationException("Finish the current analysis or cell edit before applying the proposal.");
        if (target.Worksheet.ProtectContents)
            throw new InvalidOperationException("The worksheet is protected. Choose an editable worksheet for the proposal.");
    }

    private static void CheckEditableCell(IRange cell)
    {
        // The typed cell setters deliberately bypass protection, validation, array and merge rules.
        if (cell.MergeCells || cell.HasArray || cell.HasValidation)
            throw new InvalidOperationException("Choose a range without merged cells, array formulas or worksheet validation rules.");
    }

    private static void WriteWorksheetEditValue(IWorksheet sheet, IValues values, int row, int column, object value)
    {
        if (value == null) values.Clear(row, column);
        else if (value is ValueError error) values.SetError(row, column, error);
        else SpreadsheetFiles.SetLiteral(sheet, row, column, value);
    }
}

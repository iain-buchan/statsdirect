using SpreadsheetGear;
using SpreadsheetGear.Advanced.Cells;
using StatsDirect.UI.Learning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

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
        IWorkbook preview = null;
        try
        {
            for (int row = request.Top; row <= request.Bottom; row++)
                for (int column = request.Left; column <= request.Right; column++)
                {
                    var cell = sheet.Cells[row, column];
                    CheckEditableCell(cell);
                    object after = request.Values[row - request.Top, column - request.Left];
                    if (after is double number && !cells.IsValidNumber(number))
                        throw new ArgumentException("A proposed number is outside the range supported by the worksheet.");

                    string previousFormula = cell.HasFormula ? (string)cell.Formula : null;
                    object before = cell.Value;
                    if (previousFormula == null && Equals(before, after)) continue;

                    string format = cell.NumberFormat;
                    string formattedBefore = cell.Text;
                    string formattedAfter = FormatWorksheetReplacement(after, format, sheet.Workbook, ref preview);
                    string address = cell.Address.Replace("$", "", StringComparison.Ordinal);
                    changes.Add(new WorksheetCellEdit(row, column, address, before, previousFormula, after,
                        format, formattedBefore, formattedAfter));
                }
        }
        finally { preview?.Close(); }
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
            if (formula != edit.PreviousFormula || !Equals(cell.Value, edit.Before) ||
                cell.NumberFormat != edit.NumberFormat || cell.Text != edit.FormattedBefore)
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

    private static string FormatWorksheetReplacement(object value, string numberFormat, IWorkbook source, ref IWorkbook preview)
    {
        if (value == null) return "";
        // Format the literal with the same spreadsheet engine and date system, without
        // temporarily writing values or formulas into the user's worksheet.
        preview ??= CreateWorksheetEditPreviewWorkbook(Factory.GetWorkbookSet(source.WorkbookSet.Culture), source.Date1904);
        var sheet = preview.Worksheets[0];
        var cell = sheet.Cells[0, 0];
        if (cell.NumberFormat != numberFormat) cell.NumberFormat = numberFormat;
        SpreadsheetFiles.SetLiteral(sheet, 0, 0, value);
        return cell.Text;
    }

    internal static IWorkbook CreateWorksheetEditPreviewWorkbook(IWorkbookSet workbookSet, bool date1904)
    {
        var blank = workbookSet.Workbooks.Add();
        if (!date1904) return blank;

        // SpreadsheetGear exposes the date system as read-only. Patch the workbook
        // property on an empty in-memory .xlsx, then let the normal reader apply it.
        using var buffer = new MemoryStream();
        try { blank.SaveToStream(buffer, FileFormat.OpenXMLWorkbook); }
        finally { blank.Close(); }
        buffer.Position = 0;
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            var part = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("The preview workbook has no workbook properties.");
            XDocument xml;
            using (var stream = part.Open()) xml = XDocument.Load(stream);
            var root = xml.Root;
            XNamespace ns = root.Name.Namespace;
            var properties = root.Element(ns + "workbookPr");
            if (properties == null)
            {
                properties = new XElement(ns + "workbookPr");
                var previous = root.Element(ns + "fileSharing") ?? root.Element(ns + "fileVersion");
                if (previous == null) root.AddFirst(properties);
                else previous.AddAfterSelf(properties);
            }
            properties.SetAttributeValue("date1904", "1");
            using var output = part.Open();
            output.SetLength(0);
            xml.Save(output);
        }
        buffer.Position = 0;
        return workbookSet.Workbooks.OpenFromStream(buffer);
    }

    private static void WriteWorksheetEditValue(IWorksheet sheet, IValues values, int row, int column, object value)
    {
        if (value == null) values.Clear(row, column);
        else if (value is ValueError error) values.SetError(row, column, error);
        else SpreadsheetFiles.SetLiteral(sheet, row, column, value);
    }
}

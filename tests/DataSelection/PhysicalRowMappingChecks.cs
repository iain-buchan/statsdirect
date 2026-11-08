using System.Reflection;
using System.Xml.Serialization;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.UI;
using StatsDirect.Utilities;

// Acquisition coordinates must describe the visible values, including a hidden
// title or an empty header recognized later from the neighbouring identifier.
internal static class PhysicalRowMappingChecks
{
    static int checks, failures;
    static readonly Type processor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.CellArrayProcessor");
    static readonly MethodInfo read = processor.GetMethod("ProcessCellArray", BindingFlags.Public | BindingFlags.Static);
    static readonly MethodInfo normalizeHeader = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor")
        .GetMethod("NormalizeSelectedGroupHeaders", BindingFlags.NonPublic | BindingFlags.Static);

    static void Check(bool okay, string description)
    {
        checks++; if (!okay) failures++;
        Console.WriteLine($"{(okay ? "ok  " : "FAIL")}  physical rows: {description}");
    }

    static DataFrame Read(MatchedCells cells, int column, int first, int count, DataAcquisitionMode mode = DataAcquisitionMode.NumericPreserveRows, bool refill = false, bool title = false)
    {
        CellSelection selected = cells.Selection(new[] { (column, first, count) });
        return (DataFrame)read.Invoke(null, new object[] { selected, mode, 0, refill, title, 7, "physical-row-fixture" });
    }
    static WorksheetOrigin Origin(DataFrame frame) => (WorksheetOrigin)frame.Variables[0].Origin;
    static bool Map(WorksheetOrigin origin, params int[] rows) => origin.DataRowIndices != null && origin.DataRowIndices.SequenceEqual(rows);

    internal static int Run()
    {
        checks = failures = 0;
        object[] ordinary = { "Measurement", 10d, 20d, 30d, 40d, null, null };
        var unfiltered = new MatchedCells(new[] { ordinary });
        WorksheetOrigin contiguous = Origin(Read(unfiltered, 0, 0, ordinary.Length));
        Check(!contiguous.WasFiltered && contiguous.DataRowIndices == null, "ordinary contiguous selection allocates no physical-row map");

        var hidden = new MatchedCells(new[] { ordinary }, 2);
        DataFrame selected = Read(hidden, 0, 0, ordinary.Length);
        WorksheetOrigin origin = Origin(selected);
        Check(origin.HasTitle && origin.WasFiltered && Map(origin, 1, 3, 4, 5, 6), "visible selected title is excluded while hidden rows keep their physical coordinates");
        Check(((DoubleVariable)selected.Variables[0]).Data.SequenceEqual(new[] { 10d, 30d, 40d, Constant.MISSING, Constant.MISSING }),
            "the map corresponds one-to-one with the actual acquired values and blank tail");
        Check(origin.TopRow == 0 && origin.Rows == ordinary.Length, "the original physical selection range remains intact");
        Check(((IGetCells)hidden).GetVisibleRowIndices(0, 0, 6).SequenceEqual(new[] { 0, 1, 3, 4, 5, 6 }),
            "the backwards-compatible reader fallback obtains physical visible rows");
        Check(((IGetCells)hidden).GetVisibleRowIndices(0, 3, 2).Length == 0, "the visibility fallback handles an empty range");

        object[] noTitle = { 10d, 20d, 30d, 40d, null, null };
        WorksheetOrigin withoutTitle = Origin(Read(new MatchedCells(new[] { noTitle }, 1), 0, 0, noTitle.Length));
        Check(!withoutTitle.HasTitle && Map(withoutTitle, 0, 2, 3, 4, 5), "a title-free filtered selection retains the first visible observation");

        DataFrame hiddenTitle = Read(new MatchedCells(new[] { ordinary }, 0, 2), 0, 0, ordinary.Length, refill: true, title: true);
        Check(Origin(hiddenTitle).HasTitle && Map(Origin(hiddenTitle), 1, 3, 4, 5, 6) && ((DoubleVariable)hiddenTitle.Variables[0]).Data[0] == 10,
            "a hidden physical header is not subtracted a second time on recorded replay");

        object[] offset = new object[10].Concat(ordinary).ToArray();
        WorksheetOrigin offsetOrigin = Origin(Read(new MatchedCells(new[] { offset }, 12), 0, 10, ordinary.Length));
        Check(offsetOrigin.TopRow == 10 && Map(offsetOrigin, 11, 13, 14, 15, 16), "an offset filtered selection records absolute physical data rows");
        DataFrame none = Read(new MatchedCells(new[] { ordinary }, Enumerable.Range(0, ordinary.Length).ToArray()), 0, 0, ordinary.Length);
        Check(none.MaxRows == 0 && Map(Origin(none)), "an entirely hidden selection has an empty data-row map");
        Check(Origin(Read(hidden, 0, 0, ordinary.Length, DataAcquisitionMode.NumericReplaceMissing)).DataRowIndices == null,
            "legacy acquisition modes do not invent an unsupported row map");

        object[] ids = { "Group", "A", "A", "B", "B", null, null };
        object[] blankHeader = { null, 10d, 20d, 30d, 40d, null, null };
        var paired = new MatchedCells(new[] { ids, blankHeader }, 2);
        WorksheetOrigin identifiers = Origin(Read(paired, 0, 0, 7, DataAcquisitionMode.CategoryCombineAllColumns));
        DataFrame numeric = Read(paired, 1, 0, 7);
        WorksheetOrigin before = Origin(numeric);
        Check(!before.HasTitle && Map(before, 0, 1, 3, 4, 5, 6), "an unconfirmed empty header initially keeps its visible worksheet row");
        normalizeHeader.Invoke(null, new object[] { numeric, 5, identifiers, paired });
        WorksheetOrigin after = Origin(numeric);
        Check(after.HasTitle && Map(after, 1, 3, 4, 5, 6) && numeric.MaxRows == 5, "proving an empty header drops the corresponding physical-row entry");
        Check(Map(before, 0, 1, 3, 4, 5, 6) && !before.HasTitle, "header normalization does not mutate the earlier acquisition origin or map");

        using var text = new StringWriter();
        new XmlSerializer(typeof(WorksheetOrigin)).Serialize(text, origin);
        Check(!text.ToString().Contains("DataRowIndices", StringComparison.Ordinal), "runtime-only visibility metadata does not change worksheet-origin XML");
        Console.WriteLine($"PHYSICAL ROW MAPPING: {checks} checks, {failures} failures");
        return failures;
    }
}

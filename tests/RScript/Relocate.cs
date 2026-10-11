using StatsDirect.R;

internal static partial class Program
{
    // A record's pointers go stale when an analysis writes its result columns to the left of its data, or a column is inserted or removed
    // by hand, because the record is written at the report step before such output: the reader finds a column again by its hash and title.
    private static void CheckColumnLocator()
    {
        // a sheet as a fake reader sees it: column titles and the hashes of their values; null beyond the sheet
        (string title, string hash)?[] sheet = { null, ("Before", "h-before"), ("After", "h-after"), ("Group", "h-group"), ("Score", "h-score"), ("After", "h-after2") };
        (string title, string hash)? At(int column) => column >= 1 && column < sheet.Length ? sheet[column] : null;
        ColumnPointer Pointer(int column, string title, string hash, bool titled = true) => new() { Column = column, Title = title, Hash = hash, HasTitle = titled, FirstRow = 2, LastRow = 21 };
        Check(ColumnLocator.Find(Pointer(2, "After", "h-after"), At) == 2, "a column still at its recorded place is found there");
        Check(ColumnLocator.Find(Pointer(1, "After", "h-after"), At) == 2, "a column moved one to the right, by a column inserted to its left, is found there");
        Check(ColumnLocator.Find(Pointer(4, "Before", "h-before"), At) == 1, "a column moved to the left, by columns removed to its left, is found there");
        Check(ColumnLocator.Find(Pointer(1, "After", "h-after2"), At) == 5, "of two columns with one title, the one whose values hash as the record's is found");
        Check(ColumnLocator.Find(Pointer(2, "Score", "h-after"), At) == null, "a column whose title no longer matches is not taken for the record's");
        Check(ColumnLocator.Find(Pointer(2, "Score", "h-after", titled: false), At) == 2, "without a title to go by, the hash alone identifies the column");
        Check(ColumnLocator.Find(Pointer(3, "Group", "h-changed"), At) == null, "a column whose values have changed is found nowhere");
        Check(ColumnLocator.Find(Pointer(3, "Group", null), At) == 3, "a pointer without a hash keeps its recorded place");
        Check(ColumnLocator.Find(Pointer(1, "After", "h-after2"), At, reach: 2) == null, "a column beyond the reach is not found");
    }
}

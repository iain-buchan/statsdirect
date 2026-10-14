using SpreadsheetGear;
using StatsDirect.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace StatsDirect.R;

/// <summary>Base-R interchange, shared with the Mac host. All cells cross as values, never executable source.</summary>
internal static class RDataFiles
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    internal sealed class Table
    {
        internal JsonObject Schema;
        internal string[] RowNames;
        internal string Fingerprint;
        internal readonly Dictionary<int, Dictionary<string, string>> PreciseDates = new();
        internal readonly HashSet<int> AmbiguousDates = new();
        internal readonly Dictionary<(int Row, int Column), string> OriginalDates = new();
    }
    internal sealed class Metadata : Dictionary<IWorksheet, Table> { }
    internal static bool IsRData(string extension) => extension is ".rds" or ".rdata" or ".rda";

    internal static string Executable()
    {
        var r = RController.PreferredRVersion();
        if (r == null && RController.UserMightHaveInstalledR()) r = RController.PreferredRVersion();
        return r == null ? throw new OperationCanceledException("R is required to open or save an R data file.") : Path.Combine(r.BinPath, "Rscript.exe");
    }

    internal static IWorkbook Read(IWorkbookSet set, string path, string executable, out Metadata metadata)
    {
        using var exchange = new Exchange();
        Run(executable, "read", Path.GetFullPath(path), exchange.Folder);
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(exchange.Folder, "manifest.json")));
        var book = set.Workbooks.Add(); metadata = new();
        try
        {
            int t = 0;
            foreach (JsonObject schema in manifest["tables"].AsArray())
            {
                int rows = schema["rows"].GetValue<int>(); var columns = schema["columns"].AsArray();
                if (rows < 0 || rows >= 1048576 || columns.Count is < 1 or > 16384) throw new InvalidDataException("R returned a table outside Excel's dimensions.");
                var sheet = t == 0 ? book.Worksheets[0] : book.Worksheets.Add();
                sheet.Name = SpreadsheetFiles.SheetName(schema["name"].GetValue<string>(), book, sheet);
                var table = new Table { Schema = schema.DeepClone().AsObject() };
                metadata[sheet] = table;
                for (int c = 0; c < columns.Count; c++)
                {
                    var column = columns[c]; string name = column["name"].GetValue<string>(), type = column["type"].GetValue<string>();
                    SpreadsheetFiles.SetLiteral(sheet, 0, c, name);
                    string prefix = Path.Combine(exchange.Folder, $"col-{t}-{c}");
                    byte[] kinds = File.ReadAllBytes(prefix + ".kinds");
                    if (kinds.Length != rows) throw new InvalidDataException("R returned incomplete column data.");
                    var text = Lines(prefix + ".txt", rows);
                    double[] numbers = File.Exists(prefix + ".nums") ? Doubles(prefix + ".nums", rows) : null;
                    double[] raw = File.Exists(prefix + ".raw") ? Doubles(prefix + ".raw", rows) : null;
                    var dates = new Dictionary<string, string>();
                    if (raw != null) table.PreciseDates[c] = dates;
                    var ordinaryDates = new HashSet<string>();
                    for (int first = 0; first < rows; first += 4096)
                    {
                        int count = Math.Min(4096, rows-first); var values = new object[count,1];
                        for (int i = 0; i < count; i++)
                        {
                            int r = first+i;
                            values[i,0] = kinds[r] switch { 0 => null, 1 => numbers[r], 5 => text[r] == "TRUE", _ => text[r] };
                            if (raw != null && !double.IsNaN(raw[r]))
                            {
                                string exact = raw[r].ToString("R", Invariant);
                                table.OriginalDates[(r,c)] = exact;
                                if (dates.TryGetValue(text[r], out string previous) && previous != exact) table.AmbiguousDates.Add(c);
                                dates[text[r]] = exact;
                            }
                            else if (raw != null && kinds[r] != 0) ordinaryDates.Add(text[r]);
                        }
                        for (int i = 0; i < count; i++) SpreadsheetFiles.SetLiteral(sheet, first+i+1, c, values[i,0]);
                    }
                    if (dates.Keys.Any(ordinaryDates.Contains)) table.AmbiguousDates.Add(c);
                }
                if (schema["rowNames"].GetValue<bool>()) table.RowNames = Lines(Path.Combine(exchange.Folder, $"rows-{t}.txt"), rows);
                table.Fingerprint = Fingerprint(sheet, rows, columns.Count);
                t++;
            }
            var warnings = manifest["warnings"].AsArray();
            // Keep the skipped-object notice with the imported workbook so the UI can show it once.
            LastWarnings = string.Join("; ", warnings.Select(w => w.GetValue<string>()));
            return book;
        }
        catch { book.Close(); throw; }
    }
    internal static string LastWarnings { get; private set; } = "";

    internal static void Write(IWorkbook book, IWorksheet current, string path, Metadata metadata, string executable)
    {
        bool single = Path.GetExtension(path).Equals(".rds", StringComparison.OrdinalIgnoreCase);
        using var exchange = new Exchange();
        var manifest = new List<string> { "table,object,shape,rows,column,type,ordered,tzone,naLevel,rowNamesType" };
        var sheets = single ? new[] { current } : book.Worksheets.Cast<IWorksheet>().ToArray();
        var objectNames = new HashSet<string>();
        for (int t = 0; t < sheets.Length; t++)
        {
            var sheet = sheets[t]; metadata.TryGetValue(sheet, out var original);
            var used = sheet.UsedRange;
            int columns = used.Column + used.ColumnCount, rows = used.Row + used.RowCount - 1;
            if (original != null) rows = Math.Max(rows, original.Schema["rows"].GetValue<int>());
            if (rows < 0 || columns < 1) throw new InvalidDataException("The worksheet needs a heading row and at least one column.");
            var names = Enumerable.Range(0, columns).Select(c => Convert.ToString(sheet.Cells[0,c].Value, Invariant) ?? "").ToArray();
            if (original == null && names.Any(string.IsNullOrEmpty)) throw new InvalidDataException("R data export uses the first row as column names. Give every column a heading first.");
            bool sameSchema = original != null && original.Schema["columns"].AsArray().Select(c => c["name"].GetValue<string>()).SequenceEqual(names);
            if (original != null && !sameSchema) throw new InvalidDataException("The imported R table's columns changed. Restore their names and order before saving as R data, or save the edited data as Excel.");
            bool unchanged = original != null && Fingerprint(sheet, rows, columns) == original.Fingerprint;
            if (!unchanged && original?.RowNames is { Length: > 0 }) throw new InvalidDataException("This R table has explicit row names. To avoid assigning them to the wrong rows after editing or sorting, save the edited data as Excel, or export a copy with automatic row names.");
            if (!unchanged && original?.AmbiguousDates.Count > 0) throw new InvalidDataException("This R table has timestamps that look identical but differ beyond displayed precision. Save the edited data as Excel, or edit these values in R to preserve their exact timestamps.");
            string objectName = original?.Schema["object"].GetValue<string>() ?? sheet.Name;
            string stem = objectName; int suffix = 1; while (!objectNames.Add(objectName)) objectName = stem + "_" + ++suffix;
            WriteLines(Path.Combine(exchange.Folder, $"names-{t}.txt"), names);
            for (int c = 0; c < columns; c++)
            {
                var values = ColumnValues(sheet, rows, c);
                var schema = sameSchema ? original.Schema["columns"][c] : null;
                string type = schema?["type"]?.GetValue<string>() ?? InferType(values);
                manifest.Add(string.Join(",", new[] { t.ToString(Invariant), objectName, original?.Schema["shape"].GetValue<string>() ?? "data.frame", rows.ToString(Invariant), c.ToString(Invariant), type, schema?["ordered"]?.ToJsonString() ?? "false", schema?["tzone"]?.GetValue<string>() ?? "", schema?["naLevel"]?.ToJsonString() ?? "false", original?.Schema["rowNamesType"]?.GetValue<string>() ?? "character" }.Select(Csv)));
                string prefix = Path.Combine(exchange.Folder, $"col-{t}-{c}");
                var text = new string[rows]; var missing = new byte[rows]; var numbers = new double[rows]; var raw = new string[rows];
                for (int r = 0; r < rows; r++)
                {
                    object value = values[r]; missing[r] = value == null ? (byte)1 : (byte)0;
                    text[r] = value is bool boolean ? (boolean ? "TRUE" : "FALSE") : Convert.ToString(value, Invariant) ?? "";
                    numbers[r] = value is double number ? number : double.NaN;
                    raw[r] = "";
                    if (original != null && unchanged)
                    { if (original.OriginalDates.TryGetValue((r,c), out string exact)) raw[r] = exact; }
                    else if (original != null && original.PreciseDates.TryGetValue(c, out var precise) && precise.TryGetValue(text[r], out string exact)) raw[r] = exact;
                }
                WriteLines(prefix + ".txt", text); File.WriteAllBytes(prefix + ".missing", missing);
                if (type is "double" or "integer") WriteDoubles(prefix + ".nums", numbers);
                WriteLines(prefix + ".raw", raw);
                WriteLines(Path.Combine(exchange.Folder, $"levels-{t}-{c}.txt"), schema?["levels"]?.AsArray().Select(l => l.GetValue<string>()) ?? []);
            }
            WriteLines(Path.Combine(exchange.Folder, $"rows-{t}.txt"), original?.RowNames ?? []);
        }
        File.WriteAllLines(Path.Combine(exchange.Folder, "manifest.csv"), manifest, new UTF8Encoding(false));
        string target = Path.GetFullPath(path), temp = Path.Combine(Path.GetDirectoryName(target), ".statsdirect-" + Guid.NewGuid().ToString("N") + Path.GetExtension(target));
        try { Run(executable, "write", exchange.Folder, temp, single ? "rds" : "rdata"); File.Move(temp, target, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static object[] ColumnValues(IWorksheet sheet, int rows, int column)
    {
        var result = new object[rows];
        for (int first = 0; first < rows; first += 4096)
        {
            int count = Math.Min(4096, rows-first); object batch = sheet.Cells[first+1,column,first+count,column].Value;
            if (batch is object[,] cells) for (int i = 0; i < count; i++) result[first+i] = cells[i,0];
            else result[first] = batch;
        }
        return result;
    }
    private static string InferType(object[] values) => values.All(v => v == null || v is double) ? "double" : values.All(v => v == null || v is bool) ? "logical" : "character";
    private static string Fingerprint(IWorksheet sheet, int rows, int columns)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int c = 0; c < columns; c++) foreach (var value in ColumnValues(sheet, rows, c))
        {
            string text = value == null ? "null" : value.GetType().Name + ":" + Convert.ToString(value, Invariant);
            byte[] bytes = Encoding.UTF8.GetBytes(text); hash.AppendData(BitConverter.GetBytes(bytes.Length)); hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static string[] Lines(string path, int count)
    {
        // R writes raw UTF-8 lines; a leading U+FEFF can be a data value, not a file BOM.
        using var reader = new StreamReader(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        var all = new List<string>(); string line;
        while ((line = reader.ReadLine()) != null) all.Add(Unescape(line));
        var lines = all.ToArray();
        if (lines.Length != count) throw new InvalidDataException("R returned incomplete text data."); return lines;
    }
    private static string Unescape(string value) => value.Replace("%0A", "\n").Replace("%0D", "\r").Replace("%EF%BB%BF", "\ufeff").Replace("%25", "%");
    private static void WriteLines(string path, IEnumerable<string> lines) => File.WriteAllLines(path, lines.Select(s => s.Replace("%", "%25").Replace("\n", "%0A").Replace("\r", "%0D").Replace("\ufeff", "%EF%BB%BF")), new UTF8Encoding(false));
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static double[] Doubles(string path, int count)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (reader.BaseStream.Length != 8L*count) throw new InvalidDataException("R returned incomplete numeric data.");
        return Enumerable.Range(0,count).Select(_ => reader.ReadDouble()).ToArray();
    }
    private static void WriteDoubles(string path, double[] values) { using var writer = new BinaryWriter(File.Create(path)); foreach (double value in values) writer.Write(value); }
    private static void Run(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("--vanilla"); start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "R", "data-files.R"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("R could not be started.");
        var error = process.StandardError.ReadToEndAsync(); var output = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(300_000)) { process.Kill(true); process.WaitForExit(); throw new IOException("R data-file conversion took too long. The original file has not been changed."); }
        string details = error.GetAwaiter().GetResult(); output.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidDataException("R could not convert this data file. " + details.Trim());
    }
    private sealed class Exchange : IDisposable
    {
        internal string Folder { get; } = Path.Combine(Path.GetTempPath(), "StatsDirect-R-data-" + Guid.NewGuid().ToString("N"));
        internal Exchange() => Directory.CreateDirectory(Folder);
        public void Dispose() { try { Directory.Delete(Folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}

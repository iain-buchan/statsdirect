using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SpreadsheetGear;
using StatsDirect.UI;

// The worksheet R import against the examples shared with StatsDirect for Mac (statisticalhelp WorksheetR/examples.json, which
// encodes WorksheetR/RULES.md).  Each case's cells are built in a SpreadsheetGear workbook, the prompt's labels and suggestion are
// checked, the production import writes its CSV and script, and R checks that the object is identical() to the expected one.
// Deterministic: no window.  Needs Rscript (STATSDIRECT_RSCRIPT, else the newest R under Program Files).
internal static class Program
{
    private static int checks;
    private static readonly List<string> failures = new();

    private static void Check(bool ok, string description)
    {
        if (ok) { checks++; return; }
        failures.Add(description);
        Console.WriteLine("FAIL " + description);
    }

    private static int Main(string[] args)
    {
        try
        {
            string examples = Environment.GetEnvironmentVariable("STATSDIRECT_WORKSHEET_R_EXAMPLES") ?? FindExamples();
            using var document = JsonDocument.Parse(File.ReadAllText(examples, Encoding.UTF8));
            License();
            string work = Directory.CreateTempSubdirectory("StatsDirect-worksheet-r-examples-").FullName;
            var runs = new List<(string Name, string Script, string Expected)>();
            int index = 0;
            foreach (var example in document.RootElement.GetProperty("cases").EnumerateArray())
            {
                string name = example.GetProperty("name").GetString();
                string folder = Path.Combine(work, (index++).ToString(CultureInfo.InvariantCulture));
                if (RunCase(name, example, folder) is (string script, string expected))
                    runs.Add((name, script, expected));
            }
            Check(index >= 50, "the shared examples hold at least the 50 cases of the approved rules");
            RunR(runs, work);
            if (failures.Count == 0)
            {
                try { Directory.Delete(work, true); } catch (IOException) { }
                Console.WriteLine($"PASS {checks} worksheet R example checks ({index} cases, {runs.Count} compared in R) against {examples}");
                return 0;
            }
            Console.WriteLine($"{failures.Count} of {checks + failures.Count} worksheet R example checks failed; files kept in {work}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL " + ex);
            return 1;
        }
    }

    // The pinned shared source, found from the test's folder upwards.
    private static string FindExamples()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "Learning", "Shared", "WorksheetR", "examples.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("WorksheetR/examples.json was not found under Learning/Shared; set STATSDIRECT_WORKSHEET_R_EXAMPLES.");
    }

    // The licence StatsDirect is built with, where it has one; a licence-free build uses SpreadsheetGear unlicensed.
    private static void License()
    {
        var attribute = typeof(frmMain).Assembly.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "SpreadsheetGearLicenseAttribute");
        if (attribute?.GetType().GetProperty("LicenseString")?.GetValue(attribute) is string license && license.Length > 0)
            Factory.SetSignedLicense(license);
    }

    private static (string, string)? RunCase(string name, JsonElement example, string folder)
    {
        if (example.TryGetProperty("skip", out var skip) && skip.TryGetProperty("windows", out var reason))
        {
            Console.WriteLine($"SKIP {name}: {reason.GetString()}");
            return null;
        }
        var expectation = example.GetProperty("expect");
        if (expectation.TryGetProperty("refusedNaming", out var refused) && example.GetProperty("cells").EnumerateObject()
                .FirstOrDefault(c => c.Value.TryGetProperty("text", out var t) && t.GetString()!.Contains('\0')) is { Name: string nul } && nul != null)
        {
            // A SpreadsheetGear cell ends its text at a null character, so a Windows worksheet never holds one: the rule that refuses
            // it is checked on the cell rules themselves, for the first such cell in sheet order.
            try
            {
                WorksheetRImport.Classify(example.GetProperty("cells").GetProperty(nul).GetProperty("text").GetString(),
                    () => NumberFormatType.General, () => "General", false, nul);
                Check(false, $"{name}: the null character was not refused");
            }
            catch (InvalidDataException message)
            {
                Check(nul == refused.GetString() && message.Message.Contains(nul, StringComparison.Ordinal), $"{name}: the refusal names {refused.GetString()}");
                if (expectation.TryGetProperty("refusedMessage", out var wording))
                    Check(message.Message == wording.GetString(), $"{name}: the refusal reads \"{message.Message}\"");
            }
            return null;
        }
        var workbook = Factory.GetWorkbook(CultureInfo.InvariantCulture);
        var sheet = workbook.Worksheets[0];
        sheet.Name = "Sheet1";
        foreach (var cell in example.GetProperty("cells").EnumerateObject())
            if (!SetCell(sheet.Range[cell.Name], cell.Value, out string problem))
                Check(false, $"{name}: cell {cell.Name}: {problem}");
        if (example.TryGetProperty("formatOnly", out var formatOnly))
            foreach (var reference in formatOnly.EnumerateArray()) sheet.Range[reference.GetString()].NumberFormat = "0.00";
        if (example.TryGetProperty("merged", out var merged))
            foreach (var area in merged.EnumerateArray()) sheet.Range[area.GetString()].Merge();

        var select = example.GetProperty("select");
        IRange selection = select.TryGetProperty("range", out var range) ? sheet.Range[range.GetString()]
            : select.TryGetProperty("columns", out var columns) ? sheet.Range[string.Join(",", columns.EnumerateArray().Select(c => c.GetString() + ":" + c.GetString()))]
            : select.TryGetProperty("rows", out var rows) ? sheet.Range[string.Join(",", rows.EnumerateArray().Select(r => r.GetString()))]
            : select.TryGetProperty("areas", out var areas) ? sheet.Range[string.Join(",", areas.EnumerateArray().Select(a => a.GetString()))]
            : select.TryGetProperty("all", out _) ? sheet.Cells
            : sheet.Range["A1"];   // "entire" and "none": the active cell, which a Windows worksheet always has
        bool entireChoice = select.TryGetProperty("entire", out _);

        var options = WorksheetRImport.Describe(sheet, selection, write => write());
        var expect = example.GetProperty("expect");
        if (expect.TryGetProperty("entire", out var entireLabel))
            Check(WorksheetRPrompt.EntireLabel(options) == PlatformText(entireLabel), $"{name}: Entire worksheet label \"{WorksheetRPrompt.EntireLabel(options)}\"");
        if (expect.TryGetProperty("selected", out var selectedLabel))
            Check(WorksheetRPrompt.SelectedLabel(options) == PlatformText(selectedLabel), $"{name}: Selected cells label \"{WorksheetRPrompt.SelectedLabel(options)}\"");
        if (expect.TryGetProperty("entireEnabled", out var entireEnabled))
            Check(options.EntireSheet != null == entireEnabled.GetBoolean(), $"{name}: Entire worksheet available");
        if (expect.TryGetProperty("selectedEnabled", out var selectedEnabled))
            Check(options.SelectedCells != null == selectedEnabled.GetBoolean(), $"{name}: Selected cells available");
        if (expect.TryGetProperty("labelsOnly", out _)) return null;

        WorksheetRData data = entireChoice ? options.EntireSheet : options.SelectedCells;
        if (data == null)
        {
            Check(expect.GetProperty("r").ValueKind == JsonValueKind.Null && !expect.TryGetProperty("refusedNaming", out _),
                $"{name}: the data choice is unavailable, but the example expects an import");
            return null;
        }
        if (expect.TryGetProperty("headingsSuggested", out var suggested))
            Check(data.SuggestHeaders == suggested.GetBoolean(), $"{name}: headings suggested is {data.SuggestHeaders}");
        if (expect.TryGetProperty("headingsEnabled", out var headingsEnabled))
            Check(data.RowCount > 1 == headingsEnabled.GetBoolean(), $"{name}: the first-row check box enabled");

        var choice = example.GetProperty("options");
        bool headers = choice.GetProperty("headings").ValueKind == JsonValueKind.Null ? data.SuggestHeaders : choice.GetProperty("headings").GetBoolean();
        var format = choice.GetProperty("format").GetString() == "list" ? WorksheetRDataFormat.VectorList : WorksheetRDataFormat.DataFrame;
        bool friendly = choice.GetProperty("friendly").GetBoolean();
        string script;
        try
        {
            script = data.WriteImportScript(headers, format, friendly, folder);
        }
        catch (InvalidDataException refusal) when (expect.TryGetProperty("refusedNaming", out var cell))
        {
            Check(refusal.Message.Contains(cell.GetString()!, StringComparison.Ordinal), $"{name}: the refusal names {cell.GetString()}: {refusal.Message}");
            if (expect.TryGetProperty("refusedMessage", out var message))
                Check(refusal.Message == message.GetString(), $"{name}: the refusal reads \"{refusal.Message}\"");
            return null;
        }
        if (expect.TryGetProperty("refusedNaming", out _))
        {
            Check(false, $"{name}: the import was not refused");
            return null;
        }
        if (expect.TryGetProperty("comments", out var comments))
            foreach (var line in comments.EnumerateArray())
                Check(script.Split('\n').Contains(line.GetString()), $"{name}: the script has the comment \"{line.GetString()}\"");
        return expect.GetProperty("r").ValueKind == JsonValueKind.Null ? null : (script, expect.GetProperty("r").GetString());
    }

    private static string PlatformText(JsonElement label) =>
        label.ValueKind == JsonValueKind.Object ? label.GetProperty("windows").GetString() : label.GetString();

    // A stored cell from the example's schema.
    private static bool SetCell(IRange cell, JsonElement spec, out string problem)
    {
        problem = null;
        if (spec.TryGetProperty("number", out var number)) cell.Value = double.Parse(number.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
        else if (spec.TryGetProperty("text", out var text) || spec.TryGetProperty("textUnits", out text))
        {
            // Text is stored as text: SpreadsheetGear reads "0012", "true" and "#DIV/0!" given as a Value as a number, a Boolean
            // and an error, as Excel does with typed entry, unless the cell is formatted as text.
            string value = text.ValueKind == JsonValueKind.Array ? new string(text.EnumerateArray().Select(u => (char)u.GetInt32()).ToArray()) : text.GetString();
            cell.NumberFormat = "@";
            cell.Value = value;
            if (cell.Value as string != value) problem = "SpreadsheetGear does not hold this text as given";
        }
        else if (spec.TryGetProperty("boolean", out var boolean)) cell.Value = boolean.GetBoolean();
        else if (spec.TryGetProperty("error", out var error)) cell.Value = error.GetString() switch
        {
            "#DIV/0!" => ValueError.Div0, "#N/A" => ValueError.NA, "#VALUE!" => ValueError.Value, "#REF!" => ValueError.Ref,
            "#NAME?" => ValueError.Name, "#NUM!" => ValueError.Num, "#NULL!" => ValueError.Null, var other => throw new FormatException(other)
        };
        else if (spec.TryGetProperty("formula", out var formula))
        {
            cell.Formula = formula.GetString();
            var expected = spec.GetProperty("value");
            object value = cell.Value;
            bool same = expected.TryGetProperty("number", out var n) ? value is double d && d == double.Parse(n.GetString()!, CultureInfo.InvariantCulture)
                : expected.TryGetProperty("text", out var t) ? value is string s && s == t.GetString()
                : false;
            if (!same) problem = $"the formula's value is {value ?? "blank"}, not the example's";
        }
        else
        {
            string kind = new[] { "date", "datetime", "time", "duration" }.FirstOrDefault(k => spec.TryGetProperty(k, out _));
            if (kind == null) { problem = "an unknown kind of cell"; return false; }
            cell.Value = double.Parse(spec.GetProperty(kind).GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
            cell.NumberFormat = spec.GetProperty("format").GetString();
        }
        return problem == null;
    }

    // One R process checks every case: each script runs in a clean global environment, and its worksheet_data must be identical()
    // to the expected expression, evaluated with base R only.
    private static void RunR(List<(string Name, string Script, string Expected)> runs, string work)
    {
        var runner = new StringBuilder("results <- character()\n");
        for (int i = 0; i < runs.Count; i++)
        {
            string script = Path.Combine(work, $"case-{i}.R"), expected = Path.Combine(work, $"case-{i}-expected.R");
            File.WriteAllText(script, runs[i].Script, new UTF8Encoding(false));
            File.WriteAllText(expected, runs[i].Expected + "\n", new UTF8Encoding(false));
            runner.Append("local({\n")
                .Append("  rm(list = ls(globalenv(), all.names = TRUE), envir = globalenv())\n")
                .Append($"  out <- tryCatch({{ utils::capture.output(source({R(script)}, local = globalenv(), encoding = \"UTF-8\")); \"\" }}, error = function(e) conditionMessage(e))\n")
                .Append($"  want <- eval(parse(file = {R(expected)}, encoding = \"UTF-8\")[[1]], baseenv())\n")
                .Append("  got <- if (exists(\"worksheet_data\", envir = globalenv(), inherits = FALSE)) get(\"worksheet_data\", envir = globalenv()) else NULL\n")
                .Append($"  cat(if (nzchar(out)) paste0(\"FAIL {i}: the script failed: \", out) else if (identical(got, want)) \"PASS {i}\" else paste0(\"FAIL {i}: got \", paste(utils::capture.output(dput(got)), collapse = \" \")), \"\\n\")\n")
                .Append("})\n");
        }
        string file = Path.Combine(work, "check.R");
        File.WriteAllText(file, runner.ToString(), new UTF8Encoding(false));
        var start = new ProcessStartInfo(Rscript(), $"--vanilla \"{file}\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.StandardOutputEncoding = Encoding.UTF8;
        using var process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd(), errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < runs.Count; i++)
        {
            string line = lines.FirstOrDefault(l => l.StartsWith($"PASS {i}", StringComparison.Ordinal) && l.Length == $"PASS {i}".Length
                || l.StartsWith($"FAIL {i}:", StringComparison.Ordinal));
            Check(line != null && line.StartsWith("PASS", StringComparison.Ordinal),
                $"{runs[i].Name}: in R, {line ?? "no result" + (errors.Length > 0 ? ": " + errors.Trim() : "")}");
        }
    }

    private static string R(string path) => "\"" + path.Replace('\\', '/').Replace("\"", "\\\"") + "\"";

    private static string Rscript()
    {
        if (Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT") is string configured && File.Exists(configured)) return configured;
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "R");
        return Directory.Exists(root)
            ? Directory.GetDirectories(root, "R-*").OrderByDescending(d => Version.TryParse(Path.GetFileName(d)[2..], out var v) ? v : new Version())
                .Select(d => Path.Combine(d, "bin", "Rscript.exe")).First(File.Exists)
            : throw new FileNotFoundException("Rscript was not found; set STATSDIRECT_RSCRIPT.");
    }
}

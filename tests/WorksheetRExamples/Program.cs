using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpreadsheetGear;
using StatsDirect.R;
using StatsDirect.UI;

// The worksheet R import against the examples shared with StatsDirect for Mac (statisticalhelp WorksheetR/examples.json, which
// encodes WorksheetR/RULES.md), and against Windows' own cases: the 1904 date system, null characters in sheet order, control
// characters in the sheet name and very long headings.  Each case's cells are built in a SpreadsheetGear workbook, the prompt's
// labels and suggestion are checked, the production import writes its CSV and script, and R checks that the object is identical()
// to the expected one.  Some cases also run as R+ starts them: in a new R, through StatsDirect's start-up file, with the user's own
// start-up file first.  Rule 3.8's number form is proved over a large random sample.
// Deterministic: no window.  Needs Rscript (STATSDIRECT_RSCRIPT, else the newest R under Program Files) and StatsDirect built with
// its SpreadsheetGear licence (an unlicensed SpreadsheetGear holds three workbooks of 1,000 rows); without it the test says SKIP,
// unless STATSDIRECT_REQUIRE_LICENSED is set, as on the licensed CI runs.
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

    private sealed record Run(string Name, string Folder, string Script, string Expected);

    // The cases also run as R+ starts them; the one whose user start-up file sets options(encoding = "latin1") is marked.
    private static readonly Dictionary<string, bool> RPlusCases = new()
    {
        ["Missing values in a text column"] = false,
        ["A list of vectors leaves out missing values separately"] = false,
        ["Text arrives exactly"] = true,
        ["Windows: control characters in the sheet name"] = false,
        ["Windows: very long headings with escapes"] = false,
    };

    private static int Main(string[] args)
    {
        try
        {
            if (!License())
            {
                bool required = Environment.GetEnvironmentVariable("STATSDIRECT_REQUIRE_LICENSED") is { Length: > 0 };
                Console.WriteLine((required ? "FAIL" : "SKIP") + " the worksheet R examples need StatsDirect built with its SpreadsheetGear licence");
                return required ? 1 : 0;
            }
            string examples = Environment.GetEnvironmentVariable("STATSDIRECT_WORKSHEET_R_EXAMPLES") ?? FindExamples();
            using var document = JsonDocument.Parse(File.ReadAllText(examples, Encoding.UTF8));
            Check(document.RootElement.GetProperty("version").GetInt32() == 1, "the shared examples are version 1, which this test reads");
            string work = Directory.CreateTempSubdirectory("StatsDirect-worksheet-r-examples-").FullName;
            var runs = new List<Run>();
            int index = 0;
            foreach (var example in document.RootElement.GetProperty("cases").EnumerateArray().Concat(WindowsCases()))
            {
                string name = example.GetProperty("name").GetString();
                string folder = Path.Combine(work, (index++).ToString(CultureInfo.InvariantCulture));
                try
                {
                    if (RunCase(name, example, folder) is (string script, string expected))
                        runs.Add(new Run(name, folder, script, expected));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Check(false, $"{name}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            int sharedCases = document.RootElement.GetProperty("cases").GetArrayLength();
            Check(sharedCases >= 50, $"the shared examples hold at least the 50 cases of the approved rules ({sharedCases})");
            RunR(runs, work);
            RunRPlus(runs.Where(run => RPlusCases.ContainsKey(run.Name)).ToList(), work);
            Check(runs.Count(run => RPlusCases.ContainsKey(run.Name)) == RPlusCases.Count,
                "every R+ case is among the cases compared in R: " + string.Join(", ", RPlusCases.Keys.Where(name => !runs.Any(run => run.Name == name))));
            CheckNumberForm(work);
            CheckManualCalculation(work);
            CheckFilteredBounds();
            if (failures.Count == 0)
            {
                try { Directory.Delete(work, true); } catch (IOException) { }
                Console.WriteLine($"PASS {checks} worksheet R example checks ({index} cases, {runs.Count} compared in R, {RPlusCases.Count} as R+ starts them) against {examples}");
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

    // The licence StatsDirect is built with, where it has one.
    private static bool License()
    {
        var attribute = typeof(frmMain).Assembly.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "SpreadsheetGearLicenseAttribute");
        if (attribute?.GetType().GetProperty("LicenseString")?.GetValue(attribute) is not string license || license.Length == 0) return false;
        Factory.SetSignedLicense(license);
        return true;
    }

    // ------------------------------------------------------------------------------------------------------- Windows' own cases

    // Cases in the shared schema, plus "sheetName" and "date1904" (whose serials are 1904-system serials), for what the shared
    // examples leave to each application: RULES.md 3.9 (each date system tested separately), 3.10 (a null character found in sheet
    // order, through the import), 4.4 (very long headings kept exactly) and 5.5 (the sheet name in the first comment).
    private static IEnumerable<JsonElement> WindowsCases()
    {
        static object Text(string s) => new { text = s };
        static object Date(string serial, string format) => new { date = serial, format };
        string longLatin = new string('x', 9_998) + "\u00e9", longChinese = new string('\u4e2d', 12_000), emoji = char.ConvertFromUtf32(0x1F600);
        var cases = new object[]
        {
            new
            {
                name = "Windows: the 1904 date system",
                date1904 = true,
                cells = new Dictionary<string, object>
                {
                    ["A1"] = Text("When"), ["A2"] = Date("0", "yyyy-mm-dd"), ["A3"] = Date("60", "yyyy-mm-dd"), ["A4"] = Date("44885", "yyyy-mm-dd"),
                    ["A5"] = Date("2957003", "yyyy-mm-dd"),
                    ["B1"] = Text("Beyond"), ["B2"] = Date("2957004", "yyyy-mm-dd"),
                    ["C1"] = Text("At"), ["C2"] = new { datetime = "44885.5", format = "yyyy-mm-dd hh:mm" },
                },
                select = new { entire = true },
                options = new { format = "data frame", headings = (bool?)null, friendly = false },
                expect = new
                {
                    headingsSuggested = true,
                    r = "structure(list(When = as.Date(c(\"1904-01-01\", \"1904-03-01\", \"2026-11-21\", \"9999-12-31\")), "
                        + "Beyond = c(2957004, NA, NA, NA), At = c(\"2026-11-21 12:00:00\", NA, NA, NA)), "
                        + "row.names = c(NA_integer_, -4L), class = \"data.frame\")",
                },
            },
            new
            {
                name = "Windows: null characters are found in sheet order, through the import",
                cells = new Dictionary<string, object> { ["A1"] = Text("a"), ["A2"] = Text("x\0"), ["B1"] = Text("y\0z") },
                select = new { entire = true },
                options = new { format = "data frame", headings = false, friendly = false },
                expect = new
                {
                    refusedNaming = "B1",
                    refusedMessage = "Cell B1 contains a null character, which R cannot hold. Remove it, then open the R import again.",
                    r = (string)null,
                },
            },
            new
            {
                name = "Windows: null characters below the headings are found in sheet order",
                cells = new Dictionary<string, object> { ["A1"] = Text("a"), ["B1"] = Text("b"), ["A3"] = Text("x\0"), ["B2"] = Text("y\0") },
                select = new { entire = true },
                options = new { format = "data frame", headings = true, friendly = false },
                expect = new { refusedNaming = "B2", r = (string)null },
            },
            new
            {
                name = "Windows: a null character given as UTF-16 units",
                cells = new Dictionary<string, object> { ["A1"] = new { textUnits = new[] { 97, 0, 98 } }, ["A2"] = Text("c") },
                select = new { entire = true },
                options = new { format = "data frame", headings = (bool?)null, friendly = false },
                expect = new { refusedNaming = "A1", r = (string)null },
            },
            new
            {
                name = "Windows: control characters in the sheet name",
                sheetName = "Ctrl\u001AZ\r\nTwo",
                cells = new Dictionary<string, object> { ["A1"] = Text("x"), ["A2"] = new { number = "1" } },
                select = new { entire = true },
                options = new { format = "data frame", headings = (bool?)null, friendly = false },
                expect = new
                {
                    headingsSuggested = true,
                    comments = new[] { "# Data copied from the worksheet \u201CCtrl Z  Two\u201D, A1:A2." },
                    r = "structure(list(x = 1), row.names = c(NA_integer_, -1L), class = \"data.frame\")",
                },
            },
            new
            {
                name = "Windows: every column, selected in two pieces, is select all",
                cells = new Dictionary<string, object> { ["A1"] = Text("x"), ["A2"] = new { number = "1" }, ["B1"] = Text("y"), ["B2"] = new { number = "2" } },
                select = new { areas = new[] { "A:M", "N:XFD" } },
                options = new { format = "data frame", headings = true, friendly = false },
                expect = new
                {
                    selected = "Selected cells \u2014 A1:B2 (2 rows \u00d7 2 columns)",
                    r = "structure(list(x = 1, y = 2), row.names = c(NA_integer_, -1L), class = \"data.frame\")",
                },
            },
            new
            {
                name = "Windows: every row, selected in two pieces, is select all",
                cells = new Dictionary<string, object> { ["A1"] = Text("x"), ["A2"] = new { number = "1" }, ["B1"] = Text("y"), ["B2"] = new { number = "2" } },
                select = new { areas = new[] { "1:500000", "500001:1048576" } },
                options = new { format = "data frame", headings = true, friendly = false },
                expect = new
                {
                    selected = "Selected cells \u2014 A1:B2 (2 rows \u00d7 2 columns)",
                    r = "structure(list(x = 1, y = 2), row.names = c(NA_integer_, -1L), class = \"data.frame\")",
                },
            },
            new
            {
                name = "Windows: very long headings with escapes",
                cells = new Dictionary<string, object>
                {
                    ["A1"] = Text(longLatin), ["A2"] = new { number = "1" }, ["A3"] = new { number = "2" },
                    ["B1"] = Text(longChinese), ["B2"] = new { number = "3" }, ["B3"] = new { number = "4" },
                },
                select = new { entire = true },
                options = new { format = "data frame", headings = (bool?)null, friendly = false },
                expect = new
                {
                    headingsSuggested = true,
                    r = "structure(list(c(1, 2), c(3, 4)), names = c(paste0(strrep(\"x\", 9998), \"\\u00e9\"), strrep(\"\\u4e2d\", 12000)), "
                        + "row.names = c(NA_integer_, -2L), class = \"data.frame\")",
                },
            },
            new
            {
                name = "Windows: long headings split between the pieces of a surrogate pair",
                cells = new Dictionary<string, object>
                {
                    ["A1"] = Text(new string('x', 1_999) + emoji), ["A2"] = new { number = "1" },
                    ["B1"] = Text(new string('x', 3_999) + emoji), ["B2"] = new { number = "2" },
                },
                select = new { entire = true },
                options = new { format = "data frame", headings = (bool?)null, friendly = false },
                expect = new
                {
                    headingsSuggested = true,
                    r = "structure(list(1, 2), names = c(paste0(strrep(\"x\", 1999), \"\\U0001f600\"), paste0(strrep(\"x\", 3999), \"\\U0001f600\")), "
                        + "row.names = c(NA_integer_, -1L), class = \"data.frame\")",
                },
            },
        };
        foreach (var example in cases)
            yield return JsonSerializer.SerializeToElement(example);
    }

    // ------------------------------------------------------------------------------------------------------------- One case

    private static readonly string[] SelectKinds = { "entire", "range", "columns", "rows", "areas", "all", "none" };
    private static readonly string[] ExpectKeys = { "entire", "selected", "entireEnabled", "selectedEnabled", "headingsEnabled", "headingsSuggested",
        "comments", "r", "refusedNaming", "refusedMessage", "labelsOnly" };

    private static (string, string)? RunCase(string name, JsonElement example, string folder)
    {
        if (example.TryGetProperty("skip", out var skip) && skip.TryGetProperty("windows", out var reason))
        {
            Console.WriteLine($"SKIP {name}: {reason.GetString()}");
            return null;
        }
        var expect = example.GetProperty("expect");
        foreach (var key in expect.EnumerateObject())
            Check(ExpectKeys.Contains(key.Name), $"{name}: the expectation \"{key.Name}\" is one this test checks");
        var select = example.GetProperty("select");
        Check(select.EnumerateObject().Count() == 1 && SelectKinds.Contains(select.EnumerateObject().First().Name), $"{name}: a selection this test can make");

        // Text with a null character is stored through a file, as a workbook can bring it: SpreadsheetGear ends text set as a value there.
        bool date1904 = example.TryGetProperty("date1904", out var system) && system.GetBoolean();
        var nulTexts = new Dictionary<string, string>();
        var workbook = Factory.GetWorkbook(CultureInfo.InvariantCulture);
        try
        {
            var sheet = workbook.Worksheets[0];
            sheet.Name = example.TryGetProperty("sheetName", out var sheetName) ? sheetName.GetString() : "Sheet1";
            foreach (var cell in example.GetProperty("cells").EnumerateObject())
                if (!SetCell(sheet.Range[cell.Name], cell.Value, nulTexts, cell.Name, out string problem))
                    Check(false, $"{name}: cell {cell.Name}: {problem}");
            if (example.TryGetProperty("formatOnly", out var formatOnly))
                foreach (var reference in formatOnly.EnumerateArray()) sheet.Range[reference.GetString()].NumberFormat = "0.00";
            if (example.TryGetProperty("merged", out var merged))
                foreach (var area in merged.EnumerateArray()) sheet.Range[area.GetString()].Merge();
            if (date1904 || nulTexts.Count > 0)
            {
                workbook = Reopen(workbook, date1904, nulTexts.Count > 0);
                sheet = workbook.Worksheets[0];
                Check(workbook.Date1904 == date1904, $"{name}: the workbook uses the {(date1904 ? 1904 : 1900)} date system");
                foreach (var (reference, text) in nulTexts)
                    Check(sheet.Range[reference].Value as string == text, $"{name}: cell {reference} holds its null character");
            }
            if (sheet.Name != (example.TryGetProperty("sheetName", out var given) ? given.GetString() : "Sheet1"))
                Check(false, $"{name}: SpreadsheetGear does not hold the sheet name as given");

            IRange selection = select.TryGetProperty("range", out var range) ? sheet.Range[range.GetString()]
                : select.TryGetProperty("columns", out var columns) ? sheet.Range[string.Join(",", columns.EnumerateArray().Select(c => c.GetString() + ":" + c.GetString()))]
                : select.TryGetProperty("rows", out var rows) ? sheet.Range[string.Join(",", rows.EnumerateArray().Select(r => r.GetString()))]
                : select.TryGetProperty("areas", out var areas) ? sheet.Range[string.Join(",", areas.EnumerateArray().Select(a => a.GetString()))]
                : select.TryGetProperty("all", out _) ? sheet.Cells
                : sheet.Range["A1"];   // "entire" and "none": the active cell, which a Windows worksheet always has
            bool entireChoice = select.TryGetProperty("entire", out _);

            var options = WorksheetRImport.Describe(sheet, selection, write => write());
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
            string formatName = choice.GetProperty("format").GetString();
            Check(formatName is "data frame" or "list", $"{name}: the format \"{formatName}\" is one this test knows");
            var format = formatName == "list" ? WorksheetRDataFormat.VectorList : WorksheetRDataFormat.DataFrame;
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
        finally
        {
            workbook.Close();
        }
    }

    private static string PlatformText(JsonElement label) =>
        label.ValueKind == JsonValueKind.Object ? label.GetProperty("windows").GetString() : label.GetString();

    // Stands in for a null character until the workbook is saved, then becomes the file's own _x0000_ escape.
    private const char NulStandIn = '\uE000';

    // A stored cell from the example's schema.
    private static bool SetCell(IRange cell, JsonElement spec, Dictionary<string, string> nulTexts, string reference, out string problem)
    {
        problem = null;
        if (spec.TryGetProperty("number", out var number)) cell.Value = double.Parse(number.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
        else if (spec.TryGetProperty("text", out var text) || spec.TryGetProperty("textUnits", out text))
        {
            // Text is stored as text: SpreadsheetGear reads "0012", "true" and "#DIV/0!" given as a Value as a number, a Boolean
            // and an error, as Excel does with typed entry, unless the cell is formatted as text.
            string value = text.ValueKind == JsonValueKind.Array ? new string(text.EnumerateArray().Select(u => (char)u.GetInt32()).ToArray()) : text.GetString();
            cell.NumberFormat = "@";
            if (value.Contains('\0'))
            {
                if (value.Contains(NulStandIn)) { problem = "the text holds the null character's stand-in"; return false; }
                nulTexts[reference] = value;
                value = value.Replace('\0', NulStandIn);
            }
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

    // The workbook saved as an .xlsx file and opened again, with the 1904 date system set in the file, and the null character's
    // stand-in as the file's own escape: what a workbook made elsewhere can bring.
    private static IWorkbook Reopen(IWorkbook workbook, bool date1904, bool nulCharacters)
    {
        using var saved = new MemoryStream();
        workbook.SaveToStream(saved, FileFormat.OpenXMLWorkbook);
        workbook.Close();
        saved.Position = 0;
        var patched = new MemoryStream();
        using (var source = new ZipArchive(saved, ZipArchiveMode.Read, true))
        using (var target = new ZipArchive(patched, ZipArchiveMode.Create, true))
            foreach (var entry in source.Entries)
            {
                if (!entry.FullName.EndsWith(".xml", StringComparison.Ordinal))
                {
                    using var part = entry.Open();
                    using var copy = target.CreateEntry(entry.FullName).Open();
                    part.CopyTo(copy);
                    continue;
                }
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                string xml = reader.ReadToEnd();
                if (date1904 && entry.FullName == "xl/workbook.xml")
                    xml = xml.Contains("<workbookPr", StringComparison.Ordinal)
                        ? Regex.Replace(xml, "<workbookPr(?![^>]*date1904)", "<workbookPr date1904=\"1\"")
                        : Regex.Replace(xml, "(<workbook\\b[^>]*>(?:\\s*<fileVersion\\b[^>]*/>)?)", "$1<workbookPr date1904=\"1\"/>");
                if (nulCharacters && entry.FullName.EndsWith(".xml", StringComparison.Ordinal))
                    xml = xml.Replace(NulStandIn.ToString(), "_x0000_", StringComparison.Ordinal);
                using var writer = new StreamWriter(target.CreateEntry(entry.FullName).Open(), new UTF8Encoding(false));
                writer.Write(xml);
            }
        patched.Position = 0;
        return Factory.GetWorkbookSet(CultureInfo.InvariantCulture).Workbooks.OpenFromStream(patched);
    }

    // ------------------------------------------------------------------------------------------------------------------- R

    // One R process checks every case: each script runs in a clean global environment, and its worksheet_data must be identical()
    // to the expected expression, evaluated with base R only.
    private static void RunR(List<Run> runs, string work)
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
        var (exit, output, errors) = RunRscript($"--vanilla \"{file}\"", work, null);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < runs.Count; i++)
        {
            string line = lines.FirstOrDefault(l => l.StartsWith($"PASS {i}", StringComparison.Ordinal) && l.Length == $"PASS {i}".Length
                || l.StartsWith($"FAIL {i}:", StringComparison.Ordinal));
            Check(line != null && line.StartsWith("PASS", StringComparison.Ordinal),
                $"{runs[i].Name}: in R, {line ?? "no result" + (errors.Length > 0 ? ": " + errors.Trim() : "")}");
        }
        Check(exit == 0, $"the R check of every case ran to its end (exit {exit})");
    }

    // R+ as it starts R: a new R reads StatsDirect's start-up file through R_PROFILE_USER, before R has attached its default
    // packages.  The user's own start-up file runs first (here ./.Rprofile, which for one case sets options(encoding = "latin1")),
    // R processes started from the session must not inherit StatsDirect's file, and the import must arrive whole, with its summary.
    private static void RunRPlus(List<Run> runs, string work)
    {
        // StatsDirect's start-up file gives R the user's own R_PROFILE_USER back: here there is none, so ./.Rprofile is the user's.
        Environment.SetEnvironmentVariable("R_PROFILE_USER", null);
        foreach (var (run, i) in runs.Select((run, i) => (run, i)))
        {
            string home = Path.Combine(work, $"rplus-{i}-home"), current = Path.Combine(work, $"rplus-{i}-folder");
            Directory.CreateDirectory(home);
            Directory.CreateDirectory(current);
            bool latin1 = RPlusCases[run.Name];
            File.WriteAllText(Path.Combine(current, ".Rprofile"),
                "options(statsdirect.own.profile = TRUE" + (latin1 ? ", encoding = \"latin1\"" : "") + ")\n");
            string profile = WorksheetRLauncher.WriteStartupFiles(run.Script, run.Folder);
            string expected = Path.Combine(work, $"rplus-{i}-expected.R"), check = Path.Combine(work, $"rplus-{i}-check.R");
            File.WriteAllText(expected, run.Expected + "\n", new UTF8Encoding(false));
            File.WriteAllText(check,
                $"want <- eval(parse(file = {R(expected)}, encoding = \"UTF-8\")[[1]], baseenv())\n"
                + "ok <- exists(\"worksheet_data\", envir = globalenv(), inherits = FALSE) && identical(get(\"worksheet_data\", envir = globalenv()), want)\n"
                + "cat(\"RPLUS\", ok, nzchar(Sys.getenv(\"R_PROFILE_USER\")), isTRUE(getOption(\"statsdirect.own.profile\")), \"\\n\")\n",
                new UTF8Encoding(false));
            var environment = new Dictionary<string, string>
            {
                ["R_PROFILE_USER"] = profile, ["R_USER"] = home, ["HOME"] = home,
                ["R_ENVIRON_USER"] = Path.Combine(home, "no-Renviron"),
            };
            var (exit, output, errors) = RunRscript($"\"{check}\"", current, environment);
            bool summary = output.Contains("'data.frame':", StringComparison.Ordinal) || output.Contains("List of ", StringComparison.Ordinal);
            Check(exit == 0 && output.Contains("RPLUS TRUE FALSE TRUE", StringComparison.Ordinal) && summary
                    && !(output + errors).Contains("Error", StringComparison.Ordinal) && !(output + errors).Contains("Warning", StringComparison.Ordinal),
                $"{run.Name}: as R+ starts R, with the user's own start-up file{(latin1 ? " setting options(encoding = \"latin1\")" : "")}, "
                + $"the import arrives whole with its summary and R_PROFILE_USER is not passed on (exit {exit}): {output.Trim()} {errors.Trim()}");
        }
    }

    // Rule 2.1 under an AutoFilter, which makes Find pass over hidden rows: the bounds still take in a hidden row's data, and a
    // formatted cell at the far corner of the grid (which widens the used range to every cell) does not make the prompt read every cell.
    private static void CheckFilteredBounds()
    {
        var workbook = Factory.GetWorkbook(CultureInfo.InvariantCulture);
        try
        {
            var sheet = workbook.Worksheets[0];
            sheet.Name = "Sheet1";
            sheet.Range["A1"].Value = "h";
            sheet.Range["A2"].Value = 1.0;
            sheet.Range["A3"].Value = 2.0;
            sheet.Range["A1:A3"].AutoFilter(0, "<>2", AutoFilterOperator.And, null, true);
            sheet.Range["XFD1048576"].NumberFormat = "0.00";
            var describe = Task.Run(() => WorksheetRImport.Describe(sheet, sheet.Range["A1:C5"], write => write()));
            bool finished = describe.Wait(TimeSpan.FromSeconds(20));
            var options = finished ? describe.Result : null;
            Check(finished && sheet.Range["A3"].EntireRow.Hidden && options!.EntireSheet?.Address == "A1:A3" && options.SelectedCells?.Address == "A1:C5",
                $"under an AutoFilter with a formatted far corner, the prompt finds the data, a hidden row's included, within 20 s"
                + (finished ? $" (entire {options!.EntireSheet?.Address}, selected {options.SelectedCells?.Address})" : " (it did not finish)"));
        }
        finally
        {
            workbook.Close();
        }
    }

    // RULES.md 3.11 and section 7: Windows sends formulas' current values.  In a workbook that calculates manually, a formula's
    // stored result can be older than its cells; the import calculates first, so B1 = A1 + 1 arrives as 9 after A1 becomes 8.
    private static void CheckManualCalculation(string work)
    {
        var workbook = Factory.GetWorkbook(CultureInfo.InvariantCulture);
        try
        {
            var sheet = workbook.Worksheets[0];
            sheet.Name = "Sheet1";
            workbook.WorkbookSet.Calculation = Calculation.Manual;
            sheet.Range["A1"].Value = 1.0;
            sheet.Range["B1"].Formula = "=A1+1";
            workbook.WorkbookSet.Calculate();
            sheet.Range["A1"].Value = 8.0;
            bool stale = sheet.Range["B1"].Value is double before && before == 2;
            var options = WorksheetRImport.Describe(sheet, sheet.Range["A1:B1"], write => write());
            string folder = Path.Combine(work, "manual-calculation");
            options.SelectedCells!.WriteImportScript(false, WorksheetRDataFormat.DataFrame, false, folder);
            string csv = File.ReadAllText(Path.Combine(folder, WorksheetRData.CsvFileName)).Trim();
            Check(stale && csv == "8,9" && workbook.WorkbookSet.Calculation == Calculation.Manual,
                $"a workbook that calculates manually is calculated before its formulas are sent (stale before: {stale}; sent: {csv})");
        }
        finally
        {
            workbook.Close();
        }
    }

    // RULES.md 3.8: each application's own test proves the number form over the example values and a large random sample.
    // RNumber must give the shortest decimal when it has at most 15 digits, no exponent and at most four digits after the point,
    // and R's hexadecimal form otherwise; R must read every one back exactly, through read.csv and as R code.
    private static void CheckNumberForm(string work)
    {
        var random = new Random(20261121);
        var values = new List<double>();
        void Add(double v) { if (double.IsFinite(v)) values.Add(v); }
        for (int i = 0; i < 40_000; i++) Add(BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue)));
        for (int i = 0; i < 4_000; i++) Add(BitConverter.Int64BitsToDouble(random.NextInt64(1, 0xF_FFFF_FFFF_FFFF) | (random.Next(2) == 0 ? 0 : long.MinValue)));   // subnormal
        for (int i = 0; i < 40_000; i++)
        {
            int digits = random.Next(1, 18), decimals = random.Next(0, 9);
            var text = new StringBuilder(random.Next(2) == 0 ? "" : "-").Append((char)('1' + random.Next(9)));
            for (int d = 1; d < digits; d++) text.Append((char)('0' + random.Next(10)));
            if (decimals > 0) text.Insert(Math.Max(text.Length - decimals, text[0] == '-' ? 1 : 0), text.Length - decimals <= (text[0] == '-' ? 1 : 0) ? "0." : ".");
            Add(double.Parse(text.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture));
        }
        foreach (double centre in new[] { 1e14, 1e15, 1e16, Math.Pow(2, 52), Math.Pow(2, 53) })
            for (int k = -50; k <= 50; k++) { Add(centre + k); Add(-(centre + k)); }
        for (int i = 0; i < 4_000; i++)
            Add(Math.Round(random.NextDouble() * Math.Pow(10, random.Next(8, 13)), random.Next(4, 6)));
        for (int e = -1074; e <= 1023; e++) Add(Math.ScaleB(1, e));
        foreach (double v in values.Take(4_000).ToArray()) { Add(Math.BitIncrement(v)); Add(Math.BitDecrement(v)); }

        int wrongForm = 0;
        string firstWrong = null;
        var texts = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            texts[i] = WorksheetRImport.RNumber(values[i]);
            if (texts[i] != ExpectedForm(values[i]) && wrongForm++ == 0) firstWrong = $"{values[i]:R} gives {texts[i]}, not {ExpectedForm(values[i])}";
        }
        Check(wrongForm == 0, $"rule 3.8's form for {values.Count:N0} sampled numbers: {wrongForm} differ{(firstWrong != null ? ", first " + firstWrong : "")}");
        Console.WriteLine($"Number form: {values.Count:N0} sampled numbers, {texts.Count(t => t.Contains("0x", StringComparison.Ordinal)):N0} of them in R's hexadecimal form");

        string csv = Path.Combine(work, "numbers.csv"), bits = Path.Combine(work, "numbers.bin"), check = Path.Combine(work, "numbers.R");
        File.WriteAllLines(csv, texts, new UTF8Encoding(false));
        using (var writer = new BinaryWriter(File.Create(bits)))
            foreach (double v in values) writer.Write(v);
        File.WriteAllText(check,
            $"x <- readBin({R(bits)}, \"double\", n = {values.Count}, size = 8, endian = \"little\")\n"
            + $"y <- utils::read.csv({R(csv)}, header = FALSE, colClasses = \"numeric\")[[1]]\n"
            + $"t <- readLines({R(csv)})\n"
            + "z <- unlist(lapply(split(t, ceiling(seq_along(t) / 1000)), function(p) eval(parse(text = paste0(\"c(\", paste(p, collapse = \",\"), \")\")))), use.names = FALSE)\n"
            + "cat(\"NUMBERS\", length(x), length(y), length(z), sum(x != y), sum(x != z), \"\\n\")\n",
            new UTF8Encoding(false));
        var (exit, output, errors) = RunRscript($"--vanilla \"{check}\"", work, null);
        string n = values.Count.ToString(CultureInfo.InvariantCulture);
        Check(exit == 0 && output.Contains($"NUMBERS {n} {n} {n} 0 0", StringComparison.Ordinal),
            $"R reads every one of the {values.Count:N0} sampled numbers back exactly, through read.csv and as R code: {output.Trim()} {errors.Trim()}");
    }

    // The form rule 3.8 asks for, worked out from .NET's shortest round-trip text, independently of WorksheetRImport.
    private static string ExpectedForm(double v)
    {
        if (double.IsPositiveInfinity(v)) return "Inf";
        if (double.IsNegativeInfinity(v)) return "-Inf";
        if (v == 0) return "0";
        string r = v.ToString("R", CultureInfo.InvariantCulture);
        bool negative = r.StartsWith('-');
        if (negative) r = r[1..];
        int exponent = 0, e = r.IndexOfAny(new[] { 'E', 'e' });
        if (e >= 0) { exponent = int.Parse(r[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture); r = r[..e]; }
        int dot = r.IndexOf('.');
        string whole = dot < 0 ? r : r[..dot], fraction = dot < 0 ? "" : r[(dot + 1)..];
        // value = 0.digits x 10^point
        string digits = whole + fraction;
        int point = whole.Length + exponent;
        int leading = digits.Length - digits.TrimStart('0').Length;
        digits = digits.TrimStart('0').TrimEnd('0');
        point -= leading;
        int count = digits.Length, after = Math.Max(0, count - point);
        // digits counted: the significant digits, with an integer's trailing zeros (rule 3.8)
        int counted = Math.Max(count, point);
        if (counted <= 15 && after <= 4)
        {
            string plain = point >= count ? digits + new string('0', point - count)
                : point > 0 ? digits[..point] + "." + digits[point..]
                : "0." + new string('0', -point) + digits;
            return (negative ? "-" : "") + plain;
        }
        long bits = BitConverter.DoubleToInt64Bits(v);
        int biased = (int)((bits >> 52) & 0x7FF);
        string mantissa = (bits & 0xF_FFFF_FFFF_FFFF).ToString("x13", CultureInfo.InvariantCulture);
        string hex = biased == 0 ? $"0x0.{mantissa}p-1022" : $"0x1.{mantissa}p{(biased - 1023 >= 0 ? "+" : "")}{biased - 1023}";
        return (negative ? "-" : "") + hex;
    }

    // Rscript, with its standard output and error read together (a full error pipe cannot stall it), and a time limit.
    private static (int Exit, string Output, string Errors) RunRscript(string arguments, string folder, Dictionary<string, string> environment)
    {
        var start = new ProcessStartInfo(Rscript(), arguments)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = folder, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        if (environment != null)
            foreach (var (key, value) in environment) start.Environment[key] = value;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(600_000))
        {
            process.Kill(true);
            return (-1, output.IsCompleted ? output.Result : "", "R did not finish within ten minutes");
        }
        return (process.ExitCode, output.Result, errors.Result);
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

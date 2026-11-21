using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpreadsheetGear;
using StatsDirect.R;
using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckWorksheetRSessions(frmMain main)
    {
        CheckWorksheetRPromptLayout(main);
        var installed = RController.CheckR();
        string rscript = installed.Select(version => Path.Combine(version.BinPath, "Rscript.exe")).FirstOrDefault(File.Exists);
        bool hasRGui = installed.Any(version => File.Exists(version.GuiPath));
        var originalDocument = main.ActiveMdiChild;
        using var grid = (Form)Call(main, "CreateGrid");
        var workbook = (SpreadsheetGear.Windows.Forms.WorkbookView)Field(grid, "workbookView");
        await Task.Delay(150);

        void Select(string address)
        {
            workbook.GetLock();
            try { workbook.RangeSelection = workbook.ActiveWorksheet.Cells[address]; }
            finally { workbook.ReleaseLock(); }
        }
        string Selection()
        {
            workbook.GetLock();
            try { return workbook.RangeSelection.Address.Replace("$", ""); }
            finally { workbook.ReleaseLock(); }
        }
        workbook.GetLock();
        try
        {
            var sheet = workbook.ActiveWorksheet;
            sheet.Cells["A1"].Value = "Before visit";
            sheet.Cells["B1"].Value = "Group / label";
            sheet.Cells["C1"].Value = "Outside selection";
            sheet.Cells["A2"].Value = 15;
            sheet.Cells["B2"].Value = "café, \"quoted\"%";
            sheet.Cells["B3"].Value = "%NA";
            sheet.Cells["C2"].Value = 30;
            sheet.Cells["C4"].Value = 999;
            sheet.Cells["H20"].NumberFormat = "0.000";
            Check(sheet.UsedRange.Address.Replace("$", "") == "A1:H20", "formatting alone extends the fixture worksheet's used range");
        }
        finally { workbook.ReleaseLock(); }
        Select("A1:B3");

        var toolbar = (ToolStrip)Field(main, "toolStrip");
        var embedded = (ToolStripButton)toolbar.Items["rToolStripButton"]!;
        var external = (ToolStripButton)toolbar.Items["rPlusToolStripButton"]!;
        Check(toolbar.Visible && embedded.Available && embedded.Enabled && external.Available && external.Enabled
            && embedded.AccessibleName == "Open in StatsDirect R" && external.AccessibleName == "Open in external R",
            "the worksheet toolbar has distinct and accessible R and R+ buttons");

        async Task<Form> Activate(ToolStripButton button, string title, string selectedAddress, string choice,
            bool vectorList = false, bool emptySelection = false, bool friendlyNames = false)
        {
            string selected = Selection();
            var before = main.MdiChildren.ToHashSet();
            var pending = DriveWorksheetRPrompt(main, title, selectedAddress, choice, vectorList, emptySelection, friendlyNames);
            button.PerformClick();
            await pending;
            var added = main.MdiChildren.Where(child => !before.Contains(child)).ToArray();
            if (choice == null)
            {
                Check(added.Length == 0 && main.ActiveMdiChild == grid && Selection() == selected,
                    "cancelling " + title + " opens no session and keeps the selection");
                return null;
            }
            Check(added.Length == 1 && added[0].GetType().Name == "frmScript" && main.ActiveMdiChild == added[0],
                title + " creates and activates exactly one native R document");
            return added[0];
        }

        await Activate(embedded, "Open in StatsDirect R", "A1:B3", null);
        await Activate(external, "Open in external R", "A1:B3", null);
        Select("A1,B2");
        await Activate(embedded, "Open in StatsDirect R", null, null);
        Select("H:H");
        await Activate(embedded, "Open in StatsDirect R", null, null, emptySelection: true);
        CheckWorksheetRBounds(grid, workbook);
        Select("A:B");
        await Activate(embedded, "Open in StatsDirect R", "A1:B3", null);
        if (rscript == null)
        {
            Console.WriteLine("SKIP worksheet R execution checks because no R installation is available");
            originalDocument?.Activate();
            return;
        }

        const string verifyData = """
            stopifnot(is.data.frame(worksheet_data),
                      identical(names(worksheet_data), c("Before visit", "Group / label")),
                      identical(worksheet_data[[1]], c(15, NA_real_)),
                      identical(worksheet_data[[2]], c('café, "quoted"%', "%NA")))
            """;
        const string verifyVectors = """
            stopifnot(is.list(worksheet_data), !is.data.frame(worksheet_data),
                      identical(names(worksheet_data), c("Before visit", "Group / label", "Outside selection")),
                      identical(worksheet_data[[1]], 15),
                      identical(worksheet_data[[2]], c('café, "quoted"%', "%NA")),
                      identical(worksheet_data[[3]], c(30, 999)))
            """;
        using var imported = await Activate(embedded, "Open in StatsDirect R", "A1:B3", "Selected");
        var importedConsole = RConsole(imported);
        await ((Task)Get(importedConsole, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(imported, 0);
        Check(((string)Get(importedConsole, "Script")).Contains("worksheet_data", StringComparison.Ordinal) && !toolbar.Visible,
            "R copies the data into the active console and retains its import script");
        await VerifyWorksheetRValues(imported, verifyData,
            "selecting whole columns A:B imports only A1:B3, preserving the shorter column's missing value and literal text");
        await CheckSavedWorksheetR(imported, rscript, verifyData);

        grid.Activate();
        await Task.Delay(100);
        Select("B2");
        using var whole = await Activate(embedded, "Open in StatsDirect R", "B2", "All");
        await ((Task)Get(RConsole(whole), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(whole, 0);
        await VerifyWorksheetRValues(whole, """
            stopifnot(is.data.frame(worksheet_data),
                      identical(names(worksheet_data), c("Before visit", "Group / label", "Outside selection")),
                      identical(worksheet_data[[1]], c(15, NA_real_, NA_real_)),
                      identical(worksheet_data[[2]], c('café, "quoted"%', "%NA", NA_character_)),
                      identical(worksheet_data[[3]], c(30, NA_real_, 999)))
            """, "Whole active worksheet imports the used range including cells outside the selection");

        grid.Activate();
        await Task.Delay(100);
        Select("B2");
        using var vectors = await Activate(embedded, "Open in StatsDirect R", "B2", "All", vectorList: true);
        await ((Task)Get(RConsole(vectors), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(vectors, 0);
        await VerifyWorksheetRValues(vectors, verifyVectors,
            "List of vectors keeps the named column order and types, drops internal and trailing blanks per column, and preserves literal %NA text");
        await CheckSavedWorksheetR(vectors, rscript, verifyVectors);

        grid.Activate();
        await Task.Delay(100);
        Select("B2");
        using var oneCell = await Activate(embedded, "Open in StatsDirect R", "B2", "Selected");
        await ((Task)Get(RConsole(oneCell), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(oneCell, 0);
        await VerifyWorksheetRValues(oneCell, """
            stopifnot(is.data.frame(worksheet_data),
                      identical(names(worksheet_data), "B"),
                      identical(worksheet_data[[1]], 'café, "quoted"%'))
            """, "Selected cells uses the exact single cell as one value, named by its worksheet column (RULES.md C5)");

        grid.Activate();
        await Task.Delay(100);
        Select("B2");
        using var empty = await Activate(embedded, "Open in StatsDirect R", "B2", "None");
        var emptyConsole = RConsole(empty);
        await ((Task)Get(emptyConsole, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        for (int attempt = 0; attempt < 240 && (string)Get(emptyConsole, "NativeState") != "ready"; attempt++)
            await Task.Delay(50);
        Check((string)Get(emptyConsole, "NativeState") == "ready" && RFinished(empty) == 0 && (string)Get(emptyConsole, "Script") == "",
            "Open without data starts a blank R prompt without running a worksheet import");
        await VerifyWorksheetRValues(empty, """stopifnot(!exists("worksheet_data", .GlobalEnv, inherits = FALSE))""",
            "the blank R session has no worksheet_data from the existing R session");

        grid.Activate();
        await Task.Delay(100);
        Select("A1:B3");
        if (hasRGui)
        {
            await CheckWorksheetRGuiImport(grid, "SelectedCells", "DataFrame", verifyData);
            await CheckWorksheetRGuiImport(grid, "EntireSheet", "VectorList", verifyVectors);
        }
        else Console.WriteLine("SKIP external worksheet R launch check because RGui is unavailable");

        workbook.GetLock();
        try
        {
            var sheet = workbook.ActiveWorksheet;
            sheet.Cells["A1"].Value = " PEFR   Before ";
            sheet.Cells["B1"].Value = "PEFR_Before";
            sheet.Cells["C1"].Value = "PEFR_Before_1";
        }
        finally { workbook.ReleaseLock(); }
        const string verifyFriendlyNames = """
            stopifnot(identical(names(worksheet_data), c("PEFR_Before", "PEFR_Before_2", "PEFR_Before_1")),
                      identical(worksheet_data$PEFR_Before[[1]], 15),
                      identical(worksheet_data$PEFR_Before_2[[1]], 'café, "quoted"%'),
                      identical(worksheet_data$PEFR_Before_1[[1]], 30))
            """;
        const string verifyFriendlyVectors = "stopifnot(is.list(worksheet_data), !is.data.frame(worksheet_data))\n" + verifyFriendlyNames;
        foreach (bool vectorList in new[] { false, true })
        {
            using var friendly = await Activate(embedded, "Open in StatsDirect R", "A1:B3", "All", vectorList: vectorList, friendlyNames: true);
            await ((Task)Get(RConsole(friendly), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
            await CompleteRCommand(friendly, 0);
            await VerifyWorksheetRValues(friendly,
                vectorList ? verifyFriendlyVectors : "stopifnot(is.data.frame(worksheet_data))\n" + verifyFriendlyNames,
                "R-friendly names preserve the existing suffix and give unique names to the " + (vectorList ? "vectors" : "data-frame columns"));
            await Task.Yield();
            grid.Activate();
            await Task.Delay(100);
        }
        if (hasRGui)
        {
            await CheckWorksheetRGuiImport(grid, "EntireSheet", "VectorList", verifyFriendlyVectors, friendlyNames: true);
            await CheckWorksheetRGuiImport(grid, "EntireSheet", "DataFrame", "stopifnot(is.data.frame(worksheet_data))", ownProfile: true);
        }
        originalDocument?.Activate();
    }

    static void CheckWorksheetRBounds(Form grid, SpreadsheetGear.Windows.Forms.WorkbookView workbook)
    {
        void Update(Action action)
        {
            workbook.GetLock();
            try { action(); }
            finally { workbook.ReleaseLock(); }
        }
        IWorksheet original = null;
        IWorksheet sheet = null;
        try
        {
            Update(() =>
            {
                original = workbook.ActiveWorksheet;
                sheet = workbook.ActiveWorkbook.Worksheets.Add();
                workbook.ActiveWorksheet = sheet;
                sheet.Cells["A1"].NumberFormat = "0.0";
                sheet.Cells["H20"].NumberFormat = "0.00";
            });
            object Options() => Call(grid, "DescribeWorksheetRData");
            string Address(object options, string property) => ((string)Get(Get(options, property), "Address")).Replace("$", "");
            var formattedOnly = Options();
            Check(Get(formattedOnly, "EntireSheet") == null && Get(formattedOnly, "SelectedCells") == null,
                "a worksheet with formatting only offers no R data import");

            Update(() =>
            {
                sheet.Cells["D5"].Value = "Group";
                sheet.Cells["E5"].Value = "Amount";
                sheet.Cells["D6"].Value = "keep";
                sheet.Cells["E6"].Value = 12;
                sheet.Cells["D7"].Value = "hide";
                sheet.Cells["E7"].Value = 14;
                workbook.RangeSelection = sheet.Cells["D:D"];
            });
            var wholeColumn = Options();
            Check(Address(wholeColumn, "EntireSheet") == "D5:E7" && Address(wholeColumn, "SelectedCells") == "D5:D7",
                "R import bounds ignore surrounding formatting and trim a full-column selection on the row axis");

            Update(() =>
            {
                workbook.RangeSelection = sheet.Cells["D5:H20"];
                sheet.Cells["D5:E7"].AutoFilter(0, "keep", AutoFilterOperator.Custom, null, true);
                Check(sheet.Cells["D7"].EntireRow.Hidden && !sheet.Cells["D6"].EntireRow.Hidden,
                    "the fixture's outermost data row is hidden by an active filter");
            });
            var filtered = Options();
            Check(Address(filtered, "EntireSheet") == "D5:E7" && Address(filtered, "SelectedCells") == "D5:H20",
                "filtered-out worksheet values still define the bounds and explicitly selected cells keep their full range");

            Update(() =>
            {
                sheet.Cells["F3"].Value = 101;
                sheet.Cells["F12"].Value = 102;
                workbook.RangeSelection = sheet.Cells["E:E"];
            });
            var filteredColumn = Options();
            Check(Address(filteredColumn, "EntireSheet") == "D3:F12" && Address(filteredColumn, "SelectedCells") == "E5:E7",
                "a selected whole column ignores earlier and later rows in other columns and keeps its filter-hidden last row");

            Update(() =>
            {
                sheet.AutoFilterMode = false;
                sheet.Cells["D10"].Value = 15;
                sheet.Cells["E10"].Value = 22;
                workbook.RangeSelection = sheet.Cells["D9:E10"];
            });
            void VerifyHeaders(object left, object right, bool expected, string label)
            {
                Update(() =>
                {
                    sheet.Cells["D9"].Value = left;
                    var rightCell = sheet.Cells["E9"];
                    rightCell.NumberFormat = right is string ? "@" : "General";
                    rightCell.Value = right;
                    if (right is string) Check(rightCell.Value is string, "the heading fixture retains the value as text");
                });
                var selection = Get(Options(), "SelectedCells");
                Check((bool)Get(selection, "SuggestHeaders") == expected, label);
            }
            VerifyHeaders("Group name", null, true, "a selected range below the first used row can have text headings with a blank heading cell");
            VerifyHeaders(102d, 103d, false, "typed numeric first rows remain data");
            VerifyHeaders("Group name", 103d, false, "a first row with text and a typed number remains data");
            VerifyHeaders("Group name", "0103", false, "a number entered as text prevents a heading suggestion");
            VerifyHeaders("Group name", "1.03e+02", false, "scientific notation entered as text prevents a heading suggestion");
            VerifyHeaders("Group name", false, false, "a Boolean in the first row prevents a heading suggestion");
            VerifyHeaders(null, null, false, "a completely blank first row remains data");
        }
        finally { if (original != null) Update(() => workbook.ActiveWorksheet = original); }
    }

    static async Task VerifyWorksheetRValues(Form session, string check, string label)
    {
        var console = RConsole(session);
        int before = RFinished(session);
        await ((Task)Call(console, "SubmitAsync", check + "\ncat('SHELL_WORKSHEET_R_OK\\n')", "console"))
            .WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(session, before);
        Check(((string)Get(console, "Transcript")).Split('\n').Any(line => line.Trim() == "SHELL_WORKSHEET_R_OK"), label);
    }

    // A saved worksheet script carries its data beside it, and every R window works on a copy of its own: the session's import, or, for
    // a script opened again, a copy made as it opened.  So Run, Save, Save As and R+ use the window's own data whatever is written to a
    // data file beside a script meanwhile, and a save that would replace data the window did not write asks first (Codex's third and
    // fourth reviews).  Save and Save As are the window's own, with the file the Save As dialog would give, and the answer to its
    // question, put in the user's place; Run and R+ are as the window runs them.
    static async Task CheckSavedWorksheetR(Form imported, string rscript, string verifyData)
    {
        var main = (frmMain)imported.MdiParent!;
        string source = (string)Get(RConsole(imported), "Script");
        string transientFolder = Directory.EnumerateDirectories(Path.GetTempPath(), "StatsDirect-worksheet-r-*")
            .Single(folder => source.Contains(Path.GetFileName(folder), StringComparison.Ordinal));
        string originalData = Path.Combine(transientFolder, "worksheet-data.csv");
        string scratch = Directory.CreateTempSubdirectory("StatsDirect-worksheet-save-").FullName;
        const string scriptName = "worksheet results.R";
        string destination = Path.Combine(scratch, scriptName), elsewhere = Path.Combine(scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        byte[] data = File.ReadAllBytes(originalData);
        // Another workbook's data, in the same layout: the measurement 15 is 999.
        string otherData = File.ReadAllText(originalData).Replace("15", "999", StringComparison.Ordinal);
        string otherVerify = verifyData.Replace("15", "999", StringComparison.Ordinal);
        Check(otherData != File.ReadAllText(originalData) && otherVerify != verifyData, "the other workbook's data differ from the script's");
        byte[] other = System.Text.Encoding.UTF8.GetBytes(otherData);
        var windows = new List<Form>();
        try
        {
            static string DataBeside(string script) => string.Equals(Path.GetExtension(script), ".R", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(script, null) + ".worksheet-data.csv" : script + "-worksheet-data.csv";
            static bool Refers(string text, string dataFile) => text.Contains(dataFile.Replace('\\', '/'), StringComparison.Ordinal);
            static string Script(Form window) => (string)Get(RConsole(window), "Script");
            static string OwnData(Form window) => Path.Combine((string)Get(window, "WorksheetImportFolder"), "worksheet-data.csv");
            // The import's reader of a data file, and a path as an R string, as StatsDirect writes them.
            static string ImportReader(string path) => (string)WorksheetRDataMethod("ReaderOperand").Invoke(null, new object[] { path })!;
            static string QuotedPath(string path) => (string)WorksheetRDataMethod("QuotePath").Invoke(null, new object[] { path })!;
            static MethodInfo WorksheetRDataMethod(string name) => typeof(frmMain).Assembly.GetType("StatsDirect.UI.WorksheetRData")!
                .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
            // The answer to the question before replacing someone else's data file, and the files it was asked about.
            var asked = new List<string>();
            bool answer = false;
            void Prepare(Form window, Func<string> chosen)
            {
                window.GetType().GetProperty("RScriptFileChooser", Internal)!.SetValue(window, new Func<string, string>(_ => chosen()));
                window.GetType().GetProperty("ReplaceDataConfirmer", Internal)!.SetValue(window, new Func<string, bool>(file => { asked.Add(file); return answer; }));
            }
            string chosenFile = null;
            async Task<bool> Save(Form window) => await ((Task<bool>)Call(window, "SaveInteractiveAsync", false)).WaitAsync(TimeSpan.FromSeconds(15));
            async Task<bool> SaveAs(Form window, string file) { chosenFile = file; return await ((Task<bool>)Call(window, "SaveInteractiveAsync", true)).WaitAsync(TimeSpan.FromSeconds(15)); }
            async Task<(int Exit, string Output)> RunSaved(string folder, string script, string verify)
            {
                string probe = Path.Combine(folder, "verify.R");
                await File.WriteAllTextAsync(probe,
                    $"source({JsonSerializer.Serialize(script)}, encoding = 'UTF-8')\n{verify}\ncat('SHELL_SAVED_WORKSHEET_OK\\n')\n");
                var start = new ProcessStartInfo(rscript)
                {
                    WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                // Let Windows R choose its UTF-8 locale instead of inheriting a Unix locale.
                start.Environment.Remove("LC_ALL");
                start.Environment.Remove("LC_CTYPE");
                start.ArgumentList.Add("--vanilla");
                start.ArgumentList.Add(probe);
                using var process = Process.Start(start)!;
                try
                {
                    var output = process.StandardOutput.ReadToEndAsync();
                    var errors = process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                    return (process.ExitCode, await output + "\n" + await errors);
                }
                finally { if (!process.HasExited) process.Kill(); }
            }
            // A saved script, run afresh in R, reads the data given.
            async Task Replay(string script, string verify, string label)
            {
                var (code, output) = await RunSaved(Path.GetDirectoryName(script)!, Path.GetFileName(script), verify);
                Check(code == 0 && output.Split('\n').Any(line => line.Trim() == "SHELL_SAVED_WORKSHEET_OK"), label + $"\nR exit code {code}\n{output}");
            }
            async Task Run(Form window, string verify, string label)
            {
                int before = RFinished(window);
                Call(window, "RunR");
                await CompleteRCommand(window, before);
                await VerifyWorksheetRValues(window, verify, label);
            }
            // R+ writes a file of its own, with the window's data beside it, and leaves the window's script as it is.
            async Task OpenInR(Form window, string file, byte[] expected, string verify, string label)
            {
                string before = await ((Task<string>)Call(RConsole(window), "GetScriptAsync")).WaitAsync(TimeSpan.FromSeconds(15));
                await ((Task<string>)Call(window, "WriteOpenInRCopyAsync", file)).WaitAsync(TimeSpan.FromSeconds(15));
                Check(File.ReadAllBytes(DataBeside(file)).SequenceEqual(expected) && Refers(File.ReadAllText(file), DataBeside(file)) && Script(window) == before, label);
                await Replay(file, verify, label + ", and R reads them");
            }
            // The script and its data file agree: the script reads the data file beside it, which holds the data given.
            static bool Holds(string script, byte[] expected) =>
                File.ReadAllBytes(DataBeside(script)).SequenceEqual(expected) && Refers(File.ReadAllText(script), DataBeside(script));
            // The window's script reads the window's own copy, which holds the data given.  (Each check run in the console is added to the
            // window's script, so a window that has run one has something unsaved.)
            static bool OwnCopy(Form window, byte[] expected) => Refers(Script(window), OwnData(window))
                && File.ReadAllBytes(OwnData(window)).SequenceEqual(expected);
            static bool Saved(Form window) => !(bool)Get(window, "Dirty");
            async Task<Form> Reopen(string script)
            {
                var window = (Form)Call(main, "CreateScriptWindow", script, false);
                windows.Add(window);
                await ((Task)Get(RConsole(window), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
                Prepare(window, () => chosenFile);
                return window;
            }

            // The session: Save writes the script and a copy of the import beside it, and the session goes on reading its import.
            Prepare(imported, () => chosenFile);
            imported.GetType().GetField("currentFile", Internal)!.SetValue(imported, destination);
            Check(await Save(imported) && File.Exists(destination), "saving the worksheet R document writes its script");
            string firstData = DataBeside(destination);
            Check(Holds(destination, data) && Directory.GetFiles(scratch, "*.csv", SearchOption.AllDirectories).Single() == firstData,
                "the saved worksheet R script has a durable, byte-identical copy of its selected data, which it reads");
            string sessionScript = Script(imported);
            Check(Refers(sessionScript, originalData) && !Refers(sessionScript, firstData) && !(bool)Get(imported, "Dirty"),
                "after Save, the session's script still reads the session's own import, and is saved");
            Check(await Save(imported) && Directory.GetFiles(scratch, "*.csv", SearchOption.AllDirectories).Single() == firstData && asked.Count == 0,
                "saving the worksheet R document again reuses the same companion file, without a question");
            File.WriteAllText(firstData, otherData);   // another workbook's script saved under the same name
            await Run(imported, verifyData, "with other data written beside the saved script, the session's Run still reads its own data");
            answer = false;
            Check(!await Save(imported) && asked.Count == 1 && File.ReadAllText(firstData) == otherData,
                "Save asks before replacing a data file the window did not write, and with No saves nothing");
            answer = true;
            Check(await Save(imported) && asked.Count == 2 && Holds(destination, data), "with Yes, Save puts the session's own data back beside the script");

            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            imported.Disposed += (_, _) => closed.TrySetResult();
            imported.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Check(!Directory.Exists(transientFolder) && File.Exists(firstData),
                "closing the source R window removes its transient import and retains the saved companion file");
            await Replay(destination, verifyData, "a fresh Rscript session reads the saved worksheet data after the source window closes");

            // The saved script opened again works on a copy of its own data, made as it opens.
            var first = await Reopen(destination);
            string firstFolder = (string)Get(first, "WorksheetImportFolder");
            Check(OwnCopy(first, data) && Saved(first) && !Refers(Script(first), firstData)
                    && Path.GetFileName(firstFolder).StartsWith("StatsDirect-worksheet-r-", StringComparison.Ordinal),
                "a saved worksheet script opened again reads a copy of its data of its own, and has nothing unsaved");

            // Save As elsewhere, then another workbook's data where the first script's data were (Codex's third review).
            string second = Path.Combine(elsewhere, "second results.R");
            Check(await SaveAs(first, second) && Holds(second, data) && OwnCopy(first, data) && Saved(first),
                "Save As puts a copy of the data beside the new script, and the window goes on reading its own copy");
            File.WriteAllText(firstData, otherData);
            await Run(first, verifyData, "after Save As, with another workbook's data where the first script's data were, Run reads the script's own data");
            Check(await Save(first) && Holds(second, data) && asked.Count == 2, "Save again keeps the script's own data beside it, without a question");
            await Replay(second, verifyData, "the script saved again, run afresh in R, reads its own data");
            await OpenInR(first, Path.Combine(elsewhere, "third results.R"), data, verifyData, "R+ after Save As carries the script's own data");
            // Save As back to the first name: the dialog asked before replacing the script there, and its data file goes with it.
            Check(await SaveAs(first, destination) && Holds(destination, data) && asked.Count == 2,
                "Save As back to the first script's name puts the script's own data beside it, in place of the other workbook's data");
            await Run(first, verifyData, "after Save As back to the first name, Run reads the script's own data");
            await Replay(destination, verifyData, "the script saved back under its first name, run afresh in R, reads its own data");
            // The script's data file gone: Save puts it back from the window's own copy.
            File.Delete(firstData);
            Check(await Save(first) && Holds(destination, data) && asked.Count == 2, "Save with the script's data file gone puts the data back beside it");

            // Two windows, one script name (Codex's fourth review).  The second window opens a script that reads the other workbook's
            // data: the import part of the saved script (each check run in the console is added to a window's script, and the first
            // window's checks are for its own data), with its own data file.
            const string importEnd = "utils::str(worksheet_data)\n";
            string saved = File.ReadAllText(destination);
            int end = saved.IndexOf(importEnd, StringComparison.Ordinal);
            Check(end > 0 && saved.Contains(ImportReader(firstData), StringComparison.Ordinal), "the saved script reads its data file through the import's reader");
            string twin = Path.Combine(elsewhere, "twin.R");
            File.WriteAllText(twin, saved[..(end + importEnd.Length)]
                .Replace(ImportReader(firstData), ImportReader(DataBeside(twin)), StringComparison.Ordinal));
            File.WriteAllText(DataBeside(twin), otherData);
            var secondWindow = await Reopen(twin);
            Check(OwnCopy(secondWindow, other) && Saved(secondWindow) && OwnCopy(first, data), "two windows each hold a copy of their own data");
            string same = Path.Combine(elsewhere, "same.R");
            Check(await SaveAs(first, same) && Holds(same, data), "the first window saves its script as same.R");
            Check(await SaveAs(secondWindow, same) && Holds(same, other) && asked.Count == 2,
                "the second window saves over same.R, which the dialog asked about, with its own data");
            await Run(first, verifyData, "with same.R replaced from the other window, the first window's Run reads its own data");
            await OpenInR(first, Path.Combine(elsewhere, "same R+ first.R"), data, verifyData, "the first window's R+ carries its own data");
            answer = false;
            Check(!await Save(first) && asked.Count == 3 && Holds(same, other),
                "the first window's Save asks before replacing the other window's data, and with No leaves same.R and its data as the other window saved them");
            await Replay(same, otherVerify, "same.R, as the other window saved it, reads the other window's data");
            answer = true;
            Check(await Save(first) && asked.Count == 4 && Holds(same, data), "with Yes, the first window saves same.R and its own data together");
            await Replay(same, verifyData, "same.R, saved again from the first window, reads the first window's data");
            await Run(secondWindow, otherVerify, "the second window's Run still reads its own data");
            await OpenInR(secondWindow, Path.Combine(elsewhere, "same R+ second.R"), other, otherVerify, "the second window's R+ carries its own data");

            // Two script names with one stem, in one folder: each has a data file of its own.
            string siblingR = Path.Combine(elsewhere, "sibling.R"), siblingText = Path.Combine(elsewhere, "sibling.txt");
            Check(await SaveAs(first, siblingR) && await SaveAs(secondWindow, siblingText) && DataBeside(siblingR) != DataBeside(siblingText)
                    && Holds(siblingR, data) && Holds(siblingText, other) && asked.Count == 4,
                "sibling.R and sibling.txt each have a data file of their own: " + Path.GetFileName(DataBeside(siblingR)) + " and " + Path.GetFileName(DataBeside(siblingText)));
            await Replay(siblingR, verifyData, "sibling.R, run afresh in R, reads its own data");
            await Replay(siblingText, otherVerify, "sibling.txt, run afresh in R, reads its own data");
            await Run(first, verifyData, "the window that saved sibling.R runs with its own data");
            await Run(secondWindow, otherVerify, "the window that saved sibling.txt runs with its own data");
            Check(await Save(first) && await Save(secondWindow) && Holds(siblingR, data) && Holds(siblingText, other) && asked.Count == 4,
                "saving sibling.R and sibling.txt again keeps each one's data, without a question");
            await Replay(siblingR, verifyData, "sibling.R, saved again and run afresh in R, reads its own data");
            await Replay(siblingText, otherVerify, "sibling.txt, saved again and run afresh in R, reads its own data");
            await OpenInR(first, Path.Combine(elsewhere, "sibling R+ first.R"), data, verifyData, "R+ from sibling.R's window carries its data");
            await OpenInR(secondWindow, Path.Combine(elsewhere, "sibling R+ second.R"), other, otherVerify, "R+ from sibling.txt's window carries its data");
            var siblingAgain = await Reopen(siblingR);
            Check(OwnCopy(siblingAgain, data) && Saved(siblingAgain), "sibling.R opened again reads a copy of its own data");
            await Run(siblingAgain, verifyData, "sibling.R opened again runs with its own data");
            await Replay(siblingText, otherVerify, "sibling.txt, run afresh in R again, still reads its own data");

            // Save As once more, then the first script and its data file go (Codex's second review): the script goes on with its data.
            string fourth = Path.Combine(elsewhere, "fourth results.R");
            Check(await SaveAs(first, fourth) && Holds(fourth, data), "Save As again puts the data beside the next script too");
            File.Delete(destination);
            File.Delete(firstData);
            Check(await Save(first) && Holds(fourth, data), "saving again after Save As, with the first script and its data gone, keeps the script's data");
            await Replay(fourth, verifyData, "the script saved elsewhere reads its data once the first script and its data file are gone");
            await Run(first, verifyData, "Run reads the window's own data once the first script and its data file are gone");

            // Save As over an ordinary script that reads no worksheet data, beside which a data file of that name holds other data
            // (Codex's retest of 0b9c2282): the dialog asked about the script, not the data file, so the data file is asked about too.
            // No leaves both files as they were; Yes writes the window's script and data together.
            string unrelated = Path.Combine(elsewhere, "unrelated.R");
            File.WriteAllText(unrelated, "x <- 404\n");
            File.WriteAllText(DataBeside(unrelated), otherData);
            byte[] unrelatedScript = File.ReadAllBytes(unrelated);
            int askedBefore = asked.Count;
            answer = false;
            Check(!await SaveAs(first, unrelated) && asked.Count == askedBefore + 1 && File.ReadAllBytes(unrelated).SequenceEqual(unrelatedScript)
                    && File.ReadAllBytes(DataBeside(unrelated)).SequenceEqual(other),
                "Save As over an ordinary script asks about the data file beside it, and with No leaves both files as they were");
            answer = true;
            Check(await SaveAs(first, unrelated) && asked.Count == askedBefore + 2 && Holds(unrelated, data),
                "with Yes, Save As over the ordinary script writes the window's script and its data together");
            await Replay(unrelated, verifyData, "the script saved over the ordinary one, run afresh in R, reads the window's data");

            // A saved script whose data file another program holds open, so that no copy can be made, does not open working on that file
            // (Codex's retest of 0b9c2282): the user is asked to close it and retry, and with Cancel the script stays closed.
            string locked = Path.Combine(elsewhere, "locked.R");
            File.WriteAllText(locked, File.ReadAllText(twin).Replace(ImportReader(DataBeside(twin)), ImportReader(DataBeside(locked)), StringComparison.Ordinal));
            File.WriteAllBytes(DataBeside(locked), data);
            (Form Window, Exception Error, int Asked) OpenHeld(Func<FileStream, bool> retry)
            {
                var window = (Form)Call(main, "CreateScriptWindow");
                windows.Add(window);
                var held = new FileStream(DataBeside(locked), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                int retries = 0;
                window.GetType().GetProperty("DataFileRetrier", Internal)!.SetValue(window, new Func<string, bool>(_ => { retries++; return retry(held); }));
                try { Call(window, "OpenFile", locked, false, null); return (window, null, retries); }
                catch (TargetInvocationException e) { return (window, e.InnerException, retries); }
                finally { held.Dispose(); }
            }
            var (refused, refusal, refusedAsked) = OpenHeld(_ => false);
            Check(refusal?.GetType().Name == "WorksheetDataUnavailableException" && refusedAsked == 1 && Get(refused, "WorksheetImportFolder") == null
                    && ((RichTextBox)Field(refused, "rtbDoc")).Text.Length == 0,
                "a saved script whose data file is held open elsewhere asks to retry, and with Cancel does not open" + (refusal == null ? "" : ": " + refusal.Message));
            var (retried, failure, retriedAsked) = OpenHeld(held => { held.Dispose(); return true; });
            Check(failure == null && retriedAsked == 1 && OwnCopy(retried, data), "with the file closed and Retry, the script opens with a copy of its own data");
            await ((Task)Get(RConsole(retried), "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
            File.WriteAllText(DataBeside(locked), otherData);
            await Run(retried, verifyData, "the script opened after Retry runs with its own copy, whatever is written to the data file since");

            // A reader the user has changed is the user's own code (Codex's fourth review): a comment that mentions the window's data file
            // is left as it is, and no data file is written for the script.  The window holds the other workbook's data, and the user's
            // file holds the script's own, so R shows which it read.
            var third = await Reopen(twin);
            string userData = Path.Combine(elsewhere, "user.csv");
            File.WriteAllBytes(userData, data);
            string own = OwnData(third), ownReader = ImportReader(own);
            string comment = "# first read from " + QuotedPath(own) + "\n";
            Check(OwnCopy(third, other) && Script(third).Contains(ownReader, StringComparison.Ordinal), "the window's script reads its own copy through the import's reader");
            string edited = comment + Script(third).Replace(ownReader, ImportReader(userData), StringComparison.Ordinal);
            RConsole(third).GetType().GetProperty("Script", Internal)!.SetValue(RConsole(third), edited);
            string seventh = Path.Combine(elsewhere, "seventh results.R");
            Check(await SaveAs(third, seventh) && !File.Exists(DataBeside(seventh)) && File.ReadAllText(seventh).StartsWith(comment, StringComparison.Ordinal)
                    && File.ReadAllText(seventh).Contains(ImportReader(userData), StringComparison.Ordinal),
                "a script whose reader the user changed is saved as it is, comment included, without a data file");
            await Replay(seventh, verifyData, "the script with the user's own reader reads the user's file");

            // A commented-out import is a comment (Codex's retest of 0b9c2282).  The import's whole read statement commented out, with a
            // reader of the user's own file after it: Save As and R+ leave the comment as it is, write no data file, and R reads the
            // user's file.  The window holds the other workbook's data and the user's file the script's own, so R shows which it read.
            var commentedWindow = await Reopen(twin);
            string commentedOwn = ImportReader(OwnData(commentedWindow)), commentedScript = Script(commentedWindow);
            int statementStart = commentedScript.IndexOf("worksheet_data <- " + commentedOwn, StringComparison.Ordinal);
            const string statementEnd = "blank.lines.skip = FALSE)\n";
            int statementStop = statementStart < 0 ? -1 : commentedScript.IndexOf(statementEnd, statementStart, StringComparison.Ordinal);
            Check(statementStart >= 0 && statementStop > statementStart, "the window's script holds the import's read statement");
            statementStop += statementEnd.Length;
            string statement = commentedScript[statementStart..statementStop];
            string commentedOut = string.Concat(statement.Split('\n').Where(line => line.Length > 0).Select(line => "# " + line + "\n"));
            string usersReading = statement.Replace(commentedOwn, ImportReader(userData), StringComparison.Ordinal);
            ((RichTextBox)Field(commentedWindow, "rtbDoc")).Text = commentedScript[..statementStart] + commentedOut + usersReading + commentedScript[statementStop..];
            string eighth = Path.Combine(elsewhere, "eighth results.R");
            Check(await SaveAs(commentedWindow, eighth) && !File.Exists(DataBeside(eighth))
                    && File.ReadAllText(eighth).Replace("\r\n", "\n").Contains(commentedOut, StringComparison.Ordinal)
                    && File.ReadAllText(eighth).Contains(ImportReader(userData), StringComparison.Ordinal),
                "Save As leaves a commented-out import as it is and writes no data file for it");
            await Replay(eighth, verifyData, "the script with the import commented out, run afresh in R, reads the user's file");
            string ninth = Path.Combine(elsewhere, "ninth results.R");
            await ((Task<string>)Call(commentedWindow, "WriteOpenInRCopyAsync", ninth)).WaitAsync(TimeSpan.FromSeconds(15));
            Check(!File.Exists(DataBeside(ninth)) && File.ReadAllText(ninth).Replace("\r\n", "\n").Contains(commentedOut, StringComparison.Ordinal),
                "R+ leaves a commented-out import as it is and writes no data file for it");
            await Replay(ninth, verifyData, "R+'s copy of the script with the import commented out reads the user's file");
        }
        finally
        {
            // Windows with checks added to their scripts have something unsaved: they are disposed without the question about saving.
            foreach (var window in windows)
                if (!window.IsDisposed) window.Dispose();
            await DeleteRFixtureDirectory(scratch, "StatsDirect-worksheet-save-");
        }
    }

    static async Task DriveWorksheetRPrompt(frmMain main, string title, string selectedAddress, string choice,
        bool vectorList, bool emptySelection, bool friendlyNames)
    {
        Form prompt = null;
        for (int attempt = 0; attempt < 80 && prompt == null; attempt++)
        {
            await Task.Delay(25);
            prompt = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Visible && form.GetType().Name == "WorksheetRPrompt");
        }
        Check(prompt != null, "the worksheet R button opens the actual import prompt");
        try
        {
            var scope = (Label)Field(prompt, "scope");
            var headers = (CheckBox)prompt.Controls.Find("worksheetRHeaders", true).Single();
            var names = (CheckBox)prompt.Controls.Find("worksheetRFriendlyNames", true).Single();
            var format = (ComboBox)prompt.Controls.Find("worksheetRFormat", true).Single();
            RadioButton Find(string suffix) => (RadioButton)prompt.Controls.Find("worksheetR" + suffix, true).Single();
            var all = Find("All");
            var selected = Find("Selected");
            var none = Find("None");
            Check(prompt.Owner == main && prompt.Text == title && scope.Text.Contains("worksheet_data", StringComparison.Ordinal)
                && all.Enabled && all.Text.Replace("$", "").Contains("A1:C4", StringComparison.Ordinal)
                && Regex.IsMatch(all.Text, @"\b4\D+3\b"),
                title + " identifies the full used worksheet range and its dimensions");
            Check(selectedAddress == null ? !selected.Enabled : selected.Enabled && selected.Text.Replace("$", "").Contains(selectedAddress, StringComparison.Ordinal)
                && Regex.IsMatch(selected.Text, selectedAddress == "B2" ? @"\b1\D+1\b" : @"\b3\D+2\b"),
                selectedAddress == null ? "the Selected cells choice is disabled for an unavailable range"
                    : "Selected cells identifies the exact selection and its dimensions");
            if (emptySelection)
                Check(selected.Text.Contains("no data", StringComparison.OrdinalIgnoreCase),
                    "Selected cells explains that the formatting-only selected column has no data");
            Check(none.Enabled && none.Checked && !all.Checked && !selected.Checked && !headers.Enabled && !headers.Checked,
                "No data is selected on opening and disables the headings checkbox");
            Check(names.Text == "Use R-friendly names" && !names.Checked && !names.Enabled,
                title + " preserves worksheet names by default");
            Check(!format.Enabled && format.SelectedIndex == 0 && format.AccessibleName == "Import as"
                && format.Items.Cast<object>().Select(format.GetItemText)
                    .SequenceEqual(new[] { "Data frame (keep rows aligned)", "List of vectors (independent samples)" }),
                "No data disables the format selector, which defaults to Data frame and offers List of vectors");
            var buttons = WorksheetRButtons(prompt).ToDictionary(button => button.DialogResult);
            Check(buttons.Keys.ToHashSet().SetEquals([DialogResult.OK, DialogResult.Cancel])
                && ReferenceEquals(prompt.AcceptButton, buttons[DialogResult.OK]),
                "copying worksheet data requires an explicit choice");
            var client = prompt.RectangleToScreen(prompt.ClientRectangle);
            Check(buttons.Values.Cast<Control>().Concat(new Control[] { all, selected, none, headers, names, format, scope })
                .All(control => client.Contains(control.RectangleToScreen(control.ClientRectangle))),
                "all R prompt controls fit inside the dialog at this Windows display scale");
            if (choice is "All" or "Selected")
            {
                Find(choice).Checked = true;
                Check(headers.Enabled == (choice == "All" || selectedAddress != "B2"),
                    "the headings checkbox is enabled only for a data choice with more than one row");
                Check(headers.Checked == (choice == "All" || selectedAddress == "A1:B3"),
                    "the headings checkbox automatically follows the chosen range and stays off for one cell");
                Check(names.Enabled == headers.Checked && !names.Checked,
                    "R-friendly names is an optional choice available only when importing column headings");
                Check(format.Enabled && format.SelectedIndex == 0, "a data choice enables the format selector with Data frame selected");
                if (vectorList)
                {
                    format.SelectedIndex = 1;
                    var hint = (Label)Field(prompt, "formatHint");
                    Check(hint.Visible && hint.Text.Contains("Missing values", StringComparison.Ordinal)
                        && client.Contains(hint.RectangleToScreen(hint.ClientRectangle)),
                        "the list of vectors choice explains per-column missing-value removal inside the dialog");
                    Check(PromptChoicesWhole(prompt), "with the hint shown, every choice keeps its full size inside the dialog");
                }
                if (friendlyNames)
                {
                    Check(names.Enabled, "R-friendly names can be enabled for a worksheet data import");
                    names.Checked = true;
                    Check((bool)Get(prompt, "UseFriendlyNames"), "the checkbox sets the actual import option");
                }
            }
            buttons[choice == null ? DialogResult.Cancel : DialogResult.OK].PerformClick();
        }
        finally
        {
            if (prompt.DialogResult == DialogResult.None) prompt.CancelButton?.PerformClick();
        }
    }

    // Every choice of the prompt whole: its row as tall as it, its width its preferred width (no text cut off), inside the dialog.
    // Every choice whole, within the choices' view itself rather than the dialog, and the view without scroll bars unless the working
    // area keeps the dialog smaller than its choices (Codex's third review).
    static bool PromptChoicesWhole(Form prompt)
    {
        var layout = (TableLayoutPanel)Field(prompt, "layout");
        var view = (Panel)prompt.Controls.Find("worksheetRChoices", true).Single();
        int[] rows = layout.GetRowHeights();
        var shown = view.RectangleToScreen(view.ClientRectangle);
        var area = Screen.FromControl(prompt).WorkingArea;
        bool constrained = prompt.Width >= area.Width || prompt.Height >= area.Height;
        return (constrained || !view.HorizontalScroll.Visible && !view.VerticalScroll.Visible) && PromptChoices(prompt)
            .All(control => !control.Visible || rows[layout.GetRow(control)] >= control.Height + control.Margin.Vertical - 1
                && control.Width >= control.GetPreferredSize(Size.Empty).Width - 1 && shown.Contains(control.RectangleToScreen(control.ClientRectangle)));
    }

    static IEnumerable<Control> PromptChoices(Form prompt) =>
        new[] { "worksheetRAll", "worksheetRSelected", "worksheetRNone", "worksheetRHeaders", "worksheetRFriendlyNames" }
            .Select(name => prompt.Controls.Find(name, true).Single()).Append((Control)Field(prompt, "formatHint"));

    // Each choice, the sentence above them and the format's row can be scrolled into the view: whole when it fits there, and otherwise
    // as much of it as the view holds.
    static bool PromptChoicesReachable(Form prompt)
    {
        var view = (Panel)prompt.Controls.Find("worksheetRChoices", true).Single();
        return PromptChoices(prompt).Append((Control)Field(prompt, "scope")).Append((Control)Field(prompt, "formatRow")).Where(control => control.Visible).All(control =>
        {
            view.ScrollControlIntoView(control);
            Application.DoEvents();
            var shown = view.RectangleToScreen(view.ClientRectangle);
            var bounds = control.RectangleToScreen(control.ClientRectangle);
            return control.Width <= shown.Width && control.Height <= shown.Height ? shown.Contains(bounds) : shown.IntersectsWith(bounds);
        });
    }

    // A long range of several areas (the prompt lists three, then ",…"), with the list's hint shown: the prompt grows to fit it.
    static void CheckWorksheetRPromptLayout(frmMain main)
    {
        var ui = typeof(frmMain).Assembly;
        var dataType = ui.GetType("StatsDirect.UI.WorksheetRData")!;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        object selection = Activator.CreateInstance(dataType, "XEW1000000:XEX1048576,XEZ1000000:XEZ1048576,XFB1000000:XFB1048576,\u2026", 48_577, 5, true, null)!;
        object entire = Activator.CreateInstance(dataType, "A1:XFD1048576", 1_048_576, 16_384, true, null)!;
        object options = Activator.CreateInstance(ui.GetType("StatsDirect.UI.WorksheetRImportOptions")!, "Sheet1", entire, selection, null)!;
        var prompt = (Form)Activator.CreateInstance(ui.GetType("StatsDirect.UI.WorksheetRPrompt")!, flags, null, new object[] { "Open in StatsDirect R", options }, null)!;
        try
        {
            prompt.Show(main);
            var selected = (RadioButton)prompt.Controls.Find("worksheetRSelected", true).Single();
            selected.Checked = true;
            ((ComboBox)prompt.Controls.Find("worksheetRFormat", true).Single()).SelectedIndex = 1;
            Check(selected.Text.Contains("XFB1000000:XFB1048576,\u2026 (48,577 rows \u00d7 5 columns)", StringComparison.Ordinal) && PromptChoicesWhole(prompt),
                "a long range of several areas, with the list's hint shown, leaves every choice of the import prompt whole inside it");
            // Where the dialog cannot be as large as its choices (a small working area, or a large display scale), the choices scroll:
            // the range label stays whole, and Open and Cancel stay in view (Codex's second review).
            prompt.ClientSize = new Size(prompt.LogicalToDeviceUnits(260), prompt.LogicalToDeviceUnits(150));
            Application.DoEvents();
            var view = (Panel)prompt.Controls.Find("worksheetRChoices", true).Single();
            var content = (Control)Field(prompt, "layout");
            var visible = prompt.RectangleToScreen(prompt.ClientRectangle);
            bool actionsInView = WorksheetRButtons(prompt).Count() == 2
                && WorksheetRButtons(prompt).All(button => visible.Contains(button.RectangleToScreen(button.ClientRectangle)));
            bool scrolls = (content.Width <= view.ClientSize.Width || view.HorizontalScroll.Visible)
                && (content.Height <= view.ClientSize.Height || view.VerticalScroll.Visible);
            bool reachable = PromptChoicesReachable(prompt);
            Check(actionsInView && scrolls && reachable && selected.Width >= selected.GetPreferredSize(Size.Empty).Width - 1,
                $"in a dialog smaller than its choices, the choices scroll, each can be scrolled into view, the range label stays whole, and Open and Cancel stay in view (actions {actionsInView}, scrolls {scrolls}, reachable {reachable})");
        }
        finally { prompt.Close(); prompt.Dispose(); }
    }

    static IEnumerable<Button> WorksheetRButtons(Control parent) => parent.Controls.Cast<Control>()
        .SelectMany(control => control is Button button ? new[] { button } : WorksheetRButtons(control));

    static async Task CheckWorksheetRGuiImport(Form grid, string scope, string formatName, string verifyData, bool friendlyNames = false,
        bool ownProfile = false)
    {
        // RULES.md 6.1: the user's own start-up file still runs in R+, and R processes started from the session start normally.
        // The fixture's R working folder is the test's isolated user-data folder, so its .Rprofile is the test's own.
        string userProfile = Path.Combine(StatsDirect.Configuration.SDConfiguration.MyStatsDirectRFolder, ".Rprofile");
        if (ownProfile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(userProfile)!);
            File.WriteAllText(userProfile, "options(statsdirect.own.profile = TRUE)\n");
            verifyData += """

                stopifnot(isTRUE(getOption("statsdirect.own.profile")), Sys.getenv("R_PROFILE_USER") == "",
                          identical(system2(file.path(R.home("bin"), "Rscript.exe"), c("-e", shQuote("cat(6 * 7)")), stdout = TRUE), "42"))
                """;
        }
        var launcher = typeof(frmMain).Assembly.GetType("StatsDirect.R.WorksheetRLauncher", throwOnError: true)!;
        object LaunchCall(string method, params object[] arguments) =>
            launcher.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments)!;
        var version = (RVersion)LaunchCall("FindRGui");
        string folder = (string)LaunchCall("CreateImportFolder");
        string scratch = Path.Combine(Path.GetTempPath(), "StatsDirect-rgui-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string receipt = Path.Combine(scratch, "workspace.txt");
        try
        {
            var data = Get(Call(grid, "DescribeWorksheetRData"), scope);
            var writeImportScript = (Delegate)Get(data, "WriteImportScript");
            var format = Enum.Parse(writeImportScript.GetType().GetMethod("Invoke")!.GetParameters()[1].ParameterType, formatName);
            string source = (string)writeImportScript.DynamicInvoke(true, format, friendlyNames, folder)!;
            string probe = $$"""
                .First <- function() {
                    result <- tryCatch({
                        {{verifyData}}
                        "verified"
                    }, error = function(e) conditionMessage(e))
                    writeLines(result, {{JsonSerializer.Serialize(receipt.Replace('\\', '/'))}})
                    q(save = "no", status = 0L, runLast = FALSE)
                }
                """;
            LaunchCall("Launch", version, probe + "\n" + source, folder);
            var until = DateTime.UtcNow.AddSeconds(25);
            while ((!File.Exists(receipt) || Directory.Exists(folder)) && DateTime.UtcNow < until)
                await Task.Delay(50);
            Check(File.Exists(receipt) && File.ReadAllText(receipt).Trim() == "verified" && !Directory.Exists(folder),
                "real external RGui receives the " + (formatName == "DataFrame" ? "selected-cell data frame" : "entire worksheet as a list of vectors")
                + (friendlyNames ? " with unique R-friendly names" : " with original names")
                + (ownProfile ? ", runs the user's own R start-up file, and leaves R processes started from it unaffected" : "")
                + " in its startup workspace and removes the temporary import on exit");
        }
        finally
        {
            LaunchCall("DeleteImportFolder", folder);
            await DeleteRFixtureDirectory(scratch, "StatsDirect-rgui-check-");
            if (ownProfile && File.Exists(userProfile)) File.Delete(userProfile);
        }
    }
}

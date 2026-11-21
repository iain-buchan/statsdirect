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

    static async Task CheckSavedWorksheetR(Form imported, string rscript, string verifyData)
    {
        var main = (frmMain)imported.MdiParent!;
        string source = (string)Get(RConsole(imported), "Script");
        string transientFolder = Directory.EnumerateDirectories(Path.GetTempPath(), "StatsDirect-worksheet-r-*")
            .Single(folder => source.Contains(Path.GetFileName(folder), StringComparison.Ordinal));
        string originalData = Path.Combine(transientFolder, "worksheet-data.csv");
        string scratch = Directory.CreateTempSubdirectory("StatsDirect-worksheet-save-").FullName;
        const string scriptName = "worksheet results.R";
        string destination = Path.Combine(scratch, scriptName);
        try
        {
            imported.GetType().GetField("currentFile", Internal)!.SetValue(imported, destination);
            async Task<bool> Save() => await ((Task<bool>)Call(imported, "SaveInteractiveAsync", false)).WaitAsync(TimeSpan.FromSeconds(15));
            Check(await Save() && File.Exists(destination), "saving the worksheet R document writes its script");
            string[] companions = Directory.GetFiles(scratch, "*.csv", SearchOption.AllDirectories);
            Check(companions.Length == 1 && File.ReadAllBytes(companions[0]).SequenceEqual(File.ReadAllBytes(originalData)),
                "the saved worksheet R script has a durable, byte-identical copy of its selected data");
            Check(await Save() && Directory.GetFiles(scratch, "*.csv", SearchOption.AllDirectories).SequenceEqual(companions),
                "saving the worksheet R document again reuses the same companion file");

            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            imported.Disposed += (_, _) => closed.TrySetResult();
            imported.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Check(!Directory.Exists(transientFolder) && File.Exists(companions[0]),
                "closing the source R window removes its transient import and retains the saved companion file");

            async Task<(int Exit, string Output)> RunSaved(string folder, string script)
            {
                string probe = Path.Combine(folder, "verify.R");
                await File.WriteAllTextAsync(probe,
                    $"source({JsonSerializer.Serialize(script)}, encoding = 'UTF-8')\n{verifyData}\ncat('SHELL_SAVED_WORKSHEET_OK\\n')\n");
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
            var (exit, receipt) = await RunSaved(scratch, scriptName);
            Check(exit == 0 && receipt.Split('\n').Any(line => line.Trim() == "SHELL_SAVED_WORKSHEET_OK"),
                "a fresh Rscript session reads the saved worksheet data after the source window closes" + $"\nR exit code {exit}\n{receipt}");

            // The saved script opened again and saved somewhere else (Save As, or R+, which write through the same code) takes a copy of its
            // data beside the new script and reads that copy, so the first script and its data file can go.
            var reopened = (Form)Call(main, "CreateScriptWindow", destination, false);
            try
            {
                string elsewhere = Path.Combine(scratch, "elsewhere");
                Directory.CreateDirectory(elsewhere);
                string second = Path.Combine(elsewhere, "second results.R"), secondData = Path.Combine(elsewhere, "second results.worksheet-data.csv");
                // The editor's text, which still refers to the first data file after Save As, as the window's editor does.
                string editorText = File.ReadAllText(destination);
                Call(reopened, "WriteRScript", second, editorText);
                reopened.GetType().GetField("currentFile", Internal)!.SetValue(reopened, second);   // Save As: the window's file is the new one
                Check(File.Exists(secondData) && File.ReadAllBytes(secondData).SequenceEqual(File.ReadAllBytes(companions[0]))
                        && File.ReadAllText(second).Contains(secondData.Replace('\\', '/'), StringComparison.Ordinal),
                    "a saved worksheet script opened again and saved elsewhere carries a copy of its data beside the new script");
                File.Delete(destination);
                File.Delete(companions[0]);
                // Save again, the first pair gone: the script keeps reading its own copy (Codex's second review).
                Call(reopened, "WriteRScript", second, editorText);
                Check(File.ReadAllText(second).Contains(secondData.Replace('\\', '/'), StringComparison.Ordinal) && File.Exists(secondData),
                    "saving again after Save As, with the first script and its data gone, keeps the script reading its own copy of the data");
                var (secondExit, secondReceipt) = await RunSaved(elsewhere, "second results.R");
                Check(secondExit == 0 && secondReceipt.Split('\n').Any(line => line.Trim() == "SHELL_SAVED_WORKSHEET_OK"),
                    "the script saved elsewhere reads its own copy of the data once the first script and its data file are gone" + $"\nR exit code {secondExit}\n{secondReceipt}");
                // R+ after Save As writes through the same code: a new file beside which the data are copied too.
                string third = Path.Combine(elsewhere, "third results.R"), thirdData = Path.Combine(elsewhere, "third results.worksheet-data.csv");
                Call(reopened, "WriteRScript", third, editorText);
                var (thirdExit, thirdReceipt) = await RunSaved(elsewhere, "third results.R");
                Check(File.Exists(thirdData) && File.ReadAllText(third).Contains(thirdData.Replace('\\', '/'), StringComparison.Ordinal)
                        && thirdExit == 0 && thirdReceipt.Split('\n').Any(line => line.Trim() == "SHELL_SAVED_WORKSHEET_OK"),
                    "R+ after Save As, with the first pair gone, carries the data beside its new file, and R reads them" + $"\nR exit code {thirdExit}\n{thirdReceipt}");
            }
            finally
            {
                reopened.Close();
                for (int i = 0; i < 100 && !reopened.IsDisposed; i++)
                    await Task.Delay(50);
            }
        }
        finally { await DeleteRFixtureDirectory(scratch, "StatsDirect-worksheet-save-"); }
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
    static bool PromptChoicesWhole(Form prompt)
    {
        var layout = (TableLayoutPanel)Field(prompt, "layout");
        int[] rows = layout.GetRowHeights();
        var client = prompt.RectangleToScreen(prompt.ClientRectangle);
        return new[] { "worksheetRAll", "worksheetRSelected", "worksheetRNone", "worksheetRHeaders", "worksheetRFriendlyNames" }
            .Select(name => prompt.Controls.Find(name, true).Single()).Append((Control)Field(prompt, "formatHint"))
            .All(control => !control.Visible || rows[layout.GetRow(control)] >= control.Height + control.Margin.Vertical - 1
                && control.Width >= control.GetPreferredSize(Size.Empty).Width - 1 && client.Contains(control.RectangleToScreen(control.ClientRectangle)));
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
            Check(actionsInView && scrolls && selected.Width >= selected.GetPreferredSize(Size.Empty).Width - 1,
                $"in a dialog smaller than its choices, the choices scroll, the range label stays whole, and Open and Cancel stay in view (actions {actionsInView}, scrolls {scrolls})");
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

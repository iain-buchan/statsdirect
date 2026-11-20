using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using StatsDirect.R;
using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckWorksheetRSessions(frmMain main)
    {
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
            sheet.Cells["C4"].Value = 999;
        }
        finally { workbook.ReleaseLock(); }
        Select("A1:B3");

        var toolbar = (ToolStrip)Field(main, "toolStrip");
        var embedded = (ToolStripButton)toolbar.Items["rToolStripButton"]!;
        var external = (ToolStripButton)toolbar.Items["rPlusToolStripButton"]!;
        Check(toolbar.Visible && embedded.Available && embedded.Enabled && external.Available && external.Enabled
            && embedded.AccessibleName == "Open R session" && external.AccessibleName == "Open external R session",
            "the worksheet toolbar has distinct and accessible R and R+ buttons");

        async Task<Form> Activate(ToolStripButton button, string title, string address, DialogResult action, bool headers = false)
        {
            string selected = Selection();
            var before = main.MdiChildren.ToHashSet();
            var pending = DriveWorksheetRPrompt(main, title, address, action, headers);
            button.PerformClick();
            await pending;
            var added = main.MdiChildren.Where(child => !before.Contains(child)).ToArray();
            if (action == DialogResult.Cancel)
            {
                Check(added.Length == 0 && main.ActiveMdiChild == grid && Selection() == selected,
                    "cancelling " + title + " opens no session and keeps the selection");
                return null;
            }
            Check(added.Length == 1 && added[0].GetType().Name == "frmScript" && main.ActiveMdiChild == added[0],
                title + " creates and activates exactly one native R document");
            return added[0];
        }

        await Activate(embedded, "Open R session", "A1:B3", DialogResult.Cancel);
        await Activate(external, "Open external R session", "A1:B3", DialogResult.Cancel);
        Select("B2");
        await Activate(embedded, "Open R session", "A1:C4", DialogResult.Cancel);
        Select("A1:B3");
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
        using var imported = await Activate(embedded, "Open R session", "A1:B3", DialogResult.Yes, headers: true);
        var importedConsole = RConsole(imported);
        await ((Task)Get(importedConsole, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(imported, 0);
        Check(((string)Get(importedConsole, "Script")).Contains("worksheet_data", StringComparison.Ordinal) && !toolbar.Visible,
            "R copies the data into the active console and retains its import script");
        int beforeDataCheck = RFinished(imported);
        await ((Task)Call(importedConsole, "SubmitAsync", verifyData + "\ncat('SHELL_WORKSHEET_DATA_OK\\n')", "console"))
            .WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(imported, beforeDataCheck);
        Check(((string)Get(importedConsole, "Transcript")).Split('\n').Any(line => line.Trim() == "SHELL_WORKSHEET_DATA_OK"),
            "the persistent console contains only the selected cells, with original headings, blanks and quoted Unicode text");
        await CheckSavedWorksheetR(imported, rscript, verifyData);

        grid.Activate();
        await Task.Delay(100);
        Select("B2");
        using var empty = await Activate(embedded, "Open R session", "A1:C4", DialogResult.No);
        var emptyConsole = RConsole(empty);
        await ((Task)Get(emptyConsole, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        for (int attempt = 0; attempt < 240 && (string)Get(emptyConsole, "NativeState") != "ready"; attempt++)
            await Task.Delay(50);
        Check((string)Get(emptyConsole, "NativeState") == "ready" && RFinished(empty) == 0 && (string)Get(emptyConsole, "Script") == "",
            "Open without data starts a blank R prompt without running a worksheet import");
        await ((Task)Call(emptyConsole, "SubmitAsync",
            """stopifnot(!exists("worksheet_data", .GlobalEnv, inherits = FALSE)); cat("SHELL_WORKSHEET_EMPTY_OK\n")""", "console"))
            .WaitAsync(TimeSpan.FromSeconds(45));
        await CompleteRCommand(empty, 0);
        Check(((string)Get(emptyConsole, "Transcript")).Split('\n').Any(line => line.Trim() == "SHELL_WORKSHEET_EMPTY_OK"),
            "the blank R session has no worksheet_data from the existing R session");

        grid.Activate();
        await Task.Delay(100);
        Select("A1:B3");
        if (hasRGui) await CheckWorksheetRGuiImport(grid, verifyData);
        else Console.WriteLine("SKIP external worksheet R launch check because RGui is unavailable");
        originalDocument?.Activate();
    }

    static async Task CheckSavedWorksheetR(Form imported, string rscript, string verifyData)
    {
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

            string probe = Path.Combine(scratch, "verify.R");
            await File.WriteAllTextAsync(probe,
                $"source({JsonSerializer.Serialize(scriptName)}, encoding = 'UTF-8')\n{verifyData}\ncat('SHELL_SAVED_WORKSHEET_OK\\n')\n");
            var start = new ProcessStartInfo(rscript)
            {
                WorkingDirectory = scratch, UseShellExecute = false, CreateNoWindow = true,
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
                string receipt = await output;
                string diagnostic = await errors;
                Check(process.ExitCode == 0 && receipt.Split('\n').Any(line => line.Trim() == "SHELL_SAVED_WORKSHEET_OK"),
                    "a fresh Rscript session reads the saved worksheet data after the source window closes" +
                    $"\nR exit code {process.ExitCode}\n{receipt}\n{diagnostic}");
            }
            finally { if (!process.HasExited) process.Kill(); }
        }
        finally { await DeleteRFixtureDirectory(scratch, "StatsDirect-worksheet-save-"); }
    }

    static async Task DriveWorksheetRPrompt(frmMain main, string title, string address, DialogResult action, bool useHeaders)
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
            Check(prompt.Owner == main && prompt.Text == title && scope.Text.Replace("$", "").Contains(address, StringComparison.Ordinal)
                && scope.Text.Contains("worksheet_data", StringComparison.Ordinal) && headers.Enabled,
                title + " identifies the exact workbook range and offers a heading choice");
            var buttons = WorksheetRButtons(prompt).ToDictionary(button => button.DialogResult);
            Check(buttons.Keys.ToHashSet().SetEquals([DialogResult.Yes, DialogResult.No, DialogResult.Cancel])
                && ReferenceEquals(prompt.AcceptButton, buttons[DialogResult.No]),
                "copying worksheet data requires an explicit choice");
            var client = prompt.RectangleToScreen(prompt.ClientRectangle);
            Check(buttons.Values.All(button => client.Contains(button.RectangleToScreen(button.ClientRectangle)))
                && client.Contains(headers.RectangleToScreen(headers.ClientRectangle))
                && client.Contains(scope.RectangleToScreen(scope.ClientRectangle)),
                "all R prompt controls fit inside the dialog at this Windows display scale");
            headers.Checked = useHeaders;
            buttons[action].PerformClick();
        }
        finally
        {
            if (prompt.DialogResult == DialogResult.None) prompt.CancelButton?.PerformClick();
        }
    }

    static IEnumerable<Button> WorksheetRButtons(Control parent) => parent.Controls.Cast<Control>()
        .SelectMany(control => control is Button button ? new[] { button } : WorksheetRButtons(control));

    static async Task CheckWorksheetRGuiImport(Form grid, string verifyData)
    {
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
            var data = Call(grid, "DescribeWorksheetRData", new object[] { null });
            string source = (string)((Delegate)Get(data, "WriteImportScript")).DynamicInvoke(true, folder)!;
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
                "real external RGui receives the same selected cells in its startup workspace and removes the temporary import on exit");
        }
        finally
        {
            LaunchCall("DeleteImportFolder", folder);
            await DeleteRFixtureDirectory(scratch, "StatsDirect-rgui-check-");
        }
    }
}

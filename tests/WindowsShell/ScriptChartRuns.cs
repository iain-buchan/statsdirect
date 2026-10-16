using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.Configuration;
using StatsDirect.R;
using StatsDirect.UI;

internal static partial class Program
{
    // Exercise complete R processes and their real output panes. Each script records
    // its installed default device in a private manifest, so cleanup knows precisely
    // which chart files the test owns even if a run or a pane check fails.
    static async Task CheckScriptChartRuns(frmMain main, Form grid)
    {
        string folder = SDConfiguration.MyStatsDirectRFolder;
        Directory.CreateDirectory(folder);
        string scratch = Path.Combine(Path.GetTempPath(), "StatsDirect-chart-runs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string release = Path.Combine(scratch, "release-slow.txt"), ready = Path.Combine(scratch, "slow-ready.txt");
        var manifests = new List<string>();
        var synthetic = new List<string>();
        var legacy = Directory.GetFiles(folder, "Rplot*")
            .Where(f => new[] { ".svg", ".pdf" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToDictionary(f => f, f => (Bytes: File.ReadAllBytes(f), Modified: File.GetLastWriteTimeUtc(f)));
        using var first = (Form)Call(main, "CreateScriptWindow");
        using var second = (Form)Call(main, "CreateScriptWindow");
        Control Pane(Form script) => (Control)Field(script, "outputPane");
        string RString(string value) => JsonSerializer.Serialize(value.Replace('\\', '/'));

        (string Text, string Manifest) Prepare(string stem, string body)
        {
            string manifest = Path.Combine(scratch, stem + "-device.txt");
            manifests.Add(manifest);
            string text = "writeLines(paste(deparse(getOption(\"device\")), collapse = \" \"), " + RString(manifest) + ")\n"
                + "cat(\"SHELL_TEST_DEVICE:\", paste(deparse(getOption(\"device\")), collapse = \" \"), \"\\n\")\n" + body;
            return (text, manifest);
        }

        string Start(Form script, string stem, string body)
        {
            var prepared = Prepare(stem, body);
            Call(Pane(script), "Clear");
            Call(script, "ShowR", prepared.Text, "Chart run " + stem, null, true);
            return prepared.Manifest;
        }

        string RunId(string manifest)
        {
            var ids = Regex.Matches(File.ReadAllText(manifest), @"Rplot-([0-9a-f]{32})-", RegexOptions.IgnoreCase)
                .Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToArray();
            Check(ids.Length == 1, "the installed R device records one unique automatic-chart run identifier");
            return ids[0];
        }

        async Task Completed(Form script, bool expectedError = false)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(120);
            while ((bool)Field(script, "runningR") && DateTime.UtcNow < until) await Task.Delay(50);
            Check(!(bool)Field(script, "runningR") && Pane(script).Text.Contains("End of run")
                && (expectedError ? Pane(script).Text.Contains("(R stopped with exit code 1)") : !Pane(script).Text.Contains("(R stopped with exit code")),
                expectedError ? "an R error is reported after the chart run finishes" : "the real R chart run completes successfully");
            await (Task)Call(Pane(script), "SettledAsync");
        }

        async Task CheckPdf(string path, int pages)
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            var pdf = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
            Check(pdf.PageCount == pages, "the explicitly selected PDF is readable and retains all " + pages + " chart page(s)");
        }

        void CheckSvg(string path)
        {
            var svg = System.Xml.Linq.XDocument.Load(path).Root;
            Check(svg?.Name == System.Xml.Linq.XName.Get("svg", "http://www.w3.org/2000/svg") && svg.Descendants().Any(),
                "the automatic chart is a complete, readable SVG document");
        }

        async Task<string[]> ChartLinks(Form script)
        {
            var browser = (WebView2)Field(Pane(script), "browser");
            return JsonSerializer.Deserialize<string[]>(await browser.CoreWebView2.ExecuteScriptAsync(
                "Array.from(document.querySelectorAll('#t .chart a')).map(a=>a.getAttribute('href'))"))!;
        }

        Task<string[]> CheckCharts(Form script, string manifest, params int[] pagesPerDevice)
            => CheckChartFiles(script, manifest, pagesPerDevice.SelectMany((pages, index) =>
                Enumerable.Range(1, pages).Select(page => (Device: index + 1, File: page))).ToArray());

        async Task<string[]> CheckChartFiles(Form script, string manifest, params (int Device, int File)[] charts)
        {
            string id = RunId(manifest);
            string[] expected = charts.Select(chart => Path.Combine(folder, $"Rplot-{id}-{chart.Device}-{chart.File:000}.svg")).ToArray();
            var browser = (WebView2)Field(Pane(script), "browser");
            string[][] captions = JsonSerializer.Deserialize<string[][]>(await browser.CoreWebView2.ExecuteScriptAsync(
                "Array.from(document.querySelectorAll('#t .chart a')).map(a=>[a.textContent,a.getAttribute('href')])"))!;
            Check(expected.All(File.Exists) && captions.Length == expected.Length
                && captions.Select(a => a[0]).SequenceEqual(expected.Select(f => "Chart: " + Path.GetFileName(f)))
                && captions.Select(a => a[1]).SequenceEqual(expected.Select(f => new Uri(f).AbsoluteUri)),
                "the script pane captions and links contain exactly its own automatic SVG pages");
            foreach (string path in expected) CheckSvg(path);
            string[] links = JsonSerializer.Deserialize<string[]>(await browser.CoreWebView2.ExecuteScriptAsync(
                "Array.from(document.querySelectorAll('#t a')).map(a=>a.getAttribute('href'))"))!;
            Check(!links.Any(link => link.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                    && Regex.IsMatch(Path.GetFileName(new Uri(link).LocalPath), @"^(continue|script)-[0-9a-f]{32}\.r$", RegexOptions.IgnoreCase)),
                "the script pane does not link to controller temporary source files");
            return expected;
        }

        try
        {
            string initial = Start(first, "initial", "plot(1:3, main = \"First page\")\ncat(\"SHELL_TEST_SIZE:\", paste(dev.size(\"in\"), collapse = \"x\"), \"\\n\")\nplot(3:1, main = \"Second page\")\ndev.off()\nplot(c(2, 4, 3), main = \"Second default device\")\n");
            await Completed(first);
            string[] initialFiles = await CheckCharts(first, initial, 2, 1);
            Check(Pane(first).Text.Contains("SHELL_TEST_SIZE: 7x7"), "ordinary Windows R plots use the SVG device's default 7 by 7 inch size");
            var initialState = initialFiles.ToDictionary(f => f, f => (Bytes: File.ReadAllBytes(f), Modified: File.GetLastWriteTimeUtc(f)));

            string rerun = Start(first, "rerun", "plot(c(2, 4, 1), main = \"Later run\")\n");
            await Completed(first);
            string[] laterFiles = await CheckCharts(first, rerun, 1);
            Check(!initialFiles.Intersect(laterFiles, StringComparer.OrdinalIgnoreCase).Any()
                && initialState.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified),
                "rerunning a script creates new chart files and preserves every earlier page's bytes and timestamp");

            // Hold the first process after it has drawn a page. The second must
            // finish while the first still owns an open SVG device, then release it.
            string slow = Start(first, "slow", "plot(1:4, main = \"Slow first page\")\nwriteLines(\"ready\", " + RString(ready)
                + ")\nwhile (!file.exists(" + RString(release) + ")) Sys.sleep(0.05)\nplot(4:1, main = \"Slow second page\")\n");
            DateTime readyUntil = DateTime.UtcNow.AddSeconds(60);
            while (!File.Exists(ready) && (bool)Field(first, "runningR") && DateTime.UtcNow < readyUntil) await Task.Delay(50);
            Check(File.Exists(ready) && (bool)Field(first, "runningR"), "the first concurrent R run waits with its chart device open");
            string fast = Start(second, "fast", "plot(c(4, 1, 3), main = \"Fast independent run\")\n");
            await Completed(second);
            string[] fastFiles = await CheckCharts(second, fast, 1);
            var fastState = fastFiles.ToDictionary(f => f, f => (Bytes: File.ReadAllBytes(f), Modified: File.GetLastWriteTimeUtc(f)));
            Check((bool)Field(first, "runningR"), "the second concurrent R run finishes before the first is released");
            File.WriteAllText(release, "go");
            await Completed(first);
            string[] slowFiles = await CheckCharts(first, slow, 2);
            Check(!fastFiles.Intersect(slowFiles, StringComparer.OrdinalIgnoreCase).Any()
                && fastState.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified)
                && initialState.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified),
                "concurrent runs retain separate chart files and leave completed and earlier runs unchanged");

            // The public runner is also used without a script window. Omitting the
            // optional UI ownership argument must still allocate independent runs.
            var directScripts = Enumerable.Range(1, 2).Select(i => Prepare("direct-" + i, "plot(c(1, " + (i + 1) + ", 3))\n")).ToArray();
            RVersion version = RController.PreferredRVersion();
            string[] directOutput = await Task.WhenAll(directScripts.Select(p => Task.Run(() =>
                RController.RunScriptCapturingOutput(p.Text, version, CancellationToken.None, false))));
            string[] directIds = directScripts.Select(p => RunId(p.Manifest)).ToArray();
            Check(directIds.Distinct().Count() == 2 && directIds.All(id => File.Exists(Path.Combine(folder, "Rplot-" + id + "-1-001.svg")))
                && directOutput.All(output => output.Contains("SHELL_TEST_DEVICE:") && !output.Contains("(R stopped with exit code")),
                "the public R runner allocates distinct automatic chart files without a window-supplied run identifier");
            foreach (string id in directIds) CheckSvg(Path.Combine(folder, "Rplot-" + id + "-1-001.svg"));

            string failed = Start(first, "error", "plot(1:3, main = \"Page before error\")\nplot(3:1, main = \"Another page before error\")\nstop(\"expected failure after plotting\")\n");
            await Completed(first, expectedError: true);
            await CheckCharts(first, failed, 2);
            Check(Pane(first).Text.Contains("expected failure after plotting"), "the failing script keeps its R error alongside its flushed SVG charts");

            string split = Start(first, "separate-page-files", "dev.new(onefile = FALSE, width = 5, height = 3)\ncat(\"SHELL_TEST_SIZE:\", paste(dev.size(\"in\"), collapse = \"x\"), \"\\n\")\nplot(1:3, main = \"First separate page\")\nplot(3:1, main = \"Second separate page\")\n");
            await Completed(first);
            await CheckChartFiles(first, split, (1, 1), (1, 2));
            Check(Pane(first).Text.Contains("SHELL_TEST_SIZE: 5x3"), "dev.new forwards explicit SVG width, height and separate-page choices");

            string optionsStem = "shell-options-" + Guid.NewGuid().ToString("N");
            string[] optionPdfs = Enumerable.Range(1, 2).Select(page => Path.Combine(folder, optionsStem + $"-{page:000}.pdf")).ToArray();
            synthetic.AddRange(optionPdfs);
            string options = Start(first, "pdf-options", "pdf.options(width = 6, height = 4, onefile = FALSE)\npdf(file = " + RString(Path.Combine(folder, optionsStem + "-%03d.pdf")) + ")\nplot(1:3, main = \"Options first page\")\ncat(\"SHELL_TEST_SIZE:\", paste(dev.size(\"in\"), collapse = \"x\"), \"\\n\")\nplot(3:1, main = \"Options second page\")\n");
            await Completed(first);
            string optionsId = RunId(options);
            foreach (string path in optionPdfs) await CheckPdf(path, 1);
            Check(Pane(first).Text.Contains("SHELL_TEST_SIZE: 6x4")
                && !Directory.GetFiles(folder, "Rplot-" + optionsId + "-*.svg").Any()
                && (await ChartLinks(first)).SequenceEqual(optionPdfs.Select(path => new Uri(path).AbsoluteUri)),
                "an explicit PDF device retains pdf.options dimensions and separate-page defaults with its chosen filenames");
            foreach (string path in optionPdfs) File.Delete(path);

            string nullFile = Start(first, "null-file", "pdf(file = NULL, width = 5, height = 3)\nplot(1:3, main = \"Deliberately no output file\")\ndev.off()\n");
            await Completed(first);
            string nullId = RunId(nullFile);
            Check(!Directory.GetFiles(folder, "Rplot-" + nullId + "-*.svg").Any() && (await ChartLinks(first)).Length == 0,
                "an explicit pdf(file = NULL) deliberately creates no automatic chart file or link");

            string chosenPdf = Path.Combine(folder, "shell-chosen-" + Guid.NewGuid().ToString("N") + ".pdf");
            synthetic.Add(chosenPdf);
            string chosen = Start(first, "chosen-file", "pdf(file = " + RString(chosenPdf) + ", width = 6, height = 4)\nplot(1:3, main = \"Named PDF first page\")\nplot(3:1, main = \"Named PDF second page\")\n");
            await Completed(first);
            string chosenId = RunId(chosen);
            await CheckPdf(chosenPdf, 2);
            Check(!Directory.GetFiles(folder, "Rplot-" + chosenId + "-*.svg").Any()
                && (await ChartLinks(first)).SequenceEqual(new[] { new Uri(chosenPdf).AbsoluteUri }),
                "an explicit PDF retains its chosen filename, multiple pages and original file link");
            File.Delete(chosenPdf);

            string explicitSvg = Path.Combine(folder, "shell-explicit-" + Guid.NewGuid().ToString("N") + ".svg");
            synthetic.Add(explicitSvg);
            string explicitManifest = Start(first, "explicit-svg", "dev.new(filename = " + RString(explicitSvg) + ", width = 5, height = 3)\nplot(1:3, main = \"Explicit SVG still works\")\ncat(\"SHELL_TEST_SIZE:\", paste(dev.size(\"in\"), collapse = \"x\"), \"\\n\")\ndev.off()\n");
            await Completed(first);
            string explicitId = RunId(explicitManifest);
            string[] explicitLinks = await ChartLinks(first);
            Check(File.ReadAllText(explicitSvg).Contains("<svg") && explicitLinks.SequenceEqual(new[] { new Uri(explicitSvg).AbsoluteUri })
                && Pane(first).Text.Contains("SHELL_TEST_SIZE: 5x3")
                && !Directory.GetFiles(folder, "Rplot-" + explicitId + "-*.svg").Any(),
                "dev.new forwards an explicit SVG filename and dimensions without creating another automatic chart");

            // Ownership must take priority over clocks: a foreign automatic chart
            // with a future timestamp is excluded, while this run's old chart stays.
            Guid owned = Guid.NewGuid(), foreign = Guid.NewGuid();
            string Own(string name, DateTime modified)
            {
                string path = Path.Combine(folder, name);
                synthetic.Add(path);
                File.WriteAllText(path, "test-owned discovery fixture");
                File.SetLastWriteTimeUtc(path, modified);
                return path;
            }
            DateTime now = DateTime.UtcNow;
            string ownedChart = Own($"Rplot-{owned:N}-9-001.pdf", now.AddDays(-3));
            string foreignChart = Own($"Rplot-{foreign:N}-9-001.pdf", now.AddDays(1));
            string ownedSvg = Own($"Rplot-{owned:N}-9-001.svg", now.AddDays(-3));
            string foreignSvg = Own($"Rplot-{foreign:N}-9-001.svg", now.AddDays(1));
            string foreignHead = Own($"continue-{foreign:N}.r", now.AddDays(1));
            string foreignSource = Own($"script-{foreign:N}.r", now.AddDays(1));
            string explicitChart = Own($"Rplot-results-{owned:N}.svg", now);
            string explicitScript = Own($"script-results-{owned:N}.r", now);
            var discovery = first.GetType().GetMethod("FilesWritten", BindingFlags.Static | BindingFlags.NonPublic)!;
            var found = ((IEnumerable<FileInfo>)discovery.Invoke(null, new object[] { DateTime.Now, owned })!).Select(f => f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Check(found.Contains(ownedChart) && !found.Contains(foreignChart) && found.Contains(ownedSvg) && !found.Contains(foreignSvg)
                && !found.Contains(foreignHead) && !found.Contains(foreignSource)
                && found.Contains(explicitChart) && found.Contains(explicitScript),
                "chart ownership overrides timestamps, temporary scripts stay hidden, and ordinary similarly named outputs remain discoverable");
            Check(legacy.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified),
                "all pre-existing Rplot PDF and SVG files retain their original bytes and timestamps throughout the chart runs");
        }
        finally
        {
            File.WriteAllText(release, "go");
            foreach (Form script in new[] { first, second })
            {
                if ((bool)Field(script, "runningR"))
                {
                    try { ((CancellationTokenSource)Field(script, "rRun")).Cancel(); } catch (ObjectDisposedException) { }
                    DateTime until = DateTime.UtcNow.AddSeconds(10);
                    while ((bool)Field(script, "runningR") && DateTime.UtcNow < until) await Task.Delay(50);
                }
            }
            foreach (string manifest in manifests.Where(File.Exists))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(manifest), @"Rplot-([0-9a-f]{32})-", RegexOptions.IgnoreCase))
                    foreach (string file in Directory.GetFiles(folder, "Rplot-" + match.Groups[1].Value + "-*"))
                        if (new[] { ".svg", ".pdf" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) File.Delete(file);
            }
            foreach (string file in synthetic) File.Delete(file);
            // Preserve user-owned legacy files even if this test is intentionally
            // run against an older executable to demonstrate the regression.
            foreach (var p in legacy)
                if (!File.Exists(p.Key) || !File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) || File.GetLastWriteTimeUtc(p.Key) != p.Value.Modified)
                {
                    File.WriteAllBytes(p.Key, p.Value.Bytes);
                    File.SetLastWriteTimeUtc(p.Key, p.Value.Modified);
                }
            first.Dispose(); second.Dispose();
            foreach (string file in Directory.GetFiles(scratch)) File.Delete(file);
            Directory.Delete(scratch);
            grid.Activate();
            await Task.Delay(150);
        }
    }
}

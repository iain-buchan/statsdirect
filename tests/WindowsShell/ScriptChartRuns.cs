using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using StatsDirect.Configuration;
using StatsDirect.R;
using StatsDirect.UI;

internal static partial class Program
{
    // Retain chart ownership, concurrent execution, device options and named-file
    // checks through the real persistent windows; the public batch API remains covered.
    static async Task CheckScriptChartRuns(frmMain main, Form grid)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "StatsDirect-chart-runs-" + Guid.NewGuid().ToString("N"));
        string folder = SDConfiguration.MyStatsDirectRFolder;
        Directory.CreateDirectory(folder); Directory.CreateDirectory(scratch);
        string release = Path.Combine(scratch, "release-slow.txt"), ready = Path.Combine(scratch, "slow-ready.txt");
        var manifests = new List<string>(); var synthetic = new List<string>();
        var legacy = Directory.GetFiles(folder, "Rplot*").Where(f => new[] { ".svg", ".pdf" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToDictionary(f => f, f => (Bytes: File.ReadAllBytes(f), Modified: File.GetLastWriteTimeUtc(f)));
        using var first = (Form)Call(main, "CreateScriptWindow");
        using var second = (Form)Call(main, "CreateScriptWindow");
        string Q(string value) => JsonSerializer.Serialize(value.Replace('\\', '/'));
        string Output(Form script) => (string)Get(RConsole(script), "Transcript");
        async Task<int> Start(Form script, string stem, string source)
        {
            await (Task)Call(RConsole(script), "ClearOutputAsync");
            int before = RFinished(script);
            Call(script, "ShowR", source, "Chart run " + stem, null, true);
            return before;
        }
        void Svg(string path)
        {
            var svg = System.Xml.Linq.XDocument.Load(path).Root;
            Check(svg?.Name == System.Xml.Linq.XName.Get("svg", "http://www.w3.org/2000/svg") && svg.Descendants().Any(), "the chart is a complete, readable SVG document");
        }
        async Task Pdf(string path, int pages)
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            var pdf = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
            Check(pdf.PageCount == pages, "the explicitly selected PDF is readable and retains all " + pages + " chart page(s)");
        }
        string[] Automatic(Form script, int count)
        {
            var plots = RPlots(script);
            string[] paths = plots.Select(p => p.GetProperty("path").GetString()!).ToArray();
            Check(plots.Length == count && plots.All(p => p.GetProperty("mime").GetString() == "image/svg+xml" && !p.TryGetProperty("originalPath", out _))
                && paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == count && paths.All(File.Exists),
                "the R window retains exactly its own " + count + " automatic SVG page(s) as separate immutable assets");
            foreach (string path in paths) Svg(path);
            Check(!REvents(script).Where(e => e.GetProperty("type").GetString() == "notice" && e.TryGetProperty("filePath", out _))
                .Any(e => Regex.IsMatch(Path.GetFileName(e.GetProperty("filePath").GetString()), @"^(continue|script)-[0-9a-f]{32}\.r$", RegexOptions.IgnoreCase)),
                "the console does not expose controller temporary source files as written outputs");
            return paths;
        }
        void Named(Form script, string[] paths, string mime)
        {
            var plots = RPlots(script);
            Check(plots.Length == paths.Length && plots.All(p => p.GetProperty("mime").GetString() == mime && p.TryGetProperty("originalPath", out _))
                && plots.Select(p => p.GetProperty("originalPath").GetString()).SequenceEqual(paths, StringComparer.OrdinalIgnoreCase)
                && plots.All(p => File.Exists(p.GetProperty("path").GetString()!)),
                "explicit charts retain chosen filenames and one immutable card per file without an extra automatic chart");
        }
        try
        {
            await PrepareRConsole(first, Path.Combine(scratch, "first"));
            await PrepareRConsole(second, Path.Combine(scratch, "second"));
            string firstFolder = (string)Get(RConsole(first), "WorkingDirectory");
            int initial = await Start(first, "initial", "plot(1:3,main='First page')\ncat('SHELL_TEST_SIZE:',paste(dev.size('in'),collapse='x'),'\\n')\nplot(3:1,main='Second page')\ndev.off()\nplot(c(2,4,3),main='Second default device')\n");
            await CompleteRCommand(first, initial);
            string[] initialFiles = Automatic(first, 3);
            Check(Output(first).Contains("SHELL_TEST_SIZE: 7x7"), "ordinary Windows R plots use the SVG device's default 7 by 7 inch size");
            var initialState = initialFiles.ToDictionary(f => f, f => (Bytes: File.ReadAllBytes(f), Modified: File.GetLastWriteTimeUtc(f)));
            int rerun = await Start(first, "rerun", "plot(c(2,4,1),main='Later run')\n");
            await CompleteRCommand(first, rerun);
            string[] laterFiles = Automatic(first, 1);
            Check(!initialFiles.Intersect(laterFiles, StringComparer.OrdinalIgnoreCase).Any()
                && initialState.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified),
                "a later submission creates new assets and preserves every earlier page's bytes and timestamp");

            int slow = await Start(first, "slow", "plot(1:4,main='Slow first page')\nwriteLines('ready'," + Q(ready) + ")\nwhile(!file.exists(" + Q(release) + "))Sys.sleep(.05)\nplot(4:1,main='Slow second page')\n");
            var until = DateTime.UtcNow.AddSeconds(60);
            while (!File.Exists(ready) && DateTime.UtcNow < until) await Task.Delay(30);
            Check(File.Exists(ready) && (bool)Get(first, "LearningRunning"), "the first concurrent R window waits with its chart device open");
            int fast = await Start(second, "fast", "plot(c(4,1,3),main='Fast independent run')\n");
            await CompleteRCommand(second, fast);
            string[] fastFiles = Automatic(second, 1);
            var fastState = fastFiles.ToDictionary(f => f, f => (Bytes: File.ReadAllBytes(f), Modified: File.GetLastWriteTimeUtc(f)));
            Check((bool)Get(first, "LearningRunning"), "the second R window completes while the first still waits");
            File.WriteAllText(release, "go"); await CompleteRCommand(first, slow);
            string[] slowFiles = Automatic(first, 2);
            Check(!fastFiles.Intersect(slowFiles, StringComparer.OrdinalIgnoreCase).Any()
                && fastState.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified)
                && initialState.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified),
                "concurrent R windows retain separate assets and leave completed and earlier pages unchanged");

            var directScripts = Enumerable.Range(1, 2).Select(i =>
            {
                string manifest = Path.Combine(scratch, "direct-" + i + ".txt"); manifests.Add(manifest);
                return "writeLines(paste(deparse(getOption('device')),collapse=' ')," + Q(manifest) + ")\ncat('SHELL_TEST_DEVICE\\n')\nplot(c(1," + (i + 1) + ",3))\n";
            }).ToArray();
            var version = RController.PreferredRVersion();
            string[] directOutput = await Task.WhenAll(directScripts.Select(source => Task.Run(() => RController.RunScriptCapturingOutput(source, version, CancellationToken.None, false))));
            string[] ids = manifests.Select(path => Regex.Match(File.ReadAllText(path), @"Rplot-([0-9a-f]{32})-", RegexOptions.IgnoreCase).Groups[1].Value).ToArray();
            Check(ids.All(id => id.Length == 32) && ids.Distinct().Count() == 2 && ids.All(id => File.Exists(Path.Combine(folder, "Rplot-" + id + "-1-001.svg")))
                && directOutput.All(output => output.Contains("SHELL_TEST_DEVICE") && !output.Contains("(R stopped with exit code")),
                "the public batch API retains independent SVG files without a window-supplied identity");
            foreach (string id in ids) Svg(Path.Combine(folder, "Rplot-" + id + "-1-001.svg"));

            int failed = await Start(first, "error", "plot(1:3,main='Page before error')\nplot(3:1,main='Another page before error')\nstop('expected failure after plotting')\n");
            await CompleteRCommand(first, failed, expectedError: true); Automatic(first, 2);
            Check(Output(first).Contains("expected failure after plotting"), "a failing script keeps its R error alongside its completed SVG pages");
            int split = await Start(first, "separate-pages", "dev.new(onefile=FALSE,width=5,height=3)\ncat('SHELL_TEST_SIZE:',paste(dev.size('in'),collapse='x'),'\\n')\nplot(1:3,main='First separate page')\nplot(3:1,main='Second separate page')\n");
            await CompleteRCommand(first, split); string[] splitFiles = Automatic(first, 2);
            Check(Output(first).Contains("SHELL_TEST_SIZE: 5x3") && splitFiles.All(path => System.Xml.Linq.XDocument.Load(path).Root!.Attribute("viewBox")?.Value == "0 0 360 216"),
                "dev.new forwards explicit SVG dimensions and separate-page choices to captured pages");

            string optionsStem = Path.Combine(firstFolder, "shell-options-%03d.pdf");
            string[] optionsFiles = Enumerable.Range(1, 2).Select(page => optionsStem.Replace("%03d", page.ToString("000"))).ToArray();
            int options = await Start(first, "pdf-options", "pdf.options(width=6,height=4,onefile=FALSE)\npdf(file=" + Q(optionsStem) + ")\nplot(1:3,main='Options first page')\ncat('SHELL_TEST_SIZE:',paste(dev.size('in'),collapse='x'),'\\n')\nplot(3:1,main='Options second page')\ndev.off()\n");
            await CompleteRCommand(first, options);
            foreach (string path in optionsFiles) await Pdf(path, 1);
            Named(first, optionsFiles, "application/pdf");
            Check(Output(first).Contains("SHELL_TEST_SIZE: 6x4"), "explicit PDF retains pdf.options dimensions and one-file-per-page defaults");
            int nullFile = await Start(first, "null-file", "pdf(file=NULL,width=5,height=3,onefile=TRUE)\nplot(1:3,main='Deliberately no output file')\ndev.off()\n");
            await CompleteRCommand(first, nullFile);
            Check(RPlots(first).Length == 0, "pdf(file=NULL) creates no automatic preview or chart card");
            string chosenPdf = Path.Combine(firstFolder, "shell-chosen.pdf");
            int chosen = await Start(first, "chosen-file", "pdf(file=" + Q(chosenPdf) + ",width=6,height=4,onefile=TRUE)\nplot(1:3,main='Named first page')\nplot(3:1,main='Named second page')\ndev.off()\n");
            await CompleteRCommand(first, chosen); await Pdf(chosenPdf, 2); Named(first, new[] { chosenPdf }, "application/pdf");
            string chosenSvg = Path.Combine(firstFolder, "shell-explicit.svg");
            int explicitFile = await Start(first, "explicit-svg", "dev.new(filename=" + Q(chosenSvg) + ",width=5,height=3)\nplot(1:3,main='Explicit SVG')\ncat('SHELL_TEST_SIZE:',paste(dev.size('in'),collapse='x'),'\\n')\ndev.off()\n");
            await CompleteRCommand(first, explicitFile); Svg(chosenSvg); Named(first, new[] { chosenSvg }, "image/svg+xml");
            Check(Output(first).Contains("SHELL_TEST_SIZE: 5x3"), "dev.new preserves an explicit SVG filename and dimensions");

            // Retain clock-independent ownership tests for the unchanged batch discovery API.
            Guid owned = Guid.NewGuid(), foreign = Guid.NewGuid();
            string Own(string name, DateTime modified)
            {
                string path = Path.Combine(folder, name); synthetic.Add(path);
                File.WriteAllText(path, "test-owned discovery fixture"); File.SetLastWriteTimeUtc(path, modified); return path;
            }
            DateTime now = DateTime.UtcNow;
            string ownedChart = Own($"Rplot-{owned:N}-9-001.pdf", now.AddDays(-3)), foreignChart = Own($"Rplot-{foreign:N}-9-001.pdf", now.AddDays(1));
            string ownedSvg = Own($"Rplot-{owned:N}-9-001.svg", now.AddDays(-3)), foreignSvg = Own($"Rplot-{foreign:N}-9-001.svg", now.AddDays(1));
            string foreignHead = Own($"continue-{foreign:N}.r", now.AddDays(1)), foreignSource = Own($"script-{foreign:N}.r", now.AddDays(1));
            string namedChart = Own($"Rplot-results-{owned:N}.svg", now), namedScript = Own($"script-results-{owned:N}.r", now);
            var discovery = first.GetType().GetMethod("FilesWritten", BindingFlags.Static | BindingFlags.NonPublic)!;
            var found = ((IEnumerable<FileInfo>)discovery.Invoke(null, new object[] { DateTime.Now, owned })!).Select(f => f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Check(found.Contains(ownedChart) && !found.Contains(foreignChart) && found.Contains(ownedSvg) && !found.Contains(foreignSvg)
                && !found.Contains(foreignHead) && !found.Contains(foreignSource) && found.Contains(namedChart) && found.Contains(namedScript),
                "batch ownership overrides clocks, hides temporary scripts and retains similarly named ordinary files");
            Check(legacy.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value.Bytes) && File.GetLastWriteTimeUtc(p.Key) == p.Value.Modified),
                "pre-existing Rplot PDF and SVG files retain original bytes and timestamps");
        }
        finally
        {
            File.WriteAllText(release, "go"); first.Dispose(); second.Dispose();
            foreach (string manifest in manifests.Where(File.Exists))
                foreach (Match match in Regex.Matches(File.ReadAllText(manifest), @"Rplot-([0-9a-f]{32})-", RegexOptions.IgnoreCase))
                    foreach (string file in Directory.GetFiles(folder, "Rplot-" + match.Groups[1].Value + "-*"))
                        if (new[] { ".svg", ".pdf" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) File.Delete(file);
            foreach (string file in synthetic) File.Delete(file);
            foreach (var pair in legacy)
                if (!File.Exists(pair.Key) || !File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value.Bytes) || File.GetLastWriteTimeUtc(pair.Key) != pair.Value.Modified)
                { File.WriteAllBytes(pair.Key, pair.Value.Bytes); File.SetLastWriteTimeUtc(pair.Key, pair.Value.Modified); }
            // Only this fixture's uniquely created scratch directory is removed.
            await DeleteRFixtureDirectory(scratch, "StatsDirect-chart-runs-");
            grid.Activate(); await Task.Delay(150);
        }
    }
}

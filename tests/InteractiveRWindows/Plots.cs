using StatsDirect.UI;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class Program
{
    static async Task CheckPlots(frmMain main, ConsoleHost host)
    {
        var first = (await host.Run("plot(1:3); cat('FIRST_PLOT\\n')", "FIRST_PLOT")).Single(e => Kind(e) == "plot");
        string firstPath = Text(first, "path"), firstSvg = File.ReadAllText(firstPath);
        var changed = (await host.Run("abline(h=2,col='red'); cat('UPDATED_PLOT\\n')", "UPDATED_PLOT")).Single(e => Kind(e) == "plot");
        Check(Text(first, "plotId") == Text(changed, "plotId") && Number(changed, "revision") > Number(first, "revision")
            && Text(changed, "path") != firstPath && File.ReadAllText(firstPath) == firstSvg,
            "a later abline updates the live plot as a new immutable revision without overwriting the original SVG");
        var originalResolved = (JsonElement)Call(host.View, "FindPlot", Text(first, "plotId"), (long?)Number(first, "revision"))!;
        Check(Text(originalResolved, "path") == firstPath, "native chart actions resolve an earlier plot revision to its original SVG file");
        var metadata = (Dictionary<string, JsonElement>)Get(host.View, "plots")!;
        Check(metadata.Values.All(e => !e.TryGetProperty("preview", out _)),
            "the native plot revision map retains lightweight metadata rather than unbounded base64 previews");
        Check((await host.Run("cat('NO_NEW_CHART=',saved,'\\n',sep='')", "NO_NEW_CHART=42")).All(e => Kind(e) != "plot"),
            "a non-graphical command does not duplicate the previous chart");

        // Continue in R submits the complete CRLF source once, rather than publishing each parsed expression's partial plot.
        var recipe = await NewConsole(main);
        await recipe.Start(); RememberWorker(recipe);
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TPaired.R"));
        int mark = recipe.Mark;
        Call(recipe.Document, "ShowR", source, "Paired t test", "1239", true);
        await Wait(() => Get(recipe.View, "Session") != null, "Continue in R did not create a real R session.");
        await recipe.Finished(mark); RememberWorker(recipe);
        var events = recipe.Since(mark);
        var plots = events.Where(e => Kind(e) == "plot").ToArray();
        Check(events.Count(e => Kind(e) == "commandAccepted") == 1 && events.Count(e => Kind(e) == "commandFinished") == 1,
            "Continue in R submits and finishes the complete source exactly once");
        int chartAt = Array.FindIndex(events, e => Kind(e) == "plot");
        int outputAt = Array.FindLastIndex(events, e => Kind(e) == "output");
        Check(plots.Length == 1 && chartAt > outputAt && recipe.Output(mark).Contains("Mean of differences =", StringComparison.Ordinal)
            && recipe.Output(mark).Contains("95% Limits of agreement =", StringComparison.Ordinal),
            "the complete paired-t recipe produces one final agreement chart after its numerical output");
        await WaitImages(recipe, 1);
        Check(await recipe.JS("document.images.length===1&&document.images[0].complete&&document.images[0].naturalWidth>0") == "true",
            "Continue in R displays exactly one decoded SVG chart in the actual script window");
        await recipe.TwoFrames();
        Check(await recipe.JS("(()=>{const p=document.getElementById('transcript');return p.scrollHeight-p.scrollTop-p.clientHeight<=2})()") == "true",
            "Continue in R remains at the bottom after its agreement chart decodes and the final input row is laid out");
        await CompareOrdinaryR(recipe, source, plots, "paired-agreement");
        Check(true, "the Continue-in-R SVG exactly matches the final ordinary-R drawing, including caption and mean/limit lines");
        Check(await recipe.JS("document.getElementById('help').disabled===false&&document.getElementById('ai').disabled===false") == "true",
            "method help and existing AI assistance remain available in the integrated R window");

        foreach (var fixture in new[]
        {
            (Name: "two-pages", Source: "plot(1:3,main='first page');plot(3:1,main='second page')", Pages: 2),
            (Name: "base-multipanel", Source: "par(mfrow=c(1,2));plot(1:3,main='left panel');plot(3:1,main='right panel')", Pages: 1),
            (Name: "grid-multipanel", Source: "grid::grid.newpage();grid::pushViewport(grid::viewport(layout=grid::grid.layout(1,2)));grid::grid.rect(gp=grid::gpar(fill='red'),vp=grid::viewport(layout.pos.col=1));grid::grid.rect(gp=grid::gpar(fill='blue'),vp=grid::viewport(layout.pos.col=2))", Pages: 1),
            (Name: "dev-new-size", Source: "dev.new(width=9,height=4);plot(1:3,main='wide chart')", Pages: 1)
        })
        {
            var other = await NewConsole(main); await other.Start(); RememberWorker(other);
            var pages = (await other.Run(fixture.Source, origin: "script")).Where(e => Kind(e) == "plot").ToArray();
            Check(pages.Length == fixture.Pages, fixture.Name + " preserves the intended completed page count");
            await WaitImages(other, fixture.Pages);
            if (fixture.Name == "dev-new-size")
            {
                var svg = XDocument.Load(Text(pages[0], "path")).Root!;
                var box = svg.Attribute("viewBox")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Check(Math.Abs(box[2] / box[3] - 9.0 / 4) < .001, "dev.new width and height survive display-list snapshot rendering");
            }
            else
            {
                await CompareOrdinaryR(other, fixture.Source, pages, fixture.Name);
                Check(true, fixture.Name + " matches ordinary R complete page rendering");
            }
        }

        var files = await NewConsole(main); await files.Start(); RememberWorker(files);
        const string fileSource = "svg('explicit.svg',width=5,height=3);plot(1:3,col='blue');dev.off();png('explicit.png',width=400,height=240);plot(3:1,col='red');dev.off();pdf('explicit.pdf',width=5,height=3);plot(c(2,1,3));dev.off();writeLines('owned test output','explicit.txt');cat('EXPLICIT_FILES_DONE\\n')";
        var fileEvents = await files.Run(fileSource, "EXPLICIT_FILES_DONE", "script");
        string folder = (string)Get(files.View, "WorkingDirectory")!;
        Check(new[] { "explicit.svg", "explicit.png", "explicit.pdf", "explicit.txt" }.All(name => File.Exists(Path.Combine(folder, name))),
            "explicit SVG, PNG, PDF and text outputs remain genuine R files");
        await files.TwoFrames();
        var links = JsonSerializer.Deserialize<string[]>(await files.JS("[...document.querySelectorAll('p.notice a[href]')].map(a=>a.href)"))!;
        Check(links.Contains(new Uri(Path.Combine(folder, "explicit.txt")).AbsoluteUri, StringComparer.Ordinal),
            "an explicit text file has an actual clickable file link in the integrated console output");
        var filePlots = fileEvents.Where(e => Kind(e) == "plot").ToArray();
        Check(new[] { "explicit.svg", "explicit.png", "explicit.pdf" }.All(name =>
        {
            var plot = filePlots.Single(e => Text(e, "label") == name);
            return File.ReadAllBytes(Text(plot, "path")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, name)));
        }), "explicit SVG, PNG and PDF chart actions retain byte-exact immutable copies of the original files");
        Check(await files.JS("(()=>{const figures=[...document.querySelectorAll('figure.plot')];return ['explicit.svg','explicit.png','explicit.pdf'].every(name=>figures.some(f=>f.textContent.includes(name)&&[...f.querySelectorAll('button')].some(b=>b.textContent.startsWith('Open'))))})()") == "true",
            "explicit SVG, PNG and PDF charts each have a visible native Open action");
        await CheckLargeOpen(files);

        var cleared = await NewConsole(main); await cleared.Start(); RememberWorker(cleared);
        const string clearSource = "eraseMe <- 123;rm(list=ls(all.names=TRUE));plot(1:3);cat('R_CLEAR_SAFE=',!exists('eraseMe'),'\\n',sep='')";
        var clearPlots = (await cleared.Run(clearSource, "R_CLEAR_SAFE=TRUE", "script")).Where(e => Kind(e) == "plot").ToArray();
        Check(clearPlots.Length == 1 && Text(clearPlots[0], "mime") == "image/svg+xml",
            "clearing every global R name leaves automatic SVG chart capture working");
        await CompareOrdinaryR(cleared, clearSource, clearPlots, "cleared-workspace");
        var afterClearPlot = (await cleared.Run("abline(h=2,col='red')")).Single(e => Kind(e) == "plot");
        Check(Text(afterClearPlot, "plotId") == Text(clearPlots[0], "plotId") && Number(afterClearPlot, "revision") > Number(clearPlots[0], "revision"),
            "a chart drawn after clearing R globals still supports later immutable plot revisions");
        host.Document.Activate();
    }
    static async Task CheckLargeOpen(ConsoleHost host)
    {
        const string source = "writeLines(c('<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"80\" height=\"40\"><!--',strrep('x',21L*1024L*1024L),'--><rect width=\"80\" height=\"40\" fill=\"red\"/></svg>'),'large-explicit.svg',useBytes=TRUE);cat('LARGE_OPEN_READY\\n')";
        var plot = (await host.Run(source, "LARGE_OPEN_READY", "script")).Single(e => Kind(e) == "plot" && Text(e, "label") == "large-explicit.svg");
        string path = Text(plot, "path");
        Check(new FileInfo(path).Length > 20 * 1024 * 1024 && (!plot.TryGetProperty("preview", out var preview) || preview.ValueKind == JsonValueKind.Null),
            "a large SVG remains an immutable file with an Open card instead of a base64 preview");
        await host.TwoFrames();
        Check(await host.JS("(()=>{const f=[...document.querySelectorAll('figure.plot')].find(f=>f.querySelector('figcaption').textContent==='large-explicit.svg');return !!f&&!f.querySelector('img')&&[...f.querySelectorAll('button')].some(b=>b.textContent.startsWith('Open'))})()") == "true",
            "a large SVG's visible card offers Open without trying to decode a preview");
        string? opened = null;
        var previous = Get(host.View, "PlotOpener");
        Set(host.View, "PlotOpener", (Action<string>)(value => opened = value));
        try
        {
            await host.JS("(()=>{const f=[...document.querySelectorAll('figure.plot')].find(f=>f.querySelector('figcaption').textContent==='large-explicit.svg');[...f.querySelectorAll('button')].find(b=>b.textContent.startsWith('Open')).click()})()");
            await Wait(() => opened != null, "The large SVG Open action did not reach its native file opener.");
            Check(opened == path, "Open uses the exact large SVG metadata path without requiring a preview or launching an external viewer in the fixture");
        }
        finally { Set(host.View, "PlotOpener", previous); }
    }
    static async Task WaitImages(ConsoleHost host, int minimum)
    {
        var watch = Stopwatch.StartNew();
        while (await host.JS($"document.images.length>={minimum}&&[...document.images].every(i=>i.complete&&i.naturalWidth>0)") != "true")
        {
            if (watch.ElapsedMilliseconds > 15000) throw new TimeoutException("The integrated console's SVG images did not decode.");
            await Task.Delay(40);
        }
    }
    static async Task CompareOrdinaryR(ConsoleHost host, string source, JsonElement[] plots, string name)
    {
        string directory = Path.Combine(artifacts, "ordinary-r", name); Directory.CreateDirectory(directory);
        string submitted = Path.Combine(directory, "submitted.R"); File.WriteAllText(submitted, source, new System.Text.UTF8Encoding(false));
        string Q(string value) => JsonSerializer.Serialize(value.Replace('\\', '/'));
        var start = new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("STATSDIRECT_RHOME") ?? @"C:\Program Files\R\R-4.6.1", "bin", "x64", "Rscript.exe"))
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("--vanilla"); start.ArgumentList.Add("--encoding=UTF-8"); start.ArgumentList.Add("-e");
        start.ArgumentList.Add($"grDevices::svg({Q(Path.Combine(directory, "reference-%03d.svg"))});source({Q(submitted)},encoding='UTF-8');grDevices::dev.off()");
        foreach (string key in start.Environment.Keys.Where(k => k.StartsWith("LC_", StringComparison.Ordinal)).ToArray()) start.Environment.Remove(key);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (TimeoutException) { process.Kill(entireProcessTree: true); throw; }
        if (process.ExitCode != 0) throw new InvalidOperationException("Independent ordinary R failed: " + await error + await output);
        var references = Directory.GetFiles(directory, "reference-*.svg").Order(StringComparer.Ordinal).ToArray();
        if (references.Length != plots.Length) throw new InvalidOperationException("Ordinary R and interactive console produced different completed page counts.");
        for (int index = 0; index < references.Length; index++)
            if (CanonicalSvg(File.ReadAllText(references[index])) != CanonicalSvg(File.ReadAllText(Text(plots[index], "path"))))
                throw new InvalidOperationException("The interactive console differs from final ordinary R SVG drawing: " + name + " page " + (index + 1));
    }
    static string CanonicalSvg(string text)
    {
        var document = XDocument.Parse(text);
        var ids = document.Descendants().Attributes("id").Select(a => a.Value).Distinct()
            .Select((id, index) => (id, token: "id" + index)).ToDictionary(p => p.id, p => p.token);
        foreach (var node in document.Descendants())
            node.ReplaceAttributes(node.Attributes().Select(attribute =>
            {
                string value = attribute.Value;
                if (attribute.Name.LocalName == "id" && ids.TryGetValue(value, out var token)) value = token;
                else if (value.StartsWith('#') && ids.TryGetValue(value[1..], out token)) value = "#" + token;
                else if (value.StartsWith("url(#") && value.EndsWith(')') && ids.TryGetValue(value[5..^1], out token)) value = "url(#" + token + ")";
                return new XAttribute(attribute.Name, value);
            }).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).ToArray());
        return document.Root!.ToString(SaveOptions.DisableFormatting);
    }
}

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using StatsDirect.R.Interactive;

// The newest 64-bit R installed under Program Files\R, as the console needs: a second argument names another.
string NewestRHome()
{
    string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "R");
    var homes = Directory.Exists(root) ? Directory.GetDirectories(root) : [];
    string? newest = homes.Where(home => File.Exists(Path.Combine(home, "bin", "x64", "R.dll")))
        .Select(home => (Home: home, Match: Regex.Match(Path.GetFileName(home), @"^R-(\d+(?:\.\d+){1,3})$")))
        .Where(found => found.Match.Success && Version.TryParse(found.Match.Groups[1].Value, out _))
        .OrderByDescending(found => Version.Parse(found.Match.Groups[1].Value)).Select(found => found.Home).FirstOrDefault();
    return newest ?? throw new DirectoryNotFoundException("No 64-bit R with bin\\x64\\R.dll was found under " + root + ". Name an R home as the second argument.");
}
string helper = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../bin/StatsDirect.RConsole.Native.exe"));
string rHome = args.Length > 1 ? Path.GetFullPath(args[1]) : NewestRHome();
string working = args.Length > 2 ? Path.GetFullPath(args[2]) : Path.Combine(Path.GetTempPath(), "StatsDirect-R-session-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(working);
using var session = new RConsoleSession(helper, rHome, working);
var events = new List<JsonElement>();
session.Event += e => { lock (events) events.Add(e.Clone()); };
int checks = 0;
void Check(bool yes, string message) { if (!yes) throw new Exception(message); Console.WriteLine("PASS " + message); checks++; }
JsonElement[] Since(int mark) { lock (events) return events.Skip(mark).ToArray(); }
int Mark() { lock (events) return events.Count; }
string Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
bool Is(JsonElement e, string type) => Text(e, "type") == type;
async Task<JsonElement> Wait(int mark, Func<JsonElement, bool> predicate)
{
    var until = DateTime.UtcNow.AddSeconds(30);
    while (DateTime.UtcNow < until)
    {
        foreach (var e in Since(mark)) if (predicate(e)) return e;
        await Task.Delay(20);
    }
    throw new TimeoutException(string.Join("\n", Since(mark).TakeLast(12).Select(e => e.GetRawText())));
}
async Task<JsonElement[]> Run(string source, string status = "ok", IRSession? on = null)
{
    int mark = Mark(); await (on ?? session).SubmitAsync(source, "script");
    var finished = await Wait(mark, e => Is(e, "commandFinished"));
    Check(Text(finished, "status") == status, "R submission finishes as " + status);
    await Wait(mark, e => Is(e, "state") && Text(e, "state") == "ready");
    return Since(mark);
}
string Output(JsonElement[] log) => string.Concat(log.Where(e => Is(e, "output")).Select(e => Text(e, "text")));
JsonElement[] Plots(JsonElement[] log) => log.Where(e => Is(e, "plot")).ToArray();
XElement Svg(JsonElement plot) => XDocument.Load(Text(plot, "path")).Root!;
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
int Handles() { using var me = Process.GetCurrentProcess(); return me.HandleCount; }
// The version R itself reports, as the host passes it to the Rscript session: the installed R's folder name may say less.
string RVersionOf(string rscriptPath)
{
    var query = new ProcessStartInfo(rscriptPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
    query.ArgumentList.Add("--vanilla"); query.ArgumentList.Add("-e"); query.ArgumentList.Add("cat(as.character(getRversion()))");
    using var asked = Process.Start(query) ?? throw new IOException("Rscript did not start: " + rscriptPath);
    string version = asked.StandardOutput.ReadToEnd().Trim();
    asked.WaitForExit();
    return Regex.IsMatch(version, @"^\d+\.\d+\.\d+$") ? version : throw new IOException("Rscript did not report its version: " + version);
}
try
{
    await session.StartAsync();
    var log = await Run("preserved <- 42; cat('INTERACTIVE=', interactive(), '\n', sep='')");
    Check(Output(log).Contains("INTERACTIVE=TRUE"), "the production adapter runs a genuinely interactive R workspace");
    log = await Run("cat('LIB_FIRST=', normalizePath(.libPaths()[1L])==normalizePath(file.path(getwd(),'Lib')), sep=''); cat('BASE_LIB=', normalizePath(.Library) %in% normalizePath(.libPaths()), sep=''); cat('LIB_PRIVATE=', !any(c('libdir','rlib','userdir') %in% ls(all.names=TRUE)), sep='')");
    Check(Output(log).Contains("LIB_FIRST=TRUE") && Output(log).Contains("LIB_PRIVATE=TRUE") && Directory.Exists(Path.Combine(working, "Lib")), "StatsDirect's existing Lib folder leads the package search path without adding global setup variables");
    Check(Output(log).Contains("BASE_LIB=TRUE"), "the standard R library remains on the package search path");
    log = await Run("dev.new(width=9, height=4.5); plot(1:3); abline(h=2, col='red')");
    var sized = Plots(log);
    Check(sized.Length == 1, "one completed script produces one final active-device chart");
    Check((string?)Svg(sized[0]).Attribute("width") == "648pt" && (string?)Svg(sized[0]).Attribute("height") == "324pt", "snapshot preserves the explicit 9 by 4.5 inch device dimensions");

    log = await Run("svg('manual.svg', width=5, height=3); plot(1:4, col='red'); dev.off()");
    var first = Plots(log).Single(p => Text(p, "label") == "manual.svg");
    string oldPath = Text(first, "path"), oldHash = Hash(oldPath);
    Check(Text(first, "mime") == "image/svg+xml" && Text(first, "preview").StartsWith("data:image/svg+xml;base64,"), "explicit SVG file device gets a vector preview");
    Check(oldPath != Text(first, "originalPath") && File.Exists(Text(first, "originalPath")), "the card owns an immutable copy while preserving the user's original file");
    Check((string?)Svg(first).Attribute("width") == "360pt" && (string?)Svg(first).Attribute("height") == "216pt", "explicit SVG file retains its original dimensions");
    log = await Run("svg('manual.svg', width=4, height=2); plot(1:5, col='blue'); dev.off()");
    var second = Plots(log).Single(p => Text(p, "label") == "manual.svg");
    Check(Text(second, "path") != oldPath && Hash(oldPath) == oldHash && Hash(Text(second, "path")) != oldHash, "overwriting a named chart never changes the earlier Open/Copy asset");

    log = await Run("png('manual.png', width=600, height=300); plot(1:4); dev.off()");
    var png = Plots(log).Single(p => Text(p, "label") == "manual.png");
    Check(Text(png, "mime") == "image/png" && Text(png, "preview").StartsWith("data:image/png;base64,"), "explicit PNG file gets a raster preview");
    Check(File.ReadAllBytes(Text(png, "path")).Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}), "PNG card preserves real PNG bytes");
    log = await Run("pdf('manual.pdf', width=6, height=3); plot(1:4); dev.off()");
    var pdf = Plots(log).Single(p => Text(p, "label") == "manual.pdf");
    Check(Text(pdf, "mime") == "application/pdf" && pdf.GetProperty("preview").ValueKind == JsonValueKind.Null && File.ReadAllText(Text(pdf, "path")).StartsWith("%PDF"), "explicit PDF stays available as its original vector file");
    log = await Run("write.csv(data.frame(x=1:3), 'manual.csv', row.names=FALSE)");
    Check(!Plots(log).Any() && log.Any(e => Is(e, "notice") && Text(e, "text").Contains("manual.csv")), "ordinary written data files are reported without turning them into chart cards");
    log = await Run("cat('STILL=', preserved, '\n', sep='')");
    Check(!Plots(log).Any() && Output(log).Contains("STILL=42"), "follow-on commands retain variables and do not rediscover old charts");

    int asked = Mark(); await session.SubmitAsync("name <- readline('Your name? '); cat('NAMED=', name, '\\n', sep='')");
    var request = await Wait(asked, e => Is(e, "inputRequested"));
    await session.ReplyAsync(long.Parse(Text(request, "requestId")), "Iain");
    await Wait(asked, e => Is(e, "commandFinished"));
    var echoed = Since(asked).Where(e => Is(e, "inputEcho") && Text(e, "origin") == "input").ToArray();
    Check(echoed.Length == 1 && Text(echoed[0], "prompt") == "Your name? " && Text(echoed[0], "text") == "Iain" && Output(Since(asked)).Contains("NAMED=Iain"),
        "an answered question is echoed after its prompt, as a console shows it, so the transcript keeps the question");
    await Wait(asked, e => Is(e, "state") && Text(e, "state") == "ready");
    int question = Mark(); await session.SubmitAsync("answer <- readline('Answer: '); after <- TRUE");
    await Wait(question, e => Is(e, "inputRequested"));
    await session.InterruptAsync();
    var stopped = await Wait(question, e => Is(e, "commandFinished"));
    Check(Text(stopped, "status") == "interrupted", "Stop unwinds a real input prompt");
    Check(Since(question).Any(e => Is(e, "state") && Text(e, "state") == "interrupting"), "Stop shows as interrupting until R unwinds, so Stop is not offered again meanwhile");
    await Wait(question, e => Is(e, "state") && Text(e, "state") == "ready");
    log = await Run("cat('STOP=', exists('answer'), exists('after'), preserved, '\n')");
    Check(Output(log).Contains("STOP= FALSE FALSE 42"), "Stop supplies no answer, skips following code and preserves earlier variables");
    log = await Run("svg('before-error.svg'); plot(1:2); dev.off(); stop('expected')", "error");
    Check(Plots(log).Any(p => Text(p, "label") == "before-error.svg"), "completed explicit charts survive a later R error");
    log = await Run("graphics.off(); plot(1:3); dev.off(); plot(1:4)");
    Check(Plots(log).Length == 2 && Plots(log).Select(p => Text(p, "plotId")).Distinct().Count() == 2, "plain dev.off preserves the closed page before the next automatic chart");
    log = await Run("graphics.off(); plot(1:3); grDevices::dev.off(); plot(1:4)");
    Check(Plots(log).Length == 2, "namespaced dev.off preserves both completed automatic charts");
    log = await Run("graphics.off(); dev.new(); plot(1:3); older <- dev.cur(); dev.new(); plot(1:4); newer <- dev.cur(); grDevices::dev.off(older); cat('CURRENT=', dev.cur()==newer, '\\n', sep=''); abline(h=2, col='red')");
    var devices = Plots(log);
    Check(devices.Length == 2 && devices.Select(p => Text(p, "plotId")).Distinct().Count() == 2 && Output(log).Contains("CURRENT=TRUE"), "closing an older noncurrent device preserves each device's page and current device");
    bool Red(JsonElement plot) => string.Concat(File.ReadAllText(Text(plot, "path")).Where(c => !char.IsWhiteSpace(c))).Contains("rgb(100%,0%,0%)");
    Check(!Red(devices[0]) && Red(devices[1]), "later additions remain on the newer device's chart rather than overwriting the older chart");
    log = await Run("graphics.off(); dev.new(); plot(1:3); dev.new(); plot(1:4); grDevices::graphics.off()");
    Check(Plots(log).Length == 2 && Plots(log).All(p => Svg(p).Descendants().Any()), "graphics.off preserves each completed page without extra blank charts");
    log = await Run("plot(1:3); grDevices::dev.off(); stop('expected after closing chart')", "error");
    Check(Plots(log).Length == 1, "a closed automatic chart survives a following R error");
    log = await Run("dev.new(); par(mfrow=c(1,2)); plot(1:3); plot(1:4); dev.off()");
    Check(Plots(log).Length == 1, "closing a base multipanel device produces one complete page");
    log = await Run("dev.new(); for(i in 1:3) plot(1:i); graphics.off()");
    Check(Plots(log).Length == 3, "three genuine pages followed by graphics.off remain three charts");
    log = await Run("dev.new(); grid::grid.newpage(); grid::grid.circle(); grid::grid.newpage(); grid::grid.rect(); grDevices::graphics.off()");
    Check(Plots(log).Length == 2, "grid pages also survive namespaced graphics.off without blank pages");
    log = await Run("dev.new(filename='chosen.svg', width=5, height=3); plot(1:3); dev.off()");
    var chosen = Plots(log);
    Check(chosen.Length == 1 && Text(chosen[0], "label") == "chosen.svg" && (string?)Svg(chosen[0]).Attribute("width") == "360pt" && (string?)Svg(chosen[0]).Attribute("height") == "216pt", "dev.new with an explicit filename creates one named chart at the requested dimensions");
    log = await Run("dev.new(filename='deferred.svg', width=5, height=3); plot(1:3)");
    Check(!Plots(log).Any(), "an unclosed named SVG device does not add an empty preview");
    log = await Run("grDevices::dev.off()");
    Check(Plots(log).Length == 1 && Text(Plots(log)[0], "label") == "deferred.svg", "the named SVG is shown once when its device closes and flushes");
    log = await Run("rm(list=ls(all.names=TRUE)); plot(1:3)");
    Check(Plots(log).Length == 1, "clearing every global variable preserves automatic chart capture");
    log = await Run("cat('HOST_STATE=', any(grepl('StatsDirectConsole|statsdirect.console', ls(all.names=TRUE))), '\\n', sep=''); kept_after_clear <- 17; plot(1:4); grDevices::dev.off()");
    Check(Output(log).Contains("HOST_STATE=FALSE") && Plots(log).Length == 1, "private frontend state stays out of the user workspace and close capture survives clearing it");
    log = await Run("cat('AFTER_CLEAR=', kept_after_clear, '\\n', sep='')");
    Check(Output(log).Contains("AFTER_CLEAR=17") && !Plots(log).Any(), "new user variables and ordinary commands remain usable after clearing the workspace");
    Check(Hash(Text(png, "path")) == Hash(Text(png, "originalPath")) && !png.TryGetProperty("previewPath", out _), "ordinary PNG keeps the original file bytes without a replacement encoding");
    void CheckRaster(JsonElement plot, string label, int width, int height)
    {
        Check(Text(plot, "mime") == "image/png" && Text(plot, "label") == label && File.Exists(Text(plot, "originalPath")), label + " retains its original name and supplies a PNG browser preview");
        string previewPath = Text(plot, "previewPath");
        Check(File.Exists(previewPath) && Path.GetExtension(Text(plot, "path")) == Path.GetExtension(label) && Hash(Text(plot, "path")) == Hash(Text(plot, "originalPath")), label + " retains an immutable original and separate PNG preview asset");
        byte[] preview = Convert.FromBase64String(Text(plot, "preview")["data:image/png;base64,".Length..]);
        Check(preview.SequenceEqual(File.ReadAllBytes(previewPath)), label + " preview bytes match the immutable asset used for later Copy");
        using var original = new System.Drawing.Bitmap(Text(plot, "path"));
        using var decoded = new System.Drawing.Bitmap(previewPath);
        Check(original.Width == width && original.Height == height && decoded.Size == original.Size, label + " preserves original pixel dimensions without scaling");
        Check(new[] {new System.Drawing.Point(0, 0), new System.Drawing.Point(width / 2, height / 2), new System.Drawing.Point(width - 1, height - 1)}
            .All(point => original.GetPixel(point.X, point.Y).ToArgb() == decoded.GetPixel(point.X, point.Y).ToArgb()), label + " preserves decoded pixel colours");
    }
    foreach (var (device, extension) in new[] { ("jpeg", "jpg"), ("bmp", "bmp"), ("tiff", "tif") })
    {
        string label = "retained." + extension;
        log = await Run(device + "('" + label + "', width=360, height=220); par(mar=rep(0,4)); plot.new(); rect(0,0,1,1,col='royalblue',border=NA); dev.off()");
        var raster = Plots(log).Single(p => Text(p, "label") == label);
        CheckRaster(raster, label, 360, 220);
        string originalAsset = Text(raster, "path"), previewAsset = Text(raster, "previewPath");
        string originalHash = Hash(originalAsset), previewHash = Hash(previewAsset);
        log = await Run(device + "('" + label + "', width=360, height=220); par(mar=rep(0,4)); plot.new(); rect(0,0,1,1,col='firebrick',border=NA); dev.off()");
        var updated = Plots(log).Single(p => Text(p, "label") == label);
        Check(Text(updated, "path") != originalAsset && Text(updated, "previewPath") != previewAsset
            && Hash(originalAsset) == originalHash && Hash(previewAsset) == previewHash
            && Hash(Text(updated, "previewPath")) != previewHash, label + " overwrites never alter an earlier Open/Copy asset");
    }
    // Base R has no GIF device; an R package's GIF output follows this same file path.
    string gifFixture = Path.Combine(Path.GetTempPath(), "statsdirect-raster-" + Guid.NewGuid().ToString("N") + ".gif");
    try
    {
        using (var gif = new System.Drawing.Bitmap(20, 10))
        {
            using var drawing = System.Drawing.Graphics.FromImage(gif); drawing.Clear(System.Drawing.Color.DarkRed);
            gif.Save(gifFixture, System.Drawing.Imaging.ImageFormat.Gif);
        }
        log = await Run("file.copy(" + JsonSerializer.Serialize(gifFixture.Replace('\\', '/')) + ", 'retained.gif', overwrite=TRUE)");
        CheckRaster(Plots(log).Single(p => Text(p, "label") == "retained.gif"), "retained.gif", 20, 10);
    }
    finally { File.Delete(gifFixture); }
    log = await Run("writeLines('not an image', 'invalid.jpg')");
    Check(!Plots(log).Any() && log.Any(e => Is(e, "notice") && Text(e, "label") == "invalid.jpg" && Text(e, "filePath").EndsWith("invalid.jpg")), "a malformed raster remains a file link without breaking the R session");
    log = await Run("cat('RASTER_ALIVE=', kept_after_clear, '\\n', sep='')");
    Check(Output(log).Contains("RASTER_ALIVE=17"), "variables and R execution survive an unsupported image decoder input");
    Check(session.PersistentWorkspace && session.RDescription.StartsWith("R 4.") && session.RDescription.Length > 2, "the console session describes its persistent R from the worker's ready event");

    // The Rscript runner, the console's fallback for an R the worker cannot use, against the same R's Rscript in a fresh folder of its own.
    string rscript = Path.Combine(rHome, "bin", "x64", "Rscript.exe");
    if (!File.Exists(rscript)) rscript = Path.Combine(rHome, "bin", "Rscript.exe");
    string rVersion = RVersionOf(rscript);
    string runnerFolder = Path.Combine(working, "rscript");
    string Stream(JsonElement[] run, string stream) => string.Concat(run.Where(e => Is(e, "output") && Text(e, "stream") == stream).Select(e => Text(e, "text")));
    var runner = new RScriptSession(rscript, rVersion, runnerFolder, "The interactive console needs 64-bit R 4.2 or later.");
    runner.Event += e => { lock (events) events.Add(e.Clone()); };
    int restarted = -1;
    string brokenId = "";
    try
    {
        int opened = Mark(); await runner.StartAsync();
        var opening = Since(opened);
        var announced = opening.Single(e => Is(e, "session"));
        string runnerId = Text(announced, "sessionId");
        long runnerGeneration = announced.GetProperty("generation").GetInt64();
        Check(Since(0).Where(e => Text(e, "sessionId") != runnerId).All(e => e.GetProperty("generation").GetInt64() < runnerGeneration),
            "the Rscript session's generation exceeds every generation of the console session before it, so the page, which orders sessions by generation alone, takes it as the replacement");
        Check(announced.GetProperty("capabilities").GetProperty("persistentWorkspace").ValueKind == JsonValueKind.False && !runner.PersistentWorkspace
            && opening.Any(e => Is(e, "notice") && Text(e, "text").Contains("Rscript") && Text(e, "text").Contains(rVersion)) && Is(opening.Last(), "state") && Text(opening.Last(), "state") == "ready",
            "the Rscript session announces a workspace that does not persist and names Rscript in its opening notice");
        Check(runner.RDescription == "R " + rVersion + " through Rscript" && Directory.Exists(runnerFolder), "the Rscript session describes its R and creates its working folder");
        log = await Run("cat('OUT=1\\n'); message('MSG=2')", on: runner);
        Check(Stream(log, "stdout").Contains("OUT=1\n") && Stream(log, "stderr").Contains("MSG=2\n") && !Output(log).Contains('\r') && log.Where(e => Is(e, "output")).All(e => e.GetProperty("commandId").ValueKind == JsonValueKind.Number)
            && log.Any(e => Is(e, "commandAccepted")) && log.Any(e => Is(e, "inputEcho") && Text(e, "prompt") == "> " && Text(e, "origin") == "script"),
            "an Rscript submission's printed output arrives as stdout output events and a message as stderr, under its command, with LF line ends as the worker's output has");
        log = await Run("x <- 1; cat('FIRST=', exists('x'), '\\n', sep='')", on: runner);
        var later = await Run("cat('SECOND=', exists('x'), '\\n', sep='')", on: runner);
        Check(Output(log).Contains("FIRST=TRUE") && Output(later).Contains("SECOND=FALSE"), "objects do not persist between Rscript commands, each a fresh R");
        log = await Run("cat('LIB_FIRST=', normalizePath(.libPaths()[1L])==normalizePath(file.path(getwd(),'Lib')), sep=''); cat(' LIB_PRIVATE=', !any(c('libdir','n','device') %in% ls(all.names=TRUE)), sep='')", on: runner);
        Check(Output(log).Contains("LIB_FIRST=TRUE") && Output(log).Contains("LIB_PRIVATE=TRUE") && Directory.Exists(Path.Combine(runnerFolder, "Lib")), "the Rscript runner's Lib folder leads the package search path without adding global setup variables");
        // Each run's pipe readers go with its process rather than waiting for the finalizer, which would otherwise find two handles per run to
        // close: the handles a collection gives back show that, where the count itself also grows with the pool threads the pumps occupy.
        // The Process class leaves one handle per run to the finalizer itself, readers disposed or not, so three per run would mean the readers.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); GC.WaitForPendingFinalizers();
        int handlesBefore = Handles();
        for (int i = 0; i < 12; i++) await Run("cat(" + i + ")", on: runner);
        int handlesAfter = Handles();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); GC.WaitForPendingFinalizers();
        int handlesCollected = Handles();
        Check(handlesAfter - handlesCollected < 24, $"twelve Rscript runs leave no pipe readers for the finalizer to close ({handlesBefore} before, {handlesAfter} after the runs, {handlesCollected} after a collection): each run's readers are released with its process");
        log = await Run("plot(1:3)", on: runner);
        var automatic = Plots(log);
        Check(automatic.Length == 1 && Text(automatic[0], "mime") == "image/svg+xml" && Text(automatic[0], "preview").StartsWith("data:image/svg+xml;base64,") && Text(automatic[0], "label") == "R plot 1"
            && Text(automatic[0], "plotId").StartsWith(runnerId + "-") && Text(automatic[0], "plotId").Contains("-device-1-1")
            && string.Equals(Path.GetDirectoryName(Text(automatic[0], "path")), runnerFolder, StringComparison.OrdinalIgnoreCase) && File.Exists(Text(automatic[0], "path")) && Svg(automatic[0]).Descendants().Any(),
            "plot(1:3) through Rscript produces exactly one chart card with an SVG preview whose file lies in the working folder");
        log = await Run("plot(1:3); plot(1:4); dev.new(); plot(1:5)", on: runner);
        Check(Plots(log).Length == 3 && Plots(log).Select(p => Text(p, "label")).SequenceEqual(new[] { "R plot 1", "R plot 2", "R plot 3" }) && Plots(log).Select(p => Text(p, "plotId")).Distinct().Count() == 3,
            "several pages and devices in one Rscript command become cards in device then page order, numbered within the command");
        log = await Run("svg('named.svg', width=5, height=3); plot(1:4); dev.off(); png('named.png', width=600, height=300); plot(1:4); dev.off(); write.csv(data.frame(x=1:3), 'named.csv', row.names=FALSE)", on: runner);
        var namedSvg = Plots(log).Single(p => Text(p, "label") == "named.svg");
        var namedPng = Plots(log).Single(p => Text(p, "label") == "named.png");
        Check(Plots(log).Length == 2 && Text(namedSvg, "mime") == "image/svg+xml" && (string?)Svg(namedSvg).Attribute("width") == "360pt" && Text(namedSvg, "path") != Text(namedSvg, "originalPath") && File.Exists(Text(namedSvg, "originalPath"))
            && Text(namedPng, "mime") == "image/png" && Text(namedPng, "preview").StartsWith("data:image/png;base64,")
            && log.Any(e => Is(e, "notice") && Text(e, "text").Contains("named.csv") && Text(e, "filePath").EndsWith("named.csv")),
            "named svg and png files give cards with immutable copies and write.csv gives a notice through Rscript, as the console does");
        log = await Run("stop('expected')", "error", on: runner);
        Check(log.Any(e => Is(e, "notice") && Text(e, "text").Contains("exit code 1")) && Stream(log, "stderr").Contains("expected"), "an R error through Rscript finishes as error with a notice naming the exit code");
        int sleeping = Mark(); await runner.SubmitAsync("Sys.sleep(60)", "script");
        var stopwatch = Stopwatch.StartNew(); await runner.InterruptAsync();
        var interrupted = await Wait(sleeping, e => Is(e, "commandFinished"));
        Check(Text(interrupted, "status") == "interrupted" && stopwatch.Elapsed < TimeSpan.FromSeconds(5) && Since(sleeping).Any(e => Is(e, "state") && Text(e, "state") == "interrupting")
            && !Since(sleeping).Any(e => Is(e, "notice") && Text(e, "text").Contains("exit code")), "Stop ends a sleeping Rscript command as interrupted within 5 s, shown as interrupting meanwhile, without an exit-code notice");
        await Wait(sleeping, e => Is(e, "state") && Text(e, "state") == "ready");
        bool refused = false;
        try { await runner.ReplyAsync(1, "Iain"); } catch (InvalidOperationException) { refused = true; }
        Check(refused, "ReplyAsync refuses: R through Rscript cannot ask questions");
        restarted = Mark(); await runner.RestartAsync();
        var afterRestart = Since(restarted);
        var reopened = afterRestart.Single(e => Is(e, "session"));
        Check(Text(reopened, "sessionId") == runnerId && reopened.GetProperty("generation").GetInt64() > runnerGeneration && afterRestart.All(e => e.GetProperty("generation").GetInt64() == reopened.GetProperty("generation").GetInt64())
            && afterRestart.Any(e => Is(e, "notice") && Text(e, "text").Contains("Rscript")) && afterRestart.Any(e => Is(e, "notice") && Text(e, "text").Contains("no workspace to clear"))
            && Is(afterRestart.Last(), "state") && Text(afterRestart.Last(), "state") == "ready",
            "Restart through Rscript opens a new generation with a session event, which releases the page from its restarting state, explains again why R runs this way and that there is no workspace to clear, and leaves the state ready");
        log = await Run("cat('AFTER_RESTART=1\\n')", on: runner);
        Check(Output(log).Contains("AFTER_RESTART=1"), "commands keep running through Rscript after Restart");
        bool ownFilesBefore = Directory.GetFiles(runnerFolder, "console-" + runnerId + "-*").Length > 0;
        runner.Dispose();
        Check(ownFilesBefore && Directory.GetFiles(runnerFolder, "console-" + runnerId + "-*").Length == 0 && File.Exists(Path.Combine(runnerFolder, "named.svg")),
            "Dispose removes the Rscript session's own console files and leaves the user's files");
    }
    finally { runner.Dispose(); }

    // A runner whose Rscript cannot start, a text file in its place: the command ends as an R error ends one, with one notice and no exception
    // for the host to report again.
    string brokenFolder = Path.Combine(working, "broken-r");
    Directory.CreateDirectory(brokenFolder);
    string brokenRscript = Path.Combine(brokenFolder, "Rscript.exe");
    File.WriteAllText(brokenRscript, "not a program");
    using (var broken = new RScriptSession(brokenRscript, rVersion, Path.Combine(working, "broken"), "A check."))
    {
        broken.Event += e => { lock (events) events.Add(e.Clone()); };
        int failing = Mark(); await broken.StartAsync();
        brokenId = Text(Since(failing).Single(e => Is(e, "session")), "sessionId");
        failing = Mark(); await broken.SubmitAsync("cat(1)", "script");
        var failed = await Wait(failing, e => Is(e, "commandFinished"));
        await Wait(failing, e => Is(e, "state") && Text(e, "state") == "ready");
        var told = Since(failing);
        Check(Text(failed, "status") == "error" && told.Count(e => Is(e, "notice")) == 1 && Text(told.Single(e => Is(e, "notice")), "text").StartsWith("R could not run through Rscript. ")
            && Is(told.Last(), "state") && Text(told.Last(), "state") == "ready",
            "an Rscript that cannot start ends the command as an error with one notice saying why, without an exception for the host to report again");
    }

    // The page's acceptance rules, as console.js receive has them: a session event must raise the generation, any other event must come from the
    // session held with the next seq, and while a restart is awaited only a session event is taken. Every event of the three sessions in turn is.
    string? pageSession = null; long pageGeneration = -1, pageSeq = -1; bool pageRestarting = false; int pageRefused = 0;
    var recorded = Since(0);
    for (int i = 0; i < recorded.Length; i++)
    {
        var e = recorded[i];
        if (i == restarted) pageRestarting = true;   // as the page sets it when it sends restart
        long eventGeneration = e.GetProperty("generation").GetInt64(), eventSeq = e.GetProperty("seq").GetInt64();
        if (Is(e, "session"))
        {
            if (pageSession != null && eventGeneration <= pageGeneration) { pageRefused++; continue; }
            pageSession = Text(e, "sessionId"); pageGeneration = eventGeneration; pageSeq = eventSeq; pageRestarting = false;
            continue;
        }
        if (pageRestarting || Text(e, "sessionId") != pageSession || eventGeneration != pageGeneration || eventSeq <= pageSeq) { pageRefused++; continue; }
        pageSeq = eventSeq;
    }
    Check(pageRefused == 0 && !pageRestarting && pageSession == brokenId && recorded.Count(e => Is(e, "session")) == 4,
        $"a page fed every recorded event in order, as console.js takes them, accepts all {recorded.Length}: the console, the Rscript runner, its restart and the runner that could not start each supersede the session before");
    Console.WriteLine($"PASS {checks} production R session checks");
}
finally
{
    lock (events) File.WriteAllLines(Path.Combine(working, "events.ndjson"), events.Select(e => e.GetRawText()));
    Console.WriteLine("Evidence: " + working);
}

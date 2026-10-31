using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using StatsDirect.R.Interactive;

string helper = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../bin/StatsDirect.RConsole.Native.exe"));
string rHome = args.Length > 1 ? Path.GetFullPath(args[1]) : @"C:\Program Files\R\R-4.6.2";
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
async Task<JsonElement[]> Run(string source, string status = "ok")
{
    int mark = Mark(); await session.SubmitAsync(source, "script");
    var finished = await Wait(mark, e => Is(e, "commandFinished"));
    Check(Text(finished, "status") == status, "R submission finishes as " + status);
    await Wait(mark, e => Is(e, "state") && Text(e, "state") == "ready");
    return Since(mark);
}
string Output(JsonElement[] log) => string.Concat(log.Where(e => Is(e, "output")).Select(e => Text(e, "text")));
JsonElement[] Plots(JsonElement[] log) => log.Where(e => Is(e, "plot")).ToArray();
XElement Svg(JsonElement plot) => XDocument.Load(Text(plot, "path")).Root!;
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
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
    Console.WriteLine($"PASS {checks} production R session checks");
}
finally
{
    lock (events) File.WriteAllLines(Path.Combine(working, "events.ndjson"), events.Select(e => e.GetRawText()));
    Console.WriteLine("Evidence: " + working);
}

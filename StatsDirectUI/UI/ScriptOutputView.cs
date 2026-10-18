using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.R;
using StatsDirect.UI.WebReports;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// The output pane of the script window, a WebView2 page: what a run prints, in a monospace face with R's prompts and commands in a
    /// colour of their own; the charts a run writes, an SVG drawn as it is and a raster file as an image, each narrowed to the pane when
    /// wider; and the files written as links, opened through <see cref="LinkClicked"/>.  The text is kept here as well, for the AI pane
    /// and the checks.  Appends are queued in order and reach the page once it is loaded.
    /// </summary>
    internal sealed class ScriptOutputView : Control
    {
        private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
        private readonly StringBuilder text = new();
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task queue = Task.CompletedTask;
        private bool starting, failed;
        private const string SvgDataPrefix = "data:image/svg+xml;base64,";
        private static readonly Regex Url = new(@"(https?://[^\s<>""']+|file:///[^\s<>""']+)", RegexOptions.Compiled);

        /// <summary>A link in the output was clicked: the address of a file the script wrote, or a web address.</summary>
        internal event Action<string> LinkClicked;
        /// <summary>The user clicked in the page: the pane is where Copy applies.</summary>
        internal event Action Activated;
        /// <summary>Whether the page has a selection, as the page last reported it.</summary>
        internal bool HasSelection { get; private set; }
        /// <summary>Completes when the page is loaded; faults when it cannot be.</summary>
        internal Task Ready => ready.Task;

        /// <summary>The text of the output as the pane shows it; a chart stands as its name and its file.</summary>
        public override string Text
        {
            get => text.ToString();
            set { Clear(); AppendText(value); }
        }

        internal ScriptOutputView()
        {
            Controls.Add(browser);
            HandleCreated += async (_, _) => await StartAsync();
        }

        /// <summary>Focus given to the pane goes into the page, where the keyboard copies and selects.</summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (!browser.IsDisposed) browser.Focus();
        }

        private async Task StartAsync()
        {
            if (starting) return;
            starting = true;
            try
            {
                string userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StatsDirect", "WebReports");
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData);
                if (IsDisposed) return;
                await browser.EnsureCoreWebView2Async(environment);
                if (IsDisposed) return;
                CoreWebView2 core = browser.CoreWebView2;
                core.Settings.AreHostObjectsAllowed = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                // the page is the program's own and goes nowhere else: a link is cancelled here and handed out to be opened
                bool first = true;
                core.NavigationStarting += (_, e) =>
                {
                    if (first) { first = false; return; }
                    e.Cancel = true;
                    if (e.IsUserInitiated) LinkClicked?.Invoke(e.Uri);
                };
                core.NewWindowRequested += (_, e) => { e.Handled = true; if (e.IsUserInitiated) LinkClicked?.Invoke(e.Uri); };
                core.DownloadStarting += (_, e) => e.Cancel = true;
                core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
                // the browser's menu kept to copying: the rest, reloading among it, would lose the output
                core.ContextMenuRequested += async (_, e) =>
                {
                    var deferral = e.GetDeferral();
                    try
                    {
                        // WebView2 omits SourceUri for some data-URI images. The
                        // page records the actual right-click target before this event.
                        string source = e.ContextMenuTarget.HasSourceUri ? e.ContextMenuTarget.SourceUri :
                            JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("contextChartSource()"));
                        for (int i = e.MenuItems.Count - 1; i >= 0; i--)
                        {
                            var item = e.MenuItems[i];
                            if (item.Name is not ("copy" or "selectAll" or "copyImage")) { e.MenuItems.RemoveAt(i); continue; }
                            if (item.Name != "copyImage" || source?.StartsWith(SvgDataPrefix, StringComparison.OrdinalIgnoreCase) != true) continue;
                            // Chromium's Copy image rasterizes SVG. Keep its familiar menu
                            // label, adding the vector formats alongside the Office bitmap.
                            var copy = environment.CreateContextMenuItem(item.Label, null, CoreWebView2ContextMenuItemKind.Command);
                            copy.IsEnabled = item.IsEnabled;
                            copy.CustomItemSelected += async (_, _) =>
                            {
                                try { await CopySvgChartAsync(source); }
                                catch (Exception ex) { AppendText("\nCould not copy chart: " + ex.Message + "\n"); }
                            };
                            e.MenuItems.RemoveAt(i); e.MenuItems.Insert(i, copy);
                        }
                    }
                    catch (Exception ex) { AppendText("\nCould not prepare chart copying: " + ex.Message + "\n"); }
                    finally { deferral.Complete(); }
                };
                core.WebMessageReceived += (_, e) => Receive(e);
                core.ProcessFailed += (_, _) => Fail("The output page stopped. Close and reopen the script window.");
                core.NavigationCompleted += (_, e) =>
                {
                    if (ready.Task.IsCompleted) return;
                    if (e.IsSuccess) ready.TrySetResult(); else Fail("The output page could not load: " + e.WebErrorStatus);
                };
                core.NavigateToString(Page);
            }
            catch (Exception ex) { Fail(ex.Message + " If the WebView2 Runtime is missing, run StatsDirectSetup.exe to install it."); }
        }

        /// <summary>The page cannot show the output: says so above it; the text is still kept for the AI pane.</summary>
        private void Fail(string message)
        {
            if (IsDisposed || failed) return;
            failed = true;
            ready.TrySetException(new InvalidOperationException(message));
            var notice = new Label { Dock = DockStyle.Top, AutoSize = true, Text = message, Padding = new Padding(6), ForeColor = System.Drawing.Color.DarkRed };
            Controls.Add(notice);
            notice.BringToFront();
        }

        private void Receive(CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using JsonDocument json = JsonDocument.Parse(e.WebMessageAsJson);
                JsonElement m = json.RootElement;
                if (m.ValueKind != JsonValueKind.Object) return;
                if (m.TryGetProperty("selection", out JsonElement selection) && selection.TryGetInt32(out int length)) HasSelection = length > 0;
                if (m.TryGetProperty("focus", out _)) Activated?.Invoke();
                if (m.TryGetProperty("link", out JsonElement link) && link.ValueKind == JsonValueKind.String) LinkClicked?.Invoke(link.GetString());
            }
            catch (JsonException) { }
        }

        /// <summary>Runs a script on the page after those before it, once the page is loaded; nothing when the page has failed.</summary>
        private Task Enqueue(string script)
        {
            Task previous = queue;
            return queue = Step();
            async Task Step()
            {
                try { await previous; } catch { }
                try
                {
                    await ready.Task;
                    if (IsDisposed || browser.IsDisposed) return;
                    await browser.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch { }   // the page failed, or the browser went: the text is kept
            }
        }

        /// <summary>Completes when every append so far has reached the page.</summary>
        internal Task SettledAsync() => queue;

        private static string Json(string s) => JsonSerializer.Serialize(s);
        private static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        /// <summary>Plain text as markup: escaped, with web and file addresses as links.</summary>
        internal static string TextHtml(string s) => Url.Replace(Escape(s), m => "<a href=\"" + m.Value + "\">" + m.Value + "</a>");

        /// <summary>R's transcript as markup, as an R session shows it: the commands after their prompts in a class of their own.</summary>
        internal static string TranscriptHtml(string transcript)
        {
            StringBuilder html = new();
            foreach (string line in (transcript ?? "").Replace("\r", "").Split('\n'))
            {
                bool prompt = line.StartsWith("> ", StringComparison.Ordinal) || line.StartsWith("+ ", StringComparison.Ordinal) || line == ">";
                if (prompt) html.Append("<span class=\"prompt\">").Append(Escape(line)).Append("</span>\n");
                else html.Append(TextHtml(line)).Append('\n');
            }
            return html.ToString();
        }

        internal void AppendText(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            text.Append(s);
            Enqueue("append(" + Json(TextHtml(s)) + ")");
        }

        /// <summary>Appends R's transcript as an R session shows it: the commands after their prompts in a colour of their own, what R printed in the pane's.</summary>
        internal void AppendTranscript(string transcript)
        {
            string lines = (transcript ?? "").Replace("\r", "");
            foreach (string line in lines.Split('\n')) text.Append(line).Append('\n');
            Enqueue("append(" + Json(TranscriptHtml(lines)) + ")");
        }

        /// <summary>
        /// A chart file in the pane after its name: an SVG as it is, drawn by the browser at the size its svg element gives, or narrowed to
        /// the pane; a PDF in the browser's viewer; a raster file as an image; the caption names the file and is its link.  Throws when the
        /// file is not a picture that can be shown.
        /// </summary>
        internal void AppendChart(FileInfo file)
        {
            string picture;
            if (".svg".Equals(file.Extension, StringComparison.OrdinalIgnoreCase))
            {
                var vector = RResultsParser.VectorPicture(File.ReadAllText(file.FullName));
                // Each SVG has its own document. R reuses glyph and clip IDs on
                // later pages; inline SVGs would resolve those in earlier charts.
                string data = Convert.ToBase64String(Encoding.UTF8.GetBytes(Sanitised(vector.Svg)));
                picture = "<img class=\"svg-chart\" width=\"" + vector.Width + "\" height=\"" + vector.Height + "\" alt=\"" + Escape(file.Name) + "\" src=\"data:image/svg+xml;base64," + data + "\">";
            }
            else if (".pdf".Equals(file.Extension, StringComparison.OrdinalIgnoreCase))
                picture = "<iframe class=\"pdf\" title=\"" + Escape(file.Name) + "\" src=\"data:application/pdf;base64," + Convert.ToBase64String(File.ReadAllBytes(file.FullName)) + "#toolbar=0&navpanes=0&view=FitH\"></iframe>";
            else
            {
                using System.Drawing.Image image = System.Drawing.Image.FromFile(file.FullName);
                using MemoryStream png = new();
                image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                picture = "<img width=\"" + image.Width + "\" height=\"" + image.Height + "\" alt=\"" + Escape(file.Name) + "\" src=\"data:image/png;base64," + Convert.ToBase64String(png.ToArray()) + "\">";
            }
            string link = new Uri(file.FullName).AbsoluteUri;
            text.Append("\nChart: ").Append(file.Name).Append(" (").Append(link).Append(")\n");
            Enqueue("append(" + Json("\n<div class=\"chart\"><a href=\"" + link + "\">Chart: " + Escape(file.Name) + "</a></div>" + picture + "\n") + ")");
        }

        /// <summary>
        /// Copies an embedded chart as SVG, with a standard PNG/bitmap for applications that need a raster.  There is no HTML form:
        /// PowerPoint and Word insert the image/svg+xml form as a vector graphic and take it before the PNG and the text, but Word
        /// takes an HTML form before it, and the HTML could only carry the raster.
        /// </summary>
        internal async Task CopySvgChartAsync(string sourceUri)
        {
            if (sourceUri?.StartsWith(SvgDataPrefix, StringComparison.OrdinalIgnoreCase) != true || sourceUri.Length > 27_000_000)
                throw new InvalidDataException("Choose an embedded SVG chart smaller than 20 MB.");
            await SettledAsync(); await Ready;
            // Only copy an image already in this output page. Drawing that loaded
            // image on a canvas requires no file access or additional network load.
            string result = await browser.CoreWebView2.ExecuteScriptAsync($$"""
                (()=>{
                  const image=[...document.images].find(i=>i.classList.contains('svg-chart')&&i.getAttribute('src')==={{Json(sourceUri)}});
                  if(!image||!image.complete||!image.naturalWidth||!image.naturalHeight)return null;
                  let png=null;
                  try {
                    const canvas=document.createElement('canvas'),scale=Math.min(2,4096/Math.max(image.naturalWidth,image.naturalHeight));
                    canvas.width=Math.max(1,Math.round(image.naturalWidth*scale));canvas.height=Math.max(1,Math.round(image.naturalHeight*scale));
                    const context=canvas.getContext('2d');context.fillStyle='white';context.fillRect(0,0,canvas.width,canvas.height);
                    context.drawImage(image,0,0,canvas.width,canvas.height);png=canvas.toDataURL('image/png');
                  } catch {} // The original SVG remains useful if raster conversion is unavailable.
                  return {png,alt:image.alt};
                })()
                """);
            using JsonDocument image = JsonDocument.Parse(result);
            if (image.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("The chart is no longer available.");
            byte[] original = Convert.FromBase64String(sourceUri[SvgDataPrefix.Length..]);
            if (original.Length > 20_000_000) throw new InvalidDataException("The SVG chart is too large to copy.");
            var vector = RResultsParser.VectorPicture(Sanitised(Encoding.UTF8.GetString(original)));
            var data = new DataObject();
            data.SetData(ReportClipboard.FragmentFormat, false, vector.Svg);
            string png = image.RootElement.GetProperty("png").GetString();
            byte[] pngBytes = png?.StartsWith("data:image/png;base64,", StringComparison.Ordinal) == true && png.Length <= 27_000_000
                ? Convert.FromBase64String(png["data:image/png;base64,".Length..]) : null;
            if (pngBytes != null && pngBytes.Length > 20_000_000) pngBytes = null;
            using var raster = pngBytes == null ? null : new MemoryStream(pngBytes, writable: false);
            using var bitmap = raster == null ? null : new System.Drawing.Bitmap(raster);
            if (bitmap != null)
            {
                data.SetData("PNG", false, new MemoryStream(pngBytes, writable: false));
                data.SetData(DataFormats.Bitmap, true, bitmap);
            }
            data.SetData(DataFormats.UnicodeText, image.RootElement.GetProperty("alt").GetString() ?? "R chart");
            // The SVG last, in the order of the report's copy, which PowerPoint's Paste command takes as an SVG graphic.
            data.SetData("image/svg+xml", false, new MemoryStream(Encoding.UTF8.GetBytes(PlainSized(vector.Svg, vector.Width, vector.Height)), writable: false));
            Clipboard.SetDataObject(data, true, 5, 100);
        }

        /// <summary>
        /// The SVG with its root's width and height as plain numbers, the picture's size in pixels: PowerPoint's Paste command took
        /// a root sized in points, as R's devices write it, as the PNG, and takes this as the SVG graphic at the chart's intended
        /// size.  The root's attributes are read as XML quotes them, with either quote; a root this does not read, such as one
        /// with a namespace prefix, is left as it is.
        /// </summary>
        internal static string PlainSized(string svg, int width, int height)
        {
            Match root = Regex.Match(svg ?? "", @"<svg(?=[\s/>])(?:\s+[^\s=/>]+\s*=\s*(?:""[^""]*""|'[^']*'))*\s*/?>");
            if (!root.Success) return svg;
            string tag = Regex.Replace(root.Value, @"\s+(?:width|height)\s*=\s*(?:""[^""]*""|'[^']*')", "");
            if (Regex.IsMatch(tag, @"\s(?:width|height)\s*=")) return svg;
            tag = "<svg width=\"" + width.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\" height=\"" + height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"" + tag[4..];
            return svg[..root.Index] + tag + svg[(root.Index + root.Length)..];
        }

        /// <summary>An SVG with nothing that runs: scripts, event handlers and script links go.</summary>
        internal static string Sanitised(string svg)
        {
            svg = Regex.Replace(svg, @"<script\b[\s\S]*?</script\s*>", "", RegexOptions.IgnoreCase);
            svg = Regex.Replace(svg, @"<script\b[^>]*/>", "", RegexOptions.IgnoreCase);
            svg = Regex.Replace(svg, @"\s+on\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", "", RegexOptions.IgnoreCase);
            svg = Regex.Replace(svg, @"(href\s*=\s*[""'])\s*javascript:[^""']*", "$1", RegexOptions.IgnoreCase);
            return svg;
        }

        internal void Clear()
        {
            text.Clear();
            HasSelection = false;
            Enqueue("clearAll()");
        }

        internal Task SelectAllAsync() => Enqueue("selectAll()");

        /// <summary>The text selected on the page; empty when the page is not up.</summary>
        internal async Task<string> SelectedTextAsync()
        {
            await SettledAsync();
            if (!ready.Task.IsCompletedSuccessfully || IsDisposed || browser.IsDisposed) return "";
            string json = await browser.CoreWebView2.ExecuteScriptAsync("getSelection().toString()");
            return JsonSerializer.Deserialize<string>(json) ?? "";
        }

        /// <summary>Copies the selected text to the clipboard, as Copy on the toolbar and the Edit menu ask.</summary>
        internal async Task CopyAsync()
        {
            string selected = await SelectedTextAsync();
            if (selected.Length > 0) Clipboard.SetText(selected);
        }

        /// <summary>The page: the transcript's styles and the functions the pane calls, which report selection, clicks and links back.</summary>
        internal const string Page = @"<!doctype html><html><head><meta charset=""utf-8""><title>Output</title><style>
html,body{margin:0;background:#fff;color:#000}
body{padding:4px 8px;font:12px/1.35 Consolas,""Courier New"",monospace}
#t{white-space:pre-wrap;overflow-wrap:anywhere}
.prompt{color:#0000a0}
a{color:#0645ad}
.chart{margin:8px 0 2px;color:#444}
.chart a{color:#444}
svg,img{display:block;max-width:100%;height:auto;margin:2px 0 4px;background:#fff}
iframe.pdf{display:block;width:100%;height:520px;border:1px solid #ccc;margin:2px 0 4px;background:#fff}
</style></head><body><div id=""t""></div><script>
(function(){
var t=document.getElementById('t'),contextImage=null;
function post(m){if(window.chrome&&chrome.webview)chrome.webview.postMessage(m);}
window.append=function(h){t.insertAdjacentHTML('beforeend',h);window.scrollTo(0,document.body.scrollHeight);};
window.clearAll=function(){t.innerHTML='';contextImage=null;};
window.contextChartSource=function(){return contextImage&&t.contains(contextImage)?contextImage.getAttribute('src'):null;};
window.selectAll=function(){var r=document.createRange();r.selectNodeContents(t);var s=getSelection();s.removeAllRanges();s.addRange(r);post({selection:s.toString().length});};
document.addEventListener('contextmenu',function(e){contextImage=e.target.closest?e.target.closest('img.svg-chart'):null;},true);
document.addEventListener('selectionchange',function(){post({selection:getSelection().toString().length});});
document.addEventListener('mousedown',function(){post({focus:true});},true);
document.addEventListener('click',function(e){var a=e.target.closest&&e.target.closest('a');if(a){e.preventDefault();post({link:a.href});}},true);
})();
</script></body></html>";
    }
}

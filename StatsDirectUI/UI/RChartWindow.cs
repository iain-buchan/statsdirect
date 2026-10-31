#nullable enable
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI.WebReports;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI;

/// <summary>A view of one retained R chart. Zoom changes only its presentation.</summary>
internal sealed class RChartWindow : Form
{
    private readonly string origin = "https://rchart-" + Guid.NewGuid().ToString("N") + ".statsdirect.invalid";
    private readonly string originalPath, directory, pagePath, mime;
    private readonly Action<string> notice;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ContextMenuStrip chartMenu = new();
    private readonly ToolStripButton zoomOut = new("−"), percent = new("100%"), zoomIn = new("+"), fit = new("Fit");
    private readonly ToolStripLabel status = new("Loading chart…");
    private bool starting, closed;
    private int copyRequest;
    private double zoom = 1;

    internal WebView2 Browser { get; } = new() { Dock = DockStyle.Fill };
    internal Task Ready => ready.Task;
    internal string AssetPath { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<string, bool>? IsProtectedSavePath { get; set; }

    internal RChartWindow(string sourcePath, string mime, string title, Action<string> notice)
    {
        if (mime is not ("image/svg+xml" or "image/png")) throw new ArgumentException("Choose an SVG or PNG chart.", nameof(mime));
        originalPath = Path.GetFullPath(sourcePath);
        this.mime = mime; this.notice = notice;
        directory = Path.Combine(Path.GetTempPath(), "StatsDirectRCharts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        AssetPath = Path.Combine(directory, mime == "image/svg+xml" ? "chart.svg" : "chart.png");
        pagePath = Path.Combine(directory, "viewer.htm");
        try { File.Copy(originalPath, AssetPath); }
        catch
        {
            try { File.Delete(AssetPath); Directory.Delete(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        Text = string.IsNullOrWhiteSpace(title) ? "R chart" : title;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 660); MinimumSize = new Size(360, 260);
        StartPosition = FormStartPosition.CenterParent;
        var bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 3, 6, 3) };
        void Configure(ToolStripButton button, string name, string tooltip, Func<Task> action)
        {
            button.Name = name; button.AccessibleName = tooltip;
            button.ToolTipText = tooltip; button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            // Measure text at the current Windows DPI; a fixed pixel width
            // clips the percentage and Fit label on a scaled display.
            button.AutoSize = true; button.Enabled = false;
            button.Click += async (_, _) => await RunActionAsync(action);
        }
        Configure(zoomOut, "RChartZoomOut", "Zoom out (Ctrl+−)", ZoomOutAsync);
        Configure(percent, "RChartZoomPercent", "Reset to fit (Ctrl+0)", FitAsync);
        Configure(zoomIn, "RChartZoomIn", "Zoom in (Ctrl++)", ZoomInAsync);
        Configure(fit, "RChartZoomFit", "Fit chart to window (Ctrl+0)", FitAsync);
        bar.Items.AddRange(new ToolStripItem[] { new ToolStripLabel("Zoom"), zoomOut, percent, zoomIn, fit, new ToolStripSeparator(), status });
        Controls.Add(Browser); Controls.Add(bar);
        ToolStripMenuItem Item(string label, Func<Task> action) => new(label, null, async (_, _) => await RunActionAsync(action));
        chartMenu.Items.Add(Item("Copy Chart", CopyChartAsync));
        chartMenu.Items.Add(Item("Save Chart as SVG…", () => SaveWithDialogAsync("svg")));
        chartMenu.Items.Add(Item("Save Chart as PNG…", () => SaveWithDialogAsync("png")));
        chartMenu.Items[1].Enabled = mime == "image/svg+xml";
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var available = Screen.FromControl(this).WorkingArea;
        if (Width > available.Width || Height > available.Height)
            Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
        if (!starting) { starting = true; _ = InitializeAsync(); }
    }

    private async Task InitializeAsync()
    {
        try
        {
            string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StatsDirect", "RConsole", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
            CheckOpen();
            await Browser.EnsureCoreWebView2Async(environment); CheckOpen();
            var core = Browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsPinchZoomEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.SetVirtualHostNameToFolderMapping(new Uri(origin).Host, directory, CoreWebView2HostResourceAccessKind.DenyCors);
            string assetUri = origin + "/" + Path.GetFileName(AssetPath), pageUri = origin + "/viewer.htm";
            core.NavigationStarting += (_, e) => e.Cancel = e.Uri != pageUri;
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess && !closed) Fail("The chart viewer could not load: " + e.WebErrorStatus); };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) =>
            {
                if (e.Request.Uri != assetUri && e.Request.Uri != pageUri)
                    e.Response = environment.CreateWebResourceResponse(new MemoryStream(), 403, "Forbidden", "Content-Type: text/plain");
            };
            core.ContextMenuRequested += (_, e) =>
            {
                e.Handled = true;
                if (!closed && ready.Task.IsCompletedSuccessfully) chartMenu.Show(this, PointToClient(Cursor.Position));
            };
            core.WebMessageReceived += (_, e) =>
            {
                if (closed) return;
                try
                {
                    using var document = JsonDocument.Parse(e.WebMessageAsJson);
                    var item = document.RootElement;
                    string? type = item.GetProperty("type").GetString();
                    if (type == "error") { Fail(item.GetProperty("message").GetString() ?? "The chart could not load."); return; }
                    if (type == "copy") { _ = RunActionAsync(CopyChartAsync); return; }
                    if (type is "ready" or "zoom")
                    {
                        zoom = item.GetProperty("zoom").GetDouble();
                        percent.Text = Math.Round(zoom * 100).ToString(CultureInfo.CurrentCulture) + "%";
                        zoomOut.Enabled = zoom > .25; zoomIn.Enabled = zoom < 8;
                        percent.Enabled = fit.Enabled = true;
                        if (type == "ready") { status.Text = ""; ready.TrySetResult(); }
                    }
                }
                catch (JsonException) { }
            };
            core.ProcessFailed += (_, _) => Fail("The chart viewer stopped. Close it and open the chart again.");
            Browser.ZoomFactor = 1;
            Browser.ZoomFactorChanged += (_, _) => { if (!closed && Browser.ZoomFactor != 1) Browser.ZoomFactor = 1; };
            await File.WriteAllTextAsync(pagePath, Page.Replace("__ASSET__", JsonSerializer.Serialize(assetUri), StringComparison.Ordinal));
            CheckOpen(); core.Navigate(pageUri);
        }
        catch (Exception error) { if (!closed) Fail(error.Message); }
    }

    private void CheckOpen()
    {
        if (closed || IsDisposed) throw new ObjectDisposedException(nameof(RChartWindow));
    }
    private void Fail(string message)
    {
        if (closed) return;
        status.Text = message; ready.TrySetException(new IOException(message)); notice(message);
        zoomOut.Enabled = zoomIn.Enabled = percent.Enabled = fit.Enabled = false;
    }
    private async Task RunActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception error) { if (!closed) { status.Text = error.Message; notice(error.Message); } }
    }
    private bool TryShortcut(Keys keys)
    {
        if ((keys & Keys.Control) == 0 || (keys & Keys.Alt) != 0) return false;
        Func<Task>? action = (keys & Keys.KeyCode) switch
        {
            Keys.Add or Keys.Oemplus => ZoomInAsync,
            Keys.Subtract or Keys.OemMinus => ZoomOutAsync,
            Keys.D0 or Keys.NumPad0 => FitAsync,
            Keys.C => CopyChartAsync,
            _ => null
        };
        if (action == null) return false;
        if (!closed && ready.Task.IsCompletedSuccessfully) BeginInvoke(new Action(async () => await RunActionAsync(action)));
        return true;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => TryShortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);
    private async Task ExecuteAsync(string script)
    {
        CheckOpen(); await Ready; CheckOpen();
        await Browser.CoreWebView2.ExecuteScriptAsync(script); CheckOpen();
    }
    internal Task SetZoomAsync(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return ExecuteAsync("ChartViewer.setZoom(" + Math.Clamp(value, .25, 8).ToString(CultureInfo.InvariantCulture) + ")");
    }
    internal Task ZoomInAsync() => ExecuteAsync("ChartViewer.zoomBy(1.25)");
    internal Task ZoomOutAsync() => ExecuteAsync("ChartViewer.zoomBy(0.8)");
    internal Task FitAsync() => ExecuteAsync("ChartViewer.fit()");

    private async Task<string> RasterAsync()
    {
        CheckOpen(); await Ready; CheckOpen();
        string result = await Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new
        {
            expression = "ChartViewer.renderPng()", awaitPromise = true, returnByValue = true
        })).WaitAsync(TimeSpan.FromSeconds(15));
        CheckOpen();
        using var evaluation = JsonDocument.Parse(result);
        if (evaluation.RootElement.TryGetProperty("exceptionDetails", out _)) throw new IOException("The full chart could not be drawn as PNG.");
        string? png = evaluation.RootElement.GetProperty("result").GetProperty("value").GetString();
        if (png?.StartsWith("data:image/png;base64,", StringComparison.Ordinal) != true) throw new IOException("The chart gave no PNG picture.");
        return png;
    }

    internal async Task CopyChartAsync()
    {
        int request = ++copyRequest;
        CheckOpen(); await Ready; CheckOpen();
        if (mime == "image/svg+xml")
        {
            string svg = await File.ReadAllTextAsync(AssetPath);
            string png = await RasterAsync(); CheckOpen();
            if (request != copyRequest) return;
            RConsoleView.PublishVectorChart(svg, png, Text);
        }
        else
        {
            byte[] bytes = await File.ReadAllBytesAsync(AssetPath); CheckOpen();
            string png = "data:image/png;base64," + Convert.ToBase64String(bytes);
            using var source = new MemoryStream(bytes, writable: false);
            using var image = Image.FromStream(source);
            string html = RConsoleView.ClipboardImageHtml(png, image.Width, image.Height, Text);
            var data = new DataObject(); data.SetData(ReportClipboard.FragmentFormat, false, html);
            data.SetData(DataFormats.Html, ReportClipboard.EncodeHtml(html)); data.SetData(DataFormats.UnicodeText, Text);
            if (request != copyRequest) return;
            ReportClipboard.Publish(data, png: png);
        }
        if (!closed) notice("Chart copied with PNG and bitmap alternatives" + (mime == "image/svg+xml" ? " and its original SVG." : "."));
    }

    private async Task SaveWithDialogAsync(string format)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save Chart as " + format.ToUpperInvariant(), DefaultExt = format, AddExtension = true,
            FileName = "R chart." + format, Filter = format == "svg" ? "SVG picture|*.svg|All files|*.*" : "PNG picture|*.png|All files|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) await SaveChartAsync(format, dialog.FileName);
    }
    internal async Task SaveChartAsync(string format, string path)
    {
        CheckOpen();
        if (format is not ("svg" or "png") || format == "svg" && mime != "image/svg+xml") throw new ArgumentException("Choose a supported SVG or PNG format.", nameof(format));
        path = Path.GetFullPath(path);
        if (path.Equals(originalPath, StringComparison.OrdinalIgnoreCase) || path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || IsProtectedSavePath?.Invoke(path) == true)
            throw new InvalidOperationException("Choose a different filename to preserve the chart's saved revision.");
        byte[] bytes;
        if (format == "svg") bytes = Encoding.UTF8.GetBytes(RConsoleView.SaveableChartSvg(await File.ReadAllTextAsync(AssetPath)));
        else if (mime == "image/png") bytes = await File.ReadAllBytesAsync(AssetPath);
        else bytes = Convert.FromBase64String((await RasterAsync())["data:image/png;base64,".Length..]);
        CheckOpen(); await File.WriteAllBytesAsync(path, bytes);
        if (!closed) notice("Chart saved as " + path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !closed)
        {
            closed = true; ++copyRequest; ready.TrySetCanceled();
            chartMenu.Dispose(); Browser.Dispose();
            _ = Task.Run(async () =>
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try { File.Delete(AssetPath); File.Delete(pagePath); Directory.Delete(directory); return; }
                    catch (IOException) { await Task.Delay(100); }
                    catch (UnauthorizedAccessException) { return; }
                }
            });
        }
        base.Dispose(disposing);
    }

    private const string Page = """
        <!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src 'self'; script-src 'nonce-chart-viewer'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
        <style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:white}#viewport{position:absolute;inset:0;overflow:auto;outline:none;cursor:grab;overscroll-behavior:contain;touch-action:none}#viewport.dragging{cursor:grabbing}#surface{position:relative}#chart{position:absolute;display:block;max-width:none;user-select:none;-webkit-user-drag:none;pointer-events:none}</style>
        </head><body><div id="viewport" tabindex="0" aria-label="Chart. Drag to pan. Control plus mouse wheel to zoom."><div id="surface"><img id="chart" alt="R chart" draggable="false"></div></div>
        <script nonce="chart-viewer">
        (()=>{
          const viewport=document.getElementById('viewport'),surface=document.getElementById('surface'),image=document.getElementById('chart'),source=__ASSET__;
          let zoom=1,loaded=false,width=0,height=0,left=0,top=0,drag=null,pinch=null,lastWidth=0,lastHeight=0;
          const touches=new Map();
          const post=(type,extra={})=>chrome.webview.postMessage({type,zoom,...extra});
          const snapshot=()=>({zoom,left:viewport.scrollLeft,top:viewport.scrollTop,width,height,naturalWidth:image.naturalWidth,naturalHeight:image.naturalHeight,viewportWidth:viewport.clientWidth,viewportHeight:viewport.clientHeight,scrollWidth:viewport.scrollWidth,scrollHeight:viewport.scrollHeight});
          function layout(anchor=null){
            if(!loaded)return;
            const vw=viewport.clientWidth,vh=viewport.clientHeight,scale=Math.min(1,Math.max(1,vw-24)/image.naturalWidth,Math.max(1,vh-24)/image.naturalHeight)*zoom;
            width=image.naturalWidth*scale;height=image.naturalHeight*scale;
            const sw=Math.max(vw,width+24),sh=Math.max(vh,height+24);left=(sw-width)/2;top=(sh-height)/2;
            surface.style.width=sw+'px';surface.style.height=sh+'px';image.style.width=width+'px';image.style.height=height+'px';image.style.left=left+'px';image.style.top=top+'px';
            if(anchor){viewport.scrollLeft=left+anchor.nx*width-anchor.x;viewport.scrollTop=top+anchor.ny*height-anchor.y;}
            else if(zoom===1){viewport.scrollLeft=0;viewport.scrollTop=0;}
            lastWidth=vw;lastHeight=vh;
            post('zoom');
          }
          function setZoom(value,x=viewport.clientWidth/2,y=viewport.clientHeight/2){
            if(!loaded)return snapshot();
            const anchor={x,y,nx:(viewport.scrollLeft+x-left)/width,ny:(viewport.scrollTop+y-top)/height};
            zoom=Math.max(.25,Math.min(8,value));layout(zoom===1?null:anchor);return snapshot();
          }
          const fit=()=>{zoom=1;layout();return snapshot()};
          const pan=(x,y)=>{viewport.scrollLeft+=x;viewport.scrollTop+=y;return snapshot()};
          viewport.addEventListener('wheel',event=>{if(!event.ctrlKey)return;event.preventDefault();const rect=viewport.getBoundingClientRect();setZoom(zoom*Math.exp(-event.deltaY*(event.deltaMode===1 ? .04 : .002)),event.clientX-rect.left,event.clientY-rect.top)},{passive:false});
          viewport.addEventListener('keydown',event=>{if(!event.ctrlKey||event.altKey)return;const key=event.key.toLowerCase();if(!['+','=','-','_','0','c'].includes(key))return;event.preventDefault();if(key==='c')post('copy');else if(key==='0')fit();else setZoom(zoom*((key==='-'||key==='_') ? .8 : 1.25))});
          viewport.addEventListener('pointerdown',event=>{
            if(event.button!==0)return;viewport.focus({preventScroll:true});viewport.setPointerCapture(event.pointerId);
            if(event.pointerType==='touch')touches.set(event.pointerId,{x:event.clientX,y:event.clientY});
            if(touches.size===2){const [a,b]=[...touches.values()];pinch={distance:Math.hypot(a.x-b.x,a.y-b.y),zoom};drag=null;}
            else drag={id:event.pointerId,x:event.clientX,y:event.clientY,left:viewport.scrollLeft,top:viewport.scrollTop};
            viewport.classList.add('dragging');event.preventDefault();
          });
          viewport.addEventListener('pointermove',event=>{
            if(touches.has(event.pointerId))touches.set(event.pointerId,{x:event.clientX,y:event.clientY});
            if(pinch&&touches.size===2){const [a,b]=[...touches.values()],rect=viewport.getBoundingClientRect();setZoom(pinch.zoom*Math.hypot(a.x-b.x,a.y-b.y)/Math.max(1,pinch.distance),(a.x+b.x)/2-rect.left,(a.y+b.y)/2-rect.top);return;}
            if(!drag||drag.id!==event.pointerId)return;viewport.scrollLeft=drag.left+drag.x-event.clientX;viewport.scrollTop=drag.top+drag.y-event.clientY;
          });
          const release=event=>{touches.delete(event.pointerId);pinch=null;if(drag?.id===event.pointerId)drag=null;viewport.classList.remove('dragging');if(viewport.hasPointerCapture(event.pointerId))viewport.releasePointerCapture(event.pointerId)};
          viewport.addEventListener('pointerup',release);viewport.addEventListener('pointercancel',release);
          new ResizeObserver(()=>{if(!loaded)return;const x=viewport.clientWidth/2,y=viewport.clientHeight/2;layout(zoom===1?null:{x,y,nx:(viewport.scrollLeft+lastWidth/2-left)/width,ny:(viewport.scrollTop+lastHeight/2-top)/height})}).observe(viewport);
          async function renderPng(){const original=new Image();original.src=source;await original.decode();const scale=Math.min(2,4096/Math.max(original.naturalWidth,original.naturalHeight)),canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(original.naturalWidth*scale));canvas.height=Math.max(1,Math.round(original.naturalHeight*scale));const context=canvas.getContext('2d');context.fillStyle='white';context.fillRect(0,0,canvas.width,canvas.height);context.drawImage(original,0,0,canvas.width,canvas.height);return canvas.toDataURL('image/png')}
          window.ChartViewer={snapshot,setZoom,zoomBy:factor=>setZoom(zoom*factor),fit,pan,renderPng};
          image.src=source;image.decode().then(()=>{loaded=true;layout();post('ready');viewport.focus({preventScroll:true})}).catch(error=>post('error',{message:'The chart could not load: '+error.message}));
        })();
        </script></body></html>
        """;
}

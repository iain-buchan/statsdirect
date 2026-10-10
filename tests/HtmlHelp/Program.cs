using Microsoft.Web.WebView2.Core;
using StatsDirect.UI.HtmlHelp;
using System.Text.Json;
using System.Xml.Linq;

internal static class Program
{
    private static int checks;
    private static HelpView help;
    private static string output;
    private static void Check(bool ok, string description) { if (!ok) throw new Exception(description); checks++; Console.WriteLine("PASS " + description); }
    private static async Task<JsonElement> Js(string script) => JsonDocument.Parse(await help.Browser.CoreWebView2.ExecuteScriptAsync(script).WaitAsync(TimeSpan.FromSeconds(8))).RootElement.Clone();
    private static async Task Until(string condition)
    {
        for (int i = 0; i < 150; i++)
        {
            if ((await Js(condition)).ValueKind == JsonValueKind.True) return;
            await Task.Delay(100);
        }
        throw new Exception("Timed out: " + condition + " at " + help.Browser.CoreWebView2.Source);
    }
    private static async Task Loaded(string path) => await Until($"location.pathname === {JsonSerializer.Serialize(path)} && document.readyState === 'complete' && typeof MadCap !== 'undefined'");
    [STAThread]
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "artifacts"));
        Directory.CreateDirectory(output);
        ApplicationConfiguration.Initialize();
        int exit = 1;
        using var owner = new Form { Text = "StatsDirect help tests", IsMdiContainer = true, Width = 1600, Height = 900, Location = new Point(-16000, -16000), StartPosition = FormStartPosition.Manual, ShowInTaskbar = false };
        owner.Shown += async (_, _) =>
        {
            try { await Run(owner); Console.WriteLine($"PASS {checks} HTML5 help checks"); exit = 0; }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex); }
            finally { help?.Dispose(); owner.Close(); }
        };
        Application.Run(owner);
        return exit;
    }

    private static async Task Run(Form owner)
    {
        string assets = Path.Combine(AppContext.BaseDirectory, "Help");
        var catalog = new HelpCatalog(assets);
        var invalid = catalog.Topics.Where(t => catalog.Resolve(t.Key) == null).ToArray();
        Check(invalid.Length == 0, $"all {catalog.Topics.Count} numeric and named aliases resolve to packaged topics: {string.Join(",", invalid.Select(x => x.Key))}");
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var ids = Directory.GetFiles(Path.Combine(repository, "StatsDirectUI/Assets/Operations"), "*.xml")
            .SelectMany(file => XDocument.Load(file).Descendants().Attributes("chm-id").Select(a => a.Value)).Distinct().ToArray();
        Check(ids.Length > 150 && ids.All(id => catalog.Resolve(id) != null), $"all {ids.Length} distinct operation context IDs have offline topics");
        Check(catalog.Resolve("1239").AbsolutePath == "/parametric_methods/paired_t.htm", "paired t-test context ID preserves its topic");
        Check(catalog.Resolve("https://www.statsdirect.com/help/parametric_methods/paired_t.htm#ref")?.Fragment == "#ref", "existing website topic URLs map to offline help with anchors");
        Check(catalog.Resolve("https://iain-buchan.github.io/statisticalhelp/parametric_methods/paired_t.htm")?.AbsolutePath == "/parametric_methods/paired_t.htm", "GitHub Pages help URLs map offline");
        Check(new[] { "../../contents.htm", "%2e%2e/contents.htm", "file:///C:/windows/win.ini", "javascript:alert(1)", "missing.htm", "1234567", "https://example.com/" }.All(x => catalog.Resolve(x) == null), "unknown topics, traversal and untrusted schemes do not resolve to local pages");
        Check(!HelpCatalog.IsExternal("javascript:alert(1)") && !HelpCatalog.IsExternal("file:///C:/test") && HelpCatalog.IsExternal("https://doi.org/10.1/test"), "only web/mail links can leave the help viewer");
        bool missing = false;
        try { _ = new HelpCatalog(Path.Combine(output, "absent-help")); } catch (FileNotFoundException) { missing = true; }
        Check(missing, "missing help assets produce a repair message");
        using var contentHost = new Form { Width = 900, Height = 780, StartPosition = FormStartPosition.Manual, Location = new Point(-15000, -15000), ShowInTaskbar = false };
        help = new HelpView(catalog, Path.Combine(output, "browser-profile"));
        contentHost.Controls.Add(help);
        help.ShowTopic("1239");
        contentHost.Show(owner);
        await help.Ready.WaitAsync(TimeSpan.FromSeconds(45));
        await Loaded("/parametric_methods/paired_t.htm");
        await Until("!!document.querySelector('.MCDropDownHotSpot') && document.querySelector('.MCDropDownBody').offsetHeight === 0");
        Check((await Js("document.body.innerText.includes('Paired')")).GetBoolean(), "context topic renders in WebView2");
        await Js("document.querySelector('.MCDropDownHotSpot').click()");
        await Until("document.querySelector('.MCDropDownBody').offsetHeight > 0");
        Check((await Js("document.querySelector('pre.rcode').textContent.includes('t.test')")).GetBoolean(), "R code expands and retains executable example text");
        await Until("[...document.images].filter(i=>!i.src.endsWith('transparent.gif')).every(i=>i.complete && i.naturalWidth > 0)");
        Check(true, "topic images and generated equations load offline");
        using (var png = File.Create(Path.Combine(output, "paired-t-help.png"))) await help.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        var link = await Js("[...document.querySelectorAll('a')].find(a=>a.href.includes('/references/') && a.hash)?.getAttribute('href')");
        Check(link.ValueKind == JsonValueKind.String, "topic includes a reference link to a specific anchor");
        await Js("[...document.querySelectorAll('a')].find(a=>a.href.includes('/references/') && a.hash).click()");
        await Until("location.pathname.includes('/references/') && location.hash.length > 1 && !!document.getElementById(decodeURIComponent(location.hash.slice(1)))");
        Check(true, "reference link opens its anchored entry in local references");
        help.Browser.CoreWebView2.GoBack();
        await Loaded("/parametric_methods/paired_t.htm");
        Check(help.Browser.CoreWebView2.CanGoForward, "back/forward history retains the analysis topic");
        help.Search("paired t");
        await Until("location.pathname === '/search.htm' && document.body.innerText.toLowerCase().includes('paired') && document.querySelectorAll('#resultList li, .search-result').length > 0");
        Check(true, "full-text search returns offline results");
        using (var png = File.Create(Path.Combine(output, "search-help.png"))) await help.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        await Js("document.querySelector('#resultList a, .search-result a').click()");
        await Until("location.pathname !== '/search.htm' && document.readyState === 'complete'");
        Check(true, "search result opens its help topic");
        help.ShowTopic("1239");
        await Loaded("/parametric_methods/paired_t.htm");
        string external = null;
        help.ExternalLinkRequested += url => external = url;
        // A real browser click has IsUserInitiated; script navigation must not launch a browser.
        await Js("document.body.insertAdjacentHTML('afterbegin','<a id=external-test href=https://doi.org/10.1000/test target=_blank style=\"position:fixed;left:5px;top:5px;z-index:999999;background:white\">Reference</a>')");
        await help.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", "{\"type\":\"mousePressed\",\"x\":25,\"y\":12,\"button\":\"left\",\"clickCount\":1}");
        await help.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", "{\"type\":\"mouseReleased\",\"x\":25,\"y\":12,\"button\":\"left\",\"clickCount\":1}");
        await Task.Delay(300);
        Check(external == "https://doi.org/10.1000/test" && HelpCatalog.IsLocal(help.Browser.CoreWebView2.Source), "external reference opens through the system browser boundary and leaves help in place");
        await Js("window.fetchBlocked=null;fetch('https://example.com/tracking').then(r=>window.fetchBlocked=!r.ok).catch(()=>window.fetchBlocked=true)");
        await Until("window.fetchBlocked === true");
        Check(true, "external resource requests are blocked");
        help.ShowTopic("999999");
        await Loaded("/contents.htm");
        Check(true, "unavailable context falls back to contents");
        contentHost.Close();
        using var service = new HelpService();
        using var ribbon = new Panel { Dock = DockStyle.Top, Height = 60 };
        owner.Controls.Add(ribbon);
        using var worksheet = new Form { MdiParent = owner, Text = "Worksheet", WindowState = FormWindowState.Maximized };
        var cells = new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = "Selected data range" };
        worksheet.Controls.Add(cells);
        worksheet.Show();
        cells.Focus();
        cells.Select(2, 8);
        var mdi = owner.Controls.OfType<MdiClient>().Single();
        int fullWidth = mdi.Width;
        service.Show(owner, "1239", assets);
        help = service.View;
        await help.Ready.WaitAsync(TimeSpan.FromSeconds(45));
        await Loaded("/parametric_methods/paired_t.htm");
        Check(service.IsDocked && service.DockHost.Panel.Visible && service.Window == null && mdi.Width < fullWidth, "help docks on the right and reserves real space beside the MDI workspace");
        Check(mdi.Right <= service.DockHost.Divider.Left && service.DockHost.Divider.Right <= service.DockHost.Panel.Left && ribbon.Width == owner.ClientSize.Width && service.DockHost.Panel.Top >= ribbon.Bottom, "help and its divider do not cover documents, menus or the full-width operation ribbon");
        Check(owner.ActiveMdiChild == worksheet && cells.SelectionStart == 2 && cells.SelectionLength == 8 && cells.Focused, "opening help preserves active MDI document, focus and selected range");
        service.Show(owner, "1040", assets);
        await Loaded("/basics/worksheet.htm");
        service.Show(owner, "1239", assets);
        await Loaded("/parametric_methods/paired_t.htm");
        Check(ReferenceEquals(help, service.View), "repeat context requests reuse the same browser view");
        await Js("document.querySelector('.MCDropDownHotSpot').click(); window.dockTestToken='persistent'; document.querySelector('pre.rcode').scrollIntoView();");
        await Until("document.querySelector('.MCDropDownBody').offsetHeight > 0");
        var browser = help.Browser.CoreWebView2;
        await service.ToggleModeAsync();
        await Task.Delay(150);
        Check(!service.IsDocked && service.Window?.Owner == owner && mdi.Width == fullWidth, "Pop out restores full workspace width and opens one owned help window");
        Check(Screen.FromControl(owner).WorkingArea.Contains(service.Window.Bounds), "floating help fits its monitor after DPI scaling");
        service.Window.Location = new Point(-15000, -15000);
        Check(ReferenceEquals(browser, help.Browser.CoreWebView2) && (await Js("window.dockTestToken === 'persistent' && document.querySelector('.MCDropDownBody').offsetHeight > 0")).GetBoolean(), "Pop out preserves the browser, page and expanded R example without reload");
        await service.ToggleModeAsync();
        await Task.Delay(150);
        Check(service.IsDocked && service.Window == null && help.Browser.CoreWebView2.CanGoBack && (await Js("window.dockTestToken === 'persistent'")).GetBoolean(), "Dock returns the same page/session/history to the workspace");
        Check((await Js("window.__sdHelpPosition.length > 0 && window.__sdHelpPosition.every(p=>Math.abs(p.e.scrollTop-Math.min(p.y,Math.max(0,p.e.scrollHeight-p.e.clientHeight)))<1)")).GetBoolean(), "Dock restores the original reading position, clamping only containers that no longer scroll");
        int widthBeforeDrag = service.DockHost.Panel.Width;
        service.DockHost.Divider.SplitPosition = widthBeforeDrag - 100;
        int preferredWidth = service.DockHost.Panel.Width;
        Check(preferredWidth == widthBeforeDrag - 100 && mdi.Right <= service.DockHost.Divider.Left, $"divider resizes the help pane and worksheet together ({widthBeforeDrag} -> {preferredWidth}, split {service.DockHost.Divider.SplitPosition}, layout {mdi.Bounds} / {service.DockHost.Divider.Bounds} / {service.DockHost.Panel.Bounds})");
        service.Hide();
        Check(!service.IsVisible && !service.DockHost.Panel.Visible && !service.DockHost.Divider.Visible && mdi.Width == fullWidth, "Close hides help and its divider and restores the workspace");
        service.Show(owner, "1239", assets);
        Check(service.IsDocked && service.DockHost.Panel.Width == preferredWidth && ReferenceEquals(browser, help.Browser.CoreWebView2), "reopening docked help retains its width and browser session");
        await service.ToggleModeAsync();
        service.Window.Location = new Point(-15000, -15000);
        service.Window.Close();
        Check(!service.IsVisible && !help.IsDisposed, "floating window X hides help without disposing its session");
        service.Show(owner, "1239", assets);
        Check(service.Window != null && !service.IsDocked, "reopening respects the user's floating preference for this session");
        service.Window.Location = new Point(-15000, -15000);
        await service.ToggleModeAsync();
        Check(service.IsDocked, "a previously closed floating frame can dock again");
        // Do not enter a native modal loop on a WebView2 completion callback.
        // Real parameter dialogs originate from the application's UI commands.
        await Task.Delay(50);
        using var modal = new Form { Text = "Parameter dialog", StartPosition = FormStartPosition.Manual, Location = new Point(-14000, -14000), ShowInTaskbar = false };
        var parameterInput = new TextBox { Dock = DockStyle.Top, Text = "Parameter value" };
        modal.Controls.Add(parameterInput);
        Exception modalError = null;
        modal.Shown += async (_, _) =>
        {
            try
            {
                parameterInput.Focus();
                await Task.Delay(100);
                Check(service.IsTemporary && service.Window?.Owner == modal, "visible docked help automatically floats when a modal dialog opens");
                Check(parameterInput.Focused, "automatic floating leaves keyboard focus in the parameter dialog");
                service.Show(owner, "1239", assets);
                help = service.View;
                service.Window.Location = new Point(-15000, -15000);
                await help.Ready.WaitAsync(TimeSpan.FromSeconds(45));
                await Loaded("/parametric_methods/paired_t.htm");
                Check(help.Enabled && service.Window.Owner == modal && modal.Enabled && service.IsTemporary, "help temporarily floats and stays interactive alongside a modal parameter dialog");
                await Js("if (document.querySelector('.MCDropDownBody').offsetHeight === 0) document.querySelector('.MCDropDownHotSpot').click()");
                await Until("document.querySelector('.MCDropDownBody').offsetHeight > 0");
                Check(true, "R dropdown remains usable during a modal dialog");
            }
            catch (Exception ex) { modalError = ex; }
            finally { modal.Close(); }
        };
        modal.ShowDialog(owner);
        if (modalError != null) throw modalError;
        await Task.Delay(150);
        Check(!help.IsDisposed && service.IsDocked && service.Window == null && ReferenceEquals(browser, help.Browser.CoreWebView2), "closing the dialog returns the same help session to the dock");
        Check((await Js("window.dockTestToken === 'persistent'")).GetBoolean(), "modal help roundtrip preserves the live document");
        service.Dispose();
        Check(help.IsDisposed && mdi.Width == fullWidth, "disposing help releases the browser, dock and divider");
    }
}

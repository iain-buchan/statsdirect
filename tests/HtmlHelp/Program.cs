using Microsoft.Web.WebView2.Core;
using StatsDirect.UI.HtmlHelp;
using System.Text.Json;
using System.Reflection;
using System.Xml.Linq;

internal static class Program
{
    private static int checks;
    private static HelpView help;
    private static string output;
    private static string helpDirectory;
    private static bool checkReaderEscape;
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
    private static async Task Loaded(string path) => await Until($"location.pathname === {JsonSerializer.Serialize(path)} && document.readyState === 'complete' && (typeof StatsDirectHelp !== 'undefined' || typeof MadCap !== 'undefined')");
    private static string VisibleFocus(string link) => $$"""
        (() => {
            const a = document.querySelector('#contents a[data-link="{{link}}"]'), n = document.getElementById('help-navigation');
            if (!a || !n || document.activeElement !== a || !a.getClientRects().length) return false;
            const r = a.getBoundingClientRect(), b = n.getBoundingClientRect();
            return r.top >= Math.max(0,b.top)-1 && r.bottom <= Math.min(innerHeight,b.bottom)+1;
        })()
        """;
    private static async Task NativeUntil(Func<bool> condition)
    {
        for (int i=0;i<100;i++) { if(condition()) return; await Task.Delay(50); }
        throw new Exception("Timed out waiting for native help controls");
    }
    private static async Task Browse(ToolStripButton button, string link)
    {
        await NativeUntil(() => button.Enabled);
        button.PerformClick();
        await Until(VisibleFocus(link));
    }
    private static string StatusText => help.Controls.OfType<StatusStrip>().Single().Items[0].Text;
    private static bool DialogKey(Control target, Keys key) => (bool)typeof(Control)
        .GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target,[key]);
    private static async Task EscapeToTopic()
    {
        await help.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyDown\",\"key\":\"Escape\",\"windowsVirtualKeyCode\":27}");
        await help.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyUp\",\"key\":\"Escape\",\"windowsVirtualKeyCode\":27}");
        await Until("document.activeElement === document.getElementById('topic') && !document.body.classList.contains('navigation-open')");
    }
    private static void NativeKey(Control target, Keys key)
    {
        var message=Message.Create(target.Handle,0x0100,(IntPtr)(int)key,IntPtr.Zero);
        if(!target.PreProcessMessage(ref message)) typeof(Control).GetMethod("WndProc",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,[message]);
    }
    private static async Task RememberPage() => _=await Js("window.browseSnapshot={url:location.href,history:history.length};window.browseToken='retained'");
    private static async Task CheckUnchanged(string description) => Check((await Js("location.href===window.browseSnapshot.url && history.length===window.browseSnapshot.history && window.browseToken==='retained'")).GetBoolean(),description);
    private static async Task CheckBrowseFailuresAndFallbacks(ToolStripButton browse, ToolStripButton home)
    {
        const string paired="parametric_methods/paired_t.htm";
        await Browse(browse,paired);
        await RememberPage();
        await Js("for(let p=document.querySelector('#contents a[aria-current=\"page\"]').parentElement;p&&p.id!=='contents';p=p.parentElement)if(p.tagName==='DETAILS')p.open=false");
        Check((await Js("(() => {const ancestors=[];for(let p=document.querySelector('#contents a[aria-current=\"page\"]').parentElement;p&&p.id!=='contents';p=p.parentElement)if(p.tagName==='DETAILS')ancestors.push(p);return ancestors.length>0 && ancestors.every(p=>!p.open)})()")).GetBoolean(),"the current topic has closed ancestor branches before Browse");
        await Browse(browse,paired);
        await CheckUnchanged("Browse reopens collapsed current-topic ancestors and focuses the visible entry without URL, history or document changes");
        if(checkReaderEscape)
        {
            Check((await Js("innerWidth<1280")).GetBoolean(),"fresh reader Escape is exercised in the docked layout");
            await EscapeToTopic();
            Check(true,"Escape after native Browse closes the docked tree and focuses the topic instead of the hidden reader toggle");
        }

        help.ShowTopic("graphics/markers.htm"); await Loaded("/graphics/markers.htm");
        Check((await Js("!document.querySelector('#contents a[aria-current=\"page\"]')")).GetBoolean(),"Markers is absent from the tree for the same-folder fallback test");
        await RememberPage(); await Browse(browse,"graphics/graphics.htm");
        await CheckUnchanged("An unlisted graphics topic focuses its same-folder Overview without navigating");
        help.ShowTopic("menus/analysis_options.htm"); await Loaded("/menus/analysis_options.htm");
        await RememberPage(); await Browse(browse,"contents.htm");
        await CheckUnchanged("An unlisted topic with no same-folder branch focuses Contents without navigating");

        help.ShowTopic("1239"); await Loaded("/"+paired); await Browse(browse,paired);
        await RememberPage();
        await Js("window.browseTarget=document.querySelector('#contents a[aria-current=\"page\"]');document.activeElement.blur();document.getElementById('topic').tabIndex=-1;document.getElementById('topic').focus();window.browseTarget.focus=()=>{}");
        browse.PerformClick(); await NativeUntil(()=>StatusText.StartsWith("Could not open the help topics"));
        await CheckUnchanged("A failed focus confirmation reports a status message without navigating");
        await Js("delete window.browseTarget.focus"); await Browse(browse,paired);
        Check(StatusText=="","successful Browse clears its earlier failure notice");
        await RememberPage();
        await Js("window.browseGetElementById=document.getElementById;document.getElementById=function(id){if(id==='contents')throw new Error('deliberate Browse script failure');return window.browseGetElementById.call(this,id)}");
        browse.PerformClick(); await NativeUntil(()=>StatusText.StartsWith("Could not open the help topics"));
        await CheckUnchanged("A script exception/null result reports failure without replacing the reading page");
        await Js("document.getElementById=window.browseGetElementById"); await Browse(browse,paired);
        await RememberPage();
        await Js("document.querySelectorAll('#contents a').forEach(a=>a.remove())");
        browse.PerformClick(); await NativeUntil(()=>StatusText.StartsWith("Could not open the help topics"));
        await CheckUnchanged("An existing tree with no links reports failure without falling back to the reader");

        help.ShowTopic("1000"); await Loaded("/contents.htm"); help.ShowTopic("1239"); await Loaded("/"+paired);
        await RememberPage();
        await Js("document.getElementById('contents').remove();history.replaceState(null,'','/index.html')");
        await NativeUntil(()=>new Uri(help.Browser.CoreWebView2.Source).AbsolutePath=="/index.html");
        await RememberPage(); browse.PerformClick(); await NativeUntil(()=>StatusText.StartsWith("Could not open the help topics"));
        await CheckUnchanged("Missing contents on index.html reports failure rather than entering a reader reload loop");
        help.ShowTopic("1239"); await Loaded("/"+paired);
        var navigations=new List<string>();
        void RecordNavigation(object sender,CoreWebView2NavigationStartingEventArgs e)=>navigations.Add(e.Uri);
        help.Browser.CoreWebView2.NavigationStarting+=RecordNavigation;
        try
        {
            await Js("document.getElementById('contents').remove();window.noContentsToken='removed'");
            await NativeUntil(()=>browse.Enabled); browse.PerformClick();
            await NativeUntil(()=>navigations.Any(uri=>new Uri(uri).AbsolutePath=="/index.html" && Uri.UnescapeDataString(new Uri(uri).Fragment).Contains("topic="+paired)));
            await Loaded("/"+paired); await Until("window.noContentsToken===undefined");
            Check(true,"Only a missing contents tree opens index.html with the current topic preserved");
        }
        finally {help.Browser.CoreWebView2.NavigationStarting-=RecordNavigation;}

        help.Search("paired t"); await Until("document.querySelector('#results')?.hidden===false && document.querySelectorAll('a.result').length>0");
        browse.PerformClick(); await Until("document.getElementById('results').hidden && !document.getElementById('topic').hidden && !!document.activeElement.closest('#contents')");
        Check(true,"Browse after search returns to Contents and reveals the topic even when Flare did not set contents.hidden");
        help.ShowTopic("1239"); await Loaded("/"+paired); await NativeUntil(()=>browse.Enabled);

        var strips=help.Controls.OfType<ToolStrip>().Where(strip=>strip is not StatusStrip).ToArray();
        var heading=strips.Single(strip=>strip.Items.OfType<ToolStripButton>().Any(button=>button.Text=="Close"));
        var toolbar=browse.Owner;
        var searchbar=strips.Single(strip=>strip.Items.OfType<ToolStripTextBox>().Any());
        heading.Focus(); DialogKey(help,Keys.Tab); await NativeUntil(()=>toolbar.ContainsFocus);
        DialogKey(help,Keys.Tab); await NativeUntil(()=>searchbar.ContainsFocus);
        DialogKey(help,Keys.Tab); await NativeUntil(()=>help.Browser.ContainsFocus);
        DialogKey(help,Keys.Shift|Keys.Tab); await NativeUntil(()=>searchbar.ContainsFocus);
        DialogKey(help,Keys.Shift|Keys.Tab); await NativeUntil(()=>toolbar.ContainsFocus);
        DialogKey(help,Keys.Shift|Keys.Tab); await NativeUntil(()=>heading.ContainsFocus);
        Check(true,"Native Tab and Shift+Tab traverse heading, toolbar, search and reader in visual order");
        toolbar.Focus(); home.Select(); NativeKey(toolbar,Keys.Right);
        Check(browse.Selected,"Right arrow reaches Browse topics from Home on the native toolbar");
        NativeKey(toolbar,Keys.Enter); await Until(VisibleFocus(paired));
        Check(true,"Enter on keyboard-selected Browse topics opens and focuses the current tree entry");
    }
    private static async Task CheckWideBrowse(HelpService service,ToolStripButton browse)
    {
        await service.ToggleModeAsync();
        service.Window.Location=new Point(-15000,-15000);
        service.Window.ClientSize=new Size((int)Math.Ceiling(1450*help.DeviceDpi/96d),700);
        await Until("innerWidth>=1280");
        help.ShowTopic("references/support.htm"); await Loaded("/references/support.htm");
        await Js("document.querySelectorAll('#contents details').forEach(d=>d.open=true);document.getElementById('help-navigation').scrollTop=0");
        Check((await Js("document.querySelector('#contents a[aria-current=\"page\"]').getBoundingClientRect().top>document.getElementById('help-navigation').getBoundingClientRect().bottom")).GetBoolean(),"wide-reader current entry starts below the visible contents viewport");
        await RememberPage(); await Browse(browse,"references/support.htm");
        await CheckUnchanged("Wide Browse scrolls a low current-topic entry into view and focuses it without navigation");
        if(checkReaderEscape)
        {
            await EscapeToTopic();
            Check(true,"Escape after native Browse focuses the topic in the wide reader with no overlay open");
        }
        await service.ToggleModeAsync();
    }
    private static async Task CheckDesktopChrome(string surface) =>
        Check((await Js("![...document.querySelectorAll('body > header:has(> .help-identity), #search-form')].some(element => element.getClientRects().length > 0)")).GetBoolean(),
            $"{surface} uses the native controls without a website banner or search bar");
    [STAThread]
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args.FirstOrDefault(x => !x.StartsWith("--")) ?? Path.Combine(AppContext.BaseDirectory, "artifacts"));
        int sourceArgument = Array.IndexOf(args,"--help-directory");
        if(sourceArgument >= 0) helpDirectory=Path.GetFullPath(args[sourceArgument+1]);
        checkReaderEscape=args.Contains("--check-reader-escape");
        if(checkReaderEscape && helpDirectory == null) throw new ArgumentException("--check-reader-escape requires --help-directory with freshly generated reader assets.");
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
        string assets = helpDirectory ?? Path.Combine(AppContext.BaseDirectory, "Help");
        var catalog = new HelpCatalog(assets);
        var invalid = catalog.Topics.Where(t => catalog.Resolve(t.Key) == null).ToArray();
        Check(invalid.Length == 0, $"all {catalog.Topics.Count} numeric and named aliases resolve to packaged topics: {string.Join(",", invalid.Select(x => x.Key))}");
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var ids = Directory.GetFiles(Path.Combine(repository, "StatsDirectUI/Assets/Operations"), "*.xml")
            .SelectMany(file => XDocument.Load(file).Descendants().Attributes("chm-id").Select(a => a.Value)).Distinct().ToArray();
        Check(ids.Length > 150 && ids.All(id => catalog.Resolve(id) != null), $"all {ids.Length} distinct operation context IDs have offline topics");
        Check(catalog.Resolve("1239").AbsolutePath == "/parametric_methods/paired_t.htm", "paired t-test context ID preserves its topic");
        Check(catalog.Resolve("1401")?.AbsolutePath == "/basics/r_script_window.htm", "the R script window's own topic, which its Help shows while the window is empty, is in the bundle");
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
        await Until("!!document.querySelector('details.rcode > summary') && !document.querySelector('details.rcode').open");
        Check((await Js("document.body.innerText.includes('Paired')")).GetBoolean(), "context topic renders in WebView2");
        await CheckDesktopChrome("The context topic");
        await Js("document.querySelector('details.rcode > summary').click()");
        await Until("document.querySelector('details.rcode').open && document.querySelector('pre.rcode').offsetHeight > 0");
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
        await Until("location.hash.includes('searchQuery=') && document.body.innerText.toLowerCase().includes('paired') && document.querySelectorAll('a.result').length > 0");
        Check(true, "full-text search returns offline results");
        await CheckDesktopChrome("The search results");
        using (var png = File.Create(Path.Combine(output, "search-help.png"))) await help.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        await Js("document.querySelector('a.result').click()");
        await Until("(!location.pathname.endsWith('/index.html') || new URLSearchParams(location.hash.slice(1)).has('topic')) && document.readyState === 'complete'");
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
        using var service = new HelpService(Path.Combine(output,"service-profile"));
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
        await CheckDesktopChrome("Docked help");
        var buttons = help.Controls.OfType<ToolStrip>().SelectMany(strip => strip.Items.OfType<ToolStripButton>()).ToArray();
        var home = buttons.Single(button => button.Text == "Home");
        var browse = buttons.Single(button => button.Text == "Browse topics");
        Check(Array.IndexOf(buttons, browse) == Array.IndexOf(buttons, home) + 1, "Home and Browse topics are adjacent native toolbar buttons");
        await CheckBrowseFailuresAndFallbacks(browse, home);
        await Js("window.browseTestToken = 'retained'");
        await Browse(browse,"parametric_methods/paired_t.htm");
        const string contentsLink = "document.querySelector('#contents a[data-link=\"contents.htm\"]')";
        await Until(contentsLink + "?.getClientRects().length > 0 && !!document.activeElement.closest('#contents')");
        Check((await Js("location.pathname === '/parametric_methods/paired_t.htm' && window.browseTestToken === 'retained' && document.activeElement === document.querySelector('#contents a[aria-current=\"page\"]')")).GetBoolean(),
            "Native Browse topics focuses the visible current topic without replacing the reading page");
        const string distributions = "[...document.querySelectorAll('#contents summary')].find(summary => summary.textContent.trim() === 'Probability distributions')";
        const string distributionOverview = "document.querySelector('#contents a[data-link=\"distributions/distributions.htm\"]')";
        const string normalDistribution = "document.querySelector('#contents a[data-link=\"distributions/normal.htm\"]')";
        await Until(distributions + "?.getClientRects().length > 0");
        await Js(distributions + ".click()");
        await Until(distributions + ".parentElement.open && " + normalDistribution + "?.getClientRects().length > 0");
        Check((await Js("location.pathname === '/parametric_methods/paired_t.htm' && window.browseTestToken === 'retained' && document.body.classList.contains('navigation-open')")).GetBoolean(),
            "Clicking the Probability distributions title expands its topics without leaving the current topic or closing the tree");
        Check((await Js($$"""
            (() => {
                const summary = {{distributions}}, overview = {{distributionOverview}};
                return !summary.querySelector('a') && overview?.textContent === 'Overview' &&
                    overview.getAttribute('aria-label') === 'Probability distributions overview' &&
                    summary.parentElement.querySelector('ul > li:first-child > a') === overview;
            })()
            """)).GetBoolean(), "The branch title is an expander and its page remains the first Overview link");
        await Js(normalDistribution + ".click()");
        await Loaded("/distributions/normal.htm");
        home.PerformClick();
        await Loaded("/contents.htm");
        Check(true, "A child topic opens from the expanded tree and native Home opens the welcome page");
        service.Show(owner, "1040", assets);
        await Loaded("/basics/worksheet.htm");
        service.Show(owner, "1239", assets);
        await Loaded("/parametric_methods/paired_t.htm");
        Check(ReferenceEquals(help, service.View), "repeat context requests reuse the same browser view");
        await Js("document.querySelector('details.rcode > summary').click(); window.dockTestToken='persistent'; document.querySelector('pre.rcode').scrollIntoView();");
        await Until("document.querySelector('details.rcode').open && document.querySelector('pre.rcode').offsetHeight > 0");
        var browser = help.Browser.CoreWebView2;
        await service.ToggleModeAsync();
        await Task.Delay(150);
        Check(!service.IsDocked && service.Window?.Owner == owner && mdi.Width == fullWidth, "Pop out restores full workspace width and opens one owned help window");
        Check(Screen.FromControl(owner).WorkingArea.Contains(service.Window.Bounds), "floating help fits its monitor after DPI scaling");
        service.Window.Location = new Point(-15000, -15000);
        Check(ReferenceEquals(browser, help.Browser.CoreWebView2) && (await Js("window.dockTestToken === 'persistent' && document.querySelector('details.rcode').open && document.querySelector('pre.rcode').offsetHeight > 0")).GetBoolean(), "Pop out preserves the browser, page and expanded R example without reload");
        await CheckDesktopChrome("Floating help");
        await service.ToggleModeAsync();
        await Task.Delay(150);
        Check(service.IsDocked && service.Window == null && help.Browser.CoreWebView2.CanGoBack && (await Js("window.dockTestToken === 'persistent'")).GetBoolean(), "Dock returns the same page/session/history to the workspace");
        // the program puts the reading position back in the second animation frame after the dock, which comes later on a busy machine
        string positionRestored = "window.__sdHelpPosition.length > 0 && window.__sdHelpPosition.every(p=>Math.abs(p.e.scrollTop-Math.min(p.y,Math.max(0,p.e.scrollHeight-p.e.clientHeight)))<1)";
        await Until(positionRestored);
        Check((await Js(positionRestored)).GetBoolean(), "Dock restores the original reading position, clamping only containers that no longer scroll");
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
                await Js("if (!document.querySelector('details.rcode').open) document.querySelector('details.rcode > summary').click()");
                await Until("document.querySelector('details.rcode').open && document.querySelector('pre.rcode').offsetHeight > 0");
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
        await CheckWideBrowse(service,browse);
        service.Dispose();
        Check(help.IsDisposed && mdi.Width == fullWidth, "disposing help releases the browser, dock and divider");
    }
}

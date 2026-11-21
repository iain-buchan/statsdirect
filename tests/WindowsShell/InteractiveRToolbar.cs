using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckInteractiveRToolbar(frmMain main, ToolStrip gridToolbar)
    {
        using var script = (Form)Call(main, "CreateScriptWindow");
        Call(script, "ShowR", "original", "Integrated toolbar check", null, false);
        var view = RConsole(script);
        await ((Task)Get(view, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        var browser = (WebView2)Get(view, "Browser");
        async Task<string> JS(string source) => await browser.CoreWebView2.ExecuteScriptAsync(source);
        string[] ids = JsonSerializer.Deserialize<string[]>(await JS("[...document.querySelectorAll('.toolbar>button')].map(b=>b.id)"))!;
        Check(!gridToolbar.Visible && !((ToolStrip)Field(script, "ToolStrip1")).Visible && ((Control)view).Visible
            && ids.SequenceEqual(new[] { "save", "run", "stop", "restart", "ai", "open-in-r", "help", "windows-more" })
            && await JS("document.querySelectorAll('[role=toolbar]').length") == "1",
            "R has exactly one visible shared toolbar in Save, Run, Stop, Restart, AI, R+, Help order with More last");
        Check(!((Control)Field(script, "pnlActions")).Visible && Get(view, "Session") == null,
            "the R toolbar replaces the native command row and an unrun script has no R worker");
        var openInR = view.GetType().GetField("OpenInRRequested", Internal)!;
        var launch = openInR.GetValue(view);
        Check(launch is Action, "the R window subscribes to the native Open script in R command");
        try
        {
            foreach (var mode in new[] { "script", "console" })
            {
                await JS($"document.getElementById('{mode}-tab').click()");
                const string buttonVisible = """
                    (() => {
                        const button = document.getElementById('open-in-r');
                        return !button.hidden && !button.disabled && button.getClientRects().length > 0
                            && button.tabIndex === 0 && button.textContent.trim() === 'R+'
                            && button.title === 'Open script in R or RStudio'
                            && button.getAttribute('aria-label') === button.title;
                    })()
                    """;
                string visible = await JS(buttonVisible);
                for (int wait = 0; wait < 40 && visible != "true"; wait++)
                {
                    await Task.Delay(50);
                    visible = await JS(buttonVisible);
                }
                Check(visible == "true" && await JS($"document.getElementById('{mode}-tab').getAttribute('aria-selected')") == "\"true\"",
                    $"R+ is visible and keyboard-accessible with the Mac tooltip in {mode} mode");

                // Exercise the real browser-to-native command without opening R or RStudio.
                var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                openInR.SetValue(view, (Action)(() => requested.TrySetResult()));
                await JS("document.getElementById('open-in-r').click()");
                Check(await Task.WhenAny(requested.Task, Task.Delay(TimeSpan.FromSeconds(5))) == requested.Task,
                    $"R+ dispatches the native Open script in R command from {mode} mode");
            }
        }
        finally { openInR.SetValue(view, launch); }
        Check(await JS("StatsDirectRHost.setScript('   ');const b=document.getElementById('open-in-r');!b.hidden&&b.disabled") == "true",
            "R+ stays visible but is disabled when the script is empty, as on Mac");
        await JS("StatsDirectRConsole.setMode('script');StatsDirectRHost.setScript('original');const e=document.getElementById('script-source');e.focus();e.setSelectionRange(e.value.length,e.value.length);StatsDirectRHost.edit('insertText',' edited')");
        await JS("StatsDirectRHost.edit('undo')");
        Check(JsonSerializer.Deserialize<string>(await JS("document.getElementById('script-source').value")) == "original",
            "the integrated R Script view undoes an editor insertion");
        await JS("StatsDirectRHost.edit('redo')");
        Check(JsonSerializer.Deserialize<string>(await JS("document.getElementById('script-source').value")) == "original edited",
            "the integrated R Script view redoes an editor insertion");
        string saved = Path.Combine(Path.GetTempPath(), "StatsDirect-R-toolbar-" + Guid.NewGuid().ToString("N") + ".R");
        try
        {
            script.GetType().GetField("currentFile", Internal)!.SetValue(script, saved);
            await JS("document.getElementById('save').click()");
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!File.Exists(saved) && DateTime.UtcNow < until) await Task.Delay(30);
            Check(File.Exists(saved) && File.ReadAllText(saved) == "original edited",
                "the integrated R toolbar Save writes current browser Script contents");
        }
        finally { if (File.Exists(saved)) File.Delete(saved); }
        Call(script, "EditFind");
        Check((bool)Get(script, "FindBarShown"), "the integrated R window retains the existing Find bar");
        var oldWidth = main.Width; var oldMinimum = main.MinimumSize;
        try
        {
            main.MinimumSize = Size.Empty; main.Width = main.LogicalToDeviceUnits(290);
            // the page lays out after the resize: wait until the toolbar overflows, up to two seconds
            for (int wait = 0; wait < 40 && await JS("(()=>{const t=document.querySelector('.toolbar');return t.scrollWidth>t.clientWidth+1})()") != "true"; wait++) await Task.Delay(50);
            // More must be reachable, not merely styled: the toolbar overflows at this width, scrolls to its end, and More is hit-testable there
            Check(await JS("(()=>{const t=document.querySelector('.toolbar'),b=document.getElementById('windows-more');if(!b||b.getAttribute('aria-label')!=='More R session commands')return false;if(t.scrollWidth<=t.clientWidth+1)return false;t.scrollLeft=t.scrollWidth;const r=b.getBoundingClientRect(),tr=t.getBoundingClientRect();if(r.width<=0||r.left<tr.left-1||r.right>tr.right+1)return false;const hit=document.elementFromPoint((r.left+r.right)/2,(r.top+r.bottom)/2);return b===hit||b.contains(hit);})()") == "true",
                "the narrow R toolbar scrolls to an accessible, hit-testable More");
        }
        finally { main.Width = oldWidth; main.MinimumSize = oldMinimum; }
    }
}

using Microsoft.Web.WebView2.Core;
using StatsDirect.UI.WebReports;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    // The Script AI conversation page: Markdown rendered with nothing from a reply reaching the page as markup, code blocks with their
    // buttons, links to http(s) only, and a streamed reply that the view follows while the reader is at its end and leaves alone when
    // they have scrolled up or selected earlier text.
    private static async Task CheckConversation()
    {
        var form = view.FindForm()!;
        using var page = new ConversationView(System.IO.Path.Combine(AppContext.BaseDirectory, "Tutor"), System.IO.Path.Combine(output, "tutor-profile")) { Width = 420, Height = 220, Left = 0, Top = 0 };
        form.Controls.Add(page); page.BringToFront();
        string opened = null; page.OpenLink = url => opened = url;
        var code = new List<(string what, string code)>(); var positions = new List<(int message, int block)>();
        page.CodeRequested += (what, text, message, block) => { code.Add((what, text)); positions.Add((message, block)); };
        await page.Ready.WaitAsync(TimeSpan.FromSeconds(45));
        async Task<JsonElement> Page(string script) => JsonDocument.Parse(await page.Browser.CoreWebView2.ExecuteScriptAsync(script)).RootElement.Clone();
        async Task<bool> PageTrue(string script) => (await Page(script)).ValueKind == JsonValueKind.True;
        JsonArray Messages(params (string role, string text)[] items) => new(items.Select(i => (JsonNode)new JsonObject { ["role"] = i.role, ["text"] = i.text }).ToArray());

        // Markdown as the tutor writes it, with markup and a script in the text, which must stay text
        string reply = "### Other output\n- **Limits of agreement: −10.9 to 123.1.** These are wide.\n- A second point with `code`.\n\n```r\nt.test(x, y, paired = TRUE)\n```\n\n| Term | Value |\n|---|---|\n| Mean | 56.1 |\n\n<script>alert(1)</script> and a [reference](https://example.org/paired) and a [bad link](javascript:alert(2)).";
        page.Update(Messages(("user", "Why is this **paired**?"), ("assistant", reply)), "", true);
        await page.SettledAsync();
        Check(await PageTrue("document.querySelectorAll('.message').length===2 && document.querySelector('.message.user strong').textContent==='paired' && document.querySelector('.message.assistant h3').textContent==='Other output'"), "a question and a reply render as blocks with their headings and emphasis");
        Check(await PageTrue("document.querySelector('.message.assistant li strong').textContent==='Limits of agreement: −10.9 to 123.1.' && document.querySelector('.message.assistant li code').textContent==='code'"), "list items keep their emphasis and code spans, and the minus sign is itself");
        Check(await PageTrue("document.querySelector('.code-block pre code').textContent==='t.test(x, y, paired = TRUE)' && document.querySelector('.code-actions span').textContent==='R' && document.querySelector('.code-block button[data-code=open]')!==null && document.querySelector('.code-block button[data-code=copy]')!==null"), "an R code block shows its code with Copy and Open in R");
        Check(await PageTrue("document.querySelector('.message.assistant table td').textContent==='Mean' && document.querySelectorAll('.message.assistant table th').length===2"), "a table renders with its header row");
        Check(await PageTrue("document.querySelectorAll('script').length===1 && document.querySelector('#conversation').innerText.includes('<script>alert(1)</script>')"), "markup in a reply is shown as text, not run");
        Check(await PageTrue("document.querySelectorAll('.message a').length===1 && document.querySelector('.message a').getAttribute('href')==='https://example.org/paired' && document.querySelector('.message a').target==='_blank' && document.querySelector('#conversation').innerText.includes('[bad link](javascript:alert(2))')"), "a link to an https address keeps its address and opens in a new window; a javascript link stays text");
        page.Height = 760; await Task.Delay(100); await Page("conversation.scrollTo(0)");
        using (var png = File.Create(System.IO.Path.Combine(output, "conversation-rendered.png"))) await page.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
        page.Height = 220; await Task.Delay(100);
        // the code buttons reach the program with the code
        await Page("document.querySelector('.code-block button[data-code=open]').click(); document.querySelector('.code-block button[data-code=copy]').click()");
        await Task.Delay(150);
        Check(code.Count == 2 && code[0] == ("open", "t.test(x, y, paired = TRUE)") && code[1].what == "copy" && code[1].code == code[0].code, "Open in R and Copy give the program the block's code");
        Check(await PageTrue("document.querySelector('.code-block button[data-code=copy]').textContent==='Copied'"), "Copy says it has copied");
        Check(positions[0] == (1, 0), "Open in R identifies the original reply and R block for its data context");
        int priorRequests = code.Count;
        await Page("window.chrome.webview.postMessage({action:'code',what:'open',code:'mean(before)',messageIndex:1,blockIndex:0,context:0})");
        await Task.Delay(100);
        Check(code.Count==priorRequests,"a stale code click cannot use another conversation's data");
        // a link clicked opens outside, through the program
        var box = await Page("(()=>{const a=document.querySelector('.message a[href]');a.scrollIntoView({block:'center'});const r=a.getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()");
        await page.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type = "mousePressed", x = box.GetProperty("x").GetDouble(), y = box.GetProperty("y").GetDouble(), button = "left", clickCount = 1 }));
        await page.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type = "mouseReleased", x = box.GetProperty("x").GetDouble(), y = box.GetProperty("y").GetDouble(), button = "left", clickCount = 1 }));
        await Task.Delay(300);
        Check(opened == "https://example.org/paired" && page.Browser.CoreWebView2.Source == ConversationView.Origin + "/conversation.html", "a clicked link goes to the system browser and the page stays");

        // a streamed reply: the view follows it while the reader is at the end
        page.Update(Messages(("user", "Please explain the streamed reply.")), "", true);
        await page.SettledAsync();
        string text = ""; int behind = 0;
        for (int i = 1; i <= 40; i++)
        {
            text += $"Reply paragraph {i} of the streamed answer.\n\n";
            page.Update(Messages(("user", "Please explain the streamed reply.")), text, false);
            await page.SettledAsync();
            if (!await page.EndInViewAsync()) behind++;
        }
        Check(behind == 0 && await PageTrue("document.querySelectorAll('.message.partial p').length===40"), $"a reply streamed to a reader at the end keeps its end in view after every chunk: behind on {behind} of 40");
        // a reader who has scrolled up is left where they were; back at the end, the view follows again
        Check(await page.ScrollToTextAsync("Reply paragraph 5 ") && !await page.EndInViewAsync(), "the reader can scroll back to an earlier paragraph");
        Check(await page.SelectAsync("explain the streamed"), "text of the question can be selected");
        double top = await page.ScrollTopAsync();
        for (int i = 41; i <= 45; i++) { text += $"Reply paragraph {i} after the reader scrolled up.\n\n"; page.Update(Messages(("user", "Please explain the streamed reply.")), text, false); await page.SettledAsync(); }
        Check(await page.ScrollTopAsync() == top && !await page.EndInViewAsync(), "a reader who scrolled up is left there while the reply goes on");
        Check(await page.SelectionAsync() == "explain the streamed", "a selection in the question survives the streamed reply");
        await Page("getSelection().removeAllRanges()");
        Check(await page.ScrollToEndAsync(), "the reader can return to the end");
        text += "Reply paragraph 46 at the end again.\n\n"; page.Update(Messages(("user", "Please explain the streamed reply.")), text, false); await page.SettledAsync();
        Check(await page.EndInViewAsync(), "returning to the end resumes following the reply");
        // the reply completed is kept as its block; the same conversation again changes nothing
        int renders = (await page.StateAsync()).GetProperty("renders").GetInt32();
        page.Update(Messages(("user", "Please explain the streamed reply."), ("assistant", text)), "", false); await page.SettledAsync();
        var state = await page.StateAsync();
        Check(state.GetProperty("count").GetInt32() == 2 && state.GetProperty("partial").GetInt32() == 0 && state.GetProperty("renders").GetInt32() == renders && await PageTrue("document.querySelectorAll('.message.partial').length===0 && document.querySelectorAll('.message.assistant p').length===46"), "a completed reply keeps the block that was streamed rather than rendering it again");
        page.Update(Messages(("user", "Please explain the streamed reply."), ("assistant", text)), "", false); await page.SettledAsync();
        Check((await page.StateAsync()).GetProperty("renders").GetInt32() == renders, "the same conversation given again renders nothing");
        // The paragraph being written is replaced as its Markdown grows. Selection
        // inside that paragraph, including backwards and cross-markup ranges, survives.
        text = "Select this sentence. " + string.Join(" ", Enumerable.Repeat("A long paragraph still arriving.", 100));
        var question = Messages(("user", "Please explain the streamed reply."));
        page.Update(question, text, true); await page.SettledAsync();
        await page.ScrollToTextAsync("Select this sentence.");
        Check(await page.SelectAsync("Select this sentence."), "the growing paragraph can be selected");
        top = await page.ScrollTopAsync();
        for (int i = 0; i < 6; i++) { text += " Another chunk."; page.Update(question, text, false); await page.SettledAsync(); }
        Check(await page.SelectionAsync() == "Select this sentence." && await page.ScrollTopAsync() == top, "selection and reading position survive successive replacements of the streamed paragraph");
        await Page("(()=>{const s=getSelection(),a=s.anchorNode,ao=s.anchorOffset,f=s.focusNode,fo=s.focusOffset;s.setBaseAndExtent(f,fo,a,ao)})()");
        text += " More text."; page.Update(question, text, false); await page.SettledAsync();
        Check(await page.SelectionAsync() == "Select this sentence." && await PageTrue("getSelection().anchorOffset>getSelection().focusOffset"), "backwards selection retains its text and direction while streaming");
        text = "First **bold words** and plain words.";
        page.Update(question, text, true); await page.SettledAsync();
        await Page("(()=>{const p=document.querySelector('.partial p'),a=p.querySelector('strong').firstChild,b=p.lastChild;getSelection().setBaseAndExtent(a,0,b,10)})()");
        string selected = await page.SelectionAsync();
        text += " And more."; page.Update(question, text, false); await page.SettledAsync();
        Check(await page.SelectionAsync() == selected && selected == "bold words and plain", "selection across emphasis and plain text survives new chunks");
        // A selection at the bottom pauses following without stopping rendering.
        text = string.Join("\n\n", Enumerable.Range(1, 25).Select(i => $"Paragraph {i} stable text.")) + "\n\n";
        page.Update(question, text, true); await page.SettledAsync(); await page.ScrollToEndAsync();
        await page.SelectAsync("Paragraph 25 stable text."); top = await page.ScrollTopAsync();
        text += string.Join("\n\n", Enumerable.Range(1, 5).Select(i => $"New paragraph {i} below the selection."));
        page.Update(question, text, false); await page.SettledAsync();
        Check(await page.ScrollTopAsync() == top && await page.SelectionAsync() == "Paragraph 25 stable text." && (await page.TextAsync()).Contains("New paragraph 5"), "selected text pauses automatic following while the rest of the reply keeps arriving");
        await Page("getSelection().removeAllRanges()"); await page.ScrollToEndAsync();
        text += "\n\nOne more paragraph."; page.Update(question, text, false); await page.SettledAsync();
        Check(await page.EndInViewAsync(), "clearing selection and returning to the bottom resumes following");
        // A renderer crash is real, but affects only this test's browser profile.
        // Updates while failed must be retained and replayed in a newly secured view.
        var crashed = new TaskCompletionSource();
        var oldBrowser = page.Browser;
        oldBrowser.CoreWebView2.ProcessFailed += (_, _) => crashed.TrySetResult();
        try { await oldBrowser.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.crash", "{}").WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) { }
        await crashed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        page.Update(Messages(("user", "Question retained across renderer crash")), "Recovered **reply**\n\n```r\nmean(c(1,2,3))\n```", true);
        page.Hide(); page.Show(); await page.ReopenAsync();
        await page.Ready.WaitAsync(TimeSpan.FromSeconds(30)); await page.SettledAsync();
        Check(page.Browser != oldBrowser && oldBrowser.IsDisposed && (await page.TextAsync()).Contains("Question retained across renderer crash") && (await page.TextAsync()).Contains("Recovered reply"), "closing and reopening a failed conversation recreates its browser and restores the latest history and partial reply");
        Check(!page.Browser.CoreWebView2.Settings.AreHostObjectsAllowed && !page.Browser.CoreWebView2.Settings.AreDevToolsEnabled && await PageTrue("document.querySelector('meta[http-equiv=Content-Security-Policy]').content.includes(\"connect-src 'none'\")"), "recovered conversation reapplies host-access restrictions and content security policy");
        int beforeRecoveryAction = code.Count;
        await Page("document.querySelector('[data-code=open]').click()"); await Task.Delay(100);
        Check(code.Count == beforeRecoveryAction + 1 && code.Last() == ("open", "mean(c(1,2,3))"), "code actions work once after recovery without duplicate handlers");
        page.Update(Messages(("user", "Question retained across renderer crash")), "A later reply after recovery", false); await page.SettledAsync();
        Check((await page.TextAsync()).Contains("A later reply after recovery"), "streaming continues in the recovered conversation");
        // a new conversation clears the page
        page.Update(new JsonArray(), "", true); await page.SettledAsync();
        Check((await page.TextAsync()).Trim().Length == 0 && (await page.StateAsync()).GetProperty("count").GetInt32() == 0, "a new conversation clears the page");
        form.Controls.Remove(page);
    }
}

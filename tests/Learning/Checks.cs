using StatsDirect.R;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

internal static partial class Program
{
    // a rejection is the program refusing, not the program failing: a null reference, a cast or a JSON shape error would also throw here
    static async Task Reject(Func<Task> action,string label)
    {
        try{await action();}
        catch(Exception ex)
        {
            if(ex is NullReferenceException or InvalidCastException or ArgumentNullException or IndexOutOfRangeException or System.Text.Json.JsonException)throw new Exception("Expected a refusal for: "+label+"; got "+ex.GetType().Name+": "+ex.Message);
            Check(true,label);return;
        }
        throw new Exception("Expected rejection: "+label);
    }
    static JsonArray Message(string text)=>new(new JsonObject{["role"]="user",["text"]=text});
    static async Task TestCourses(LearningService service,LearningView view)
    {
        await service.HandleAsync(view,new JsonObject{["action"]="exampleCourse"});await Task.Delay(100);
        Check((await JS(view,"document.querySelectorAll('[data-lesson]').length")).GetValue<int>()==15,"provider pack adds its three lessons to the shared library");
        var saved=LearningJson.Read(Path.Combine(service.Storage,"course-pack.json"),2_000_000);
        Check(saved.I("schemaVersion")==2,"provider course schema is preserved");
        string bad=Path.Combine(root,"bad-course.json");var invalid=saved.DeepClone();invalid["provider"]["reviewEmail"]="x@example.invalid\r\nBcc: injected@example.invalid";File.WriteAllText(bad,invalid.ToJsonString());
        string original=File.ReadAllText(Path.Combine(service.Storage,"course-pack.json"));
        await Reject(()=>{service.ImportPack(bad);return Task.CompletedTask;},"malformed course/contact metadata is rejected");
        Check(File.ReadAllText(Path.Combine(service.Storage,"course-pack.json"))==original,"failed import preserves the active course");
        string id="course:"+saved.S("id")+":"+saved.S("version")+":"+saved.A("lessons")[0].S("id");
        service.OpenExample(id,true);await Task.Delay(100);
        var script=service.Documents.OfType<frmScript>().Last();
        var console=script.InteractiveConsole;await console.Ready;
        Check(script.LearningScript==saved.A("lessons")[0].S("r")&&!script.LearningRunning&&console.Session==null,"provider R code opens for review without execution");
        Check(await console.Browser.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('[role=toolbar]').length===1&&document.getElementById('ai')?.getAttribute('aria-label')==='AI assistance'")=="true","R script has an AI command in the existing single toolbar");
        string rFile=Path.Combine(root,"existing.R");File.WriteAllText(rFile,"# Read only test\nmean(c(1,2))\n");script.OpenFile(rFile,false,null);
        Check(((ComboBox)Field(script,"cboLanguage")).SelectedIndex==1,"opening an R file selects the R language and keeps R tools");
    }
    static async Task TestTutor(ChatGPTTutor tutor)
    {
        var missing = new TutorRuntimeInstallation(Path.Combine(root,"missing-runtime"),Path.Combine(AppContext.BaseDirectory,"Licenses","OpenAI"));
        using (var firstUse = ChatGPTTutor.OnDemand(Path.Combine(root,"first-use-account"),missing))
        {
            await firstUse.RefreshAccountAsync();
            Check(firstUse.Email==null&&firstUse.Status.Contains("Use my ChatGPT")&&!Directory.Exists(Path.GetDirectoryName(missing.DirectoryPath)),"opening Learning or AI before setup performs no download or runtime startup");
        }
        await tutor.RefreshAccountAsync();Check(tutor.Email==null,"fresh isolated account starts disconnected");
        await TestPrivateDiagnostics();
        Check(ChatGPTTutor.IsLoginUrl("https://auth.openai.com/authorize")&&!ChatGPTTutor.IsLoginUrl("http://auth.openai.com/authorize")&&!ChatGPTTutor.IsLoginUrl("https://auth.openai.com.evil.invalid/authorize"),"sign-in accepts only official HTTPS authentication hosts");
        await tutor.SignInAsync();await Wait(()=>tutor.Email=="mock@example.invalid","mocked sign-in completion updates account state");
        string streamed="";
        var reply=await tutor.ConverseAsync("fictional context",Message("Explain paired tests"),new(),true,t=>streamed=t,(_,_)=>Task.FromResult<JsonNode>(new JsonObject()),CancellationToken.None);
        Check(reply.Text.Contains("paired")&&streamed==reply.Text&&reply.Model=="mock-no-network","mocked text streams and completes without a live model request");
        try{await tutor.ConverseAsync("",Message("FORCE_ERROR"),new(),true,null,(_,_)=>Task.FromResult<JsonNode>(new JsonObject()),CancellationToken.None);throw new Exception("missing provider error");}
        catch(IOException ex){Check(!ex.Message.Contains("SECRET")&&ex.Message.Contains("sign-in"),"provider authentication errors are redacted and actionable");}
        using var stop=new CancellationTokenSource(100);
        await Reject(()=>tutor.ConverseAsync("",Message("SLOW_REPLY"),new(),true,null,(_,_)=>Task.FromResult<JsonNode>(new JsonObject()),stop.Token),"cancel interrupts a pending mocked reply");
        reply=await tutor.ConverseAsync("",Message("retry"),new(),true,null,(_,_)=>Task.FromResult<JsonNode>(new JsonObject()),CancellationToken.None);
        Check(reply.Text.Length>0,"a question can be retried after cancellation");
        int calls=0;var tools=new QuestionContext([],true,CancellationToken.None).Tools;
        reply=await tutor.ConverseAsync("",Message("TOOL_BOUNDARIES"),tools,true,null,(_,_)=>{calls++;return Task.FromResult<JsonNode>(new JsonObject());},CancellationToken.None);
        var boundary=JsonNode.Parse(reply.Text);
        Check(calls==1&&boundary.B("valid")&&!boundary.B("wrong")&&!boundary.B("namespace")&&!boundary.B("unknown")&&!boundary.B("duplicate"),"transport rejects wrong threads, namespaces, unknown tools and duplicate call IDs before invoking the host");
        string actual=Environment.GetEnvironmentVariable("STATSDIRECT_TEST_TUTOR_RUNTIME");
        if(actual!=null)
        {
            using var runtime=new ChatGPTTutor(actual,Path.Combine(root,"actual-runtime-no-credentials"),Path.Combine(AppContext.BaseDirectory,"Tutor"));
            await runtime.RefreshAccountAsync();
            Check(runtime.Email==null&&runtime.Status=="Not connected","official pinned app-server starts with strict isolated config and no inherited login; no model request made");
        }
    }
    static async Task TestPrivateDiagnostics()
    {
        string store=Path.Combine(root,"diagnostic-account"),mock=Path.Combine(root,"malformed-server.mjs");
        Directory.CreateDirectory(store);
        string logPath=Path.Combine(store,"last-disconnect.txt");
        await File.WriteAllTextAsync(logPath,"old diagnostic\nPRIVATE_LEGACY_RESPONSE");
        await File.WriteAllTextAsync(mock,"import readline from 'node:readline';readline.createInterface({input:process.stdin}).on('line',l=>{const q=JSON.parse(l);if(q.method==='initialize')process.stdout.write(JSON.stringify({id:q.id,result:{}})+'\\n');if(q.method==='account/read'){process.stdout.write(JSON.stringify({method:'PRIVATE_METHOD_SENTINEL',params:{text:'PRIVATE_PARAMS_SENTINEL'}})+'\\n');process.stdout.write('{\"response\":\"PRIVATE_REPLY_SENTINEL\",BROKEN\\n');}});");
        using(var diagnostics=new ChatGPTTutor(Environment.GetEnvironmentVariable("STATSDIRECT_TEST_NODE"),store,Path.Combine(AppContext.BaseDirectory,"Tutor"),mock))
        {
            Check(!File.Exists(logPath),"upgrading removes legacy diagnostics containing raw response text");
            await Reject(()=>diagnostics.RefreshAccountAsync(),"malformed protocol response disconnects without disclosing raw content");
            string log=await File.ReadAllTextAsync(logPath);
            Check(log.Contains("metadata only")&&log.Contains("JsonReaderException")&&log.Contains("response length:")&&log.Contains("other notification"),"disconnect diagnostics retain useful error type, message shape and length");
            Check(!log.Contains("PRIVATE_")&&!log.Contains("BROKEN")&&!diagnostics.Status.Contains("PRIVATE_"),"diagnostics exclude reply content, arbitrary method names and parser exception details");
        }
        using(var next=new ChatGPTTutor(Environment.GetEnvironmentVariable("STATSDIRECT_TEST_NODE"),store,Path.Combine(AppContext.BaseDirectory,"Tutor"),mock))
            Check(File.Exists(logPath),"new metadata-only diagnostics remain available across restarts");
    }
    static async Task TestConsent(frmMain main,LearningService service,LearningView view,frmSpreadsheetGear grid)
    {
        grid.LoadLearningExample(new JsonArray(new JsonObject{["title"]="Before",["values"]=new JsonArray(1,2,3)},new JsonObject{["title"]="After",["values"]=new JsonArray(2,3,4)}));
        using var empty=new CancellationTokenSource();var noSharing=new QuestionContext([],true,empty.Token);
        Check(noSharing.Overview().A("documents").Count==0&&!noSharing.InitialContext().ToJsonString().Contains("Before"),"question-only context contains no document titles, headings or values");
        await Reject(()=>noSharing.CallAsync("statsdirect_read_data",new JsonObject{["documentId"]=grid.Id()}),"unapproved worksheet reads are denied");
        var context=new QuestionContext([grid],true,empty.Token);
        var data=await context.CallAsync("statsdirect_read_data",new JsonObject{["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=4,["columns"]=new JsonArray(0,1)});
        Check(data.I("rowCount")==3&&data.A("columns")[0].A("values")[0].GetValue<double>()==1,"approved worksheet reads preserve literal rows and numeric values");
        await Reject(()=>context.CallAsync("statsdirect_read_data",new JsonObject{["documentId"]=grid.Id(),["firstRow"]=1,["lastRow"]=10000}),"large ranges are rejected without silent sampling");
        Check(context.Tools.All(t=>t.S("name")!="statsdirect_open_analysis"),"script assistant exposes no analysis execution or code-editing tool");
        grid.LoadLearningExample(new JsonArray(new JsonObject{["title"]="Changed",["values"]=new JsonArray(8,9)}));
        await Reject(()=>context.CallAsync("statsdirect_workspace",new()),"worksheet changes invalidate an existing sharing grant");
        view.Activate();await view.CallAsync("restore",service.State.DeepClone());await Task.Delay(70);
        // Use the actual web composer and native confirmation hook; inspect only local state.
        await JS(view,"document.querySelector('[data-lesson=precision]').click()");
        service.ConfirmSharing=_=>LearningService.SharingDecision.Share;view.SourceId=grid.Id();service.RefreshWorkspace();
        await service.HandleAsync(view,new JsonObject{["action"]="workspace"});
        Check(view.SourceId==grid.Id(),"refreshing the workspace preserves the learner's explicit document choice");
        await JS(view,"document.getElementById('question').value='Explain this example';document.getElementById('send').click()");
        await Wait(()=>service.State?.A("sharingConsents").Count==1,"learning stores a per-question sharing record before tutor access");
        await Wait(()=>service.State?.A("conversation").Any(m=>m.S("text").Contains("Mock answer"))==true,"approved Learning question completes through the native bridge");
        service.ConfirmSharing=_=>throw new Exception("Acceptance should be reused for the same data document.");
        await JS(view,"document.getElementById('question').value='Another question on these data';document.getElementById('send').click()");
        await Wait(()=>service.State?.A("sharingConsents").Count==2,"Learning records every shared question even when acceptance is reused");
        Check(service.State.A("sharingConsents")[1].B("reusedAcceptance")&&service.State.A("sharingConsents")[1].S("scope")=="app-session","reused Learning acceptance records its session scope");
        await Wait(()=>((CancellationTokenSource)Field(service,"question"))==null,"reused-acceptance Learning question finishes");
        service.ConfirmSharing=_=>LearningService.SharingDecision.LessonsOnly;
        service.RevokeAISharing();
        await JS(view,"document.getElementById('question').value='Lessons only please';document.getElementById('send').click()");
        await Wait(()=>view.SourceId=="__none__","lessons-only choice removes the document scope for the next question");
        Check(service.State.A("sharingConsents").Count==2,"lessons-only question does not reuse the previous consent");
        await Task.Delay(250);
        var script=service.Documents.OfType<frmScript>().Last();service.ShowScriptAI(script);await Task.Delay(100);
        var pane=(ScriptAIPane)Field(service,"ai");
        var paneView=(Control)Field(pane,"view");
        static int Top(Control c)=>c.PointToScreen(Point.Empty).Y;
        void CheckComposerOrder(string mode)
        {
            Check(Top(pane.SharingOptions)<Top(pane.Composer)&&Top(pane.Composer)+pane.Composer.Height<=Top(pane.Send)&&Top(pane.Send)+pane.Send.Height<Top(pane.Transcript),mode+": sharing, question and Send precede the conversation");
            Check(paneView.ClientRectangle.Contains(paneView.RectangleToClient(pane.Composer.RectangleToScreen(pane.Composer.ClientRectangle)))&&pane.Transcript.Height>=80,mode+": composer is visible with room for replies (pane "+paneView.Size+", question "+pane.Composer.Bounds+", replies "+pane.Transcript.Bounds+")");
        }
        void CapturePane(string name){using var bitmap=new Bitmap(paneView.Width,paneView.Height);paneView.DrawToBitmap(bitmap,paneView.ClientRectangle);bitmap.Save(Path.Combine(artifacts,name+".png"));}
        CapturePane("script-ai-expanded");CheckComposerOrder("expanded docked AI");
        Check(!pane.ShareScript.Checked&&!pane.ShareWorkbook.Checked,"script AI sharing starts off");
        pane.ShareScript.Checked=true;Check(!pane.Send.Enabled,"shared script question cannot send without acknowledgment");
        pane.Composer.Text="Check consent";await pane.SendAsync();
        Check(((Label)Field(pane,"status")).Text.Contains("confirm sharing"),"unconfirmed context request displays the sharing reminder");
        pane.Acknowledge.Checked=true;await Task.Delay(20);
        Check(((Label)Field(pane,"status")).Text=="","confirming sharing clears the previous confirmation reminder");
        Check(pane.Composer.Focused&&pane.SharingOptions.Text.Contains("Confirmed"),"confirming sharing collapses controls and focuses the composer");
        CheckComposerOrder("confirmed docked AI");
        pane.Composer.Text="Explain the script";await pane.SendAsync();
        await pane.Transcript.SettledAsync();
        Check((await pane.Transcript.TextAsync()).Contains("Mock answer")&&pane.Acknowledge.Checked&&pane.Send.Enabled,"script reply retains session acceptance for the next shared question");
        CapturePane("script-ai-reply");
        pane.ShareScript.Checked=false;Check(pane.Send.Enabled,"question-only conversation can continue without document access");
        pane.Composer.Text="Keep this question while moving the pane.";
        var session=(StatsDirect.UI.ToolWindows.ToolPaneSession)Field(pane,"session");
        await session.ToggleModeAsync();await Task.Delay(60);CheckComposerOrder("floating AI");
        CapturePane("script-ai-floating");
        session.Window.ClientSize=new Size((int)(360*session.Window.DeviceDpi/96f),(int)(420*session.Window.DeviceDpi/96f));await Task.Delay(60);
        var viewport=(Panel)Field(pane,"viewport");
        Check(!viewport.HorizontalScroll.Visible&&viewport.VerticalScroll.Visible&&pane.Acknowledge.Height>pane.Acknowledge.Font.Height*2,"small AI pane wraps confirmation text and scrolls vertically without clipping");
        viewport.ScrollControlIntoView(pane.Transcript);await Task.Delay(20);
        Check(viewport.RectangleToScreen(viewport.ClientRectangle).IntersectsWith(pane.Transcript.RectangleToScreen(pane.Transcript.ClientRectangle)),"conversation remains reachable in a small AI window");
        viewport.ScrollControlIntoView(pane.Composer);pane.Composer.Focus();CapturePane("script-ai-small");
        await session.ToggleModeAsync();await Task.Delay(60);
        Check(pane.Composer.Text=="Keep this question while moving the pane."&&(await pane.Transcript.TextAsync()).Contains("Mock answer")&&pane.Composer.Focused,"dock and pop-out preserve the draft, replies and typing focus");
        // a workbook opened after sharing was confirmed: Send says the open workbooks changed, not the general reminder
        pane.ShareWorkbook.Checked=true;pane.Acknowledge.Checked=true;await Task.Delay(20);
        var opened=(frmSpreadsheetGear)main.CreateGrid("Opened after confirmation");service.RefreshWorkspace();await Task.Delay(60);
        pane.Composer.Text="After a new workbook";await pane.SendAsync();
        Check(((Label)Field(pane,"status")).Text.StartsWith("The open workbooks changed"),"a question after the open workbooks changed says so, not the general sharing reminder");
        pane.ShareWorkbook.Checked=false;pane.Composer.Text="";opened.Dispose();service.RefreshWorkspace();await Task.Delay(60);
        await TestScriptReplyStability(pane);
        pane.Hide();
    }
    static async Task TestScriptReplyStability(ScriptAIPane pane)
    {
        // The conversation is a page: a streamed reply is rendered as it grows, the view follows it while the reader is at the end and
        // stays put when they have scrolled up or are selecting text, and a reply that is complete is kept as its block, not rendered again.
        pane.NewConversation();pane.Composer.Focus();
        var render=typeof(ScriptAIPane).GetMethod("Render",Members)!;
        var partial=typeof(ScriptAIPane).GetField("partial",Members)!;
        var viewport=(Panel)Field(pane,"viewport");
        var page=pane.Transcript;
        var messages=(JsonArray)typeof(ScriptAIPane).GetField("chat",Members)!.GetValue(pane)!.GetType().GetField("Messages",Members)!.GetValue(typeof(ScriptAIPane).GetField("chat",Members)!.GetValue(pane))!;
        messages.Add(new JsonObject{["role"]="user",["text"]="Please explain the streamed reply."});
        async Task Stream(string value){partial.SetValue(pane,value);render.Invoke(pane,null);await page.SettledAsync();}
        string text=string.Join("\n",Enumerable.Range(1,80).Select(i=>$"Reply line {i}: a streamed explanation."));
        await Stream(text);
        Check(await page.SelectAsync("explain the streamed")&&await page.ScrollToTextAsync("Please explain"),"the question before a reply can be selected and brought into view");
        var question=pane.Composer.RectangleToScreen(pane.Composer.ClientRectangle);
        var bounds=page.Bounds;var scroll=viewport.AutoScrollPosition;double first=await page.ScrollTopAsync();
        for(int i=0;i<12;i++){text+=$"\nAdditional reply line {i}.";await Stream(text);}
        Check(await page.SelectionAsync()=="explain the streamed","incoming reply preserves selected conversation text");
        Check(await page.ScrollTopAsync()==first,"incoming reply leaves earlier lines in view while the user reads them");
        Check(page.Bounds==bounds&&pane.Composer.RectangleToScreen(pane.Composer.ClientRectangle)==question&&viewport.AutoScrollPosition==scroll,"streaming leaves the pane geometry and outer scroll position unchanged");
        Check(pane.Composer.Focused,"incoming replies do not steal typing focus");
        await page.ScrollToTextAsync("Reply line 40:");first=await page.ScrollTopAsync();
        text+="\nAnother chunk while reading earlier lines.";await Stream(text);
        Check(await page.ScrollTopAsync()==first,"scrolling back pauses automatic following without needing a text selection");
        await page.Browser.CoreWebView2.ExecuteScriptAsync("getSelection().removeAllRanges()");
        await page.ScrollToEndAsync();first=await page.ScrollTopAsync();
        text+=string.Concat(Enumerable.Range(1,10).Select(i=>$"\nNew ending {i}."));await Stream(text);
        Check(await page.EndInViewAsync()&&await page.ScrollTopAsync()>first,"returning to the bottom resumes following new reply text");
        int renders=(await page.StateAsync()).GetProperty("renders").GetInt32();
        await Stream(text);
        Check((await page.StateAsync()).GetProperty("renders").GetInt32()==renders,"identical completion text does not repaint or reset the conversation");
        text=string.Join("\n\n",Enumerable.Range(1,4000).Select(i=>$"Long reply line {i}."));await Stream(text);
        await page.ScrollToTextAsync("Long reply line 3000.");first=await page.ScrollTopAsync();
        text+="\n\nFinal corrected tail.";await Stream(text);
        Check(await page.ScrollTopAsync()==first,"long conversations preserve a reading position beyond the 16-bit pixel scroll range");
        text=text.Replace("Final corrected tail.","Replacement ending.");await Stream(text);
        Check((await page.TextAsync()).TrimEnd().EndsWith("Replacement ending.")&&await page.ScrollTopAsync()==first,"corrected final reply updates only its tail and preserves earlier reading position");
        text=text.Substring(0,text.LastIndexOf('\n')).TrimEnd();await Stream(text);
        Check((await page.TextAsync()).TrimEnd().EndsWith("Long reply line 4000.")&&await page.ScrollTopAsync()==first,"trimming a streamed tail works in the read-only conversation without moving the reader");
        pane.NewConversation();await page.SettledAsync();
        Check((await page.TextAsync()).Trim().Length==0&&(await page.SelectionAsync()).Length==0,"a new conversation clears the old reply and selection");
    }
    static async Task TestExports(LearningService service,LearningView view)
    {
        view.Activate();await JS(view,"document.getElementById('recordNav').click()");
        var payload=(JsonObject)await JS(view,"(()=>{const old=window.webkit.messageHandlers.statsDirectLearn.postMessage;let result;window.webkit.messageHandlers.statsDirectLearn.postMessage=m=>result=m;window.statsDirectLearn.exportRecord();window.webkit.messageHandlers.statsDirectLearn.postMessage=old;return result})()");
        Check(payload.S("text").Contains("learning",StringComparison.OrdinalIgnoreCase)&&payload.S("html").Length>0,"learning record preview produces text, structured data and formatted content");
        foreach(string format in new[]{"txt","json","html","pdf","docx"})
        {
            string path=Path.Combine(artifacts,"learning-record."+format);await service.ExportToAsync(payload,path,format);
            Check(new FileInfo(path).Length>100,"learning record exports to "+format.ToUpperInvariant());
        }
        payload["recipient"]="reviewer@example.invalid";string draft=service.PrepareEmail(payload);string eml=File.ReadAllText(draft);
        Check(eml.StartsWith("X-Unsent: 1")&&eml.Contains("learning-record.txt")&&eml.Contains("learning-record.json"),"assessment handoff prepares an unsent email with both record formats; no mail client or sending invoked");
        var pdfPack=CoursePack.Import([Path.Combine(artifacts,"learning-record.pdf")]);Check(pdfPack.A("documents").Count>0,"searchable PDF course material imports locally");
    }
    static async Task TestRSetup(frmMain main)
    {
        int attempts=0;
        using var dialog=new frmInstallR(_=>Task.FromResult(++attempts>1));dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new(-16000,-16000);dialog.ShowInTaskbar=false;dialog.Show(main);
        var check=(Button)Field(dialog,"check");check.PerformClick();await Task.Delay(50);
        Check(!dialog.UserThinksRIsInstalled&&dialog.Visible,"missing-R check does not claim installation or lose the pending dialog");
        check.PerformClick();await Task.Delay(50);Check(dialog.UserThinksRIsInstalled,"successful injected R readiness probe resumes the pending action");
        using var cancelled=new frmInstallR(_=>Task.FromResult(false));cancelled.StartPosition=FormStartPosition.Manual;cancelled.Location=new(-16000,-16000);cancelled.ShowInTaskbar=false;cancelled.Show(main);((Button)Field(cancelled,"cancel")).PerformClick();
        Check(!cancelled.UserThinksRIsInstalled,"declining setup leaves the requested action unexecuted");
        var slow=new TaskCompletionSource<bool>();
        using var late=new frmInstallR(_=>slow.Task);late.StartPosition=FormStartPosition.Manual;late.Location=new(-16000,-16000);late.ShowInTaskbar=false;late.Show(main);
        ((Button)Field(late,"check")).PerformClick();late.Close();slow.SetResult(true);await Task.Delay(50);
        Check(!late.UserThinksRIsInstalled&&!late.Visible,"closing R setup prevents a late readiness reply from resuming or reopening it");
        bool launched=false;frmInstallR modal=null;
        using(modal=new frmInstallR(_=>Task.FromResult(launched),async(_,ct)=>await Task.Delay(30,ct),async ct=>{Check(modal.Visible&&modal.WindowState==FormWindowState.Minimized,"setup stays modal but out of the installer's way while the native installer runs");await Task.Delay(30,ct);launched=true;}))
        {
            modal.Shown+=(_,_)=>((Button)Field(modal,"download")).PerformClick();
            Check(modal.ShowDialog(main)==DialogResult.OK&&modal.UserThinksRIsInstalled&&launched,"modal setup returns success only after installer exit and readiness, so the pending action can resume");
        }
        int downloads=0;bool didLaunch=false;
        using var retry=new frmInstallR(_=>Task.FromResult(didLaunch),async(_,ct)=>{if(++downloads==1)await Task.Delay(30000,ct);},_=>{didLaunch=true;return Task.CompletedTask;});
        retry.StartPosition=FormStartPosition.Manual;retry.Location=new(-16000,-16000);retry.ShowInTaskbar=false;retry.Show(main);
        ((Button)Field(retry,"download")).PerformClick();((Button)Field(retry,"cancel")).PerformClick();
        await Wait(()=>((Button)Field(retry,"download")).Enabled,"cancelled download restores Retry without losing the setup form");
        Check(!didLaunch&&!retry.UserThinksRIsInstalled,"cancel does not launch an installer or resume the pending action");
        ((Button)Field(retry,"download")).PerformClick();await Wait(()=>retry.UserThinksRIsInstalled,"retry after cancellation completes setup and resumes once ready");
        string bytes=Path.Combine(root,"bad-installer.exe");await File.WriteAllTextAsync(bytes,"not an installer");
        await Reject(()=>RInstallation.VerifyHashAsync(bytes,RInstallation.Sha256,CancellationToken.None),"R installer checksum rejects substituted content");
        using var package=new RInstallation();using var client=new HttpClient(new FakeHandler());
        await Reject(()=>package.DownloadAsync(client,null,CancellationToken.None),"R setup rejects redirected or unexpected download responses");
        Check(!File.Exists(package.FilePath),"failed verification never exposes a launchable installer");
        string reviewed=Environment.GetEnvironmentVariable("STATSDIRECT_TEST_R_INSTALLER");
        if(reviewed!=null){await RInstallation.VerifyHashAsync(reviewed,RInstallation.Sha256,CancellationToken.None);RInstallation.VerifySignature(reviewed);Check(true,"downloaded official R package passes hash and Windows publisher verification without being installed");}
        Check(await RInstallation.ReadyAsync(CancellationToken.None),"installed R passes a harmless executable-readiness probe");
    }
    sealed class FakeHandler:HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=new HttpRequestMessage(HttpMethod.Get,"https://elsewhere.invalid/package.exe"),Content=new ByteArrayContent([1,2,3])});}
}

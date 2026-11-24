using StatsDirect.R;
using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Diagnostics;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static async Task RunScriptAIConversationFixture(frmMain main)
    {
        using var tutor = WorksheetMockTutor(Path.Combine(root, "script-ai-account"), () => AISecurityLevel.Unrestricted);
        using var service = new LearningService(main, Path.Combine(root, "script-ai-learning"), tutor);
        typeof(frmMain).GetField("learning", Members)!.SetValue(main, service);
        try
        {
            var grid = (frmSpreadsheetGear)main.CreateGrid("Script AI test workbook");
            await tutor.SignInAsync();
            await Wait(() => tutor.Email == "mock@example.invalid", "Script AI connects to the local mock account");
            await TestConversationR(main, service, grid);
        }
        finally { typeof(frmMain).GetField("learning", Members)!.SetValue(main, null); }
    }

    static async Task TestConversationR(frmMain main, LearningService service, frmSpreadsheetGear grid)
    {
        const string source = "before <- c(312,242,340,388,296,254,391,402,290)\nafter <- c(300,201,232,312,220,256,328,330,231)\n";
        var original = service.OpenScript(source, "PEFR source", false);
        var console = original.InteractiveConsole;
        await console.Ready.WaitAsync(TimeSpan.FromSeconds(40));
        int CompletedCommands() => console.History.Count(entry =>
            entry.GetProperty("type").GetString() == "commandFinished" && entry.GetProperty("status").GetString() == "ok");
        async Task RunAndWait(Func<Task> run, string description)
        {
            int completed = CompletedCommands();
            await run();
            await Wait(() => CompletedCommands() > completed && console.NativeState == "ready", description, 30000);
        }
        await RunAndWait(() => console.SubmitAsync(source + "session_only <- 123\n", "script"), "the original R session loads data and a session-only object");
        Check(console.Session?.PersistentWorkspace == true, "the Script AI session test uses the persistent R worker");

        service.ShowScriptAI(original);
        var pane = (ScriptAIPane)Field(service, "ai");
        pane.ShareScript.Checked = true;
        pane.Acknowledge.Checked = true;
        pane.Composer.Text = "PEFR_CONTEXT_SESSION";
        await pane.SendAsync();
        await pane.Transcript.SettledAsync();
        var messages = (JsonArray)Field(Field(pane, "chat"), "Messages");
        string exported = ConversationRScript.Build(messages, 1);
        const string edited = "before <- after <- 999\n";
        await console.ExecuteAsync("window.StatsDirectRHost.setScript(" + System.Text.Json.JsonSerializer.Serialize(edited) + ")");
        string editorBeforeRun = await console.GetScriptAsync();
        Check(editorBeforeRun.Contains("999", StringComparison.Ordinal), "the R editor has unrun changes before the AI action");
        Check(exported.Contains(source) && !exported.Contains("999"), "the reviewable export retains the original shared snapshot after the source script is edited");
        await RunConversationR(exported, "ai-pefr-open", "stopifnot(length(before)==9, length(after)==9, abs(unname(t.test(before, after, paired=TRUE)$statistic)-4.9257744860354)<1e-11, identical(d, before-after))\ncat('PEFR_CONTEXT_OK\\n')", "PEFR_CONTEXT_OK");

        var paneView = (Control)Field(pane, "view");
        IEnumerable<Control> Controls(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Controls(x)));
        var runInSession = Controls(paneView).OfType<Button>().Single(b => b.Text == "Run in this R session");
        int count = service.Documents.OfType<frmScript>().Count();
        try
        {
            await RunAndWait(() => { runInSession.PerformClick(); return Task.CompletedTask; },
                "the pane's Run in this R session runs the reply in the original console");
        }
        catch
        {
            var status = (Label)Field(pane, "status");
            Console.WriteLine($"Script AI action: button visible={runInSession.Visible}, enabled={runInSession.Enabled}; pane open={pane.IsOpen}; console state={console.NativeState}; status={status.Text}");
            Console.WriteLine(string.Join("\n", console.History.TakeLast(6)));
            throw;
        }
        Check(service.Documents.OfType<frmScript>().Count() == count && ReferenceEquals(original.InteractiveConsole, console),
            "running an AI reply keeps the original R console and creates no window");
        Check(await console.GetScriptAsync() == editorBeforeRun, "running an AI reply preserves the editor's unrun changes");
        await RunAndWait(() => console.SubmitAsync("stopifnot(identical(session_only,123),identical(d,before-after),length(before)==9)", "script"),
            "running the reply sees existing R variables and keeps the session's objects");

        const string secondBlock = "document.querySelectorAll('[data-code=run]')[1]";
        Check(await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync(secondBlock + ".textContent") == "\"Run in this R session\"",
            "the R block button makes its immediate execution clear");
        await RunAndWait(() => console.SubmitAsync("d <- seq_len(9)", "script"), "the user changes an object in the current R workspace");
        await RunAndWait(() => pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync(secondBlock + ".click()"),
            "the selected AI block runs in the original R console");
        await RunAndWait(() => console.SubmitAsync("stopifnot(identical(d,seq_len(9)),identical(session_only,123))", "script"),
            "running a particular block does not rerun its preceding setup or the editor contents");
        Check(service.Documents.OfType<frmScript>().Count() == count, "per-block execution creates no other script window");
        string exportedBlock = ConversationRScript.Build(messages, 1, 1);
        Check(exportedBlock.Contains(source) && exportedBlock.IndexOf("d <- before - after", StringComparison.Ordinal) < exportedBlock.IndexOf("t.test(before", StringComparison.Ordinal),
            "the separate-script export still includes preceding definitions in order");

        pane.ShareScript.Checked = false;
        pane.Composer.Text = "PEFR_FOLLOWUP";
        await pane.SendAsync();
        await pane.Transcript.SettledAsync();
        string followup = ConversationRScript.Build(messages, 3);
        Check(followup.Contains(source) && !followup.Contains("999"), "a question-only follow-up retains the earlier approved snapshot without reading unshared edits");
        await RunConversationR(followup, "ai-pefr-followup", "cat('FOLLOWUP_CONTEXT_OK\\n')", "FOLLOWUP_CONTEXT_OK");
        Check(!ConversationRScript.Build(messages, 1, 0).Contains("stopifnot(length(d)"), "an older reply's export excludes later replies");
        string runFollowup = ConversationRScript.BuildForCurrentSession(messages, 3);
        Check(!runFollowup.Contains(source) && !runFollowup.Contains("d <- before - after"),
            "a follow-up runs without replaying the shared editor snapshot or past suggestions");
        await RunAndWait(() => { runInSession.PerformClick(); return Task.CompletedTask; }, "a follow-up runs in the same R workspace");

        pane.NewConversation();
        pane.Composer.Text = "A new unrelated example";
        await pane.SendAsync();
        await pane.Transcript.SettledAsync();
        var newMessages = (JsonArray)Field(Field(pane, "chat"), "Messages");
        Check(!ConversationRScript.BuildForCurrentSession(newMessages, 1).Contains("before <-"),
            "a new conversation does not replay old or unshared data");

        var language = (ComboBox)Field(original, "cboLanguage");
        language.SelectedIndex = 0;
        await pane.Transcript.SettledAsync();
        Check(await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-code=open]').textContent") == "\"Open in R\"",
            "switching the same script window to C# restores the separate Open in R action");
        language.SelectedIndex = 1;
        await pane.Transcript.SettledAsync();
        Check(await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-code=run]').textContent") == "\"Run in this R session\"",
            "switching the same script window to R restores Run in this R session");

        // The report pane has no R workspace and retains its review-before-run action.
        var report = (frmReportWebView)main.CreateReport();
        service.ShowReportAI(report);
        pane.Composer.Text = "Give me an R example";
        await pane.SendAsync();
        await pane.Transcript.SettledAsync();
        Check(await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-code=open]').textContent") == "\"Open in R\"",
            "Report AI keeps its separate Open in R action");
        await pane.Transcript.Browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-code=open]').click()");
        await Wait(() => service.Documents.OfType<frmScript>().Count() == count + 1, "Report AI opens a new, reviewable script");
        var review = (frmScript)main.ActiveMdiChild;
        Check(review.LearningScript.Contains("mean(x)") && !review.LearningRunning && review.LearningOutput.Length == 0,
            "Report AI leaves the suggestion for the user to review before running");
        pane.Hide();
        CloseTestDocument(review);
        CloseTestDocument(report);
        CloseTestDocument(original);

        grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Before","values":[312,242,340]},{"title":"After","values":[300,201,232]}]""")!.AsArray());
        var context=new QuestionContext([grid],true,CancellationToken.None);
        var data=await context.CallAsync("statsdirect_read_data",new JsonObject{["documentId"]=grid.Id(),["firstRow"]=2,["lastRow"]=4,["columns"]=new JsonArray(0,1)});
        string datasetId=RScriptWriter.Quote(data.S("datasetId"));
        string aliases=$"before <- .statsdirect_data[[{datasetId}]][['column_0']]\nafter <- .statsdirect_data[[{datasetId}]][['column_1']]\n";
        var snapshotMessages=new JsonArray(new JsonObject{["role"]="assistant",["rData"]=context.RData,["text"]="```r\n"+aliases+"d <- before - after\n```"});
        grid.LoadLearningExample(JsonNode.Parse("""[{"title":"Replaced","values":[999]}]""")!.AsArray());
        Check(data.S("rCode")==context.RData,"worksheet tools expose the same R bindings used by the local export");
        await RunConversationR(ConversationRScript.Build(snapshotMessages,0),"ai-workbook-snapshot","stopifnot(identical(before,c(312,242,340)),identical(after,c(300,201,232)))\ncat('WORKBOOK_CONTEXT_OK\\n')","WORKBOOK_CONTEXT_OK");
        snapshotMessages.Add(new JsonObject { ["role"] = "assistant", ["text"] = "```r\ncat(mean(.statsdirect_data[[" + datasetId + "]][['column_0']]))\n```" });
        string currentSession = ConversationRScript.BuildForCurrentSession(snapshotMessages, 1, 0);
        Check(currentSession.Contains(context.RData) && !currentSession.Contains("d <- before - after"),
            "the current-session action includes an earlier fixed workbook snapshot without re-executing the earlier reply");
        await RunConversationR(currentSession, "ai-session-workbook-snapshot", "cat('SESSION_WORKBOOK_CONTEXT_OK\\n')", "SESSION_WORKBOOK_CONTEXT_OK");
        var literal=new JsonObject{["datasetId"]="escaped",["columns"]=new JsonArray(new JsonObject{["index"]=3,["values"]=new JsonArray("quoted\"\\\ntext",null,"plain")},new JsonObject{["index"]=7,["values"]=new JsonArray(2,null,4)})};
        await RunConversationR(ConversationRScript.Dataset(literal),"ai-literal-data","stopifnot(identical(.statsdirect_data[['escaped']][['column_3']],c(\"quoted\\\"\\\\\\ntext\",NA,\"plain\")),identical(.statsdirect_data[['escaped']][['column_7']],c(2,NA,4)))\ncat('LITERAL_CONTEXT_OK\\n')","LITERAL_CONTEXT_OK");
        await TestSnapshotMetadata();
        Check(ConversationRScript.Blocks("```python\nno_r()\n```\n~~~RSCRIPT\nx <- 1\n~~~\n```r\nincomplete <-").SequenceEqual(new[]{"x <- 1"}),"R extraction accepts Rscript and tilde fences but excludes other languages and unfinished blocks");
        Check(ConversationRScript.Build(new JsonArray(new JsonObject{["role"]="user",["rSource"]=source,["text"]="```r\nmean(before)\n```"}),0,0).Contains(source),"R blocks in the user's own question still open with their source context");
    }
    static async Task TestSnapshotMetadata()
    {
        // A real random UUID can contain 999. Metadata and another approved column
        // must not be mistaken for the replacement value in the edited column.
        const string id = "99900000000040008000000000000001";
        var data = new JsonObject {
            ["datasetId"] = id, ["documentId"] = "StatsDirectForm:999",
            ["workbook"] = "Workbook 999", ["sheet"] = "Sheet 999", ["range"] = "A2:B3",
            ["columns"] = new JsonArray(
                new JsonObject { ["index"] = 0, ["values"] = new JsonArray(101, 102) },
                new JsonObject { ["index"] = 1, ["values"] = new JsonArray(999, 201) })
        };
        string frozen = ConversationRScript.Dataset(data);
        data["columns"][0]["values"][0] = 999;
        string exported = ConversationRScript.Build(new JsonArray(new JsonObject {
            ["role"] = "assistant", ["rData"] = frozen, ["text"] = "```r\ninvisible(.statsdirect_data)\n```"
        }), 0);
        Check(exported.Contains(frozen), "Open in R keeps the frozen snapshot when IDs, labels and other data contain the edited value");
        await RunConversationR(exported, "ai-snapshot-metadata",
            $"x <- .statsdirect_data[[{RScriptWriter.Quote(id)}]]\n" +
            "stopifnot(length(.statsdirect_data)==1,identical(x$column_0,c(101,102)),identical(x$column_1,c(999,201))," +
            "identical(attr(x,'statsdirect'),list(documentId='StatsDirectForm:999',workbook='Workbook 999',sheet='Sheet 999',range='A2:B3')))\ncat('SNAPSHOT_METADATA_OK\\n')",
            "SNAPSHOT_METADATA_OK");
    }
    static async Task RunConversationR(string code,string name,string assertions,string marker)
    {
        string folder=Path.Combine(artifacts,name);Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"check.R");File.WriteAllText(path,code+"\n"+assertions);
        string executable=Environment.GetEnvironmentVariable("STATSDIRECT_RSCRIPT")??Path.Combine(StatsDirect.R.RController.PreferredRVersion()?.BinPath??@"C:\Program Files\R\R-none\bin\x64","Rscript.exe");
        var start=new ProcessStartInfo(executable){WorkingDirectory=folder,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("--vanilla");start.ArgumentList.Add(path);
        using var process=Process.Start(start)!;
        Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));}catch{process.Kill(true);throw;}
        string output=await stdout+await stderr;File.WriteAllText(Path.Combine(folder,"output.txt"),output);
        Check(process.ExitCode==0&&output.Contains(marker)&&!output.Contains("Error"),name+": real R executes the exported script and validates the original data ("+output.Trim().TakeLast(100).Aggregate("",(s,c)=>s+c)+")");
    }
}

using StatsDirect.UI;
using StatsDirect.UI.Learning;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    static void TestSecurityLevelResolution()
    {
        var cases = new (object Configured, object LegacySharing, object LegacyTutor, AISecurityLevel Expected, string Description)[]
        {
            (null, null, null, AISecurityLevel.Unrestricted, "absent policy defaults to level 1"),
            (1, null, null, AISecurityLevel.Unrestricted, "explicit level 1 preserves standard AI"),
            (2, null, null, AISecurityLevel.DataSharingRestricted, "explicit level 2 restricts data sharing"),
            (3, null, null, AISecurityLevel.AIRestricted, "explicit level 3 restricts online AI"),
            (null, 1, null, AISecurityLevel.DataSharingRestricted, "legacy sharing policy applies without a level"),
            (null, null, 1, AISecurityLevel.AIRestricted, "legacy online tutor policy applies without a level"),
            (1, 1, 0, AISecurityLevel.DataSharingRestricted, "level 1 cannot relax legacy sharing restrictions"),
            (2, 0, 1, AISecurityLevel.AIRestricted, "level 2 cannot relax legacy online AI restrictions"),
            (3, 0, 0, AISecurityLevel.AIRestricted, "disabled legacy flags cannot relax level 3"),
            (1, 0, 0, AISecurityLevel.Unrestricted, "zero legacy flags impose no additional restrictions"),
            (0, null, null, AISecurityLevel.AIRestricted, "an invalid DWORD fails closed"),
            ("2", null, null, AISecurityLevel.AIRestricted, "an invalid registry type fails closed")
        };
        foreach (var entry in cases)
            Check(ChatGPTTutor.ResolveSecurityLevel(entry.Configured, entry.LegacySharing, entry.LegacyTutor) == entry.Expected, entry.Description);
    }

    static async Task TestAISecurityLevels(frmMain main, LearningService service, LearningView view, Action<AISecurityLevel> setPolicy)
    {
        const string prefix = "TRE_GUIDANCE_PROBE ";
        const string workbookTitle = "TRE_PRIVATE_WORKBOOK";
        const string scriptValue = "TRE_PRIVATE_R_FRAME";
        const string reportValue = "TRE_PRIVATE_REPORT";
        const int workbookValue = 271828;
        var workbook = (frmSpreadsheetGear)main.CreateGrid(workbookTitle);
        workbook.LoadLearningExample(new JsonArray(new JsonObject { ["title"] = "TRE_PRIVATE_COLUMN", ["values"] = new JsonArray(workbookValue) }));
        var script = service.OpenScript("d <- data.frame(label = '" + scriptValue + "')\n", "TRE_PRIVATE_SCRIPT", false);
        var report = (frmReportWebView)main.CreateReport();
        var previousConfirm = service.ConfirmSharing;
        ScriptAIPane pane = null;
        try
        {
            await report.LearningReport.Ready.WaitAsync(TimeSpan.FromSeconds(30));
            string result = await report.LearningReport.AppendAsync("<p>" + reportValue + "</p>", "TRE_PRIVATE_RESULT", "", 0);
            service.ShowScriptAI(script);
            pane = (ScriptAIPane)Field(service, "ai");
            async Task<JsonNode> AskPane(string question)
            {
                pane.Composer.Text = question;
                await pane.SendAsync();
                await pane.Transcript.SettledAsync();
                var messages = (JsonArray)Field(Field(pane, "chat"), "Messages");
                Check(messages.Last().S("role") == "assistant", "AI pane completes the probe: " + question);
                return JsonNode.Parse(messages.Last().S("text"));
            }

            pane.ShareScript.Checked = pane.ShareWorkbook.Checked = true;
            pane.Acknowledge.Checked = true;
            var scriptBaseline = await AskPane(prefix + "script baseline");
            Check(scriptBaseline["request"].S("referenceContext").Contains(scriptValue) && scriptBaseline.B("canReadData") && scriptBaseline.I("value") == workbookValue,
                "the normal Script AI path can send the approved R source and read the approved workbook");

            service.ShowReportAI(report, result);
            pane.ShareScript.Checked = pane.ShareWorkbook.Checked = true;
            pane.Acknowledge.Checked = true;
            var reportBaseline = await AskPane(prefix + "report baseline");
            Check(reportBaseline["request"].S("referenceContext").Contains(reportValue) && reportBaseline.B("canReadData"),
                "the normal Report AI path can send approved report content and read the workbook");

            view.Activate();
            await JS(view, "document.querySelector('[data-lesson=precision]').click()");
            service.ConfirmSharing = _ => LearningService.SharingDecision.Share;
            view.SourceId = workbook.Id();
            service.RefreshWorkspace();
            await LearningAsk(service, view, prefix + "TRE_PRIVATE_EARLIER_LEARNING");
            var learningBaseline = JsonNode.Parse(service.State.A("conversation").Last().S("text"));
            Check(learningBaseline.B("canReadData") && learningBaseline.I("value") == workbookValue,
                "the normal Learning path can read the approved workbook");

            var priorScope = new QuestionContext([workbook], false, CancellationToken.None, tutor: service.Tutor);
            var toolStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var toolResult = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            var inFlight = service.Tutor.ConverseAsync("", Message(prefix + "policy transition"), priorScope.Tools, false, null,
                (_, _) => { toolStarted.TrySetResult(); return toolResult.Task; }, CancellationToken.None);
            await toolStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            setPolicy(AISecurityLevel.DataSharingRestricted);
            toolResult.SetResult(new JsonObject { ["documents"] = new JsonArray(), ["value"] = "TRE_PRIVATE_IN_FLIGHT" });
            try
            {
                await inFlight.WaitAsync(TimeSpan.FromSeconds(15));
                throw new Exception("The policy must stop a pending request before it sends a tool result.");
            }
            catch (InvalidOperationException ex)
            {
                Check(ex.Message == ChatGPTTutor.DataSharingPolicyMessage,
                    "changing policy during a tool call stops the reply before its result is sent");
            }
            await Reject(() => priorScope.CallAsync("statsdirect_workspace", new()),
                "a context captured before the machine policy changed cannot return open document metadata");
            service.ConfirmSharing = _ => throw new Exception("Guidance-only mode must never request document-sharing consent.");
            int consentCount = service.State.A("sharingConsents").Count;
            await view.CallAsync("settings", service.Settings());
            await service.HandleAsync(view, new JsonObject { ["action"] = "workspace", ["choice"] = workbook.Id() });
            Check(service.Tutor.DataSharingDisabled && view.SourceId == "__none__",
                "machine policy refuses a stale Learning request to select an open workbook");
            Check((await JS(view, "document.body.hasAttribute('data-guidance-only') && (()=>{const picker=document.getElementById('workspaceChoice');return !picker || picker.disabled || picker.getClientRects().length===0;})() && !document.getElementById('send').disabled")).GetValue<bool>(),
                "Learning hides or locks its context chooser while retaining the Send action");

            void CheckGuidance(JsonNode received, string question, string paneName)
            {
                var request = received["request"];
                var turns = request.A("conversation");
                Check(turns.Count == 1 && turns[0].S("role") == "user" && turns[0].S("text") == question,
                    paneName + " sends exactly the current question, without earlier turns or lesson-title prefixes");
                Check(!request.ToJsonString().Contains("TRE_PRIVATE_") && !request.ToJsonString().Contains(workbookValue.ToString()) &&
                    !request.S("referenceContext").Contains("Local assessment course") && !received.B("canReadData"),
                    paneName + " shares no workbook, script, report, prior reply or learning settings, and rejects the data tool call");
                string[] toolNames = received.A("tools").Select(t => t.GetValue<string>()).ToArray();
                Check(toolNames.SequenceEqual(["statsdirect_method_help"]),
                    paneName + " offers bundled method help without document reading or analysis tools");
            }

            view.SourceId = workbook.Id(); // Simulate an old selector event reaching Send before the UI refresh.
            string lessonQuestion = prefix + "paired test guidance";
            await LearningAsk(service, view, lessonQuestion);
            var lessonProbe = JsonNode.Parse(service.State.A("conversation").Last().S("text"));
            CheckGuidance(lessonProbe, lessonQuestion, "Learning");
            Check(lessonProbe["request"].S("referenceContext").Contains(service.Lesson("precision").S("title")) &&
                service.State.A("sharingConsents").Count == consentCount,
                "the bundled lesson remains available to the tutor without asking for sharing consent");

            var pack = LearningJson.Read(Path.Combine(service.Storage, "course-pack.json"), 2_000_000);
            var importedLesson = pack.A("lessons")[0];
            string importedId = "course:" + pack.S("id") + ":" + pack.S("version") + ":" + importedLesson.S("id");
            await JS(view, "document.querySelector(" + JsonSerializer.Serialize("[data-lesson='" + importedId + "']") + ").click()");
            string importedQuestion = prefix + "help with study design";
            await LearningAsk(service, view, importedQuestion);
            var importedProbe = JsonNode.Parse(service.State.A("conversation").Last().S("text"));
            CheckGuidance(importedProbe, importedQuestion, "Learning from an imported lesson");
            Check(!importedProbe.ToJsonString().Contains(importedLesson.S("title")) &&
                !importedProbe.ToJsonString().Contains(pack.A("documents")[0].S("title")),
                "imported lesson titles, material and excerpts stay local while the user can still ask for guidance");

            service.ShowScriptAI(script);
            Check(!pane.SharingOptions.Enabled && !pane.ShareScript.Enabled && !pane.ShareWorkbook.Enabled && pane.Send.Enabled,
                "policy locks Script AI sharing controls without disabling Send");
            string scriptQuestion = prefix + "how to compare paired data in R";
            CheckGuidance(await AskPane(scriptQuestion), scriptQuestion, "Script AI");

            service.ShowReportAI(report, result);
            Check(!pane.SharingOptions.Enabled && pane.Send.Enabled, "policy locks Report AI sharing controls without disabling Send");
            string reportQuestion = prefix + "how to report a confidence interval";
            CheckGuidance(await AskPane(reportQuestion), reportQuestion, "Report AI");

            await Reject(() => service.Tutor.ConverseAsync("TRE_PRIVATE_BYPASS", Message("Would this bypass the policy?"), new(), false,
                null, (_, _) => Task.FromResult<JsonNode>(new JsonObject()), CancellationToken.None),
                "the AI transport refuses a stale unrestricted caller after the policy takes effect");

            view.Activate();
            await JS(view, "document.getElementById('recordNav').click()");
            Check((await JS(view, "Array.from(document.getElementById('evidenceDocument').options).some(o=>o.value===" + JsonSerializer.Serialize(script.Id()) + ")")).GetValue<bool>(),
                "local evidence capture still lists R scripts while AI sharing is locked");

            setPolicy(AISecurityLevel.AIRestricted);
            await view.CallAsync("settings", service.Settings());
            await JS(view, "document.querySelector('[data-lesson=precision]').click()");
            Check(service.Tutor.Disabled && !service.Settings().B("configured") &&
                (await JS(view, "document.getElementById('send').disabled && document.querySelector('[data-action=r]')?.disabled === false")).GetValue<bool>(),
                "level 3 disables online Learning while keeping the bundled R example available");

            const string blockedRequest = "restricted-bridge-probe";
            await service.HandleAsync(view, new JsonObject { ["action"] = "ask", ["id"] = blockedRequest, ["lesson"] = "precision" });
            Check(view.ReplyEvent.S("action") == "tutorError" && view.ReplyEvent["value"].S("message") == ChatGPTTutor.PolicyMessage,
                "level 3 rejects a stale request through the Learning bridge");

            foreach (var (document, name) in new (StatsDirectForm Document, string Name)[] { (script, "Script AI"), (report, "Report AI") })
            {
                if (document is frmScript rScript) service.ShowScriptAI(rScript);
                else service.ShowReportAI((frmReportWebView)document, result);
                var messages = (JsonArray)Field(Field(pane, "chat"), "Messages");
                int messageCount = messages.Count;
                const string draft = "Question kept for local review";
                pane.Composer.Text = draft;
                await pane.SendAsync();
                Check(!pane.Send.Enabled && messages.Count == messageCount && pane.Composer.Text == draft &&
                    ((Label)Field(pane, "status")).Text == ChatGPTTutor.PolicyMessage,
                    "level 3 blocks " + name + " Send and preserves the local draft");
            }

            await ExpectAIRestriction(() => service.Tutor.SignInAsync(), "level 3 refuses online sign-in");
            await ExpectAIRestriction(() => service.Tutor.ConverseAsync("", Message("Would guidance still go out?"), new(), false,
                null, (_, _) => Task.FromResult<JsonNode>(new JsonObject()), CancellationToken.None, guidanceOnly: true),
                "level 3 refuses even guidance-only requests at the transport");
        }
        finally
        {
            setPolicy(AISecurityLevel.Unrestricted);
            service.ConfirmSharing = previousConfirm;
            pane?.Hide();
            service.ShareAIDocument = service.ShareAIWorkbook = false;
            CloseTestDocument(report);
            CloseTestDocument(script);
            CloseTestDocument(workbook);
            if (!view.IsDisposed) await view.CallAsync("settings", service.Settings());
        }
    }

    static async Task ExpectAIRestriction(Func<Task> action, string description)
    {
        try { await action(); }
        catch (InvalidOperationException ex) when (ex.Message == ChatGPTTutor.PolicyMessage)
        {
            Check(true, description);
            return;
        }
        throw new Exception(description);
    }
}

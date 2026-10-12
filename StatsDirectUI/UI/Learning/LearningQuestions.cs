using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed partial class LearningService
{
    internal enum SharingDecision { Share, LessonsOnly, Cancel }
    internal Func<StatsDirectForm[], SharingDecision> ConfirmSharing { get; set; }
    internal static SharingDecision AskSharing(IWin32Window owner, StatsDirectForm[] documents)
    {
        using var form = new Form { Text = "Share document context for this question?", Font = new Font("Arial", 10), ClientSize = new Size(570, 380), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(525, 0), Text = "OpenAI will receive these document names and headings and may read their values or text for this question. The learning assistant can run supported StatsDirect analyses on approved worksheet ranges and add results to your report. Remove names, dates of birth, patient IDs and identifying free text. Earlier shared content may remain in the conversation." });
        layout.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = string.Join(Environment.NewLine, documents.Select(d => "• " + d.Text + " (" + Kind(d) + ")")) });
        var acknowledge = new CheckBox { Name = "sharingAcknowledgement", AutoSize = true, MaximumSize = new Size(525, 0), Text = "I have checked these documents and confirm they contain no person identifiers." }; layout.Controls.Add(acknowledge);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill }; var share = new Button { Name = "shareForQuestion", AutoSize = true, Text = "Share for this question", Enabled = false, DialogResult = DialogResult.Yes };
        var none = new Button { AutoSize = true, Text = "Use lessons only", DialogResult = DialogResult.No }; var cancel = new Button { AutoSize = true, Text = "Cancel", DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([share, none, cancel]); layout.Controls.Add(buttons); form.Controls.Add(layout); form.CancelButton = cancel; form.AcceptButton = none;
        acknowledge.CheckedChanged += (_, _) => share.Enabled = acknowledge.Checked;
        return form.ShowDialog(owner) switch { DialogResult.Yes when acknowledge.Checked => SharingDecision.Share, DialogResult.No => SharingDecision.LessonsOnly, _ => SharingDecision.Cancel };
    }
    private async Task AskAsync(LearningView view, JsonObject body)
    {
        string id = body.S("id");
        if (id.Length == 0) return;
        if (question != null) { await view.CallAsync("tutorError", new JsonObject { ["id"] = id, ["message"] = "Wait for the current question or stop it first." }); return; }
        using var pending = new CancellationTokenSource(); question = pending; requestId = id;
        try
        {
            if (ChatGPTTutor.Disabled) throw new InvalidOperationException(ChatGPTTutor.PolicyMessage);
            if (State?["quiz"].S("mode") == "test" && State["quiz"]?["completedAt"] == null) throw new InvalidOperationException("Finish or end the independent practice before asking the tutor.");
            if (Tutor.Email == null) throw new InvalidOperationException("Choose Use my ChatGPT to connect, then send your question.");
            var lesson = Lesson(body.S("lesson")) ?? throw new InvalidDataException("Choose a current lesson.");
            string choice = view.SourceId;
            var approved = choice == "__none__" ? Array.Empty<StatsDirectForm>() : Documents.Where(d => choice.Length == 0 || d.Id() == choice).ToArray();
            // Capture revisions before displaying consent; no snapshot is sent or read until it is granted.
            var revisions = approved.OfType<frmSpreadsheetGear>().ToDictionary(d => d.Id(), d => d.LearningVersion);
            if (approved.Length > 0)
            {
                var decision = ConfirmSharing?.Invoke(approved) ?? AskSharing(main, approved);
                pending.Token.ThrowIfCancellationRequested();
                if (decision == SharingDecision.Cancel) throw new OperationCanceledException("Document sharing cancelled. No question or document context was sent.");
                if (decision == SharingDecision.LessonsOnly) { approved = []; view.SourceId = "__none__"; }
                else
                {
                    if (approved.Any(d => d.IsDisposed || d.MdiParent == null || d is frmSpreadsheetGear g && g.LearningVersion != revisions[g.Id()])) throw new InvalidOperationException("A document changed while you reviewed sharing. Review it again for a new question.");
                    var consent = new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["requestID"] = id, ["policyVersion"] = "document-sharing-v1", ["noPersonIdentifiers"] = true, ["statement"] = "Share only the listed documents for this question. I have checked that they contain no person identifiers.", ["documents"] = new JsonArray(approved.Select(d => (JsonNode)new JsonObject { ["id"] = d.Id(), ["title"] = d.Text, ["kind"] = Kind(d), ["version"] = d is frmSpreadsheetGear g ? g.LearningVersion : 0 }).ToArray()) };
                    var result = await view.CallAsync("recordSharingConsent", consent);
                    SaveState(result?["state"]); // Must persist before granting access.
                }
            }
            var workspace = new QuestionContext(approved, false, pending.Token);
            string context = TeachingContext(lesson, body);
            string metadata = workspace.InitialContext().ToJsonString();
            if (metadata.Length > 14000) throw new InvalidOperationException("The selected document context is too large. Choose fewer documents for this question.");
            context += "\nCURRENT APPROVED WORKSPACE (separate from teaching examples):\n" + metadata;
            var reply = await Tutor.ConverseAsync(context, body.A("messages"), workspace.Tools, false,
                text => { if (question == pending && !view.IsDisposed) _ = view.CallAsync("partial", new JsonObject { ["id"] = id, ["text"] = text }); }, workspace.CallAsync, pending.Token);
            workspace.Check();
            // Rendering the reply re-enables Send in the shared web UI. Release this request
            // first, so an immediate next question cannot hit a stale busy flag.
            if (question == pending) { question = null; requestId = null; }
            await view.CallAsync("reply", new JsonObject { ["id"] = id, ["text"] = reply.Text, ["model"] = reply.Model, ["responseID"] = reply.ResponseID, ["promptVersion"] = "statsdirect-chatgpt-tutor-v4", ["workspaceSources"] = new JsonArray(approved.Select(d => (JsonNode)JsonValue.Create(d.Text)).ToArray()), ["courseSources"] = new JsonArray(new JsonObject { ["id"] = "library:" + lesson.S("id"), ["title"] = lesson.S("title") + " · " + lesson.S("contentVersion") }) });
        }
        catch (Exception ex)
        {
            if (question == pending) { question = null; requestId = null; }
            if (!view.IsDisposed) await view.CallAsync("tutorError", new JsonObject { ["id"] = id, ["message"] = ex is OperationCanceledException ? "Reply stopped. No further document content will be shared. Your question is kept in the learning record." : ex.Message });
        }
        finally { if (question == pending) { question = null; requestId = null; } if (question == null) RefreshWorkspace(); }
    }
    internal string TeachingContext(JsonNode lesson, JsonObject body)
    {
        var goals = new JsonObject();
        foreach (string key in new[] { "statisticalSkills", "needs", "qualifications", "priorKnowledge", "targetDate", "style" }) goals[key] = body["learningGoals"].S(key).Limit(1000);
        var training = new JsonObject();
        // Reviewer email and the learner's local identity are for export, not tutor context.
        foreach (string key in new[] { "providerName", "courseTitle", "courseVersion", "requirements", "cpdStatement", "assessmentType" }) training[key] = State?["training"].S(key).Limit(500);
        var intro = new JsonObject { ["profile"] = body.S("profile").Limit(100), ["stage"] = body.S("stage").Limit(100), ["learningGoals"] = goals, ["training"] = training, ["practiceContext"] = body.S("practiceContext").Limit(6000) };
        string prefix = intro.ToJsonString().Limit(8000);
        var curated = new JsonObject();
        foreach (string field in new[] { "id", "title", "objective", "summary", "steps", "challenge", "r", "contentVersion", "review", "prerequisites", "learningObjectives", "keyConcepts", "misconceptions", "workedExample", "teachingPrompts", "rProgression", "sources", "assessmentIds", "practiceQuestions" })
            if (lesson[field] != null) curated[field] = lesson[field].DeepClone();
        string material = curated.ToJsonString();
        if (material.Length > 10000) { curated.Remove("r"); material = curated.ToJsonString(); }
        if (material.Length > 10000) throw new InvalidOperationException("This lesson is too large for tutor context. Use its guide and ask a shorter question.");
        var excerpts = CoursePack.Excerpts(pack, body.A("messages").LastOrDefault().S("text") + " " + lesson.S("topic"));
        string sources = excerpts.ToJsonString();
        while (prefix.Length + material.Length + sources.Length > 17000 && excerpts.Count > 0) { excerpts.RemoveAt(excerpts.Count - 1); sources = excerpts.ToJsonString(); }
        return "CURRENT LEARNING OPTIONS (supersede older replies; respect skill level and R experience):\n" + prefix + "\nCURATED LESSON, separate from open worksheet data:\n" + material + "\nCOURSE EXCERPTS (only these excerpts have been read; cite their IDs/titles/URLs):\n" + sources;
    }
}

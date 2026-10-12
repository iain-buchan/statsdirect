using StatsDirect.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed partial class LearningService : IDisposable
{
    private readonly frmMain main;
    internal string Storage { get; }
    internal ChatGPTTutor Tutor { get; }
    internal LearningView View { get; private set; }
    internal JsonObject State { get; private set; }
    private JsonObject pack;
    private readonly JsonArray lessons;
    private bool canSave, disposed, configuringTutor;
    private CancellationTokenSource question;
    private string requestId;
    private ScriptAIPane ai;
    internal LearningService(frmMain main, string storage = null, ChatGPTTutor tutor = null)
    {
        this.main = main;
        Storage = storage ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StatsDirect", "Learning");
        Tutor = tutor ?? ChatGPTTutor.OnDemand(Storage);
        Tutor.Changed += UpdateConnection;
        lessons = LearningJson.Read(Path.Combine(SDConfiguration.InstallationDirectory, "Learn", "lessons.json"), 2_000_000) as JsonArray ?? new();
        string packPath = Path.Combine(Storage, "course-pack.json");
        if (File.Exists(packPath)) { try { pack = CoursePack.Validate(LearningJson.Read(packPath, 2_000_000)); } catch (Exception) { /* Preserve an unreadable pack; the learner can reimport a reviewed copy. */ } }
    }
    internal static string Kind(StatsDirectForm doc) => doc is frmSpreadsheetGear ? "grid" : doc is frmScript ? "r" : doc is frmReportWebView ? "report" : "learn";
    internal StatsDirectForm[] Documents => main.MdiChildren.OfType<StatsDirectForm>().Where(d => d is frmSpreadsheetGear or frmScript or frmReportWebView).ToArray();
    internal void Show(bool options)
    {
        if (View == null || View.IsDisposed)
        { View = new LearningView(this, Storage) { OptionsOnReady = options }; main.AddLearningDocument(View); }
        else { View.Activate(); if (options) _ = View.CallAsync("showOptions"); }
    }
    internal void ShowScriptAI(frmScript script) { ai ??= new ScriptAIPane(main, this); ai.Open(script); }
    internal void ActiveScriptChanged(frmScript script) { if (ai?.IsOpen == true) ai.Open(script); }
    internal void RefreshWorkspace()
    {
        ai?.RefreshWorkbooks();
        if (View == null || View.IsDisposed || !View.Ready.Task.IsCompletedSuccessfully || question != null) return;
        var choices = new JsonArray(Documents.Select(d => (JsonNode)new JsonObject { ["id"] = d.Id(), ["title"] = d.Text, ["kind"] = Kind(d) }).ToArray());
        string label = View.SourceId == "__none__" ? "Lessons only · open documents are not shared" : View.SourceId == "" ? "All open documents · confirmation required when you send" : "Confirm sharing when you send: " + Documents.FirstOrDefault(d => d.Id() == View.SourceId)?.Text;
        _ = View.CallAsync("workspace", new JsonObject { ["choice"] = View.SourceId, ["choices"] = choices, ["label"] = label });
    }
    private void UpdateConnection()
    {
        if (disposed) return;
        if (View?.IsDisposed == false) _ = View.CallAsync("settings", Settings());
        ai?.UpdateConnection();
    }
    internal JsonObject Settings() => new() { ["configured"] = !ChatGPTTutor.Disabled && Tutor.Email != null, ["disabled"] = ChatGPTTutor.Disabled, ["service"] = "ChatGPT", ["label"] = ChatGPTTutor.Disabled ? "Online tutor disabled" : Tutor.Status, ["signingIn"] = !ChatGPTTutor.Disabled && Tutor.LoginUrl != null };
    internal async Task HandleAsync(LearningView view, JsonObject body)
    {
        if (view != View || disposed) return;
        switch (body.S("action"))
        {
            case "ready":
                try
                {
                    string path = Path.Combine(Storage, "portfolio.json");
                    var state = File.Exists(path) ? LearningJson.Read(path, 10_000_000) : null;
                    if (state != null && (state is not JsonObject || state.I("schemaVersion") != 2)) throw new InvalidDataException("The saved learning record has an unsupported format. It has not been overwritten.");
                    canSave = true; await view.CallAsync("restore", state); await view.CallAsync("settings", Settings());
                    await view.CallAsync("coursePack", PackInfo());
                    await view.CallAsync("diagnostics", new JsonObject { ["appVersion"] = Application.ProductVersion, ["engineVersion"] = Application.ProductVersion, ["tutorRuntime"] = TutorRuntimeInstallation.Version, ["promptVersion"] = "statsdirect-chatgpt-tutor-v4", ["libraryVersion"] = lessons.FirstOrDefault().S("contentVersion") });
                    view.Loaded(); RefreshWorkspace();
                    if (view.OptionsOnReady) { view.OptionsOnReady = false; await view.CallAsync("showOptions"); }
                    if (!ChatGPTTutor.Disabled) _ = RefreshAccountAsync();
                }
                catch (Exception ex) { canSave = false; await view.CallAsync("loadError", JsonValue.Create(ex.Message)); view.Ready.TrySetException(ex); }
                break;
            case "save":
                SaveState(body["state"]);
                await view.CallAsync("saved", body["revision"]?.DeepClone()); break;
            case "settings": await ConfigureTutorAsync(view); break;
            case "workspace":
                if (question != null) break;
                string choice = body.S("choice", view.SourceId);
                if (choice is "" or "__none__" || Documents.Any(d => d.Id() == choice)) view.SourceId = ChatGPTTutor.Disabled ? "__none__" : choice;
                RefreshWorkspace(); break;
            case "ask": await AskAsync(view, body); break;
            case "cancel": if (body.S("id") == requestId) CancelQuestion(); break;
            case "help":
                var helpLesson = Lesson(body.S("lesson")); if (helpLesson != null && CoursePack.HelpPath(helpLesson.S("help")) && helpLesson.S("help").Length > 0) SdApplication.SoleInstance.ShowHelp(main, helpLesson.S("help")); break;
            case "example": case "r": OpenExample(body.S("lesson"), body.S("action") == "r"); break;
            case "copyCode": if (body.S("code").Length is > 0 and <= 100_000) Clipboard.SetText(body.S("code")); break;
            case "openRCode": if (body.S("code").Length <= 100_000) OpenScript(body.S("code"), "Tutor example", false); break;
            case "exampleCourse": ImportPack(Path.Combine(SDConfiguration.InstallationDirectory, "Learn", "provider-course-example.json")); await view.CallAsync("coursePack", PackInfo()); break;
            case "importCoursePack":
                if (question != null) throw new InvalidOperationException("Wait for the reply or stop it before changing course packs.");
                using (var dialog = new OpenFileDialog { Title = "Import course materials — relevant extracts accompany questions sent to OpenAI", Filter = "Course materials|*.json;*.pdf;*.txt;*.md", Multiselect = true })
                    if (dialog.ShowDialog(main) == DialogResult.OK) { ImportPack(dialog.FileNames); await view.CallAsync("coursePack", PackInfo()); await view.CallAsync("notice", JsonValue.Create("Course pack imported locally. Excerpts accompany your next tutor question; nothing was sent during import.")); }
                break;
            case "importProviderReview":
                using (var dialog = new OpenFileDialog { Title = "Import provider assessment response", Filter = "JSON response|*.json" })
                    if (dialog.ShowDialog(main) == DialogResult.OK) await view.CallAsync("providerReview", LearningJson.Read(dialog.FileName, 200_000));
                break;
            case "captureLearningEvidence": await CaptureEvidenceAsync(body.S("documentID")); break;
            case "export": await ExportAsync(body); break;
            case "email":
                string draft = PrepareEmail(body); Process.Start(new ProcessStartInfo(draft) { UseShellExecute = true });
                await view.CallAsync("notice", JsonValue.Create("Email draft prepared with TXT and JSON attachments. Review and send it in your mail application. If it cannot open .eml drafts, attach the files from " + Path.GetDirectoryName(draft) + ". No email has been sent by StatsDirect.")); break;
        }
    }
    internal void SaveState(JsonNode state)
    {
        if (!canSave || state is not JsonObject || state.I("schemaVersion") != 2 || state.S("id").Length == 0) throw new InvalidDataException("The learning record is not ready to save. Export a copy before closing.");
        LearningJson.Save(Path.Combine(Storage, "portfolio.json"), state); State = (JsonObject)state.DeepClone();
    }
    internal bool SaveCurrent() { if (State == null) return false; SaveState(State); return true; }
    internal void ImportPack(params string[] paths)
    {
        if (question != null) throw new InvalidOperationException("Wait for the reply or stop it before changing course packs.");
        var imported = CoursePack.Import(paths); string target = Path.Combine(Storage, "course-pack.json");
        Directory.CreateDirectory(Storage);
        if (File.Exists(target)) { string archive = Path.Combine(Storage, "Previous course packs"); Directory.CreateDirectory(archive); File.Copy(target, Path.Combine(archive, Guid.NewGuid() + ".json")); }
        LearningJson.Save(target, imported, 2_000_000); pack = imported;
    }
    private JsonNode PackInfo()
    {
        if (pack == null) return null;
        var info = (JsonObject)pack.DeepClone();
        info["documents"] = new JsonArray(pack.A("documents").Select(d => (JsonNode)new JsonObject { ["id"] = d.S("id"), ["title"] = d.S("title") }).ToArray());
        info["textBytes"] = pack.A("documents").Sum(d => Encoding.UTF8.GetByteCount(d.S("text"))); return info;
    }
    internal JsonNode Lesson(string id)
    {
        var lesson = lessons.FirstOrDefault(l => l.S("id") == id); if (lesson != null) return lesson;
        if (pack?.I("schemaVersion") == 2)
        {
            string prefix = "course:" + pack.S("id") + ":" + pack.S("version") + ":";
            if (id.StartsWith(prefix, StringComparison.Ordinal))
            { var found = pack.A("lessons").FirstOrDefault(l => l.S("id") == id[prefix.Length..])?.DeepClone(); if (found != null) { found["id"] = id; found["providerCourse"] = true; } return found; }
        }
        return null;
    }
    internal frmScript OpenScript(string script, string title, bool run)
    { var doc = (frmScript)main.CreateScriptWindow(); doc.ShowR(script, title, runImmediately: run); return doc; }
    internal void OpenExample(string id, bool r)
    {
        var lesson = Lesson(id) ?? throw new InvalidDataException("Choose a lesson in the current library.");
        if (r) { OpenScript(lesson.S("r"), lesson.S("title"), !lesson.B("providerCourse")); return; }
        StatsDirectForm source = null;
        if (lesson.A("columns").Count > 0) { var grid = (frmSpreadsheetGear)main.CreateGrid("Example · " + lesson.S("title")); grid.LoadLearningExample(lesson.A("columns")); source = grid; }
        main.OpenLearningAnalysis(lesson.S("operation"), source);
    }
    private async Task RefreshAccountAsync() { try { await Tutor.RefreshAccountAsync(); } catch (Exception ex) { if (View?.IsDisposed == false) await View.CallAsync("notice", JsonValue.Create(ex.Message)); } }
    internal async Task ConfigureTutorAsync(IWin32Window owner)
    {
        if (configuringTutor || disposed) return;
        configuringTutor = true;
        try { await ConfigureTutorCoreAsync(owner); }
        finally { configuringTutor = false; }
    }
    private async Task ConfigureTutorCoreAsync(IWin32Window owner)
    {
        if (ChatGPTTutor.Disabled) throw new InvalidOperationException(ChatGPTTutor.PolicyMessage);
        if (Tutor.Runtime != null && !await Task.Run(() => Tutor.Runtime.IsValidAsync(CancellationToken.None)))
        {
            if (disposed || owner is Control { IsDisposed: true }) return;
            using var setup = new frmInstallTutor(Tutor.Runtime);
            if (setup.ShowDialog(owner) != DialogResult.OK) return;
        }
        if (disposed) return;
        if (ChatGPTTutor.Disabled) throw new InvalidOperationException(ChatGPTTutor.PolicyMessage);
        await Tutor.RefreshAccountAsync();
        if (Tutor.Email != null)
        {
            if (MessageBox.Show(owner, "Connected as " + Tutor.Email + ". StatsDirect uses this account's Codex access and allowance. Questions and recent conversation go to OpenAI when you send. Document sharing starts off and needs confirmation for each question.\n\nSign out of StatsDirect's ChatGPT connection?", "Tutor connection", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) await Tutor.SignOutAsync();
        }
        else if (Tutor.LoginUrl != null)
        {
            var result = MessageBox.Show(owner, "Finish sign-in in your browser. Your question is kept here.\n\nYes: reopen the sign-in page. No: cancel sign-in.", "Finish ChatGPT sign-in", MessageBoxButtons.YesNoCancel);
            if (result == DialogResult.Yes) Process.Start(new ProcessStartInfo(Tutor.LoginUrl.AbsoluteUri) { UseShellExecute = true });
            if (result == DialogResult.No) await Tutor.CancelSignInAsync();
        }
        else if (MessageBox.Show(owner, "Sign in with your own ChatGPT account; no API key is needed. Your plan's Codex access and allowance apply.\n\nEach Send shares your question and recent conversation with OpenAI. Learning also includes your learning preferences and relevant lesson/course extracts. Document sharing starts off and needs confirmation for each question. Do not include person identifiers. Existing ChatGPT chats are not imported.\n\nOpen the sign-in page?", "Use my ChatGPT", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
            Process.Start(new ProcessStartInfo((await Tutor.SignInAsync()).AbsoluteUri) { UseShellExecute = true });
    }
    internal void CancelQuestion() { question?.Cancel(); }
    public void Dispose() { disposed = true; question?.Cancel(); ai?.Dispose(); Tutor.Changed -= UpdateConnection; Tutor.Dispose(); }
}

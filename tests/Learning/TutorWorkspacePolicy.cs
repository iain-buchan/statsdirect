using StatsDirect.UI.Learning;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private const string TrustWorkspace = "aabbccdd-1111-4222-8333-444444444444";
    private const string ReplacementWorkspace = "bbccddaa-1111-4222-8333-444444444444";

    static async Task TestTutorWorkspacePolicy(string node)
    {
        Check(ChatGPTTutor.ResolveWorkspaceId(null) == null, "an unconfigured workspace policy leaves the optional pin unset");
        Check(ChatGPTTutor.ResolveWorkspaceId(TrustWorkspace.ToUpperInvariant()) == TrustWorkspace,
            "the managed workspace UUID is normalized for Codex");
        foreach (object malformed in new object[] { "", " ", 2, Guid.Empty.ToString("D"), "{" + TrustWorkspace + "}", TrustWorkspace + "\"\ninvalid" })
        {
            await ExpectWorkspacePolicyError(() => Task.FromResult(ChatGPTTutor.ResolveWorkspaceId(malformed)),
                ChatGPTTutor.InvalidWorkspacePolicyMessage, "invalid workspace setting fails closed (" + (malformed is string s ? "string length " + s.Length : "non-string") + ")");
        }

        (ChatGPTTutor Tutor, string Probe) Open(string name, Func<AISecurityLevel> level, Func<object> pin, params string[] extra)
        {
            string storage = Path.Combine(root, "workspace-policy-" + name);
            Directory.CreateDirectory(storage);
            string probe = Path.Combine(storage, "mock-workspace-probe.jsonl");
            var args = new[] { Path.Combine(AppContext.BaseDirectory, "mock-server.mjs"), "--workspace-policy-probe", probe }.Concat(extra).ToArray();
            return (new ChatGPTTutor(node, storage, Path.Combine(AppContext.BaseDirectory, "Tutor"), args)
                { ReadSecurityLevel = level, ReadWorkspaceId = pin }, probe);
        }

        static async Task SignIn(ChatGPTTutor tutor)
        {
            await tutor.SignInAsync();
            await Wait(() => tutor.Email == "mock@example.invalid", "the mocked ChatGPT account is connected");
        }

        var unrestricted = Open("level-1", () => AISecurityLevel.Unrestricted, () => "irrelevant invalid value");
        using (var tutor = unrestricted.Tutor)
        {
            await SignIn(tutor);
            CheckWorkspaceLaunch(unrestricted.Probe, null, "level 1 ignores the workspace setting and starts Codex without a pin");
        }

        var unpinned = Open("level-2-unconfigured", () => AISecurityLevel.DataSharingRestricted, () => null);
        using (var tutor = unpinned.Tutor)
        {
            await SignIn(tutor);
            CheckWorkspaceLaunch(unpinned.Probe, null, "level 2 without a configured workspace retains its existing guidance-only sign-in");
        }

        var invalid = Open("invalid", () => AISecurityLevel.DataSharingRestricted, () => TrustWorkspace + "\"\ninvalid");
        using (var tutor = invalid.Tutor)
        {
            await ExpectWorkspacePolicyError(() => tutor.SignInAsync(), ChatGPTTutor.InvalidWorkspacePolicyMessage,
                "level 2 rejects a malformed workspace before starting Codex");
            Check(!File.Exists(invalid.Probe), "the malformed workspace never reaches the child process");
        }

        var disabled = Open("level-3", () => AISecurityLevel.AIRestricted, () => TrustWorkspace);
        using (var tutor = disabled.Tutor)
        {
            await ExpectAIRestriction(() => tutor.SignInAsync(), "level 3 refuses sign-in even with a valid workspace setting");
            Check(!File.Exists(disabled.Probe), "level 3 does not start Codex");
        }

        object configuredPin = TrustWorkspace.ToUpperInvariant();
        var pinned = Open("level-2", () => AISecurityLevel.DataSharingRestricted, () => configuredPin);
        using (var tutor = pinned.Tutor)
        {
            Uri login = await tutor.SignInAsync();
            Check(login.Query == "?allowed_workspace_id=" + TrustWorkspace,
                "the pinned level 2 login link names the single approved workspace");
            await Wait(() => tutor.Email == "mock@example.invalid", "the app accepts the mock account routed to the approved workspace");
            CheckWorkspaceLaunch(pinned.Probe, TrustWorkspace, "level 2 pins only this StatsDirect Codex process");

            var toolStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var toolResult = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            var context = new QuestionContext([], true, CancellationToken.None);
            var pending = tutor.ConverseAsync("", Message("WORKSPACE_POLICY_TOOL_PROBE"), context.Tools, true, null,
                (_, _) => { toolStarted.SetResult(); return toolResult.Task; }, CancellationToken.None, guidanceOnly: true);
            await toolStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            configuredPin = ReplacementWorkspace;
            toolResult.SetResult(new JsonObject { ["text"] = "synthetic result" });
            await ExpectWorkspacePolicyError(() => pending, ChatGPTTutor.WorkspacePolicyChangedMessage,
                "a changed workspace stops an in-flight reply");
            Check(!WorkspaceProbe(pinned.Probe).Any(row => row.S("event") == "response"),
                "an in-flight tool result cannot be returned under a stale workspace");
            await ExpectWorkspacePolicyError(() => tutor.SignInAsync(), ChatGPTTutor.WorkspacePolicyChangedMessage,
                "a process launched for another workspace cannot start a fresh sign-in");
        }

        var restarted = Open("new-workspace", () => AISecurityLevel.DataSharingRestricted, () => configuredPin);
        using (var tutor = restarted.Tutor)
        {
            await SignIn(tutor);
            CheckWorkspaceLaunch(restarted.Probe, ReplacementWorkspace, "a fresh tutor uses the newly approved workspace");
            configuredPin = null;
            await ExpectWorkspacePolicyError(() => tutor.RefreshAccountAsync(), ChatGPTTutor.WorkspacePolicyChangedMessage,
                "removing a workspace pin stops use of the existing session until restart");
        }

        var level = AISecurityLevel.Unrestricted;
        var previouslyUnrestricted = Open("policy-added", () => level, () => TrustWorkspace);
        using (var tutor = previouslyUnrestricted.Tutor)
        {
            await SignIn(tutor);
            level = AISecurityLevel.DataSharingRestricted;
            await ExpectWorkspacePolicyError(
                () => tutor.ConverseAsync("", Message("new question"), new(), true, null,
                    (_, _) => Task.FromResult<JsonNode>(new JsonObject()), CancellationToken.None, guidanceOnly: true),
                ChatGPTTutor.WorkspacePolicyChangedMessage, "enabling a pin blocks requests through a previously unrestricted sign-in");
            Check(!WorkspaceProbe(previouslyUnrestricted.Probe).Any(row => row.S("method") is "thread/start" or "turn/start"),
                "the old unrestricted session does not receive the question");
        }

        var incorrectLogin = Open("wrong-login-url", () => AISecurityLevel.DataSharingRestricted, () => TrustWorkspace, "--wrong-workspace-url");
        using (var tutor = incorrectLogin.Tutor)
        {
            await ExpectWorkspacePolicyError(() => tutor.SignInAsync(), ChatGPTTutor.WorkspaceSignInMessage,
                "the tutor rejects a Codex browser sign-in link for the wrong workspace");
            Check(tutor.LoginUrl == null, "the mismatched sign-in URL is never exposed to the UI");
        }

        foreach (string accountShape in new[] { "wrong-workspace", "missing-workspace" })
        {
            var cached = Open(accountShape, () => AISecurityLevel.DataSharingRestricted, () => TrustWorkspace, "--cached-account-" + accountShape);
            using var tutor = cached.Tutor;
            await ExpectWorkspacePolicyError(() => tutor.RefreshAccountAsync(), ChatGPTTutor.WorkspaceSignInMessage,
                "a cached account with " + accountShape + " is refused");
            Check(tutor.Email == null, "an unverified cached account is not displayed as connected");
        }
    }

    static void CheckWorkspaceLaunch(string path, string expectedPin, string description)
    {
        var launch = WorkspaceProbe(path).Single(row => row.S("event") == "launch");
        string[] args = launch.A("arguments").Select(value => value.GetValue<string>()).ToArray();
        string[] overrides = args.Zip(args.Skip(1)).Where(pair => pair.First == "--config").Select(pair => pair.Second).ToArray();
        string[] pinArgs = overrides.Where(value => value.StartsWith("forced_chatgpt_workspace_id=", StringComparison.Ordinal)).ToArray();
        Check(expectedPin == null
            ? pinArgs.Length == 0
            : pinArgs.SequenceEqual(["forced_chatgpt_workspace_id=\"" + expectedPin + "\""]) &&
                overrides.Contains("forced_login_method=\"chatgpt\""), description);
        Check(!launch.S("config").Contains("forced_chatgpt_workspace_id", StringComparison.Ordinal),
            "the workspace override is passed on the command line and stays out of the generated tutor config");
    }

    static JsonNode[] WorkspaceProbe(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var entries = new List<JsonNode>();
        while (reader.ReadLine() is string line)
            entries.Add(JsonNode.Parse(line));
        return entries.ToArray();
    }

    static async Task ExpectWorkspacePolicyError(Func<Task> action, string expectedMessage, string description)
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(15)); }
        catch (Exception ex) when ((ex is InvalidOperationException or IOException) && ex.Message == expectedMessage)
        {
            Check(true, description);
            return;
        }
        throw new Exception("Expected workspace policy rejection for " + description);
    }
}

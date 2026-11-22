using System.Diagnostics;
using StatsDirect.UI.HtmlHelp;

internal static class CorruptIndexViewChecks
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);
    private const string IndexFailure = "Could not open the help index:";
    internal static async Task Run(Form owner, string assets, string output, Action<bool, string> check)
    {
        string fixture = Path.Combine(output, "corrupt-index-view");
        string copy = Path.Combine(fixture, "Help");
        foreach (string source in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(copy, Path.GetRelativePath(assets, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }

        (string Name, string Json, string Error)[] cases =
        [
            ("invalid JSON", "{ not valid JSON", "contains invalid JSON"),
            ("unsafe absolute target", """{"schemaVersion":1,"entries":[{"term":"Unsafe","title":"Unsafe","path":"https://example.com/"}]}""",
                "does not name a packaged topic")
        ];
        for (int number = 0; number < cases.Length; number++)
        {
            var test = cases[number];
            File.WriteAllText(Path.Combine(copy, "keyword-index.json"), test.Json);
            using var frame = new Form
            {
                Text = "Corrupt keyword index regression",
                Width = 900, Height = 780, ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-17000, -17000)
            };
            using var view = new HelpView(new HelpCatalog(copy), Path.Combine(fixture, "profile-" + number));
            frame.Controls.Add(view);
            var escaped = new List<Exception>();
            // Opening is async void. Catch the real UI-thread escape so a regression
            // fails this private fixture instead of displaying an exception dialog.
            System.Threading.ThreadExceptionEventHandler handler = (_, e) => escaped.Add(e.Exception);
            Application.ThreadException += handler;
            try
            {
                view.ShowTopic("1239");
                frame.Show(owner);
                await view.Ready.WaitAsync(TimeSpan.FromSeconds(45));
                var core = view.Browser.CoreWebView2;
                var index = view.Controls.OfType<ToolStrip>().SelectMany(strip => strip.Items.OfType<ToolStripDropDownButton>())
                    .Single(button => button.Text == "Index");
                var status = view.Controls.OfType<StatusStrip>().Single().Items[0];
                string State() => $"For {test.Name}, source '{core.Source}', Index enabled {index.Enabled}, status '{status.Text}'.";
                await Wait(() => index.Enabled && core.Source.EndsWith("/parametric_methods/paired_t.htm", StringComparison.Ordinal),
                    () => $"Waiting for the paired t-test topic to enable Index. {State()}");
                await core.ExecuteScriptAsync("window.corruptIndexSnapshot={url:location.href,history:history.length};window.corruptIndexToken='retained';")
                    .WaitAsync(WaitTimeout);
                index.ShowDropDown();
                await Wait(() => escaped.Count > 0 || !string.IsNullOrEmpty(status.Text) || index.DropDown.Visible,
                    () => $"Waiting for the Index command to report a result. {State()}");
                check(escaped.Count == 0, $"Index button handles {test.Name} without an unhandled exception" +
                    (escaped.Count == 0 ? "" : $": {escaped[0].GetType().Name}: {escaped[0].Message}"));
                bool expectedError = status.Text.StartsWith(IndexFailure, StringComparison.Ordinal)
                    && status.Text.Contains(test.Error, StringComparison.Ordinal);
                check(expectedError, $"Index button reports {test.Name} in the native status line" +
                    (expectedError ? "" : $" (actual status '{status.Text}')"));
                check(!index.DropDown.Visible, $"Index button does not show a picker for {test.Name}");
                string unchanged = await core.ExecuteScriptAsync("location.href===window.corruptIndexSnapshot.url && history.length===window.corruptIndexSnapshot.history && window.corruptIndexToken==='retained'")
                    .WaitAsync(WaitTimeout);
                check(unchanged == "true", $"Index button preserves the page, history and document after {test.Name}");
            }
            finally
            {
                Application.ThreadException -= handler;
                frame.Close();
            }
        }
    }

    private static async Task Wait(Func<bool> condition, Func<string> context)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed >= WaitTimeout)
                throw new TimeoutException($"{context()} Deadline {WaitTimeout.TotalSeconds:0} seconds.");
            await Task.Delay(50);
        }
    }
}

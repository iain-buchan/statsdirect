using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.WebReports;

/// <summary>The browser boundary; it has no access to the engine or arbitrary native objects.</summary>
internal sealed class ReportView : UserControl
{
    internal const string Origin = "https://reports.statsdirect.invalid";
    internal readonly WebView2 Browser = new() { Dock = DockStyle.Fill };
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> requests = new();
    private readonly string assets, userData;
    private bool starting, busy;
    private Task mutations = Task.CompletedTask;
    internal event Action Changed;
    internal event Action<int> TopicRequested;
    internal event Action<string> Notice;
    internal event Action<string> CommandRequested;
    internal int HelpContext { get; private set; }
    internal Task Ready => ready.Task;
    internal long Revision { get; private set; }

    internal ReportView(string assetDirectory, string userDataDirectory = null)
    {
        assets = assetDirectory;
        userData = userDataDirectory ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StatsDirect", "WebReports");
        Controls.Add(Browser);
        Load += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (starting) return;
        starting = true;
        try
        {
            if (!File.Exists(System.IO.Path.Combine(assets, "index.html"))) throw new FileNotFoundException("The Web Report assets are missing.");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData);
            await Browser.EnsureCoreWebView2Async(environment);
            var core = Browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.SetVirtualHostNameToFolderMapping("reports.statsdirect.invalid", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NewWindowRequested += (_, e) => { e.Handled = true; if (e.IsUserInitiated) OpenExternal(e.Uri); };
            bool firstNavigation = true;
            core.NavigationStarting += (_, e) =>
            {
                bool local = e.Uri.Split('#')[0] == Origin + "/index.html";
                if (local && firstNavigation) { firstNavigation = false; return; }
                if (local && e.Uri.Contains('#') && e.Uri != core.Source) return;
                e.Cancel = true;
                if (!local && e.IsUserInitiated) OpenExternal(e.Uri);
            };
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) =>
            {
                if (!e.Request.Uri.StartsWith(Origin + "/", StringComparison.Ordinal) && !e.Request.Uri.StartsWith("data:", StringComparison.Ordinal) && !e.Request.Uri.StartsWith("blob:" + Origin + "/", StringComparison.Ordinal))
                    e.Response = environment.CreateWebResourceResponse(null, 403, "External resources are disabled", "Content-Type: text/plain");
            };
            core.WebMessageReceived += Receive;
            core.ProcessFailed += (_, _) => Fail(new InvalidOperationException("The report browser stopped. Unsaved edits may be unavailable; reopen the last saved HTML report in a new window."));
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) Fail(new IOException("The report editor could not load: " + e.WebErrorStatus)); };
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex)
    {
        ready.TrySetException(ex);
        foreach (var p in requests.Values) p.TrySetException(ex);
        requests.Clear();
        Notice?.Invoke(ex.Message + " If the WebView2 Runtime is missing, run StatsDirectSetup.exe to install it.");
    }

    private static bool Trusted(string source) => Uri.TryCreate(source, UriKind.Absolute, out var u) && u.GetLeftPart(UriPartial.Authority) == Origin;
    private void Receive(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Trusted(e.Source)) return;
        try
        {
            if (e.WebMessageAsJson.Length > 90_000_000) throw new InvalidDataException("The report response is too large.");
            using var json = JsonDocument.Parse(e.WebMessageAsJson);
            var m = json.RootElement;
            string action = m.GetProperty("action").GetString();
            switch (action)
            {
                case "ready": ready.TrySetResult(); break;
                case "reply":
                    if (!requests.Remove(m.GetProperty("id").GetString(), out var request)) break;
                    if (m.TryGetProperty("error", out var error)) request.TrySetException(new InvalidOperationException(error.GetString()));
                    else request.TrySetResult(m.GetProperty("value").Clone());
                    break;
                case "changed": Revision = m.GetProperty("revision").GetInt64(); Changed?.Invoke(); break;
                case "context": HelpContext = m.GetProperty("context").GetInt32(); break;
                case "help": TopicRequested?.Invoke(m.GetProperty("context").GetInt32()); break;
                case "transferNotice": Notice?.Invoke(m.GetProperty("text").GetString()); break;
                case "clipboard": _ = ClipboardCommandAsync(m.GetProperty("command").GetString()); break;
                case "command":
                    string command = m.GetProperty("command").GetString();
                    if (command is "save" or "saveAs" or "open" or "print") CommandRequested?.Invoke(command);
                    break;
            }
        }
        catch (Exception ex) { Notice?.Invoke(ex.Message); }
    }

    internal async Task<JsonElement> CallAsync(string method, object args = null)
    {
        await Ready.WaitAsync(TimeSpan.FromSeconds(45));
        string id = Guid.NewGuid().ToString("N");
        var result = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        requests.Add(id, result);
        try
        {
            await Browser.CoreWebView2.ExecuteScriptAsync("WindowsReport.run(" + JsonSerializer.Serialize(new { id, method, args }) + ")");
            return await result.Task.WaitAsync(TimeSpan.FromSeconds(45));
        }
        finally { requests.Remove(id); }
    }

    internal Task AppendAsync(string html, string title, string operation, int helpContextId)
    {
        async Task AppendAfter(Task prior)
        {
            await prior;
            await CallAsync("append", new { id = Guid.NewGuid().ToString("D"), html, title, operation, helpContextId });
        }
        return mutations = AppendAfter(mutations);
    }

    internal async Task OpenHtmlAsync(string html, string title)
    {
        if (Encoding.UTF8.GetByteCount(html) > 30_000_000) throw new InvalidDataException("This report is too large to open (30 MB limit).");
        await mutations;
        Match metadata = Regex.Match(html, "<script\\s+type=\"application/json\"\\s+id=\"statsdirect-report-data\">(.*?)</script>", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (metadata.Success)
        {
            using var doc = JsonDocument.Parse(metadata.Groups[1].Value);
            if (doc.RootElement.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Unsupported saved report version.");
            await CallAsync("load", new { entries = doc.RootElement.GetProperty("entries").Clone() });
        }
        else await CallAsync("load", new { html, title });
        Revision = 0;
    }

    internal async Task<long> SaveAsync(string filename, string title, string format)
    {
        await mutations;
        byte[] data;
        if (format == "pdf")
        {
            await CallAsync("snapshot");
            string tempPdf = TemporaryName(filename);
            try
            {
                if (!await Browser.CoreWebView2.PrintToPdfAsync(tempPdf)) throw new IOException("The report could not be printed to PDF.");
                File.Move(tempPdf, filename, true);
            }
            finally { if (File.Exists(tempPdf)) File.Delete(tempPdf); }
            return Revision;
        }
        var exported = await CallAsync("export", new { format, title });
        string content = exported.GetProperty("content").GetString();
        data = format == "docx" ? Convert.FromBase64String(content) : Encoding.UTF8.GetBytes(content);
        if (format == "html" && data.Length > 30_000_000) throw new IOException("This report is too large to save and reopen as one HTML file (30 MB limit).");
        string temp = TemporaryName(filename);
        try { await File.WriteAllBytesAsync(temp, data); File.Move(temp, filename, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return exported.GetProperty("revision").GetInt64();
    }

    private static string TemporaryName(string filename) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(filename)), ".statsdirect-" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(filename));

    internal async Task ClipboardCommandAsync(string command)
    {
        if (busy || command is not ("copy" or "cut" or "paste")) return;
        busy = true;
        try
        {
            await mutations;
            if (command == "paste")
            {
                var target = await CallAsync("preparePaste");
                if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty("token", out var token)) return;
                IDataObject contents = Clipboard.GetDataObject();
                string html = contents?.GetData(ReportClipboard.FragmentFormat, false) as string;
                if (html == null && contents?.GetData(DataFormats.Html) is string publicHtml) html = ReportClipboard.DecodeHtml(publicHtml);
                string text = contents?.GetData(DataFormats.UnicodeText) as string ?? "";
                if ((html?.Length ?? 0) + text.Length > 30_000_000) throw new InvalidDataException("The clipboard content is too large.");
                await CallAsync("paste", new { token = token.GetString(), payload = new { html, text } });
            }
            else
            {
                var fragment = await CallAsync("prepareClipboard", new { cut = command == "cut" });
                if (fragment.ValueKind != JsonValueKind.Object || !fragment.TryGetProperty("token", out var token)) return;
                string html = fragment.GetProperty("html").GetString();
                using var office = JsonDocument.Parse((await CallAsync("officeClipboard", new { html })).GetString());
                var data = new DataObject();
                data.SetData(ReportClipboard.FragmentFormat, false, html);
                data.SetData(DataFormats.Html, ReportClipboard.EncodeHtml(office.RootElement.GetProperty("html").GetString()));
                data.SetData(DataFormats.UnicodeText, fragment.GetProperty("text").GetString());
                Clipboard.SetDataObject(data, true, 5, 100);
                // A failed clipboard write never removes the selected content.
                if (command == "cut") await CallAsync("cut", new { token = token.GetString() });
            }
        }
        catch (Exception ex) { Notice?.Invoke(ex.Message); }
        finally { busy = false; }
    }

    private void OpenExternal(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto")
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { Notice?.Invoke(ex.Message); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { ready.TrySetCanceled(); foreach (var p in requests.Values) p.TrySetCanceled(); requests.Clear(); }
        base.Dispose(disposing);
    }
}

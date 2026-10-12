using StatsDirect.Configuration;
using StatsDirect.UI.WebReports;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed partial class LearningService
{
    private async Task CaptureEvidenceAsync(string id)
    {
        var doc = Documents.FirstOrDefault(d => d.Id() == id);
        string text = doc switch { frmScript script => "R SCRIPT\n" + script.LearningScript + "\nR OUTPUT\n" + script.LearningOutput, frmReportWebView report => await QuestionContext.ReportTextAsync(report), _ => throw new InvalidOperationException("Choose an open report or R session.") };
        if (text.Length > 200_000 || doc.IsDisposed) throw new IOException("The document is too large to attach, or has closed. Export it separately.");
        await View.CallAsync("evidence", new JsonObject { ["title"] = doc.Text, ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["text"] = text });
    }
    private static void ValidateExport(JsonObject body)
    {
        if (body["record"] is not JsonObject || body.S("text").Length > 10_000_000 || body["record"].ToJsonString().Length > 10_000_000) throw new InvalidDataException("The learning record is too large to export.");
    }
    private async Task ExportAsync(JsonObject body)
    {
        ValidateExport(body); string format = body.S("format", "txt");
        if (!new[] { "txt", "json", "html", "pdf", "docx" }.Contains(format)) return;
        using var dialog = new SaveFileDialog { Title = "Export learning record", Filter = format.ToUpperInvariant() + " file|*." + format, FileName = "StatsDirect-learning-record." + format };
        if (dialog.ShowDialog(main) != DialogResult.OK) return;
        await ExportToAsync(body, dialog.FileName, format);
        await View.CallAsync("notice", JsonValue.Create("Learning record exported to " + Path.GetFileName(dialog.FileName)));
    }
    internal async Task ExportToAsync(JsonObject body, string path, string format)
    {
        ValidateExport(body);
        if (format == "json") { LearningJson.Save(path, body["record"]); return; }
        if (format == "txt") { await File.WriteAllTextAsync(path, body.S("text"), new UTF8Encoding(false)); return; }
        if (format is not ("html" or "pdf" or "docx")) throw new ArgumentException("Choose a supported learning-record format.");
        string html = body.S("html");
        if (html.Length == 0 || Encoding.UTF8.GetByteCount(html) > 15_000_000) throw new IOException("The formatted record is empty or too large.");
        // Use the report editor's existing sanitiser and exporters. Its sources and bundle are unchanged.
        using var window = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-16000, -16000), Size = new Size(900, 800) };
        using var report = new ReportView(Path.Combine(SDConfiguration.InstallationDirectory, "ReportEditor"), Path.Combine(Storage, "ExportWebView")) { Dock = DockStyle.Fill };
        window.Controls.Add(report); window.Show();
        await report.Ready.WaitAsync(TimeSpan.FromSeconds(40));
        await report.OpenHtmlAsync(html, "StatsDirect learning record");
        await report.SaveAsync(path, "StatsDirect learning record", format);
    }
    internal string PrepareEmail(JsonObject body)
    {
        ValidateExport(body); string recipient = body.S("recipient");
        if (!CoursePack.Email(recipient)) throw new InvalidDataException("Enter one valid review email address.");
        string folder = Path.Combine(Storage, "Review drafts", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string text = body.S("text"), json = body["record"].ToJsonString();
        File.WriteAllText(Path.Combine(folder, "learning-record.txt"), text, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, "learning-record.json"), json, new UTF8Encoding(false));
        string boundary = "StatsDirect-" + Guid.NewGuid().ToString("N");
        var message = new StringBuilder("X-Unsent: 1\r\nTo: " + recipient + "\r\nSubject: StatsDirect learning record - review request\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=\"" + boundary + "\"\r\n\r\n");
        void Part(string type, string name, string content)
        {
            message.Append("--").Append(boundary).Append("\r\nContent-Type: ").Append(type).Append("; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n");
            if (name != null) message.Append("Content-Disposition: attachment; filename=\"").Append(name).Append("\"\r\n");
            message.Append("\r\n").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(content), Base64FormattingOptions.InsertLineBreaks)).Append("\r\n");
        }
        Part("text/plain", null, "Please review my attached learning record and advise whether it meets your assessment or CPD requirements. TXT and JSON contain the same record. Provisional scores have not been independently verified.");
        Part("text/plain", "learning-record.txt", text); Part("application/json", "learning-record.json", json);
        message.Append("--").Append(boundary).Append("--\r\n");
        string path = Path.Combine(folder, "Review learning record.eml"); File.WriteAllText(path, message.ToString(), Encoding.ASCII); return path;
    }
}

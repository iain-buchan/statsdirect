using StatsDirect.R;
using StatsDirect.UI.WebReports;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class Program
{
    private static JsonObject NotebookRecord(string operation = "TPaired") => new()
    {
        ["version"] = 1, ["operation"] = operation,
        ["parameters"] = new JsonObject { ["gamma"] = 0.95 },
        ["inputs"] = new JsonArray(new JsonObject
        {
            ["name"] = "data", ["kind"] = "frame", ["file"] = "PEFR.xlsx", ["sheet"] = "Paired",
            ["columns"] = new JsonArray(
                new JsonObject { ["title"] = "PEFR Before", ["column"] = 1, ["rows"] = new JsonArray(2, 10) },
                new JsonObject { ["title"] = "PEFR After", ["column"] = 2, ["rows"] = new JsonArray(2, 10) })
        }),
        ["data"] = new JsonObject { ["data"] = new JsonObject
        {
            ["PEFR Before"] = new JsonArray(312, 242, 340, 388, 296, 254, 391, 402, 290),
            ["PEFR After"] = new JsonArray(300, 201, 232, 312, 220, 256, 328, 330, 231)
        } }
    };

    /// <summary>An SVG attachment's text: decoded when it is base64, as the export writes it, else as it is.</summary>
    private static string SvgText(JsonElement svg)
    {
        string value = svg.GetString()!;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); } catch (FormatException) { return value; }
    }

    private static async Task CheckNotebook()
    {
        await view.Ready.WaitAsync(TimeSpan.FromSeconds(45));
        RRecipes.Folder = Path.Combine(AppContext.BaseDirectory, "R");
        string chart = "<svg xmlns='http://www.w3.org/2000/svg' width='120' height='60' viewBox='0 0 120 60'><rect width='120' height='60' fill='#235f89'/><text x='5' y='20' fill='white'>Original β</text></svg>";
        string pixel = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
        JsonObject record = NotebookRecord();
        await view.CallAsync("load", new { entries = new[]
        {
            new { id="one", title="PEFR β", operation="TPaired", helpContextId=1150,
                html="<h2>Original paired result</h2><p>Reviewer’s note: café &amp; β</p><table><tr><th>Estimate</th><th>Value</th></tr><tr><td>Native difference</td><td>56</td></tr></table>" + chart + "<p>After the chart</p>", record=(JsonElement?)JsonSerializer.SerializeToElement(record) },
            new { id="two", title="Manual notes $5 ~ish", operation="", helpContextId=0,
                html="<p>Second result without a record</p><pre>line one\n\nline three</pre><img width='12' height='12' src='"+pixel+"'/><table><tr><td>Chart in table</td><td>"+chart+"</td></tr></table>" +
                    "<ol start='3'><li>First numbered point</li><li>**literal**"+chart+"Tail after list picture</li></ol><p style='color:rgb(160,0,0)'>![literal](https://example.invalid/not-an-image.png)<img src='"+pixel+"'/>Caption</p>", record=(JsonElement?)null }
        } });
        string before = (await Js("document.getElementById('results').innerHTML")).GetString()!;
        long revision = view.Revision;
        JsonElement snapshot = await view.NotebookSnapshotAsync("Statistical review β");
        Check(snapshot.GetProperty("entries").GetArrayLength() == 2, "notebook capture preserves result order, including manual report notes");
        Check(snapshot.GetProperty("reportHtml").GetString()!.Contains("Original paired result"), "notebook capture includes the complete portable original report");
        Check(before == (await Js("document.getElementById('results').innerHTML")).GetString() && view.Revision == revision,
            "notebook capture leaves the live report and its revision unchanged");
        string notebook = ReportNotebook.Write(snapshot, "Statistical review β", _ => throw new Exception("Held data must not open a workbook"), "5.2.8-test");
        string filename = Path.Combine(output, "review.ipynb");
        await File.WriteAllTextAsync(filename, notebook, new UTF8Encoding(false));
        using var parsed = JsonDocument.Parse(notebook);
        JsonElement root = parsed.RootElement, cells = root.GetProperty("cells");
        Check(root.GetProperty("nbformat").GetInt32() == 4 && root.GetProperty("nbformat_minor").GetInt32() == 5,
            "notebook uses standard nbformat 4.5");
        Check(root.GetProperty("metadata").GetProperty("kernelspec").GetProperty("language").GetString() == "R", "notebook declares an R kernel");
        var ids = cells.EnumerateArray().Select(c => c.GetProperty("id").GetString()!).ToArray();
        Check(ids.Distinct().Count() == ids.Length && ids.All(id => id.Length is >= 1 and <= 64 && id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')),
            "notebook cells have unique valid stable-format identifiers");
        var code = cells.EnumerateArray().Where(c => c.GetProperty("cell_type").GetString() == "code").ToArray();
        Check(code.Length == 1 && code.All(c => c.GetProperty("execution_count").ValueKind == JsonValueKind.Null && c.GetProperty("outputs").GetArrayLength() == 0),
            "only actual R replay is a code cell and it is explicitly unexecuted");
        string source = code[0].GetProperty("source").GetString()!;
        Check(source.Contains("312") && source.Contains("before <-") && source.Contains("after <-") && source.Contains("t.test(before, after, paired = TRUE"),
            "notebook embeds the original paired data, settings and executable existing R recipe");
        string markdown = string.Join("\n", cells.EnumerateArray().Where(c => c.GetProperty("cell_type").GetString() == "markdown").Select(c => c.GetProperty("source").GetString()));
        Check(markdown.Contains("Reviewer’s note") && markdown.Contains("<table") && markdown.Contains("Native difference") && markdown.Contains("After the chart") && markdown.Contains("Second result without a record"),
            "notebook retains edited narrative, Unicode, original tables and text after charts");
        var attachments = cells.EnumerateArray().Where(c => c.TryGetProperty("attachments", out _)).SelectMany(c => c.GetProperty("attachments").EnumerateObject()).ToArray();
        Check(attachments.Any(a => a.Value.TryGetProperty("image/svg+xml", out var svg) && SvgText(svg).Contains("Original β") && a.Value.TryGetProperty("image/png", out _)),
            "notebook chart attachments retain SVG vectors and PNG fallback together");
        foreach (var a in attachments)
            if (a.Value.TryGetProperty("image/png", out var png))
            {
                byte[] bytes = Convert.FromBase64String(png.GetString()!);
                Check(bytes.Length > 8 && bytes[0] == 0x89 && Encoding.ASCII.GetString(bytes, 1, 3) == "PNG", "each notebook PNG fallback contains real PNG bytes");
            }
        // Jupyter renderers read an attachment as base64, and JupyterLab takes the bundle's first key and renders rasters only: so the PNG
        // comes first and the SVG is the base64 of its text, which nbconvert prefers and decodes
        foreach (var a in attachments)
            if (a.Value.TryGetProperty("image/svg+xml", out var vector))
            {
                string decoded; try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(vector.GetString()!)); } catch (FormatException) { decoded = ""; }
                Check(decoded.TrimStart().StartsWith("<svg", StringComparison.Ordinal), "each notebook SVG attachment is the base64 of its SVG text, as Jupyter renderers read it");
                Check(a.Value.EnumerateObject().First().Name == "image/png", "each notebook chart attachment lists its PNG first, which JupyterLab renders");
            }
        Check(markdown.Contains("Chart in table") && markdown.Contains("<img"), "pictures nested in report tables retain their table layout");
        Check(markdown.Contains("<ol") && markdown.Contains("start=\"3\"") && markdown.Contains("<li") && markdown.Contains("**literal**") && markdown.Contains("Tail after list picture"),
            "pictures inside numbered lists preserve numbering, surrounding text and literal punctuation");
        var literal = cells.EnumerateArray().Where(c => c.GetProperty("cell_type").GetString() == "markdown" && c.GetProperty("source").GetString()!.Contains("![literal]")).ToArray();
        Check(literal.Length > 0 && literal.All(c => c.GetProperty("source").GetString()!.StartsWith("<div>\n")),
            "report text beside images remains a raw HTML block instead of becoming Markdown links or formatting");
        // A Markdown renderer ends a raw HTML block at a blank line: no report cell has one, and a preformatted block's blank line is
        // written as a character reference, which reads back as the newline itself
        var reportCells = cells.EnumerateArray().Where(c => c.GetProperty("metadata").GetProperty("statsdirect").GetProperty("role").GetString() == "original StatsDirect report").ToArray();
        Check(reportCells.Length > 0 && reportCells.All(c => c.GetProperty("source").GetString()!.Split('\n').All(line => line.Trim().Length > 0)) && markdown.Contains("line one&#10;\nline three</pre>"),
            "no report cell carries a blank line, and a preformatted block's blank line is written as a character reference");
        Check(markdown.Contains("**Manual notes \\$5 \\~ish:**"), "dollar signs and tildes in titles are escaped, so a renderer neither typesets them as mathematics nor strikes through");
        Check(notebook.Contains("PEFR.xlsx") && notebook.Contains("5.2.8-test"), "notebook records source provenance and the actual exporting application version");
        CheckNotebookDataValidation(snapshot);
        CheckNotebookMoreData(snapshot);
        var version = RController.PreferredRVersion();
        if (version == null) throw new Exception("R must be available to verify the exported replay");
        string script = Path.Combine(output, "notebook-replay.R");
        await File.WriteAllTextAsync(script, "pdf('" + Path.Combine(output, "notebook-replay.pdf").Replace("\\", "/") + "')\n" + source + "\ndev.off()\n");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(version.BinPath, "Rscript.exe"), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "--vanilla", script }
        })!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
        string printed = await stdout, errors = await stderr;
        Check(process.ExitCode == 0 && printed.Contains("Paired t-test") && !errors.Contains("Error"), "the exported R code runs independently in R on its embedded data");
        await File.WriteAllTextAsync(Path.Combine(output, "notebook-replay-output.txt"), printed);
        // An edit while image preparation is pending must not pair old report HTML with newer metadata.
        await Js("window.notebookExportOriginal=StatsDirectReportExport;window.StatsDirectReportExport={...notebookExportOriginal,notebook:async options=>{const result=await notebookExportOriginal.notebook(options);await new Promise(resolve=>window.notebookExportRelease=resolve);return result;}};");
        try
        {
            Task<JsonElement> pending = view.NotebookSnapshotAsync("Concurrent edit");
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!await True("typeof window.notebookExportRelease==='function'") && DateTime.UtcNow < until) await Task.Delay(10);
            if (!await True("typeof window.notebookExportRelease==='function'")) throw new Exception("Notebook capture did not reach the image-preparation boundary");
            await view.CallAsync("extend", new { id = snapshot.GetProperty("entries")[0].GetProperty("id").GetString(), html = "<p>New note during notebook export</p>" });
            await Js("notebookExportRelease();");
            bool refused = false;
            try { await pending; } catch (InvalidOperationException ex) { refused = ex.Message.Contains("report changed", StringComparison.OrdinalIgnoreCase); }
            Check(refused, "editing during notebook preparation refuses an inconsistent snapshot and asks for a retry");
        }
        finally { await Js("window.notebookExportRelease?.();window.StatsDirectReportExport=notebookExportOriginal;delete window.notebookExportOriginal;delete window.notebookExportRelease;"); }
    }

    private static void CheckNotebookDataValidation(JsonElement snapshot)
    {
        JsonObject Copy() => JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        string Write(JsonObject document, Func<FramePointer, FrameValues>? reader = null) => ReportNotebook.Write(JsonSerializer.SerializeToElement(document), "Validation", reader!, "test");
        void Refuses(JsonObject document, Func<FramePointer, FrameValues>? reader, string label)
        {
            bool refused = false;
            try { Write(document, reader); } catch (InvalidOperationException) { refused = true; }
            Check(refused, label);
        }
        JsonObject pointerOnly = Copy();
        pointerOnly["entries"]![0]!["record"]!.AsObject().Remove("data");
        Refuses(pointerOnly, _ => new FrameValues { Unavailable = "original workbook absent" }, "missing workbook data is refused instead of producing an incomplete handover");
        FrameValues Data(bool changed = false)
        {
            var values = new FrameValues();
            values.Columns.Add(new ColumnValues { Title = "PEFR Before", Values = new object[] { 312d, 242d, 340d, 388d, 296d, 254d, 391d, 402d, 290d }, Changed = changed });
            values.Columns.Add(new ColumnValues { Title = "PEFR After", Values = new object[] { 300d, 201d, 232d, 312d, 220d, 256d, 328d, 330d, 231d } });
            return values;
        }
        Refuses(pointerOnly, _ => Data(true), "changed workbook values cannot silently replace the data of saved results");
        int reads = 0;
        string current = Write(pointerOnly, _ => { reads++; return Data(); });
        Check(reads == 1 && current.Contains("312"), "pointer data is read once and shared between complete metadata and R replay");
        JsonObject omitted = Copy();
        omitted["entries"]![0]!["record"]!["inputs"] = new JsonArray(new JsonObject { ["name"] = "data2d", ["kind"] = "frame2d", ["value"] = new JsonObject { ["omitted"] = true } });
        Refuses(omitted, null, "omitted nested data is detected even though the existing R writer has no unavailable flag for it");
        JsonObject typed = Copy();
        typed["entries"]![0]!["record"]!["operation"] = "NoRecipeForThisOperation";
        typed["entries"]![0]!["record"]!["inputs"] = new JsonArray(new JsonObject
        {
            ["name"] = "extra", ["kind"] = "entered", ["columns"] = new JsonArray(
                new JsonObject { ["title"] = "Identifier", ["values"] = new JsonArray("0012", "A β", null) },
                new JsonObject { ["title"] = "Flag", ["values"] = new JsonArray(true, false, null) })
        });
        string typedNotebook = Write(typed);
        Check(typedNotebook.Contains("0012") && typedNotebook.Contains("true") && typedNotebook.Contains("false"), "entered text, flags and missing values remain typed in the notebook data");
    }
}

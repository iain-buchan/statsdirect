#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using StatsDirect.R;

namespace StatsDirect.UI;

internal sealed partial class RConsoleView
{
    private ContextMenuStrip? chartContextMenu;
    private string? contextPlotId;
    private long contextPlotRevision;
    private readonly Dictionary<string, RChartWindow> chartWindows = new(StringComparer.Ordinal);

    private bool IsChartAssetPath(string path)
    {
        foreach (var saved in plots.Values)
            foreach (string field in new[] { "path", "previewPath" })
                if (saved.TryGetProperty(field, out var asset) && string.Equals(path, Path.GetFullPath(asset.GetString()!), StringComparison.OrdinalIgnoreCase)) return true;
        return chartWindows.Values.Any(window => string.Equals(path, window.AssetPath, StringComparison.OrdinalIgnoreCase));
    }

    internal static string SaveableChartSvg(string svg)
    {
        var vector = RResultsParser.VectorPicture(ScriptOutputView.Sanitised(svg));
        string flattened = ScriptOutputView.FlattenedGlyphs(vector.Svg);
        if (Encoding.UTF8.GetByteCount(flattened) > 20_000_000) flattened = vector.Svg;
        return ScriptOutputView.PlainSized(flattened, vector.Width, vector.Height);
    }

    private void CloseChartWindows()
    {
        var windows = chartWindows.Values.ToArray();
        chartWindows.Clear();
        foreach (var window in windows) window.Dispose();
    }

    private void ShowChartWindow(string id, long revision, string path, string mime, string title)
    {
        string key = id + ":" + revision;
        if (chartWindows.TryGetValue(key, out var existing) && !existing.IsDisposed)
        {
            if (existing.WindowState == FormWindowState.Minimized) existing.WindowState = FormWindowState.Normal;
            existing.Show(); existing.Activate();
            return;
        }
        var window = new RChartWindow(path, mime, title, message => { if (!closing) ReportNotice(message); })
        {
            IsProtectedSavePath = IsChartAssetPath
        };
        chartWindows[key] = window;
        window.FormClosed += (_, _) =>
        {
            if (chartWindows.TryGetValue(key, out var current) && ReferenceEquals(current, window)) chartWindows.Remove(key);
        };
        var owner = FindForm();
        window.Show(owner?.MdiParent ?? owner);
    }

    internal void ShowChartContextMenu(string id, long revision, Point location)
    {
        if (closing || IsDisposed) return;
        var plot = PlotMetadata(id, revision);
        contextPlotId = id;
        contextPlotRevision = revision;
        if (chartContextMenu == null)
        {
            // WinForms dispatches an item's Click after closing its drop-down and
            // then finishes activation bookkeeping. Keep this menu owned by the
            // view: disposing it in Closed can throw from ModalMenuFilter later.
            chartContextMenu = new ContextMenuStrip();
            ToolStripMenuItem Item(string label, Func<string, long?, Task> action) => new(label, null, async (_, _) =>
            {
                string? selectedId = contextPlotId;
                long selectedRevision = contextPlotRevision;
                if (selectedId == null || closing) return;
                try { await action(selectedId, selectedRevision); }
                catch (Exception error) { if (!closing) ReportNotice(error.Message); }
            });
            chartContextMenu.Items.Add(Item("&Copy", CopyPlotAsync));
            chartContextMenu.Items.Add(Item("Save chart as &SVG…", (selectedId, selectedRevision) => SavePlotWithDialogAsync(selectedId, selectedRevision, "svg")));
            chartContextMenu.Items.Add(Item("Save chart as &PNG…", (selectedId, selectedRevision) => SavePlotWithDialogAsync(selectedId, selectedRevision, "png")));
        }
        chartContextMenu.Items[1].Enabled = plot.GetProperty("mime").GetString() == "image/svg+xml";
        chartContextMenu.Show(this, location);
    }

    private async Task SavePlotWithDialogAsync(string id, long? revision, string format)
    {
        var plot = PlotMetadata(id, revision);
        string label = plot.TryGetProperty("label", out var value) ? value.GetString() ?? "chart" : "chart";
        string name = Path.GetFileNameWithoutExtension(label);
        name = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (string.IsNullOrWhiteSpace(name)) name = "chart";
        using var dialog = new SaveFileDialog
        {
            Title = format == "png" ? "Save chart as PNG" : "Save chart as SVG",
            Filter = format == "png" ? "PNG picture|*.png|All files|*.*" : "SVG picture|*.svg|All files|*.*",
            DefaultExt = format,
            FileName = name + "." + format,
            AddExtension = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await SavePlotAsync(id, revision, format, dialog.FileName);
    }

    /// <summary>Exports the selected immutable chart revision, independently of its original R filename.</summary>
    internal async Task SavePlotAsync(string id, long? revision, string format, string path)
    {
        if (format is not ("svg" or "png")) throw new ArgumentException("Choose SVG or PNG.", nameof(format));
        string originalSession = sessionId;
        long originalGeneration = generation;
        var plot = FindPlot(id, revision);
        string mime = plot.GetProperty("mime").GetString()!;
        if (mime is not ("image/svg+xml" or "image/png") || (format == "svg" && mime != "image/svg+xml"))
            throw new InvalidOperationException("This chart is not a vector SVG. Save it as PNG instead.");
        path = Path.GetFullPath(path);
        if (IsChartAssetPath(path)) throw new InvalidOperationException("Choose a different filename to preserve the R chart's saved revision.");

        string source = plot.TryGetProperty("previewPath", out var previewPath) ? previewPath.GetString()! : plot.GetProperty("path").GetString()!;
        source = Path.GetFullPath(source);
        string relative = Path.GetRelativePath(Path.GetFullPath(WorkingDirectory), source);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(source))
            throw new InvalidOperationException("That chart is no longer available in this R session's output folder.");
        byte[] bytes;
        if (format == "svg")
        {
            // Use the same positioned SVG glyphs and plain dimensions as Copy,
            // so Office can import the saved vector without displacing labels.
            bytes = Encoding.UTF8.GetBytes(SaveableChartSvg(await File.ReadAllTextAsync(source)));
        }
        else if (mime == "image/png") bytes = await File.ReadAllBytesAsync(source);
        else
        {
            await Ready;
            string uri = "data:image/svg+xml;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(source));
            string expression = $$"""
                (async () => {
                  const image = new Image(); image.src = {{JsonSerializer.Serialize(uri)}}; await image.decode();
                  const scale = Math.min(2, 4096 / Math.max(image.naturalWidth, image.naturalHeight));
                  const canvas = document.createElement('canvas');
                  canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
                  canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
                  const context = canvas.getContext('2d', {willReadFrequently:true});
                  context.fillStyle = 'white'; context.fillRect(0, 0, canvas.width, canvas.height);
                  context.drawImage(image, 0, 0, canvas.width, canvas.height);
                  return canvas.toDataURL('image/png');
                })()
                """;
            string result = await Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate",
                JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true })).WaitAsync(TimeSpan.FromSeconds(15));
            using var evaluation = JsonDocument.Parse(result);
            if (evaluation.RootElement.TryGetProperty("exceptionDetails", out _)) throw new InvalidOperationException("The chart could not be drawn as PNG.");
            string? png = evaluation.RootElement.GetProperty("result").GetProperty("value").GetString();
            const string prefix = "data:image/png;base64,";
            if (png?.StartsWith(prefix, StringComparison.Ordinal) != true) throw new InvalidOperationException("The chart gave no PNG picture.");
            bytes = Convert.FromBase64String(png[prefix.Length..]);
        }
        if (closing || IsDisposed || originalSession != sessionId || originalGeneration != generation)
            throw new InvalidOperationException("The R session changed before the chart could be saved.");
        _ = PlotMetadata(id, revision);
        await File.WriteAllBytesAsync(path, bytes);
        if (!closing) ReportNotice("Chart saved as " + path);
    }
}

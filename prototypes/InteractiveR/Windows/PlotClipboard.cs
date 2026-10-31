using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StatsDirect.InteractiveR;

internal sealed partial class MainForm
{
    private JsonElement FindPlot(string id, long? revision) => (revision.HasValue ? plots.TryGetValue(id + ":" + revision.Value, out var plot) : latestPlots.TryGetValue(id, out plot))
        ? plot : throw new InvalidOperationException("That R plot is no longer available.");

    private void OpenPlot(string id, long? revision)
    {
        var plot = FindPlot(id, revision);
        if (!plot.TryGetProperty("path", out var pathValue)) throw new InvalidOperationException("This plot has no saved vector file.");
        string path = Path.GetFullPath(pathValue.GetString()!);
        string relative = Path.GetRelativePath(options.WorkingDirectory, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path))
            throw new InvalidOperationException("The plot is outside this prototype session's output folder.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async Task CopyPlotAsync(string id, long? revision)
    {
        var plot = FindPlot(id, revision);
        string source = plot.TryGetProperty("preview", out var preview) ? preview.GetString()! : plot.GetProperty("uri").GetString()!;
        const string svgPrefix = "data:image/svg+xml;base64,";
        if (!source.StartsWith(svgPrefix, StringComparison.Ordinal) || source.Length > 27_000_000)
            throw new InvalidOperationException("Choose an embedded SVG plot smaller than 20 MB.");
        // Use the image already drawn in the page, with no file or network access.
        string result = await browser.CoreWebView2.ExecuteScriptAsync($$"""
            (() => {
              const image = [...document.images].find(i => i.getAttribute('src') === {{JsonSerializer.Serialize(source)}});
              if (!image?.complete || !image.naturalWidth || !image.naturalHeight) return null;
              const canvas = document.createElement('canvas');
              const scale = Math.min(2, 4096 / Math.max(image.naturalWidth, image.naturalHeight));
              canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
              canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
              const context = canvas.getContext('2d'); context.fillStyle = 'white';
              context.fillRect(0, 0, canvas.width, canvas.height); context.drawImage(image, 0, 0, canvas.width, canvas.height);
              return canvas.toDataURL('image/png');
            })()
            """);
        string? png = JsonSerializer.Deserialize<string>(result);
        if (png is null || !png.StartsWith("data:image/png;base64,", StringComparison.Ordinal) || png.Length > 27_000_000)
            throw new InvalidOperationException("The plot is not yet ready to copy.");
        byte[] svgBytes = Convert.FromBase64String(source[svgPrefix.Length..]);
        byte[] pngBytes = Convert.FromBase64String(png["data:image/png;base64,".Length..]);
        if (svgBytes.Length > 20_000_000 || pngBytes.Length > 20_000_000) throw new InvalidOperationException("The plot is too large to copy.");
        using var svgStream = new MemoryStream(svgBytes, writable: false);
        using var pngStream = new MemoryStream(pngBytes, writable: false);
        using var bitmap = new Bitmap(pngStream);
        var data = new DataObject();
        data.SetData("StatsDirect.ReportFragment.HTML.v1", false, Encoding.UTF8.GetString(svgBytes));
        data.SetData("image/svg+xml", false, svgStream);
        data.SetData("PNG", false, pngStream);
        data.SetData(DataFormats.Bitmap, true, bitmap);
        data.SetData(DataFormats.UnicodeText, plot.TryGetProperty("label", out var label) ? label.GetString() ?? "R plot" : "R plot");
        Clipboard.SetDataObject(data, true, 5, 100);
        ReportNotice("Plot copied as SVG, with PNG and bitmap alternatives.");
    }
}

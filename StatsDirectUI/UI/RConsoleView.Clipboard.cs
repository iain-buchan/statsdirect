using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using StatsDirect.R;
using StatsDirect.UI.WebReports;

namespace StatsDirect.UI;

internal sealed partial class RConsoleView
{
    /// <summary>Copies the exact displayed revision using the report's Windows SVG and PNG publication.</summary>
    internal async Task CopyPlotAsync(string id, long? revision)
    {
        var plot = FindPlot(id, revision);
        if (plot.TryGetProperty("mime", out var mime) && mime.GetString() == "application/pdf")
        {
            ReportNotice("Use Open chart to open or save this vector PDF.");
            return;
        }
        string originalSession = sessionId;
        long originalGeneration = generation;
        if (!plot.TryGetProperty("path", out var pathValue)) throw new InvalidOperationException("This chart has no saved vector file.");
        string path = Path.GetFullPath(pathValue.GetString());
        string relative = Path.GetRelativePath(WorkingDirectory, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("This chart is outside this R session's output folder.");
        string source = plot.TryGetProperty("preview", out var preview) ? preview.GetString() : plot.GetProperty("uri").GetString();
        const string svgPrefix = "data:image/svg+xml;base64,";
        const string pngPrefix = "data:image/png;base64,";
        bool raster = source?.StartsWith(pngPrefix, StringComparison.Ordinal) == true;
        if ((!raster && source?.StartsWith(svgPrefix, StringComparison.OrdinalIgnoreCase) != true) || source.Length > 27_000_000)
            throw new InvalidDataException("Choose an embedded SVG or PNG chart smaller than 20 MB.");
        await Ready;
        if (IsDisposed || Browser.IsDisposed || originalSession != sessionId || originalGeneration != generation)
            throw new InvalidOperationException("The R session changed before the chart could be copied.");
        string result = await Browser.CoreWebView2.ExecuteScriptAsync($$"""
            (() => {
              const image = [...document.images].find(i => i.closest('figure.plot') && i.getAttribute('src') === {{JsonSerializer.Serialize(source)}});
              if (!image?.complete || !image.naturalWidth || !image.naturalHeight) return null;
              if (image.getAttribute('src').startsWith('data:image/png;base64,'))
                return {png: image.getAttribute('src'), alt: image.alt, width: image.naturalWidth, height: image.naturalHeight};
              let png = null;
              try {
                const canvas = document.createElement('canvas');
                const scale = Math.min(2, 4096 / Math.max(image.naturalWidth, image.naturalHeight));
                canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
                canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
                const context = canvas.getContext('2d'); context.fillStyle = 'white';
                context.fillRect(0, 0, canvas.width, canvas.height); context.drawImage(image, 0, 0, canvas.width, canvas.height);
                png = canvas.toDataURL('image/png');
              } catch {} // SVG remains useful if the optional raster cannot be prepared.
              return {png, alt: image.alt, width: image.naturalWidth, height: image.naturalHeight};
            })()
            """);
        if (IsDisposed || Browser.IsDisposed || originalSession != sessionId || originalGeneration != generation)
            throw new InvalidOperationException("The R session changed before the chart could be copied.");
        using var image = JsonDocument.Parse(result);
        if (image.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("The chart is no longer available to copy.");
        // FindPlot also rejects an obsolete context action after the host has cleared its assets.
        _ = FindPlot(id, revision);
        byte[] original = Convert.FromBase64String(source[(raster ? pngPrefix.Length : svgPrefix.Length)..]);
        if (original.Length > 20_000_000) throw new InvalidDataException("The chart is too large to copy.");
        string alt = image.RootElement.GetProperty("alt").GetString() ?? "R chart";
        if (raster)
        {
            string html = ClipboardImageHtml(source, image.RootElement.GetProperty("width").GetInt32(), image.RootElement.GetProperty("height").GetInt32(), alt);
            var rasterData = new DataObject();
            rasterData.SetData(ReportClipboard.FragmentFormat, false, html);
            rasterData.SetData(DataFormats.Html, ReportClipboard.EncodeHtml(html));
            rasterData.SetData(DataFormats.UnicodeText, alt);
            ReportClipboard.Publish(rasterData, png: source);
            ReportNotice("Chart copied as PNG, with a bitmap alternative.");
            return;
        }
        string png = image.RootElement.GetProperty("png").GetString();
        PublishVectorChart(Encoding.UTF8.GetString(original), png, alt);
        ReportNotice("Chart copied as SVG, with PNG and bitmap alternatives.");
    }

    internal static void PublishVectorChart(string svg, string png, string alt)
    {
        var vector = RResultsParser.VectorPicture(ScriptOutputView.Sanitised(svg));
        if (png?.StartsWith("data:image/png;base64,", StringComparison.Ordinal) != true || png.Length > 27_000_000) png = null;
        string flattened = ScriptOutputView.FlattenedGlyphs(vector.Svg);
        if (Encoding.UTF8.GetByteCount(flattened) > 20_000_000) flattened = vector.Svg;
        string plain = ScriptOutputView.PlainSized(flattened, vector.Width, vector.Height);
        var data = new DataObject();
        data.SetData(ReportClipboard.FragmentFormat, false, flattened);
        if (png != null)
            data.SetData(DataFormats.Html, ReportClipboard.EncodeHtml(ClipboardImageHtml(png, vector.Width, vector.Height, alt)));
        data.SetData(DataFormats.UnicodeText, alt);
        ReportClipboard.Publish(data, plain, png);
    }

    internal static string ClipboardImageHtml(string png, int naturalWidth, int naturalHeight, string alt)
    {
        int width = Math.Min(640, naturalWidth);
        int height = (int)Math.Round((double)naturalHeight * width / Math.Max(1, naturalWidth));
        return "<img width=\"" + (int)Math.Round(width * .75) + "\" height=\"" + (int)Math.Round(height * .75)
            + "\" alt=\"" + WebUtility.HtmlEncode(alt) + "\" style=\"width:" + width + "px;height:" + height + "px\" src=\"" + png + "\">";
    }
}

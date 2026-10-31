#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StatsDirect.R.Interactive;

/// <summary>The files a command leaves in the R folder, found by comparing the folder before and after it, for the console and the Rscript
/// runner alike: a chart file the user named becomes a card with an immutable copy, any other file a notice with a link.  Each session emits
/// the events returned here through its own Emit, so they carry that session's envelope.</summary>
internal static class ROutputFiles
{
    internal readonly record struct FileStamp(long Length, long Written);

    /// <summary>An event for a session to emit as its own: the type and the fields before the envelope is added.</summary>
    internal readonly record struct OutputEvent(string Type, Dictionary<string, object?> Fields);

    // Discover named file-device charts separately from the console's active-device snapshots.
    // Auto-device files can still be open/incomplete, and belong only to the snapshot mechanism.
    // The old runner's fixed names too: a template's R analysis finishing meanwhile is not this command's output
    internal static bool InternalOutput(string name) => Regex.IsMatch(name,
        @"^(?:console-(?:plots-)?[0-9a-f]{32}-|Rplot-[0-9a-f]{32}-|(?:continue|script)-[0-9a-f]{32}\.r$|(?:script|open-in-r)\.r$|(?:results|error)\.txt$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The folder's files by full path, with their size and time of writing, leaving out the sessions' own files.</summary>
    internal static Dictionary<string, FileStamp> Snapshot(string workingDirectory)
    {
        var files = new Dictionary<string, FileStamp>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in new DirectoryInfo(workingDirectory).GetFiles())
                if (!InternalOutput(file.Name)) files[file.FullName] = new(file.Length, file.LastWriteTimeUtc.Ticks);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return files;
    }

    /// <summary>The events for the files written or changed since <paramref name="before"/>, in the order the folder lists them: plot cards for
    /// svg, png and pdf files and for jpeg, gif, bmp and tiff files decoded to a PNG preview, each with an immutable copy named
    /// console-&lt;sessionId&gt;-&lt;generation&gt;-file-&lt;id&gt;&lt;ext&gt; so that a later overwrite never changes the card; notices for other files.
    /// <paramref name="nextFilePlot"/> supplies each copy's id; <paramref name="live"/> says whether the session still wants this command's
    /// files, so that no copies are made for a session that has moved on or closed.</summary>
    internal static async Task<List<OutputEvent>> ChangedAsync(string workingDirectory, IReadOnlyDictionary<string, FileStamp> before, string sessionId, long generation, Func<long> nextFilePlot, Func<bool> live)
    {
        var events = new List<OutputEvent>();
        foreach (var (path, stamp) in Snapshot(workingDirectory))
        {
            if (before.TryGetValue(path, out var earlier) && earlier == stamp) continue;
            if (!live()) return events;
            string name = Path.GetFileName(path), extension = Path.GetExtension(path).ToLowerInvariant();
            bool decodeRaster = extension is ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".tif" or ".tiff";
            string? mime = extension switch { ".svg" => "image/svg+xml", ".png" => "image/png", ".pdf" => "application/pdf", _ => decodeRaster ? "image/png" : null };
            // Cairo file devices commonly leave an empty file until dev.off flushes it.
            // Wait for that completed file rather than appending a blank preview now.
            if (mime != null && stamp.Length == 0) continue;
            if (mime == null)
            {
                events.Add(new("notice", new() { ["text"] = "File written: " + name, ["filePath"] = path, ["label"] = name }));
                continue;
            }
            try
            {
                // The immutable asset, rather than the user's overwriteable filename,
                // is the exact revision later opened/copied from this card.
                long id = nextFilePlot();
                string snapshot = Path.Combine(workingDirectory, $"console-{sessionId}-{generation}-file-{id}{extension}");
                File.Copy(path, snapshot, overwrite: false);
                string? preview = null, previewPath = null;
                if (mime != "application/pdf" && stamp.Length <= 20 * 1024 * 1024)
                {
                    byte[] bytes;
                    if (decodeRaster)
                    {
                        // Match the previous output pane: decode the original pixels,
                        // retain its original file and publish PNG for browser/clipboard use.
                        bytes = await Task.Run(() =>
                        {
                            using var image = System.Drawing.Image.FromFile(snapshot);
                            using var png = new MemoryStream();
                            image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                            return png.ToArray();
                        });
                        previewPath = Path.Combine(workingDirectory, $"console-{sessionId}-{generation}-file-{id}-preview.png");
                        await File.WriteAllBytesAsync(previewPath, bytes);
                    }
                    else bytes = await File.ReadAllBytesAsync(snapshot);
                    if (bytes.Length <= 20 * 1024 * 1024)
                        preview = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
                }
                var fields = new Dictionary<string, object?>
                {
                    ["plotId"] = $"{sessionId}-{generation}-file-{id}", ["revision"] = 1,
                    ["mime"] = mime, ["preview"] = preview, ["path"] = snapshot,
                    ["uri"] = new Uri(snapshot).AbsoluteUri, ["originalPath"] = path, ["label"] = name
                };
                if (previewPath != null) fields["previewPath"] = previewPath;
                events.Add(new("plot", fields));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException or OutOfMemoryException)
            {
                // GDI+ also reports malformed/unsupported image data as OutOfMemoryException.
                events.Add(new("notice", new() { ["text"] = "File written: " + name + ". Its preview could not be read: " + error.Message, ["filePath"] = path, ["label"] = name }));
            }
        }
        return events;
    }
}

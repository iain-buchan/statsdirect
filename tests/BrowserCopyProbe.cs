using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;

// Exercise Chromium's real copy command. Materialise the existing clipboard before
// copying and restore it afterwards, unless another application changed it meanwhile.
internal static class BrowserCopyProbe
{
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    internal static async Task<string> CopyAsync(CoreWebView2 browser)
    {
        var original = Clipboard.GetDataObject();
        var saved = new DataObject();
        if (original != null)
            foreach (string format in original.GetFormats(false))
            {
                // A bitmap from another process comes only through the converting read; it goes back as the one Bitmap format.
                if (format == DataFormats.Bitmap || format == typeof(System.Drawing.Bitmap).FullName)
                {
                    if (saved.GetDataPresent(DataFormats.Bitmap)) continue;
                    System.Drawing.Image kept = (original.GetData(DataFormats.Bitmap) as System.Drawing.Image ?? Clipboard.GetImage())?.Clone() as System.Drawing.Image
                        ?? throw new InvalidOperationException("Cannot preserve clipboard format: " + format);
                    saved.SetData(DataFormats.Bitmap, true, kept);
                    continue;
                }
                object value = original.GetData(format, false);
                if (value == null) throw new InvalidOperationException("Cannot preserve clipboard format: " + format);
                if (value is MemoryStream stream) value = new MemoryStream(stream.ToArray());
                if (value is System.Drawing.Image image) value = image.Clone();
                saved.SetData(format, false, value);
            }
        uint copied = 0;
        try
        {
            uint before=GetClipboardSequenceNumber();
            var reply = await browser.CallDevToolsProtocolMethodAsync("Runtime.evaluate", "{\"expression\":\"document.execCommand('copy')\",\"userGesture\":true,\"returnByValue\":true}");
            await Task.Delay(75);
            copied = GetClipboardSequenceNumber();
            using var json = System.Text.Json.JsonDocument.Parse(reply);
            if (!json.RootElement.GetProperty("result").TryGetProperty("value", out var result) || result.ValueKind != System.Text.Json.JsonValueKind.True)
                throw new InvalidOperationException("The browser copy command failed.");
            if(copied==before)throw new InvalidOperationException("The browser copy command did not update the clipboard.");
            return Clipboard.GetText(TextDataFormat.UnicodeText).Replace("\r\n", "\n");
        }
        finally
        {
            if (copied != 0 && GetClipboardSequenceNumber() == copied)
            {
                // Let WebView2 process clipboard-release messages between attempts;
                // WinForms' synchronous retry sleeps block that same STA message loop.
                for (int attempt=0;;attempt++)
                {
                    try
                    {
                        if (original == null) Clipboard.Clear();
                        else Clipboard.SetDataObject(saved, true, 0, 0);
                        break;
                    }
                    catch (ExternalException) when (attempt<40) { await Task.Delay(50); }
                }
            }
        }
    }
}

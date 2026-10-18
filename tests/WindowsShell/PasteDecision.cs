using System.Reflection;
using StatsDirect.UI;

internal static partial class Program
{
    // Pasting into a report: an HTML fragment that is nothing but pictures pointing at files or web addresses, as Word, PowerPoint and
    // browsers copy a picture, yields to the PNG or bitmap beside it on the clipboard; text, and pictures embedded as data, keep the HTML.
    static void CheckPasteDecision()
    {
        var clipboard = typeof(frmMain).Assembly.GetType("StatsDirect.UI.WebReports.ReportClipboard")!;
        var decide = clipboard.GetMethod("OnlyExternalPictures", BindingFlags.NonPublic | BindingFlags.Static)!;
        bool Only(string html) => (bool)decide.Invoke(null, new object[] { html })!;
        Check(Only("<html><body><!--StartFragment--><img width=320 height=200 src=\"file:///C:/Users/x/AppData/Local/Temp/msohtmlclip1/01/clip_image001.png\" v:shapes=\"Picture_x0020_1\"><!--EndFragment--></body></html>"),
              "a Word picture copy, an img with a file address and no text, yields to the picture formats");
        Check(Only("<img src=\"https://example.org/chart.png\" alt=\"chart\">"), "a browser's Copy image, an img with a web address, yields to the picture formats");
        Check(!Only("<p>Mean difference</p><img src=\"https://example.org/chart.png\">"), "a fragment with text beside a picture keeps its HTML");
        Check(!Only("<img src=\"data:image/png;base64,iVBORw0KGgo=\">"), "a picture embedded as data keeps its HTML");
        Check(!Only("<p>Only text</p>") && !Only("") && !Only(null), "text alone, an empty fragment and no fragment keep the HTML path");
    }
}

using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using System.Drawing.Imaging;
using System.Reflection;

internal static partial class Program
{
    private static async Task CheckRetirement()
    {
        var parameters = new ParameterBag();
        parameters.AddOutput("warning", "Expected frequency < 5\r\nTEST MAY NOT BE RELIABLE");
        string html = new CreoleHtmlReportRenderer().Render(null, "<report><p><warn>@warning</warn></p></report>", parameters);
        Check(html.Contains("Expected frequency &lt; 5<br />TEST MAY NOT BE RELIABLE") && !html.Contains(@"\par"), "multiline warnings use encoded HTML and real line breaks, with no RTF controls");
        using var bitmap = new Bitmap(32, 16);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Blue);
        string picturePath = Path.Combine(output, "r-chart.png");
        bitmap.Save(picturePath, ImageFormat.Png);
        // Exercise the actual R parser's chart conversion without requiring an
        // installed R interpreter or allowing it to start an interactive script.
        var parserType = typeof(CreoleHtmlReportRenderer).Assembly.GetType("StatsDirect.R.RResultsParser");
        var parser = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(parserType);
        var picture = (ReportPicture)parserType.GetMethod("PathToChart", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(parser, new object[] { picturePath });
        // The scripts of the program draw with R's svg device: the file becomes a vector picture, inline in the report
        string vectorPath = Path.Combine(output, "r-chart.svg");
        File.WriteAllText(vectorPath, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"432pt\" height=\"288pt\" viewBox=\"0 0 432 288\"><rect width=\"432\" height=\"288\" fill=\"white\"/></svg>\n");
        var vectorPicture = (ReportVectorPicture)parserType.GetMethod("PathToChart", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(parser, new object[] { vectorPath });
        parameters.AddOutput("vector", vectorPicture);
        string vectorHtml = new CreoleHtmlReportRenderer().Render(null, "<report>@vector</report>", parameters);
        Check(vectorHtml.Contains("<svg xmlns=\"http://www.w3.org/2000/svg\"") && !vectorHtml.Contains("<?xml") && !vectorHtml.Contains("data:image") && vectorPicture.Width == 576 && vectorPicture.Height == 384, "R chart files written by the svg device become inline vector pictures sized from their points");
        parameters.AddOutput("chart", picture);
        html = new CreoleHtmlReportRenderer().Render(null, "<report>@chart</report>", parameters);
        Check(html.Contains("data:image/png;base64,") && !html.Contains(@"\pict") && picture.Width == 32 && picture.Height == 16, "R chart files become embedded PNG pictures without RTF or an EMF report dependency");
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync(html, "R chart", "", 0);
        // ExecuteScriptAsync does not await a JavaScript Promise. The DevTools
        // evaluator explicitly waits for image decoding before reading its size.
        string decoded = await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate",
            System.Text.Json.JsonSerializer.Serialize(new { expression = "(async()=>{const i=document.querySelector('.report-body img');await i.decode();return i.naturalWidth===32&&i.naturalHeight===16;})()", awaitPromise = true, returnByValue = true }));
        using var result = System.Text.Json.JsonDocument.Parse(decoded);
        Check(result.RootElement.GetProperty("result").GetProperty("value").GetBoolean(), "an R chart displays from its embedded data in the report");
        await view.CallAsync("preview");
        Check(await True("document.body.classList.contains('chart-preview') && getComputedStyle(document.getElementById('report-toolbar')).display==='none' && !document.querySelector('.report-body').isContentEditable"), "chart preview is read-only and hides report-editing controls");
    }
}

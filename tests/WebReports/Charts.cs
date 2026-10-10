internal static partial class Program
{
    // A chart under the pointer at a right-click is reported to the context menu, and given as a file: its own markup for an SVG file,
    // or a PNG drawn from it at a scale of its size on the page; a raster picture gives its own data and no SVG
    private static async Task CheckChartFiles()
    {
        await view.CallAsync("load", new { entries = Array.Empty<object>() });
        await view.AppendAsync("<p>A result</p><svg viewBox=\"0 0 300 150\" width=\"300\" height=\"150\"><rect x=\"10\" y=\"10\" width=\"280\" height=\"130\" fill=\"#2a7\"/><text x=\"20\" y=\"80\">A chart</text></svg>", "With a chart", "Chart", 1234);
        await Js("document.querySelector('#results svg rect').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        var target = await view.CallAsync("contextTarget");
        Check(target.GetProperty("chart").GetBoolean() && target.GetProperty("chartKind").GetString() == "svg", "a right-click on a chart reports a vector chart under the pointer");
        string svg = (await view.CallAsync("chartImage", new { format = "svg", scale = 1 })).GetString();
        Check(svg.StartsWith("<svg") && svg.Contains("xmlns=\"http://www.w3.org/2000/svg\"") && svg.Contains("<rect") && svg.Contains("A chart") && svg.Contains("viewBox=\"0 0 300 150\""), "the SVG file is the chart's own markup with its namespace");
        string png = (await view.CallAsync("chartImage", new { format = "png", scale = 2 })).GetString();
        Check(png.StartsWith("data:image/png;base64,"), "the PNG is drawn from the chart");
        byte[] bytes = Convert.FromBase64String(png[(png.IndexOf(',') + 1)..]);
        int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19], height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        double shown = (await Js("document.querySelector('#results svg').getBoundingClientRect().width")).GetDouble();
        Check(bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G' && width == 2 * Math.Round(shown) && height == width / 2, $"the PNG is a PNG at twice the chart's size on the page: {width} by {height} for {shown} wide");
        await Js("document.querySelector('#results p').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        Check(!(await view.CallAsync("contextTarget")).GetProperty("chart").GetBoolean(), "a right-click on text reports no chart");
        const string pixel = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";
        await view.CallAsync("load", new { entries = new[] { new { id = "p", title = "Picture", operation = "", helpContextId = 0, html = "<p>Before</p><img width='40' height='40' src='" + pixel + "'>" } } });
        await Js("document.querySelector('#results img').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        target = await view.CallAsync("contextTarget");
        Check(target.GetProperty("chart").GetBoolean() && target.GetProperty("chartKind").GetString() == "png", "a right-click on a raster picture reports a picture that is not a vector chart");
        Check((await view.CallAsync("chartImage", new { format = "png", scale = 2 })).GetString() == pixel, "a raster picture gives its own data as the PNG");
        bool refused = false;
        try { await view.CallAsync("chartImage", new { format = "svg", scale = 1 }); } catch (Exception) { refused = true; }
        Check(refused, "a raster picture gives no SVG");
    }
}

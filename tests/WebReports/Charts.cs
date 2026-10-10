using System.Text.Json;

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
        // an engine chart as the program draws it: styling in style attributes on spans, baseline shifts in em, a width with no height;
        // saved as other programs read it, and drawn the same
        await view.CallAsync("load", new { entries = new[] { new { id = "e", title = "Engine chart", operation = "", helpContextId = 0, html = "<p>An engine chart</p><svg width=\"3in\" viewBox=\"0 0 400 200\" data-report-chart-index=\"0\"><line x1=\"40\" y1=\"160\" x2=\"380\" y2=\"160\" style=\"stroke:#868686;stroke-linecap:square;\"/><g transform=\"translate(200,167)\"><text x=\"0\" y=\"0\" transform=\"rotate(0)\"><tspan dy=\"0.8em\" style=\"font-family:'Arial',sans-serif;font-size:20px;fill:#000000;text-anchor:middle;\">220</tspan></text></g><g transform=\"translate(30,100)\"><text x=\"0\" y=\"0\" transform=\"rotate(0)\"><tspan dy=\"0.4em\" style=\"font-family:'Arial',sans-serif;font-size:20px;fill:#000000;text-anchor:end;\">50.0</tspan></text></g><g transform=\"translate(12,100)\"><text x=\"0\" y=\"0\" transform=\"rotate(-90)\"><tspan dy=\"0\" style=\"font-family:'Arial',sans-serif;font-size:20px;font-weight:bold;fill:#000000;text-anchor:middle;\">Difference</tspan></text></g><ellipse cx=\"200\" cy=\"100\" rx=\"6\" ry=\"6\" style=\"stroke:#40699C;fill:none;\"/></svg>" } } });
        await Js("document.querySelector('#results svg line').dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))");
        string saved = (await view.CallAsync("chartImage", new { format = "svg", scale = 1 })).GetString();
        // (a report loaded again has had its charts normalised by the importer: colours as rgb(), a viewBox that fits the drawing)
        Check(saved.Contains("width=\"3in\"") && System.Text.RegularExpressions.Regex.IsMatch(saved, "height=\"1\\.4\\d*in\"") && !saved.Contains("data-report-chart-index"), "the saved SVG has both its width and its height in the chart's own unit, and no attribute of the editor's own");
        Check(System.Text.RegularExpressions.Regex.IsMatch(saved, "<text[^>]*text-anchor=\"middle\"[^>]*>220</text>") && System.Text.RegularExpressions.Regex.IsMatch(saved, "<text[^>]*font-family=\"Arial, ?sans-serif\"[^>]*font-size=\"20px\"[^>]*>") && !saved.Contains("<tspan") && !saved.Contains("style=\"") && !saved.Contains("rotate(0)"),
              "text alignment, font and colour are attributes of the text element, with no spans, style attributes or empty rotations left");
        Check(System.Text.RegularExpressions.Regex.IsMatch(saved, "<text[^>]*y=\"16\"[^>]*>220</text>") && System.Text.RegularExpressions.Regex.IsMatch(saved, "<text[^>]*y=\"8\"[^>]*text-anchor=\"end\"[^>]*>50.0</text>") && System.Text.RegularExpressions.Regex.IsMatch(saved, "<text[^>]*transform=\"rotate\\(-90\\)\"[^>]*>Difference</text>"),
              "a span's baseline shift in em is folded into the text's position, and a real rotation is kept");
        Check(saved.Contains("<line ") && (saved.Contains("stroke=\"#868686\"") || saved.Contains("stroke=\"rgb(134, 134, 134)\"")) && saved.Contains("stroke-linecap=\"square\"") && saved.Contains("<ellipse ") && saved.Contains("fill=\"none\""), "shape styling becomes presentation attributes");
        // drawn the same as the original: the two rasters differ in few pixels
        string same = JsonDocument.Parse(await view.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new { awaitPromise = true, returnByValue = true, expression = "(async()=>{const draw=async markup=>{const img=new Image();await new Promise((ok,bad)=>{img.onload=ok;img.onerror=bad;img.src='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(markup);});const c=document.createElement('canvas');c.width=400;c.height=200;const x=c.getContext('2d');x.fillStyle='white';x.fillRect(0,0,400,200);x.drawImage(img,0,0,400,200);return x.getImageData(0,0,400,200).data;};const original=document.querySelector('#results svg').cloneNode(true);original.setAttribute('xmlns','http://www.w3.org/2000/svg');original.setAttribute('width','400');original.setAttribute('height','200');const a=await draw(new XMLSerializer().serializeToString(original));const b=await draw(" + System.Text.Json.JsonSerializer.Serialize(System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(saved, " height=\"[0-9.]+in\"", ""), "width=\"3in\" ", "width=\"400\" height=\"200\" ")) + ");let differing=0,inked=0;for(let i=0;i<a.length;i+=4){const ia=a[i]+a[i+1]+a[i+2]<720,ib=b[i]+b[i+1]+b[i+2]<720;if(ia||ib)inked++;if(ia!==ib)differing++;}return inked+' '+differing;})()" }))).RootElement.GetProperty("result").GetProperty("value").GetString();
        int inked = int.Parse(same.Split(' ')[0]), differing = int.Parse(same.Split(' ')[1]);
        Check(inked > 500 && differing < inked / 20, $"the saved SVG draws the same as the chart in the report: {differing} of {inked} inked pixels differ");
        string png = (await view.CallAsync("chartImage", new { format = "png", scale = 2 })).GetString();
        Check(png.StartsWith("data:image/png;base64,"), "the PNG is drawn from the chart");
        byte[] bytes = Convert.FromBase64String(png[(png.IndexOf(',') + 1)..]);
        int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19], height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        double shownWidth = (await Js("document.querySelector('#results svg').getBoundingClientRect().width")).GetDouble(), shownHeight = (await Js("document.querySelector('#results svg').getBoundingClientRect().height")).GetDouble();
        Check(bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G' && width == 2 * Math.Round(shownWidth) && height == 2 * Math.Round(shownHeight), $"the PNG is a PNG at twice the chart's size on the page: {width} by {height} for {shownWidth} by {shownHeight}");
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

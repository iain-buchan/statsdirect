using System.Globalization;
using System.Reflection;
using StatsDirect.R;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;

// Checks of the rendering of reports as HTML (StatsDirectUI/TemplateProcessing/CreoleHtmlReportRenderer.cs), which the Mac version
// and Windows report windows both show: the text of a substituted value is to be encoded as the text
// of the template is, so that a P value below the display threshold, "P < 0.0001", and a title with an ampersand or an angle
// bracket give HTML that is well formed.  Run with: dotnet run -c Release

// A stand-in for the program as a template host: a P value is formatted as the program formats it below the display threshold
public class Host : DispatchProxy
{
    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        switch (method.Name)
        {
            case "pval":
            case "pval_half":
                double p = (double)arguments[0];
                return p < 0.0001 ? "P < 0.0001" : "P = " + p.ToString("0.####", CultureInfo.InvariantCulture);
            case "RoundU":
                return ((double)arguments[0]).ToString("R", CultureInfo.InvariantCulture);
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

static class Program
{
    private static int failures = 0;

    private static void Say(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {what}");
        if (!ok)
            failures++;
    }

    private static readonly ITemplateHost host = DispatchProxy.Create<ITemplateHost, Host>();

    // The HTML of a template with the values given as the outputs of a report
    private static string Rendered(string template, params (string name, object value)[] values)
    {
        ParameterBag bag = new();
        foreach ((string name, object value) in values)
            bag.AddOutput(name, value);
        // a template is a report of paragraphs, as the templates of the program are
        return new CreoleHtmlReportRenderer().Render(host, "<report>\n<p>" + template + "</p>\n</report>\n", bag);
    }

    private static void Check(string what, string html, string expectedPart, string forbiddenPart)
    {
        bool ok = html.Contains(expectedPart) && (forbiddenPart == null || !html.Contains(forbiddenPart));
        Say(ok, what + ": " + html.Trim() + (ok ? "" : $"  (expected \"{expectedPart}\"" + (forbiddenPart == null ? ")" : $" and not \"{forbiddenPart}\")")));
    }

    private static int Main()
    {
        Console.WriteLine("Reports rendered as HTML: the text of a value encoded, as the text of the template is");
        Check("a P value below the display threshold", Rendered("<pval>@{p:pval}</pval>", ("p", 0.00001)), "P &lt; 0.0001", "P < 0.0001");
        Check("a P value above it, as before", Rendered("<pval>@{p:pval}</pval>", ("p", 0.0432)), "P = 0.0432", null);
        Check("a title with an ampersand and angle brackets", Rendered("Title: @{title}", ("title", "A & B <c>")), "A &amp; B &lt;c&gt;", "<c>");
        Check("the text of the template, as before (an ampersand is written as an entity there)", Rendered("By Smith &amp; Jones"), "Smith &amp; Jones", "Smith & Jones");
        Check("a rounded number, as before", Rendered("Value: @{v:roundu}", ("v", 1.25)), "1.25", null);
        Console.WriteLine("An R chart as a vector picture: the svg element inline, sized from its width and height");
        // R's svg device writes an XML declaration and then the svg element with its size in points and a viewBox
        string r = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"432pt\" height=\"288pt\" viewBox=\"0 0 432 288\">\n<rect x=\"0\" y=\"0\" width=\"432\" height=\"288\" fill=\"white\"/>\n</svg>\n";
        MethodInfo vector = typeof(CreoleHtmlReportRenderer).Assembly.GetType("StatsDirect.R.RResultsParser")?.GetMethod("VectorPicture", BindingFlags.NonPublic | BindingFlags.Static);
        if (vector == null)
            Say(false, "the program has no VectorPicture");
        else
        {
            ReportVectorPicture picture = (ReportVectorPicture)vector.Invoke(null, new object[] { r });
            Say(picture.Width == 576 && picture.Height == 384, $"432 by 288 points are 576 by 384 pixels: {picture.Width} by {picture.Height}");
            Say(picture.Svg.StartsWith("<svg") && !picture.Svg.Contains("<?xml"), "the markup starts at the svg element, without the XML declaration");
            string html = Rendered("Chart: @{chart}", ("chart", picture));
            Check("the chart is inline in the report", html, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"432pt\"", "data:image");
            Check("nothing of the markup is encoded", html, "<rect x=\"0\"", "&lt;svg");
            ReportVectorPicture box = (ReportVectorPicture)vector.Invoke(null, new object[] { "<svg viewBox=\"0 0 300 150\"><g/></svg>" });
            Say(box.Width == 300 && box.Height == 150, $"with no width and height the viewBox gives the size: {box.Width} by {box.Height}");
            // svglite, which the Mac uses and a Windows R may, writes single quotes and decimals
            ReportVectorPicture lite = (ReportVectorPicture)vector.Invoke(null, new object[] { "<svg xmlns='http://www.w3.org/2000/svg' width='432.00pt' height='288.00pt' viewBox='0 0 432.00 288.00'><g/></svg>" });
            Say(lite.Width == 576 && lite.Height == 384, $"svglite's single quotes and decimals read the same: {lite.Width} by {lite.Height}");
            ReportVectorPicture liteBox = (ReportVectorPicture)vector.Invoke(null, new object[] { "<svg viewBox='0 0 300.00 150.00'><g/></svg>" });
            Say(liteBox.Width == 300 && liteBox.Height == 150, $"and so does a single-quoted viewBox: {liteBox.Width} by {liteBox.Height}");
        }
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASS" : $"{failures} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}

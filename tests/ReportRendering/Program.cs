using System.Text.RegularExpressions;
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
        // Copy image's glyph flattening: R's references to glyph symbols become the symbols' drawings at the references' positions
        MethodInfo flatten = typeof(CreoleHtmlReportRenderer).Assembly.GetTypes().FirstOrDefault(t => t.Name == "ScriptOutputView")
            ?.GetMethod("FlattenedGlyphs", BindingFlags.NonPublic | BindingFlags.Static);
        if (flatten == null) Say(false, "the program has no FlattenedGlyphs");
        else
        {
            string Flat(string svg) => (string)flatten.Invoke(null, new object[] { svg });
            string cairo = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"360pt\" height=\"288pt\" viewBox=\"0 0 360 288\">\n<defs>\n<g>\n<g id=\"glyph-0-0\">\n<path d=\"M 1 2 L 3 4 Z\"/>\n</g>\n<g id=\"glyph-0-1\">\n</g>\n</g>\n</defs>\n<g fill=\"rgb(0%, 0%, 0%)\">\n<use xlink:href=\"#glyph-0-0\" x=\"10.5\" y=\"20\"/>\n<use xlink:href=\"#glyph-0-1\" x=\"15\" y=\"20\"/>\n<use xlink:href=\"#glyph-0-0\" x=\"-3e1\" y=\"1.5E2\"/>\n</g>\n<use xlink:href=\"#clip-1\" x=\"0\" y=\"0\"/>\n</svg>";
            string flat = Flat(cairo);
            Say(flat.Contains("<g transform=\"translate(10.5 20)\"><path d=\"M 1 2 L 3 4 Z\"/></g>") && flat.Contains("<g transform=\"translate(-3e1 1.5E2)\"><path d=\"M 1 2 L 3 4 Z\"/></g>"),
                "a glyph reference becomes the glyph's drawing translated to its position");
            Say(!flat.Contains("#glyph-0-1") && flat.Contains("<use xlink:href=\"#clip-1\"") && flat.Contains("<g id=\"glyph-0-0\">"),
                "a reference to an empty glyph goes, another reference and the definitions stay");
            Say(Flat("<svg><rect/></svg>") == "<svg><rect/></svg>" && Flat("") == "" && Flat(null) == null, "an SVG with no references is left as it is");
            string nested = Flat("<svg><defs><g id=\"glyph-9-9\"><g transform=\"matrix(1 0 0 1 0 0)\"><rect width=\"1\" height=\"1\"/></g></g></defs><use xlink:href=\"#glyph-9-9\" x=\"1\" y=\"2\"/></svg>");
            Say(nested.Contains("<use xlink:href=\"#glyph-9-9\"") && !nested.Contains("translate("), "a glyph holding a group of its own keeps its references");
        }
        // the report's fragment reader: a string from a data object, UTF-8 bytes from a Win32 write (terminated or not), or nothing
        MethodInfo fragmentReader = typeof(CreoleHtmlReportRenderer).Assembly.GetTypes().FirstOrDefault(t => t.Name == "ReportClipboard")
            ?.GetMethod("Fragment", BindingFlags.NonPublic | BindingFlags.Static);
        if (fragmentReader == null) Say(false, "the program has no ReportClipboard.Fragment");
        else
        {
            string Read(System.Windows.Forms.DataObject contents) => (string)fragmentReader.Invoke(null, new object[] { contents });
            var asString = new System.Windows.Forms.DataObject(); asString.SetData("StatsDirect.ReportFragment.HTML.v1", false, "<svg>s</svg>");
            var asBytes = new System.Windows.Forms.DataObject(); asBytes.SetData("StatsDirect.ReportFragment.HTML.v1", false, new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<svg>b</svg>\0")));
            Say(Read(asString) == "<svg>s</svg>" && Read(asBytes) == "<svg>b</svg>" && Read(new System.Windows.Forms.DataObject()) == null,
                "the report's fragment reader takes a string, UTF-8 bytes with a terminator, or nothing");
        }
        // Copy image's SVG form: the root sized in plain pixel numbers however it is quoted; a root the method does not read, unchanged
        MethodInfo plain = typeof(CreoleHtmlReportRenderer).Assembly.GetTypes().FirstOrDefault(t => t.Name == "ScriptOutputView")
            ?.GetMethod("PlainSized", BindingFlags.NonPublic | BindingFlags.Static);
        if (plain == null) Say(false, "the program has no PlainSized");
        else
        {
            string Plain(string svg, int w, int h) => (string)plain.Invoke(null, new object[] { svg, w, h });
            static bool WellFormed(string svg) { try { System.Xml.Linq.XElement.Parse(svg); return true; } catch { return false; } }
            string cairo = Plain("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"360pt\" height=\"288pt\" viewBox=\"0 0 360 288\"><g/></svg>", 480, 384);
            Say(cairo.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"360\" height=\"288\" viewBox=") && !cairo.Contains("pt\"") && WellFormed(cairo),
                "a cairo root, sized in points, takes its viewBox's plain numbers in place, xmlns first: " + cairo[..Math.Min(72, cairo.Length)]);
            string lite = Plain("<svg xmlns='http://www.w3.org/2000/svg' width='432.00pt' height='288.00pt' viewBox='0 0 432.00 288.00'><g/></svg>", 576, 384);
            Say(Regex.Matches(lite, "width=").Count == 1 && Regex.Matches(lite, "height=").Count == 1 && lite.StartsWith("<svg xmlns='http://www.w3.org/2000/svg' width=\"432.00\" height=\"288.00\" viewBox=") && WellFormed(lite),
                "an svglite root, single-quoted, is sized once and stays well formed: " + lite[..Math.Min(72, lite.Length)]);
            string spaced = Plain("<svg xmlns=\"http://www.w3.org/2000/svg\" width = \"360pt\" height = \"288pt\" aria-label=\"a > b\"><g/></svg>", 480, 384);
            Say(Regex.Matches(spaced, "width").Count == 1 && Regex.IsMatch(spaced, "width = \"480\" height = \"384\"") && spaced.Contains("aria-label=\"a > b\"") && WellFormed(spaced),
                "spaces around = and a > inside a value are read as XML reads them, and no viewBox gives the pixel size");
            string unsized = Plain("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 150\"><g/></svg>", 300, 150);
            Say(unsized.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 300 150\" width=\"300\" height=\"150\">") && WellFormed(unsized), "a root with no size gets one after its attributes");
            string half = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"300\"><g/></svg>";
            Say(Plain(half, 300, 150) == half, "a root with a width and no height is left as it is");
            string prefixed = "<svg:svg xmlns:svg=\"http://www.w3.org/2000/svg\" width=\"360pt\" height=\"288pt\"><svg:g/></svg:svg>";
            Say(Plain(prefixed, 480, 384) == prefixed, "a namespace-prefixed root is left as it is");
            Say(Plain("no chart", 480, 384) == "no chart" && Plain(null, 1, 1) == null, "text with no root is left as it is");
        }
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASS" : $"{failures} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}

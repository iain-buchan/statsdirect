using System.Globalization;
using System.Reflection;
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
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASS" : $"{failures} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}

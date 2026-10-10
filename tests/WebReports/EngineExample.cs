using StatsDirect.Builtins;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.TemplateProcessing;
using StatsDirect.Templates;
using System.Globalization;
using System.Reflection;

public class ReportHostProxy : DispatchProxy
{
    protected override object Invoke(MethodInfo method, object[] args)
    {
        if (method.Name is "RoundU" or "pval" or "pval_half") return ((double)args[0]).ToString("G12", CultureInfo.InvariantCulture);
        if (method.ReturnType == typeof(void)) return null;
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

internal static class EngineExample
{
    // The real paired-t help example through the production builtin AND renderer,
    // including the agreement chart emitted by the same operation.
    internal static string PairedAgreement()
    {
        // Populate this test process's in-memory preferences before charting.
        // Missing preferences trigger first-run saves; a harness must never
        // overwrite the user's settings just to render its example chart.
        var settingsType = typeof(Parametric).Assembly.GetType("StatsDirect.UI.Properties.Settings");
        var settings = settingsType.GetProperty("Default").GetValue(null);
        settingsType.GetProperty("Markers").SetValue(settings, string.Join("|", Enumerable.Repeat("1;64,105,156;1;0;0;6", 10)));
        settingsType.GetProperty("TitleFont").SetValue(settings, new FontDescriptor("Arial", 1, 22).ToString());
        settingsType.GetProperty("LabelFont").SetValue(settings, new FontDescriptor("Arial", 0, 15).ToString());
        var data = new DataFrame();
        data.Variables.Add(new DoubleVariable(new double[] {312,242,340,388,296,254,391,402,290}, "PEFR Before"));
        data.Variables.Add(new DoubleVariable(new double[] {300,201,232,312,220,256,328,330,231}, "PEFR After"));
        var inputs = new ParameterBag();
        inputs.AddInput("data", data); inputs.AddInput("gamma", .95); inputs.AddInput("doAgreement", true);
        ParameterBag outputs = Parametric.RptTPaired(inputs).ParameterBag;
        if (Math.Abs(outputs["mean"].AsDouble - 56.111111111111114) > 1e-12) throw new Exception("Paired example mean differs.");
        var host = DispatchProxy.Create<ITemplateHost, ReportHostProxy>();
        var renderer = new CreoleHtmlReportRenderer();
        string folder = Path.Combine(AppContext.BaseDirectory, "Template");
        return renderer.Render(host, File.ReadAllText(Path.Combine(folder, "m_paired.creole")), outputs)
            + renderer.Render(host, File.ReadAllText(Path.Combine(folder, "charts.creole")), outputs);
    }
}

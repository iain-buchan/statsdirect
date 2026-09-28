// What the reports of the Chi-square Tests menu ask of the program about them: preferences, and a progress bar that shows nothing.
using System.Reflection;
using StatsDirect.Builtins;
using StatsDirect.Templates;

internal sealed class NoProgress : IProgressBarHost, IProgressBar
{
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => this;
    public void Finish() { }
    public bool Update(double fractionComplete) => false;
    public void Dispose() { }
}

// the preferences: those of the meta-analysis calculations as they are set here, six decimal places, and the default of everything else
public class PreferencesProxy : DispatchProxy
{
    public static bool MetaExact = true;
    public static double MetaCC = -9;
    public static bool Delay = false;

    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        switch (method.Name)
        {
            case "get_PDecimalPlaces":
            case "get_DisplayDecimalPlaces": return 6;
            case "get_MetaExact": return MetaExact;
            case "get_MetaCC": return MetaCC;
            case "get_DelayContinuityCorrection": return Delay;
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

internal sealed class Host : IPreferencesAndProgressBar
{
    private readonly NoProgress none = new();
    public string RoundU(double amount) => amount.ToString("G10");
    public string pval(double p) => p.ToString("G6");
    public string pval_half(double p) => p.ToString("G6");
    public SDPreferences Preferences { get; } = DispatchProxy.Create<SDPreferences, PreferencesProxy>();
    public IProgressBar StartProgress(string operationDescription, bool provideProgress, bool display = true) => none;
}

// What the r by c analysis asks of the program about it: the same, answers to its questions, and the scores of its tests, which
// come back as the program's own dialog gives them: in a bag, under "values1" and "values2"
public class TemplateHost : DispatchProxy
{
    public static double[] Scores1, Scores2;        // the scores that the dialog gives back; nothing: the scores that it was shown
    private static readonly NoProgress none = new();
    private static readonly SDPreferences preferences = DispatchProxy.Create<SDPreferences, PreferencesProxy>();

    protected override object Invoke(MethodInfo method, object[] arguments)
    {
        switch (method.Name)
        {
            case "get_Preferences": return preferences;
            case "StartProgress": return none;
            case "RoundU": return ((double)arguments[0]).ToString("G10");
            case "pval":
            case "pval_half": return ((double)arguments[0]).ToString("G6");
            case "GetBoolean":
                arguments[3] = false;
                return true;
            case "Amend":
                if (arguments[0] is ScoresOptions shown)
                {
                    ParameterBag bag = new();
                    bag.AddInput("values1", Scores1 ?? shown.Values1.ToArray());
                    bag.AddInput("values2", Scores2 ?? shown.Values2.ToArray());
                    return bag;
                }
                return new ParameterBag();
        }
        Type type = method.ReturnType;
        return type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static ITemplateHost New() => DispatchProxy.Create<ITemplateHost, TemplateHost>();
}

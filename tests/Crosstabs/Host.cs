// What the Crosstabs report asks of the program about it: preferences, a progress bar that shows nothing, answers to its questions and
// the scores of a test for trend.  The scores come back as the program's own dialog gives them: in the bag that Amend returns, under
// "values1" and "values2", the options that were handed over being left as they were.
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

public class HostProxy : DispatchProxy
{
    public static bool Symmetrise = false;          // the answer to "Force it to be symmetrical?"
    public static bool Continue = true;             // the answer to "Table is large ... Continue?"
    public static double[] Scores1, Scores2;        // the scores that the dialog gives back; nothing: the scores that it was shown
    public static bool CancelScores = false;
    public static List<string> Questions = new();
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
                Questions.Add((string)arguments[1] + ": " + ((string)arguments[0]).Replace("\r\n", " "));
                arguments[3] = false;
                return ((string)arguments[1]).Contains("symmetry") ? Symmetrise : Continue;
            case "Amend":
                if (arguments[0] is ScoresOptions shown)
                {
                    if (CancelScores) return null;
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

    public static ITemplateHost New() => DispatchProxy.Create<ITemplateHost, HostProxy>();
}

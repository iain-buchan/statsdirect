// What the reports of the Meta-analysis menu ask of the program about them: preferences, and a progress bar that shows nothing.
using System.Reflection;
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

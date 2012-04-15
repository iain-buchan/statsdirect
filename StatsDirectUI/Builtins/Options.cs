using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    /// <summary>
    /// A marker class to be passed through ITemplateHost.Amend
    /// </summary>
    public sealed class GraphicsOptions : IFillable
    {
        public string FillerToUse
        {
            get { return "GraphicsOptions"; }
        }
    }

    class Options
    {
        public static StepResult SetAnalysisOptions(ITemplateHost host, ParameterBag parameters)
        {
            SDPreferences preferences = host.Preferences;
            preferences.CanDefaultConfidenceInterval = parameters["use-default-ci"].AsBoolean;
            preferences.DefaultConfidenceInterval = double.Parse(parameters["default-ci"].AsString) / 100.0;
            preferences.GIDV = parameters["gidv"].AsBoolean;
            preferences.DisplayDecimalPlaces = int.Parse(parameters["decp"].AsString);
            preferences.PDecimalPlaces = int.Parse(parameters["pdecp"].AsString);
            preferences.ShouldKeepData = parameters["should-keep-data"].AsBoolean;
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }

        public static StepResult ShowGraphicsOptions(ITemplateHost host, ParameterBag parameters)
        {
            host.Amend(new GraphicsOptions(), parameters);
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }
    }
}

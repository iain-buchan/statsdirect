using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    static class Options
    {
        public static ParameterBag SetAnalysisOptions(ITemplateHost host, ParameterBag parameters)
        {
            SDPreferences preferences = host.Preferences;
            preferences.CanDefaultConfidenceInterval = parameters["use-default-ci"].AsBoolean;
            preferences.DefaultConfidenceInterval = Parsing.Cdbl_Txt(parameters["default-ci"].AsString) / 100.0;
            preferences.SelectGroupsByIdentifier = parameters["selectGroupsByIdentifier"].AsBoolean;
            preferences.DisplayDecimalPlaces = Parsing.Cint_Txt(parameters["decp"].AsString);
            preferences.PDecimalPlaces = Parsing.Cint_Txt(parameters["pdecp"].AsString);
            preferences.ShouldKeepData = parameters["should-keep-data"].AsBoolean;
            preferences.UseScientificNotationForSmallPValues = parameters["use-scientific-notation-for-small-p-values"].AsBoolean;
            return new ParameterBag();
        }

        public static ParameterBag ShowGraphicsOptions(ITemplateHost host, ParameterBag parameters)
        {
            host.Amend(new GraphicsOptions(), parameters);
            return new ParameterBag();
        }
    }
}

using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    internal class Options
    {
        private ISdPreferences SdPreferences { get; }
        private IUiPreferences UiPreferences { get; }
        private IUserInterface UserInterface { get; }

        public Options(ISdPreferences sdPreferences, IUiPreferences uiPreferences, IUserInterface userInterface)
        {
            SdPreferences = sdPreferences;
            UiPreferences = uiPreferences;
            UserInterface = userInterface;
        }

        public StepOutput SetAnalysisOptions(ParameterBag parameters)
        {
            SdPreferences.CanDefaultConfidenceInterval = parameters["use-default-ci"].AsBoolean;
            SdPreferences.DefaultConfidenceInterval = Parsing.Cdbl_Txt(parameters["default-ci"].AsString) / 100.0;
            UiPreferences.SelectGroupsByIdentifier = parameters["selectGroupsByIdentifier"].AsBoolean;
            SdPreferences.DisplayDecimalPlaces = Parsing.Cint_Txt(parameters["decp"].AsString);
            SdPreferences.PDecimalPlaces = Parsing.Cint_Txt(parameters["pdecp"].AsString);
            SdPreferences.UseScientificNotationForSmallPValues = parameters["use-scientific-notation-for-small-p-values"].AsBoolean;
            return StepOutput.Empty();
        }

        public StepOutput ShowGraphicsOptions(ParameterBag parameters)
        {
            UserInterface.Amend(new GraphicsOptions(), parameters);
            return StepOutput.Empty();
        }
    }
}

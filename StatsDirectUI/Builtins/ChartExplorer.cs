using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public enum ChartExplorerChartType
    {
        BoxWhisker,
        Histogram
    }

    public class ChartExplorerOptions : IFillable
    {
        public ChartExplorerChartType ChartType { get; set; }
        public ParameterBag Parameters { get; set; }
        public string ChartAsRtf { get; set; }

        #region IFillable Members

        public string FillerToUse => "ChartExplorer";

        #endregion
    }

    public class ChartExplorer
    {
        private IUserInterface UserInterface { get; }

        public ChartExplorer(IUserInterface userInterface)
        {
            UserInterface = userInterface;
        }

        public StepOutput ExploreContinuousDistributions(ParameterBag parameters)
        {
            ChartExplorerOptions options = new() { ChartType = ChartExplorerChartType.Histogram, Parameters = parameters};
            if (null == UserInterface.Amend(options, parameters))
                throw new TemplateOperationCancelledException();
            ParameterBag outputParameters = new();
            if (null != options.ChartAsRtf)
                outputParameters.AddOutput("chart", options.ChartAsRtf);
            return new StepOutput(outputParameters);
        }

        public StepOutput CompareSeveralContinuousVariables(ParameterBag parameters)
        {
            ChartExplorerOptions options = new() { ChartType = ChartExplorerChartType.BoxWhisker, Parameters = parameters};
            if (null == UserInterface.Amend(options, parameters))
                throw new TemplateOperationCancelledException();
            ParameterBag outputParameters = new();
            if (null != options.ChartAsRtf)
                outputParameters.AddOutput("chart", options.ChartAsRtf);
            return new StepOutput(outputParameters);
        }
    }
}

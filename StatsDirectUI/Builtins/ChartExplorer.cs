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

        public string FillerToUse
        {
            get { return "ChartExplorer"; }
        }

        #endregion
    }

    public class ChartExplorer
    {
        public static StepResult ExploreContinuousDistributions(ITemplateHost host, ParameterBag parameters)
        {
            ChartExplorerOptions options = new ChartExplorerOptions
                                               {ChartType = ChartExplorerChartType.Histogram, Parameters = parameters};
            if (!host.Amend(options, parameters))
                throw new TemplateOperationCancelledException();
            ParameterBag outputParameters = new ParameterBag();
            if (null != options.ChartAsRtf)
            {
                outputParameters.AddOutput("chart", options.ChartAsRtf);
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult CompareSeveralContinuousVariables(ITemplateHost host, ParameterBag parameters)
        {
            ChartExplorerOptions options = new ChartExplorerOptions
                                               {ChartType = ChartExplorerChartType.BoxWhisker, Parameters = parameters};
            if (!host.Amend(options, parameters))
                throw new TemplateOperationCancelledException();
            ParameterBag outputParameters = new ParameterBag();
            if (null != options.ChartAsRtf)
            {
                outputParameters.AddOutput("chart", options.ChartAsRtf);
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }
    }
}

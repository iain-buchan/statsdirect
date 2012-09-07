using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class Distribution
    {
        public static StepResult DistNormal(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Z);
        }


        public static StepResult DistT(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.T);
        }


        public static StepResult DistF(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.F);
        }


        public static StepResult DistChiSquare(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.ChiSq);
        }


        public static StepResult DistQ(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Q);
        }


        public static StepResult DistBinomial(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Binomial);
        }


        public static StepResult DistPoisson(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Poisson);
        }


        public static StepResult DistKendall(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Kendall);
        }


        public static StepResult DistSpearman(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Rho);
        }


        public static StepResult DistNonCentralT(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.NonCentralT);
        }


        private static StepResult DistributionOf(ITemplateHost host, ParameterBag parameters, DistributionType selectedText)
        {
            DistributionOptions distributionOptions = new DistributionOptions { SelectedTest = selectedText };
            host.Amend(distributionOptions, parameters);
            ParameterBag outputParameters = new ParameterBag();
            return new StepResult(StepSuccess.Success, outputParameters);
        }

    }

    public enum DistributionType
    {
        None,
        Z,
        T,
        F,
        ChiSq,
        Q,
        Binomial,
        Poisson,
        Kendall,
        Rho,
        NonCentralT
    }

    public class DistributionOptions : IFillable
    {
        public DistributionType SelectedTest { get; set; }
        public string FillerToUse
        {
            get { return "Distribution"; }
        }
    }
}

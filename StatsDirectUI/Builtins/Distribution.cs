using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class Distribution
    {
        public static ParameterBag DistNormal(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Z);
        }

        public static ParameterBag DistT(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.T);
        }

        public static ParameterBag DistF(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.F);
        }

        public static ParameterBag DistChiSquare(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.ChiSq);
        }

        public static ParameterBag DistQ(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Q);
        }

        public static ParameterBag DistBinomial(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Binomial);
        }

        public static ParameterBag DistPoisson(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Poisson);
        }

        public static ParameterBag DistKendall(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Kendall);
        }

        public static ParameterBag DistSpearman(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.Rho);
        }

        public static ParameterBag DistNonCentralT(ITemplateHost host, ParameterBag parameters)
        {
            return DistributionOf(host, parameters, DistributionType.NonCentralT);
        }

        private static ParameterBag DistributionOf(ITemplateHost host, ParameterBag parameters, DistributionType selectedText)
        {
            DistributionOptions distributionOptions = new DistributionOptions { SelectedTest = selectedText };
            ParameterBag outputParameters = host.Amend(distributionOptions, parameters);
            return outputParameters;
        }
    }
}

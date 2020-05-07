using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public static class Distribution
    {
        public static ParameterBag DistNormal(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.Z);

        public static ParameterBag DistT(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.T);

        public static ParameterBag DistF(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.F);

        public static ParameterBag DistChiSquare(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.ChiSq);

        public static ParameterBag DistQ(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.Q);

        public static ParameterBag DistBinomial(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.Binomial);

        public static ParameterBag DistPoisson(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.Poisson);

        public static ParameterBag DistKendall(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.Kendall);

        public static ParameterBag DistSpearman(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.Rho);

        public static ParameterBag DistNonCentralT(IUserInterface host, ParameterBag parameters) => DistributionOf(host, parameters, DistributionType.NonCentralT);

        private static ParameterBag DistributionOf(IUserInterface host, ParameterBag parameters, DistributionType selectedTest)
        {
            DistributionOptions distributionOptions = new DistributionOptions { SelectedTest = selectedTest };
            ParameterBag outputParameters = host.Amend(distributionOptions, parameters);
            return outputParameters;
        }
    }
}

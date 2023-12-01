using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class Distribution
    {
        private IUserInterface UserInterface { get; }

        public Distribution(IUserInterface userInterface)
        {
            UserInterface = userInterface;
        }

        public StepOutput DistNormal(ParameterBag parameters) => DistributionOf(parameters, DistributionType.Z);

        public StepOutput DistT(ParameterBag parameters) => DistributionOf(parameters, DistributionType.T);

        public StepOutput DistF(ParameterBag parameters) => DistributionOf(parameters, DistributionType.F);

        public StepOutput DistChiSquare(ParameterBag parameters) => DistributionOf(parameters, DistributionType.ChiSq);

        public StepOutput DistQ(ParameterBag parameters) => DistributionOf(parameters, DistributionType.Q);

        public StepOutput DistBinomial(ParameterBag parameters) => DistributionOf(parameters, DistributionType.Binomial);

        public StepOutput DistPoisson(ParameterBag parameters) => DistributionOf(parameters, DistributionType.Poisson);

        public StepOutput DistKendall(ParameterBag parameters) => DistributionOf(parameters, DistributionType.Kendall);

        public StepOutput DistSpearman(ParameterBag parameters) => DistributionOf(parameters, DistributionType.Rho);

        public StepOutput DistNonCentralT(ParameterBag parameters) => DistributionOf(parameters, DistributionType.NonCentralT);

        private StepOutput DistributionOf(ParameterBag parameters, DistributionType selectedTest)
        {
            DistributionOptions distributionOptions = new() { SelectedTest = selectedTest };
            ParameterBag? outputParameters = UserInterface.Amend(distributionOptions, parameters);
            return new StepOutput(outputParameters);
        }
    }
}

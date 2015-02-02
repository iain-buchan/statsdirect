using System;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class GroupedCovarianceParameter: Parameter
    {
        public override bool RequiresGrid
        {
            get
            {
                return true;
            }
        }

        public override ParameterType Type
        {
            get { return ParameterType.GroupedCovariance; }
        }
        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return (MustRequest || null != Name && null != parameters && !parameters.ContainsKey(Name)) ? InputDuringStep.Always : InputDuringStep.Never;
        }
    }
}

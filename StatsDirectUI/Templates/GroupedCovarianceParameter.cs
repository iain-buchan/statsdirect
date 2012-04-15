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
    }
}

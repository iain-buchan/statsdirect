using System;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class Double2By2ByKParameter: Parameter
    {
        public override ParameterType Type
        {
            get { return ParameterType.Double2By2ByK; }
        }
    }
}

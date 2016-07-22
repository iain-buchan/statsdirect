using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class AgreementOptions : GenericOptions
    {
        public AgreementOptions(bool useColour)
            : base(useColour)
        {
        }

        public override ChartOptionType OptionType
        {
            get
            {
                return ChartOptionType.Agreement;
            }
        }

        public double[] av;
        public double[] mxd;
        public double lla;
        public double ula;
        public double mean;
        public double P0;
        public bool HasLimits;

        public override bool ShowLegendIsRelevant
        {
            get
            {
                return false;
            }
        }

        public override bool UsesShowLegend
        {
            get
            {
                return false;
            }
        }
    }


}

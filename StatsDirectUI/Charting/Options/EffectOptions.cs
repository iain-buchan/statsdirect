using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class EffectOptions : ForestishOptions
    {
        public IList<double> ControlGroupSizes { get; }
        public IList<double> ExperimentGroupSizes { get; }
        public double rmh { get; }
        public double LowerLimit { get; }
        public double UpperLimit { get; }
        public string Caption { get; }
        public int pbias { get; }
        public string qid { get; }

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public EffectOptions(IChartPreferences chartPreferences, int k, double[] controlGroupSizes, double[] experimentGroupSizes, string[] titles, double rmh, double ll, double ul, double cco, double[] oddsRatios, double[] oddsRatioLcis, double[] oddsRatioUcis, string cap, int pbias, string qid)
            : base(chartPreferences, cco, groupSizes, k, markCentres, markerTypes, oddsRatioLcis, oddsRatios, oddsRatioUcis, seriesOptions, titles)
        {
            ControlGroupSizes = controlGroupSizes;
            ExperimentGroupSizes = experimentGroupSizes;
            this.rmh = rmh;
            LowerLimit = ll;
            UpperLimit = ul;
            this.Caption = cap;
            this.pbias = pbias;
            this.qid = qid;
        }
    }
}

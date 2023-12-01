using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class MHOptions : ForestishOptions
    {
        public double Rmh { get; }
        public double LowerLimit { get; }
        public double UpperLimit { get; }
        public IReadOnlyList<bool> Lerr { get; }
        public IReadOnlyList<bool> Uerr { get; }
        public string Caption { get; }
        public int Pbias { get; }
        public string Qid { get; }
        /// <summary>
        /// For test of IncludeTable, the result.  Where not required, set null to assume all true.
        /// </summary>
        public IReadOnlyList<bool>? Included { get; }
        public int LowerBound { get; }

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public MHOptions(IChartPreferences chartPreferences,
            int lowerBound,
            int k,
            IReadOnlyList<double> odw,
            IReadOnlyList<string> titles,
            double rmh,
            double lowerLimit,
            double upperLimit,
            double cco,
            IReadOnlyList<double> oddsRatios,
            IReadOnlyList<double> oddsRatioLcis,
            IReadOnlyList<double> oddsRatioUcis,
            IReadOnlyList<bool> lerr,
            IReadOnlyList<bool> uerr,
            IReadOnlyList<bool>? included,
            string cap,
            int pbias,
            string qid)
            : base(chartPreferences, cco, odw, k, markCentres, markerTypes, oddsRatioLcis, oddsRatios, oddsRatioUcis, seriesOptions, titles)
        {
            Caption = cap;
            Included = included;
            Lerr = lerr;
            LowerBound = lowerBound;
            LowerLimit = lowerLimit;
            Pbias = pbias;
            Qid = qid;
            Rmh = rmh;
            Uerr = uerr;
            UpperLimit = upperLimit;
        }
    }
}

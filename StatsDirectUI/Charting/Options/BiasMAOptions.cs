using System;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class BiasMAOptions : AbstractGenericOptions
    {
        public double[] X { get; }
        public double[] YY { get; }
        public double[] YW { get; }
        public int Rows { get; }
        public double[] Cl { get; }
        public double[] Cu { get; }
        public double Cco { get; }
        public double Cit { get; }
        public double Rmh { get; }
        public Transformation Xform { get; }
        public bool Diagonal { get; }

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public BiasMAOptions(double[] x, double[] yy, double[] yw, int rows, string xtxt, double[] cl, double[] cu, double cco, double cit, double rmh, Transformation xform, bool diagonal, IChartPreferences chartPreferences)
            : base(chartPreferences)
        {
            X = x;
            YY = yy;
            YW = yw;
            Rows = rows;
            XAxisTitle = xtxt;
            Cl = cl;
            Cu = cu;
            Cco = cco;
            Cit = cit;
            Rmh = rmh;
            Xform = xform;
            Diagonal = diagonal;
        }
    }
}

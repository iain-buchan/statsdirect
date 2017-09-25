using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using StatsDirect.Data;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    [Serializable]
    public class PyramidOptions : GenericOptions
    {

        public DataFrame MaleFrame;
        public DataFrame FemaleFrame;
        public DataFrame LabelFrame;
        public double ScaleMaximum;

        public PyramidOptions(bool useColour) : base(useColour)
        {
        }

        public void SetOptions()
        {
            DataFrame f = new DataFrame();
            double maxRow = 0;
            if (MaleFrame != null && MaleFrame.VariableCount > 0)
                f.Variables.Add(MaleFrame.Variables[0]);
            if (FemaleFrame != null && FemaleFrame.VariableCount > 0)
                f.Variables.Add(FemaleFrame.Variables[0]);

            //  A pyramid plot has one marker for male and an optional second for female.
            MarkerTypes = new List<MarkerType>();
            for (int seriesIndex = 0; seriesIndex <= f.VariableCount - 1; seriesIndex++)
            {
                IVariable v = f.Variables[seriesIndex];

                MarkerType marker = new MarkerType { MarkerColor = Color.Gray, LineColor = Color.Gray, IsMarkerFilled = false };

                if (Regex.Match(v.Title, @"\b(male|males|men)\b", RegexOptions.IgnoreCase).Success)
                {
                    marker.MarkerColor = Color.Blue;
                    marker.LineColor = Color.Blue;
                }
                else if (Regex.Match(v.Title, @"\b(female|females|women)\b", RegexOptions.IgnoreCase).Success)
                {
                    marker.MarkerColor = Color.Magenta;
                    marker.LineColor = Color.Magenta;
                }
                MarkerTypes.Add(marker);

                SeriesOptionsDescriptor sod = new SeriesOptionsDescriptor
                {
                    SeriesName = v.Title,
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    AllowChangeToMarkerSize = false,
                    AllowChangeToMarkerType = false,
                    AllowChangeToFill = true,
                    MarkerIndex = seriesIndex
                };
                SeriesOptions.Add(sod);
                maxRow = Math.Max(maxRow, (v as DoubleVariable).Max);
            }

            //  Work out a reasonable axis value
            IAxisScale axisScale = AxisScalerFactory.AxisScalerFor(ScaleType.Linear).Q_Axis(0, 0, maxRow, false, false);
            ScaleMaximum = axisScale.MaximumScaleValue;
        }

        public override bool UsesChartTitle => true;

        public override bool UsesShowLegend => false;

        public override bool UsesAxisLineThickness => false;

        public override bool UsesAxisLabelFontDescriptor => true;

        public override bool ShowPyramidOptions => true;

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

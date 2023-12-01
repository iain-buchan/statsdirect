using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class PyramidOptions : AbstractGenericOptions
        , IAxisLabelFontOptions
        , IChartTitleOptions
        , IMarkerTypes
    {
        public DoubleSeries? FemaleSeries { get; }
        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public StringSeries? LabelSeries { get; }
        public DoubleSeries MaleOrOnlySeries { get; }
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }
        public double ScaleMaximum { get; }

        public PyramidOptions(IChartPreferences chartPreferences,
            DoubleSeries maleOrOnlySeries,
            DoubleSeries? femaleSeries,
            StringSeries? labelSeries,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            FontDescriptor? legendFontDescriptor = default,
            ChartOrientation? orientation = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
            bool? showLegend = default,
            string? title = default,
            FontDescriptor? titleFontDescriptor = default,
            bool? useColour = default,
            string? xAxisTitle = default,
            string? yAxisTitle = default)
            : base(chartPreferences,
                  axisLabelFontDescriptor,
                  axisLineThickness,
                  axisTitleFontDescriptor,
                  legendFontDescriptor,
                  orientation,
                  seriesTitles,
                  shouldAutoscale,
                  shouldBoxAxes,
                  showLegend,
                  title,
                  titleFontDescriptor,
                  useColour,
                  xAxisTitle,
                  yAxisTitle)
        {
            MaleOrOnlySeries = maleOrOnlySeries;
            FemaleSeries = femaleSeries;
            LabelSeries = labelSeries;

            List<DoubleSeries> toProcess = new() { maleOrOnlySeries };
            double maxRow = 0;
            if (femaleSeries is not null)
                toProcess.Add(femaleSeries);

            //  A pyramid plot has one marker for male and an optional second for female.
            List<MarkerType> markerTypes = new();
            List<SeriesOptionsDescriptor> seriesOptionsDescriptors = new();
            for (int seriesIndex = 0; seriesIndex < toProcess.Count; seriesIndex++)
            {
                DoubleSeries v = toProcess[seriesIndex];

                ColorDescriptor markerColor = ColorDescriptor.Gray;
                if (v.Title is not null)
                {
                    if (Regex.Match(v.Title, @"\b(male|males|men)\b", RegexOptions.IgnoreCase).Success)
                        markerColor = ColorDescriptor.Blue;
                    else if (Regex.Match(v.Title, @"\b(female|females|women)\b", RegexOptions.IgnoreCase).Success)
                        markerColor = ColorDescriptor.Magenta;
                }

                markerTypes.Add(new(MarkerType.Default,
                    isMarkerFilled: false,
                    markerColor: markerColor,
                    lineColor: markerColor
                ));

                SeriesOptionsDescriptor sod = new()
                {
                    SeriesName = v.Title,
                    AllowChangeToDashStyle = false,
                    AllowChangeToLineThickness = false,
                    AllowChangeToMarkerSize = false,
                    AllowChangeToMarkerType = false,
                    AllowChangeToFill = true,
                    MarkerIndex = seriesIndex
                };
                seriesOptionsDescriptors.Add(sod);
                maxRow = Math.Max(maxRow, v.Max);
            }

            //  Work out a reasonable axis value
            IAxisScale axisScale = AxisScalerFactory.AxisScalerFor(ScaleType.Linear).QAxis(0, 0, maxRow, false, false);
            SeriesOptions = seriesOptionsDescriptors;
            MarkerTypes = markerTypes;
            ScaleMaximum = axisScale.MaximumScaleValue;
        }

        public override bool ShowLegendIsRelevant => false;
        public override bool UsesAxisLineThickness => false;
        public override bool UsesShowLegend => false;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}

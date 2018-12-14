using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [Serializable]
    public class ForestOptions : GenericOptions
    {
        public double[] gn;
        public int k;
        public double[] OddsRatios { get; set; }
        public double[] OddsRatioLcis { get; set; }
        public double[] OddsRatioUcis { get; set; }
        public double[] pg; // Should really be integer or bool but assigning a double variable is as efficient
        public string[] Titles { get; set; }
        public int EffectSizeAndIntervalDecimalPlaces { get; set; }
        public bool MarkCentres { get; set; }

        public ForestOptions(bool useColour)
            : base(useColour)
        {
            MarkCentres = true;

            //  A forest plot has one marker for the study and a second for the pooled effect
            MarkerTypes = new List<MarkerType>();
            MarkerType studyMarkerType = new MarkerType
                                             {
                                                 MarkerColor = ColorDescriptor.Gray,
                                                 LineColor = ColorDescriptor.Black,
                                                 IsMarkerFilled = true,
                                                 MarkerShape = MarkerShape.Square,
                                                 LineDashStyle = DashStyleDescriptor.Solid,
                                                 Width = 1
                                             };
            MarkerTypes.Add(studyMarkerType);
            MarkerType pooledMarkerType = new MarkerType
                                              {
                                                  MarkerColor = ColorDescriptor.Gray,
                                                  LineColor = ColorDescriptor.Black,
                                                  IsMarkerFilled = true,
                                                  MarkerShape = MarkerShape.Diamond,
                                                  LineDashStyle = DashStyleDescriptor.Solid,
                                                  Width = 1
                                              };
            MarkerTypes.Add(pooledMarkerType);

            SeriesOptionsDescriptor studyOptions = new SeriesOptionsDescriptor
                                                       {
                                                           SeriesName = "Study",
                                                           AllowChangeToDashStyle = false,
                                                           AllowChangeToMarkerSize = false,
                                                           AllowChangeToLineThickness = false,
                                                           MarkerIndex = 0
                                                       };
            SeriesOptions.Add(studyOptions);

            SeriesOptionsDescriptor pooledOptions = new SeriesOptionsDescriptor
                                                        {
                                                            SeriesName = "Pooled effect",
                                                            AllowChangeToDashStyle = false,
                                                            AllowChangeToMarkerSize = false,
                                                            AllowChangeToLineThickness = false,
                                                            MarkerIndex = 1
                                                        };
            SeriesOptions.Add(pooledOptions);
        }

        public override bool UsesChartTitle => true;

        public override bool UsesXAxisTitle => true;

        public override bool ShowForestOptions => true;

        public override bool UsesAxisLabelFontDescriptor => true;

        public override bool UsesAxisTitleFontDescriptor => true;

        public override bool UsesShowLegend => false;

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

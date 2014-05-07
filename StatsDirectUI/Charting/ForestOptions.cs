using System;
using System.Collections.Generic;
using System.Drawing;

namespace StatsDirect.Charting
{
    [Serializable]
    public class ForestOptions : GenericOptions
    {
        public override ChartOptionType OptionType
        {
            get
            {
                return ChartOptionType.Forest;
            }
        }

        public double[] gn;
        public int k;
        public double[] odr;
        public double[] odrl;
        public double[] odru;
        public double[] pg; //  Should really be Integer but assigning a double variable is as efficient
        public string[] titles;
        public int EffectSizeAndIntervalDecimalPlaces;
        public float StudyCiLineThickness;

        public ForestOptions(bool useColour)
            : base(useColour)
        {
            //  A forest plot has one marker for the study and a second for the pooled effect
            MarkerTypes = new List<MarkerType>();
            MarkerType studyMarkerType = new MarkerType
                                             {
                                                 Color = Color.Black,
                                                 IsFilled = true,
                                                 Shape = MarkerShape.Square,
                                                 Style = System.Drawing.Drawing2D.DashStyle.Solid,
                                                 Width = 1
                                             };
            MarkerTypes.Add(studyMarkerType);
            MarkerType pooledMarkerType = new MarkerType
                                              {
                                                  Color = Color.Black,
                                                  IsFilled = false,
                                                  Shape = MarkerShape.Diamond,
                                                  Style = System.Drawing.Drawing2D.DashStyle.Solid,
                                                  Width = 1
                                              };
            MarkerTypes.Add(pooledMarkerType);

            SeriesOptionsDescriptor studyOptions = new SeriesOptionsDescriptor
                                                       {
                                                           SeriesName = "Study",
                                                           AllowChangeToDashStyle = false,
                                                           AllowChangeToLineThickness = false,
                                                           AllowChangeToMarkerSize = false,
                                                           MarkerIndex = 0
                                                       };
            SeriesOptions.Add(studyOptions);

            SeriesOptionsDescriptor pooledOptions = new SeriesOptionsDescriptor
                                                        {
                                                            SeriesName = "Pooled effect",
                                                            AllowChangeToDashStyle = false,
                                                            AllowChangeToLineThickness = false,
                                                            AllowChangeToMarkerSize = false,
                                                            MarkerIndex = 1
                                                        };
            SeriesOptions.Add(pooledOptions);
        }

        public override bool UsesChartTitle
        {
            get
            {
                return true;
            }
        }

        public override bool UsesXAxisTitle
        {
            get
            {
                return true;
            }
        }

        public override bool ShowForestOptions
        {
            get
            {
                return true;
            }
        }

        public override bool UsesAxisLabelFontDescriptor
        {
            get
            {
                return true;
            }
        }

        public override bool UsesAxisTitleFontDescriptor
        {
            get
            {
                return true;
            }
        }

        public override bool UsesShowLegend
        {
            get
            {
                return false;
            }
        }

        public override bool ShowLegendIsRelevant
        {
            get
            {
                return false;
            }
        }
    }
}

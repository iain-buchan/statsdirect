using System;
using System.Collections.Generic;
using System.Drawing;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class ForestOptions
    [ Serializable ]
    public class ForestOptions : GenericOptions 
    { 
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.Forest; 
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
        
        public ForestOptions( bool useColour ) : base( useColour ) 
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
            MarkerTypes.Add( studyMarkerType );
            MarkerType pooledMarkerType = new MarkerType
                                              {
                                                  Color = Color.Black,
                                                  IsFilled = false,
                                                  Shape = MarkerShape.Diamond,
                                                  Style = System.Drawing.Drawing2D.DashStyle.Solid,
                                                  Width = 1
                                              };
            MarkerTypes.Add( pooledMarkerType );

            SeriesOptionsDescriptor studyOptions = new SeriesOptionsDescriptor
                                                       {
                                                           SeriesName = "Study",
                                                           AllowChangeToDashStyle = false,
                                                           AllowChangeToLineThickness = false,
                                                           AllowChangeToMarkerSize = false,
                                                           MarkerIndex = 0
                                                       };
            SeriesOptions.Add( studyOptions );

            SeriesOptionsDescriptor pooledOptions = new SeriesOptionsDescriptor
                                                        {
                                                            SeriesName = "Pooled effect",
                                                            AllowChangeToDashStyle = false,
                                                            AllowChangeToLineThickness = false,
                                                            AllowChangeToMarkerSize = false,
                                                            MarkerIndex = 1
                                                        };
            SeriesOptions.Add( pooledOptions ); 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesChartTitle
        public override bool UsesChartTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesXAxisTitle
        public override bool UsesXAxisTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property ShowForestOptions
        public override bool ShowForestOptions 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesAxisLabelFontDescriptor
        public override bool UsesAxisLabelFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesAxisTitleFontDescriptor
        public override bool UsesAxisTitleFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesShowLegend
        public override bool UsesShowLegend 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property ShowLegendIsRelevant
        public override bool ShowLegendIsRelevant 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
    } 
    
    
} 

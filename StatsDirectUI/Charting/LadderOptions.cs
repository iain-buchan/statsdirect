using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class LadderOptions
    [ Serializable ]
    public class LadderOptions : GenericOptions 
    { 
        
        public LadderOptions( bool useColour ) : base( useColour ) 
        { 
            
            //  A ladder plot's markers are derived from the first two series.
            MarkerTypes = new List<MarkerType>(); 
            MarkerType leftHandMarkerType = ChartRenderer.MarkerTypes[ 0 ].Clone(); 
            MarkerType rightHandMarkerType = ChartRenderer.MarkerTypes[ 1 ].Clone(); 
            leftHandMarkerType.MarkerSize = 6; 
            rightHandMarkerType.MarkerSize = 6; 
            MarkerTypes.Add( leftHandMarkerType ); 
            MarkerTypes.Add( rightHandMarkerType ); 
            
            //  A ladder plot has a left-hand and a right-hand series, connected by a line.
            //  The line uses the left-hand marker's line type and thickness
            SeriesOptionsDescriptor leftHandOptions = new SeriesOptionsDescriptor
                                                          {
                                                              SeriesName = "Left hand markers",
                                                              AllowChangeToDashStyle = false,
                                                              AllowChangeToLineThickness = false,
                                                              MarkerIndex = 0
                                                          };




            SeriesOptions.Add( leftHandOptions );

            SeriesOptionsDescriptor ladderRungOptions = new SeriesOptionsDescriptor
                                                                                 {
                                                                                     SeriesName = "Ladder rungs",
                                                                                     AllowChangeToMarkerColour = false,
                                                                                     AllowChangeToMarkerSize = false,
                                                                                     AllowChangeToMarkerType = false,
                                                                                     MarkerIndex = 0
                                                                                 };





            SeriesOptions.Add( ladderRungOptions );

            SeriesOptionsDescriptor rightHandOptions = new SeriesOptionsDescriptor
                                                           {
                                                               SeriesName = "Right hand markers",
                                                               AllowChangeToDashStyle = false,
                                                               AllowChangeToLineThickness = false,
                                                               MarkerIndex = 1
                                                           };




            SeriesOptions.Add( rightHandOptions ); 
        } 
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override ChartOptionType OptionType 
        { 
            get 
            { 
                return ChartOptionType.Ladder; 
            } 
        } 
        // TRANSMISSINGCOMMENT: Property UsesAutoscale
        public override bool UsesAutoscale 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesBoxAxes
        public override bool UsesBoxAxes 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesChartTitle
        public override bool UsesChartTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesSeriesLabels
        public override bool UsesSeriesLabels 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesYAxisTitle
        public override bool UsesYAxisTitle 
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
        
        // TRANSMISSINGCOMMENT: Property UsesAxisLabelFontDescriptor
        public override bool UsesAxisLabelFontDescriptor 
        { 
            get 
            { 
                return true; 
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

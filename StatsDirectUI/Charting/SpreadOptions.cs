using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class SpreadOptions : GenericOptions 
    { 
        
        public SpreadOptions( bool UseColour ) : base( UseColour ) 
        { 
            
            MarkerTypes = new List<MarkerType>(); 
            int mkr = SeriesNumberToMarkerNumber( 0 ); 
            MarkerType markerType = ChartRenderer.MarkerTypes[ mkr ].Clone(); 
            markerType.MarkerSize = 6; 
            MarkerTypes.Add( markerType ); 
            
            //  A spread plot has a single series with no lines.
            SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
                                                      {
                                                          SeriesName = "Markers",
                                                          AllowChangeToDashStyle = false,
                                                          AllowChangeToLineThickness = false,
                                                          MarkerIndex = 0
                                                      };
            SeriesOptions.Add( soleOptions ); 
        } 
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.Spread; 
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
        
        // TRANSMISSINGCOMMENT: Property UsesXAxisTitle
        public override bool UsesXAxisTitle 
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
                return false; 
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
        
        // TRANSMISSINGCOMMENT: Property UsesOrientation
        public override bool UsesOrientation 
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
        
        // TRANSMISSINGCOMMENT: Property IsNaturalOrientation
        public override bool IsNaturalOrientation 
        { 
            get 
            { 
                return Orientation == ChartOrientation.Horizontal; 
            } 
        } 
    } 
} 

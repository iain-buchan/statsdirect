using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class ScatterXYOptions
    [ Serializable ]
    public class ScatterXYOptions : GenericOptions 
    { 
        
        public bool PlotMarkers; 
        public bool IsAscii; 
        private readonly bool showLegendIsRelevant; 
        
        public ScatterXYOptions( bool useColour, IList <Series>xSeries, bool useLines ) : base( useColour ) 
        { 
            
            PlotMarkers = true; 
            
            MarkerTypes = new List<MarkerType>(); 
            for ( int i=0; i <= xSeries.Count - 1; i++ ) 
            { 
                int mkr = SeriesNumberToMarkerNumber( i ); 
                MarkerType markerType = ChartRenderer.MarkerTypes[ mkr ].Clone(); 
                markerType.MarkerSize = 6; 
                MarkerTypes.Add( markerType ); 
                
                //  A scatter plot has series with no lines.
                SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
                                                                               {
                                                                                   SeriesName = xSeries[i].Title,
                                                                                   AllowChangeToDashStyle = useLines,
                                                                                   AllowChangeToLineThickness = useLines,
                                                                                   MarkerIndex = i
                                                                               };
                SeriesOptions.Add( soleOptions ); 
            } 
            showLegendIsRelevant = xSeries.Count > 1; 
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
        
        // TRANSMISSINGCOMMENT: Property UsesYAxisTitle
        public override bool UsesYAxisTitle 
        { 
            get 
            { 
                return true; 
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
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.ScatterXY; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property ShowScatterXYOptions
        public override bool ShowScatterXYOptions 
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
                return showLegendIsRelevant; 
            } 
        } 
    } 
    
    
} 

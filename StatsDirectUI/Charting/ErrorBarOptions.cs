using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class ErrorBarOptions
    [ Serializable ]
    public class ErrorBarOptions : GenericOptions 
    { 
        
        public bool PlotMarkers; 
        public Data.DataFrame ydat; 
        public Data.DataFrame xdat; 
        public Data.DataFrame ydatl; 
        public Data.DataFrame ydatu; 
        public bool JoinMarkersWithLines; 
        
        public ErrorBarOptions( bool useColour ) : base( useColour ) 
        { 
            
        } 
        
        // TRANSMISSINGCOMMENT: Method SetMarkers
        public void SetMarkers() 
        { 
            MarkerTypes = new List<MarkerType>(); 
            for ( int i=0; i <= ydat.VariableCount - 1; i++ ) 
            { 
                int mkr = SeriesNumberToMarkerNumber( i ); 
                MarkerType markerType = ChartRenderer.MarkerTypes[ mkr ].Clone(); 
                markerType.MarkerSize = 6; 
                MarkerTypes.Add( markerType ); 
                
                //  An error plot has series with possible lines.
                SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
                                                          {
                                                              SeriesName = ydat.Variables[i].Title,
                                                              AllowChangeToDashStyle = true,
                                                              AllowChangeToLineThickness = true,
                                                              MarkerIndex = i
                                                          };
                SeriesOptions.Add( soleOptions ); 
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
        
        // TRANSMISSINGCOMMENT: Property UsesBoxAxes
        public override bool UsesBoxAxes 
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
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.ErrorBars; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property ShowErrorBarOptions
        public override bool ShowErrorBarOptions 
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
                return ydat.VariableCount > 1; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesLegendFontDescriptor
        public override bool UsesLegendFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
    } 
    
    
} 

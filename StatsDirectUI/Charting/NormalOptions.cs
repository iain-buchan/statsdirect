using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class NormalOptions : GenericOptions 
    { 
        
        public enum ScoreMethod 
        { 
            VanDerWaerden = 1,
            Blom = 2,
            ExpectedNormalOrder = 3,
        } 
        
        
        public ScoreMethod Method; 
        ///  <summary>
        ///  If true, show z scores as z * SD + mean, where mean and SD are the mean and standard deviation of the observed/input values and z are the normal scores.
        ///  If false, show z scores as z.
        ///  </summary>
        ///  <remarks></remarks>
        public bool Scaling; 
        
        public NormalOptions( bool UseColour ) : base( UseColour ) 
        { 
            
            //  A normal plot's marker is derived from the first series
            MarkerTypes = new List<MarkerType>(); 
            MarkerType markerType = ChartRenderer.MarkerTypes[ 0 ].Clone(); 
            markerType.MarkerSize = 6; 
            MarkerTypes.Add( markerType ); 
            
            //  A normal plot has a single series with no lines.
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
        public override ChartOptionType OptionType 
        { 
            get 
            { 
                return ChartOptionType.Normal; 
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
        
        // TRANSMISSINGCOMMENT: Property ShowNormalOptions
        public override bool ShowNormalOptions 
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

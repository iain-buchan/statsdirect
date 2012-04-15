using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class BarOptions : GenericOptions 
    { 
        
        private double _MaxBarWidth = 0.5; 
        
        public BarOptions( bool UseColour ) : base( UseColour )
        {
            RotateWhenStacked = true;
        } 
        
        ///  <summary>
        ///  The widest a bar may be, as a fraction of its containing space.
        ///  </summary>
        public double MaxBarWidth 
        { 
            get 
            { 
                return _MaxBarWidth; 
            } 
            set 
            { 
                _MaxBarWidth = value; 
            } 
        } 
        
        
        ///  <summary>
        ///  If false, bars should be drawn side-by-side.  If true, bars should be drawn end-to-end.
        ///  </summary>
        public bool Stacked;

        /// <summary>
        /// If false, stacked bar charts should be drawn per Excel.  If true, they should be drawn per StatsDirect.
        /// </summary>
        public bool RotateWhenStacked { get; set; }

        ///  <summary>
        ///  If Stacked and true, bars should be drawn end-to-end scaled 0..1.  If Stacked and false, bars should be drawn end-to-end scaled to the largest bar.
        ///  If not Stacked, no effect.
        ///  </summary>
        public bool Stacked100Percent; 

        private bool _ShowLegendIsRelevant; 
        
        // TRANSMISSINGCOMMENT: Method SetMarkers
        public void SetMarkers( IList <Series>seriesToUse ) 
        { 
            //  Markers will be calculated automatically as required (though we need to force fills); we just need to set up the option descriptors.
            // ShouldForceIsFilled = True
            // ForcedIsFilled = True
            // ShouldForceFillStyle = True
            // ForcedFillStyle = FillStyle.None
            
            for ( int i=0; i <= seriesToUse.Count - 1; i++ ) 
            {
                SeriesOptionsDescriptor soleOptions = new SeriesOptionsDescriptor
                                                          {
                                                              SeriesName = seriesToUse[i].Title,
                                                              AllowChangeToMarkerSize = false,
                                                              AllowChangeToMarkerType = false,
                                                              AllowChangeToDashStyle = true,
                                                              AllowChangeToLineThickness = true,
                                                              AllowChangeToFill = true,
                                                              MarkerIndex = i
                                                          };
                SeriesOptions.Add( soleOptions ); 
            } 
            _ShowLegendIsRelevant = seriesToUse.Count > 1; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property UsesChartTitle
        public override bool UsesChartTitle 
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
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.Bar; 
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
        
        // TRANSMISSINGCOMMENT: Property ShowBarOptions
        public override bool ShowBarOptions 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property OrientationLabel
        public override string OrientationLabel 
        { 
            get 
            { 
                return "Bar orientation"; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property ShowLegendIsRelevant
        public override bool ShowLegendIsRelevant 
        { 
            get 
            { 
                return _ShowLegendIsRelevant; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property IsNaturalOrientation
        public override bool IsNaturalOrientation 
        { 
            get 
            { 
                return Orientation == ChartOrientation.Vertical; 
            } 
        } 
    } 
    
    
} 

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
            MarkerType leftHandMarkerType = ChartPreferences.MarkerTypes[ 0 ].Clone(); 
            MarkerType rightHandMarkerType = ChartPreferences.MarkerTypes[ 1 ].Clone(); 
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
                AllowChangeToLineColour = false,
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
                AllowChangeToLineColour = false,
                MarkerIndex = 1
            };
            SeriesOptions.Add( rightHandOptions ); 
        } 
        
        public override bool UsesAutoscale 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesBoxAxes 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesChartTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesSeriesLabels 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesYAxisTitle 
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
        
        public override bool UsesAxisLabelFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool ShowLegendIsRelevant 
        { 
            get 
            { 
                return false; 
            } 
        }

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
} 

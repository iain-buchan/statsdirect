using System;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class GiniOptions
    [ Serializable ]
    public class GiniOptions : GenericOptions 
    { 
        public GiniOptions( bool useColour ) : base( useColour ) 
        { 
            
        } 
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override ChartOptionType OptionType 
        { 
            get 
            { 
                return ChartOptionType.Gini; 
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

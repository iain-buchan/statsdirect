using System;

using StatsDirect.Numerics;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class ControlOptions
    [ Serializable ]
    public class ControlOptions : GenericOptions 
    { 
        
        public bool UseDates; 
        public bool UseMean; 
        public bool Use1SD; 
        public bool Use2SD; 
        public bool Use3SD; 
        public bool HasUserSpecifiedMeanAndSD; 
        public double UserSpecifiedMean; 
        public double UserSpecifiedSD; 
        public bool HasUserSpecifiedLimits; 
        public double LowerWarningLimit; 
        public double UpperWarningLimit; 
        public double LowerControlLimit; 
        public double UpperControlLimit; 
        public int ObservationsToUse; 
        public int RightHandDecimalPlaces; 
        
        public ControlOptions( bool UseColour ) : base( UseColour ) 
        { 
            
            LowerControlLimit = Constant.MISSING; 
            LowerWarningLimit = Constant.MISSING; 
            UpperControlLimit = Constant.MISSING; 
            UpperWarningLimit = Constant.MISSING; 
        } 
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.Control; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property ShowControlOptions
        public override bool ShowControlOptions 
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
        
        // TRANSMISSINGCOMMENT: Property UsesLegendFontDescriptor
        public override bool UsesLegendFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property LegendFontLabel
        public override string LegendFontLabel 
        { 
            get 
            { 
                return "Control Label"; 
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

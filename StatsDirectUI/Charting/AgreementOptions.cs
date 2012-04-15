using System;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class AgreementOptions : GenericOptions 
    { 
        
        public AgreementOptions( bool useColour ) : base( useColour ) 
        { 
            
        } 
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.Agreement; 
            } 
        } 
        
        public double[] av; 
        public double[] mxd; 
        public double lla; 
        public double ula; 
        public double mean; 
        public double P0; 
        public bool HasLimits; 
        
        // TRANSMISSINGCOMMENT: Property ShowLegendIsRelevant
        public override bool ShowLegendIsRelevant 
        { 
            get 
            { 
                return false; 
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
    } 
    
    
} 

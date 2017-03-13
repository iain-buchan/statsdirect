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
        
        public override bool UsesChartTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesXAxisTitle 
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

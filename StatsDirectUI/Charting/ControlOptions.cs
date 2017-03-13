using System;

using StatsDirect.Numerics;

namespace StatsDirect.Charting
{
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
        
        public override bool ShowControlOptions 
        { 
            get 
            { 
                return true; 
            } 
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
        
        public override bool UsesAxisLabelFontDescriptor 
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
        
        public override bool UsesLegendFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override string LegendFontLabel 
        { 
            get 
            { 
                return "Control Label"; 
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

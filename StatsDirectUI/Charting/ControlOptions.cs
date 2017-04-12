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
        
        public override bool ShowControlOptions => true;

        public override bool UsesAutoscale => true;

        public override bool UsesBoxAxes => true;

        public override bool UsesChartTitle => true;

        public override bool UsesXAxisTitle => true;

        public override bool UsesYAxisTitle => true;

        public override bool UsesAxisLabelFontDescriptor => true;

        public override bool UsesAxisTitleFontDescriptor => true;

        public override bool UsesLegendFontDescriptor => true;

        public override string LegendFontLabel => "Control Label";

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
} 

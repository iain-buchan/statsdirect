using System;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class LinearRegressionOptions : ChartOptions 
    { 
        
        public double Slope; 
        public double Intercept; 
        public bool FullWidth; 
        
        public LinearRegressionOptions( bool useColour ) : base( useColour ) 
        { 
            
        } 
        
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
} 

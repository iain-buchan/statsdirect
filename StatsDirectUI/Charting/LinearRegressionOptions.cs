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
        
        public override ChartOptionType OptionType 
        { 
            get 
            { 
                return ChartOptionType.LinearRegression; 
            } 
        } 
        
        public override bool ShowLegendIsRelevant 
        { 
            get 
            { 
                return false; 
            } 
        } 
    } 
    
    
} 

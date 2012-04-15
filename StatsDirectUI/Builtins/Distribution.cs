using StatsDirect.Templates; 

namespace StatsDirect.Builtins
{
    public class Distribution  
    { 
        public static StepResult DistNormal( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 0); 
        } 
        
        
        public static StepResult DistT( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 1); 
        } 
        
        
        public static StepResult DistF( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 2); 
        } 
        
        
        public static StepResult DistChiSquare( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 3); 
        } 
        
        
        public static StepResult DistQ( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 4); 
        } 
        
        
        public static StepResult DistBinomial( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 5); 
        } 
        
        
        public static StepResult DistPoisson( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 6); 
        } 
        
        
        public static StepResult DistKendall( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 7); 
        } 
        
        
        public static StepResult DistSpearman( ITemplateHost host, ParameterBag parameters ) 
        {
            return DistributionOf(host, parameters, 8); 
        } 
        
        
        public static StepResult DistNonCentralT( ITemplateHost host, ParameterBag parameters ) 
        { 
            return DistributionOf( host, parameters, 9 ); 
        }


        private static StepResult DistributionOf(ITemplateHost host, ParameterBag parameters, int selectedText)
        {
            DistributionOptions distributionOptions = new DistributionOptions { SelectedTest = selectedText };
            host.Amend(distributionOptions, parameters);
            ParameterBag outputParameters = new ParameterBag(); 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
    }

    public class DistributionOptions : IFillable
    {
        public int SelectedTest { get; set; }
        public string FillerToUse
        {
            get { return "Distribution"; }
        }
    }
} 

namespace StatsDirect.Charting
{
    // TODO: Remove requirements for this module entirely
    static public class SDGlobalStub 
    { 
        
        public static string DECP_CHAR = System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator; 
        
        // largest relative spacing of doubles = B**(-MACHEP)
        public const double EPSNEG = 0.000000000000000111022302462516; 
        // smallest relative spacing of doubles = B**(-D)
        public const double EPSILON = 0.000000000000000222044604925031; 
        
    } 
} 

namespace StatsDirect.Charting
{
    public class StringSeries : Series 
    { 
        
        public string[] Data; 
        
        public StringSeries() 
        { 
        } 
        
        public StringSeries( int dataLength ) 
        { 
            Data = new string[ dataLength ]; 
        } 
        
        public int Length 
        { 
            get 
            { 
                return Data.Length; 
            } 
        } 
        
        public override StringSeries AsStringSeries 
        { 
            get 
            { 
                return this; 
            } 
        } 
    } 
} 

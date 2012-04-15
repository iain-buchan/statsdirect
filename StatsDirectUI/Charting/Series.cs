using System;

namespace StatsDirect.Charting
{
    public class Series  
    { 
        public string Title; 
        
        public virtual DoubleSeries AsDoubleSeries 
        { 
            get 
            { 
                throw new Exception( "Attempt to cast a non-Double Series to a DoubleSeries" ); 
            } 
        } 
        
        public virtual StringSeries AsStringSeries 
        { 
            get 
            { 
                throw new Exception( "Attempt to cast a non-String Series to a StringSeries" ); 
            } 
        } 
    } 
} 

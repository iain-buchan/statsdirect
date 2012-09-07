using System;

namespace StatsDirect.Charting
{
    public class Series  
    {
        public string Title { get; set; }

        public virtual DoubleSeries AsDoubleSeries 
        { 
            get 
            { 
                throw new InvalidOperationException( "Attempt to cast a non-Double Series to a DoubleSeries" ); 
            } 
        } 
        
        public virtual StringSeries AsStringSeries 
        { 
            get 
            {
                throw new InvalidOperationException("Attempt to cast a non-String Series to a StringSeries"); 
            } 
        } 
    } 
} 

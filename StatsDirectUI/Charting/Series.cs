using System;

namespace StatsDirect.Charting
{
    public abstract class Series  
    {
        public string Title { get; set; }

        public virtual DoubleSeries AsDoubleSeries 
        { 
            get 
            { 
                throw new InvalidOperationException( "Attempt to cast a non-Double Series to a DoubleSeries" ); 
            } 
        }

        public virtual MultiDoubleSeries AsMultiDoubleSeries
        {
            get
            {
                throw new InvalidOperationException("Attempt to cast a non-MultiDouble Series to a MultiDoubleSeries");
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

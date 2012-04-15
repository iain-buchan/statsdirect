using System;

namespace StatsDirect.Data
{
    // TRANSMISSINGCOMMENT: Class Group
    [ Serializable ]
    public class Group  
    { 
        private string _label; 
        private double _id; 
        private int _nBin; 
        
        public Group( string Name, double Id ) 
        { 
            _label = Name; 
            _id = Id; 
        } 
        
        // TRANSMISSINGCOMMENT: Property Label
        public string Label 
        { 
            get 
            { 
                return _label; 
            } 
            set 
            { 
                _label = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property Id
        public double Id 
        { 
            get 
            { 
                return _id; 
            } 
            set 
            { 
                _id = value; 
            } 
        } 
        
        ///  <summary>
        ///  The number of members of this group
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public int NBin 
        { 
            get 
            { 
                return _nBin; 
            } 
            set 
            { 
                _nBin = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method ToString
        public override string ToString() 
        { 
            return "Group " + Id + ": " + Label; 
        } 
        
    } 
    
    
} 

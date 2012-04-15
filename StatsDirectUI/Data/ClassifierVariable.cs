using System.Xml.Serialization; 
using System.Text; 
using System;
using System.Collections.Generic;

namespace StatsDirect.Data
{
    // TRANSMISSINGCOMMENT: Class ClassifierVariable
    [ Serializable ]
    public class ClassifierVariable : DoubleVariable 
    { 
        
        private IList<Group> _groups; 
        
        public ClassifierVariable() 
        { 
            _groups = new List<Group>(); 
        } 
        
        // TRANSMISSINGCOMMENT: Property IsClassifier
        public override bool IsClassifier 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method SameSizeForResults
        public override Variable SameSizeForResults() 
        { 
            Variable newVariable = new DoubleVariable(); 
            newVariable.EnsureLength( Length ); 
            return newVariable; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method StealDataFrom
        public override void StealDataFrom( Variable victim ) 
        { 
            if ( !( victim.IsClassifier ) ) 
            { 
                throw new InvalidCastException( "Victim must be of the same type when stealing variables" ); 
            } 
            base.StealDataFrom( victim ); 
            ClassifierVariable cVictim = victim.AsClassifierVariable; 
            _groups = cVictim._groups; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property Groups
        [ XmlIgnore ]
        public IList <Group>Groups 
        { 
            get 
            { 
                return _groups; 
            } 
            set 
            { 
                _groups = value; 
            } 
        } 
        
        // TRANSWARNING: Automatically generated because of properties with parameter(s) 
        // TRANSMISSINGCOMMENT: Method get_Group
        public Group get_Group( int Index ) 
        { 
            return _groups[ Index ]; 
        } 
        
        // TRANSWARNING: Automatically generated because of properties with parameter(s) 
        // TRANSMISSINGCOMMENT: Method set_Group
        public void set_Group( int Index, Group value ) 
        { 
            EnsureGroups( Index + 1 ); 
            _groups[ Index ] = value; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property GroupCount
        public int GroupCount 
        { 
            get 
            { 
                return _groups.Count; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method EnsureGroups
        private void EnsureGroups( int MinimumSize ) 
        { 
            while ( _groups.Count < MinimumSize ) 
            { 
                _groups.Add( null ); 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property AsClassifierVariable
        public override ClassifierVariable AsClassifierVariable 
        { 
            get 
            { 
                return this; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property VariableType
        public override VariableType VariableType 
        { 
            get 
            { 
                return StatsDirect.Data.VariableType.ClassifierType; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedo
        public override object CopyAndStripForRedo( bool shouldKeepData ) 
        { 
            ClassifierVariable copy = new ClassifierVariable(); 
            CopyAndStripForRedoInto( copy, shouldKeepData ); 
            if ( Origin == null || shouldKeepData ) 
            { 
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy._groups = _groups; 
            } 
            return copy; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property CommaSeparatedCategoryNames
        public string CommaSeparatedCategoryNames 
        { 
            get 
            { 
                StringBuilder sb = new StringBuilder(); 
                bool first = true; 
                foreach ( Group g in Groups ) 
                { 
                    if ( first )
                    { 
                        first = false; 
                    } else { sb.Append( ", " ); } 
                    sb.Append( g.Label ); 
                }
                return sb.ToString(); 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property SortedCategoryNames
        public string[] SortedCategoryNames 
        { 
            get 
            { 
                List<string> n = new List<string>(); 
                foreach ( Group g in Groups ) 
                { 
                    n.Add( g.Label ); 
                }
                n.Sort(); 
                return n.ToArray(); 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property HasData
        protected override bool HasData 
        { 
            get 
            { 
                return base.HasData && _groups != null; 
            } 
        } 
    } 
    
    
} 

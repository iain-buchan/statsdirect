using System.Xml.Serialization; 
using System;
using System.Collections.Generic;
namespace StatsDirect.Data
{
    [ Serializable ]
    public class DataFrame : IStripForRedo
    { 
        private string _name; 
        private bool _isLong; 
        private IList<Variable> _variables; 
        
        public DataFrame() 
        { 
            _variables = new List<Variable>(); 
        } 
        
        public DataFrame( Variable v ) 
        { 
            _variables = new List<Variable> {v};
        } 
        
        public DataFrame( Variable v, string name ) 
        { 
            _variables = new List<Variable> {v};
            _name = name; 
        } 
        
        ///  <summary>
        ///  A name that can be used to identify the frame by the user
        ///  </summary>
        [ XmlElement( "name" ) ]
        public string Name 
        { 
            get 
            { 
                return _name; 
            } 
            set 
            { 
                _name = value; 
            } 
        } 
        
        [ XmlElement( "is-long" ) ]
        public bool IsLong 
        { 
            get 
            { 
                return _isLong; 
            } 
            set 
            { 
                _isLong = value; 
            } 
        } 
        
        [ XmlIgnore ]
        public bool IsWide 
        { 
            get 
            { 
                return !( IsLong ); 
            } 
            set 
            { 
                IsLong = !( value ); 
            } 
        } 
        
        [ XmlIgnore ]
        public IList <Variable>Variables 
        { 
            get 
            { 
                return _variables; 
            } 
            set 
            { 
                _variables = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property VariablesForXml
        [ XmlArray( "variables" ) ]
        [ XmlArrayItem( "classifier-variable", typeof( ClassifierVariable ) ) ]
        [ XmlArrayItem( "date-variable", typeof( DateVariable ) ) ]
        [ XmlArrayItem( "double-variable", typeof( DoubleVariable ) ) ]
        [ XmlArrayItem( "string-variable", typeof( StringVariable ) ) ]
        public Variable[] VariablesForXml 
        { 
            get 
            {
                Variable[] retval = new Variable[_variables.Count - 1 + 1 ]; 
                int i = 0; 
                foreach ( Variable v in _variables ) 
                { 
                    retval[ i ] = v; 
                    i += 1; 
                }
                return retval; 
            } 
            set 
            { 
                _variables.Clear(); 
                foreach ( Variable v in value ) 
                { 
                    _variables.Add( v ); 
                }
            } 
        } 
        
        // TRANSWARNING: Automatically generated because of properties with parameter(s) 
        // TRANSMISSINGCOMMENT: Method get_Variable
        public Variable get_Variable( int Index ) 
        { 
            return _variables[ Index ]; 
        } 
        
        // TRANSWARNING: Automatically generated because of properties with parameter(s) 
        // TRANSMISSINGCOMMENT: Method set_Variable
        public void set_Variable( int Index, Variable value ) 
        { 
            EnsureVariables( Index + 1 ); 
            _variables[ Index ] = value; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property VariableCount
        public int VariableCount 
        { 
            get 
            { 
                return _variables.Count; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method EnsureVariables
        public void EnsureVariables( int MinimumSize ) 
        { 
            while ( _variables.Count < MinimumSize ) 
            { 
                _variables.Add( null ); 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property MaxRows
        public int MaxRows 
        { 
            get 
            { 
                int maxLength = 0; 
                foreach ( Variable v in _variables ) 
                { 
                    maxLength = Math.Max( maxLength, v.Length ); 
                }
                return maxLength; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property MinRows
        public int MinRows 
        { 
            get 
            { 
                if ( _variables.Count == 0 ) 
                { 
                    return 0; 
                } 
                
                int minLength = int.MaxValue; 
                foreach ( Variable v in _variables ) 
                { 
                    minLength = Math.Min( minLength, v.Length ); 
                }
                return minLength; 
            } 
        } 
        
        ///  <summary>
        ///  Fill the specified target array as (row, column) from our own variables.
        ///  </summary>
        ///  <param name="Target"></param>
        ///  <remarks>The array is assumed to be large enough to hold all of our contents.  Non-double values in the source are represented as Double.NaN.</remarks>
        public void FillArray( double[,] Target ) 
        { 
            for ( int c=0; c <= _variables.Count - 1; c++ ) 
            { 
                DoubleVariable v = ( ( DoubleVariable )( _variables[ c ] ) ); 
                for ( int r=0; r <= v.Length - 1; r++ ) 
                { 
                    Target[ r, c ] = v.Data[r]; 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  Return a new DataFrame with the same number of variables, each of the same size, as this frame.
        ///  The new frame is not otherwise initialised - no names, no values.
        ///  </summary>
        ///  <returns>a new DataFrame with the same number of variables, each of the same size, as this frame</returns>
        public DataFrame SameSizeForResults() 
        { 
            DataFrame newFrame = new DataFrame(); 
            newFrame.EnsureVariables( VariableCount ); 
            foreach ( Variable v in Variables ) 
            { 
                newFrame.Variables.Add( v.SameSizeForResults() ); 
            }
            return newFrame; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method FindVariable
        public Variable FindVariable( string Title ) 
        { 
            foreach ( Variable v in _variables ) 
            { 
                if ( v.Title != null && v.Title.Equals( Title ) ) 
                { 
                    return v; 
                } 
            }
            return null; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedo
        public object CopyAndStripForRedo( bool shouldKeepData ) 
        {
            DataFrame copy = new DataFrame {_isLong = _isLong, _name = _name};
            foreach ( Variable v in _variables ) 
                copy._variables.Add( ( ( Variable )( v.CopyAndStripForRedo( shouldKeepData ) ) ) ); 
            return copy; 
        } 

        object IStripForRedo.CopyAndStripForRedo( bool shouldKeepData )
        { 
            return CopyAndStripForRedo( shouldKeepData );
        }
        
        public void RefillForRedo( IRefillSource refillSource ) 
        { 
            foreach ( Variable v in _variables ) 
            { 
                v.RefillForRedo( refillSource ); 
            }
        } 

        void IStripForRedo.RefillForRedo( IRefillSource refillSource )
        { 
            RefillForRedo( refillSource );
        }
    } 
} 

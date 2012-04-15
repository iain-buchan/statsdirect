using System;
using StatsDirect.Utilities; 

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single string-based non-classifier variable/factor/column/field.
    ///  Use ClassiferVariable to represent a classifier variable.
    ///  </summary>
    [Serializable]
    public class StringVariable : Variable 
    { 
        private string[] _data; 
        
        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public string[] Data 
        { 
            get 
            { 
                return _data; 
            } 
            set 
            { 
                _data = value; 
            } 
        } 
        
        public StringVariable( int length, string title ) 
        { 
            EnsureLength( length ); 
            Title = title; 
        } 
        
        public StringVariable( string[] Data ) 
        { 
            _data = Data; 
        } 
        
        public StringVariable( string[] Data, string title ) 
        { 
            _data = Data; 
            Title = title; 
        } 
        
        public StringVariable() 
        { 
            //  Nothing else required
        } 
        
        ///  <summary>
        ///  Access a single element of the data array
        ///  </summary>
        ///  <param name="index">The element to access</param>
        ///  <param name="value">The new value to set. Storage management is done internally, so the array is always sufficently large to hold the value</param>
        ///  <returns>The value at the specified index, or an exception if the index is out of bounds</returns>
        ///  <remarks></remarks>
        public void set_Data(int index, string value) 
        { 
            EnsureLength( index + 1 ); 
            _data[ index ] = value; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property Length
        public override int Length 
        { 
            get
            {
                return ( _data == null ) ? 0 : _data.Length;
            }
        } 
        
        // TRANSMISSINGCOMMENT: Method EnsureLength
        public override void EnsureLength( int minimumLength ) 
        { 
            if ( ( _data == null ) ) 
            { 
                _data = new string[ minimumLength ]; 
            } 
            else 
            { 
                if ( _data.Length < minimumLength ) 
                { 
                    string[] transTemp0 = new string[ minimumLength ]; 
                    Array.Copy( _data, transTemp0, _data.Length ); 
                    _data = transTemp0; 
                } 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method EnsureLength
        public override void EnsureLength( int minimumLength, bool useMissing ) 
        { 
            int currentLength; 
            if ( ( _data == null ) ) 
            { 
                currentLength = 0; 
                _data = new string[ minimumLength ]; 
            } 
            else 
            { 
                currentLength = _data.Length; 
                if ( _data.Length < minimumLength ) 
                { 
                    string[] transTemp1 = new string[ minimumLength ]; 
                    Array.Copy( _data, transTemp1, _data.Length ); 
                    _data = transTemp1; 
                } 
            } 
            if ( useMissing ) 
            { 
                for ( int i=currentLength; i <= _data.Length - 1; i++ ) 
                { 
                    _data[ i ] = Formatting.ASTERISK; 
                } 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method TruncateDataToLength
        public override void TruncateDataToLength( int maximumLength ) 
        { 
            if ( ( _data.Length > maximumLength ) ) 
            { 
                string[] transTemp2 = new string[ maximumLength ]; 
                Array.Copy( _data, transTemp2, maximumLength ); 
                _data = transTemp2; 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method SameSizeForResults
        public override Variable SameSizeForResults() 
        { 
            Variable newVariable = new StringVariable(); 
            newVariable.EnsureLength( Length ); 
            return newVariable; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property AsStringVariable
        public override StringVariable AsStringVariable 
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
                return StatsDirect.Data.VariableType.StringType; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedo
        public override object CopyAndStripForRedo( bool shouldKeepData ) 
        { 
            StringVariable copy = new StringVariable(); 
            CopyAndStripForRedoInto( copy, shouldKeepData ); 
            return copy; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedoInto
        protected void CopyAndStripForRedoInto( StringVariable copy, bool ShouldKeepData ) 
        { 
            base.CopyAndStripForRedoInto( copy, ShouldKeepData ); 
            if ( Origin == null || ShouldKeepData ) 
            { 
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy._data = _data; 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method StealDataFrom
        public override void StealDataFrom( Variable victim ) 
        { 
            if ( !( victim.IsStringVariable ) ) 
            { 
                throw new InvalidCastException( "Victim must be of the same type when stealing variables" ); 
            } 
            _data = victim.AsStringVariable._data; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property IsStringVariable
        public override bool IsStringVariable 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property HasData
        protected override bool HasData 
        { 
            get 
            { 
                return _data != null; 
            } 
        } 
    } 
} 

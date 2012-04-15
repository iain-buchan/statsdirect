using System;
namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single non-classifier variable/factor/column/field.
    ///  Use ClassiferVariable to represent a classifier variable.
    ///  </summary>
    [Serializable]
    public class DateVariable : Variable 
    { 
        private DateTime[] _data; 
        
        public DateVariable() 
        { 
            //  Do nothing
        } 
        
        public DateVariable( DateTime[] data ) 
        { 
            _data = data; 
        } 
        
        public DateVariable( DateTime[] data, string title ) 
        { 
            _data = data; 
            Title = title; 
        } 
        
        public DateVariable( int length, string title ) 
        { 
            EnsureLength( length ); 
            Title = title; 
        } 
        
        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public DateTime[] Data 
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
        
        ///  <summary>
        ///  Access a single element of the data array
        ///  </summary>
        ///  <param name="index">The element to access</param>
        ///  <param name="value">The new value to set. Storage management is done internally, so the array is always sufficently large to hold the value</param>
        ///  <returns>The value at the specified index, or an exception if the index is out of bounds</returns>
        ///  <remarks></remarks>
        public void set_Data( int index, DateTime value ) 
        { 
            EnsureLength( index + 1 ); 
            _data[ index ] = value; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property Length
        public override int Length 
        { 
            get {
                return ( _data == null ) ? 0 : _data.Length;
            }
        } 
        
        // TRANSMISSINGCOMMENT: Method EnsureLength
        public override void EnsureLength( int minimumLength ) 
        { 
            if ( ( _data == null ) ) 
            { 
                _data = new DateTime[ minimumLength ]; 
            } 
            else 
            { 
                if ( _data.Length < minimumLength ) 
                { 
                    DateTime[] transTemp0 = new DateTime[ minimumLength ]; 
                    Array.Copy( _data, transTemp0, _data.Length ); 
                    _data = transTemp0; 
                } 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method EnsureLength
        public void EnsureLength( int minimumLength, DateTime fillValue ) 
        { 
            if ( ( _data == null ) ) 
            { 
                _data = new DateTime[ minimumLength ]; 
                for ( int i=0; i < minimumLength; i++ ) 
                { 
                    _data[ i ] = fillValue; 
                } 
            } 
            else 
            { 
                if ( _data.Length < minimumLength ) 
                { 
                    int oldLength = _data.Length; 
                    DateTime[] transTemp1 = new DateTime[ minimumLength ]; 
                    Array.Copy( _data, transTemp1, _data.Length ); 
                    _data = transTemp1; 
                    for ( int i=oldLength; i < minimumLength; i++ ) 
                    { 
                        _data[ i ] = fillValue; 
                    } 
                } 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method EnsureLength
        public override void EnsureLength( int minimumLength, bool useMissing ) 
        { 
            if ( useMissing ) 
            { 
                EnsureLength( minimumLength, DateTime.MinValue ); 
            } 
            else 
            { 
                EnsureLength( minimumLength ); 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method TruncateDataToLength
        public override void TruncateDataToLength( int maximumLength ) 
        { 
            if ( ( _data.Length > maximumLength ) ) 
            { 
                DateTime[] transTemp2 = new DateTime[ maximumLength ]; 
                Array.Copy( _data, transTemp2, Math.Min( _data.Length, transTemp2.Length ) ); 
                _data = transTemp2; 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method SameSizeForResults
        public override Variable SameSizeForResults() 
        { 
            Variable newVariable = new DoubleVariable(); 
            newVariable.EnsureLength( Length ); 
            return newVariable; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property AsDateVariable
        public override DateVariable AsDateVariable 
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
                return StatsDirect.Data.VariableType.DateType; 
            } 
        } 
        
        ///  <summary>
        ///  True if this Variable is a date variable; false if it is not
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public override bool IsDateVariable 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedo
        public override object CopyAndStripForRedo( bool shouldKeepData ) 
        { 
            DateVariable copy = new DateVariable(); 
            CopyAndStripForRedoInto( copy, shouldKeepData ); 
            return copy; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedoInto
        protected void CopyAndStripForRedoInto( DateVariable copy, bool ShouldKeepData ) 
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
            if ( !( victim.IsDateVariable ) ) 
            { 
                throw new InvalidCastException( "Victim must be of the same type when stealing variables" ); 
            } 
            _data = victim.AsDateVariable._data; 
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

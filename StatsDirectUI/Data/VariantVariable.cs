using System;
using StatsDirect.Utilities; 

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single variant variable/factor/column/field.
    ///  </summary>
    [Serializable]
    public class VariantVariable : Variable 
    { 
        private object[] _data; 
        
        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public object[] Data 
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
        
        public VariantVariable( int length, string title ) 
        { 
            EnsureLength( length ); 
            Title = title; 
        } 
        
        public VariantVariable( object[] Data ) 
        { 
            _data = Data; 
        }

        public VariantVariable(object[] Data, string title) 
        { 
            _data = Data; 
            Title = title; 
        }

        public VariantVariable() 
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
        public void set_Data(int index, object value) 
        { 
            EnsureLength( index + 1 ); 
            _data[ index ] = value; 
        } 
        
        public override int Length 
        { 
            get
            {
                return ( _data == null ) ? 0 : _data.Length;
            }
        } 
        
        public override void EnsureLength( int minimumLength ) 
        { 
            if ( ( _data == null ) ) 
            {
                _data = new object[minimumLength]; 
            } 
            else 
            { 
                if ( _data.Length < minimumLength ) 
                {
                    object[] transTemp0 = new object[minimumLength]; 
                    Array.Copy( _data, transTemp0, _data.Length ); 
                    _data = transTemp0; 
                } 
            } 
        } 
        
        public override void EnsureLength( int minimumLength, bool useMissing ) 
        { 
            int currentLength; 
            if ( ( _data == null ) ) 
            { 
                currentLength = 0;
                _data = new object[minimumLength]; 
            } 
            else 
            { 
                currentLength = _data.Length; 
                if ( _data.Length < minimumLength ) 
                {
                    object[] transTemp1 = new object[minimumLength]; 
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
        
        public override void TruncateDataToLength( int maximumLength ) 
        { 
            if ( ( _data.Length > maximumLength ) ) 
            {
                object[] transTemp2 = new object[maximumLength]; 
                Array.Copy( _data, transTemp2, maximumLength ); 
                _data = transTemp2; 
            } 
        } 
        
        public override Variable SameSizeForResults() 
        { 
            Variable newVariable = new VariantVariable(); 
            newVariable.EnsureLength( Length ); 
            return newVariable; 
        } 
        
        public override VariantVariable AsVariantVariable 
        { 
            get 
            { 
                return this; 
            } 
        } 
        
        public override VariableType VariableType 
        { 
            get 
            { 
                return StatsDirect.Data.VariableType.Variant; 
            } 
        } 
        
        public override object CopyAndStripForRedo( bool shouldKeepData ) 
        { 
            VariantVariable copy = new VariantVariable(); 
            CopyAndStripForRedoInto( copy, shouldKeepData ); 
            return copy; 
        } 
        
        protected void CopyAndStripForRedoInto( VariantVariable copy, bool shouldKeepData ) 
        { 
            base.CopyAndStripForRedoInto( copy, shouldKeepData ); 
            if ( Origin == null || shouldKeepData ) 
            { 
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy._data = _data; 
            } 
        } 
        
        public override void StealDataFrom( Variable victim ) 
        { 
            if ( !( victim.IsVariantVariable ) ) 
            { 
                throw new InvalidCastException( "Victim must be of the same type when stealing variables" ); 
            } 
            _data = victim.AsVariantVariable._data; 
        } 
        
        public override bool IsVariantVariable 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        protected override bool HasData 
        { 
            get 
            { 
                return _data != null; 
            } 
        } 
    } 
} 

using System;
using StatsDirect.Numerics;

namespace StatsDirect.Data
{
    ///  <summary>
    ///  Represents a single non-classifier variable/factor/column/field.
    ///  Use ClassiferVariable to represent a classifier variable.
    ///  </summary>
    [Serializable]
    public class DoubleVariable : Variable 
    { 
        private double[] _data; 
        private double _sum; 
        private double _min; 
        private double _max; 
        private bool _hasMinMax; 
        
        public DoubleVariable() 
        { 
            //  Do nothing
        } 
        
        public DoubleVariable( double[] data ) 
        { 
            _data = data; 
        } 
        
        public DoubleVariable( double[] data, string title ) 
        { 
            _data = data; 
            Title = title; 
        } 
        
        public DoubleVariable( int Length, string title ) 
        { 
            EnsureLength( Length ); 
            Title = title; 
        } 
        
        public double Sum 
        { 
            get 
            { 
                return _sum; 
            } 
            set 
            { 
                _sum = value; 
            } 
        } 
        
        public double Min 
        { 
            get 
            { 
                if ( !( _hasMinMax ) ) 
                { 
                    CalculateMinMax(); 
                } 
                return _min; 
            } 
        } 
        
        public double Max 
        { 
            get 
            { 
                if ( !( _hasMinMax ) ) 
                { 
                    CalculateMinMax(); 
                } 
                return _max; 
            } 
        } 
        
        private void CalculateMinMax() 
        { 
            double mn = double.MaxValue; 
            double mx = double.MinValue; 
            for ( int i=_data.GetLowerBound( 0 ); i <= _data.GetUpperBound( 0 ); i++ ) 
            { 
                double d = _data[ i ]; 
                if ( d < mn )
                { 
                    mn = d; 
                } 
                if ( d > mx )
                { 
                    mx = d; 
                } 
            } 
            _min = mn; 
            _max = mx; 
            _hasMinMax = true; 
        } 
        
        
        ///  <summary>
        ///  Manage the entire data array at one time
        ///  </summary>
        ///  <value>The new data array to set</value>
        ///  <returns>The current data array</returns>
        ///  <remarks></remarks>
        public double[] Data 
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
        public void set_Data( int index, double value ) 
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
                _data = new double[ minimumLength ]; 
            } 
            else 
            { 
                if ( _data.Length < minimumLength ) 
                { 
                    double[] transTemp0 = new double[ minimumLength ]; 
                    Array.Copy( _data, transTemp0, _data.Length ); 
                    _data = transTemp0; 
                } 
            } 
        } 
        
        public void EnsureLength( int minimumLength, double fillValue ) 
        { 
            if ( ( _data == null ) ) 
            { 
                _data = new double[ minimumLength ]; 
                for ( int i=0; i <= minimumLength - 1; i++ ) 
                { 
                    _data[ i ] = fillValue; 
                } 
            } 
            else 
            { 
                if ( _data.Length < minimumLength ) 
                { 
                    int oldLength = _data.Length; 
                    double[] transTemp1 = new double[ minimumLength ]; 
                    Array.Copy( _data, transTemp1, _data.Length ); 
                    _data = transTemp1; 
                    for ( int i=oldLength; i <= minimumLength - 1; i++ ) 
                    { 
                        _data[ i ] = fillValue; 
                    } 
                } 
            } 
        } 
        
        public override void EnsureLength( int minimumLength, bool useMissing ) 
        { 
            if ( useMissing ) 
            { 
                EnsureLength( minimumLength, Constant.MISSING ); 
            } 
            else 
            { 
                EnsureLength( minimumLength ); 
            } 
        } 
        
        public override void TruncateDataToLength( int maximumLength ) 
        { 
            if ( ( _data.Length > maximumLength ) ) 
            { 
                double[] transTemp2 = new double[ maximumLength ]; 
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
        
        
        // TRANSMISSINGCOMMENT: Property AsDoubleVariable
        public override DoubleVariable AsDoubleVariable 
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
                return StatsDirect.Data.VariableType.DoubleType; 
            } 
        } 
        
        ///  <summary>
        ///  True if this Variable is a classifier variable; false if it is not
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public override bool IsDoubleVariable 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method StealDataFrom
        public override void StealDataFrom( Variable victim ) 
        { 
            if ( !( victim.IsDoubleVariable ) ) 
            { 
                throw new InvalidCastException( "Victim must be of the same type when stealing variables" ); 
            } 
            _data = victim.AsDoubleVariable._data; 
            _hasMinMax = false; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedo
        public override object CopyAndStripForRedo( bool shouldKeepData ) 
        { 
            DoubleVariable copy = new DoubleVariable(); 
            CopyAndStripForRedoInto( copy, shouldKeepData ); 
            return copy; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method CopyAndStripForRedoInto
        protected void CopyAndStripForRedoInto( DoubleVariable copy, bool shouldKeepData ) 
        { 
            base.CopyAndStripForRedoInto( copy, shouldKeepData ); 
            if ( Origin == null || shouldKeepData ) 
            { 
                //  Note: This is deliberately a shallow copy for speed.  It does mean that callers should not alter anything in copy's data, though.
                copy._data = _data; 
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

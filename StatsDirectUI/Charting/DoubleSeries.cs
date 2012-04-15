using System;

using StatsDirect.Numerics;
using System.Drawing;

namespace StatsDirect.Charting
{
    public class DoubleSeries : Series 
    { 
        
        public double[] Data; 
        // internal double[] LL; 
        // internal double[] UL; 
        // internal string[] Labels; 
        
        //  Similar to markers
        internal Pen UnstyledPen; 
        internal Pen StyledPen; 
        internal MarkerShape Shape; 
        internal bool IsFilled; 
        internal double MarkerSize; 
        internal System.Drawing.Drawing2D.DashStyle Style; 
        internal FillStyle FillStyle; 
        
        private bool hasSum; 
        private double sum; 
        private bool hasStdDev; 
        private double stdDev; 
        private double min; 
        private double max; 
        private bool hasMinMax; 
        
        public DoubleSeries() 
        { 
            //  Do nothing; this is only here because we also have a custom constructor
        } 
        
        public DoubleSeries( double[] data, string title ) 
        { 
            Data = data; 
            Title = title; 
        } 
        
        // TRANSMISSINGCOMMENT: Property Points
        public int Points 
        { 
            get 
            { 
                return Data.Length; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property Sum
        public double Sum 
        { 
            get 
            { 
                if ( !( hasSum ) ) 
                { 
                    double s = 0.0; 
                    for ( int i=Data.GetLowerBound( 0 ); i <= Data.GetUpperBound( 0 ); i++ ) 
                    { 
                        if ( Data[ i ] != Constant.MISSING ) 
                        { 
                            s += Data[ i ]; 
                        } 
                    } 
                    sum = s; 
                    hasSum = true; 
                } 
                return sum; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property StdDev
        public double StdDev 
        { 
            get 
            { 
                if ( !( hasStdDev ) ) 
                { 
                    
                    double avg = Sum / Convert.ToDouble( Points ); 
                    double ep = 0.0; double var = 0.0; 
                    for ( int C=Data.GetLowerBound( 0 ); C <= Data.GetUpperBound( 0 ); C++ ) 
                    { 
                        double s = Data[ C ] - avg; 
                        ep += s; 
                        var += s * s; 
                    } 
                    var = ( var - Math.Pow( ep, 2.0 ) / Convert.ToDouble( Points ) ) / Convert.ToDouble( Points - 1 ); 
                    stdDev = Math.Sqrt( var ); 
                    hasStdDev = true; 
                } 
                return stdDev; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property Min
        public double Min 
        { 
            get 
            { 
                if ( !( hasMinMax ) )
                { 
                    CalcMinMax(); 
                } 
                return min; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property Max
        public double Max 
        { 
            get 
            { 
                if ( !( hasMinMax ) )
                { 
                    CalcMinMax(); 
                } 
                return max; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method CalcMinMax
        private void CalcMinMax() 
        { 
            double mn = double.MaxValue; 
            double mx = double.MinValue; 
            for ( int i=Data.GetLowerBound( 0 ); i <= Data.GetUpperBound( 0 ); i++ ) 
            { 
                if ( Data[ i ] != Constant.MISSING ) 
                { 
                    if ( Data[ i ] < mn )
                    { 
                        mn = Data[ i ]; 
                    } 
                    if ( Data[ i ] > mx )
                    { 
                        mx = Data[ i ]; 
                    } 
                } 
            } 
            min = mn; 
            max = mx; 
            hasMinMax = true; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property AsDoubleSeries
        public override DoubleSeries AsDoubleSeries 
        { 
            get 
            { 
                return this; 
            } 
        } 
    } 
} 

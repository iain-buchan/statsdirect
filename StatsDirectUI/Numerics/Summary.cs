using StatsDirect.Utilities; 

using System;
using System.Collections.Generic;
namespace StatsDirect.Numerics
{
    // TRANSMISSINGCOMMENT: Class Summary
    public class Summary  
    { 
        public int ValidData; 
        public int MissingData; 
        public double sum; 
        public double mean; 
        public double Variance; 
        public double sd; 
        public double sem; 
        public double MeanLCL; 
        public double MeanUCL; 
        public double ConfidenceLevel; 
        public double GeometricMean; 
        public double Skewness; 
        public double Kurtosis; 
        public double VarianceCoefficient; 
        
        public double Maximum; 
        public double UpperQuartile; 
        public double median; 
        public double LowerQuartile; 
        public double Minimum; 
        public double UserCentileL; 
        public double UserCentileU; 
        public double Range; 
        public double WeightSum; 
        
        public string UserCentileLCaption; 
        public string UserCentileUCaption; 
        public string CLCaption; 
        public string title; 
        
        public int CentileType; 
        
        private struct VarAndWt 
        { 
            public double Data; 
            public double wt; 
        } 
        
        private VarAndWt[] xs; 
        
        private class VarAndWtByData : IComparer<VarAndWt>
        {
            private int Compare( VarAndWt x, VarAndWt y ) 
            { 
                //  First check TM
                if ( x.Data > y.Data ) 
                    return 1; 
                if ( x.Data < y.Data ) 
                    return -1; 

                //  If we get here, there are no meaningful differences
                return 0; 
            } 
            // interface methods implemented by Compare
            int IComparer<VarAndWt>.Compare( VarAndWt x, VarAndWt y )
            { 
                return Compare( x, y );
            }
            
        } 
        
        
        ///  <summary>
        ///  Univariate summary statistics with optional analytical weights
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="v"></param>
        ///  <param name="Start"></param>
        ///  <param name="finish"></param>
        ///  <param name="UserCL"></param>
        ///  <param name="UserCentL"></param>
        ///  <param name="UserCentU"></param>
        ///  <param name="NVSum"></param>
        ///  <returns></returns>
        ///  <remarks>see Gleason JR. Univariate summaries with boxplots. Stata Technical Bulletin sg67, 1997 and sg67.1, 1999.</remarks>
        private bool FullSummary( double[] x, double[] v, int Start, int finish, double UserCL, double UserCentL, double UserCentU, double NVSum ) 
        { 
            bool fullSummaryReturn;
            int i;
            
            
            // preparatory counting and feeder arrays
            bool doUserCentL;
            if (UserCentL > 0.0 & UserCentL < 100.0) 
            { 
                doUserCentL = true; 
                UserCentileLCaption = "Centile " + UserCentL.ToString(); 
            } 
            else 
            { 
                doUserCentL = false; 
                UserCentileLCaption = ""; 
            }
            bool doUserCentU;
            if (UserCentU > 0.0 & UserCentU < 100.0) 
            { 
                doUserCentU = true;
                UserCentileUCaption = "Centile " + UserCentU.ToString(); 
            } 
            else 
            { 
                doUserCentU = false; 
                UserCentileUCaption = ""; 
            } 
            ValidData = finish - Start + 1;
            xs = new VarAndWt[ValidData + 1 /* VB to C# conversion */ ];
            double[] xo = new double[ValidData + 1 /* VB to C# conversion */ ];
            double[] w = new double[ValidData + 1 /* VB to C# conversion */ ]; 
            ValidData = 0; 
            double sumv = 0.0;
            int k = 0;
            for ( i=Start; i <= finish; i++ ) 
            { 
                if ( x[ i ] != Constant.MISSING & v[ i ] != Constant.MISSING ) 
                { 
                    k = k + 1; 
                    xs[ k ].Data = x[ i ]; 
                    xo[ k ] = x[ i ]; 
                    sumv = sumv + v[ i ]; 
                    ValidData = ValidData + 1; 
                } 
            } 
            MissingData = ( finish - Start ) - ValidData + 1; 
            double nnx = Convert.ToDouble( ValidData ); 
            // set up normalised analytical weights
            WeightSum = 0.0; 
            if ( sumv == 0 ) 
            { 
                ValidData = 0; 
            } 
            else 
            {
                double nsumv;
                if ( NVSum != Constant.MISSING )
                { 
                    nsumv = NVSum; 
                } else { nsumv = nnx / sumv; } 
                for ( i=1; i <= ValidData; i++ ) 
                { 
                    w[ i ] = v[ i ] * nsumv; 
                    xs[ i ].wt = w[ i ]; 
                    WeightSum = WeightSum + w[ i ]; 
                } 
            } 
            // confidence interval prep
            if ( UserCL <= 0.0 | UserCL >= 1.0 )
            { 
                UserCL = 0.95; 
            } 
            double P = ( 1.0 - UserCL ) / 2.0; 
            if ( P > 1.0 - P )
                P = 1.0 - P;
            double cit = PDF.tfromp( P, Convert.ToDouble( ValidData - 1 ) ); 
            CLCaption = " " + Formatting.XRound( UserCL * 100, 1 ) + "% CL"; 
            
            if ( ValidData > 1 ) 
            { 
                // nonparametric summary
                
                Array.Sort( xs, 1, ValidData, new VarAndWtByData() ); 
                
                // get quantiles
                Minimum = GetCentile( xs, ValidData, 0 ); 
                LowerQuartile = GetCentile( xs, ValidData, 0.25 ); 
                median = GetCentile( xs, ValidData, 0.5 ); 
                UpperQuartile = GetCentile( xs, ValidData, 0.75 ); 
                Maximum = GetCentile( xs, ValidData, 1 ); 
                Range = Maximum - Minimum; 
                UserCentileL = doUserCentL ? GetCentile( xs, ValidData, UserCentL / 100.0 ) : Constant.MISSING; 
                UserCentileU = doUserCentU ? GetCentile( xs, ValidData, UserCentU / 100.0 ) : Constant.MISSING; 
                
                // parametric univariate summary
                
                // basic sums
                sum = 0.0; 
                double slog = 0.0; 
                double sumsqdev = 0.0; 
                bool gmok = false; 
                for ( i=1; i <= ValidData; i++ ) 
                { 
                    sum = sum + xo[ i ] * w[ i ]; 
                    if ( xo[ i ] * w[ i ] > 0.0 )
                    { 
                        slog = slog + Math.Log( xo[ i ] * w[ i ] ); 
                    } 
                    else
                    {
                        gmok = true;
                    } 
                } 
                mean = sum / nnx; 
                
                // deviations from the mean
                for ( i=1; i <= ValidData; i++ ) 
                { 
                    if ( Math.Abs( sumsqdev ) > 1.0E+300 ) 
                    { 
                        sumsqdev = Constant.MISSING; 
                        break;
                    } 
                    sumsqdev = sumsqdev + ( xo[ i ] - mean ) * ( xo[ i ] - mean ) * w[ i ]; 
                } 
                if ( sumsqdev == Constant.MISSING ) 
                { 
                    Variance = Constant.MISSING; 
                } 
                else 
                { 
                    Variance = sumsqdev / Convert.ToDouble( ValidData - 1 ); 
                } 
                sd = Variance < 0.0 ? Constant.MISSING : Math.Sqrt( Variance ); 
                if ( ValidData <= 0 | sd == Constant.MISSING ) 
                { 
                    sem = Constant.MISSING; 
                    MeanLCL = Constant.MISSING; 
                    MeanUCL = Constant.MISSING; 
                } 
                else 
                { 
                    sem = sd / Math.Sqrt( nnx ); 
                    double bit = cit * sd / Math.Sqrt( nnx ); 
                    MeanLCL = mean - bit; 
                    MeanUCL = mean + bit; 
                } 
                GeometricMean = gmok == false ? Math.Exp( slog / nnx ) : Constant.MISSING; 
                if ( sd != Constant.MISSING & mean != Constant.MISSING & mean != 0.0 ) 
                { 
                    VarianceCoefficient = sd / mean; 
                } 
                else 
                { 
                    VarianceCoefficient = Constant.MISSING; 
                } 
                
                // moments
                if ( Variance != Constant.MISSING & Variance != 0 & ValidData > 3 ) 
                { 
                    double m2 = 0.0; 
                    double m3 = 0.0; 
                    double m4 = 0.0; 
                    bool toobig = false;
                    for ( i=1; i <= ValidData; i++ ) 
                    { 
                        double xd = xo[ i ] - mean; 
                        m2 = m2 + ( Math.Pow( xd, 2.0 ) ) * w[ i ]; 
                        m3 = m3 + ( Math.Pow( xd, 3.0 ) ) * w[ i ]; 
                        m4 = m4 + ( Math.Pow( xd, 4.0 ) ) * w[ i ]; 
                        if ( m4 > 1.0E+300 ) 
                        { 
                            toobig = true; 
                            break;
                        } 
                    } 
                    if ( toobig ) 
                    { 
                        Skewness = Constant.MISSING; 
                        Kurtosis = Constant.MISSING; 
                    } 
                    else 
                    { 
                        m2 = m2 / nnx; 
                        m3 = m3 / nnx; 
                        m4 = m4 / nnx; 
                        // Numerically consistent with R but not Stata
                        Skewness = m3 * Math.Pow( m2, ( -1.5 ) ); 
                        Kurtosis = m4 * Math.Pow( m2, ( -2.0 ) ); 
                    } 
                } 
                else 
                { 
                    Skewness = Constant.MISSING; 
                    Kurtosis = Constant.MISSING; 
                } 
                fullSummaryReturn = true; 
                
            } 
            else if ( ValidData == 1 ) 
            { 
                Skewness = Constant.MISSING; 
                Kurtosis = Constant.MISSING; 
                LowerQuartile = Constant.MISSING; 
                UpperQuartile = Constant.MISSING; 
                UserCentileL = Constant.MISSING; 
                UserCentileU = Constant.MISSING; 
                GeometricMean = Constant.MISSING; 
                median = Constant.MISSING; 
                Variance = Constant.MISSING; 
                Maximum = xo[ 1 ]; 
                Minimum = xo[ 1 ]; 
                sum = xo[ 1 ]; 
                sd = Constant.MISSING; 
                sem = Constant.MISSING; 
                MeanLCL = Constant.MISSING; 
                MeanUCL = Constant.MISSING; 
                fullSummaryReturn = true; 
                
            } 
            else 
            { 
                Skewness = Constant.MISSING; 
                Kurtosis = Constant.MISSING; 
                LowerQuartile = Constant.MISSING; 
                UpperQuartile = Constant.MISSING; 
                UserCentileL = Constant.MISSING; 
                UserCentileU = Constant.MISSING; 
                GeometricMean = Constant.MISSING; 
                median = Constant.MISSING; 
                mean = Constant.MISSING; 
                Variance = Constant.MISSING; 
                Maximum = Constant.MISSING; 
                Minimum = Constant.MISSING; 
                sum = Constant.MISSING; 
                sd = Constant.MISSING; 
                sem = Constant.MISSING; 
                VarianceCoefficient = Constant.MISSING; 
                MeanLCL = Constant.MISSING; 
                MeanUCL = Constant.MISSING; 
                Range = Constant.MISSING; 
                fullSummaryReturn = false; 
            } 
            
            return fullSummaryReturn;
        } 
        
        
        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="x">x() is assumed to be an array from 1 to n with all elements valid</param>
        ///  <param name="N"></param>
        ///  <param name="centile"></param>
        ///  <returns></returns>
        ///  <remarks>see Gleason JR. Univariate summaries with boxplots. Stata Technical Bulletin sg67, 1997 and sg67.1, 1999.</remarks>
        private double GetCentile( VarAndWt[] x, int N, double centile ) 
        { 
            double index;
            double lastcumsum = 0;

            if ( centile < 0.0 | centile > 1.0 ) 
            { 
                return Constant.MISSING; 
            } 
            if ( centile == 0.0 ) 
            { 
                return x[ 1 ].Data; 
            } 
            if ( centile == 1.0 ) 
            { 
                return x[ N ].Data; 
            } 
            if ( CentileType == 2 ) 
            { 
                index = Math.Floor(centile * Convert.ToDouble( N + 1 )); 
                double h = centile * Convert.ToDouble( N + 1 ) - index;
                int bottom = index < 1 ? 1 : Convert.ToInt32( index );
                int top;
                if ( index + 1 > N )
                { 
                    top = N; 
                } else { top = Convert.ToInt32( index ) + 1; } 
                return ( 1.0 - h ) * x[ bottom ].Data + h * x[ top ].Data; 
            } 

            index = centile * Convert.ToDouble( N ); 
            double cumsum = 0.0;
            int i;
            for ( i=1; i <= N; i++ ) 
            { 
                cumsum = cumsum + x[ i ].wt; 
                if ( cumsum > index )
                    break;
                lastcumsum = cumsum;
            } 
            if ( i > N )
            { 
                i = N; 
            } 
            if ( lastcumsum == index ) 
                return ( x[ i - 1 ].Data + x[ i ].Data ) / 2.0; 
            return x[ i ].Data; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Method WeightedSummaryFromXK
        public bool WeightedSummaryFromXK( int k, double[,] x, int rows, string ti, double userCL, double userCentL, double userCentU, double[,] wt, string wti, double NVSum ) 
        {
            title = ti + " (weight: " + wti + ")";
            double[] z = new double[rows + 1 /* VB to C# conversion */ ];
            double[] v = new double[rows + 1 /* VB to C# conversion */ ]; 
            for (int i=1; i <= rows; i++ ) 
            { 
                z[ i ] = x[ k, i ]; 
                v[ i ] = wt[ k, i ]; 
            } 
            CentileType = 1; 
            bool weightedSummaryFromXKReturn = FullSummary( z, v, 1, rows, userCL, userCentL, userCentU, NVSum ); 
            xs = null; 
            return weightedSummaryFromXKReturn;
        } 
        
        
        // TRANSMISSINGCOMMENT: Method FullSummaryFromXSort
        public bool FullSummaryFromXSort( ref double[] x, ref double[] XSRT, ref int rows, ref string ti, ref double UserCL, ref double UserCentL, ref double UserCentU, ref int CentileDef ) 
        {
            int i; 
            
            title = ti;
            double[] v = new double[rows + 1 /* for VB to C# conversion */ ]; 
            for ( i=1; i <= rows; i++ ) 
            { 
                v[ i ] = 1.0; 
            } 
            CentileType = CentileDef; 
            bool fullSummaryFromXSortReturn = FullSummary( x, v, 1, rows, UserCL, UserCentL, UserCentU, Constant.MISSING ); 
            for ( i=1; i <= rows; i++ ) 
            { 
                XSRT[ i ] = xs[ i ].Data; 
            } 
            xs = null; 
            return fullSummaryFromXSortReturn;
        } 
        
        public bool FullSummaryFromX( double[] x, int rows, string ti, double UserCL, double UserCentL, double UserCentU, int CentileDef ) 
        {
            title = ti;
            double[] v = new double[rows + 1 /* VB to C# conversion */ ]; 
            for (int i=1; i <= rows; i++ ) 
                v[ i ] = 1.0; 
            CentileType = CentileDef; 
            bool fullSummaryFromXReturn = FullSummary( x, v, 1, rows, UserCL, UserCentL, UserCentU, Constant.MISSING ); 
            xs = null; 
            return fullSummaryFromXReturn;
        } 
        
        
        public bool FullSummaryFromXK( int k, double[,] x, int rows, string ti, double UserCL, double UserCentL, double UserCentU, int CentileDef ) 
        {
            title = ti;
            double[] z = new double[rows + 1 /* VB to C# conversion */ ];
            double[] v = new double[rows + 1 /* VB to C# conversion */ ]; 
            for (int i=1; i <= rows; i++ ) 
            { 
                z[ i ] = x[ k, i ]; 
                v[ i ] = 1.0; 
            } 
            CentileType = CentileDef; 
            bool fullSummaryFromXKReturn = FullSummary( z, v, 1, rows, UserCL, UserCentL, UserCentU, Constant.MISSING ); 
            xs = null; 
            return fullSummaryFromXKReturn;
        } 
        
    } 
    
    
} 

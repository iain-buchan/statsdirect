using System;

using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class ExactBB  
    { 
        
        //   This is a bare-bones program which calculates the conditional maximum
        //   likelihood estimate, exact confidence limits, and exact P-values for
        //   either an an odds ratio (given a series of 2x2 tables with person-count
        //   denominators) or a rate ratio (given a series of 2x2 tables with person-
        //   time denominators). It utilizes an efficient algorithm for calculating
        //   the coefficients of the conditional distribution as described in the
        //   references. To increase execution speed, the arithmetic is performed on
        //   the natural scale (not the log scale). If overflow ocurrs then a log
        //   scale is used.
        // 
        //   References
        //      1. Martin,D Austin,H (1991) An efficient program for computing
        //         conditional maximum likelihood estimates and exact confidence
        //         limits for a common odds ratio. Epidemiology 2, 359-362.
        //      2. Martin,DO Austin,H Exact estimates for a rate ratio.
        //         Submitted to Epidemiology.
        // 
        //   Author David O. Martin, MD, MPH
        //   Translation and extension (log scaling) by Iain Buchan
        //   Last mod 20/5/2001
        
        private const int MAXDEGREE = 1000000; // Max degree of a polynomial
        private const int maxIter = 300; // Max # of iterations to bracket/converge to a root
        private const double TOLERANCE = 0.00000000001; // Relative tolerance in results (do not use < 1e-15 if Pegasus rootfinder used)
        
        public struct Rec2x2 
        { // Data for one "unique" 2x2 table
        
            public double a; 
            public double m1; 
            public double n1; 
            public double n0; 
            public int freq; 
            public bool informative; 
        } 
        
        
        private static double[] polyD; // The polynomial of conditional coefficients
        private static int degD; // The degree of polyD
        
        private static double[] polyN; // The "numerator" polynomial in Func
        private static int degN; // The degree of polyN
        
        private static double Value; // Used in defining Func
        
        private static int sumA; // Sum of the observed "a" cells
        private static int minSumA; // Lowest value of "a" cell sum w/ given margins
        private static int maxSumA; // Highest value of "a" cell sum w/ given margins
        
        private static bool logScale; 
        
        private static double MAXEXP; 
        
        /// <summary>
        /// Brent alternative to Pegasus method for root finding - can be faster when high precision demanded
        /// </summary>
        /// <param name="XL">lower bound of search interval</param>
        /// <param name="xu">upper bound of search interval</param>
        /// <param name="ierr"></param>
        /// <returns></returns>
        private double BrentRoot( double XL, double xu, out int ierr ) 
        { 
            double brentRootReturn = 0;
            double D = 0; 
            
            double e = 0.0; 
            double a = XL; 
            double b = xu; 
            
            double fa = Func( ref a, out ierr ); 
            if ( ierr != 0 )
            { 
                return brentRootReturn; 
            } 
            double fb = Func( ref b, out ierr ); 
            if ( ierr != 0 )
            { 
                return brentRootReturn; 
            } 
            double C = b; 
            double fc = fb; 
            
            ierr = 0; 
            int nIter = 0; 
            
            do 
            { 
                nIter = nIter + 1; 
                if ( nIter > maxIter ) 
                { 
                    ierr = 1; 
                    break; /* TRANSWARNING: check that break is in correct scope */ 
                } 
                
                if ( ( fb > 0.0 & fc > 0.0 ) | ( fb < 0.0 & fc < 0.0 ) ) 
                { 
                    C = a; 
                    fc = fa; 
                    D = b - a; 
                    e = D; 
                } 
                
                if ( ( Math.Abs( fc ) < Math.Abs( fb ) ) ) 
                { 
                    a = b; 
                    b = C; 
                    C = a; 
                    fa = fb; 
                    fb = fc; 
                    fc = fa; 
                } 
                
                double tol1 = 2.0 * Constant.EPSILON * Math.Abs( b ) + 0.5 * TOLERANCE; 
                
                double XM = 0.5 * ( C - b ); 
                
                if ( ( Math.Abs( XM ) <= tol1 | fb == 0.0 ) )
                { 
                    break;
                } 
                
                if ( ( Math.Abs( e ) >= tol1 & Math.Abs( fa ) > Math.Abs( fb ) ) ) 
                { 
                    double s = fb / fa;
                    double P;
                    double Q;
                    if ( ( a == C ) ) 
                    { 
                        P = 2.0 * XM * s; 
                        Q = 1.0 - s; 
                    } 
                    else 
                    { 
                        Q = fa / fc; 
                        double r = fb / fc; 
                        P = s * ( 2.0 * XM * Q * ( Q - r ) - ( b - a ) * ( r - 1.0 ) ); 
                        Q = ( Q - 1.0 ) * ( r - 1.0 ) * ( s - 1.0 ); 
                    } 
                    
                    if ( ( P > 0.0 ) )
                    { 
                        Q = -Q; 
                    } 
                    
                    P = Math.Abs( P ); 
                    double xmin = Math.Abs( e * Q ); 
                    double tmp = 3.0 * XM * Q - Math.Abs( tol1 * Q ); 
                    
                    if ( ( xmin < tmp ) )
                    { 
                        xmin = tmp; 
                    } 
                    
                    if ( ( 2.0 * P < xmin ) ) 
                    { 
                        e = D; 
                        D = P / Q; 
                    } 
                    else 
                    { 
                        D = XM; 
                        e = D; 
                    } 
                } 
                else 
                { 
                    D = XM; 
                    e = D; 
                } 
                
                a = b; 
                fa = fb; 
                
                if ( ( Math.Abs( D ) > tol1 ) ) 
                { 
                    b = b + D; 
                } 
                else 
                { 
                    if ( XM < 0.0 ) 
                    { 
                        b = b - Math.Abs( tol1 ); 
                    } 
                    else 
                    { 
                        b = b + Math.Abs( tol1 ); 
                    } 
                } 
                
                fb = Func( ref b, out ierr ); 
                if ( ierr != 0 )
                { 
                    return brentRootReturn; 
                } 
            } 
            while ( true ); 
            
            brentRootReturn = b; 
            
            return brentRootReturn;
        } 
        
        
        public static void Exact22k( ITemplateHost Host, int numTables, int dataType, Rec2x2[] tables, double confLevel, ref double cMLE, out double upFishLim, out double loFishLim, out double upMidPLim, out double loMidPLim, ref double FishP1, ref double FishP2, ref double MidP1, ref double MidP2, ref bool useLogScale, out int ierr ) 
        { 
            //   Stratified case-control data, matched case-control data, and
            //   stratified person-time data are all held in a record (Rec2x2). With
            //   stratified case-control data, the record is defined so that
            // 
            //                            Exposed        Non-Exposed       Total
            //         Diseased              a                 b             m1
            //         Non-Diseased          c                 d             m0
            //         --------------------------------------------------------
            //         Total                 n1                n0             t
            // 
            //   For stratified case-control data, FREQ is set to 1. For matched case-
            //   control data, the record is defined in the same way except that FREQ
            //   corresponds to the frequency of like 2x2 tables. Note that for
            //   matched data, M1 must ALWAYS equal 1.
            // 
            //   For stratified person-time data, the record is defined so that
            // 
            //                            Exposed        Non-Exposed       Total
            //         Diseased              a                 b             m1
            //         Person-Time           n1                n0             t
            // 
            //   For stratified person-time data, FREQ is set to 1. For all types of
            //   data, the variable INFORMATIVE is TRUE if no margins of the given 2x2
            //   table are zero, otherwise INFORMATIVE is FALSE.
            // 
            //   For failure time data, a/b is events at time t in exposed/unexposed and
            //   c/d is number at risk at time t minus a/b.
            // 
            //   dataType 1 - odds ratio
            //   Tables(i).freq = 1
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + d(1)  'cases
            //   Tables(i).n1 = d(0) + d(2)  'exposed
            //   Tables(i).n0 = d(1) + d(3)  'unexposed
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)
            // 
            //   dataType 2 - matched RR
            //   Tables(i).freq = d(3)
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + 1# - d(0)  'cases
            //   Tables(i).n1 = d(0) + d(1)  'exposed
            //   Tables(i).n0 = 1# - d(0) + d(2)  'unexposed
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)
            // 
            //   dataType 3
            //   Tables(i).freq = 1
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + d(1)  'cases
            //   Tables(i).n1 = d(2)         'exposed
            //   Tables(i).n0 = d(3)         'unexposed
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)
            // 
            //   dataType 4
            //   Tables(i).freq = 1
            //   Tables(i).a = d(0)
            //   Tables(i).m1 = d(0) + d(1)  'events at time t
            //   Tables(i).n1 = d(2)         'exposed at risk
            //   Tables(i).n0 = d(3)         'unexposed at risk
            //   Tables(i).informative = (d(0) * d(3) <> 0) Or (d(1) * d(2) <> 0)
            
            
            MAXEXP = Math.Log( Constant.LMREAL ); 
            //  Make sure that exact calculations can be performed
            logScale = useLogScale; 
            CheckData( dataType, numTables, tables, out ierr ); 
            if ( ierr == 1 | ierr == 2 ) 
            { 
                ierr = -ierr; 
                polyD = null; 
                polyN = null;
                loFishLim = Constant.MISSING;
                upFishLim = Constant.MISSING;
                loMidPLim = Constant.MISSING;
                upMidPLim = Constant.MISSING;
                return; 
            } 
            //  Try on natural scale first then log scale if overflow
            CalcPoly( Host, dataType, numTables, tables, out ierr ); 
            if ( ierr == 7 ) 
            { 
                polyD = null; 
                polyN = null;
                loFishLim = Constant.MISSING;
                upFishLim = Constant.MISSING;
                loMidPLim = Constant.MISSING;
                upMidPLim = Constant.MISSING;
                return; 
            } 
            if ( ierr == 0 )
            { 
                CalcCmle( 1.0, ref cMLE, ref ierr ); 
            } 
            if ( ierr != 0 ) 
            { 
                logScale = true; 
                CalcPoly( Host, dataType, numTables, tables, out ierr ); 
                if ( ierr == 0 )
                { 
                    CalcCmle( 1.0, ref cMLE, ref ierr ); 
                } 
            } 
            if ( ierr == 0 ) 
            { 
                CalcExactLim( false, true, cMLE, confLevel, out upFishLim, ref ierr ); 
                CalcExactLim( true, true, cMLE, confLevel, out loFishLim, ref ierr ); 
                CalcExactLim( false, false, cMLE, confLevel, out upMidPLim, ref ierr ); 
                CalcExactLim( true, false, cMLE, confLevel, out loMidPLim, ref ierr ); 
                CalcExactPVals( ref FishP1, ref FishP2, ref MidP1, ref MidP2, ref ierr ); 
                useLogScale = logScale; 
            } 
            else 
            { 
                cMLE = Constant.MISSING; 
                upFishLim = Constant.MISSING; 
                loFishLim = Constant.MISSING; 
                upMidPLim = Constant.MISSING; 
                loMidPLim = Constant.MISSING; 
                FishP1 = Constant.MISSING; 
                FishP2 = Constant.MISSING; 
                MidP1 = Constant.MISSING; 
                MidP2 = Constant.MISSING; 
            } 
            polyD = null; 
            polyN = null; 
        } 
        
        /// <summary>
        /// get log(exp(a)-exp(b)) avoiding overflow due to exp(a) or exp(b)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static double SubLog( double a, double b ) 
        { 
            if ( a == b ) 
                return 0.0; 
            double BIG = a > b ? a : b;
            return Math.Log( Math.Exp( a - BIG ) - Math.Exp( b - BIG ) ) + BIG; 
        } 
        
        /// <summary>
        /// get log(exp(a)+exp(b)) avoiding overflow due to exp(a) or exp(b)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static double SumLog( double a, double b ) 
        { 
            double BIG = Math.Max(a, b); 
            return Math.Log( Math.Exp( a - BIG ) + Math.Exp( b - BIG ) ) + BIG; 
        } 
        
        /// <summary>
        /// Returns the combination y choose x
        /// </summary>
        /// <param name="y"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        private static double Comb( double y, double x ) 
        { 
            double f = 1.0; 
            
            for (int i=1; i <= Convert.ToInt32( Math.Min( x, y - x ) ); i++ ) 
            { 
                f = f * y / Convert.ToDouble( i ); 
                y = y - 1.0; 
            } 
            return f;
        } 
        
        /// <summary>
        /// This routine determines if the data allow exact estimates to be calculated.
        /// It MUST be called once prior to calling CalcPoly() given below.
        /// </summary>
        /// <param name="dataType">
        /// indicates the type of data to be analyzed (1 = stratified case-control,
        /// 2 = matched case-control, 3 = stratified person-time).</param>
        /// <param name="numTables"></param>
        /// <param name="tables"></param>
        /// <param name="ierr"> Exact estimates
        /// can only be calculated if ierr = 0.
        /// 
        /// Errors  0 = can calc exact estimates, i.e., no error,
        ///         1 = too much data (MAXDEGREE too small),
        ///         2 = no informative strata,
        ///         3 = matched table a > 1.
        ///</param>
        private static void CheckData( int dataType, int numTables, Rec2x2[] tables, out int ierr ) 
        { 
            int i; 
            
            ierr = 0; 
            
            if ( dataType == 2 ) 
            { 
                for ( i=1; i <= numTables; i++ ) 
                { 
                    if ( tables[ i ].a > 1.0 ) 
                    { 
                        ierr = 3; 
                        return; 
                    } 
                } 
            } 
            
            // Compute the global vars SUMA, MINSUMA, MAXSUMA
            sumA = 0; 
            minSumA = 0; 
            maxSumA = 0; 
            
            for ( i=1; i <= numTables; i++ ) 
            { 
                Rec2x2 transTemp2 = tables[ i ];
                if ( transTemp2.informative ) 
                { 
                    sumA = Convert.ToInt32( transTemp2.a ) * transTemp2.freq + sumA; 
                    if ( dataType == 3 ) 
                    { 
                        // Person-time data
                        minSumA = 0; 
                        maxSumA = Convert.ToInt32( transTemp2.m1 ) * transTemp2.freq + maxSumA; 
                    } 
                    else 
                    { 
                        // Case-control or survival data
                        minSumA = Convert.ToInt32( Math.Max( 0.0, transTemp2.m1 - transTemp2.n0 ) ) * transTemp2.freq + minSumA; 
                        maxSumA = Convert.ToInt32( Math.Min( transTemp2.m1, transTemp2.n1 ) ) * transTemp2.freq + maxSumA; 
                    } 
                } 
                
            } 
            
            // Check for errors
            if ( ( maxSumA - minSumA > MAXDEGREE ) ) 
            { 
                // Poly too small
                ierr = 1; 
            } 
            else if ( ( minSumA == maxSumA ) ) 
            { 
                // No informative strata }
                ierr = 2; 
            } 
            
        }


        ///  <summary>
        ///  This routine multiplies together two polynomials P1 and P2 to obtain the product polynomial P3.
        ///  </summary>
        /// <param name="host"></param>
        /// <param name="P1"></param>
        ///  <param name="P2"></param>
        ///  <param name="deg1"></param>
        ///  <param name="deg2"></param>
        ///  <param name="P3"></param>
        ///  <param name="deg3"></param>
        ///  <param name="ierr"></param>
        /// <param name="job"></param>
        /// <remarks>Reference 'Algorithms 2nd ed.', by R. Sedgewick (Addison-Wesley, 1988), p. 522.</remarks>
        private static void MultPoly( ITemplateHost host, double[] P1, double[] P2, int deg1, int deg2, double[] P3, out int deg3, out int ierr, string job ) 
        { 
            deg3 = deg1 + deg2; 
            bool waiter = Convert.ToDouble( deg1 ) * Convert.ToDouble( deg2 ) > 300000; 
            
            if ( logScale ) 
            { 
                for ( int i=0; i <= deg3; i++ ) 
                { 
                    P3[ i ] = -Constant.MISSING; 
                } 
            } 
            else 
            { 
                for ( int i=0; i <= deg3; i++ ) 
                { 
                    P3[ i ] = 0.0; 
                } 
            } 
            
            if ( waiter ) 
            { 
                host.StartProgress( "Multiplying polynomials: " + job ); 
            } 
            
            if ( logScale ) 
            { 
                for ( int i=0; i <= deg1; i++ ) 
                { 
                    for ( int j=0; j <= deg2; j++ ) 
                    { 
                        if ( P3[ i + j ] == -Constant.MISSING ) 
                        { 
                            P3[ i + j ] = P1[ i ] + P2[ j ]; 
                        } 
                        else 
                        { 
                            P3[ i + j ] = SumLog( P1[ i ] + P2[ j ], P3[ i + j ] ); 
                        } 
                    } 
                    if ( waiter ) 
                    { 
                        if ( host.UpdateProgress( i / (double)deg1 ) ) 
                        { 
                            ierr = 1; 
                            host.FinishProgress(); 
                            return; 
                        } 
                    } 
                } 
            } 
            else 
            { 
                for ( int i=0; i <= deg1; i++ ) 
                { 
                    for ( int j=0; j <= deg2; j++ ) 
                    { 
                        P3[ i + j ] = P1[ i ] * P2[ j ] + P3[ i + j ]; 
                    } 
                    if ( waiter ) 
                    { 
                        if ( host.UpdateProgress( i / (double)deg1 ) ) 
                        { 
                            ierr = 1; 
                            host.FinishProgress(); 
                            return; 
                        } 
                    } 
                } 
                
                if ( waiter ) 
                { 
                    host.FinishProgress(); 
                } 
                
                //  Test for overflow; if so, set an appropriate error value.
                for ( int i=0; i <= deg3; i++ ) 
                { 
                    if ( double.IsInfinity( P3[ i ] ) || double.IsNaN( P3[ i ] ) ) 
                    { 
                        ierr = 6; //  Old VB6 code for an overflow
                        return; 
                    } 
                } 
            }

            // If we get here, there were no errors
            ierr = 0;
        } 
        
        
        ///  <summary>
        ///  Outputs to P the coefficients of the binomial expansion of (C0 + C1*R)^F.
        ///  </summary>
        ///  <param name="C0"></param>
        ///  <param name="C1"></param>
        ///  <param name="f"></param>
        ///  <param name="P"></param>
        ///  <param name="degP"></param>
        ///  <param name="ierr"></param>
        ///  <remarks>
        ///  An alternative to this Sub would be to multiply the polynomial
        ///  (C0 + C1*R) by itself (F-1) times but using the binomial expansion is much
        ///  faster.</remarks>
        private static void BinomialExpansion( double C0, double C1, int f, double[] P, out int degP, ref int ierr ) 
        { 
            degP = f; 
            
            if ( logScale ) 
            { 
                P[ degP ] = Math.Log( C1 ) * Math.Log( Convert.ToDouble( degP ) ); 
                for ( int i=degP - 1; i >= 0; i-- ) 
                { 
                    P[ i ] = P[ i + 1 ] + Math.Log( C0 ) + Math.Log( Convert.ToDouble( i + 1 ) ) - ( Math.Log( C1 ) + Math.Log( Convert.ToDouble( degP - i ) ) ); 
                } 
            } 
            else 
            { 
                P[ degP ] = Math.Pow( C1, Convert.ToDouble( degP ) ); 
                for ( int i=degP - 1; i >= 0; i-- ) 
                { 
                    P[ i ] = P[ i + 1 ] * C0 * Convert.ToDouble( i + 1 ) / ( C1 * Convert.ToDouble( degP - i ) ); 
                    if ( double.IsInfinity( P[ i ] ) || double.IsNaN( P[ i ] ) ) 
                    { 
                        ierr = 6; //  Old VB6 code for overflow
                        return; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  This routine outputs the stratum-specific polynomial of conditional
        ///  distribution coefficients due to a SINGLE 2x2 table with person-count
        ///  denominators. If the 2x2 table is uninformative, then degDi is set to 0
        ///  and polyDi[0] to 1.0. Note that the coefficients are scaled so that
        ///  the first coefficient is equal to 1.0.
        ///  </summary>
        ///  <param name="Table"></param>
        ///  <param name="polyDi"></param>
        ///  <param name="degDi"></param>
        ///  <param name="ierr"></param>
        ///  <remarks></remarks>
        private static void PolyStratCC( ref Rec2x2 Table, double[] polyDi, out int degDi, out int ierr ) 
        { 
            degDi = 0; 
            ierr = 0;
            polyDi[0] = logScale ? 0.0 : 1.0;

            Rec2x2 transTemp3 = Table;
            if ( transTemp3.informative ) 
            { 
                double minA = Math.Max( 0.0, transTemp3.m1 - transTemp3.n0 ); // Min val of the "a" cell w/ these margins
                double maxA = Math.Min( transTemp3.m1, transTemp3.n1 ); // Max val of the "a" cell w/ these margins
                degDi = Convert.ToInt32( maxA - minA ); // The degree of this table's polynomial
                
                // The polynomial coefficients are scaled so that the first
                // coef is 1.0. Note the recursive relation between coefficients.
                double aa = minA; // Corresponds to the "a" cell
                double bb = transTemp3.m1 - minA + 1.0; // Corresponds to the "b" cell
                double cc = transTemp3.n1 - minA + 1.0; // Corresponds to the "c" cell
                double dd = transTemp3.n0 - transTemp3.m1 + minA; // Corresponds to the "d" cell
                
                if ( logScale ) 
                { 
                    for ( int i=1; i <= degDi; i++ ) 
                    { 
                        double xi = Convert.ToDouble( i ); 
                        polyDi[ i ] = polyDi[ i - 1 ] + Math.Log( ( ( bb - xi ) / ( aa + xi ) ) * ( ( cc - xi ) / ( dd + xi ) ) ); 
                    } 
                } 
                else 
                { 
                    for ( int i=1; i <= degDi; i++ ) 
                    { 
                        double xi = Convert.ToDouble( i ); 
                        polyDi[ i ] = polyDi[ i - 1 ] * ( ( bb - xi ) / ( aa + xi ) ) * ( ( cc - xi ) / ( dd + xi ) ); 
                        //  Overflow test
                        if ( double.IsInfinity( polyDi[ i ] ) || double.IsNaN( polyDi[ i ] ) ) 
                        { 
                            ierr = 6; //  Old VB6 code for overflow
                            return; 
                        } 
                    } 
                } 
            } 
            
        } 
        
        
        private static void PolyMatchCC( ref Rec2x2 Table, double[] polyEi, out int degEi, ref int ierr ) 
        { 
            // This routine outputs the polynomial of conditional distribution
            // coefficients due to a single matched table. (A single matched table is
            // generally equivalent to a number of sparse 2x2 tables.) If the table is
            // uninformative (e.g., cases and controls all exposed), then degEi is set
            // to 0 and polyEi[0] to 1.0.


            degEi = 0; 
            polyEi[ 0 ] = 1; 
            
            if (Table.informative) 
            {
                double C0 = Comb(Table.n1, 0.0) * Comb(Table.n0, Table.m1);
                double C1 = Comb( Table.n1,  1.0) * Comb( Table.n0,  Table.m1 - 1.0);
                BinomialExpansion( C0, C1, Table.freq, polyEi, out degEi, ref ierr); 
            } 
        } 
        
        /// <summary>
        /// This routine outputs the stratum-specific polynomial of conditional
        /// distribution coefficients due to a SINGLE 2x2 table with person-time
        /// denominators. If the 2x2 table is uninformative, then degDi is set to 0
        /// and polyDi[0] to 1.0.
        /// </summary>
        /// <param name="Table"></param>
        /// <param name="polyDi"></param>
        /// <param name="degDi"></param>
        /// <param name="ierr"></param>
        /// <remarks>This routine is based on the algorithm discussed in
        /// the paper by Martin and Austin, 'Exact estimates for a rate ratio',
        /// Epidemiology (in press).
        /// </remarks>
        private static void PolyStratPT1( Rec2x2 Table, double[] polyDi, out int degDi, ref int ierr ) 
        { 
            degDi = 0; 
            polyDi[ 0 ] = 1.0; 
            
            if ( Table.informative) 
                BinomialExpansion( Table.n0 / Table.n1, 1.0, Convert.ToInt32(Table.m1), polyDi, out degDi, ref ierr); 
        } 
        
        
        
        ///  <summary>
        ///  This routine outputs the stratum-specific polynomial of conditional
        ///  distribution coefficients due to a SINGLE 2x2 table with person-time
        ///  denominators. If the 2x2 table is uninformative, then degDi is set to 0
        ///  and polyDi[0] to 1.0.
        ///  </summary>
        ///  <param name="Table"></param>
        ///  <param name="polyDi"></param>
        ///  <param name="degDi"></param>
        ///  <param name="ierr"></param>
        ///  <remarks>
        ///  This routine is an alternative to PolyStratPT1. It is based on
        ///  PolyStratCC() and the idea that by letting the c and d cells of a 2x2
        ///  table approach infinity, the noncentral hypergeometric becomes binomial.
        ///  Unlike the above routine, this routine scales the coefficients so that
        ///  the first coefficient is always 1.0.
        ///  </remarks>
        private void PolyStratPT2( ref Rec2x2 Table, ref double[] polyDi, out int degDi, ref int ierr ) 
        { 
            degDi = 0; 
            polyDi[ 0 ] = 1.0; 
            
            Rec2x2 transTemp6 = Table;
            if ( transTemp6.informative ) 
            { 
                degDi = Convert.ToInt32( transTemp6.m1 ); // The degree of this table's polynomial
                
                // The polynomial coefficients are scaled so that the first
                // coef is 1.0. Note the recursive relation between coefficients
                
                double aa = 0; // Corresponds to the "a" cell
                double bb = transTemp6.m1 + 1; // Corresponds to the "b" cell
                if ( logScale ) 
                { 
                    for ( int i=1; i <= degDi; i++ ) 
                    { 
                        double xi = Convert.ToDouble( i ); 
                        polyDi[ i ] = polyDi[ i - 1 ] + Math.Log( ( ( bb - xi ) / ( aa + xi ) ) * ( transTemp6.n1 / transTemp6.n0 ) ); 
                    } 
                } 
                else 
                { 
                    for ( int i=1; i <= degDi; i++ ) 
                    { 
                        double xi = Convert.ToDouble( i ); 
                        polyDi[ i ] = polyDi[ i - 1 ] * ( ( bb - xi ) / ( aa + xi ) ) * ( transTemp6.n1 / transTemp6.n0 ); 
                        //  Overflow test
                        if ( double.IsInfinity( polyDi[ i ] ) || double.IsNaN( polyDi[ i ] ) ) 
                        { 
                            ierr = 6; //  Old VB6 code for overflow
                            return; 
                        } 
                    } 
                } 
            } 
            
        }


        ///  <summary>
        ///  This routine outputs the "main" polynomial of conditional distribution
        ///  coefficients which will subsequently be used to calculate the conditional
        ///  maximum likelihood estimate, exact confidence limits, and exact P-values.
        ///  For a given data set, this routine MUST be called once before calling
        ///  CalcExactPVals(), CalcCmle(), and CalcExactLim().
        ///  </summary>
        /// <param name="host"></param>
        /// <param name="dataType">indicates the type of data to be analyzed
        ///  (1 = stratified case-control,
        ///  2 = matched case-control, 3 = stratified person-time).</param>
        ///  <param name="numTables"></param>
        ///  <param name="tables"></param>
        ///  <param name="ierr"></param>
        ///  <remarks></remarks>
        private static void CalcPoly( ITemplateHost host, int dataType, int numTables, Rec2x2[] tables, out int ierr ) 
        { 
            ierr = 0; 
            
            int polydim = maxSumA - minSumA;
            double[] poly1 = new double[polydim + 1 /* for VB to C# conversion */ ];
            double[] poly2 = new double[polydim + 1 /* for VB to C# conversion */];
            polyD = new double[polydim + 1 /* for VB to C# conversion */ ];
            polyN = new double[polydim + 1 /* for VB to C# conversion */]; 
            
            switch ( dataType ) 
            {
                case 1: case 4:
                    PolyStratCC( ref tables[ 1 ], polyD, out degD, out ierr ); // Stratified case-control/survival
                    break;
                case 2:
                    PolyMatchCC( ref tables[ 1 ], polyD, out degD, ref ierr ); // Matched case-control
                    break;
                case 3:
                    PolyStratPT1( tables[ 1 ], polyD, out degD, ref ierr ); // Stratified person-time
                    break;
            }
            
            if ( ierr != 0 ) 
            { 
                return; 
            } 
            
            // Multiply polynomials
            for (int i=2; i <= numTables; i++ ) 
            { 
                if ( tables[ i ].informative ) 
                { 
                    int deg1 = degD;
                    int deg2;
                    Array.Copy(polyD, poly1, polyD.Length); 
                    switch ( dataType ) 
                    {
                        case 1: case 4:
                            PolyStratCC( ref tables[ i ], poly2, out deg2, out ierr ); // Stratified case-control
                            break;
                        case 2:
                            PolyMatchCC( ref tables[ i ], poly2, out deg2, ref ierr ); // Matched case-control }
                            break;
                        case 3:
                            PolyStratPT1( tables[ i ], poly2, out deg2, ref ierr ); // Stratified person-time }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("dataType", dataType, "Datatype must be 1 to 4");
                    }
                    
                    if ( ierr != 0 ) 
                    { 
                        return; 
                    } 
                    MultPoly( host, poly1, poly2, deg1, deg2, polyD, out degD, out ierr, (i - 1).ToString() + " of " +  (numTables - 1).ToString() ); 
                    if ( ierr != 0 ) 
                    { 
                        return; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  This routine returns the value of the polynomial C, a polynomial of
        ///  conditional coefficients of degree DEGC, evaluated at an odds ratio or
        ///  rate ratio R. If R > 1.0 then the poly evaluated is C / R^(DEGC) - helps avoid overflows.
        ///  Horner's method is used to evaluate the polynomial.
        ///  </summary>
        ///  <param name="C"></param>
        ///  <param name="degC"></param>
        ///  <param name="r"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static double EvalPoly( double[] C, int degC, double r ) 
        {
            int i; 
            double y; double z; 
            
            
            
            if ( logScale ) 
            { 
                
                if ( r == 0.0 ) 
                { 
                    y = C[ 0 ]; 
                } 
                else if ( r <= 1.0 ) 
                { 
                    y = C[ degC ]; 
                    if ( r < 1 ) 
                    { 
                        for ( i=( degC - 1 ); i >= 0; i-- ) 
                        { 
                            y = SumLog( y + Math.Log( r ), C[ i ] ); 
                        } 
                    } 
                    else 
                    { 
                        for ( i=( degC - 1 ); i >= 0; i-- ) 
                        { 
                            y = SumLog( y, C[ i ] ); 
                        } 
                    } 
                } 
                else 
                { 
                    y = C[ 0 ]; 
                    z = Math.Log( 1.0 / r ); 
                    for ( i=1; i <= degC; i++ ) 
                    { 
                        y = SumLog( y + z, C[ i ] ); 
                    } 
                } 
                
            } 
            else 
            { 
                
                if ( r == 0.0 ) 
                { 
                    y = C[ 0 ]; 
                } 
                else if ( r <= 1.0 ) 
                { 
                    y = C[ degC ]; 
                    if ( r < 1.0 ) 
                    { 
                        for ( i=( degC - 1 ); i >= 0; i-- ) 
                        { 
                            y = y * r + C[ i ]; 
                        } 
                    } 
                    else 
                    { 
                        for ( i=( degC - 1 ); i >= 0; i-- ) 
                        { 
                            y = y + C[ i ]; 
                        } 
                    } 
                } 
                else 
                { 
                    y = C[ 0 ]; 
                    z = 1.0 / r; 
                    for ( i=1; i <= degC; i++ ) 
                    { 
                        y = y * z + C[ i ]; 
                    } 
                } 
                
            } 
            
            return y; 
        } 
        
        
        private static double Func( ref double r, out int ierr ) 
        {
            ierr = 0;
            double funcReturn = 0;
            // The root (value at which func = 0) of this function is the conditional MLE of the common odds ratio
            // or common rate ratio, or an exact confidence limit depending on how the
            // global variables VALUE, POLYN, and POLYD are defined.

            double numer = EvalPoly( polyN, degN, r ); 
            double denom = EvalPoly( polyD, degD, r ); 
            
            if ( logScale ) 
            { 
                if ( r <= 1.0 ) 
                { 
                    funcReturn = Math.Exp( numer - denom ) - Value; 
                } 
                else 
                { 
                    if ( degD - degN == 0 ) 
                    { 
                        funcReturn = Math.Exp( numer - denom ) - Value; 
                    } 
                    else 
                    { 
                        funcReturn = Math.Exp( ( numer - ( Math.Log( r ) * ( Convert.ToDouble( degD - degN ) ) ) ) - denom ) - Value; 
                    } 
                } 
            } 
            else 
            { 
                if ( denom == 0.0 ) 
                { 
                    ierr = 6; 
                } 
                else 
                { 
                    if ( r <= 1.0 ) 
                    { 
                        funcReturn = numer / denom - Value; 
                    } 
                    else 
                    { 
                        funcReturn = ( numer / ( Math.Pow( r, Convert.ToDouble( degD - degN ) ) ) ) / denom - Value; 
                    } 
                } 
            }

            return funcReturn;
        } 
        
        /// <summary>
        /// Given a positive non-zero starting value APPROX, this routine searches for
        /// a bracket to the root of the function Func on the interval [0, infinity)
        /// so that on output F0 * F1 &lt;= 0 which guarantees that a root lies in the
        /// interval [X0, X1].
        /// </summary>
        /// <param name="approx"></param>
        /// <param name="x0"></param>
        /// <param name="x1"></param>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <param name="ierr"></param>
        private static void BracketRoot(double approx, out double x0, out double x1, out double f0, ref double f1, out int ierr ) 
        {
            int iter = 0; 
            x1 = Math.Max( 0.5, approx ); // X1 is the upper bound
            x0 = 0.0; // X0 is the lower bound
            f0 = Func( ref x0, out ierr ); // Func at X0
            if ( ierr != 0 )
            { 
                return; 
            } 
            f1 = Func( ref x1, out ierr ); // Func at X1
            if ( ierr != 0 )
            { 
                return; 
            } 
            
            // if necessary, increase X1 until F1 and F0 have different signs
            while ( ( f1 * f0 > 0.0 ) & ( iter < maxIter ) ) 
            { 
                iter = iter + 1; 
                x0 = x1; 
                f0 = f1; 
                x1 = x1 * 1.5 * iter; 
                f1 = Func( ref x1, out ierr ); 
                if ( ierr != 0 )
                { 
                    return; 
                } 
            } 
            
        } 
        
        /// <summary>
        /// This Sub returns a single real root of the function Func on the
        /// interval [X0, X1] to within a relative tolerance TOLERANCE. The Sub
        /// implements an elegant modified regula falsi algorithm (the Pegasus
        /// modification). Brent's method for root solving is slightly faster but more
        /// complex.
        /// </summary>
        /// <param name="x0"></param>
        /// <param name="x1"></param>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <param name="root"></param>
        /// <param name="ierr">
        /// 0 = no error,
        /// 1 = X0 and X1 don't bracket the root (i.e. F0 * F1 > 0),
        /// 2 = root not found in MAXITER iterations.
        /// </param>
        /// <remarks>
        /// Reference
        ///    Jarrat, P., A review of methods for solving non-linear algebraic
        ///    equations in one variable, in Rabinowitz, P. (ed.), Numerical Methods
        ///    for Nonlinear Algebraic Equations, 1973, Gordon & Breach, Science
        ///    Publ., New York.
        ///</remarks>
        private static void Zero( ref double x0, ref double x1, ref double f0, ref double f1, out double root, out int ierr ) 
        {
            ierr = 0; // Initialize
            int iter = 0; 
            
            if ( Math.Abs( f0 ) < Math.Abs( f1 ) ) 
            { // Make X1 best approx to root
            
                double swap = x0; 
                x0 = x1; 
                x1 = swap; 
                swap = f0; 
                f0 = f1; 
                f1 = swap; 
            } 
            
            bool found = ( f1 == 0.0 );
            if ( ( found == false ) && ( f0 * f1 > 0.0 ) ) 
            { 
                ierr = 1; // Root not bracketed
            } 
            
            // Converge to root
            while ( ( found == false ) && ( iter < maxIter ) && ( ierr == 0 ) ) 
            { 
                iter = iter + 1; 
                double x2 = x1 - f1 * ( x1 - x0 ) / ( f1 - f0 ); 
                double f2 = Func( ref x2, out ierr ); 
                if ( ierr != 0 )
                {
                    root = Constant.MISSING;
                    return; 
                } 
                if ( f1 * f2 < 0.0 ) 
                { // X0 not retained
                
                    x0 = x1; 
                    f0 = f1; 
                } 
                else 
                { // X0 retained => modify F0
                
                    f0 = f0 * f1 / ( f1 + f2 ); // The Pegasus modification
                } 
                x1 = x2; 
                f1 = f2; 
                found = ( Math.Abs( x1 - x0 ) < ( Math.Abs( x1 ) * TOLERANCE ) ) | ( f1 == 0.0 ); 
            } 
            
            root = x1; // Estimated root
            if ( ( !( found ) ) & ( iter >= maxIter ) & ( ierr == 0 ) ) 
            { 
                ierr = 2; // Too many iterations
            } 
            
        } 
        
        /// <summary>
        /// This routine returns the root of Func above on the interval [0, infinity).
        /// </summary>
        /// <param name="approx"></param>
        /// <param name="root"></param>
        /// <param name="ierr"></param>
        private static void Converge( double approx, out double root, out int ierr ) 
        { 
            double x0; double x1; double f0; double f1 = 0; 
            
            BracketRoot( approx, out x0, out x1, out f0, ref f1, out ierr );
            if ( ierr != 0 )
            {
                root = Constant.MISSING;
                return;
            }
            
            // alternative Brent root method - good if tol <1e-15
            // root = BrentRoot(x0, x1, ierr)
            Zero( ref x0, ref x1, ref f0, ref f1, out root, out ierr ); 
            
        } 
        
        /// <summary>
        /// This routine returns the exact P-values as defined in 'Modern
        /// Epidemiology ' by K. J. Rothman (Little, Brown, and Co., 1986).
        /// </summary>
        /// <param name="FishP1"></param>
        /// <param name="FishP2"></param>
        /// <param name="MidP1"></param>
        /// <param name="MidP2"></param>
        /// <param name="ierr"></param>
        private static void CalcExactPVals( ref double FishP1, ref double FishP2, ref double MidP1, ref double MidP2, ref int ierr ) 
        { 
            
            int i;
            double denom; double upFishPVal; double loFishPVal; double upMidPPVal; double loMidPPVal;


            int diff = sumA - minSumA; 
            double upTail = polyD[ degD ]; 
            double upZ = polyD[ degD ] <= polyD[ diff ] ? polyD[ degD ] : 0.0; 
            double loZ = 0.0; 
            
            if ( logScale ) 
            { 
                for ( i=degD - 1; i >= diff; i-- ) 
                { 
                    upTail = SumLog( upTail, polyD[ i ] ); 
                    if ( polyD[ i ] <= polyD[ diff ] )
                    { 
                        upZ = SumLog( upZ, polyD[ i ] ); 
                    } 
                } 
                denom = upTail; 
                for ( i=diff - 1; i >= 0; i-- ) 
                { 
                    denom = SumLog( denom, polyD[ i ] ); 
                    if ( polyD[ i ] <= polyD[ diff ] )
                    { 
                        loZ = SumLog( loZ, polyD[ i ] ); 
                    } 
                } 
                upFishPVal = zExp( upTail - denom, out ierr ); 
                loFishPVal = 1.0 - zExp( SubLog( upTail, polyD[ diff ] ) - denom, out ierr ); 
                FishP1 = Math.Min( upFishPVal, loFishPVal ); 
                FishP2 = zExp( SumLog( upZ, loZ ) - denom, out ierr ); 
                upMidPPVal = zExp( SubLog( upTail, Math.Log( 0.5 ) + polyD[ diff ] ) - denom, out ierr ); 
                loMidPPVal = 1.0 - upMidPPVal; 
                MidP1 = Math.Min( upMidPPVal, loMidPPVal ); 
                MidP2 = Math.Min( 2.0 * MidP1, 1.0 ); 
            } 
            else 
            { 
                for ( i=degD - 1; i >= diff; i-- ) 
                { 
                    upTail = upTail + polyD[ i ]; 
                    if ( polyD[ i ] <= polyD[ diff ] )
                    { 
                        upZ = upZ + polyD[ i ]; 
                    } 
                } 
                denom = upTail; 
                for ( i=diff - 1; i >= 0; i-- ) 
                { 
                    denom = denom + polyD[ i ]; 
                    if ( polyD[ i ] <= polyD[ diff ] )
                    { 
                        loZ = loZ + polyD[ i ]; 
                    } 
                } 
                if ( denom == 0 ) 
                { 
                    ierr = 6; 
                    // upFishPVal = Constant.MISSING; 
                    // loFishPVal = Constant.MISSING; 
                    // upMidPPVal = Constant.MISSING; 
                    // loMidPPVal = Constant.MISSING; 
                } 
                else 
                { 
                    upFishPVal = upTail / denom; 
                    loFishPVal = 1.0 - ( upTail - polyD[ diff ] ) / denom; 
                    FishP1 = Math.Min( upFishPVal, loFishPVal ); 
                    FishP2 = ( upZ + loZ ) / denom; 
                    upMidPPVal = ( upTail - 0.5 * polyD[ diff ] ) / denom; 
                    loMidPPVal = 1.0 - upMidPPVal; 
                    MidP1 = Math.Min( upMidPPVal, loMidPPVal ); 
                    MidP2 = Math.Min( 2.0 * MidP1, 1.0 ); 
                } 
            } 
            
        } 
        
        /// <summary>
        /// This routine returns the conditional maximum likelihood estimate of the
        /// common odds ratio or common rate ratio. APPROX may be set to 1.0 if no
        /// estimate is available, but the solution is obtained faster with a good
        /// approximation. CMLE returns as Constant.MISSING if convergence to a solution did not
        /// occur in MAXITER iterations.
        /// </summary>
        /// <param name="approx"></param>
        /// <param name="cMLE"></param>
        /// <param name="ierr"></param>
        private static void CalcCmle( double approx, ref double cMLE, ref int ierr ) 
        {
            if ( ( minSumA < sumA ) && ( sumA < maxSumA ) ) 
            { // Can calc point estimate
                Value = sumA; // The sum of the observed "a" cells
                degN = degD; // Degree of the numerator polynomial
                int i;
                if ( logScale ) 
                { 
                    for ( i=0; i <= degN; i++ ) 
                    { // Defines the numerator polynomial
                    
                        if ( minSumA + i == 0 ) 
                        { 
                            polyN[ i ] = -1.0E+300; // safe to use v. small number as exp(<minexp) does not underflow but returns 0
                        } 
                        else 
                        { 
                            polyN[ i ] = Math.Log( Convert.ToDouble( minSumA + i ) ) + polyD[ i ]; 
                        } 
                    } 
                } 
                else 
                { 
                    for ( i=0; i <= degN; i++ ) 
                    { // Defines the numerator polynomial
                    
                        polyN[ i ] = Convert.ToDouble( minSumA + i ) * polyD[ i ]; 
                    } 
                } 
                Converge( approx, out cMLE, out ierr ); // Solves so that Func(cmle) = 0
                if ( ierr != 0 )
                { 
                    cMLE = Constant.MISSING; 
                } // Failed convergence
            } 
            else if ( ( sumA == minSumA ) ) 
            { // Point estimate = 0
            
                cMLE = 0.0; 
            } 
            else if ( ( sumA == maxSumA ) ) 
            { // Point estimate = inf
            
                cMLE = double.PositiveInfinity; 
            } 
        } 
        
        /// <summary>
        /// This routine returns an exact confidence limit for the common odds ratio
        /// or common rate ratio with the level of confidence determined by CONFLEVEL
        /// which *must* satisfy 0 &lt;= CONFLEVEL &lt; 1. APPROX may be set to 1.0 if no
        /// estimate is available, but the solution is obtained faster with a good
        /// approximation. LIMIT returns as Constant.MISSING if convergence to a solution did not
        /// occur in MAXITER iterations.
        /// </summary>
        /// <param name="lower"></param>
        /// <param name="fisher"></param>
        /// <param name="approx"></param>
        /// <param name="confLevel"></param>
        /// <param name="limit"></param>
        /// <param name="ierr"></param>
        private static void CalcExactLim( bool lower, bool fisher, double approx, double confLevel, out double limit, ref int ierr ) 
        { 
            if ( ( sumA == minSumA ) ) 
            { // Point estimate = 0 => lower limit = 0
            
                if ( lower ) 
                { 
                    limit = 0; 
                    return; 
                } 
            } 
            else if ( ( sumA == maxSumA ) ) 
            { // Point estimate = inf => upper limit = inf
            
                if ( lower == false ) 
                { 
                    limit = double.PositiveInfinity; 
                    return; 
                } 
            } 
            
            if ( lower ) 
            { 
                Value = 0.5 * ( 1.0 + confLevel ); // = 1 - alpha / 2
            } 
            else 
            { 
                Value = 0.5 * ( 1.0 - confLevel ); // = alpha / 2
            } 
            
            // Degree of numerator polynomial
            if ( lower && fisher ) 
            { 
                degN = sumA - minSumA - 1; 
            } 
            else 
            { 
                degN = sumA - minSumA; 
            } 
            
            Array.Copy(polyD, polyN, polyD.Length ); 
            
            // Mid-P adjustment
            if ( logScale ) 
            { 
                if ( !( fisher ) )
                { 
                    polyN[ degN ] = polyD[ degN ] - Math.Log( 2.0 ); 
                } 
            } 
            else 
            { 
                if ( !( fisher ) )
                { 
                    polyN[ degN ] = 0.5 * polyD[ degN ]; 
                } 
            } 
            
            Converge( approx, out limit, out ierr ); // Solves so that Func(limit) = 0
            
            if ( ierr != 0 )
            { 
                limit = Constant.MISSING; 
            } // Failed convergence
        }


        private static double zExp( double z, out int ierr ) 
        { 
            double zExpReturn;
            
            // no need to check for z<minexp as exp in vb returns 0 and does not underflow
            if ( z > MAXEXP ) 
            { 
                zExpReturn = Constant.MISSING; 
                ierr = 6; 
            } 
            else 
            { 
                zExpReturn = Math.Exp( z ); 
                ierr = 0; 
            } 
            return zExpReturn;
        } 
        
    } 
    
    
} 

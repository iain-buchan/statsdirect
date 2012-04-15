using System;

namespace StatsDirect.Numerics
{
    public class NonParametric  
    { 
        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="x">1-based array of values</param>
        ///  <param name="nsum"></param>
        ///  <param name="n1"></param>
        ///  <param name="n2"></param>
        ///  <param name="w1"></param>
        ///  <param name="u"></param>
        ///  <param name="z"></param>
        ///  <param name="xf"></param>
        ///  <param name="r1"></param>
        ///  <param name="fault"></param>
        ///  <remarks></remarks>
        public static void x_mwut( ref double[] x, int nsum, int n1, int n2, double[] w1, ref double u, ref double z, ref double xf, ref double r1, out bool fault ) 
        { 
            fault = false; 
            if ( nsum < 2 ) 
            { 
                fault = true; 
            } 
            else if ( n1 >= nsum | n1 < 1 ) 
            { 
                fault = true; 
            } 
            else 
            { 
                ExFortran.Rank( x, w1, 1, nsum, 1, out xf ); 
                r1 = 0.0; 
                for ( int j=1; j <= n1; j++ ) 
                { 
                    r1 = r1 + w1[ j ]; 
                } 
                double r2 = 0.0; 
                for ( int i=n1 + 1; i <= nsum; i++ ) 
                { 
                    r2 = r2 + w1[ i ]; 
                } 
                double fts = nsum;
                double f2 = n2; 
                double fx = Convert.ToDouble( n1 ) * f2; 
                u = fx + f2 * ( f2 + 1 ) * 0.5 - r2; 
                double se; 
                if ( xf != 0 ) 
                { 
                    se = fx / ( 12 * fts * ( fts - 1.0 ) ); 
                    se = Math.Sqrt( se * ( Math.Pow( fts, 3.0 ) - fts - xf * 12.0 ) ); 
                } 
                else 
                { 
                    se = Math.Sqrt( fx * ( fts + 1.0 ) / 12.0 ); 
                } 
                z = ( u - 0.5 * fx ) / se; 
            } 
        } 
    } 
} 

using System;
using System.Collections.Generic;

using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public class Analysis  
    { 
        public static StepResult RptRateDirectStd( ITemplateHost host, ParameterBag parameters ) 
        {
            double xu; 
            double xl;
            int fault; 
            
            DataFrame datFrame = parameters[ "data" ].AsDataFrame; 
            DoubleVariable datV0 = datFrame.Variables[ 0 ].AsDoubleVariable; 
            DoubleVariable datV1 = datFrame.Variables[ 1 ].AsDoubleVariable; 
            DoubleVariable datV2 = datFrame.Variables[ 2 ].AsDoubleVariable; 
            int rows = datFrame.MaxRows;
            double[] idxy = new double[rows + 1 /* VB to C# conversion */ ];
            double[] idxn = new double[rows + 1 /* VB to C# conversion */ ];
            double[] idxr = new double[rows + 1 /* VB to C# conversion */ ];
            double[] refn = new double[rows + 1 /* VB to C# conversion */ ];
            double[] refw = new double[rows + 1 /* VB to C# conversion */ ]; 
            
            double cco = parameters[ "cco" ].AsDouble; 
            if ( cco > 1.0 | cco < 0.0 )
            { 
                cco = 0.95; 
            } 
            double alpha = 1.0 - cco; 
            
            double nunit = Parsing.Cdbl_Txt( parameters[ "nunit" ].AsString ); 
            if ( nunit <= 0.0 )
            { 
                nunit = 1.0; 
            } 
            
            double refntot = 0.0; 
            double revents = 0.0; 
            double ntot = 0.0; 
            for (int j=1; j <= rows; j++ ) 
            { 
                double x = datV0.Data[j - 1]; 
                idxy[ j ] = x; 
                revents = revents + x; 
                x = datV1.Data[j - 1]; 
                idxn[ j ] = x; 
                ntot = ntot + x; 
                if ( x <= 0.0 ) 
                { 
                    throw new InvalidDataException(); 
                } 
                x = datV2.Data[j - 1]; 
                refn[ j ] = x; 
                refntot = refntot + x; 
                if ( idxy[ j ] > idxn[ j ] ) 
                { 
                    throw new InvalidDataException( "Number of events must be greater then person-time, do not scale person-time"); 
                } 
            } 
            
            if ( refntot <= 0.0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            
            for (int j=1; j <= rows; j++ ) 
            { 
                refw[ j ] = refn[ j ] / refntot; 
            } 
            
            double stdr = 0.0; 
            double pois_var = 0.0; 
            double bino_var = 0.0; 
            for (int j=1; j <= rows; j++ ) 
            { 
                idxr[ j ] = idxy[ j ] / idxn[ j ]; 
                stdr = stdr + idxr[ j ] * refn[ j ]; 
                pois_var = pois_var + refn[ j ] * refn[ j ] * idxr[ j ] / idxn[ j ]; 
                bino_var = bino_var + refn[ j ] * refn[ j ] * idxr[ j ] * ( 1.0 - idxr[ j ] ) / idxn[ j ]; 
            } 
            stdr = stdr / refntot; 
            pois_var = pois_var / ( refntot * refntot ); 
            bino_var = bino_var / ( refntot * refntot ); 
            
            //  RTF_LoadTemplate("dstdr.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            if ( nunit == 1.0 ) 
            { 
                outputParameters.AddOutput( "units", "1 unit" ); 
            } 
            else 
            {
                outputParameters.AddOutput("units", nunit.ToString("#,##0") + " units"); 
            } 
            
            List<ParameterBag> inputsList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*inputs", inputsList ); 
            for (int  j=1; j <= rows; j++ ) 
            { 
                ParameterBag inputsParameters = new ParameterBag(); 
                inputsList.Add( inputsParameters ); 
                inputsParameters.AddOutput( "idxy", host.RoundU( idxy[ j ] ) ); 
                inputsParameters.AddOutput( "idxn", host.RoundU( idxn[ j ] ) ); 
                inputsParameters.AddOutput( "idxr", host.RoundU( idxr[ j ] * nunit ) ); 
                inputsParameters.AddOutput( "refn", host.RoundU( refn[ j ] ) ); 
                inputsParameters.AddOutput( "refw", host.RoundU( refw[ j ] ) ); 
            } 
            
            // CIs for the single Poisson parameter (stratum specific rate)
            outputParameters.AddOutput( "pc", Formatting.XRound( cco * 100, 2 ) ); 
            List<ParameterBag> cisList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*cis", cisList ); 
            for (int j=1; j <= rows; j++ ) 
            { 
                ParameterBag cisParameters = new ParameterBag(); 
                cisList.Add( cisParameters ); 
                cisParameters.AddOutput( "idxr", host.RoundU( idxr[ j ] * nunit ) ); 
                Rates.poisson_ci( alpha, idxy[ j ], idxn[ j ], out xl, out xu ); 
                cisParameters.AddOutput( "from", host.RoundU( xl * nunit ) ); 
                cisParameters.AddOutput( "to", host.RoundU( xu * nunit ) ); 
                cisParameters.AddOutput( "label", "" ); 
            } 
            
            // pooled
            outputParameters.AddOutput( "events", host.RoundU( revents ) ); 
            outputParameters.AddOutput( "stde", host.RoundU( stdr * ntot ) ); 
            
            outputParameters.AddOutput( "crude", host.RoundU( revents * nunit / ntot ) ); 
            outputParameters.AddOutput( "stdr", host.RoundU( stdr * nunit ) ); 
            double cit = PDF.gauinv( cco + ( 1.0 - cco ) / 2.0, out fault ); 
            
            // Binomial approx CI - see Armitage
            double ser = bino_var > 0.0 ? Math.Sqrt( bino_var ) : Constant.MISSING; 
            outputParameters.AddOutput( "ser_any", host.RoundU( ser * nunit ) ); 
            if ( fault != 0 ) 
            { 
                xl = Constant.MISSING; 
                xu = Constant.MISSING; 
            } 
            else 
            { 
                xl = stdr - cit * ser; 
                xu = stdr + cit * ser; 
            } 
            outputParameters.AddOutput( "from_any", host.RoundU( xl * nunit ) ); 
            outputParameters.AddOutput( "to_any", host.RoundU( xu * nunit ) ); 
            
            // Poisson approx CI
            ser = pois_var > 0.0 ? Math.Sqrt( pois_var ) : Constant.MISSING; 
            outputParameters.AddOutput( "ser_small", host.RoundU( ser * nunit ) ); 
            if ( fault != 0 ) 
            { 
                xl = Constant.MISSING; 
                xu = Constant.MISSING; 
            } 
            else 
            { 
                xl = stdr - cit * ser; 
                xu = stdr + cit * ser; 
            } 
            outputParameters.AddOutput( "from_small", host.RoundU( xl * nunit ) ); 
            outputParameters.AddOutput( "to_small", host.RoundU( xu * nunit ) ); 
            
            // Dobson improved approx Poisson CI - Stats in Medicine 1991 (10) 457-
            Rates.poisson_ci( alpha, revents, 1.0, out xl, out xu ); 
            if ( xl != Constant.MISSING & pois_var >= 0.0 & revents > 0.0 ) 
            { 
                xl = stdr + Math.Sqrt( pois_var / revents ) * ( xl - revents ); 
            } 
            else 
            { 
                xl = Constant.MISSING; 
            } 
            if ( xu != Constant.MISSING & pois_var >= 0.0 & revents > 0.0 ) 
            { 
                xu = stdr + Math.Sqrt( pois_var / revents ) * ( xu - revents ); 
            } 
            else 
            { 
                xu = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "from_dobson", host.RoundU( xl * nunit ) ); 
            outputParameters.AddOutput( "to_dobson", host.RoundU( xu * nunit ) ); 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptRateCompareTwo( ITemplateHost host, ParameterBag parameters ) 
        {
            double p2m = 0; 
            double p1m = 0; 
            double p2f = 0; 
            double p1f = 0; 
            double llm = 0; 
            double ulm = 0; 
            double llf = 0; 
            double ulf = 0; 
            double eor = 0; 
            double irr2; 
            double irr0; 
            double f; 
            double irr1; 
            double ird2; 
            double ird1;
            int fault;

            double a = parameters[ "a" ].AsDouble; 
            double b = parameters[ "b" ].AsDouble; 
            double pt1 = parameters[ "pt1" ].AsDouble; 
            double pt2 = parameters[ "pt2" ].AsDouble; 
            bool opt = parameters[ "do_cml" ].AsBoolean; 
            double pt = pt1 + pt2; 
            double M = a + b; 
            double GAMMA = parameters[ "gamma" ].AsDouble; 
            if ( GAMMA <= 0.0 | GAMMA >= 1.0 )
            { 
                GAMMA = 0.95; 
            } 
            
            if ( a + b <= 0.0 | pt1 <= 0.0 | pt2 <= 0.0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            
            double ir1 = a / pt1; 
            double ir2 = b / pt2; 
            double ird = ir1 - ir2; 
            double xmh = ( ( a - ( M * pt1 ) / pt ) * ( a - ( M * pt1 ) / pt ) ) / ( ( M * pt1 * pt2 ) / ( pt * pt ) ); 
            double pxmh = PDF.chivalp( xmh, 1.0 ); 
            
            double P = 1.0 - ( ( 1.0 - GAMMA ) / 2.0 ); 
            double z = PDF.gauinv( P, out fault ); 
            
            if ( xmh == 0 ) 
            { 
                ird1 = Constant.MISSING; 
                ird2 = Constant.MISSING; 
            } 
            else 
            { 
                ird1 = ird - z * Math.Sqrt( ( ird * ird ) / xmh ); 
                ird2 = ird + z * Math.Sqrt( ( ird * ird ) / xmh ); 
            } 
            
            if ( a == 0.0 ) 
            { 
                irr1 = 0.0; 
            } 
            else 
            { 
                f = PDF.ffromp( 2.0 * a, 2.0 * ( b + 1 ), 1.0 - P ); 
                irr1 = ( pt2 / pt1 ) * ( a / ( b + 1.0 ) ) * ( 1.0 / f ); 
            } 
            if ( b == 0.0 ) 
            { 
                irr0 = Constant.MISSING; 
                irr2 = Constant.MISSING; 
            } 
            else 
            { 
                irr0 = ( a / pt1 ) / ( b / pt2 ); 
                f = PDF.ffromp( 2.0 * b, 2.0 * ( a + 1 ), 1.0 - P ); 
                irr2 = ( pt2 / pt1 ) * ( ( a + 1.0 ) / b ) * f; 
            } 
            
            if ( opt ) 
            {
                ExactBB.Rec2x2[] tabl = new ExactBB.Rec2x2[1 + 1 /* VB to C# conversion */ ]; 
                tabl[ 1 ].freq = 1; 
                tabl[ 1 ].a = a; 
                tabl[ 1 ].m1 = b + a; 
                tabl[ 1 ].n1 = pt1; 
                tabl[ 1 ].n0 = pt2; 
                tabl[ 1 ].informative = ( a * pt1 != 0 ) | ( b * pt2 != 0 );
                bool useLogScale = false;
                int ierr;
                ExactBB.Exact22k( host, 1, 3, tabl, GAMMA, ref eor, out ulf, out llf, out ulm, out llm, ref p1f, ref p2f, ref p1m, ref p2m, ref useLogScale, out ierr ); 
                if ( ierr != 0 ) 
                { 
                    host.Error( Formatting.ERRCOLON + "Error in calculation", "StatsDirect" ); 
                } 
            } 
            
            if ( fault != 0 ) 
            { 
                // TODO: Error
                return null; 
            } 
            //  RTF_LoadTemplate("incid.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "a_out",  a.ToString() ); 
            outputParameters.AddOutput( "b_out",  b.ToString() ); 
            outputParameters.AddOutput( "m",  M.ToString() ); 
            outputParameters.AddOutput( "pt1_out",  pt1.ToString() ); 
            outputParameters.AddOutput( "pt2_out",  pt2.ToString() ); 
            outputParameters.AddOutput( "pt",  pt.ToString() ); 
            
            outputParameters.AddOutput( "ir1", host.RoundU( ir1 ) ); 
            outputParameters.AddOutput( "ir2", host.RoundU( ir2 ) ); 
            
            outputParameters.AddOutput( "ird", host.RoundU( ird ) ); 
            outputParameters.AddOutput( "pc", Formatting.XRound( GAMMA * 100, 2 ) ); 
            outputParameters.AddOutput( "ird_from", host.RoundU( ird1 ) ); 
            outputParameters.AddOutput( "ird_to", host.RoundU( ird2 ) ); 
            
            outputParameters.AddOutput( "xmh", host.RoundU( xmh ) ); 
            outputParameters.AddOutput( "p", host.pval( pxmh ) );

            outputParameters.AddOutput("irr", irr0 == Constant.MISSING ? Formatting.INFRES : host.RoundU(irr0));
            outputParameters.AddOutput("irr_from", irr1 == Constant.MISSING ? Formatting.INFRESNEG : host.RoundU(irr1));
            outputParameters.AddOutput("irr_to", irr2 == Constant.MISSING ? Formatting.INFRES : host.RoundU(irr2));

            List<ParameterBag> exactList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*exact", exactList ); 
            if ( opt ) 
            { 
                ParameterBag exactParameters = new ParameterBag(); 
                exactList.Add( exactParameters ); 
                exactParameters.AddOutput( "eor", host.RoundU( eor ) ); 
                exactParameters.AddOutput( "llf", host.RoundU( llf ) ); 
                exactParameters.AddOutput( "ulf", host.RoundU( ulf ) ); 
                exactParameters.AddOutput( "p1f", host.pval( p1f ) ); 
                exactParameters.AddOutput( "p2f", host.pval( p2f ) ); 
                exactParameters.AddOutput( "llm", host.RoundU( llm ) ); 
                exactParameters.AddOutput( "ulm", host.RoundU( ulm ) ); 
                exactParameters.AddOutput( "p1m", host.pval( p1m ) ); 
                exactParameters.AddOutput( "p2m", host.pval( p2m ) ); 
            } 
            
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        private static double Prop_MidPFisher2( int a, int b, int c, int d ) 
        { 
            double prop_MidPFisher2Return;
            int fault = 0;
            int t;

            if ( a > d ) 
            { 
                t = a; 
                a = d; 
                d = t; 
            } 
            if ( b > c ) 
            { 
                t = b; 
                b = c; 
                c = t; 
            } 
            
            int p = a + b; 
            int q = c + d; 
            int r = a + c; 
            int s = b + d; 
            int n = p + q; 
            
            if ( p > 0 & q > 0 & r > 0 & s > 0 ) 
            { 
                
                double b0 = 1.0; 
                double n1 = n; 
                double S1 = s; 
                do 
                { 
                    if ( b0 > 1.0E+300 | S1 <= 0.0 ) 
                    { 
                        fault = 1; 
                        break;
                    } 
                    b0 = b0 * n1 / S1; 
                    S1 = S1 - 1.0; 
                    n1 = n1 - 1.0; 
                } 
                while ( n1 > Convert.ToDouble( q ) ); 
                
                double e1 = p * r / (double)n; 
                
                if ( fault != 0 ) 
                { 
                    prop_MidPFisher2Return = Constant.MISSING; 
                    return prop_MidPFisher2Return; 
                    
                }
                double[] f1 = new double[ p + 1 + 1 /* VB to C# conversion */ ];
                double[] g1 = new double[p + 1 + 1/* VB to C# conversion */ ];
                double[] h1 = new double[p + 1 + 1/* VB to C# conversion */]; 
                int a1 = 0; 
                int q1 = q - r; 
                int p1 = p; 
                int r1 = r; 
                double h = 1.0 / b0; 
                double f = h; 
                f1[ 1 ] = f; 
                h1[ 1 ] = h; 
                // g = 1.0; 
                g1[ 1 ] = 1.0;

                int a2;
                do 
                { 
                    a1 = a1 + 1; 
                    q1 = q1 + 1; 
                    h = h * p1 / a1 * r1 / q1; 
                    f = f + h; 
                    a2 = a1 + 1; 
                    f1[ a2 ] = f; 
                    h1[ a2 ] = h; 
                    p1 = p1 - 1; 
                    r1 = r1 - 1; 
                } 
                while ( p1 > 0 ); 
                    
                //   UPPER TAIL PROBABILITIES WOULD BE SUBJECT TO SUBTRACTION ERRORS
                //   IF CALCULATED BY 1 - F. THEREFORE ......
                    
                double g = 0.0;
                int j;
                for ( j=a2; j >= 2; j-- ) 
                { 
                    g = g + h1[ j ]; 
                    g1[ j ] = g; 
                } 
                    
                a1 = a + 1; 
                h = 1.00000000000001 * h1[ a1 ]; 
                if ( a > e1 ) 
                { 
                        
                    g = g1[ a1 ] - h1[ a1 ] / 2.0; 
                    f = 0.0; 
                    for ( j=1; j <= a2; j++ ) 
                    { 
                        if ( h1[ j ] > h )
                        { 
                            break;
                        } 
                        f = f1[ j ] - h1[ j ] / 2.0; 
                    } 

                    /* g2 is never used.  PJC 2012/04/09.
                    double g2 = 2.0 * g; 
                    if ( g2 > 1.0 )
                        g2 = 1.0; 
                     */
                } 
                else 
                { 
                        
                    f = f1[ a1 ] - h1[ a1 ] / 2.0; 
                    g = 0.0; 
                    for ( j=a2; j >= 1; j-- ) 
                    { 
                        if ( h1[ j ] > h )
                        { 
                            break;
                        } 
                        g = g1[ j ] - h1[ j ] / 2.0; 
                    } 
                    /* f2 is never used.  PJC 2012/04/09.
                    double f2 = 2.0 * f; 
                    if ( f2 > 1.0 )
                        f2 = 1.0; 
                     */
                } 
                    
                double z = f + g; 
                if ( z > 1.0 )
                { 
                    z = 1.0; 
                } 
                prop_MidPFisher2Return = z;
            } 
            else 
            { 
                
                prop_MidPFisher2Return = Constant.MISSING; 
                
            } 
            
            return prop_MidPFisher2Return;
        } 
        
        
        public static StepResult RptMiscRetroRisk( ITemplateHost host, ParameterBag parameters ) 
        {
            double p2m = 0; 
            double p1m = 0; 
            double p2f = 0; 
            double p1f = 0; 
            double llm = 0; 
            double ulm = 0; 
            double llf; 
            double ulf; 
            double eor = 0;
            double par_ul = 0; 
            double par_ll = 0;
            double par = 0; 
            double pe = 0;

            double a = parameters[ "a" ].AsDouble; 
            double b = parameters[ "b" ].AsDouble; 
            double C = parameters[ "c" ].AsDouble; 
            double D = parameters[ "d" ].AsDouble; 
            double m1 = a + b; 
            double m2 = C + D; 
            double n1 = a + C; 
            double n2 = b + D; 
            double N = m1 + m2; 
            
            double GAMMA = parameters[ "cco" ].AsDouble; 
            if ( GAMMA <= 0.0 | GAMMA >= 1.0 )
                GAMMA = 0.95; 
            
            if ( b * C <= 0 | D * m1 <= 0 | b * m1 <= 0 | D * m2 <= 0 | C + D <= 0 ) 
                throw new InvalidDataException(); 
            
            double odr = ( a * D ) / ( b * C ); 
            
            double P = 1.0 - ( ( 1.0 - GAMMA ) / 2.0 ); 
            int fault; 
            double zp = PDF.gauinv( P, out fault ); 
            
            //  only calculate PAR for ODR > 1 because -ve PAR is meaningless
            if ( odr > 1.0 ) 
            { 
                bool wasCancelled; 
                pe = host.GetDouble( "Enter the population proportion exposed to the risk factor", "Risk Analysis (retrospective)", ( C / ( C + D ) ), out wasCancelled ); 
                if ( wasCancelled ) 
                { 
                    fault = -1; 
                } 
                else 
                { 
                    if ( pe == Constant.MISSING | pe < 0.0 | pe > 1.0 )
                    { 
                        pe = ( a + C ) / N; 
                    } 
                    par = ( pe * ( odr - 1.0 ) ) / ( 1.0 + ( pe * ( odr - 1.0 ) ) ); 
                    double var_par = ( ( b * m2 ) / ( D * m1 ) ) * ( ( b * m2 ) / ( D * m1 ) ) * ( a / ( b * m1 ) + C / ( D * m2 ) ); 
                    par_ll = par - zp * Math.Sqrt( var_par ); 
                    par_ul = par + zp * Math.Sqrt( var_par ); 
                } 
            } 
            else 
            { 
                par = Constant.MISSING; 
                par_ll = Constant.MISSING; 
                par_ul = Constant.MISSING; 
            } 
            
            if ( fault != 0 ) 
            { 
                return null; 
            } 
            ParameterBag outputParameters = new ParameterBag(); 
            //  RTF_LoadTemplate("retro_r.rtf")
            outputParameters.AddOutput( "a",  a.ToString() ); 
            outputParameters.AddOutput( "b",  b.ToString() ); 
            outputParameters.AddOutput( "c",  C.ToString() ); 
            outputParameters.AddOutput( "d",  D.ToString() );

            odr = b*C > 0 ? (a*D)/(b*C) : Constant.MISSING;
            outputParameters.AddOutput( "odds", host.RoundU( odr ) );
            bool dofish = true;
            double power = Power.fishpower( 1.0 - GAMMA, a, b, n1, n2, ref dofish ); 
            outputParameters.AddOutput( "pwr", Formatting.pwr( power, 1.0 - GAMMA ) ); 
            
            List<ParameterBag> powerList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*power", powerList ); 
            if ( b * C > 0 & a * D > 0 ) 
            { 
                ParameterBag powerParameters = new ParameterBag(); 
                powerList.Add( powerParameters ); 
                double seodr = Math.Sqrt( 1 / a + 1 / b + 1 / C + 1 / D ); 
                double yodr = Math.Log( odr ) - zp * seodr; 
                double xodr = Math.Log( odr ) + zp * seodr; 
                powerParameters.AddOutput( "ci", host.RoundU( GAMMA * 100 ) ); 
                powerParameters.AddOutput( "ci_1", host.RoundU( Math.Exp( yodr ) ) ); 
                powerParameters.AddOutput( "ci_2", host.RoundU( Math.Exp( xodr ) ) ); 
            } 
            
            if ( ( a * D != 0 ) || ( b * C != 0 ) ) 
            {
                ExactBB.Rec2x2[] tabl = new ExactBB.Rec2x2[1 + 1 /* VB to C# conversion */]; 
                tabl[ 1 ].freq = 1; 
                tabl[ 1 ].a = a; 
                tabl[ 1 ].m1 = a + b; 
                tabl[ 1 ].n1 = a + C; 
                tabl[ 1 ].n0 = b + D; 
                tabl[ 1 ].informative = ( a * D != 0 ) | ( b * C != 0 );
                bool useLogScale = false;
                int ierr;
                ExactBB.Exact22k( host, 1, 1, tabl, GAMMA, ref eor, out ulf, out llf, out ulm, out llm, ref p1f, ref p2f, ref p1m, ref p2m, ref useLogScale, out ierr ); 
            } 
            else 
            { 
                eor = Constant.MISSING; 
                llf = Constant.MISSING; 
                ulf = Constant.MISSING; 
                p1f = Constant.MISSING; 
                p2f = Constant.MISSING; 
                p1m = Constant.MISSING; 
                p2m = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "eor", host.RoundU( eor ) ); 
            outputParameters.AddOutput( "pc", Formatting.XRound( GAMMA * 100.0, 2 ) ); 
            outputParameters.AddOutput( "llf", host.RoundU( llf ) ); 
            outputParameters.AddOutput( "ulf", host.RoundU( ulf ) ); 
            outputParameters.AddOutput( "p1f", host.pval( p1f ) ); 
            outputParameters.AddOutput( "p2f", host.pval( p2f ) ); 
            outputParameters.AddOutput( "llm", host.RoundU( llm ) ); 
            outputParameters.AddOutput( "ulm", host.RoundU( ulm ) ); 
            outputParameters.AddOutput( "p1m", host.pval( p1m ) ); 
            outputParameters.AddOutput( "p2m", host.pval( p2m ) ); 
            
            List<ParameterBag> riskList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*risk", riskList ); 
            if ( par != Constant.MISSING ) 
            { 
                ParameterBag riskParameters = new ParameterBag(); 
                riskList.Add( riskParameters ); 
                riskParameters.AddOutput( "pe", host.RoundU( pe * 100.0 ) ); 
                riskParameters.AddOutput( "par", host.RoundU( par * 100.0 ) ); 
                riskParameters.AddOutput( "from", host.RoundU( par_ll * 100.0 ) ); 
                riskParameters.AddOutput( "to", host.RoundU( par_ul * 100.0 ) ); 
            } 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        private static string x_ben_harm( ITemplateHost Host, double x, bool roundup ) 
        {
            string x_ben_harmReturn = roundup ? Formatting.RoundUp( Math.Abs( x ) ) : Host.RoundU( Math.Abs( x ) ); 
            if ( x < 0 ) 
            { 
                x_ben_harmReturn = x_ben_harmReturn + "_harm"; 
            } 
            else 
            { 
                x_ben_harmReturn = x_ben_harmReturn + "_benefit"; 
            } 
            return x_ben_harmReturn;
        } 
        
        
        private static void x_nn_swap( ref double nnl, ref double nnu ) 
        { 
            double tmp; 
            if ( nnl < 0.0 & nnu < 0.0 ) 
            { 
                if ( nnl < nnu ) 
                { 
                    tmp = nnl; 
                    nnl = nnu; 
                    nnu = tmp; 
                } 
            } 
            else 
            { 
                if ( nnl > nnu ) 
                { 
                    tmp = nnl; 
                    nnl = nnu; 
                    nnu = tmp; 
                } 
            } 
        } 
        
        
        public static StepResult RptMiscDiagnostic( ITemplateHost host, ParameterBag parameters ) 
        { 
            double lrneg; 
            double thetau; 
            double thetal; 
            double lrpos;
            double speci; 
            double sensi; 
            double ptlnd; 
            double ptlng; 
            double temp2; 
            double temp1; 
            double ptld; 
            double piu; 
            double pil;
            double eor = 0; double ulf; double llf; double ulm; double llm; double p1f = 0; double p2f = 0; double p1m = 0; double p2m = 0; 
            int fault; 
            string warn;

            double a = parameters[ "a" ].AsDouble; 
            double b = parameters[ "b" ].AsDouble; 
            double C = parameters[ "c" ].AsDouble; 
            double D = parameters[ "d" ].AsDouble; 
            double N = a + b + C + D; 
            double cco = parameters[ "cco" ].AsDouble; 
            if ( cco <= 0.0 | cco >= 1.0 )
            { 
                cco = 0.95; 
            } 
            
            if ( N <= 0.0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            
            //  RTF_LoadTemplate("diagnost.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "a",  a.ToString() ); 
            outputParameters.AddOutput( "b",  b.ToString() ); 
            outputParameters.AddOutput( "ab",  a + b.ToString() ); 
            
            outputParameters.AddOutput( "c",  C.ToString() ); 
            outputParameters.AddOutput( "d",  D.ToString() ); 
            outputParameters.AddOutput( "cd",  C + D.ToString() ); 
            
            outputParameters.AddOutput( "ac",  a + C.ToString() ); 
            outputParameters.AddOutput( "bd",  b + D.ToString() ); 
            outputParameters.AddOutput( "tot",  N.ToString() ); 
            
            // CI level
            outputParameters.AddOutput( "pc", Formatting.XRound( cco * 100.0, 0 ) ); 
            
            // prevalence
            double prevel = ( a + C ) / N; 
            outputParameters.AddOutput( "prevalence", host.RoundU( prevel ) ); 
            // Clopper-Pearson CI
            MathDbl.binci( a + C, N, out pil, out piu, cco, out warn ); 
            outputParameters.AddOutput( "prevalence_from", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "prevalence_to", host.RoundU( piu ) + warn ); 
            // as percentage
            outputParameters.AddOutput( "prevalence_pc", Formatting.XRound( prevel * 100.0, 2 ) ); 
            if ( pil != Constant.MISSING )
            { 
                pil = 100.0 * pil; 
            } else { pil = Constant.MISSING; } 
            outputParameters.AddOutput( "prevalence_from_pc", Formatting.XRound( pil, 2 ) ); 
            if ( piu != Constant.MISSING )
            { 
                piu = 100.0 * piu; 
            } else { piu = Constant.MISSING; } 
            outputParameters.AddOutput( "prevalence_to_pc", Formatting.XRound( piu, 2 ) ); 
            
            // ppv
            if ( a + b > 0.0 ) 
            { 
                ptld = a / ( a + b ); 
                temp1 = ptld * 100.0; 
                temp2 = Convert.ToInt64( ptld * 100.0 ) - Convert.ToInt64( prevel * 100.0 ); 
            } 
            else 
            { 
                ptld = Constant.MISSING; 
                temp1 = Constant.MISSING; 
                temp2 = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "likely", host.RoundU( ptld ) ); 
            // Clopper-Pearson CI
            MathDbl.binci( a, a + b, out pil, out piu, cco, out warn ); 
            outputParameters.AddOutput( "likely_from", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "likely_to", host.RoundU( piu ) + warn ); 
            // as percentage
            outputParameters.AddOutput( "likely_pc", Formatting.XRound( temp1, 2 ) ); 
            if ( pil != Constant.MISSING )
            { 
                pil = 100.0 * pil; 
            } else { pil = Constant.MISSING; } 
            outputParameters.AddOutput( "likely_from_pc", Formatting.XRound( pil, 2 ) ); 
            if ( piu != Constant.MISSING )
            { 
                piu = 100.0 * piu; 
            } else { piu = Constant.MISSING; } 
            outputParameters.AddOutput( "likely_to_pc", Formatting.XRound( piu, 2 ) ); 
            // change
            outputParameters.AddOutput( "likely_change", Formatting.XRound( temp2, 2 ) ); 
            
            // npv
            if ( D + C > 0.0 ) 
            { 
                ptlng = D / ( D + C ); 
                temp1 = ptlng * 100.0; 
                temp2 = Convert.ToInt64( ptlng * 100.0 ) - Convert.ToInt64( ( ( b + D ) / N ) * 100.0 ); 
            } 
            else 
            { 
                ptlng = Constant.MISSING; 
                temp1 = Constant.MISSING; 
                temp2 = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "likely_negative", host.RoundU( ptlng ) ); 
            // Clopper-Pearson CI
            MathDbl.binci( D, D + C, out pil, out piu, cco, out warn ); 
            outputParameters.AddOutput( "likely_negative_from", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "likely_negative_to", host.RoundU( piu ) + warn ); 
            // as percentage
            outputParameters.AddOutput( "likely_negative_pc", Formatting.XRound( temp1, 2 ) ); 
            if ( pil != Constant.MISSING )
            { 
                pil = 100.0 * pil; 
            } else { pil = Constant.MISSING; } 
            outputParameters.AddOutput( "likely_negative_from_pc", Formatting.XRound( pil, 2 ) ); 
            if ( piu != Constant.MISSING )
            { 
                piu = 100.0 * piu; 
            } else { piu = Constant.MISSING; } 
            outputParameters.AddOutput( "likely_negative_to_pc", Formatting.XRound( piu, 2 ) ); 
            // change
            outputParameters.AddOutput( "likely_negative_change", Formatting.XRound( temp2, 2 ) ); 
            
            // p[dx] despite -ve test
            if ( D + C > 0.0 ) 
            { 
                ptlnd = 1.0 - ( D / ( D + C ) ); 
                temp1 = ptlnd * 100.0; 
                temp2 = Convert.ToInt64( ptlnd * 100.0 ) - Convert.ToInt64( prevel * 100.0 ); 
            } 
            else 
            { 
                ptlnd = Constant.MISSING; 
                temp1 = Constant.MISSING; 
                temp2 = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "likely_despite", host.RoundU( ptlnd ) ); 
            // Clopper-Pearson CI
            MathDbl.binci( D, D + C, out pil, out piu, cco, out warn ); 
            outputParameters.AddOutput( "likely_despite_from", host.RoundU( Math.Min( 1.0 - pil, 1.0 - piu ) ) ); 
            outputParameters.AddOutput( "likely_despite_to", host.RoundU( Math.Max( 1.0 - pil, 1.0 - piu ) ) + warn ); 
            // as percentage
            outputParameters.AddOutput( "likely_despite_pc", Formatting.XRound( temp1, 2 ) ); 
            if ( pil != Constant.MISSING )
            { 
                pil = 100.0 * ( 1.0 - pil ); 
            } else { pil = Constant.MISSING; } 
            if ( piu != Constant.MISSING )
            { 
                piu = 100.0 * ( 1.0 - piu ); 
            } else { piu = Constant.MISSING; } 
            outputParameters.AddOutput( "likely_despite_from_pc", Formatting.XRound( Math.Min( pil, piu ), 2 ) ); 
            outputParameters.AddOutput( "likely_despite_to_pc", Formatting.XRound( Math.Max( pil, piu ), 2 ) ); 
            // change
            outputParameters.AddOutput( "likely_despite_change", Formatting.XRound( temp2, 2 ) ); 
            
            // sensitivity
            if ( a + C > 0.0 ) 
            { 
                sensi = a / ( a + C ); 
                temp1 = 100.0 * sensi; 
            } 
            else 
            { 
                sensi = Constant.MISSING; 
                temp1 = Constant.MISSING; 
            } 
            // Clopper-Pearson CI for sensitivity
            outputParameters.AddOutput( "sensitive", host.RoundU( sensi ) ); 
            MathDbl.binci( a, a + C, out pil, out piu, cco, out warn ); 
            outputParameters.AddOutput( "sensitive_from", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "sensitive_to", host.RoundU( piu ) + warn ); 
            // as percentage
            outputParameters.AddOutput( "sensitive_pc", Formatting.XRound( temp1, 2 ) ); 
            if ( pil != Constant.MISSING )
            { 
                pil = 100.0 * pil; 
            } else { pil = Constant.MISSING; } 
            outputParameters.AddOutput( "sensitive_from_pc", Formatting.XRound( pil, 2 ) ); 
            if ( piu != Constant.MISSING )
            { 
                piu = 100.0 * piu; 
            } else { piu = Constant.MISSING; } 
            outputParameters.AddOutput( "sensitive_to_pc", Formatting.XRound( piu, 2 ) ); 
            
            // specificity
            if ( D + b > 0.0 ) 
            { 
                speci = D / ( D + b ); 
                temp1 = 100.0 * speci; 
            } 
            else 
            { 
                speci = Constant.MISSING; 
                temp1 = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "specific", host.RoundU( speci ) ); 
            // Clopper-Pearson CI for specificity
            MathDbl.binci( D, D + b, out pil, out piu, cco, out warn ); 
            outputParameters.AddOutput( "specific_from", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "specific_to", host.RoundU( piu ) + warn ); 
            // as percentage
            outputParameters.AddOutput( "specific_pc", Formatting.XRound( temp1, 2 ) ); 
            if ( pil != Constant.MISSING )
            { 
                pil = 100.0 * pil; 
            } else { pil = Constant.MISSING; } 
            outputParameters.AddOutput( "specific_from_pc", Formatting.XRound( pil, 2 ) ); 
            if ( piu != Constant.MISSING )
            { 
                piu = 100.0 * piu; 
            } else { piu = Constant.MISSING; } 
            outputParameters.AddOutput( "specific_to_pc", Formatting.XRound( piu, 2 ) ); 
            
            // + likelihood ratio with CI
            double zc = 1.0 - ( ( 1.0 - cco ) / 2.0 ); 
            // fault = 0; 
            zc = PDF.gauinv( zc, out fault ); 
            if ( b + D > 0.0 & a + C > 0.0 & b > 0.0 & ptld > 0.0 )
            {
                double abpos = b / ( b + D );
                // presneg = C / ( a + C ); 
                lrpos = sensi / abpos;
            }
            else 
            { 
                lrpos = Constant.MISSING; 
            } 
            MathDbl.lr_ci( b, a, b + D, a + C, zc, out thetal, out thetau ); 
            outputParameters.AddOutput( "lr_pos", host.RoundU( lrpos ) ); 
            outputParameters.AddOutput( "lr_pos_from", host.RoundU( thetal ) ); 
            outputParameters.AddOutput( "lr_pos_to", host.RoundU( thetau ) ); 
            
            // - likelihood ratio with CI
            if ( b + D > 0.0 & a + C > 0.0 & speci > 0.0 & ptlnd > 0.0 )
            {
                // abpos = b / ( b + D ); 
                double presneg = C / ( a + C );
                lrneg = presneg / speci;
            }
            else 
            { 
                lrneg = Constant.MISSING; 
            } 
            MathDbl.lr_ci( D, C, b + D, a + C, zc, out thetal, out thetau ); 
            outputParameters.AddOutput( "lr_neg", host.RoundU( lrneg ) ); 
            outputParameters.AddOutput( "lr_neg_from", host.RoundU( thetal ) ); 
            outputParameters.AddOutput( "lr_neg_to", host.RoundU( thetau ) ); 
            
            // diagnostic odds ratio
            ExactBB.Rec2x2[] tabl = new ExactBB.Rec2x2[1 + 1 /* VB to C# conversion */]; 
            tabl[ 1 ].freq = 1; 
            tabl[ 1 ].a = a; 
            tabl[ 1 ].m1 = a + b; 
            tabl[ 1 ].n1 = a + C; 
            tabl[ 1 ].n0 = b + D; 
            tabl[ 1 ].informative = ( a * D != 0 ) | ( b * C != 0 );
            bool useLogScale = false;
            ExactBB.Exact22k( host, 1, 1, tabl, cco, ref eor, out ulf, out llf, out ulm, out llm, ref p1f, ref p2f, ref p1m, ref p2m, ref useLogScale, out fault ); 
            if ( fault != 0 ) 
            { 
                // eor = Constant.MISSING; 
                ulf = Constant.MISSING; 
                llf = Constant.MISSING; 
                // ulm = Constant.MISSING; 
                // llm = Constant.MISSING; 
                // p1f = Constant.MISSING; 
                // p2f = Constant.MISSING; 
                // p1m = Constant.MISSING; 
                // p2m = Constant.MISSING; 
            } 
            if ( b * C > 0.0 && a * D > 0.0 ) 
                eor = ( a * D ) / ( b * C ); 
            else 
                eor = Constant.MISSING; 
            outputParameters.AddOutput( "odr", host.RoundU( eor ) ); 
            outputParameters.AddOutput( "odr_from", host.RoundU( llf ) ); 
            outputParameters.AddOutput( "odr_to", host.RoundU( ulf ) ); 
            
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptMiscFalseResult( ITemplateHost host, ParameterBag parameters ) 
        {
            double pt = parameters[ "pt" ].AsDouble; 
            if ( Math.Abs( pt - 0.5 ) >= 0.5 ) 
            { 
                throw new InvalidDataException(); 
            } 
            double pf = parameters[ "pf" ].AsDouble; 
            if ( Math.Abs( pf - 0.5 ) >= 0.5 ) 
            { 
                throw new InvalidDataException(); 
            } 
            
            double pd = 1.0 / parameters[ "pd" ].AsDouble; 
            
            //  RTF_LoadTemplate("falser.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            
            outputParameters.AddOutput( "population",  (pd * 10000).ToString() ); 
            
            double PP = ( pf * ( 1 - pd ) ) / ( pf + pd * ( pt - pf ) ); 
            double pn = ( ( 1.0 - pt ) * pd ) / ( 1.0 - pf - pd * ( pt - pf ) ); 
            
            outputParameters.AddOutput( "sensitive", host.RoundU( pt * 100 ) ); 
            outputParameters.AddOutput( "positive", host.RoundU( PP ) ); 
            
            outputParameters.AddOutput( "specific", host.RoundU( ( 1 - pf ) * 100 ) ); 
            outputParameters.AddOutput( "negative", host.RoundU( pn ) ); 
            
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptKappaScreen( ITemplateHost host, ParameterBag parameters ) 
        { 
            double cco = parameters[ "ci" ].AsDouble; 
            if ( cco <= 0.0 | cco >= 1.0 )
            { 
                cco = 0.95; 
            } 
            
            string wtypeString = parameters[ "method" ].AsString;
            int wtype = "1".Equals( wtypeString ) ? 1 : 2; 
            int fault; 
            double cit = PDF.gauinv( cco + ( 1.0 - cco ) / 2.0, out fault ); 
            if ( fault != 0 ) 
            { 
                return null; 
            } 
            DataFrame datFrame = parameters[ "responsesCrosstab" ].AsDataFrame; 
            int rows = datFrame.MaxRows; 
            int cols = datFrame.VariableCount; 
            int g = Math.Max( rows, cols );
            double[,] o = new double[g - 1 + 1 /* for VB to C# conversion */, g - 1 + 1 /* for VB to C# conversion */];
            double[,] w = new double[g - 1 + 1 /* for VB to C# conversion */, g - 1 + 1 /* for VB to C# conversion */]; 
            
            for ( int i=0; i <= g - 1; i++ ) 
            { 
                for ( int j=0; j <= g - 1; j++ ) 
                { 
                    o[ i, j ] = 0.0; 
                    w[ i, j ] = 0.0; 
                } 
            } 
            
            for ( int i=0; i <= rows - 1; i++ ) 
            { 
                for ( int j=0; j <= cols - 1; j++ ) 
                { 
                    o[ i, j ] = datFrame.Variables[ j ].AsDoubleVariable.Data[i]; 
                } 
            } 
            
            
            for ( int i=0; i <= g - 1; i++ ) 
            { 
                for ( int j=0; j <= g - 1; j++ ) 
                { 
                    switch ( wtype ) 
                    {
                        case 2:
                            w[ i, j ] = 1 - Math.Pow( ( Convert.ToDouble( i - j ) / Convert.ToDouble( g - 1 ) ), 2.0 ); 
                            break;
                        default:
                            w[ i, j ] = 1 - Convert.ToDouble( Math.Abs( i - j ) ) / Convert.ToDouble( g - 1 ); 
                            break;
                    }
                    
                } 
            } 
            
            double k = 0; 
            double sek = 0; 
            double kcil = 0; 
            double kciu = 0; 
            double kw = 0; 
            double sekw = 0; 
            double kwcil = 0; 
            double kwciu = 0; 
            double po = 0; 
            double pe = 0; 
            double pow = 0; 
            double pew = 0; 
            double spe = 0; 
            double spi = 0; 
            bool ierror; 
            Tables.Kappa( host, o, w, g, ref k, ref sek, ref kcil, ref kciu, ref kw, ref sekw, ref kwcil, ref kwciu, ref po, ref pe, ref pow, ref pew, ref cit, ref spe, ref spi, out ierror ); 
            if ( ierror ) 
            { 
                return null; 
            } 
            
            //  RTF_LoadTemplate("kappa.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "po", Formatting.XRound( po * 100, 2 ) ); 
            outputParameters.AddOutput( "pe", Formatting.XRound( pe * 100, 2 ) ); 
            outputParameters.AddOutput( "kappa", host.RoundU( k ) ); 
            outputParameters.AddInput( "kDouble", k ); 
            outputParameters.AddOutput( "se", host.RoundU( sek ) ); 
            outputParameters.AddOutput( "pc", host.RoundU( cco * 100 ) ); 
            outputParameters.AddOutput( "from", host.RoundU( kcil ) ); 
            outputParameters.AddOutput( "to", host.RoundU( kciu ) );
            double z = sek != 0.0 ? k/sek : Constant.MISSING;
            outputParameters.AddOutput( "z", host.RoundU( z ) );
            double P = z != Constant.MISSING ? 1.0 - PDF.alnorm(z) : Constant.MISSING;
            outputParameters.AddOutput( "p", host.pval( P ) ); 
            switch ( wtype ) 
            {
                case 3:
                    outputParameters.AddOutput( "methodName", "user defined" ); 
                    break;
                case 2:
                    outputParameters.AddOutput( "methodName", "1-[(i-j)/(1-k)]²" ); 
                    break;
                case 1:
                    outputParameters.AddOutput( "methodName", "1-abs(i-j)/(1-k)" ); 
                    break;
            }
            
            List<ParameterBag> weightsList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*weights", weightsList ); 
            for ( int i=1; i <= g; i++ ) 
            { 
                ParameterBag weightsParameters = new ParameterBag(); 
                weightsList.Add( weightsParameters ); 
                List<ParameterBag> totList = new List<ParameterBag>(); 
                weightsParameters.AddOutput( "*tot", totList ); 
                for ( int j=1; j <= g; j++ ) 
                { 
                    ParameterBag totParameters = new ParameterBag(); 
                    totList.Add( totParameters ); 
                    totParameters.AddOutput( "tot", Formatting.XRound( w[ i - 1, j - 1 ], host.PDecimalPlaces ) ); 
                } 
            } 
            outputParameters.AddOutput( "pow", Formatting.XRound( pow * 100, 2 ) ); 
            outputParameters.AddOutput( "pew", Formatting.XRound( pew * 100, 2 ) ); 
            outputParameters.AddOutput( "kappaw", host.RoundU( kw ) ); 
            outputParameters.AddInput( "kwDouble", kw ); 
            outputParameters.AddOutput( "sekw", host.RoundU( sekw ) ); 
            outputParameters.AddOutput( "pcw", Formatting.XRound( cco * 100, 1 ) ); 
            outputParameters.AddOutput( "fromw", host.RoundU( kwcil ) ); 
            outputParameters.AddOutput( "tow", host.RoundU( kwciu ) );
            double zw = sekw != 0.0 ? kw/sekw : Constant.MISSING;
            outputParameters.AddOutput( "zw", host.RoundU( zw ) );
            P = zw != Constant.MISSING ? 1.0 - PDF.alnorm(zw) : Constant.MISSING;
            outputParameters.AddOutput( "pw", host.pval( P ) ); 
            
            outputParameters.AddOutput( "pocopy", Formatting.XRound( po * 100, 2 ) ); 
            outputParameters.AddOutput( "spe", Formatting.XRound( spe * 100, 2 ) ); 
            outputParameters.AddOutput( "spi", host.RoundU( spi ) ); 
            List<ParameterBag> deciList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*deci", deciList ); 
            if ( g == 2 ) 
            { 
                double ka = 0; 
                double lwr = 0; 
                double upr = 0; 
                Tables.x_kappa_ci_22( Convert.ToInt32( o[ 0, 0 ] ), Convert.ToInt32( o[ 0, 1 ] + o[ 1, 0 ] ), Convert.ToInt32( o[ 1, 1 ] ), cit, ref ka, ref lwr, ref upr, out fault ); 
                if ( fault == 0 ) 
                { 
                    ParameterBag deciParameters = new ParameterBag(); 
                    deciList.Add( deciParameters ); 
                    deciParameters.AddOutput( "pc", Formatting.XRound( cco * 100, 1 ) ); 
                    deciParameters.AddOutput( "lwr", host.RoundU( lwr ) ); 
                    deciParameters.AddOutput( "upr", host.RoundU( upr ) ); 
                } 
            } 
            
            // Maxwell's test
            double x2 = 0; 
            double x2m = 0; 
            int dfm; 
            Tables.maxwell( o, g, ref x2, ref x2m, out dfm ); 
            if ( x2 == Constant.MISSING ) 
            { 
                outputParameters.AddOutput( "x2", host.RoundU( x2 ) ); 
                outputParameters.AddOutput( "df", host.RoundU( x2 ) ); 
                outputParameters.AddOutput( "pmaxwell", host.RoundU( x2 ) ); 
            } 
            else 
            { 
                outputParameters.AddOutput( "x2", host.RoundU( x2 ) ); 
                outputParameters.AddOutput( "df",  (g - 1).ToString() ); 
                outputParameters.AddOutput( "pmaxwell", host.pval( PDF.chivalp( x2, Convert.ToDouble( g - 1 ) ) ) ); 
            } 
            // general McNemar
            if ( x2m == Constant.MISSING ) 
            { 
                outputParameters.AddOutput( "x2m", host.RoundU( x2m ) ); 
                outputParameters.AddOutput( "dfmcnemar", host.RoundU( x2m ) ); 
                outputParameters.AddOutput( "pmcnemar", host.RoundU( x2m ) ); 
            } 
            else 
            { 
                outputParameters.AddOutput( "x2m", host.RoundU( x2m ) ); 
                outputParameters.AddOutput( "dfmcnemar",  dfm.ToString() ); 
                outputParameters.AddOutput( "pmcnemar", host.pval( PDF.chivalp( x2m, Convert.ToDouble( dfm ) ) ) ); 
            } 
            
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptMiscLikely( ITemplateHost Host, ParameterBag Parameters ) 
        {
            double c2tot = 0; double c1tot = 0; 
            int i;

            DataFrame datFrame = Parameters[ "data" ].AsDataFrame; 
            DoubleVariable datV0 = datFrame.Variables[ 0 ].AsDoubleVariable; 
            DoubleVariable datV1 = datFrame.Variables[ 1 ].AsDoubleVariable; 
            int rows = datFrame.MaxRows;

            double[] C1 = new double[rows + 1 /* VB to C# conversion */ ];
            double[] C2 = new double[rows + 1 /* VB to C# conversion */ ]; 
            
            for ( i=1; i <= rows; i++ ) 
            { 
                double rtd = datV0.Data[i - 1]; 
                C1[ i ] = rtd; 
                
                rtd = datV1.Data[i - 1]; 
                C2[ i ] = rtd; 
                
                c1tot = c1tot + C1[ i ]; 
                c2tot = c2tot + C2[ i ]; 
            } 
            
            if ( c1tot <= 0 | c2tot <= 0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            
            double zl = Parameters[ "z1" ].AsDouble; 
            if ( zl <= 0.0 | zl >= 1.0 )
            { 
                zl = 0.95; 
            } 
            double zc = 1.0 - ( ( 1.0 - zl ) / 2.0 ); 
            int fault; 
            zc = PDF.gauinv( zc, out fault ); 
            
            //  RTF_LoadTemplate("likely.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "pc",  (100 * zl).ToString() ); 
            
            List<ParameterBag> rowList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*row", rowList ); 
            for ( i=1; i <= rows; i++ ) 
            { 
                ParameterBag rowParameters = new ParameterBag(); 
                rowList.Add( rowParameters ); 
                rowParameters.AddOutput( "result",  i.ToString() ); 
                rowParameters.AddOutput( "+feature",  C1[ i ].ToString() ); 
                rowParameters.AddOutput( "-feature",  C2[ i ].ToString() );

                double li;
                if ( c1tot <= 0.0 | C2[ i ] <= 0.0 | c2tot <= 0.0 ) 
                { 
                    li = Constant.MISSING; 
                } 
                else 
                { 
                    li = ( C1[ i ] / c1tot ) / ( C2[ i ] / c2tot ); 
                } 
                
                rowParameters.AddOutput( "likely", Host.RoundU( li ) );

                double thetau;
                double thetal;
                MathDbl.lr_ci( C2[ i ], C1[ i ], c2tot, c1tot, zc, out thetal, out thetau ); 
                rowParameters.AddOutput( "from", Host.RoundU( thetal ) ); 
                rowParameters.AddOutput( "to", Host.RoundU( thetau ) ); 
            } 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptMiscNumberNeededToTreat( ITemplateHost host, ParameterBag parameters ) 
        {
            double tmp;
            double rre; double rrel; double rreu; double rrne; double rrnel; double rrneu;
            double cl; double cu;
            double eor = 0; double ulf; double llf; double ulm; double llm; double p1f = 0; double p2f = 0; double p1m = 0; double p2m = 0; 
            int ierr;
            string warn;

            double nt = parameters[ "nt" ].AsDouble; 
            double xt = parameters[ "xt" ].AsDouble; 
            double nc = parameters[ "nc" ].AsDouble; 
            double xc = parameters[ "xc" ].AsDouble; 
            
            if ( nt < xt ) 
            { 
                tmp = xt; 
                xt = nt; 
                nt = tmp; 
            } 
            if ( nc < xc ) 
            { 
                tmp = xc; 
                xc = nc; 
                nc = tmp; 
            } 
            
            double t1 = xt; 
            double t2 = nt - xt; 
            double t3 = xc; 
            double t4 = nc - xc; 
            
            if ( nc < 1.0 || nt < 1.0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            double zl = parameters[ "cco" ].AsDouble; 
            if ( zl <= 0.0 | zl >= 1.0 )
            { 
                zl = 0.95; 
            } 
            double zc = 1.0 - ( ( 1.0 - zl ) / 2.0 );
            int ifault;
            zc = PDF.gauinv( zc, out ifault ); 
            if ( xc > nc ) 
            { 
                tmp = xc; 
                xc = nc; 
                nc = tmp; 
                parameters[ "nc" ] = new FilledParameter( true, nc ); 
                parameters[ "xc" ] = new FilledParameter( true, xc ); 
            } 
            double pc = xc / nc; 
            if ( xt > nt ) 
            { 
                tmp = xt; 
                xt = nt; 
                nt = tmp; 
                parameters[ "nt" ] = new FilledParameter( true, nt ); 
                parameters[ "xt" ] = new FilledParameter( true, xt ); 
            } 
            double pt = xt / nt; 
            
            //  RTF_LoadTemplate("treat.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "pc", Formatting.XRound( 100.0 * zl, 2 ) ); 
            
            outputParameters.AddOutput( "ce",  xc.ToString() + "/" +  nc.ToString() + " = " + host.RoundU( pc ) ); 
            MathDbl.binci( xc, nc, out cl, out cu, zl, out warn ); 
            outputParameters.AddOutput( "ce_from", host.RoundU( cl ) ); 
            outputParameters.AddOutput( "ce_to", host.RoundU( cu ) + warn ); 
            outputParameters.AddOutput( "te",  xt.ToString() + "/" +  nt.ToString() + " = " + host.RoundU( pt ) ); 
            MathDbl.binci( xt, nt, out cl, out cu, zl, out warn ); 
            outputParameters.AddOutput( "te_from", host.RoundU( cl ) ); 
            outputParameters.AddOutput( "te_to", host.RoundU( cu ) + warn ); 
            MathDbl.lr_ci( xc, xt, nc, nt, zc, out rrel, out rreu ); 
            if ( rrel > rreu ) 
            { 
                tmp = rrel; 
                rrel = rreu; 
                rreu = tmp; 
            }
            rre = pc != 0 ? pt/pc : double.PositiveInfinity;
            outputParameters.AddOutput( "rre", host.RoundU( rre ) ); 
            outputParameters.AddOutput( "rre_from", host.RoundU( rrel ) ); 
            outputParameters.AddOutput( "rre_to", host.RoundU( rreu ) ); 
            
            outputParameters.AddOutput( "cne",  (nc - xc).ToString() + "/" +  nc.ToString() + " = " + host.RoundU( 1.0 - pc ) ); 
            MathDbl.binci( nc - xc, nc, out cl, out cu, zl, out warn ); 
            outputParameters.AddOutput( "cne_from", host.RoundU( cl ) ); 
            outputParameters.AddOutput( "cne_to", host.RoundU( cu ) + warn ); 
            outputParameters.AddOutput( "tne",  (nt - xt).ToString() + "/" +  nt.ToString() + " = " + host.RoundU( 1.0 - pt ) ); 
            MathDbl.binci( nt - xt, nt, out cl, out cu, zl, out warn ); 
            outputParameters.AddOutput( "tne_from", host.RoundU( cl ) ); 
            outputParameters.AddOutput( "tne_to", host.RoundU( cu ) + warn ); 
            MathDbl.lr_ci( nc - xc, nt - xt, nc, nt, zc, out rrnel, out rrneu ); 
            if ( rrnel > rrneu ) 
            { 
                tmp = rrnel; 
                rrnel = rrneu; 
                rrneu = tmp; 
            }
            rrne = pc != 1.0 ? (1.0 - pt)/(1.0 - pc) : double.PositiveInfinity;
            outputParameters.AddOutput( "rrne", host.RoundU( rrne ) ); 
            outputParameters.AddOutput( "rrne_from", host.RoundU( rrnel ) ); 
            outputParameters.AddOutput( "rrne_to", host.RoundU( rrneu ) );

            ExactBB.Rec2x2[] tabl = new ExactBB.Rec2x2[1 + 1 /* VB to C# conversion */]; 
            tabl[ 1 ].freq = 1; 
            tabl[ 1 ].a = t1; 
            tabl[ 1 ].m1 = t1 + t2; 
            tabl[ 1 ].n1 = t1 + t3; 
            tabl[ 1 ].n0 = t2 + t4; 
            tabl[ 1 ].informative = ( t1 * t4 != 0 ) | ( t2 * t3 != 0 );
            bool useLogScale = false;
            ExactBB.Exact22k( host, 1, 1, tabl, zl, ref eor, out ulf, out llf, out ulm, out llm, ref p1f, ref p2f, ref p1m, ref p2m, ref useLogScale, out ierr ); 
            if ( ierr != 0 )
                eor = Constant.MISSING;
            double oor = t2*t3 == 0.0 ? Constant.MISSING : (t1*t4)/(t2*t3);
            outputParameters.AddOutput( "oor", host.RoundU( oor ) ); 
            outputParameters.AddOutput( "oor_from", host.RoundU( llf ) ); 
            outputParameters.AddOutput( "oor_to", host.RoundU( ulf ) );

            double rrr = pc != 0.0 ? (pc - pt)/pc : 0.0;
            double rrrl = rrel != Constant.MISSING ? 1.0 - rrel : Constant.MISSING;
            double rrru = rreu != Constant.MISSING ? 1.0 - rreu : Constant.MISSING;
            if ( rrrl > rrru ) 
            { 
                tmp = rrrl; 
                rrrl = rrru; 
                rrru = tmp; 
            } 
            outputParameters.AddOutput( "rrr", host.RoundU( rrr ) ); 
            outputParameters.AddOutput( "rrr_from", host.RoundU( rrrl ) ); 
            outputParameters.AddOutput( "rrr_to", host.RoundU( rrru ) ); 
            
            double rd = pc - pt; 
            MathDbl.uppci( Convert.ToInt32( xc ), Convert.ToInt32( nc ), Convert.ToInt32( xt ), Convert.ToInt32( nt ), out cl, out cu, zc, 100.0 * zl ); 
            double rdl = cl; 
            double rdu = cu; 
            if ( rdl > rdu ) 
            { 
                tmp = rdl; 
                rdl = rdu; 
                rdu = tmp; 
            } 
            outputParameters.AddOutput( "rd", host.RoundU( rd ) ); 
            outputParameters.AddOutput( "rd_from", host.RoundU( rdl ) ); 
            outputParameters.AddOutput( "rd_to", host.RoundU( rdu ) ); 
            
            // NNT_risk difference
            double nnt = rd != 0.0 ? 1.0/rd : double.PositiveInfinity;
            double nnl = rdl != 0.0 ? 1.0/rdl : double.PositiveInfinity;
            double nnu = rdu != 0.0 ? 1.0/rdu : double.PositiveInfinity;

            // Jan 02 change to benefit/harm notation
            // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
            x_nn_swap( ref nnl, ref nnu ); 
            outputParameters.AddOutput( "treat", x_ben_harm( host, nnt, false ) ); 
            outputParameters.AddOutput( "treat_from", x_ben_harm( host, nnl, false ) ); 
            outputParameters.AddOutput( "treat_to", x_ben_harm( host, nnu, false ) ); 
            outputParameters.AddOutput( "treat_round", x_ben_harm( host, nnt, true ) ); 
            outputParameters.AddOutput( "treat_round_from", x_ben_harm( host, nnl, true ) ); 
            outputParameters.AddOutput( "treat_round_to", x_ben_harm( host, nnu, true ) ); 
            // <--
            
            // **************************************************************************************
            // substitute external baseline event rate (brr) for control event rate (pc) if brr given
            bool hasBrr = parameters.ContainsKey( "brr" ) && parameters[ "brr" ] != null; 
            List<ParameterBag> adjustedList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*adjusted", adjustedList ); 
            if ( hasBrr ) 
            { 
                ParameterBag adjustedParameters = new ParameterBag(); 
                adjustedList.Add( adjustedParameters ); 
                double brr = parameters[ "brr" ].AsDouble;
                string brt;
                if ( brr < 0.0 | brr > 1.0 ) 
                { 
                    if ( brr > 1.0 & brr < 100.0 ) 
                    { 
                        brr = brr / 100.0; 
                        brt = "(from percentage) "; 
                    } 
                    else 
                    { 
                        brt = "(reset to control event rate) "; 
                        brr = pc; 
                    } 
                } 
                else 
                { 
                    brt = ""; 
                } 
                adjustedParameters.AddOutput( "type", brt ); 
                adjustedParameters.AddOutput( "brr", Formatting.XRound( 100.0 * brr, 2 ) ); 
                
                adjustedParameters.AddOutput( "pc", Formatting.XRound( 100.0 * zl, 2 ) ); 
                
                // NNT_risk difference
                if ( rd != 0.0 )
                { 
                    nnt = 1.0 / rd; 
                } else { nnt = double.PositiveInfinity; } 
                if ( rdl != 0.0 )
                { 
                    nnl = 1.0 / rdl; 
                } else { nnl = double.PositiveInfinity; } 
                if ( rdu != 0.0 )
                { 
                    nnu = 1.0 / rdu; 
                } else { nnu = double.PositiveInfinity; } 
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                x_nn_swap( ref nnl, ref nnu ); 
                adjustedParameters.AddOutput( "rd_treat", x_ben_harm( host, nnt, false ) ); 
                adjustedParameters.AddOutput( "rd_treat_from", x_ben_harm( host, nnl, false ) ); 
                adjustedParameters.AddOutput( "rd_treat_to", x_ben_harm( host, nnu, false ) ); 
                adjustedParameters.AddOutput( "rd_treat_round", x_ben_harm( host, nnt, true ) ); 
                adjustedParameters.AddOutput( "rd_treat_round_from", x_ben_harm( host, nnl, true ) ); 
                adjustedParameters.AddOutput( "rd_treat_round_to", x_ben_harm( host, nnu, true ) ); 
                // <--
                
                // NNT_risk ratio of event
                double D = brr * rrr; 
                if ( D != 0.0 )
                { 
                    nnt = 1.0 / D; 
                } else { nnt = double.PositiveInfinity; } 
                D = brr * rrrl; 
                if ( D != 0.0 )
                { 
                    nnl = 1.0 / D; 
                } else { nnl = double.PositiveInfinity; } 
                D = brr * rrru; 
                if ( D != 0.0 )
                { 
                    nnu = 1.0 / D; 
                } else { nnu = double.PositiveInfinity; } 
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                x_nn_swap( ref nnl, ref nnu ); 
                adjustedParameters.AddOutput( "rr_treat", x_ben_harm( host, nnt, false ) ); 
                adjustedParameters.AddOutput( "rr_treat_from", x_ben_harm( host, nnl, false ) ); 
                adjustedParameters.AddOutput( "rr_treat_to", x_ben_harm( host, nnu, false ) ); 
                adjustedParameters.AddOutput( "rr_treat_round", x_ben_harm( host, nnt, true ) ); 
                adjustedParameters.AddOutput( "rr_treat_round_from", x_ben_harm( host, nnl, true ) ); 
                adjustedParameters.AddOutput( "rr_treat_round_to", x_ben_harm( host, nnu, true ) ); 
                // <--
                
                // NNT_risk ratio of no event
                // Sally Hollis pointed out not (1-brr) * (1-rrr) as given by Jon Deeks
                D = ( 1.0 - brr ) * ( rrne - 1.0 ); 
                if ( D != 0.0 )
                { 
                    nnt = 1.0 / D; 
                } else { nnt = double.PositiveInfinity; } 
                D = ( 1.0 - brr ) * ( rrnel - 1.0 ); 
                if ( D != 0.0 )
                { 
                    nnl = 1.0 / D; 
                } else { nnl = double.PositiveInfinity; } 
                D = ( 1.0 - brr ) * ( rrneu - 1.0 ); 
                if ( D != 0.0 )
                { 
                    nnu = 1.0 / D; 
                } else { nnu = double.PositiveInfinity; } 
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                x_nn_swap( ref nnl, ref nnu ); 
                adjustedParameters.AddOutput( "rrn_treat", x_ben_harm( host, nnt, false ) ); 
                adjustedParameters.AddOutput( "rrn_treat_from", x_ben_harm( host, nnl, false ) ); 
                adjustedParameters.AddOutput( "rrn_treat_to", x_ben_harm( host, nnu, false ) ); 
                adjustedParameters.AddOutput( "rrn_treat_round", x_ben_harm( host, nnt, true ) ); 
                adjustedParameters.AddOutput( "rrn_treat_round_from", x_ben_harm( host, nnl, true ) ); 
                adjustedParameters.AddOutput( "rrn_treat_round_to", x_ben_harm( host, nnu, true ) ); 
                // <--
                
                // NNT_odds ratio
                D = ( ( 1.0 - brr ) * brr * ( 1.0 - oor ) ); 
                if ( D != 0.0 )
                { 
                    nnt = ( 1.0 - ( brr * ( 1.0 - oor ) ) ) / D; 
                } else { nnt = double.PositiveInfinity; } 
                D = ( ( 1.0 - brr ) * brr * ( 1.0 - llf ) ); 
                if ( D != 0.0 )
                { 
                    nnl = ( 1.0 - ( brr * ( 1.0 - llf ) ) ) / D; 
                } else { nnl = double.PositiveInfinity; } 
                D = ( ( 1.0 - brr ) * brr * ( 1.0 - ulf ) ); 
                if ( D != 0.0 )
                { 
                    nnu = ( 1.0 - ( brr * ( 1.0 - ulf ) ) ) / D; 
                } else { nnu = double.PositiveInfinity; } 
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                x_nn_swap( ref nnl, ref nnu ); 
                adjustedParameters.AddOutput( "or_treat", x_ben_harm( host, nnt, false ) ); 
                adjustedParameters.AddOutput( "or_treat_from", x_ben_harm( host, nnl, false ) ); 
                adjustedParameters.AddOutput( "or_treat_to", x_ben_harm( host, nnu, false ) ); 
                adjustedParameters.AddOutput( "or_treat_round", x_ben_harm( host, nnt, true ) ); 
                adjustedParameters.AddOutput( "or_treat_round_from", x_ben_harm( host, nnl, true ) ); 
                adjustedParameters.AddOutput( "or_treat_round_to", x_ben_harm( host, nnu, true ) ); 
                // <--
                
            } 
            
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptMiscRelRisk( ITemplateHost host, ParameterBag parameters ) 
        { 
            int fault;
            double par_ul = 0; 
            double par_ll = 0;
            double par = 0; 
            double pe = 0; 
            double dif_ul; 
            double dif_ll;

            double a = parameters[ "a" ].AsDouble; 
            double b = parameters[ "b" ].AsDouble; 
            double C = parameters[ "c" ].AsDouble; 
            double D = parameters[ "d" ].AsDouble; 
            double m1 = a + b; 
            double m2 = C + D; 
            double n1 = a + C; 
            double n2 = b + D; 
            double N = m1 + m2; 
            
            double GAMMA = parameters[ "cco" ].AsDouble; 
            if ( GAMMA <= 0.0 | GAMMA >= 1.0 )
            { 
                GAMMA = 0.95; 
            } 
            
            if ( a + C <= 0 | b + D <= 0 | b <= 0 ) 
            { 
                throw new InvalidDataException( "Relative risk can not be calculated for these data." );
            } 
            
            double rr = ( a / ( a + C ) ) / ( b / ( b + D ) ); 
            double P = 1.0 - ( ( 1.0 - GAMMA ) / 2.0 ); 
            double zp = PDF.gauinv( P, out fault ); 
            
            double P1 = a / n1; 
            double P2 = b / n2; 
            double dif = P1 - P2; 
            MathDbl.uppci( Convert.ToInt32( a ), Convert.ToInt32( n1 ), Convert.ToInt32( b ), Convert.ToInt32( n2 ), out dif_ll, out dif_ul, zp, ( 100.0 * ( 1.0 - GAMMA ) ) );

            bool dofish = true;
            double power = Power.fishpower( 1.0 - GAMMA, a, b, n1, n2, ref dofish ); 
            
            //  only calculate PAR for RR > 1 because -ve PAR is meaningless
            if ( rr > 1.0 ) 
            { 
                bool wasCancelled; 
                pe = host.GetDouble( "Enter the population proportion exposed to the risk factor", "Risk Analysis (prospective)", ( a + C ) / N, out wasCancelled ); 
                if ( wasCancelled ) 
                { 
                    fault = -1; 
                } 
                else 
                { 
                    if ( pe == Constant.MISSING | pe < 0.0 | pe > 1.0 )
                    { 
                        pe = ( a + C ) / N; 
                    } 
                    par = ( pe * ( rr - 1.0 ) ) / ( 1.0 + ( pe * ( rr - 1.0 ) ) ); 
                    double var_par = ( ( b * N ) / ( Math.Pow( m1, 3.0 ) * Math.Pow( n2, 3.0 ) ) ) * ( a * D * ( N - b ) + b * b * C ); 
                    par_ll = par - zp * Math.Sqrt( var_par ); 
                    par_ul = par + zp * Math.Sqrt( var_par ); 
                } 
            } 
            else 
            { 
                par = Constant.MISSING; 
                par_ll = Constant.MISSING; 
                par_ul = Constant.MISSING; 
            } 
            
            if ( fault == 0 ) 
            { 
                //  RTF_LoadTemplate("relrisk.rtf") Then
                ParameterBag outputParameters = new ParameterBag();
                double ul;
                double ll;
                MathDbl.lr_ci( b, a, b + D, a + C, zp, out ll, out ul ); 
                outputParameters.AddOutput( "a_out",  a.ToString() ); 
                outputParameters.AddOutput( "b_out",  b.ToString() ); 
                outputParameters.AddOutput( "c_out",  C.ToString() ); 
                outputParameters.AddOutput( "d_out",  D.ToString() ); 
                
                outputParameters.AddOutput( "ratio", host.RoundU( rr ) ); 
                outputParameters.AddOutput( "pc", Formatting.XRound( GAMMA * 100, 2 ) ); 
                outputParameters.AddOutput( "koopman_from", host.RoundU( ll ) ); 
                outputParameters.AddOutput( "koopman_to", host.RoundU( ul ) ); 
                outputParameters.AddOutput( "pwr", Formatting.pwr( power, 1.0 - GAMMA ) ); 
                
                outputParameters.AddOutput( "dif", host.RoundU( dif ) ); 
                outputParameters.AddOutput( "miettinen_from", host.RoundU( dif_ll ) ); 
                outputParameters.AddOutput( "miettinen_to", host.RoundU( dif_ul ) ); 
                
                List<ParameterBag> exposureList = new List<ParameterBag>(); 
                outputParameters.AddOutput( "*exposure", exposureList ); 
                if ( par != Constant.MISSING ) 
                { 
                    ParameterBag exposureParameters = new ParameterBag(); 
                    exposureList.Add( exposureParameters ); 
                    exposureParameters.AddOutput( "pe", host.RoundU( pe * 100.0 ) ); 
                    exposureParameters.AddOutput( "par", host.RoundU( par * 100.0 ) ); 
                    exposureParameters.AddOutput( "walter_from", host.RoundU( par_ll * 100.0 ) ); 
                    exposureParameters.AddOutput( "walter_to", host.RoundU( par_ul * 100.0 ) ); 
                } 
                
                return new StepResult( StepSuccess.Success, outputParameters ); 
            } 
            return null; 
        } 
        
        
        public static StepResult rptRateSMR( ITemplateHost Host, ParameterBag Parameters ) 
        {
            double etot = 0;
            int j;
            int fault = 0; 
            
            DataFrame datFrame = Parameters[ "data" ].AsDataFrame; 
            DoubleVariable datV0 = datFrame.Variables[ 0 ].AsDoubleVariable; 
            DoubleVariable datV1 = datFrame.Variables[ 1 ].AsDoubleVariable; 
            int rows = datFrame.MaxRows;
            double[] asm = new double[rows + 1 /* VB to C# conversion */ ];
            double[] spop = new double[rows + 1 /* VB to C# conversion */ ]; 
            
            double nunit = Parsing.Cdbl_Txt( Parameters[ "nunit" ].AsString ); 
            double cco = Parameters[ "cco" ].AsDouble; 
            if ( cco <= 0.0 || cco >= 1.0 )
                cco = 0.95; 
            
            if ( nunit <= 0 ) 
                throw new InvalidDataException(); 
            
            for ( j=1; j <= rows; j++ ) 
            { 
                double rtd = datV0.Data[j - 1]; 
                asm[ j ] = rtd; 
                rtd = datV1.Data[j - 1]; 
                spop[ j ] = rtd; 
                etot = etot + ( asm[ j ] / nunit ) * spop[ j ]; 
            } 
            
            if ( etot <= 0 ) 
                throw new InvalidDataException(); 
            
            //  RTF_LoadTemplate("stdmort.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            List<ParameterBag> groupsList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*groups", groupsList ); 
            for ( j=1; j <= rows; j++ ) 
            { 
                ParameterBag groupsParameters = new ParameterBag(); 
                groupsList.Add( groupsParameters ); 
                groupsParameters.AddOutput( "group", Host.RoundU( asm[ j ] / nunit ) ); 
                groupsParameters.AddOutput( "observed",  spop[ j ].ToString() ); 
                groupsParameters.AddOutput( "expected", Host.RoundU( spop[ j ] * ( asm[ j ] / nunit ) ) ); 
            } 
            outputParameters.AddOutput( "total",  etot.ToString() ); 
            
            double dead = Parameters[ "dead" ].AsDouble; 
            if ( fault == 0 ) 
            { 
                outputParameters.AddOutput( "ratio", Host.RoundU( dead / etot ) ); 
                outputParameters.AddOutput( "smr", Formatting.XRound( Convert.ToInt64( ( dead / etot ) * 100 ), 0 ) );

                double xu;
                double xl;
                Rates.poisson_ci( 1.0 - cco, dead, 1.0, out xl, out xu ); 
                
                if ( xl != Constant.MISSING )
                { 
                    xl = xl / etot; 
                } 
                if ( xl != Constant.MISSING )
                { 
                    xu = xu / etot; 
                } 
                outputParameters.AddOutput( "pc", Formatting.XRound( 100 * cco, 2 ) ); 
                outputParameters.AddOutput( "from", Host.RoundU( xl ) ); 
                outputParameters.AddOutput( "to", Host.RoundU( xu ) ); 
                outputParameters.AddOutput( "from100", Formatting.XRound( Convert.ToInt64( 100 * xl ), 0 ) ); 
                outputParameters.AddOutput( "to100", Host.RoundU( Convert.ToInt64( 100 * xu ) ) ); 
                
                double term;
                double plo;
                double phi;
                ExFortran.poisson( etot, Convert.ToInt32( dead ), out phi, out plo, out term, out fault ); 
                if ( fault != 0 )
                { 
                    phi = Constant.MISSING; 
                } 
                
                outputParameters.AddOutput( "qty",  Convert.ToInt64( dead ).ToString() ); 
                outputParameters.AddOutput( "p_hi", Host.pval( phi ) ); 
                
                outputParameters.AddOutput( "p_lo", Host.pval( plo ) ); 
            } 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptPropPairs( ITemplateHost host, ParameterBag parameters ) 
        { 
            double piu; 
            double pil; 
            double cu; 
            double cl;
            bool fault; 
            int ifault;

            double N = parameters[ "n" ].AsDouble; 
            
            if ( N <= 0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            double r = parameters[ "r" ].AsDouble; 
            double s = parameters[ "s" ].AsDouble; 
            double t = parameters[ "t" ].AsDouble; 
            double cco = parameters[ "cco" ].AsDouble; 
            if ( cco <= 0.0 | cco >= 1.0 )
            { 
                cco = 0.95; 
            } 
            double pt = ( 1.0 - cco ) / 2.0; 
            
            double cit = PDF.gauinv( cco + pt, out ifault ); 
            if ( ifault != 0 ) 
            { 
                return null; 
            } 
            
            double P1 = ( r + s ) / N; 
            double P2 = ( r + t ) / N; 
            double P3 = ( s - t ) / N; 
            
            //  RTF_LoadTemplate("p_pair.rtf") Then
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "n", Formatting.XRound( N, 1 ) ); 
            outputParameters.AddOutput( "r", Formatting.XRound( r, 1 ) ); 
            outputParameters.AddOutput( "s", Formatting.XRound( s, 1 ) ); 
            outputParameters.AddOutput( "t", Formatting.XRound( t, 1 ) ); 
            outputParameters.AddOutput( "prop_1", host.RoundU( P1 ) ); 
            outputParameters.AddOutput( "prop_2", host.RoundU( P2 ) ); 
            outputParameters.AddOutput( "prop_diff", host.RoundU( P3 ) ); 
            
            double nx = s + t; 
            double rx = s; 
            if ( rx < 0 && rx >= nx ) 
            { 
                return null; 
            } 
            if ( rx > nx / 2 )
            { 
                rx = nx - rx; 
            } 
            double fl = Math.Pow( 0.5, nx ); 
            
            List<ParameterBag> exactList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*exact", exactList ); 
            List<ParameterBag> approxList = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*approx", approxList ); 
            if ( fl > 0 ) 
            { 
                ParameterBag exactParameters = new ParameterBag(); 
                exactList.Add( exactParameters ); 
                double pl = fl; 
                if ( rx != 0 )
                {
                    double i;
                    for ( i=1; i <= rx; i++ ) 
                    { 
                        fl = fl * ( nx - i + 1 ) / i; 
                        pl = pl + fl; 
                    }
                }

                double p2l = 2.0 * pl; 
                double P = pl; 
                if ( p2l > 1.0 )
                { 
                    p2l = 1.0; 
                } 
                P2 = p2l; 
                exactParameters.AddOutput( "cum_2", host.pval( P2 ) ); 
                exactParameters.AddOutput( "cum_1", host.pval( P ) ); 
                
                pl = pl - fl / 2.0; 
                p2l = 2.0 * pl; 
                P = pl; 
                if ( p2l > 1.0 )
                { 
                    p2l = 1.0; 
                } 
                P2 = p2l; 
                exactParameters.AddOutput( "cum_2_mid", host.pval( P2 ) ); 
                exactParameters.AddOutput( "cum_1_mid", host.pval( P ) ); 
            } 
            else 
            { 
                ParameterBag approxParameters = new ParameterBag(); 
                approxList.Add( approxParameters ); 
                
                double D = Math.Abs( nx / 2 - rx ) - 0.5;
                double z;
                if ( D < 0 )
                { 
                    z = 0; 
                } else { z = D / Math.Sqrt( nx / 4 ); } 
                approxParameters.AddOutput( "z", host.RoundU( z ) ); 
            } 
            
            // Following snippet is only used if calculating qcl according to commented-out code below.  PJC 2012/04/09.
            // double qcl = ( 1.0 - cco ) / 2.0; 
            // qcl = cco + qcl; 

            // Following code was commented out in original.  PJC 2012/04/09.
            // If n < 200 Then
            //  ia = CLng(n - s - T)
            //  ib = CLng(s)
            //  ic = CLng(T)
            //  Call cipair(ia, ib, ic, CL, cu, qcl, cit, ierr)
            //  q = "Exact (unconditional)"
            // Else
            int ial = Convert.ToInt32( r ); 
            int ibl = Convert.ToInt32( s ); 
            int icl = Convert.ToInt32( t ); 
            int idl = Convert.ToInt32( N - r - s - t ); 
            MathDbl.Wilson( ial, ibl, icl, idl, out cl, out cu, cit, out fault ); 
            const string Q = "Score based (Newcombe)"; 
            // End If
            if ( fault == false ) 
            { 
                pil = cl; 
                piu = cu; 
            } 
            else 
            { 
                pil = Constant.MISSING; 
                piu = Constant.MISSING; 
            } 
            
            outputParameters.AddOutput( "qcl", Q ); 
            outputParameters.AddOutput( "pc", Formatting.XRound( cco * 100, 2 ) ); 
            outputParameters.AddOutput( "lower", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "upper", host.RoundU( piu ) ); 
            
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptPropSingle( ITemplateHost host, ParameterBag parameters ) 
        {
            double P2; 
            double dp2; 
            double dp1;
            double piu; 
            double pil;
            int fault; 
            string warn;

            double N = parameters[ "n" ].AsDouble; 
            double r = parameters[ "r" ].AsDouble; 
            if ( r > N )
            { 
                double tmp = r; 
                r = N;
                N = tmp;
            } 
            
            if ( N <= 0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            double qpi = parameters[ "qpi" ].AsDouble; 
            if ( qpi <= 0 )
            { 
                qpi = 1.0E-28; 
            } 
            if ( qpi >= 1 )
            { 
                qpi = 0.9999999; 
            } 
            if ( r != Math.Floor(r) && N <= r ) 
            { 
                return null; 
            } 
            double cco = parameters[ "cco" ].AsDouble; 
            if ( cco <= 0.0 | cco >= 1.0 )
            { 
                cco = 0.95; 
            } 
            double cit = PDF.gauinv( cco + ( 1 - cco ) / 2, out fault ); 
            double P = r / N; 
            //  RTF_LoadTemplate("p_sng.rtf") Then
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "n", Formatting.XRound( N, 1 ) ); 
            outputParameters.AddOutput( "r", Formatting.XRound( r, 1 ) ); 
            outputParameters.AddOutput( "prop", host.RoundU( P ) ); 
            
            // Clopper Pearson by F distribution
            outputParameters.AddOutput( "ci_exact", Formatting.XRound( cco * 100, 2 ) ); 
            MathDbl.binci( r, N, out pil, out piu, cco, out warn ); 
            outputParameters.AddOutput( "lower_exact", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "upper_exact", host.RoundU( piu ) + warn ); 
            
            // binomial exact P
            string aprx = "Binomial "; 
            double qpix = qpi == 1.0E-28 ? 0 : qpi; 
            outputParameters.AddOutput( "null", host.RoundU( qpix ) ); 
            if ( N > 1000000 ) 
            { 
                aprx = "Normal "; 
                P = 1.0 - PDF.alnorm( ( Math.Abs( r - N * qpi ) - 0.5 ) / Math.Sqrt( N * qpi * ( 1.0 - qpi ) ) ); 
                if ( P > 1.0 - P )
                { 
                    P = 1.0 - P; 
                } 
                P2 = 2.0 * P; 
            } 
            else 
            { 
                PDF.bino2( Convert.ToInt32( N ), qpi, Convert.ToInt32( r ), out dp1, out dp2, out fault ); 
                P = dp1; 
                P2 = dp2; 
            } 
            if ( fault != 0 ) 
            { 
                P = Constant.MISSING; 
                P2 = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "ap_1_exact", aprx ); 
            outputParameters.AddOutput( "p_1_exact", host.pval( P ) ); 
            outputParameters.AddOutput( "ap_2_exact", aprx ); 
            outputParameters.AddOutput( "p_2_exact", host.pval( P2 ) ); 
            
            // Wilson approximate mid-P
            double t1 = 2.0 * r + cit * cit; 
            double t2 = cit * Math.Sqrt( cit * cit + 4.0 * r * ( 1.0 - ( r / N ) ) ); 
            double t3 = 2.0 * ( N + cit * cit ); 
            pil = ( t1 - t2 ) / t3; 
            piu = ( t1 + t2 ) / t3; 
            outputParameters.AddOutput( "ci_approx", Formatting.XRound( cco * 100, 2 ) ); 
            outputParameters.AddOutput( "lower_approx", host.RoundU( pil ) ); 
            outputParameters.AddOutput( "upper_approx", host.RoundU( piu ) ); 
            
            // binomial mid P
            if ( N > 1000000 ) 
            { 
                P = 1.0 - PDF.alnorm( ( Math.Abs( r - N * qpi ) - 0.5 ) / Math.Sqrt( N * qpi * ( 1.0 - qpi ) ) ); 
                if ( P > 1.0 - P )
                { 
                    P = 1.0 - P; 
                } 
                P2 = 2.0 * P; 
            } 
            else 
            { 
                PDF.binomid( Convert.ToInt32( N ), qpi, Convert.ToInt32( r ), out dp1, out dp2, out fault ); 
                P = dp1; 
                P2 = dp2; 
            } 
            if ( fault != 0 ) 
            { 
                P = Constant.MISSING; 
                P2 = Constant.MISSING; 
            } 
            outputParameters.AddOutput( "ap_1_approx", aprx ); 
            outputParameters.AddOutput( "p_1_approx", host.pval( P ) ); 
            outputParameters.AddOutput( "ap_2_approx", aprx ); 
            outputParameters.AddOutput( "p_2_approx", host.pval( P2 ) ); 
            
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
        
        
        public static StepResult RptPropUnPaired( ITemplateHost host, ParameterBag parameters ) 
        { 
            double z;
            double cu; 
            double cl;
            double rTmp;

            double n1 = parameters[ "n1" ].AsDouble; 
            double r1 = parameters[ "r1" ].AsDouble; 
            
            if ( r1 > n1 ) 
            { 
                rTmp = r1; 
                r1 = n1; 
                n1 = rTmp; 
            } 
            if ( n1 <= 0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            double n2 = parameters[ "n2" ].AsDouble; 
            double r2 = parameters[ "r2" ].AsDouble; 
            if ( r2 > n2 )
            { 
                rTmp = r2; 
                r2 = n2;
                n2 = rTmp;
            } 
            
            if ( n2 <= 0 ) 
            { 
                throw new InvalidDataException(); 
            } 
            
            double cco = parameters[ "cco" ].AsDouble; 
            if ( cco <= 0.0 | cco >= 1.0 )
            { 
                cco = 0.95; 
            } 
            int fault; 
            double cit = PDF.gauinv( cco + ( 1 - cco ) / 2, out fault ); 
            if ( fault != 0 ) 
            { 
                return null; 
            } 
            
            double P1 = r1 / n1; 
            double P2 = r2 / n2; 
            double P = ( r1 + r2 ) / ( n1 + n2 ); 
            //  RTF_LoadTemplate("p_unpair.rtf")
            ParameterBag outputParameters = new ParameterBag(); 
            outputParameters.AddOutput( "n_1", Formatting.XRound( n1, 1 ) ); 
            outputParameters.AddOutput( "r_1", Formatting.XRound( r1, 1 ) ); 
            outputParameters.AddOutput( "prop_1", host.RoundU( P1 ) ); 
            outputParameters.AddOutput( "n_2", Formatting.XRound( n2, 1 ) ); 
            outputParameters.AddOutput( "r_2", Formatting.XRound( r2, 1 ) ); 
            outputParameters.AddOutput( "prop_2", host.RoundU( P2 ) ); 
            outputParameters.AddOutput( "prop_diff", host.RoundU( P1 - P2 ) ); 
            
            MathDbl.uppci( Convert.ToInt32( r1 ), Convert.ToInt32( n1 ), Convert.ToInt32( r2 ), Convert.ToInt32( n2 ), out cl, out cu, cit, 100.0 * cco ); 
            
            outputParameters.AddOutput( "ci", Formatting.XRound( 100 * cco, 1 ) ); 
            outputParameters.AddOutput( "from", host.RoundU( cl ) ); 
            outputParameters.AddOutput( "to", host.RoundU( cu ) ); 
            
            double mp = Prop_MidPFisher2( Convert.ToInt32( r1 ), Convert.ToInt32( n1 - r1 ), Convert.ToInt32( r2 ), Convert.ToInt32( n2 - r2 ) ); 
            List<ParameterBag> exact2List = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*exact2", exact2List ); 
            if ( mp != Constant.MISSING ) 
            { 
                ParameterBag exact2Parameters = new ParameterBag(); 
                exact2List.Add( exact2Parameters ); 
                exact2Parameters.AddOutput( "mp", host.pval( mp ) ); 
            } 
            
            double sepest = Math.Sqrt( P * ( 1 - P ) * ( ( 1 / n1 ) + ( 1 / n2 ) ) ); 
            if ( sepest == 0 ) 
            { 
                sepest = Constant.MISSING; 
                z = Constant.MISSING; 
            } 
            else 
            { 
                z = ( ( P1 - P2 ) / sepest ); 
            } 
            
            outputParameters.AddOutput( "se", host.RoundU( sepest ) ); 
            outputParameters.AddOutput( "z", host.RoundU( z ) ); 
            
            List<ParameterBag> approx2List = new List<ParameterBag>(); 
            outputParameters.AddOutput( "*approx2", approx2List ); 
            if ( z != Constant.MISSING ) 
            { 
                ParameterBag approx2Parameters = new ParameterBag(); 
                approx2List.Add( approx2Parameters ); 
                approx2Parameters.AddOutput( "p_2", host.zvalp2( z ) ); 
                approx2Parameters.AddOutput( "p_1", host.zvalp1( z ) ); 
            } 
            return new StepResult( StepSuccess.Success, outputParameters ); 
        } 
    } 
} 

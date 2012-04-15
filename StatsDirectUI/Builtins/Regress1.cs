using System;

using StatsDirect.Numerics; 

namespace StatsDirect.Builtins
{
    public class Regress1  
    { 
        
        public static void X_Comat( out double xc, out double XR, ref double[,] x, ref int nx, ref int idx, ref int idy ) 
        { 
            int i; 
            double co = 0; double avx ; double avy ; double SDX ; double sdy ; 
            
            x_avsd( x, nx, idx, out avx, out SDX ); 
            x_avsd( x, nx, idy, out avy, out sdy ); 
            for ( i=1; i <= nx; i++ ) 
            { 
                co = co + ( x[ idx, i ] - avx ) * ( x[ idy, i ] - avy ); 
            } 
            XR = co / ( Convert.ToDouble( nx - 1 ) * SDX * sdy ); 
            xc = co / Convert.ToDouble( nx - 1 ); 
        } 
        
        
        public static void x_avsd( double[,] x, int nx, int id, out double av, out double sd ) 
        { 
            int j ;
            double ep = 0; double var = 0; 
            
            double sum = 0.0; 
            for ( j=1; j <= nx; j++ ) 
            { 
                sum = sum + x[ id, j ]; 
            } 
            av = sum / Convert.ToDouble( nx ); 
            for ( j=1; j <= nx; j++ ) 
            { 
                double s = x[ id, j ] - av; 
                double P = s * s; 
                ep = ep + s; 
                var = var + P; 
            } 
            var = ( var - ( ep * ep ) / Convert.ToDouble( nx ) ) / Convert.ToDouble( nx - 1 ); 
            sd = Math.Sqrt( var ); 
        } 
        
        
        public static void X_SVDCP( ref double[,] ad, ref int nx, ref int P, ref double[] wd, ref double[,] vd, ref int ifault ) 
        { 
            int i ; int j ; int k ; int L = 0;
            double s ; double f ; double h ;

            double[] rv1 = new double[P + 1 /* for VB to C# conversion */ ]; 
            if ( nx < P ) 
            { 
                ifault = 1; 
                return; 
            } 
            const int maxit = 30; 
            double g = 0.0; 
            double sca = 0.0; 
            double anorm = 0.0; 
            for ( i=1; i <= P; i++ ) 
            { 
                L = i + 1; 
                rv1[ i ] = sca * g; 
                g = 0.0; 
                s = 0.0; 
                sca = 0.0; 
                if ( i <= nx ) 
                { 
                    for ( k=i; k <= nx; k++ ) 
                    { 
                        sca = sca + Math.Abs( ad[ k, i ] ); 
                    } 
                    if ( sca != 0.0 ) 
                    { 
                        for ( k=i; k <= nx; k++ ) 
                        { 
                            ad[ k, i ] = ad[ k, i ] / sca; 
                            s = s + ad[ k, i ] * ad[ k, i ]; 
                        } 
                        f = ad[ i, i ]; 
                        if ( f >= 0.0 )
                        { 
                            g = -Math.Abs( Math.Sqrt( s ) ); 
                        } else { g = Math.Abs( Math.Sqrt( s ) ); } 
                        h = f * g - s; 
                        ad[ i, i ] = f - g; 
                        for ( j=L; j <= P; j++ ) 
                        { 
                            s = 0.0; 
                            for ( k=i; k <= nx; k++ ) 
                            { 
                                s = s + ad[ k, i ] * ad[ k, j ]; 
                            } 
                            f = s / h; 
                            for ( k=i; k <= nx; k++ ) 
                            { 
                                ad[ k, j ] = ad[ k, j ] + f * ad[ k, i ]; 
                            } 
                        } 
                        for ( k=i; k <= nx; k++ ) 
                        { 
                            ad[ k, i ] = sca * ad[ k, i ]; 
                        } 
                    } 
                } 
                wd[ i ] = sca * g; 
                g = 0.0; 
                s = 0.0; 
                sca = 0.0; 
                if ( i <= nx & i != P ) 
                { 
                    for ( k=L; k <= P; k++ ) 
                    { 
                        sca = sca + Math.Abs( ad[ i, k ] ); 
                    } 
                    if ( sca != 0.0 ) 
                    { 
                        for ( k=L; k <= P; k++ ) 
                        { 
                            ad[ i, k ] = ad[ i, k ] / sca; 
                            s = s + ad[ i, k ] * ad[ i, k ]; 
                        } 
                        f = ad[ i, L ]; 
                        if ( f >= 0.0 ) 
                        { 
                            g = -Math.Abs( Math.Sqrt( s ) ); 
                        } 
                        else 
                        { 
                            g = Math.Abs( Math.Sqrt( s ) ); 
                        } 
                        h = f * g - s; 
                        ad[ i, L ] = f - g; 
                        for ( k=L; k <= P; k++ ) 
                        { 
                            rv1[ k ] = ad[ i, k ] / h; 
                        } 
                        for ( j=L; j <= nx; j++ ) 
                        { 
                            s = 0.0; 
                            for ( k=L; k <= P; k++ ) 
                            { 
                                s = s + ad[ j, k ] * ad[ i, k ]; 
                            } 
                            for ( k=L; k <= P; k++ ) 
                            { 
                                ad[ j, k ] = ad[ j, k ] + s * rv1[ k ]; 
                            } 
                        } 
                        for ( k=L; k <= P; k++ ) 
                        { 
                            ad[ i, k ] = sca * ad[ i, k ]; 
                        } 
                    } 
                } 
                if ( ( Math.Abs( wd[ i ] ) + Math.Abs( rv1[ i ] ) ) > anorm )
                { 
                    anorm = Math.Abs( wd[ i ] ) + Math.Abs( rv1[ i ] ); 
                } 
            } 
            
            for ( i=P; i >= 1; i-- ) 
            { 
                if ( i < P ) 
                { 
                    if ( g != 0.0 ) 
                    { 
                        for ( j=L; j <= P; j++ ) 
                        { 
                            vd[ j, i ] = ( ad[ i, j ] / ad[ i, L ] ) / g; 
                        } 
                        for ( j=L; j <= P; j++ ) 
                        { 
                            s = 0.0; 
                            for ( k=L; k <= P; k++ ) 
                            { 
                                s = s + ad[ i, k ] * vd[ k, j ]; 
                            } 
                            for ( k=L; k <= P; k++ ) 
                            { 
                                vd[ k, j ] = vd[ k, j ] + s * vd[ k, i ]; 
                            } 
                        } 
                    } 
                    for ( j=L; j <= P; j++ ) 
                    { 
                        vd[ i, j ] = 0.0; 
                        vd[ j, i ] = 0.0; 
                    } 
                } 
                vd[ i, i ] = 1.0; 
                g = rv1[ i ]; 
                L = i; 
            } 
            for ( i=P; i >= 1; i-- ) 
            { 
                L = i + 1; 
                g = wd[ i ]; 
                for ( j=L; j <= P; j++ ) 
                { 
                    ad[ i, j ] = 0.0; 
                } 
                if ( g != 0.0 ) 
                { 
                    g = 1.0 / g; 
                    for ( j=L; j <= P; j++ ) 
                    { 
                        s = 0.0; 
                        for ( k=L; k <= nx; k++ ) 
                        { 
                            s = s + ad[ k, i ] * ad[ k, j ]; 
                        } 
                        f = ( s / ad[ i, i ] ) * g; 
                        for ( k=i; k <= nx; k++ ) 
                        { 
                            ad[ k, j ] = ad[ k, j ] + f * ad[ k, i ]; 
                        } 
                    } 
                    for ( j=i; j <= nx; j++ ) 
                    { 
                        ad[ j, i ] = ad[ j, i ] * g; 
                    } 
                } 
                else 
                { 
                    for ( j=i; j <= nx; j++ ) 
                    { 
                        ad[ j, i ] = 0.0; 
                    } 
                } 
                ad[ i, i ] = ad[ i, i ] + 1.0; 
            } 
            for ( k=P; k >= 1; k-- )
            {
                int its ;
                for ( its=1; its <= maxit; its++ ) 
                {
                    int nm ;
                    double z ;
                    double C ;
                    double y ;
                    for ( L=k; L >= 1; L-- ) 
                    { 
                        nm = L - 1; 
                        if ( ( Math.Abs( rv1[ L ] ) + anorm ) == anorm )
                        { 
                            break; /* TRANSWARNING: check that break is in correct scope */ 
                        } 
                        if ( ( Math.Abs( wd[ nm ] ) + anorm ) == anorm ) 
                        { 
                            s = 1.0; 
                            for ( i=L; i <= k; i++ ) 
                            { 
                                f = s * rv1[ i ]; 
                                if ( ( Math.Abs( f ) + anorm ) != anorm )
                                { 
                                    break; /* TRANSWARNING: check that break is in correct scope */ 
                                } 
                                g = wd[ i ]; 
                                h = X_PYTHAG( f, g ); 
                                wd[ i ] = h; 
                                h = 1.0 / h; 
                                C = ( g * h ); 
                                s = -( f * h ); 
                                for ( j=1; j <= nx; j++ ) 
                                { 
                                    y = ad[ j, nm ]; 
                                    z = ad[ j, i ]; 
                                    ad[ j, nm ] = ( y * C ) + ( z * s ); 
                                    ad[ j, i ] = -( y * s ) + ( z * C ); 
                                } 
                            } 
                            break; /* TRANSWARNING: check that break is in correct scope */ 
                        } 
                    } 
                    z = wd[ k ]; 
                    if ( L == k ) 
                    { 
                        if ( z < 0.0 ) 
                        { 
                            wd[ k ] = -z; 
                            for ( j=1; j <= P; j++ ) 
                            { 
                                vd[ j, k ] = -vd[ j, k ]; 
                            } 
                        } 
                        break; /* TRANSWARNING: check that break is in correct scope */ 
                    } 
                    if ( its >= maxit ) 
                    { 
                        ifault = 2; 
                        return; 
                    } 
                    double x = wd[ L ]; 
                    nm = k - 1; 
                    y = wd[ nm ]; 
                    g = rv1[ nm ]; 
                    h = rv1[ k ]; 
                    f = ( ( y - z ) * ( y + z ) + ( g - h ) * ( g + h ) ) / ( 2.0 * h * y ); 
                    g = X_PYTHAG( f, 1.0 );
                    double temp ;
                    if ( f >= 0 ) 
                    { 
                        temp = Math.Abs( g ); 
                    } 
                    else 
                    { 
                        temp = -Math.Abs( g ); 
                    } 
                    f = ( ( x - z ) * ( x + z ) + h * ( ( y / ( f + temp ) ) - h ) ) / x; 
                    C = 1.0; 
                    s = 1.0; 
                    for ( j=L; j <= nm; j++ ) 
                    { 
                        i = j + 1; 
                        g = rv1[ i ]; 
                        y = wd[ i ]; 
                        h = s * g; 
                        g = C * g; 
                        z = X_PYTHAG( f, h ); 
                        rv1[ j ] = z; 
                        C = f / z; 
                        s = h / z; 
                        f = ( x * C ) + ( g * s ); 
                        g = -( x * s ) + ( g * C ); 
                        h = y * s; 
                        y = y * C;
                        int jj ;
                        for ( jj=1; jj <= P; jj++ ) 
                        { 
                            x = vd[ jj, j ]; 
                            z = vd[ jj, i ]; 
                            vd[ jj, j ] = ( x * C ) + ( z * s ); 
                            vd[ jj, i ] = -( x * s ) + ( z * C ); 
                        } 
                        z = X_PYTHAG( f, h ); 
                        wd[ j ] = z; 
                        if ( z != 0.0 ) 
                        { 
                            z = 1.0 / z; 
                            C = f * z; 
                            s = h * z; 
                        } 
                        f = ( C * g ) + ( s * y ); 
                        x = -( s * g ) + ( C * y ); 
                        for ( jj=1; jj <= nx; jj++ ) 
                        { 
                            y = ad[ jj, j ]; 
                            z = ad[ jj, i ]; 
                            ad[ jj, j ] = ( y * C ) + ( z * s ); 
                            ad[ jj, i ] = -( y * s ) + ( z * C ); 
                        } 
                    } 
                    rv1[ L ] = 0.0; 
                    rv1[ k ] = f; 
                    wd[ k ] = x; 
                }
            }
        } 
        
        
        private static double X_PYTHAG( double a, double b ) 
        {
            double absa = Math.Abs( a ); 
            double absb = Math.Abs( b ); 
            if ( absa > absb ) 
                return absa * Math.Sqrt( 1.0 + ( absb / absa ) * ( absb / absa ) ); 
            if ( absb == 0.0 ) 
                return 0.0; 
            return absb * Math.Sqrt( 1.0 + ( absa / absb ) * ( absa / absb ) );
        } 
        
        
        public static void X_Eigsrt( ref double[] D, ref double[,] v, int N ) 
        { 
            for (int i=1; i <= N - 1; i++ ) 
            { 
                int k = i; 
                double P = D[ i ];
                int j ;
                for ( j=i + 1; j <= N; j++ ) 
                { 
                    if ( D[ j ] >= P ) 
                    { 
                        k = j; 
                        P = D[ j ]; 
                    } 
                } 
                if ( k != i ) 
                { 
                    D[ k ] = D[ i ]; 
                    D[ i ] = P; 
                    for ( j=1; j <= N; j++ ) 
                    { 
                        P = v[ j, i ]; 
                        v[ j, i ] = v[ j, k ]; 
                        v[ j, k ] = P; 
                    } 
                } 
            } 
        } 
        
        
        private static void X_SVDBKD( double[,] ud, double[] wd, double[,] vd, int nx, int P, double[] yd, double[] sig, double[] bd ) 
        {
            double[] t = new double[P + 1 /* for VB to C# conversion */ ]; 
            for (int j=1; j <= P; j++ ) 
            { 
                double s = 0.0; 
                if ( wd[ j ] != 0.0 ) 
                {
                    for (int i=1; i <= nx; i++ ) 
                    { 
                        s += ud[ i, j ] * yd[ i ] / sig[ i ]; 
                    } 
                    s /= wd[ j ]; 
                } 
                t[ j ] = s; 
            } 
            for (int j=1; j <= P; j++ ) 
            { 
                double s = 0.0;
                for (int k=1; k <= P; k++ ) 
                { 
                    s += vd[ j, k ] * t[ k ]; 
                } 
                bd[ j ] = s; 
            } 
        } 
        
        
        public static void X_SVDVRD( double[,] x, double[,] v, double[] w, int nx, int P, double[,] xtxi, double[] cn, double[] hi ) 
        { 
            int i ; int j ; int k ;
            double sum ;

            double[] owt = new double[P + 1 /* for VB to C# conversion */ ]; 
            double wmax = w[ 1 ]; 
            for ( i=1; i <= P; i++ ) 
            { 
                owt[ i ] = 0.0; 
                if ( w[ i ] != 0.0 )
                { 
                    owt[ i ] = 1.0 / ( w[ i ] * w[ i ] ); 
                } 
                if ( w[ i ] > wmax )
                { 
                    wmax = w[ i ]; 
                } 
            } 
            for ( i=1; i <= P; i++ ) 
            { 
                cn[ i ] = wmax * w[ i ] * owt[ i ]; 
            } 
            for ( j=1; j <= P; j++ ) 
            { 
                for ( i=j; i <= P; i++ ) 
                { 
                    sum = 0.0; 
                    for ( k=1; k <= P; k++ ) 
                    { 
                        sum = sum + v[ j, k ] * v[ i, k ] * owt[ k ]; 
                    } 
                    xtxi[ j, i ] = sum; 
                    xtxi[ i, j ] = sum; 
                } 
            } 
            for ( j=1; j <= nx; j++ ) 
            { 
                hi[ j ] = 0.0; 
                for ( i=1; i <= P; i++ ) 
                { 
                    sum = 0.0; 
                    for ( k=1; k <= P; k++ ) 
                    { 
                        sum = sum + xtxi[ i, k ] * x[ j, k ]; 
                    } 
                    hi[ j ] = hi[ j ] + x[ j, i ] * sum; 
                } 
            } 
        } 
        
        
        public static void X_SVGO( double[,] xd, double[] yd, double[] sig, int nx, int P, double[] bd, double[,] ud, double[,] vd, double[] wd, double[] yfit, double[] er, out int ifault ) 
        { 
            int i ; int j ;

            for ( i=1; i <= nx; i++ )
            {
                double osig = 1.0 / sig[ i ];
                for ( j=1; j <= P; j++ ) 
                { 
                    ud[ i, j ] = xd[ i, j ] * osig; 
                }
            }
            // X_SVDCP ud(), nx, p, wd(), vd(), ifault
            double[] rv1 = new double[P + 1 /* for VB to C# conversion */ ]; 
            svd(nx, P, wd, ud, vd, out ifault, rv1 ); 
            double wmax = wd[ 1 ]; 
            for ( j=1; j <= P; j++ ) 
            { 
                if ( wmax < wd[ j ] )
                { 
                    wmax = wd[ j ]; 
                } 
            } 
            // tol = WMAX * EPSNEG
            const double tol = Constant.EPSNEG; 
            for ( j=1; j <= P; j++ ) 
            { 
                if ( wd[ j ] < tol )
                { 
                    wd[ j ] = 0.0; 
                } 
            } 
            X_SVDBKD( ud, wd, vd, nx, P, yd, sig, bd ); 
            for ( i=1; i <= nx; i++ ) 
            { 
                double sum = 0.0; 
                for ( j=1; j <= P; j++ ) 
                { 
                    sum = sum + bd[ j ] * xd[ i, j ]; 
                } 
                yfit[ i ] = sum; 
                er[ i ] = yd[ i ] - yfit[ i ]; 
            } 
        } 
        
        public static void glsqr( int ido, int intcep, int isub, int nrow, int nvar, double[,] x, int iind, int[] indind, int idep, int[] inddep, int ifrq, int iwt, double[] b, double[,] r, double[] d, ref int irank, ref double dfe, ref double scpe, ref int nrmiss, double[] xmin, double[] xmax, double[] wk, ref int ifault ) 
        {
            int i , j , iobs , nobs ;
            int irow;

            double[] sparam = new double[5 + 1 /* for VB to C# conversion */ ]; 
            double frq = 0, temp , wt = 0 ;

            bool skip = false; 
            
            if ( ifault != 0 )
            {
                irank = 0;
                return; 
            } 
            
            int ndep = Math.Abs( idep ); 
            int nind = Math.Abs( iind ); 
            int ncoef = intcep + nind; 
            int intp1 = intcep + 1; 
            int idepx = ncoef + 1;

            double[,] b2 = new double[ncoef + 1 /* for VB to C# conversion */, ncoef + 1 /* for VB to C# conversion */]; 
            
            // tolerance of 100 * largest relative spacing
            const double tol = 100.0 * Constant.EPSILON; 
            const double tolsq = tol * tol; 
            
            if ( ido <= 1 ) 
            { 
                nrmiss = 0; 
                dfe = 0.0; 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    for ( j=1; j <= ncoef; j++ ) 
                    { 
                        r[ j, i ] = 0.0; 
                    } 
                } 
                for ( i=1; i <= ndep; i++ ) 
                { 
                    for ( j=1; j <= ncoef; j++ ) 
                    { 
                        b2[ j, i ] = 0.0; 
                    } 
                } 
                for ( j=1; j <= ncoef; j++ ) 
                { 
                    d[ j ] = 1.0; 
                    xmin[ j ] = Constant.MISSING; 
                    xmax[ j ] = Constant.MISSING; 
                } 
                scpe = 0.0; 
            } 
            
            if ( nrow < 0 ) 
            { 
                nobs = -nrow; 
                irow = -1; 
            } 
            else 
            { 
                nobs = nrow; 
                irow = 1; 
            } 
            int i1 = isub == 0 ? 1 : 2; 
            
            for ( iobs=1; iobs <= nobs; iobs++ ) 
            {
                int igo;
                checkobs( ido, x, iobs, irow, ifrq, iwt, Constant.MISSING, ref nrmiss, ref frq, ref wt, out igo, ref ifault ); 
                if ( igo == 3 )
                { 
                    return; 
                } 
                if ( igo != 2 & igo != 1 ) 
                { 
                    if ( intcep == 1 )
                    { 
                        wk[ 1 ] = 1.0; 
                    } 
                    for ( i=1; i <= iind; i++ ) 
                    { 
                        wk[ intcep + i ] = x[ iobs, indind[ i ] ]; 
                    } 
                    for ( i=1; i <= -iind; i++ ) 
                    { 
                        wk[ intcep + i ] = x[ iobs, i ]; 
                    } 
                    if ( ixnan( nind, wk, intp1 ) > 0 ) 
                    { 
                        nrmiss = nrmiss + irow; 
                    } 
                    else 
                    { 
                        int jdepx = idepx; 
                        for ( i=1; i <= idep; i++ ) 
                        { 
                            wk[ jdepx ] = x[ iobs, inddep[ i ] ]; 
                            jdepx = jdepx + 1; 
                        } 
                        for ( i=idep + 1; i <= 0; i++ ) 
                        { 
                            wk[ jdepx ] = x[ iobs, nvar + i ]; 
                            jdepx = jdepx + 1; 
                        } 
                        if ( ixnan( ndep, wk, idepx ) > 0 ) 
                        { 
                            nrmiss = nrmiss + irow; 
                        } 
                        else 
                        { 
                            dfe += frq; 
                            if ( irow == 1 ) 
                            { 
                                if ( ncoef > 0 ) 
                                { 
                                    if ( xmin[ 1 ] == Constant.MISSING ) 
                                    { 
                                        for ( j=1; j <= ncoef; j++ ) 
                                        { 
                                            xmin[ j ] = wk[ j ]; 
                                            xmax[ j ] = wk[ j ]; 
                                        } 
                                    } 
                                } 
                                for ( i=1; i <= ncoef; i++ ) 
                                { 
                                    temp = wk[ i ]; 
                                    if ( temp < xmin[ i ] )
                                    { 
                                        xmin[ i ] = temp; 
                                    } 
                                    if ( temp > xmax[ i ] )
                                    { 
                                        xmax[ i ] = temp; 
                                    } 
                                } 
                            } 
                            else 
                            { 
                                for ( i=intp1; i <= ncoef; i++ ) 
                                { 
                                    temp = wk[ i ]; 
                                    if ( temp == xmin[ i ] )
                                    { 
                                        ifault = 10; 
                                    } 
                                    if ( temp == xmax[ i ] )
                                    { 
                                        ifault = 11; 
                                    } 
                                } 
                            } 
                            if ( wt != 0.0 ) 
                            { 
                                double sd2 = wt * frq; 
                                if ( isub == 1 ) 
                                { 
                                    double sumwt = r[ 1, 1 ]; 
                                    r[ 1, 1 ] = sumwt + sd2; 
                                    if ( nind > 0 ) 
                                    { 
                                        for ( i=2; i <= nind + 1; i++ ) 
                                        { 
                                            r[ 1, i ] = r[ 1, i ] + sd2 * wk[ i ]; 
                                        } 
                                    } 
                                    for ( i=1; i <= ndep; i++ ) 
                                    { 
                                        b2[ 1, i ] = b2[ 1, i ] + sd2 * wk[ idepx - 1 + i ]; 
                                    } 
                                    skip = false; 
                                    if ( r[ 1, 1 ] != 0.0 ) 
                                    { 
                                        d[ 1 ] = 1.0 / r[ 1, 1 ]; 
                                        if ( nind > 0 ) 
                                        { 
                                            for ( i=2; i <= nind + 1; i++ ) 
                                            { 
                                                wk[ i ] = wk[ i ] - d[ 1 ] * r[ 1, i ]; 
                                            } 
                                        } 
                                        j = idepx; 
                                        for ( i=1; i <= ndep; i++ ) 
                                        { 
                                            wk[ j ] = wk[ j ] - d[ 1 ] * b2[ 1, i ]; 
                                            j = j + 1; 
                                        } 
                                        if ( sumwt == 0.0 ) 
                                        { 
                                            skip = true; 
                                        } 
                                        else 
                                        { 
                                            sd2 = sd2 * r[ 1, 1 ] / sumwt; 
                                        } 
                                    } 
                                    else 
                                    { 
                                        skip = true; 
                                    } 
                                } 
                                if ( skip == false ) 
                                { 
                                    for ( i=i1; i <= ncoef; i++ ) 
                                    { 
                                        drotmg( ref d[ i ], ref sd2, ref r[ i, i ], wk[ i ], sparam ); 
                                        drotm_21( ndep, b2, i, 1, wk, idepx, sparam ); 
                                        if ( i != ncoef ) 
                                        { 
                                            drotm_21( ncoef - i, r, i, i + 1, wk, i + 1, sparam ); 
                                        } 
                                    } 
                                    int jdepjx = idepx; 
                                    int jdepix = idepx; 
                                    scpe += wk[ jdepix ] * sd2 * wk[ jdepjx ]; 
                                    // jdepix = jdepix + 1; 
                                    // jdepjx = jdepjx + 1; 
                                } 
                            } 
                        } 
                    } 
                } 
            } 
            
            // drop collinear variables
            if ( ido == 0 | ido == 3 ) 
            { 
                int nconst = 0; 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    int ldep = 0;
                    int k ;
                    if ( xmin[ i ] == xmax[ i ] ) 
                    { 
                        if ( xmin[ i ] == 0.0 ) 
                        { 
                            ldep = 1; 
                        } 
                        else 
                        { 
                            nconst = nconst + 1; 
                            if ( nconst > 1 )
                            { 
                                ldep = 1; 
                            } 
                        } 
                    } 
                    else if ( i > intp1 ) 
                    { 
                        temp = 0.0; 
                        k = intp1; 
                        for ( j=1; j <= i - intcep; j++ ) 
                        { 
                            temp = temp + r[ k, i ] * d[ k ] * r[ k, i ]; 
                            k = k + 1; 
                        } 
                        if ( d[ i ] * r[ i, i ] * r[ i, i ] <= tolsq * temp )
                        { 
                            ldep = 1; 
                        } 
                    } 
                    if ( ldep == 1 ) 
                    { 
                        for ( j=i + 1; j <= ncoef; j++ ) 
                        { 
                            drotmg( ref d[ j ], ref d[ i ], ref r[ j, j ], r[ i, j ], sparam ); 
                            drotm_22( ndep, b2, j, 1, b2, i, 1, sparam ); 
                            if ( j != ncoef )
                            { 
                                drotm_22( ncoef - j, r, j, j + 1, r, i, j + 1, sparam ); 
                            } 
                        } 
                        scpe = scpe + b2[ i, 1 ] * d[ i ] * b2[ i, 1 ]; 
                        for ( k=1; k <= ndep; k++ ) 
                        { 
                            b2[ i, k ] = 0.0; 
                        } 
                        for ( k=0; k <= ncoef - i; k++ ) 
                        { 
                            r[ i, i + k ] = 0.0; 
                        } 
                    } 
                } 
                
                // calculate b by back-substitution
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    b[ i ] = b2[ i, 1 ]; 
                } 
                mxinv2( ncoef, r, b, true, false, false, r, out irank, ref ifault ); 
                dfe = dfe - irank; 
                if ( dfe <= 0.0 )
                { 
                    ifault = 12; 
                } 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    d[ i ] = dsign( Math.Sqrt( d[ i ] ), r[ i, i ] ); 
                } 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    for ( j=0; j <= ncoef - i; j++ ) 
                    { 
                        r[ i, i + j ] = r[ i, i + j ] * d[ i ]; 
                    } 
                } 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    d[ i ] = 1.0; 
                } 
                
            } 
            
        } 
        
        
        public static void glsqr1( int ido, int intcep, int isub, int nrow, int nvar, double[] x, int ldx, int iind, int[] indind, int idep, int[] inddep, int ifrq, int iwt, ref double[] b, ref double[,] r, ref double[] d, ref int irank, ref double dfe, ref double scpe, ref int nrmiss, ref double[] xmin, ref double[] xmax, ref double[] wk, ref int ifault ) 
        {
            int i , j , iobs , nobs ;
            int irow;

            double[] sparam = new double[5 + 1 /* for VB to C# conversion */ ];
            double frq = 0, temp , wt = 0;

            bool skip = false; 
            
            if ( ifault != 0 )
            { 
                return; 
            } 
            
            int ndep = Math.Abs( idep ); 
            int nind = Math.Abs( iind ); 
            int ncoef = intcep + nind; 
            int intp1 = intcep + 1; 
            int idepx = ncoef + 1;

            double[,] b2 = new double[ncoef + 1 /* for VB to C# conversion */, ncoef + 1 /* for VB to C# conversion */]; 
            
            // tolerance of 100 * largest relative spacing
            const double tol = 100.0 * Constant.EPSILON; 
            const double tolsq = tol * tol; 
            
            if ( ido <= 1 ) 
            { 
                nrmiss = 0; 
                dfe = 0.0; 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    for ( j=1; j <= ncoef; j++ ) 
                    { 
                        r[ j, i ] = 0.0; 
                    } 
                } 
                for ( i=1; i <= ndep; i++ ) 
                { 
                    for ( j=1; j <= ncoef; j++ ) 
                    { 
                        b2[ j, i ] = 0.0; 
                    } 
                } 
                for ( j=1; j <= ncoef; j++ ) 
                { 
                    d[ j ] = 1.0; 
                    xmin[ j ] = Constant.MISSING; 
                    xmax[ j ] = Constant.MISSING; 
                } 
                scpe = 0.0; 
            } 
            
            if ( nrow < 0 ) 
            { 
                nobs = -nrow; 
                irow = -1; 
            } 
            else 
            { 
                nobs = nrow; 
                irow = 1; 
            } 
            int i1 = isub == 0 ? 1 : 2; 
            
            for ( iobs=1; iobs <= nobs; iobs++ ) 
            {
                int igo;
                checkobs1( ido, x, ldx, iobs, irow, ifrq, iwt, Constant.MISSING, ref nrmiss, ref frq, ref wt, out igo, ref ifault ); 
                if ( igo == 3 )
                { 
                    return; 
                } 
                if ( igo != 2 & igo != 1 ) 
                { 
                    if ( intcep == 1 )
                    { 
                        wk[ 1 ] = 1.0; 
                    } 
                    for ( i=1; i <= iind; i++ ) 
                    { 
                        wk[ intcep + i ] = x[ iobs + ( indind[ i ] - 1 ) * ldx ]; 
                    } 
                    for ( i=1; i <= -iind; i++ ) 
                    { 
                        wk[ intcep + i ] = x[ iobs + ( i - 1 ) * ldx ]; 
                    } 
                    if ( ixnan( nind, wk, intp1 ) > 0 ) 
                    { 
                        nrmiss = nrmiss + irow; 
                    } 
                    else 
                    { 
                        int jdepx = idepx; 
                        for ( i=1; i <= idep; i++ ) 
                        { 
                            wk[ jdepx ] = x[ iobs + ( inddep[ i ] - 1 ) * ldx ]; 
                            jdepx = jdepx + 1; 
                        } 
                        for ( i=idep + 1; i <= 0; i++ ) 
                        { 
                            wk[ jdepx ] = x[ iobs + ( nvar + i - 1 ) * ldx ]; 
                            jdepx = jdepx + 1; 
                        } 
                        if ( ixnan( ndep, wk, idepx ) > 0 ) 
                        { 
                            nrmiss = nrmiss + irow; 
                        } 
                        else 
                        { 
                            dfe = dfe + frq; 
                            if ( irow == 1 ) 
                            { 
                                if ( ncoef > 0 ) 
                                { 
                                    if ( xmin[ 1 ] == Constant.MISSING ) 
                                    { 
                                        for ( j=1; j <= ncoef; j++ ) 
                                        { 
                                            xmin[ j ] = wk[ j ]; 
                                            xmax[ j ] = wk[ j ]; 
                                        } 
                                    } 
                                } 
                                for ( i=1; i <= ncoef; i++ ) 
                                { 
                                    temp = wk[ i ]; 
                                    if ( temp < xmin[ i ] )
                                    { 
                                        xmin[ i ] = temp; 
                                    } 
                                    if ( temp > xmax[ i ] )
                                    { 
                                        xmax[ i ] = temp; 
                                    } 
                                } 
                            } 
                            else 
                            { 
                                for ( i=intp1; i <= ncoef; i++ ) 
                                { 
                                    temp = wk[ i ]; 
                                    if ( temp == xmin[ i ] )
                                    { 
                                        ifault = 10; 
                                    } 
                                    if ( temp == xmax[ i ] )
                                    { 
                                        ifault = 11; 
                                    } 
                                } 
                            } 
                            if ( wt != 0.0 ) 
                            { 
                                double sd2 = wt * frq; 
                                if ( isub == 1 ) 
                                { 
                                    double sumwt = r[ 1, 1 ]; 
                                    r[ 1, 1 ] = sumwt + sd2; 
                                    if ( nind > 0 ) 
                                    { 
                                        for ( i=2; i <= nind + 1; i++ ) 
                                        { 
                                            r[ 1, i ] = r[ 1, i ] + sd2 * wk[ i ]; 
                                        } 
                                    } 
                                    for ( i=1; i <= ndep; i++ ) 
                                    { 
                                        b2[ 1, i ] = b2[ 1, i ] + sd2 * wk[ idepx - 1 + i ]; 
                                    } 
                                    skip = false; 
                                    if ( r[ 1, 1 ] != 0.0 ) 
                                    { 
                                        d[ 1 ] = 1.0 / r[ 1, 1 ]; 
                                        if ( nind > 0 ) 
                                        { 
                                            for ( i=2; i <= nind + 1; i++ ) 
                                            { 
                                                wk[ i ] = wk[ i ] - d[ 1 ] * r[ 1, i ]; 
                                            } 
                                        } 
                                        j = idepx; 
                                        for ( i=1; i <= ndep; i++ ) 
                                        { 
                                            wk[ j ] = wk[ j ] - d[ 1 ] * b2[ 1, i ]; 
                                            j = j + 1; 
                                        } 
                                        if ( sumwt == 0.0 ) 
                                        { 
                                            skip = true; 
                                        } 
                                        else 
                                        { 
                                            sd2 = sd2 * r[ 1, 1 ] / sumwt; 
                                        } 
                                    } 
                                    else 
                                    { 
                                        skip = true; 
                                    } 
                                } 
                                if ( skip == false ) 
                                { 
                                    for ( i=i1; i <= ncoef; i++ ) 
                                    { 
                                        drotmg( ref d[ i ], ref sd2, ref r[ i, i ], wk[ i ], sparam ); 
                                        drotm_21( ndep, b2, i, 1, wk, idepx, sparam ); 
                                        if ( i != ncoef ) 
                                        { 
                                            drotm_21( ncoef - i, r, i, i + 1, wk, i + 1, sparam ); 
                                        } 
                                    } 
                                    int jdepjx = idepx; 
                                    int jdepix = idepx; 
                                    scpe = scpe + wk[ jdepix ] * sd2 * wk[ jdepjx ]; 
                                    // jdepix = jdepix + 1; 
                                    // jdepjx = jdepjx + 1; 
                                } 
                            } 
                        } 
                    } 
                } 
            } 
            
            // drop collinear variables
            if ( ido == 0 | ido == 3 ) 
            { 
                int nconst = 0; 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    int ldep = 0;
                    int k;
                    if ( xmin[ i ] == xmax[ i ] ) 
                    { 
                        if ( xmin[ i ] == 0.0 ) 
                        { 
                            ldep = 1; 
                        } 
                        else 
                        { 
                            nconst = nconst + 1; 
                            if ( nconst > 1 )
                            { 
                                ldep = 1; 
                            } 
                        } 
                    } 
                    else if ( i > intp1 ) 
                    { 
                        temp = 0.0; 
                        k = intp1; 
                        for ( j=1; j <= i - intcep; j++ ) 
                        { 
                            temp = temp + r[ k, i ] * d[ k ] * r[ k, i ]; 
                            k = k + 1; 
                        } 
                        if ( d[ i ] * r[ i, i ] * r[ i, i ] <= tolsq * temp )
                        { 
                            ldep = 1; 
                        } 
                    } 
                    if ( ldep == 1 ) 
                    { 
                        for ( j=i + 1; j <= ncoef; j++ ) 
                        { 
                            drotmg( ref d[ j ], ref d[ i ], ref r[ j, j ], r[ i, j ], sparam ); 
                            drotm_22( ndep, b2, j, 1, b2, i, 1, sparam ); 
                            if ( j != ncoef )
                            { 
                                drotm_22( ncoef - j, r, j, j + 1, r, i, j + 1, sparam ); 
                            } 
                        } 
                        scpe = scpe + b2[ i, 1 ] * d[ i ] * b2[ i, 1 ]; 
                        for ( k=1; k <= ndep; k++ ) 
                        { 
                            b2[ i, k ] = 0.0; 
                        } 
                        for ( k=0; k <= ncoef - i; k++ ) 
                        { 
                            r[ i, i + k ] = 0.0; 
                        } 
                    } 
                } 
                
                // calculate b by back-substitution
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    b[ i ] = b2[ i, 1 ]; 
                } 
                mxinv2( ncoef, r, b, true, false, false, r, out irank, ref ifault ); 
                dfe = dfe - irank; 
                if ( dfe <= 0.0 )
                { 
                    ifault = 12; 
                } 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    d[ i ] = dsign( Math.Sqrt( d[ i ] ), r[ i, i ] ); 
                } 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    for ( j=0; j <= ncoef - i; j++ ) 
                    { 
                        r[ i, i + j ] = r[ i, i + j ] * d[ i ]; 
                    } 
                } 
                for ( i=1; i <= ncoef; i++ ) 
                { 
                    d[ i ] = 1.0; 
                } 
                
            } 
            
        } 
        
        /// <summary>
        /// Returns x with y's sign.
        /// </summary>
        private static double dsign( double x, double y ) 
        { 
            if ( y < 0.0 )
                return -Math.Abs( x ); 
            return Math.Abs( x );
        } 
        
        private static void checkobs( int ido, double[,] x, int iobs, int irow, int ifrq, int iwt, double xmiss, ref int nmiss, ref double frq, ref double wt, out int igo, ref int ifault ) 
        { 
            igo = 0; 
            if ( ifrq > 0 ) 
            { 
                frq = x[ iobs, ifrq ]; 
                if ( frq == Constant.MISSING ) 
                { 
                    nmiss = nmiss + irow; 
                    igo = 2; 
                } 
                else if ( frq == 0.0 ) 
                { 
                    igo = 1; 
                    return; 
                } 
            } 
            if ( iwt > 0 ) 
            { 
                wt = x[ iobs, iwt ]; 
                if ( wt == Constant.MISSING ) 
                { 
                    if ( igo != 2 ) 
                    { 
                        nmiss = nmiss + irow; 
                        igo = 2; 
                    } 
                } 
            } 
            if ( ifrq > 0 ) 
            { 
                if ( frq == Constant.MISSING ) 
                { 
                    if ( frq < 0.0 ) 
                    { 
                        ifault = ido > 0 ? 2 : 3; 
                        igo = 3; 
                        return; 
                    } 
                } 
            } 
            else 
            { 
                frq = 1.0; 
            } 
            if ( irow == -1 )
            { 
                frq = -frq; 
            } 
            if ( iwt > 0 ) 
            { 
                if ( wt == Constant.MISSING ) 
                { 
                    if ( wt < 0.0 ) 
                    { 
                        ifault = ido > 0 ? 5 : 6; 
                        igo = 3; 
                        return; 
                    } 
                } 
            } 
            else 
            { 
                wt = 1.0; 
            } 
        } 
        
        
        private static void checkobs1( int ido, double[] x, int ldx, int iobs, int irow, int ifrq, int iwt, double xmiss, ref int nmiss, ref double frq, ref double wt, out int igo, ref int ifault ) 
        { 
            igo = 0; 
            if ( ifrq > 0 ) 
            { 
                frq = x[ iobs + ( ifrq - 1 ) * ldx ]; 
                if ( frq == Constant.MISSING ) 
                { 
                    nmiss = nmiss + irow; 
                    igo = 2; 
                } 
                else if ( frq == 0.0 ) 
                { 
                    igo = 1; 
                    return; 
                } 
            } 
            if ( iwt > 0 ) 
            { 
                wt = x[ iobs + ( iwt - 1 ) * ldx ]; 
                if ( wt == Constant.MISSING ) 
                { 
                    if ( igo != 2 ) 
                    { 
                        nmiss = nmiss + irow; 
                        igo = 2; 
                    } 
                } 
            } 
            if ( ifrq > 0 ) 
            { 
                if ( frq == Constant.MISSING ) 
                { 
                    if ( frq < 0.0 ) 
                    { 
                        ifault = ido > 0 ? 2 : 3; 
                        igo = 3; 
                        return; 
                    } 
                } 
            } 
            else 
            { 
                frq = 1.0; 
            } 
            if ( irow == -1 )
            { 
                frq = -frq; 
            } 
            if ( iwt > 0 ) 
            { 
                if ( wt == Constant.MISSING ) 
                { 
                    if ( wt < 0.0 ) 
                    { 
                        ifault = ido > 0 ? 5 : 6; 
                        igo = 3; 
                        return; 
                    } 
                } 
            } 
            else 
            { 
                wt = 1.0; 
            } 
        } 
        
        
        private static int ixnan( int n, double[] sx, int ix ) 
        {
            //   smallest index of vector element = nan

            int iret = 0; 
            if ( n >= 0 ) 
            { 
                int i = ix; 
                int k = ix; 
                while ( k <= n ) 
                { 
                    if ( sx[ i ] == Constant.MISSING )
                    { 
                        iret = k; 
                    } 
                    i = i + 1; 
                    k = k + 1; 
                    if ( iret != 0 )
                    { 
                        break;
                    } 
                } 
            } 
            
            return iret; 
        } 
        
        /// <summary>
        /// blas modified givens rotations application
        /// </summary>
        /// <param name="n"></param>
        /// <param name="sx"></param>
        /// <param name="ix1"></param>
        /// <param name="ix2"></param>
        /// <param name="sy"></param>
        /// <param name="iy"></param>
        /// <param name="sparam"></param>
        private static void drotm_21( int n, double[,] sx, int ix1, int ix2, double[] sy, int iy, double[] sparam )
        {
            double sflag = sparam[ 1 ];
            if ( n > 0 & sflag != -2.0 )
            {
                int i;
                double sh12;
                double z;
                double w;
                double sh21;
                if ( sflag == 0.0 ) 
                { 
                    sh12 = sparam[ 4 ]; 
                    sh21 = sparam[ 3 ]; 
                    for ( i=0; i <= n - 1; i++ ) 
                    { 
                        w = sx[ ix1, ix2 + i ]; 
                        z = sy[ iy + i ]; 
                        sx[ ix1, ix2 + i ] = w + z * sh12; 
                        sy[ iy + i ] = w * sh21 + z; 
                    } 
                } 
                else
                {
                    double sh11;
                    double sh22;
                    if ( sflag > 0.0 ) 
                    { 
                        sh11 = sparam[ 2 ]; 
                        sh22 = sparam[ 5 ]; 
                        for ( i=0; i <= n - 1; i++ ) 
                        { 
                            w = sx[ ix1, ix2 + i ]; 
                            z = sy[ iy + i ]; 
                            sx[ ix1, ix2 + i ] = w * sh11 + z; 
                            sy[ iy + i ] = -w + sh22 * z; 
                        } 
                    } 
                    else if ( sflag < 0.0 ) 
                    { 
                        sh11 = sparam[ 2 ]; 
                        sh12 = sparam[ 4 ]; 
                        sh21 = sparam[ 3 ]; 
                        sh22 = sparam[ 5 ]; 
                        for ( i=0; i <= n - 1; i++ ) 
                        { 
                            w = sx[ ix1, ix2 + i ]; 
                            z = sy[ iy + i ]; 
                            sx[ ix1, ix2 + i ] = w * sh11 + z * sh12; 
                            sy[ iy + i ] = w * sh21 + z * sh22; 
                        } 
                    }
                }
            }
        }


        private static void drotm_22( int n, double[,] sx, int ix1, int ix2, double[,] sy, int iy1, int iy2, double[] sparam ) 
        { 
            
            //      blas modified givens rotations application

            double sflag = sparam[ 1 ]; 
            if ( n > 0 & sflag != -2.0 )
            {
                double sh12;
                int i;
                double z;
                double w;
                double sh21;
                if ( sflag == 0.0 ) 
                { 
                    sh12 = sparam[ 4 ]; 
                    sh21 = sparam[ 3 ]; 
                    for ( i=0; i <= n - 1; i++ ) 
                    { 
                        w = sx[ ix1, ix2 + i ]; 
                        z = sy[ iy1, iy2 + i ]; 
                        sx[ ix1, ix2 + i ] = w + z * sh12; 
                        sy[ iy1, iy2 + i ] = w * sh21 + z; 
                    } 
                } 
                else
                {
                    double sh11;
                    double sh22;
                    if ( sflag > 0.0 ) 
                    { 
                        sh11 = sparam[ 2 ]; 
                        sh22 = sparam[ 5 ]; 
                        for ( i=0; i <= n - 1; i++ ) 
                        { 
                            w = sx[ ix1, ix2 + i ]; 
                            z = sy[ iy1, iy2 + i ]; 
                            sx[ ix1, ix2 + i ] = w * sh11 + z; 
                            sy[ iy1, iy2 + i ] = -w + sh22 * z; 
                        } 
                    } 
                    else if ( sflag < 0.0 ) 
                    { 
                        sh11 = sparam[ 2 ]; 
                        sh12 = sparam[ 4 ]; 
                        sh21 = sparam[ 3 ]; 
                        sh22 = sparam[ 5 ]; 
                        for ( i=0; i <= n - 1; i++ ) 
                        { 
                            w = sx[ ix1, ix2 + i ]; 
                            z = sy[ iy1, iy2 + i ]; 
                            sx[ ix1, ix2 + i ] = w * sh11 + z * sh12; 
                            sy[ iy1, iy2 + i ] = w * sh21 + z * sh22; 
                        } 
                    }
                }
            }
        } 
        
        
        private static void drotmg( ref double d1, ref double d2, ref double x, double y, double[] p ) 
        { 
            
            //      blas modified givens rotations
            
            double u, h21, h11, h12, h22; 
            
            const double g = 4096; 
            const double g2 = g * g;

            if ( d1 < 0.0 ) 
            { 
                //  d1 < 0
                p[ 1 ] = -1.0; 
                p[ 2 ] = 0.0; 
                p[ 3 ] = 0.0; 
                p[ 4 ] = 0.0; 
                p[ 5 ] = 0.0; 
                d1 = 0.0; 
                d2 = 0.0; 
                x = 0.0; 
                return; 
            } 
            if ( d2 * y == 0.0 ) 
            { 
                //  h = i
                p[ 1 ] = -2.0; 
                return; 
            } 
            if ( Math.Abs( d1 * x * x ) > Math.Abs( d2 * y * y ) ) 
            { 
                //  equation a6
                p[ 1 ] = 0.0; 
                h11 = 1.0; 
                h12 = ( d2 * y ) / ( d1 * x ); 
                h21 = -y / x; 
                h22 = 1.0; 
                u = 1.0 - h21 * h12; 
                if ( u <= 0.0 ) 
                { 
                    //  reject u <= 0
                    p[ 1 ] = -1.0; 
                    p[ 2 ] = 0.0; 
                    p[ 3 ] = 0.0; 
                    p[ 4 ] = 0.0; 
                    p[ 5 ] = 0.0; 
                    d1 = 0.0; 
                    d2 = 0.0; 
                    x = 0.0; 
                    return; 
                } 
                d1 = d1 / u; 
                d2 = d2 / u; 
                x = x * u; 
            } 
            else 
            { 
                //  equation a7
                if ( d2 * y * y < 0.0 ) 
                { 
                    p[ 1 ] = -1.0; 
                    p[ 2 ] = 0.0; 
                    p[ 3 ] = 0.0; 
                    p[ 4 ] = 0.0; 
                    p[ 5 ] = 0.0; 
                    d1 = 0.0; 
                    d2 = 0.0; 
                    x = 0.0; 
                    return; 
                } 
                p[ 1 ] = 1.0; 
                h11 = ( d1 * x ) / ( d2 * y ); 
                h12 = 1.0; 
                h21 = -1.0; 
                h22 = x / y; 
                u = 1.0 + h11 * h22; 
                d1 = d1 / u; 
                d2 = d2 / u; 
                double tmp = d2; 
                d2 = d1; 
                d1 = tmp; 
                x = y * u; 
            } 
            
            //  rescale d1 in the range rg2, g2
            while ( ( d1 <= 1.0 / g2 & d1 != 0.0 ) ) 
            { 
                p[ 1 ] = -1.0; 
                d1 = d1 * g2; 
                x = x / g; 
                h11 = h11 / g; 
                h12 = h12 / g; 
            } 
            while ( ( d1 >= g2 ) ) 
            { 
                p[ 1 ] = -1.0; 
                d1 = d1 / g2; 
                x = x * g; 
                h11 = h11 * g; 
                h12 = h12 * g; 
            } 
            //  rescale d2 in the range rg2, g2
            while ( ( Math.Abs( d2 ) <= 1.0 / g2 & d2 != 0.0 ) ) 
            { 
                p[ 1 ] = -1.0; 
                d2 = d2 * g2; 
                h21 = h21 / g; 
                h22 = h22 / g; 
            } 
            while ( ( Math.Abs( d2 ) >= g2 ) ) 
            { 
                p[ 1 ] = -1.0; 
                d2 = d2 / g2; 
                h21 = h21 * g; 
                h22 = h22 * g; 
            } 
            //  populate the parameter array with rescaled values
            if ( ( p[ 1 ] == -1.0 ) ) 
            { 
                p[ 2 ] = h11; 
                p[ 3 ] = h21; 
                p[ 4 ] = h12; 
                p[ 5 ] = h22; 
            } 
            else if ( ( p[ 1 ] == 0.0 ) ) 
            { 
                p[ 3 ] = h21; 
                p[ 4 ] = h12; 
            } 
            else if ( ( p[ 1 ] == 1.0 ) ) 
            { 
                p[ 2 ] = h11; 
                p[ 5 ] = h22; 
            } 
            
        } 
        
        ///  <summary>
        ///  variance-covariance matrix from the r matrix
        ///  </summary>
        ///  <param name="ncoef"></param>
        ///  <param name="r"></param>
        ///  <param name="s2"></param>
        ///  <param name="covb"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        public static void rcovarb( int ncoef, double[,] r, double s2, double[,] covb, ref int ifault ) 
        { 
            int i, j, k, irank;

            mxinv2( ncoef, r, null, false, false, true, covb, out irank, ref ifault ); 
            
            if ( ifault != 0 )
            { 
                return; 
            } 
            
            for ( j=1; j <= ncoef; j++ ) 
            { 
                if ( covb[ j, j ] > 0.0 ) 
                {
                    double t;
                    for ( k=1; k <= j - 1; k++ ) 
                    { 
                        t = covb[ k, j ]; 
                        for ( i=1; i <= k; i++ ) 
                        { 
                            covb[ i, k ] = covb[ i, k ] + covb[ i, j ] * t; 
                        } 
                    } 
                    t = covb[ j, j ]; 
                    for ( k=1; k <= j; k++ ) 
                    { 
                        covb[ k, j ] = covb[ k, j ] * t; 
                    } 
                } 
                else 
                { 
                    for ( k=1; k <= j; k++ ) 
                    { 
                        covb[ k, j ] = 0.0; 
                    } 
                } 
            } 
            
            for ( j=1; j <= ncoef; j++ ) 
            { 
                for ( k=1; k <= j; k++ ) 
                { 
                    covb[ k, j ] = covb[ k, j ] * s2; 
                } 
            } 
            
            // fill in the lower triangle
            for ( i=1; i <= ncoef - 1; i++ ) 
            { 
                for ( j=i + 1; j <= ncoef; j++ ) 
                { 
                    covb[ j, i ] = covb[ i, j ]; 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  Solve a set of linear systems and/or compute a generalized inverse of upper triangular matrix
        ///  </summary>
        ///  <param name="n"></param>
        ///  <param name="r"></param>
        ///  <param name="b"></param>
        ///  <param name="use_b">true for all cases of old paths 1-4</param>
        ///  <param name="transpose_r">true to transpose (old path 2 or 4)</param>
        ///  <param name="invert_r">true for old paths 3, 4</param>
        ///  <param name="rinv"></param>
        ///  <param name="irank"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        public static void mxinv2( int n, double[,] r, double[] b, bool use_b, bool transpose_r, bool invert_r, double[,] rinv, out int irank, ref int ifault ) 
        { 
            int k, i, j, ii;
            double xddot, temp1;

            if ( ifault != 0 )
            {
                irank = 0;
                return; 
            } 
            for ( i=1; i <= n; i++ ) 
            { 
                if ( r[ i, i ] == 0.0 ) 
                { 
                    for ( j=i + 1; j <= n; j++ ) 
                    { 
                        if ( ( r[ i, j ] != 0.0 ) ) 
                        { 
                            ifault = 5;
                            irank = 0;
                            return; 
                        } 
                    } 
                } 
            } 
            if ( ifault != 0 )
            {
                irank = 0;
                return; 
            } 
            
            irank = 0; 
            for ( i=1; i <= n; i++ ) 
            { 
                if ( r[ i, i ] != 0.0 )
                { 
                    irank = irank + 1; 
                } 
            } 
            
            if ( transpose_r ) 
            { 
                if ( irank < n ) 
                { 
                    if ( use_b ) 
                    { 
                        for ( j=1; j <= n; j++ ) 
                        { 
                            xddot = 0.0; 
                            for ( k=1; k <= j - 1; k++ ) 
                            { 
                                xddot = xddot + r[ k, j ] * b[ k ]; 
                            } 
                            temp1 = b[ j ] - xddot; 
                            if ( r[ j, j ] == 0.0 ) 
                            { 
                                double absprod = 0.0; 
                                for ( ii=1; ii <= j - 1; ii++ ) 
                                { 
                                    absprod = absprod + Math.Abs( r[ ii, j ] * b[ ii ] ); 
                                } 
                                double temp2 = Math.Abs( b[ j ] ) + absprod; 
                                temp2 = temp2 * 200.0 * Constant.EPSILON; 
                                if ( Math.Abs( temp1 ) > temp2 ) 
                                { 
                                    ifault = 2; 
                                } 
                                b[ j ] = 0.0; 
                            } 
                            else 
                            { 
                                b[ j ] = temp1 / r[ j, j ]; 
                            } 
                        } 
                    } 
                } 
                else 
                { 
                    if ( use_b ) 
                    { 
                        for ( j=1; j <= n; j++ ) 
                        { 
                            xddot = 0.0; 
                            for ( k=1; k <= j - 1; k++ ) 
                            { 
                                xddot = xddot + r[ k, j ] * b[ k ]; 
                            } 
                            b[ j ] = b[ j ] - xddot; 
                            b[ j ] = b[ j ] / r[ j, j ]; 
                        } 
                    } 
                } 
            } 
            else 
            { 
                if ( irank < n ) 
                { 
                    if ( use_b ) 
                    { 
                        for ( j=n; j >= 1; j-- ) 
                        { 
                            if ( r[ j, j ] == 0.0 ) 
                            { 
                                if ( b[ j ] != 0.0 ) 
                                { 
                                    ifault = 1; 
                                } 
                                b[ j ] = 0.0; 
                            } 
                            else 
                            { 
                                b[ j ] = b[ j ] / r[ j, j ]; 
                                temp1 = -b[ j ]; 
                                for ( k=1; k <= j - 1; k++ ) 
                                { 
                                    b[ k ] = b[ k ] + temp1 * r[ k, j ]; 
                                } 
                            } 
                        } 
                    } 
                } 
                else 
                { 
                    if ( use_b ) 
                    { 
                        for ( j=n; j >= 1; j-- ) 
                        { 
                            if ( j < n ) 
                            { 
                                xddot = 0.0; 
                                for ( k=1; k <= n - j; k++ ) 
                                { 
                                    xddot = xddot + r[ j, j + k ] * b[ j + k ]; 
                                } 
                                b[ j ] = b[ j ] - xddot; 
                            } 
                            b[ j ] = b[ j ] / r[ j, j ]; 
                        } 
                    } 
                } 
            } 
            
            if ( invert_r ) 
            { 
                for ( j=1; j <= n; j++ ) 
                { 
                    for ( k=1; k <= j; k++ ) 
                    { 
                        rinv[ k, j ] = r[ k, j ]; 
                    } 
                } 
                for ( k=1; k <= n; k++ ) 
                { 
                    if ( rinv[ k, k ] == 0.0 ) 
                    { 
                        for ( i=1; i <= k; i++ ) 
                        { 
                            rinv[ i, k ] = 0.0; 
                        } 
                        if ( n != k ) 
                        { 
                            for ( i=1; i <= n - k; i++ ) 
                            { 
                                rinv[ k, k + i ] = 0.0; 
                            } 
                        } 
                    } 
                    else 
                    { 
                        rinv[ k, k ] = 1.0 / rinv[ k, k ]; 
                        temp1 = -rinv[ k, k ]; 
                        for ( i=1; i <= k - 1; i++ ) 
                        { 
                            rinv[ i, k ] = rinv[ i, k ] * temp1; 
                        } 
                        if ( k < n ) 
                        { 
                            for ( j=1; j <= n - k; j++ ) 
                            { 
                                for ( i=1; i <= k - 1; i++ ) 
                                { 
                                    rinv[ i, k + j ] = rinv[ i, k + j ] + rinv[ k, k + j ] * rinv[ i, k ]; 
                                } 
                            } 
                            for ( i=1; i <= n - k; i++ ) 
                            { 
                                rinv[ k, k + i ] = rinv[ k, k + i ] * rinv[ k, k ]; 
                            } 
                        } 
                    } 
                } 
                for ( i=1; i <= n - 1; i++ ) 
                { 
                    for ( ii=i + 1; ii <= n; ii++ ) 
                    { 
                        rinv[ ii, i ] = 0.0; 
                    } 
                } 
            } 
            
        } 
        
        
        private static void svd(int m, int n, double[] w, double[,] u, double[,] v, out int ierr, double[] rv1 ) 
        {
            int i, j, k, l = 0, ii;
            int kk;
            int l1 = 0;

            double f, h, s;

            //      this subroutine is a translation of the algol procedure svd,
            //      num. math. 14, 403-420(1970) by golub and reinsch.
            //      handbook for auto. comp., vol ii-linear algebra, 134-151(1971).
            
            //      this subroutine determines the singular value decomposition
            
            //      a=usv  of a real m by n rectangular matrix.  householder
            //      bidiagonalization and a variant of the qr algorithm are used.
            
            //      on input
            
            //         m is the number of rows of a (and u).
            
            //         n is the number of columns of a (and u) and the order of v.
            
            //         a contains the rectangular input matrix to be decomposed.
            
            //      on output
            
            //         a is unaltered (unless overwritten by u or v).
            
            //         w contains the n (non-negative) singular values of a (the
            //           diagonal elements of s).  they are unordered.  if an
            //           error exit is made, the singular values should be correct
            //           for indices ierr+1,ierr+2,...,n.
            
            //         u contains the matrix u (orthogonal column vectors) of the
            //           decomposition if matu has been set to  true   otherwise
            //           u is used as a temporary array.  u may coincide with a.
            //           if an error exit is made, the columns of u corresponding
            //           to indices of correct singular values should be correct.
            
            //         v contains the matrix v (orthogonal) of the decomposition if
            //           matv has been set to  true   otherwise v is not referenced.
            //           v may also coincide with a if u is not needed.  if an error
            //           exit is made, the columns of v corresponding to indices of
            //           correct singular values should be correct.
            
            //         ierr is set to
            //           zero       for normal return,
            //           k          if the k-th singular value has not been
            //                      determined after 30 iterations.
            
            //         rv1 is a temporary storage array.
            
            //      calls pythag for  dsqrt(a*a + b*b) .
            
            //      questions and comments should be directed to burton s. garbow,
            //      mathematics and computer science div, argonne national laboratory
            
            //      this version dated august 1983.
            
            //      ------------------------------------------------------------------
            
            ierr = 0; 
            //      .......... householder reduction to bidiagonal form ..........
            double g = 0.0; 
            double scale = 0.0; 
            double x = 0.0; 
            for ( i=1; i <= n; i++ ) 
            { 
                l = i + 1; 
                rv1[ i ] = scale * g; 
                g = 0.0; 
                s = 0.0; 
                scale = 0.0; 
                if ( i <= m ) 
                { 
                    for ( k=i; k <= m; k++ ) 
                    { 
                        scale = scale + Math.Abs( u[ k, i ] ); 
                    } 
                    if ( scale != 0.0 ) 
                    { 
                        for ( k=i; k <= m; k++ ) 
                        { 
                            u[ k, i ] = u[ k, i ] / scale; 
                            s = s + Math.Pow( u[ k, i ], 2.0 ); 
                        } 
                        f = u[ i, i ]; 
                        g = -dsign( Math.Sqrt( s ), f ); 
                        h = f * g - s; 
                        u[ i, i ] = f - g; 
                        if ( i != n ) 
                        { 
                            for ( j=l; j <= n; j++ ) 
                            { 
                                s = 0.0; 
                                for ( k=i; k <= m; k++ ) 
                                { 
                                    s = s + u[ k, i ] * u[ k, j ]; 
                                } 
                                f = s / h; 
                                for ( k=i; k <= m; k++ ) 
                                { 
                                    u[ k, j ] = u[ k, j ] + f * u[ k, i ]; 
                                } 
                            } 
                        } 
                        for ( k=i; k <= m; k++ ) 
                        { 
                            u[ k, i ] = scale * u[ k, i ]; 
                        } 
                    } 
                } 
                w[ i ] = scale * g; 
                g = 0.0; 
                s = 0.0; 
                scale = 0.0; 
                if ( i <= m & i != n ) 
                { 
                    for ( k=l; k <= n; k++ ) 
                    { 
                        scale = scale + Math.Abs( u[ i, k ] ); 
                    } 
                    if ( scale != 0.0 ) 
                    { 
                        for ( k=l; k <= n; k++ ) 
                        { 
                            u[ i, k ] = u[ i, k ] / scale; 
                            s = s + Math.Pow( u[ i, k ], 2.0 ); 
                        } 
                        f = u[ i, l ]; 
                        g = -dsign( Math.Sqrt( s ), f ); 
                        h = f * g - s; 
                        u[ i, l ] = f - g; 
                        for ( k=l; k <= n; k++ ) 
                        { 
                            rv1[ k ] = u[ i, k ] / h; 
                        } 
                        if ( i != m ) 
                        { 
                            for ( j=l; j <= m; j++ ) 
                            { 
                                s = 0.0; 
                                for ( k=l; k <= n; k++ ) 
                                { 
                                    s = s + u[ j, k ] * u[ i, k ]; 
                                } 
                                for ( k=l; k <= n; k++ ) 
                                { 
                                    u[ j, k ] = u[ j, k ] + s * rv1[ k ]; 
                                } 
                            } 
                        } 
                        for ( k=l; k <= n; k++ ) 
                        { 
                            u[ i, k ] = scale * u[ i, k ]; 
                        } 
                    } 
                } 
                x = Math.Max( x, Math.Abs( w[ i ] ) + Math.Abs( rv1[ i ] ) ); 
            } 
            //      .......... accumulation of right-hand transformations ..........
            //      .......... for i=n step -1 until 1 do -- ..........
            for ( ii=1; ii <= n; ii++ ) 
            { 
                i = n + 1 - ii; 
                if ( i != n ) 
                { 
                    if ( g != 0.0 ) 
                    { 
                        for ( j=l; j <= n; j++ ) 
                        { 
                            //          .......... double division avoids possible underflow ..........
                            v[ j, i ] = ( u[ i, j ] / u[ i, l ] ) / g; 
                        } 
                        for ( j=l; j <= n; j++ ) 
                        { 
                            s = 0.0; 
                            for ( k=l; k <= n; k++ ) 
                            { 
                                s = s + u[ i, k ] * v[ k, j ]; 
                            } 
                            for ( k=l; k <= n; k++ ) 
                            { 
                                v[ k, j ] = v[ k, j ] + s * v[ k, i ]; 
                            } 
                        } 
                    } 
                    for ( j=l; j <= n; j++ ) 
                    { 
                        v[ i, j ] = 0.0; 
                        v[ j, i ] = 0.0; 
                    } 
                } 
                v[ i, i ] = 1.0; 
                g = rv1[ i ]; 
                l = i; 
            } 
            //      .......... accumulation of left-hand transformations ..........
            //      ..........for i=min(m,n) step -1 until 1 do -- ..........
            int mn = n; 
            if ( m < n )
            { 
                mn = m; 
            } 
            for ( ii=1; ii <= mn; ii++ ) 
            { 
                i = mn + 1 - ii; 
                l = i + 1; 
                g = w[ i ]; 
                if ( i != n ) 
                { 
                    for ( j=l; j <= n; j++ ) 
                    { 
                        u[ i, j ] = 0.0; 
                    } 
                } 
                if ( g != 0.0 ) 
                { 
                    if ( i != mn ) 
                    { 
                        for ( j=l; j <= n; j++ ) 
                        { 
                            s = 0.0; 
                            for ( k=l; k <= m; k++ ) 
                            { 
                                s = s + u[ k, i ] * u[ k, j ]; 
                            } 
                            //          .......... double division avoids possible underflow ..........
                            f = ( s / u[ i, i ] ) / g; 
                            for ( k=i; k <= m; k++ ) 
                            { 
                                u[ k, j ] = u[ k, j ] + f * u[ k, i ]; 
                            } 
                        } 
                    } 
                    for ( j=i; j <= m; j++ ) 
                    { 
                        u[ j, i ] = u[ j, i ] / g; 
                    } 
                } 
                else 
                { 
                    for ( j=i; j <= m; j++ ) 
                    { 
                        u[ j, i ] = 0.0; 
                    } 
                } 
                u[ i, i ] = u[ i, i ] + 1.0; 
            } 
            //      .......... diagonalization of the bidiagonal form ..........
            double tst1 = x; 
            //      .......... for k=n step -1 until 1 do -- ..........
            for ( kk=1; kk <= n; kk++ ) 
            { 
                int k1 = n - kk; 
                k = k1 + 1; 
                int its = 0; 
                //      .......... test for splitting.
                //                 for l=k step -1 until 1 do -- ..........
                double z;
                do 
                { 
                    bool skip = false;
                    int ll;
                    double tst2;
                    for ( ll=1; ll <= k; ll++ ) 
                    { 
                        l1 = k - ll; 
                        l = l1 + 1; 
                        tst2 = tst1 + Math.Abs( rv1[ l ] ); 
                        if ( tst2 == tst1 ) 
                        { 
                            skip = true; 
                            break; /* TRANSWARNING: check that break is in correct scope */ 
                        } 
                        //      .......... rv1(1) is always zero, so there is no exit
                        //                 through the bottom of the loop ..........
                        tst2 = tst1 + Math.Abs( w[ l1 ] ); 
                        if ( tst2 == tst1 )
                        { 
                            break; /* TRANSWARNING: check that break is in correct scope */ 
                        } 
                    }
                    double c;
                    double y;
                    if ( skip == false ) 
                    { 
                        //      .......... cancellation of rv1(l) if l greater than 1 ..........
                        c = 0.0; 
                        s = 1.0; 
                        for ( i=l; i <= k; i++ ) 
                        { 
                            f = s * rv1[ i ]; 
                            rv1[ i ] = c * rv1[ i ]; 
                            tst2 = tst1 + Math.Abs( f ); 
                            if ( tst2 == tst1 )
                            { 
                                break; /* TRANSWARNING: check that break is in correct scope */ 
                            } 
                            g = w[ i ]; 
                            h = pythag( f, g ); 
                            w[ i ] = h; 
                            c = g / h; 
                            s = -f / h; 
                            for ( j=1; j <= m; j++ ) 
                            { 
                                y = u[ j, l1 ]; 
                                z = u[ j, i ]; 
                                u[ j, l1 ] = y * c + z * s; 
                                u[ j, i ] = -y * s + z * c; 
                            } 
                        } 
                    } 
                    //      .......... test for convergence ..........
                    z = w[ k ]; 
                    if ( l != k ) 
                    { 
                        //      .......... shift from bottom 2 by 2 minor ..........
                        if ( its >= 30 ) 
                        { 
                            //      .......... set error -- no convergence to a
                            //                 singular value after 30 iterations ..........
                            ierr = k; 
                            return; 
                        } 
                        its = its + 1; 
                        x = w[ l ]; 
                        y = w[ k1 ]; 
                        g = rv1[ k1 ]; 
                        h = rv1[ k ]; 
                        f = 0.5 * ( ( ( g + z ) / h ) * ( ( g - z ) / y ) + y / h - h / y ); 
                        g = pythag( f, 1.0 ); 
                        f = x - ( z / x ) * z + ( h / x ) * ( y / ( f + dsign( g, f ) ) - h ); 
                        //      .......... next qr transformation ..........
                        c = 1.0; 
                        s = 1.0;
                        int i1;
                        for ( i1=l; i1 <= k1; i1++ ) 
                        { 
                            i = i1 + 1; 
                            g = rv1[ i ]; 
                            y = w[ i ]; 
                            h = s * g; 
                            g = c * g; 
                            z = pythag( f, h ); 
                            rv1[ i1 ] = z; 
                            c = f / z; 
                            s = h / z; 
                            f = x * c + g * s; 
                            g = -x * s + g * c; 
                            h = y * s; 
                            y = y * c; 
                            for ( j=1; j <= n; j++ ) 
                            { 
                                x = v[ j, i1 ]; 
                                z = v[ j, i ]; 
                                v[ j, i1 ] = x * c + z * s; 
                                v[ j, i ] = -x * s + z * c; 
                            } 
                            z = pythag( f, h ); 
                            w[ i1 ] = z; 
                            //      .......... rotation can be arbitrary if z is zero ..........
                            if ( z != 0.0 ) 
                            { 
                                c = f / z; 
                                s = h / z; 
                            } 
                            f = c * g + s * y; 
                            x = -s * g + c * y; 
                            for ( j=1; j <= m; j++ ) 
                            { 
                                y = u[ j, i1 ]; 
                                z = u[ j, i ]; 
                                u[ j, i1 ] = y * c + z * s; 
                                u[ j, i ] = -y * s + z * c; 
                            } 
                        } 
                        rv1[ l ] = 0.0; 
                        rv1[ k ] = f; 
                        w[ k ] = x; 
                    } 
                    else 
                    { 
                        break; /* TRANSWARNING: check that break is in correct scope */ 
                    } 
                } 
                while ( true ); 
                //      .......... convergence ..........
                if ( z < 0.0 ) 
                { 
                    //      .......... w(k) is made non-negative ..........
                    w[ k ] = -z; 
                    for ( j=1; j <= n; j++ ) 
                    { 
                        v[ j, k ] = -v[ j, k ]; 
                    } 
                } 
            } 
            
        } 
        
        /// <summary>
        /// finds dsqrt(a**2+b**2) without overflow or destructive underflow
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static double pythag( double a, double b ) 
        {
            double p = Math.Max( Math.Abs( a ), Math.Abs( b ) ); 
            if ( p != 0.0 )
            {
                double r = Math.Pow( ( Math.Min( Math.Abs( a ), Math.Abs( b ) ) / p ), 2.0 );
                do 
                { 
                    double t = 4.0 + r; 
                    if ( t == 4.0 )
                    { 
                        break;
                    } 
                    double s = r / t; 
                    double u = 1.0 + 2.0 * s; 
                    p = u * p; 
                    r = Math.Pow( ( s / u ), 2.0 ) * r; 
                } 
                while ( true );
            }
            return p; 
        } 
        
        public static void x_dwsd( double[] er, int nx, out double dw ) 
        { 
            double[] erd = new double[nx + 1 /* for VB to C# conversion */ ]; 
            int iseas = 0; 
            const int idif = 1; 
            x_difd(  er,  erd,  nx,  idif, ref iseas ); 
            erd[ 1 ] = 0.0; 
            double sum1 = 0.0; 
            double sum2 = 0.0; 
            for (int i=1; i <= nx; i++ ) 
            { 
                sum1 = sum1 + erd[ i ] * erd[ i ]; 
                sum2 = sum2 + er[ i ] * er[ i ]; 
            } 
            dw = sum1 / sum2; 
        } 
        
        
        private static void x_difd( double[] xd, double[] yd, int nx, int idif, ref int nd ) 
        { 
            if ( idif <= 0 )
                return; 
            int ix = idif + 1; 
            nd = 0; 
            for (int i=ix; i <= nx; i++ ) 
            { 
                nd++;
                yd[ nd ] = xd[ i ] - xd[ i - idif ]; 
            } 
        } 
        
        
        public static void x_ciyp( double[] XV, double[,] xtxi, int P, double rms, double cit, out double cl, out double pl ) 
        {
            double xcx = 0; 
            
            for (int i=1; i <= P; i++ ) 
            { 
                double sLoop = 0.0;
                for (int j=1; j <= P; j++ ) 
                {
                    sLoop += xtxi[i, j] * XV[j]; 
                }
                xcx += sLoop * XV[i]; 
            } 
            double sey = Math.Sqrt( rms * xcx ); 
            cl = cit * sey; 
            double s = Math.Sqrt( rms * ( 1.0 + xcx ) ); 
            pl = cit * s; 
        } 
        
        
        
        ///  <summary>
        ///  trapezoidal numerical recipies p 131
        ///  </summary>
        ///  <param name="a"></param>
        ///  <param name="b"></param>
        ///  <param name="s">Output</param>
        ///  <param name="n"></param>
        ///  <param name="bd"></param>
        ///  <param name="ip"></param>
        ///  <remarks></remarks>
        public static void trapzd( double a, double b, ref double s, int n, double[] bd, int ip ) 
        { 
            if ( n == 1 ) 
            { 
                s = 0.5 * ( b - a ) * ( polyfunc( a, bd, ip ) + polyfunc( b, bd, ip ) ); 
            } 
            else 
            { 
                int it = Convert.ToInt32( Math.Pow( 2, ( n - 2 ) ) ); 
                double tnm = it; 
                double del = ( b - a ) / tnm; 
                double x = a + 0.5 * del; 
                double sum = 0.0; 
                for ( int j=1; j <= it; j++ ) 
                { 
                    sum += polyfunc( x, bd, ip ); 
                    x += del; 
                } 
                s = 0.5 * ( s + ( b - a ) * sum / tnm ); 
            } 
        } 
        
        
        ///  <summary>
        ///  expand a polynomial
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="bd"></param>
        ///  <param name="ip"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public static double polyfunc( double x, double[] bd, int ip ) 
        { 
            double pf; 
            if ( x == 0.0 ) 
            { 
                pf = bd[ 1 ]; 
            } 
            else 
            { 
                pf = bd[ 1 ] + bd[ 2 ] * x; 
                if ( ip > 2 ) 
                { 
                    for ( int j=3; j <= ip; j++ ) 
                    { 
                        pf += bd[ j ] * Math.Pow( x, Convert.ToDouble( j - 1 ) ); 
                    } 
                } 
            } 
            return pf; 
        }


        ///  <summary>
        ///  numerical recipies p103
        ///  </summary>
        ///  <param name="xa"></param>
        ///  <param name="ya"></param>
        /// <param name="StartIndex"></param>
        /// <param name="n"></param>
        ///  <param name="x"></param>
        ///  <param name="y"></param>
        ///  <param name="dy"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        public static void polint( double[] xa, double[] ya, int StartIndex, int n, double x, out double y, ref double dy, ref int ifault ) 
        { 
            const int nmax = 10;
            double[] c = new double[nmax + 1 /* for VB to C# conversion */ ];
            double[] d = new double[nmax + 1 /* for VB to C# conversion */ ]; 
            int ns = 1; 
            double dif = Math.Abs( x - xa[ StartIndex ] ); 
            for ( int i=1; i <= n; i++ ) 
            { 
                double dift = Math.Abs( x - xa[ i + StartIndex - 1 ] ); 
                if ( dift < dif ) 
                { 
                    ns = i; 
                    dif = dift; 
                } 
                c[ i ] = ya[ i + StartIndex - 1 ]; 
                d[ i ] = c[ i ]; 
            } 
            y = ya[ ns ]; 
            ns -= 1; 
            for ( int m=1; m <= n - 1; m++ ) 
            { 
                for ( int i=1; i <= n - m; i++ ) 
                { 
                    double ho = xa[ i + StartIndex - 1 ] - x; 
                    double hp = xa[ i + m + StartIndex - 1 ] - x; 
                    double w = c[ i + 1 ] - d[ i ]; 
                    double den = ho - hp; 
                    if ( den == 0.0 ) 
                    { 
                        ifault = 1; 
                        return ; 
                    } 
                    den = w / den; 
                    d[ i ] = hp * den; 
                    c[ i ] = ho * den; 
                } 
                if ( 2 * ns < n - m ) 
                { 
                    dy = c[ ns + 1 ]; 
                } 
                else 
                { 
                    dy = d[ ns ]; 
                    ns = ns - 1; 
                } 
                y = y + dy; 
            } 
        }


        ///  <summary>
        ///  generalized linear modelling by iterative least squares (SVD)
        ///  </summary>
        ///  <param name="mean"></param>
        ///  <param name="ioffs"></param>
        ///  <param name="iweight"></param>
        ///  <param name="N"></param>
        ///  <param name="x"></param>
        ///  <param name="M"></param>
        ///  <param name="isx"></param>
        ///  <param name="ip"></param>
        ///  <param name="y"></param>
        ///  <param name="t"></param>
        ///  <param name="wt"></param>
        ///  <param name="dev"></param>
        ///  <param name="idf"></param>
        ///  <param name="b"></param>
        ///  <param name="irank"></param>
        ///  <param name="se"></param>
        ///  <param name="cov"></param>
        ///  <param name="tol"></param>
        ///  <param name="maxit"></param>
        ///  <param name="fvl"></param>
        ///  <param name="var"></param>
        ///  <param name="dr"></param>
        ///  <param name="h"></param>
        ///  <param name="offst"></param>
        ///  <param name="ierror"></param>
        /// <param name="msg"></param>
        /// <remarks>Poisson errors
        ///  log link</remarks>
        public static void X_POISREG( bool mean, bool ioffs, ref bool iweight, int N, double[,] x, int M, int[] isx, int ip, double[] y, double[] t, double[] wt, ref double dev, ref int idf, double[] b, ref int irank, double[] se, double[] cov, double tol, int maxit, double[] fvl, double[] var, double[] dr, double[] h, double[] offst, out int ierror, ref string msg ) 
        {
            double ti = 0;

            double[] eta = new double[N + 1 /* for VB to C# conversion */];
            double[,] Q = new double[N + 1 /* for VB to C# conversion */, N + 1 /* for VB to C# conversion */];
            double[] wwt = new double[N + 1 /* for VB to C# conversion */];
            double[] WK = new double[N * 2 + 1 /* for VB to C# conversion */]; 
            double eps = Constant.EPSILON; 
            if ( N < 2 ) 
            { 
                ierror = 1; 
            } 
            else if ( M < 1 ) 
            { 
                ierror = 1; 
            } 
            else if ( ip < 1 ) 
            { 
                ierror = 1; 
            } 
            else if ( maxit < 0 ) 
            { 
                ierror = 1; 
            } 
            else if ( tol < 0 ) 
            { 
                ierror = 1; 
            } 
            else if ( eps < 0 ) 
            { 
                ierror = 1; 
            } 
            else 
            { 
                ierror = 0; 
            } 
            if ( ierror != 1 ) 
            { 
                const double acc = Constant.EPSNEG;
                int maxita = maxit == 0 ? 10 : maxit;
                double tola = tol < acc ? acc*10.0 : tol;
                double epsa = eps < acc ? acc : eps;
                int i ;
                int no ;
                if ( iweight ) 
                { 
                    no = 0; 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] < 0.0 ) 
                        { 
                            ierror = 2; 
                            return; 
                        } 
                        if ( wt[ i ] > 0.0 )
                        { 
                            no = no + 1; 
                        } 
                    } 
                } 
                else 
                { 
                    no = N; 
                } 
                int k = 0;
                int j ;
                for ( j=1; j <= M; j++ )
                {
                    if ( isx[ j ] < 0 ) 
                    { 
                        ierror = 3; 
                        return; 
                    }
                    if ( isx[ j ] > 0 ) 
                    { 
                        k = k + 1; 
                    }
                }
                if ( mean )
                { 
                    k = k + 1; 
                } 
                if ( ip != k ) 
                { 
                    ierror = 3; 
                    return; 
                }
                if ( ip > no ) 
                { 
                    ierror = 3; 
                    return; 
                }
                if ( iweight ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] > 0.0 ) 
                        { 
                            if ( y[ i ] < 0.0 ) 
                            { 
                                ierror = 5; 
                                return; 
                            } 
                        } 
                    } 
                } 
                else 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( y[ i ] < 0.0 ) 
                        { 
                            ierror = 5; 
                            return; 
                        } 
                    } 
                } 
                if ( ioffs == false ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        offst[ i ] = 0.0; 
                    } 
                } 
                // get starting values for linear predictor (eta) and fitted values (fvl)
                X_PLRINIT( N, y, fvl, eta, wt, no ); 
                // iteratively re-weighted least squares by SVD
                int ifault ;
                int iter ;
                X_GENWLS( 2, mean, ref iweight, N, x, M, isx, y, t, wt, ref no, ref dev, out irank, b, ip, fvl, eta, var, wwt, offst, Q, tola, maxita, out iter, epsa, WK, out ifault, ref msg ); 
                // IEB July 2009: Call again if boundaries hit so that completely determined observations have zero weight   
                if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ msg.Length > 0 )
                { 
                    X_GENWLS( 2, mean, ref iweight, N, x, M, isx, y, t, wt, ref no, ref dev, out irank, b, ip, fvl, eta, var, wwt, offst, Q, tola, maxita, out iter, epsa, WK, out ifault, ref msg ); 
                } 
                if ( ifault == 1 ) 
                { 
                    ierror = 6; 
                    return; 
                }
                if ( ifault == 2 ) 
                { 
                    ierror = 7; 
                    return; 
                }
                if ( ifault == 3 ) 
                { 
                    ierror = 8; 
                } 
                else if ( ifault == 4 ) 
                { 
                    ierror = 9; 
                }
                idf = no - irank; 
                if ( idf <= 0 ) 
                { 
                    ierror = 10; 
                } 
                else 
                { 
                    // get leverages from matrix of derivatives
                    X_BLRTRI( mean, N, M, x, isx, ip, Q, irank, wwt, h, WK ); 
                } 
                if ( iweight == false ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        dr[ i ] = Math.Sqrt( X_PLRDEV( fvl[ i ], y[ i ], ref ti ) ); 
                        if ( y[ i ] < fvl[ i ] | y[ i ] == 0.0 )
                        { 
                            dr[ i ] = -dr[ i ]; 
                        } 
                    } 
                } 
                else 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] > 0.0 ) 
                        { 
                            dr[ i ] = Math.Sqrt( wt[ i ] * X_PLRDEV( fvl[ i ], y[ i ], ref ti ) ); 
                            if ( y[ i ] < fvl[ i ] | y[ i ] == 0.0 )
                            { 
                                dr[ i ] = -dr[ i ]; 
                            } 
                        } 
                        else 
                        { 
                            dr[ i ] = 0.0; 
                        } 
                    } 
                } 
                // get variance-covariance matrix from SVD
                X_BLRSET( ip, irank, Q, cov, WK ); 
                for ( j=1; j <= ip; j++ ) 
                { 
                    if ( ( cov[ ( ( int )( Math.Floor((j * j + j) / 2.0) ) ) ] > 0.0 ) ) 
                    { 
                        se[ j ] = Math.Sqrt( cov[ ( ( int )( Math.Floor((j * j + j) / 2.0) ) ) ] ); 
                    } 
                    else 
                    { 
                        se[ j ] = 0.0; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  generalized linear modelling by iterative least squares (SVD)
        ///  
        ///  binomial errors
        ///  logistic link
        ///  </summary>
        public static void X_LOGIREG( bool mean, bool ioffs, ref bool iweight, int N, double[,] x, int M, int[] isx, int ip, double[] y, double[] t, double[] wt, ref double dev, ref int idf, double[] b, ref int irank, double[] se, double[] cov, double tol, int maxit, double[] fvl, double[] var, double[] dr, double[] h, double[] offst, out int ierror, ref string msg ) 
        {
            int no = 0;

            double[] eta = new double[N + 1 /* for VB to C# conversion */ ];
            double[,] Q = new double[N + 1 /* for VB to C# conversion */, N + 1 /* for VB to C# conversion */];
            double[] wwt = new double[N + 1 /* for VB to C# conversion */ ];
            double[] WK = new double[N * 2 + 1 /* for VB to C# conversion */ ]; 
            const double eps = Constant.EPSILON; 
            if ( N < 2 ) 
            { 
                ierror = 1; 
            } 
            else if ( M < 1 ) 
            { 
                ierror = 1; 
            } 
            else if ( ip < 1 ) 
            { 
                ierror = 1; 
            } 
            else if ( maxit < 0 ) 
            { 
                ierror = 1; 
            } 
            else if ( tol < 0 ) 
            { 
                ierror = 1; 
            } 
            else 
            { 
                ierror = 0; 
            } 
            if ( ierror != 1 ) 
            { 
                const double acc = Constant.EPSNEG;
                int maxita = maxit == 0 ? 10 : maxit;
                double tola = tol < acc ? acc*10.0 : tol;
                double epsa = eps < acc ? acc : eps;
                int i ;
                if ( iweight ) 
                { 
                    no = 0; 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] < 0.0 ) 
                        { 
                            ierror = 2; 
                            return; 
                        } 
                        if ( wt[ i ] > 0.0 & t[ i ] > 0.0 )
                        { 
                            no = no + 1; 
                        } 
                    } 
                } 
                int k = 0;
                int j ;
                for ( j=1; j <= M; j++ )
                {
                    if ( isx[ j ] < 0 ) 
                    { 
                        ierror = 3; 
                        return; 
                    }
                    if ( isx[ j ] > 0 ) 
                    { 
                        k = k + 1; 
                    }
                }
                if ( mean )
                { 
                    k = k + 1; 
                } 
                if ( iweight ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] > 0.0 ) 
                        { 
                            if ( t[ i ] < 0.0 ) 
                            { 
                                ierror = 4; 
                                return; 
                            } 
                        } 
                    } 
                } 
                else 
                { 
                    no = 0; 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( t[ i ] < 0.0 ) 
                        { 
                            ierror = 4; 
                            return; 
                        } 
                        if ( t[ i ] > 0.0 )
                        { 
                            no = no + 1; 
                        } 
                    } 
                } 
                if ( ip != k ) 
                { 
                    // nrec = 2; 
                    ierror = 3; 
                    return; 
                }
                if ( ip > no ) 
                { 
                    ierror = 3; 
                    return; 
                }
                if ( iweight ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] > 0.0 ) 
                        { 
                            if ( y[ i ] < 0.0 | y[ i ] > t[ i ] ) 
                            { 
                                ierror = 5; 
                                return; 
                            } 
                        } 
                    } 
                } 
                else 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( y[ i ] < 0.0 | y[ i ] > t[ i ] ) 
                        { 
                            ierror = 5; 
                            return; 
                        } 
                    } 
                } 
                if ( ioffs == false ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        offst[ i ] = 0.0; 
                    } 
                } 
                // get starting values for linear predictor (eta) and fitted values (fvl)
                int ind ;
                X_BLRINIT( N, y, t, fvl, eta, wt, no, out ind ); 
                if ( ind == 0 ) 
                { 
                    ierror = 6; 
                    return; 
                } 
                // iteratively re-weighted least squares by SVD
                int iter ;
                int ifault ;
                X_GENWLS( 1, mean, ref iweight, N, x, M, isx, y, t, wt, ref no, ref dev, out irank, b, ip, fvl, eta, var, wwt, offst, Q, tola, maxita, out iter, epsa, WK, out ifault, ref msg ); 
                // IEB July 2009: Call again if boundaries hit so that completely determined observations have zero weight   
                if ( msg.Length > 0 )
                { 
                    X_GENWLS( 1, mean, ref iweight, N, x, M, isx, y, t, wt, ref no, ref dev, out irank, b, ip, fvl, eta, var, wwt, offst, Q, tola, maxita, out iter, epsa, WK, out ifault, ref msg ); 
                } 
                if ( ifault == 1 ) 
                { 
                    ierror = 6; 
                    return; 
                }
                if ( ifault == 2 ) 
                { 
                    ierror = 7; 
                    return; 
                }
                if ( ifault == 3 ) 
                { 
                    ierror = 8; 
                } 
                else if ( ifault == 4 ) 
                { 
                    ierror = 9; 
                }
                idf = no - irank; 
                if ( idf <= 0 ) 
                { 
                    ierror = 10; 
                } 
                else 
                { 
                    // get leverages from matrix of derivatives
                    X_BLRTRI( mean, N, M, x, isx, ip, Q, irank, wwt, h, WK ); 
                } 
                if ( iweight == false ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( t[ i ] > 0.0 ) 
                        { 
                            dr[ i ] = Math.Sqrt( X_BLRDEV( fvl[ i ], y[ i ], t[ i ] ) ); 
                            if ( y[ i ] < fvl[ i ] | y[ i ] == 0.0 )
                            { 
                                dr[ i ] = -dr[ i ]; 
                            } 
                        } 
                        else 
                        { 
                            dr[ i ] = 0.0; 
                        } 
                    } 
                } 
                else 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] > 0.0 ) 
                        { 
                            if ( t[ i ] > 0.0 ) 
                            { 
                                dr[ i ] = Math.Sqrt( wt[ i ] * X_BLRDEV( fvl[ i ], y[ i ], t[ i ] ) ); 
                                if ( y[ i ] < fvl[ i ] | y[ i ] == 0.0 )
                                { 
                                    dr[ i ] = -dr[ i ]; 
                                } 
                            } 
                            else 
                            { 
                                dr[ i ] = 0.0; 
                            } 
                        } 
                        else 
                        { 
                            dr[ i ] = 0.0; 
                        } 
                    } 
                } 
                // get variance-covariance matrix from SVD
                X_BLRSET( ip, irank, Q, cov, WK ); 
                for ( j=1; j <= ip; j++ ) 
                { 
                    int idx = ( ( int )( Math.Floor((j * j + j) / 2.0) ) ); 
                    if ( ( cov[ idx ] > 0.0 ) ) 
                    { 
                        se[ j ] = Math.Sqrt( cov[ idx ] ); 
                    } 
                    else 
                    { 
                        se[ j ] = 0.0; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  get starting values for linear predictor (eta) and fitted values (fvl) for logistic regression
        ///  </summary>
        private static void X_PLRINIT( int N, double[] y, double[] fvl, double[] eta, double[] wt, int no ) 
        { 
            int i; 
            if ( N == no ) 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    if ( y[ i ] > 0.0 ) 
                    { 
                        fvl[ i ] = y[ i ]; 
                        eta[ i ] = Math.Log( y[ i ] ); 
                    } 
                    else 
                    { 
                        fvl[ i ] = 1.0; 
                        eta[ i ] = 0.0; 
                    } 
                } 
            } 
            else 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] > 0.0 ) 
                    { 
                        if ( y[ i ] > 0.0 ) 
                        { 
                            fvl[ i ] = y[ i ]; 
                            eta[ i ] = Math.Log( y[ i ] ); 
                        } 
                        else 
                        { 
                            fvl[ i ] = 1.0; 
                            eta[ i ] = 0.0; 
                        } 
                    } 
                    else 
                    { 
                        eta[ i ] = 0.0; 
                        fvl[ i ] = 0.0; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  get starting values for linear predictor (eta) and fitted values (fvl) for logistic regression
        ///  </summary>
        private static void X_BLRINIT( int N, double[] y, double[] t, double[] fvl, double[] eta, double[] wt, int no, out int ind ) 
        { 
            ind = 1; 
            if ( N == no ) 
            { 
                for ( int i=1; i <= N; i++ ) 
                { 
                    fvl[ i ] = t[ i ] * ( y[ i ] + 0.5 ) / ( t[ i ] + 1.0 ); 
                    eta[ i ] = Math.Log( fvl[ i ] / ( t[ i ] - fvl[ i ] ) ); 
                } 
            } 
            else 
            { 
                for ( int i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] == 0.0 | t[ i ] == 0.0 ) 
                    { 
                        fvl[ i ] = 0.0; 
                        eta[ i ] = 0.0; 
                    } 
                    else 
                    { 
                        fvl[ i ] = t[ i ] * ( y[ i ] + 0.5 ) / ( t[ i ] + 1.0 ); 
                        eta[ i ] = Math.Log( fvl[ i ] / ( t[ i ] - fvl[ i ] ) ); 
                    } 
                } 
            } 
        } 
        
        
        private static void X_GENWLS( int Model, bool mean, ref bool iweight, int N, double[,] x, int M, int[] isx, double[] y, double[] t, double[] wt, ref int no, ref double dev, out int irank, double[] b, int ip, double[] fvl, double[] eta, double[] var, double[] wwt, double[] offst, double[,] Q, double tol, int maxit, out int iter, double eps, double[] WK, out int ierror, ref string msg ) 
        { 
            int i ; int k ;
            int j ; int irank1 = 0;
            double dev1 = 0;

            ierror = 0; 
            irank = ip; 
            bool final = false; 
            int indqy = 1; 
            iter = 0; 
            // setup x matrix
            if ( mean ) 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    for ( k=1; k <= M; k++ ) 
                    { 
                        Q[ i, k ] = 1.0; 
                    } 
                } 
                k = 1; 
            } 
            else 
            { 
                k = 0; 
            } 
            for ( j=1; j <= M; j++ ) 
            { 
                if ( isx[ j ] > 0 ) 
                { 
                    k = k + 1; 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        Q[ i, k ] = x[ i, j ]; 
                    } 
                } 
            } 
            // working weights and response
            do 
            { 
                iter = iter + 1; 
                // get derivative then variance
                switch ( Model ) 
                {
                    case 1:
                        x_blrder( N, eta, t, wwt, wt, no ); 
                        x_blrvar( N, fvl, t, var, wt, no ); 
                        break;
                    case 2:
                        x_plrder( N, eta, wwt, wt, no ); 
                        x_plrvar( N, fvl, var, wt, no ); 
                        break;
                }
                
                if ( final == false ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        fvl[ i ] = ( ( eta[ i ] - offst[ i ] ) * wwt[ i ] + y[ i ] - fvl[ i ] ) * var[ i ]; 
                    } 
                } 
                for ( i=1; i <= N; i++ ) 
                { 
                    wwt[ i ] = var[ i ] * wwt[ i ]; 
                } 
                if ( iweight ) 
                { 
                    if ( final ) 
                    { 
                        for ( i=1; i <= N; i++ ) 
                        { 
                            if ( wt[ i ] > 0.0 )
                            { 
                                wwt[ i ] = wwt[ i ] * Math.Sqrt( wt[ i ] ); 
                            } 
                        } 
                    } 
                    else 
                    { 
                        for ( i=1; i <= N; i++ ) 
                        { 
                            if ( wt[ i ] > 0.0 ) 
                            { 
                                double sqwt = Math.Sqrt( wt[ i ] ); 
                                wwt[ i ] = wwt[ i ] * sqwt; 
                                fvl[ i ] = fvl[ i ] * sqwt; 
                            } 
                        } 
                    } 
                } 
                for ( i=1; i <= ip; i++ ) 
                { 
                    if ( N > 0 ) 
                    { 
                        for ( j=1; j <= N; j++ ) 
                        { 
                            Q[ j, i ] = wwt[ j ] * Q[ j, i ]; 
                        } 
                    } 
                } 
                int ifault = -1; 
                X_M_QRMN( N, ip, Q, N, WK, ref ifault ); 
                if ( final == false )
                { 
                    X_M_QRHT( N, ip, Q, N, WK, fvl, N ); 
                }
                X_E_SVDX( ip, Q, N, indqy, fvl, ip, WK, out ifault ); 
                if ( ifault != 0 ) 
                { 
                    ierror = 2; 
                } 
                else 
                { 
                    irank = isrank( ip, WK ); 
                    if ( iter == 1 ) 
                    { 
                        irank1 = irank; 
                    } 
                    else 
                    { 
                        if ( irank != irank1 ) 
                        { 
                            ierror = 4; 
                            // Exit Sub
                        } 
                    } 
                    for ( i=1; i <= irank; i++ ) 
                    { 
                        WK[ i ] = 1.0 / WK[ i ]; 
                    } 
                    for ( i=1; i <= ip; i++ ) 
                    { 
                        if ( N > 0 ) 
                        { 
                            for ( j=1; j <= irank; j++ ) 
                            { 
                                Q[ j, i ] = WK[ j ] * Q[ j, i ]; 
                            } 
                        } 
                    } 
                } 
                if ( final ) 
                { 
                    return; 
                }
                for ( i=1; i <= ip; i++ ) 
                { 
                    b[ i ] = 0.0; 
                } 
                for ( j=1; j <= irank; j++ ) 
                { 
                    if ( fvl[ j ] != 0.0 ) 
                    { 
                        for ( i=1; i <= ip; i++ ) 
                        { 
                            b[ i ] = b[ i ] + fvl[ j ] * Q[ j, i ]; 
                        } 
                    } 
                } 
                if ( mean ) 
                { 
                    k = 1; 
                    for ( i=1; i <= ip; i++ ) 
                    { 
                        if ( N > 0 ) 
                        { 
                            for ( j=1; j <= N; j++ ) 
                            { 
                                Q[ j, i ] = 1.0; 
                            } 
                        } 
                    } 
                } 
                else 
                { 
                    k = 0; 
                } 
                for ( j=1; j <= M; j++ ) 
                { 
                    if ( isx[ j ] > 0 ) 
                    { 
                        k = k + 1; 
                        for ( i=1; i <= N; i++ ) 
                        { 
                            Q[ i, k ] = x[ i, j ]; 
                        } 
                    } 
                } 
                if ( N == no ) 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        eta[ i ] = X_SUMPROD( ip, b, Q, i ) + offst[ i ]; 
                    } 
                } 
                else 
                { 
                    for ( i=1; i <= N; i++ ) 
                    { 
                        if ( wt[ i ] == 0.0 ) 
                        { 
                            eta[ i ] = 0.0; 
                        } 
                        else 
                        { 
                            eta[ i ] = X_SUMPROD( ip, b, Q, i ) + offst[ i ]; 
                        } 
                    } 
                } 
                //  fit response from linear predictor
                switch ( Model ) 
                {
                    case 1:
                        X_BLRFIT( N, eta, fvl, t, wt, ref no, ref iweight, ref msg ); 
                        break;
                    case 2:
                        X_PLRFIT( N, eta, fvl, wt, ref no, ref iweight, ref msg ); 
                        break;
                }
                    
                dev = 0.0; 
                if ( iweight ) 
                { 
                    //  calculate deviance
                    switch ( Model ) 
                    {
                        case 1:
                            for ( i=1; i <= N; i++ ) 
                            { 
                                if ( wt[ i ] > 0.0 ) 
                                { 
                                    dev = dev + wt[ i ] * X_BLRDEV( fvl[ i ], y[ i ], t[ i ] ); 
                                    if ( t[ i ] < 0.0 ) 
                                    { 
                                        ierror = 1; 
                                        return; 
                                    } 
                                } 
                            } 
                            break;
                        case 2:
                            for ( i=1; i <= N; i++ ) 
                            { 
                                if ( wt[ i ] > 0.0 ) 
                                { 
                                    dev = dev + wt[ i ] * X_PLRDEV( fvl[ i ], y[ i ], ref t[ i ] ); 
                                    if ( t[ i ] < 0.0 ) 
                                    { 
                                        ierror = 1; 
                                        return; 
                                    } 
                                } 
                            } 
                            break;
                    }
                        
                } 
                else 
                { 
                    switch ( Model ) 
                    {
                        case 1:
                            for ( i=1; i <= N; i++ ) 
                            { 
                                dev = dev + X_BLRDEV( fvl[ i ], y[ i ], t[ i ] ); 
                                if ( t[ i ] < 0.0 ) 
                                { 
                                    ierror = 1; 
                                    return; 
                                } 
                            } 
                            break;
                        case 2:
                            for ( i=1; i <= N; i++ ) 
                            { 
                                dev = dev + X_PLRDEV( fvl[ i ], y[ i ], ref t[ i ] ); 
                                if ( t[ i ] < 0.0 ) 
                                { 
                                    ierror = 1; 
                                    return; 
                                } 
                            } 
                            break;
                    }
                        
                } 
                if ( dev <= 0 ) 
                { 
                    final = true; 
                    indqy = 0; 
                } 
                else if ( iter == 1 ) 
                { 
                    dev1 = dev; 
                } 
                else 
                { 
                    if ( Math.Abs( dev - dev1 ) < ( 1.0 + dev ) * tol ) 
                    { 
                        final = true; 
                        indqy = 0; 
                    } 
                    else 
                    { 
                        dev1 = dev; 
                    } 
                } 
                if ( iter > maxit ) 
                { 
                    ierror = 3; 
                    final = true; 
                }
            } 
            while ( true ); 
        } 
        
        
        private static void X_BLRTRI( bool mean, int N, int M, double[,] x, int[] isx, int ip, double[,] Q, int irank, double[] wwt, double[] h, double[] WK ) 
        {
            int im = mean ? 1 : 0; 
            WK[ 1 ] = 1.0; 
            for (int i=1; i <= N; i++ ) 
            { 
                int k = im;
                int j;
                for ( j=1; j <= M; j++ ) 
                { 
                    if ( isx[ j ] > 0 ) 
                    { 
                        k = k + 1; 
                        WK[ k ] = x[ i, j ]; 
                    } 
                }
                for (int ix=1; ix <= irank; ix++ ) 
                { 
                    WK[ ip + ix ] = 0.0; 
                } 
                for ( j=1; j <= ip + 1; j++ )
                {
                    double temp = WK[ j ];
                    if ( temp != 0.0 ) 
                    { 
                        for (int ix=1; ix <= irank; ix++ ) 
                        { 
                            WK[ ip + ix ] = WK[ ip + ix ] + temp * Q[ ix, j ]; 
                        } 
                    }
                }
                if ( irank > 0 ) 
                { 
                    h[ i ] = 0.0; 
                    for ( j=ip + 1; j <= irank + ip; j++ ) 
                    { 
                        h[ i ] = h[ i ] + WK[ j ] * WK[ j ]; 
                    } 
                } 
                h[ i ] = h[ i ] * wwt[ i ] * wwt[ i ]; 
            } 
        } 
        
        
        ///  <summary>
        ///  log Poisson deviance
        ///  </summary>
        ///  <param name="fvl"></param>
        ///  <param name="y"></param>
        ///  <param name="t"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static double X_PLRDEV( double fvl, double y, ref double t ) 
        {
            double dev = 0.0; 
            if ( fvl <= 0.0 ) 
            { 
                t = -1.0; 
            } 
            else if ( y > 0.0 ) 
            { 
                dev = y * Math.Log( y / fvl ) - ( y - fvl ); 
            } 
            else 
            { 
                dev = fvl; 
            } 
            if ( dev < 0.0 )
            { 
                dev = 0.0; 
            } 
            return 2.0 * dev; 
        } 
        
        
        ///  <summary>
        ///  logistic binary deviance
        ///  </summary>
        ///  <param name="fvl"></param>
        ///  <param name="y"></param>
        ///  <param name="t"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static double X_BLRDEV( double fvl, double y, double t ) 
        {
            double dev = 0.0; 
            //  Hosmer and Lemeshow p 138
            if ( y == 0.0 ) 
            { 
                dev = t * Math.Abs( Math.Log( 1.0 - fvl / t ) ); 
            } 
            else if ( y == t ) 
            { 
                dev = t * Math.Abs( Math.Log( fvl / t ) ); 
            } 
            else 
            { 
                if ( y > 0.0 & y < t )
                { 
                    dev = ( y * Math.Log( y / fvl ) + ( t - y ) * Math.Log( ( t - y ) / ( t - fvl ) ) ); 
                } 
            } 
            if ( dev < 0.0 )
            { 
                dev = 0.0; 
            } 
            return 2.0 * dev; 
        } 
        
        
        private static void X_BLRSET( int ip, int irank, double[,] Q, double[] cov, double[] WK ) 
        {
            int ij = 1;
            for (int i=1; i <= ip; i++ )
            {
                if ( irank > 0 )
                {
                    for (int iy=1; iy <= irank; iy++ )
                    { 
                        WK[ iy ] = Q[ iy, i ]; 
                    } 
                } 
                for (int k=1; k <= i; k++ ) 
                { 
                    cov[ ij - 1 + k ] = 0.0; 
                } 
                for (int j=1; j <= irank; j++ )
                {
                    double temp = WK[ j ];
                    if ( temp != 0.0 ) 
                    { 
                        for (int k=1; k <= i; k++ ) 
                        { 
                            cov[ ij - 1 + k ] = cov[ ij - 1 + k ] + temp * Q[ j, k ]; 
                        } 
                    }
                }
                ij = ij + i; 
            } 
        } 
        
        
        ///  <summary>
        ///  Poisson derivative of log link function
        ///  </summary>
        private static void x_plrder( int N, double[] eta, double[] der, double[] wt, int no ) 
        { 
            int i; 
            
            if ( N == no ) 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    der[ i ] = Math.Exp( eta[ i ] ); 
                } 
            } 
            else 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] > 0.0 ) 
                    { 
                        der[ i ] = Math.Exp( eta[ i ] ); 
                    } 
                    else 
                    { 
                        der[ i ] = 0.0; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  binomial logistic derivative
        ///  </summary>
        private static void x_blrder( int N, double[] eta, double[] t, double[] der, double[] wt, int no ) 
        { 
            int i; 
            double e; 
            
            if ( N == no ) 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    e = Math.Exp( eta[ i ] ); 
                    der[ i ] = t[ i ] * e / ( ( 1.0 + e ) * ( 1.0 + e ) ); 
                } 
            } 
            else 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] != 0.0 & t[ i ] != 0.0 ) 
                    { 
                        e = Math.Exp( eta[ i ] ); 
                        der[ i ] = t[ i ] * e / ( ( 1.0 + e ) * ( 1.0 + e ) ); 
                    } 
                    else 
                    { 
                        der[ i ] = 0.0; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  Poisson variance
        ///  </summary>
        private static void x_plrvar( int N, double[] fvl, double[] var, double[] wt, int no ) 
        { 
            if ( N != no ) 
            { 
                for (int i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] == 0.0 ) 
                    { 
                        var[ i ] = 0.0; 
                    } 
                    else 
                    { 
                        var[ i ] = 1.0 / Math.Sqrt( fvl[ i ] ); 
                    } 
                } 
            } 
            else 
            { 
                for (int i=1; i <= N; i++ ) 
                { 
                    var[ i ] = 1.0 / Math.Sqrt( fvl[ i ] ); 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  binomial variance
        ///  </summary>
        private static void x_blrvar( int N, double[] fvl, double[] t, double[] var, double[] wt, int no ) 
        { 
            if ( N != no ) 
            { 
                for (int i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] == 0.0 | t[ i ] == 0.0 ) 
                    { 
                        var[ i ] = 0.0; 
                    } 
                    else 
                    { 
                        double D = fvl[ i ] * ( t[ i ] - fvl[ i ] ); 
                        var[ i ] = Math.Sqrt( t[ i ] / D ); 
                    } 
                } 
            } 
            else 
            { 
                for (int i=1; i <= N; i++ ) 
                { 
                    double D = fvl[ i ] * ( t[ i ] - fvl[ i ] ); 
                    var[ i ] = Math.Sqrt( t[ i ] / D ); 
                } 
            } 
        } 
        
        
        private static void X_M_QRMN( int M, int N, double[,] a, int lda, double[] zeta, ref int ifail ) 
        {
            for (int k=1; k <= Math.Min( M - 1, N ); k++ ) 
            { 
                X_L_ERFL( M - k, ref a[ k, k ], a, out zeta[ k ], k + 1, k, lda ); 
                if ( zeta[ k ] > 0.0 & k < N ) 
                { 
                    double temp = a[ k, k ]; 
                    a[ k, k ] = zeta[ k ];
                    for (int i=1; i <= N - k; i++ ) 
                    { 
                        zeta[ k + i ] = 0.0; 
                    } 
                    int iz = k; 
                    int iz2 = k;
                    for (int j=1; j <= M - k + 1; j++ ) 
                    { 
                        double tp = a[ iz, iz2 ]; 
                        iz = iz + 1; 
                        if ( iz > lda ) 
                        { 
                            iz = 1; 
                            iz2 = iz2 + 1; 
                        } 
                        if ( tp != 0.0 ) 
                        { 
                            for (int i=1; i <= N - k; i++ ) 
                            { 
                                zeta[ k + i ] = zeta[ k + i ] + tp * a[ k - 1 + j, k + i ]; 
                            } 
                        } 
                    } 
                    for (int j=1; j <= N - k; j++ ) 
                    { 
                        if ( zeta[ k + j ] != 0.0 ) 
                        { 
                            iz = k; 
                            iz2 = k; 
                            for (int i=1; i <= M - k + 1; i++ ) 
                            { 
                                a[ k - 1 + i, k + j ] = a[ k - 1 + i, k + j ] + a[ iz, iz2 ] * -zeta[ k + j ]; 
                                iz = iz + 1; 
                                if ( iz > lda ) 
                                { 
                                    iz = 1; 
                                    iz2 = iz2 + 1; 
                                } 
                            } 
                        } 
                    } 
                    a[ k, k ] = temp; 
                } 
            } 
            if ( M == N )
            { 
                zeta[ N ] = 0.0; 
            } 
        } 
        
        
        private static void X_M_QRHT( int M, int N, double[,] a, int lda, double[] zeta, double[] b, int ldb ) 
        {
            for (int k=1; k <= N; k++ )
            {
                double zetak = zeta[ k ];
                if ( zetak > 0.0 ) 
                { 
                    double temp = a[ k, k ]; 
                    a[ k, k ] = zetak; 
                    double work1 = 0.0; 
                    int ixa = k; 
                    int ixa2 = k; 
                    for (int j=1; j <= M - k + 1; j++ ) 
                    { 
                        if ( a[ ixa, ixa2 ] != 0.0 )
                        { 
                            work1 = work1 + a[ ixa, ixa2 ] * b[ j + k - 1 ]; 
                        } 
                        ixa = ixa + 1; 
                        if ( ixa > lda ) 
                        { 
                            ixa = 1; 
                            ixa2 = ixa2 + 1; 
                        } 
                    } 
                    if ( work1 != 0.0 ) 
                    { 
                        ixa = k; 
                        for (int i=1; i <= M - k + 1; i++ ) 
                        { 
                            b[ ixa ] = b[ ixa ] + a[ i + k - 1, k ] * -work1; 
                            ixa = ixa + 1; 
                            if ( ixa > ldb ) 
                            { 
                                ixa = 1; 
                            } 
                        } 
                    } 
                    a[ k, k ] = temp; 
                }
            }
        } 
        
        
        private static void X_E_SVDX( int N, double[,] a, int lda, int ncolb, double[] b, int ldb, double[] sv, out int ifail ) 
        { 
            int ierr = 0;

            double[] work = new double[2 * lda + 1 /* for VB to C# conversion */ ]; 
            X_E_SVOT( N, a, lda, sv, work, ncolb, b, ref ierr ); 
            X_E_SVZP( N, a, lda ); 
            int ncolp = N;
            X_E_SVUT( N, sv, work, ncolb, b, ldb, ncolp, a, lda, out ierr ); 
            ifail = ierr != 0 ? ierr : 0; 
        } 
        
        
        private static void X_E_SVOT( int N, double[,] a, int lda, double[] D, double[] e, int ncoly, double[] y, ref int ifail ) 
        {
            for (int k=1; k <= N - 2; k++ ) 
            { 
                int ix = 1 + ( N - k - 2 ) * lda;
                int iax ;
                int izb ;
                int iz2b ;
                for (int i=N - k - 1; i >= 2; i-- ) 
                { 
                    iax = ix - lda; 
                    int iz = iax - ( iax / lda ) * lda + k - 1; 
                    int iz2 = iax / lda + 2 + k; 
                    iax = ix; 
                    izb = iax - ( iax / lda ) * lda + k - 1; 
                    iz2b = iax / lda + 2 + k; 
                    X_L_TANR( ref a[ iz, iz2 ], ref a[ izb, iz2b ], out e[ i + k ], out D[ i + k ] ); 
                    ix = ix - lda; 
                } 
                iax = ix; 
                izb = iax - ( iax / lda ) * lda + k - 1; 
                iz2b = iax / lda + 1 + k + 1; 
                X_L_TANR( ref a[ k, k + 1 ], ref a[ izb, iz2b ], out e[ k + 1 ], out D[ k + 1 ] ); 
                X_L_QRUZ( N - k, 1, N - k, e, D, a, lda, k ); 
                if ( ncoly > 0 ) 
                { 
                    if ( Math.Min( N, k + 1 ) >= 1 & N > k + 1 ) 
                    { 
                        for (int j=N - 1; j >= k + 1; j-- ) 
                        { 
                            if ( e[ j ] != 1.0 | D[ j ] != 0.0 ) 
                            { 
                                double etemp = e[ j ]; 
                                double dtemp = D[ j ]; 
                                double temp = y[ j + 1 ]; 
                                y[ j + 1 ] = etemp * temp - dtemp * y[ j ]; 
                                y[ j ] = dtemp * temp + etemp * y[ j ]; 
                            } 
                        } 
                    } 
                } 
            } 
            for (int k=1; k <= N - 1; k++ ) 
            { 
                D[ k ] = a[ k, k ]; 
                e[ k ] = a[ k, k + 1 ]; 
            } 
            D[ N ] = a[ N, N ]; 
        } 
        
        
        private static void X_E_SVUT( int N, double[] D, double[] e, int ncolb, double[] b, int ldb, int NCOLZ, double[,] z, int LDZ, out int ifail ) 
        {
            double temp ;
            int i ;
            int j ; int L ;

            double[] wrk = new double[N + 1 /* for VB to C# conversion */];
            double[] wrk1 = new double[N + 1 /* for VB to C# conversion */];
            double[] wrk2 = new double[N + 1 /* for VB to C# conversion */];
            double[] wrk3 = new double[N + 1 /* for VB to C# conversion */]; 
            wrk[ 1 ] = 0; 
            bool wantb = ncolb > 0; 
            bool wantz = NCOLZ > 0; 
            double amax = Math.Abs( D[ 1 ] ); 
            for ( i=2; i <= N; i++ ) 
            { 
                amax = Max3( amax, Math.Abs( D[ i ] ), Math.Abs( e[ i - 1 ] ) ); 
            } 
            if ( amax > 0 ) 
            { 
                X_L_VXSC( N, 1.0 / amax, D ); 
                X_L_VXSC( N - 1, 1.0 / amax, e ); 
            } 
            int maxit = 50 * N; 
            int iter = 1; 
            int k = N; 
            while ( k > 1 && iter <= maxit ) 
            {
                bool force ;
                int P ;
                X_E_SPLT( k, D, e, out force, out P ); 
                L = P + 1;
                double ctemp ;
                double stemp ;
                if ( force ) 
                { 
                    if ( P == k ) 
                    { 
                        X_E_XXEL( k, D, e, wantz, wrk2, wrk3 ); 
                        if ( wantz ) 
                        { 
                            if ( Math.Min( N, NCOLZ ) >= 1 & k > 1 & k <= N ) 
                            { 
                                for ( j=k - 1; j >= 1; j-- ) 
                                { 
                                    if ( wrk2[ j ] != 1.0 | wrk3[ j ] != 0.0 ) 
                                    { 
                                        ctemp = wrk2[ j ]; 
                                        stemp = wrk3[ j ]; 
                                        for ( i=1; i <= NCOLZ; i++ ) 
                                        { 
                                            temp = z[ j, i ]; 
                                            z[ j, i ] = stemp * z[ k, i ] + ctemp * temp; 
                                            z[ k, i ] = ctemp * z[ k, i ] - stemp * temp; 
                                        } 
                                    } 
                                } 
                            } 
                        } 
                    } 
                    else 
                    { 
                        if ( P > 0 & P < k ) 
                        { 
                            i = P; 
                            temp = e[ i ]; 
                            e[ i ] = 0.0; 
                            X_L_TANR( ref D[ i + 1 ], ref temp, out ctemp, out stemp ); 
                            if ( wantb ) 
                            { 
                                wrk[ i ] = ctemp; 
                                wrk1[ i ] = -stemp; 
                            } 
                            for ( i=P + 1; i <= k - 1; i++ ) 
                            { 
                                temp = -stemp * e[ i ]; 
                                e[ i ] = ctemp * e[ i ]; 
                                X_L_TANR( ref D[ i + 1 ], ref temp, out ctemp, out stemp ); 
                                if ( wantb ) 
                                { 
                                    wrk[ i ] = ctemp; 
                                    wrk1[ i ] = -stemp; 
                                } 
                            } 
                        } 
                        if ( wantb ) 
                        { 
                            if ( Math.Min( N, P ) >= 1 & k > P & k <= N ) 
                            { 
                                for ( j=P + 1; j <= k; j++ ) 
                                { 
                                    ctemp = wrk[ j - 1 ]; 
                                    stemp = wrk1[ j - 1 ]; 
                                    if ( ctemp != 1.0 | stemp != 0.0 ) 
                                    { 
                                        temp = b[ j ]; 
                                        b[ j ] = ctemp * temp - stemp * b[ P ]; 
                                        b[ P ] = stemp * temp + ctemp * b[ P ]; 
                                    } 
                                } 
                            } 
                        } 
                    } 
                } 
                if ( L >= k ) 
                { 
                    k = k - 1; 
                } 
                else 
                {
                    double ekm2 = k > ( L + 1 ) ? e[ k - 2 ] : 0.0;
                    double cs ;
                    double sn ;
                    X_E_QRSP( D[ L ], e[ L ], D[ k - 1 ], D[ k ], ekm2, e[ k - 1 ], out cs, out sn ); 
                    X_E_QRST( L, k, D, e, cs, sn, wantb, wrk, wrk1, wantz, wrk2, wrk3 ); 
                    if ( wantb ) 
                    { 
                        if ( Math.Min( N, L ) >= 1 && k > L && k <= N ) 
                        { 
                            for ( j=L; j <= k - 1; j++ ) 
                            { 
                                if ( wrk[ j ] != 1.0 | wrk1[ j ] != 0.0 ) 
                                { 
                                    ctemp = wrk[ j ]; 
                                    stemp = wrk1[ j ]; 
                                    temp = b[ j + 1 ]; 
                                    b[ j + 1 ] = ctemp * temp - stemp * b[ j ]; 
                                    b[ j ] = stemp * temp + ctemp * b[ j ]; 
                                } 
                            } 
                        } 
                    } 
                    if ( wantz ) 
                    { 
                        if ( Min3( N, NCOLZ, L ) >= 1 || k > L || k <= N ) 
                        { 
                            for ( j=L; j <= k - 1; j++ ) 
                            { 
                                if ( wrk2[ j ] != 1.0 || wrk3[ j ] != 0.0 ) 
                                { 
                                    ctemp = wrk2[ j ]; 
                                    stemp = wrk3[ j ]; 
                                    for ( i=1; i <= NCOLZ; i++ ) 
                                    { 
                                        temp = z[ j + 1, i ]; 
                                        z[ j + 1, i ] = ctemp * temp - stemp * z[ j, i ]; 
                                        z[ j, i ] = stemp * temp + ctemp * z[ j, i ]; 
                                    } 
                                } 
                            } 
                        } 
                    } 
                    iter = iter + 1; 
                } 
            } 
            if ( amax > 0.0 ) 
            { 
                X_L_VXSC( N, amax, D ); 
                X_L_VXSC( N - 1, amax, e ); 
            } 
            for ( i=k; i <= N; i++ ) 
            { 
                if ( D[ i ] < 0.0 ) 
                { 
                    D[ i ] = -D[ i ]; 
                    if ( wantb ) 
                    { 
                        if ( ncolb > 0 )
                        { 
                            b[ i ] = -b[ i ]; 
                        } 
                    } 
                } 
            } 
            for ( j=1; j <= k - 1; j++ ) 
            { 
                wrk[ j ] = Convert.ToDouble( j ) + 0.25; 
            } 
            for ( j=k; j <= N; j++ ) 
            { 
                double bmax = D[ j ]; 
                L = j; 
                for ( i=j + 1; i <= N; i++ ) 
                { 
                    if ( D[ i ] > bmax ) 
                    { 
                        bmax = D[ i ]; 
                        L = i; 
                    } 
                } 
                wrk[ j ] = Convert.ToDouble( L ) + 0.25; 
                if ( L > j ) 
                { 
                    temp = D[ j ]; 
                    D[ j ] = D[ L ]; 
                    D[ L ] = temp; 
                } 
            } 
            if ( wantb ) 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    L = Convert.ToInt32( wrk[ i ] ); 
                    if ( L != i ) 
                    { 
                        temp = b[ i ]; 
                        b[ i ] = b[ L ]; 
                        b[ L ] = temp; 
                    } 
                } 
            } 
            if ( wantz ) 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    L = Convert.ToInt32( wrk[ i ] ); 
                    if ( L != i ) 
                    { 
                        for ( j=1; j <= NCOLZ; j++ ) 
                        { 
                            temp = z[ i, j ]; 
                            z[ i, j ] = z[ L, j ]; 
                            z[ L, j ] = temp; 
                        } 
                    } 
                } 
            } 
            wrk[ 1 ] = iter; 
            ifail = k == 1 ? 0 : k; 
        } 
        
        
        private static void X_E_SVZP( int N, double[,] a, int lda ) 
        {
            double[] work = new double[2 * lda + 1 /* for VB to C# conversion */]; 
            if ( N > 1 ) 
            { 
                a[ N, N ] = 1.0; 
                a[ N - 1, N ] = 0.0; 
                a[ N, N - 1 ] = 0.0; 
                if ( N > 2 )
                {
                    for (int k=N - 2; k >= 1; k-- ) 
                    { 
                        a[ k + 1, k + 1 ] = 1.0; 
                        a[ k, k + 1 ] = 0.0;
                        for (int j=k + 2; j <= N; j++ ) 
                        { 
                            X_L_CSTN( -a[ k, j ], out work[ j - 1 ], out work[ N + j - 2 ] ); 
                            a[ k, j ] = 0.0; 
                        } 
                        int iz = k + 1; 
                        int iz2 = k;
                        for (int ix=1; ix <= N - k; ix++ ) 
                        { 
                            a[ iz, iz2 ] = 0.0; 
                            iz = iz + 1; 
                            if ( iz > lda ) 
                            { 
                                iz = 1; 
                                iz2 = iz2 + 1; 
                            } 
                        } 
                        for (int j=1; j <= N - k - 1; j++ ) 
                        { 
                            if ( work[ k + j ] != 1.0 | work[ N + k - 1 + j ] != 0.0 ) 
                            { 
                                double ctemp = work[ k + j ]; 
                                double stemp = work[ N + k - 1 + j ]; 
                                iz = k; 
                                iz2 = k;
                                for (int i=1; i <= N - k; i++ ) 
                                { 
                                    double temp = a[ iz + i, iz2 + j + 1 ]; 
                                    a[ iz + i, iz2 + j + 1 ] = ctemp * temp - stemp * a[ iz + i, iz2 + j ]; 
                                    a[ iz + i, iz2 + j ] = stemp * temp + ctemp * a[ iz + i, iz2 + j ]; 
                                } 
                            } 
                        } 
                    }
                }
            } 
            a[ 1, 1 ] = 1.0; 
        } 
        
        
        private static double X_SUMPROD( int ip, double[] b, double[,] Q, int N ) 
        { 
            double sum = 0.0; 
            for ( int j=1; j <= ip; j++ ) 
            { 
                sum += b[ j ] * Q[ N, j ]; 
            } 
            return sum; 
        } 
        
        
        ///  <summary>
        ///  log Poisson response fitted from linear predictors
        ///  </summary>
        ///  <remarks>IEB July 2009: updated to auto-drop observations at the boundary (complete prediction of outcome)</remarks>
        private static void X_PLRFIT( int N, double[] eta, double[] fvl, double[] wt, ref int no, ref bool iweight, ref string msg ) 
        { 
            int i;

            double b = -Math.Log( Constant.EPSNEG ); 
            if ( N == no ) 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    if ( Math.Abs( eta[ i ] ) > b ) 
                    { 
                        BinBound( ref msg, i, out iweight, ref no, wt ); 
                    } 
                    else 
                    { 
                        fvl[ i ] = Math.Exp( eta[ i ] ); 
                    } 
                } 
            } 
            else 
            { 
                for ( i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] != 0.0 ) 
                    { 
                        if ( Math.Abs( eta[ i ] ) > b ) 
                        { 
                            BinBound( ref msg, i, out iweight, ref no, wt ); 
                        } 
                        else 
                        { 
                            fvl[ i ] = Math.Exp( eta[ i ] ); 
                        } 
                    } 
                    else 
                    { 
                        fvl[ i ] = 0.0; 
                    } 
                } 
            } 
        } 
        
        
        ///  <summary>
        ///  logistic binomial response fitted from linear predictors
        ///  </summary>
        ///  <remarks>IEB July 2009: updated to auto-drop observations at the boundary (complete prediction of outcome)</remarks>
        private static void X_BLRFIT( int N, double[] eta, double[] fvl, double[] t, double[] wt, ref int no, ref bool iweight, ref string msg ) 
        { 
            double b = -Math.Log( Constant.EPSNEG ); 
            if ( N == no ) 
            { 
                for (int i=1; i <= N; i++ ) 
                { 
                    if ( Math.Abs( eta[ i ] ) >= b ) 
                    { 
                        BinBound( ref msg, i, out iweight, ref no, wt ); 
                    } 
                    else 
                    { 
                        double e = Math.Exp( eta[ i ] ); 
                        fvl[ i ] = t[ i ] * e / ( 1.0 + e ); 
                    } 
                } 
            } 
            else 
            { 
                b = -Math.Log( Constant.EPSNEG ); 
                for (int i=1; i <= N; i++ ) 
                { 
                    if ( wt[ i ] != 0.0 & t[ i ] != 0.0 ) 
                    { 
                        if ( Math.Abs( eta[ i ] ) >= b ) 
                        { 
                            BinBound( ref msg, i, out iweight, ref no, wt ); 
                        } 
                        else 
                        { 
                            double e = Math.Exp( eta[ i ] ); 
                            fvl[ i ] = t[ i ] * e / ( 1.0 + e ); 
                        } 
                    } 
                    else 
                    { 
                        fvl[ i ] = 0.0; 
                        eta[ i ] = 0.0; 
                    } 
                } 
            } 
        } 
        
        
        ///  <remarks>IEB July 2009: updated to auto-drop observations at the boundary (complete prediction of outcome)</remarks>
        private static void BinBound( ref string msg, int i, out bool iweight, ref int no, double[] wt ) 
        { 
            
            iweight = true; 
            if ( wt[ i ] != 0.0 ) 
            { 
                wt[ i ] = 0.0; 
                no = no - 1; 
                if ( msg.Length == 0 ) 
                { 
                    msg = "The following observations were dropped due to complete determination of the outcome: " +  i.ToString(); 
                } 
                else 
                { 
                    msg = msg + ", " +  i.ToString(); 
                } 
            } 
        } 
        
        
        private static void X_L_ERFL( int N, ref double alpha, double[,] a, out double zeta, int iz, int iz2, int lda ) 
        {
            if ( N < 1 ) 
            { 
                zeta = 0.0; 
            } 
            else if ( N == 1 & a[ iz, iz2 ] == 0.0 ) 
            { 
                zeta = 0.0; 
            } 
            else
            {
                const double eps = Constant.EPSNEG;
                double BETA ;
                if ( N == 1 ) 
                { 
                    if ( alpha == 0.0 ) 
                    { 
                        zeta = 1.0; 
                        alpha = Math.Abs( a[ iz, iz2 ] ); 
                        a[ iz, iz2 ] = -dsign( 1, a[ iz, iz2 ] ); 
                    } 
                    else if ( Math.Abs( a[ iz, iz2 ] ) <= eps * Math.Abs( alpha ) ) 
                    { 
                        zeta = 0.0; 
                    } 
                    else 
                    { 
                        if ( Math.Abs( alpha ) >= Math.Abs( a[ iz, iz2 ] ) ) 
                        { 
                            BETA = Math.Abs( alpha ) * Math.Sqrt( 1.0 + Math.Pow( ( a[ iz, iz2 ] / alpha ), 2.0 ) ); 
                        } 
                        else 
                        { 
                            BETA = Math.Abs( a[ iz, iz2 ] ) * Math.Sqrt( 1.0 + Math.Pow( ( alpha / a[ iz, iz2 ] ), 2.0 ) ); 
                        } 
                        zeta = Math.Sqrt( ( Math.Abs( alpha ) + BETA ) / BETA ); 
                        if ( alpha >= 0.0 )
                        { 
                            BETA = -BETA; 
                        } 
                        a[ iz, iz2 ] = -a[ iz, iz2 ] / ( zeta * BETA ); 
                        alpha = BETA; 
                    } 
                } 
                else 
                { 
                    double ssq = 1.0; 
                    double sca = 0.0; 
                    int iiz = iz; 
                    int iiz2 = iz2;
                    for (int ix=1; ix <= N; ix++ ) 
                    { 
                        if ( a[ iiz, iiz2 ] != 0.0 )
                        {
                            double absxi = Math.Abs( a[ iiz, iiz2 ] );
                            if ( sca < absxi ) 
                            { 
                                ssq = 1 + ssq * Math.Pow( ( sca / absxi ), 2.0 ); 
                                sca = absxi; 
                            } 
                            else 
                            { 
                                ssq = ssq + Math.Pow( ( absxi / sca ), 2.0 ); 
                            }
                        }
                        iiz = iiz + 1; 
                        if ( iiz > lda ) 
                        { 
                            iiz = 1; 
                            iiz2 = iiz2 + 1; 
                        } 
                    } 
                    if ( sca == 0.0 | sca <= eps * Math.Abs( alpha ) ) 
                    { 
                        zeta = 0.0; 
                    } 
                    else if ( alpha == 0.0 ) 
                    { 
                        zeta = 1.0; 
                        alpha = sca * Math.Sqrt( ssq ); 
                        iiz = iz; 
                        iiz2 = iz2; 
                        for (int ix=1; ix <= N; ix++ ) 
                        { 
                            a[ iiz, iiz2 ] = -1.0 / alpha * a[ iiz, iiz2 ]; 
                            iiz = iiz + 1; 
                            if ( iiz > lda ) 
                            { 
                                iiz = 1; 
                                iiz2 = iiz2 + 1; 
                            } 
                        } 
                    } 
                    else 
                    { 
                        if ( sca < Math.Abs( alpha ) ) 
                        { 
                            BETA = Math.Abs( alpha ) * Math.Sqrt( 1.0 + ssq * Math.Pow( ( sca / alpha ), 2.0 ) ); 
                        } 
                        else 
                        { 
                            BETA = sca * Math.Sqrt( ssq + Math.Pow( ( alpha / sca ), 2.0 ) ); 
                        } 
                        zeta = Math.Sqrt( ( BETA + Math.Abs( alpha ) ) / BETA ); 
                        if ( alpha > 0.0 )
                        { 
                            BETA = -BETA; 
                        } 
                        iiz = iz; 
                        iiz2 = iz2; 
                        for (int ix=1; ix <= N; ix++ ) 
                        { 
                            a[ iiz, iiz2 ] = -1.0 / ( zeta * BETA ) * a[ iiz, iiz2 ]; 
                            iiz = iiz + 1; 
                            if ( iiz > lda ) 
                            { 
                                iiz = 1; 
                                iiz2 = iiz2 + 1; 
                            } 
                        } 
                        alpha = BETA; 
                    } 
                }
            }
        } 
        
        
        private static void X_L_QRUZ( int N, int K1, int K2, double[] C, double[] s, double[,] a, int lda, int ist ) 
        { 
            for (int j=K2 - 1; j >= K1; j-- ) 
            { 
                if ( ( C[ j + ist ] != 1 ) | ( s[ j + ist ] != 0 ) ) 
                { 
                    double ctemp = C[ j + ist ]; 
                    double stemp = s[ j + ist ]; 
                    for (int i=1; i <= j; i++ ) 
                    { 
                        double temp = a[ i + ist, j + 1 + ist ]; 
                        a[ i + ist, j + 1 + ist ] = ctemp * temp - stemp * a[ i + ist, j + ist ]; 
                        a[ i + ist, j + ist ] = stemp * temp + ctemp * a[ i + ist, j + ist ]; 
                    } 
                    double fill = s[ j + ist ] * a[ j + 1 + ist, j + 1 + ist ]; 
                    a[ j + 1 + ist, j + 1 + ist ] = C[ j + ist ] * a[ j + 1 + ist, j + 1 + ist ]; 
                    X_L_TANR( ref a[ j + ist, j + ist ], ref fill, out C[ j + ist ], out s[ j + ist ] ); 
                } 
            } 
            for (int j=N; j >= K1 + 1; j-- ) 
            { 
                int I1 = Math.Min( K2, j ); 
                double aij = a[ I1 + ist, j + ist ]; 
                for (int i=I1 - 1; i >= K1; i-- ) 
                { 
                    double temp = a[ i + ist, j + ist ]; 
                    a[ i + 1 + ist, j + ist ] = C[ i + ist ] * aij - s[ i + ist ] * temp; 
                    aij = s[ i + ist ] * aij + C[ i + ist ] * temp; 
                } 
                a[ K1 + ist, j + ist ] = aij; 
            } 
        } 
        
        
        private static void X_L_TANR( ref double a, ref double b, out double C, out double s ) 
        {
            if ( b == 0.0 ) 
            { 
                C = 1.0; 
                s = 0.0; 
            } 
            else 
            {
                bool fail;
                double t = X_DL_QUOT( b, a, out fail ); 
                X_L_CSTN( t, out C, out s ); 
                a = C * a + s * b; 
                b = t; 
            } 
        } 
        
        
        private static void X_L_VXSC( int N, double alpha, double[] x ) 
        {
            if ( N > 0 )
            {
                if ( alpha == 0.0 ) 
                { 
                    for (int ix=1; ix <= N; ix++ ) 
                    { 
                        x[ ix ] = 0.0; 
                    } 
                } 
                else if ( alpha == -1.0 ) 
                { 
                    for (int ix=1; ix <= N; ix++ ) 
                    { 
                        x[ ix ] = -x[ ix ]; 
                    } 
                } 
                else if ( alpha != 1.0 ) 
                { 
                    for (int ix=1; ix <= N; ix++ ) 
                    { 
                        x[ ix ] = alpha * x[ ix ]; 
                    } 
                }
            }
        } 
        
        
        public static void X_E_SPLT( int N, double[] D, double[] e, out bool force, out int P ) 
        {
            const double eps = Constant.EPSNEG; 
            const double flmin = Constant.SPREAL; 
            const double small = flmin / eps; 
            force = false; 
            int i = N; 
            if ( N == 1 ) 
            { 
                if ( Math.Abs( D[ N ] ) < small ) 
                { 
                    force = true; 
                    P = i; 
                    return; 
                } 
            } 
            else 
            { 
                double absd = Math.Abs( D[ N ] ); 
                double abse = Math.Abs( e[ N - 1 ] ); 
                double amax = Math.Max( absd, abse ); 
                if ( ( absd <= eps * amax ) | ( amax < small ) ) 
                { 
                    force = true; 
                    P = i; 
                    return; 
                }
                double bmax ;
                double absdi ;
                for ( i=N - 1; i >= 2; i-- ) 
                { 
                    absdi = Math.Abs( D[ i ] ); 
                    bmax = Math.Max( absdi, absd ); 
                    amax = Math.Max( bmax, abse ); 
                    if ( ( abse <= eps * bmax ) | ( amax < small ) ) 
                    { 
                        P = i; 
                        return; 
                    } 
                    double absei = Math.Abs( e[ i - 1 ] ); 
                    double emax = Math.Max( abse, absei ); 
                    amax = Math.Max( emax, absdi ); 
                    if ( ( absdi <= eps * emax ) | ( amax < small ) ) 
                    { 
                        force = true; 
                        P = i; 
                        return; 
                    } 
                    absd = absdi; 
                    abse = absei; 
                } 
                absdi = Math.Abs( D[ 1 ] ); 
                bmax = Math.Max( absdi, absd ); 
                amax = Math.Max( bmax, abse ); 
                if ( ( abse <= eps * bmax ) | ( amax < small ) ) 
                { 
                    P = i; 
                    return; 
                } 
                amax = Math.Max( abse, absdi ); 
                if ( ( absdi <= eps * abse ) | ( amax < small ) ) 
                { 
                    force = true; 
                    P = i; 
                    return; 
                } 
            } 
            i = 0; 
            P = i; 
        } 
        
        
        private static void X_E_XXEL( int N, double[] D, double[] e, bool WANTCS, double[] C, double[] s ) 
        {
            if ( N > 1 ) 
            { 
                int i = N - 1; 
                double temp = e[ i ]; 
                e[ i ] = 0;
                double cs;
                double sn;
                X_L_TANR( ref D[ i ], ref temp, out cs, out sn ); 
                if ( WANTCS ) 
                { 
                    C[ i ] = cs; 
                    s[ i ] = sn; 
                } 
                for ( i=N - 2; i >= 1; i-- ) 
                { 
                    temp = -sn * e[ i ]; 
                    e[ i ] = cs * e[ i ]; 
                    X_L_TANR( ref D[ i ], ref temp, out cs, out sn ); 
                    if ( WANTCS ) 
                    { 
                        C[ i ] = cs; 
                        s[ i ] = sn; 
                    } 
                } 
            } 
        } 
        
        
        private static void X_E_QRSP( double D1, double E1, double DNM1, double dn, double ENM2, double ENM1, out double C, out double s ) 
        {
            double a; double b; double corr;

            double top = Math.Pow( ( dn * ENM1 ), 2.0 ); 
            if ( top == 0.0 ) 
            { 
                corr = 0.0; 
            } 
            else 
            { 
                double f = ( ( DNM1 - dn ) * ( DNM1 + dn ) + Math.Pow( ENM1, 2.0 ) ) / 2.0; 
                double bot = f + dsign( 1, f ) * Math.Sqrt( top + Math.Pow( f, 2.0 ) );
                bool fail;
                corr = X_DL_QUOT( top, bot, out fail ); 
            } 
            if ( D1 != 0.0 ) 
            { 
                a = ( 1.0 - dn / D1 ) * ( D1 + dn ) + corr / D1; 
                b = E1; 
            } 
            else 
            { 
                a = 1.0; 
                b = 0.0; 
            } 
            X_L_TANR( ref a, ref b, out C, out s ); 
        } 
        
        
        private static void X_E_QRST( int M, int N, double[] D, double[] e, double C, double s, bool WANTLT, double[] cl, double[] sl, bool WANTRT, double[] cr, double[] sr ) 
        {
            double cs; double sn; 
            int i; 
            
            if ( WANTRT ) 
            { 
                cr[ M ] = C; 
                sr[ M ] = s; 
            } 
            double temp = C * D[ M ] + s * e[ M ]; 
            e[ M ] = C * e[ M ] - s * D[ M ]; 
            D[ M ] = temp; 
            temp = s * D[ M + 1 ]; 
            D[ M + 1 ] = C * D[ M + 1 ]; 
            for ( i=M; i <= N - 2; i++ ) 
            { 
                X_L_TANR( ref D[ i ], ref temp, out cs, out sn ); 
                if ( WANTLT ) 
                { 
                    cl[ i ] = cs; 
                    sl[ i ] = sn; 
                } 
                temp = cs * e[ i ] + sn * D[ i + 1 ]; 
                D[ i + 1 ] = cs * D[ i + 1 ] - sn * e[ i ]; 
                e[ i ] = temp; 
                temp = sn * e[ i + 1 ]; 
                e[ i + 1 ] = cs * e[ i + 1 ]; 
                X_L_TANR( ref e[ i ], ref temp, out cs, out sn ); 
                if ( WANTRT ) 
                { 
                    cr[ i + 1 ] = cs; 
                    sr[ i + 1 ] = sn; 
                } 
                temp = cs * D[ i + 1 ] + sn * e[ i + 1 ]; 
                e[ i + 1 ] = cs * e[ i + 1 ] - sn * D[ i + 1 ]; 
                D[ i + 1 ] = temp; 
                temp = sn * D[ i + 2 ]; 
                D[ i + 2 ] = cs * D[ i + 2 ]; 
            } 
            X_L_TANR( ref D[ N - 1 ], ref temp, out cs, out sn ); 
            if ( WANTLT ) 
            { 
                cl[ N - 1 ] = cs; 
                sl[ N - 1 ] = sn; 
            } 
            temp = cs * e[ N - 1 ] + sn * D[ N ]; 
            D[ N ] = cs * D[ N ] - sn * e[ N - 1 ]; 
            e[ N - 1 ] = temp; 
        } 
        
        
        private static void X_L_CSTN( double t, out double C, out double s ) 
        {
            const double eps = Constant.EPSNEG;
            double rteps = Math.Sqrt( eps ); 
            double rrteps = 1.0 / rteps; 
            double abst = Math.Abs( t ); 
            if ( abst < rteps ) 
            { 
                C = 1.0; 
                s = t; 
            } 
            else if ( abst > rrteps ) 
            { 
                C = 1.0 / abst; 
                s = dsign( 1, t ); 
            } 
            else 
            { 
                C = 1.0 / Math.Sqrt( 1.0 + abst * abst ); 
                s = C * t; 
            } 
        } 
        
        
        private static double X_DL_QUOT( double a, double b, out bool fail ) 
        {
            double div;

            if ( a == 0.0 )
            {
                div = 0.0;
                fail = b == 0.0;
            }
            else 
            { 
                const double flmin = Constant.SPREAL; 
                const double flmax = 1.0 / flmin; 
                if ( b == 0.0 ) 
                { 
                    div = dsign( flmax, a ); 
                    fail = true; 
                } 
                else
                {
                    double absb = Math.Abs( b );
                    if ( absb >= 1.0 ) 
                    { 
                        fail = false; 
                        if ( Math.Abs( a ) >= absb * flmin ) 
                        { 
                            div = a / b; 
                        } 
                        else 
                        { 
                            div = 0.0; 
                        } 
                    } 
                    else 
                    { 
                        if ( Math.Abs( a ) <= absb * flmax ) 
                        { 
                            fail = false; 
                            div = a / b; 
                        } 
                        else 
                        { 
                            fail = true; 
                            div = flmax; 
                            if ( ( a < 0.0 && b > 0.0 ) || ( a > 0.0 && b < 0.0 ) )
                            { 
                                div = -div; 
                            } 
                        } 
                    }
                }
            } 
            return div; 
        } 
        
        
        private static int isrank( int N, double[] x ) 
        {
            int k = 0; 
            if ( N >= 1 ) 
            { 
                int ix = 1; 
                const double tl = Constant.EPSNEG; 
                double xMax = Math.Abs( x[ ix ] ); 
                while ( k < N ) 
                { 
                    if ( Math.Abs( x[ ix ] ) <= tl * xMax )
                    { 
                        break;
                    } 
                    if ( Math.Abs( x[ ix ] ) > xMax )
                    { 
                        xMax = Math.Abs( x[ ix ] ); 
                    } 
                    k = k + 1; 
                    ix = ix + 1; 
                } 
            } 
            return k; 

        } 
        
        
        private static double Max3( double a, double b, double c ) 
        {
            double x = a > b ? a : b; 
            return c > x ? c : x; 
        } 
        
        
        private static int Min3( int ia, int ib, int ic ) 
        {
            int ix = ia < ib ? ia : ib; 
            if ( ic < ix )
                ix = ic; 
            return ix; 
        } 
    } 
} 

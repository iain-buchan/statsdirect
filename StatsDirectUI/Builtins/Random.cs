using StatsDirect.Data; 
using StatsDirect.Numerics; 
using StatsDirect.Templates; 
using StatsDirect.Utilities; 

using System;
namespace StatsDirect.Builtins
{
    public class Random  
    { 
        private const string BADPARA = "The parameters are not acceptable."; 
        
        public static DataFrame rndPoisson( ITemplateHost Host, int rows, int cols, double XM, int Seed ) 
        { 
            PoissonRNG RNG = new PoissonRNG(); 
            RNG.Seed( Seed, null ); 
            string ti = "Poisson (seed " +  Seed.ToString() + ", mean " +  XM.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenPoisson( XM ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndUni( ITemplateHost Host, int rows, int cols, double a, double b, bool isCount, int Seed ) 
        { 
            UniformXRNG RNG = new UniformXRNG(); 
            RNG.Seed( Seed, null ); 
            DataFrame outputFrame = new DataFrame(); 
            if ( a != Constant.MISSING & b != Constant.MISSING ) 
            { 
                string ti = "Uniform " + Math.Min( a, b ).ToString() + " to " + Math.Max( a, b ).ToString() + " (seed " +  Seed.ToString() + ")"; 
                for ( int C=0; C <= cols - 1; C++ ) 
                { 
                    DoubleVariable v = new DoubleVariable( rows, ti ); 
                    outputFrame.Variables.Add( v ); 
                    for ( int N=0; N <= rows - 1; N++ ) 
                    { 
                        v.set_Data( N, RNG.GenUniAB( a, b, isCount ) ); 
                    } 
                } 
            } 
            else 
            { 
                string ti = "Uniform 0 to 1 (seed " +  Seed.ToString() + ")"; 
                for ( int C=0; C <= cols - 1; C++ ) 
                { 
                    DoubleVariable v = new DoubleVariable( rows, ti ); 
                    outputFrame.Variables.Add( v ); 
                    for ( int N=0; N <= rows - 1; N++ ) 
                    { 
                        v.Data[N] = RNG.GenUni(); 
                    } 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndBino( ITemplateHost Host, int rows, int cols, int nn, double PP, int Seed ) 
        { 
            BinomialRND RNG = new BinomialRND(); 
            RNG.Seed( Seed ); 
            double nx = Convert.ToDouble( nn ); 
            string ti = "Binomial (seed " +  Seed.ToString() + ", n = " +  nn.ToString() + ", p = " +  PP.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenBinom( nx, PP ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndExpo( ITemplateHost Host, int rows, int cols, double M, int Seed ) 
        { 
            ExponentialRNG RNG = new ExponentialRNG();

            const string mx = "Exponential deviates"; 
            if ( M <= 0.0 | rows <= 0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            RNG.Seed( Seed ); 
            string ti = "Exponential (seed " +  Seed.ToString() + ", rate = " +  M.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.Data[N] = RNG.GenExp() / M; 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndF( ITemplateHost Host, int rows, int cols, double dfn, double dfd, int Seed ) 
        {
            const string mx = "F deviates"; 
            if ( dfn <= 0.0 | dfd <= 0.0 | rows <= 0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            GammaRNG RNG = new GammaRNG(); 
            RNG.Seed( Seed ); 
            string ti = "F (seed " +  Seed.ToString() + ", dfn = " +  dfn.ToString() + ", dfd = " +  dfd.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenF( dfn, dfd ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndGeom( ITemplateHost Host, int rows, int cols, double a, int Seed ) 
        { 
            const string mx = "Geometric deviates"; 
            if ( rows <= 0 | a <= 0.0 | a > 1.0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            PoissonRNG RNG = new PoissonRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Geometric (seed " +  Seed.ToString() + ", P = " +  a.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenGeom( a ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndNegBin( ITemplateHost Host, int rows, int cols, double a, double b, int Seed ) 
        {
            const string mx = "Negative binomial deviates"; 
            if ( rows <= 0 | b <= 0.0 | b > 1.0 | a <= 0.0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            PoissonRNG RNG = new PoissonRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Negative binomial (seed " +  Seed.ToString() + ", size = " +  a.ToString() + ", P = " +  b.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenNegbin( a, b ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndBeta( ITemplateHost Host, int rows, int cols, double a, double b, int Seed ) 
        {
            const string mx = "beta deviates"; 
            if ( rows <= 0 | b <= 0.0 | a <= 0.0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            BetaRNG RNG = new BetaRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Beta (seed " +  Seed.ToString() + ", a = " +  a.ToString() + ", b = " +  b.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenBeta( a, b ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndCauchy( ITemplateHost Host, int rows, int cols, double a, double b, int Seed ) 
        {
            const string mx = "Cauchy deviates"; 
            if ( rows <= 0 | b < 0.0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            UniformXRNG RNG = new UniformXRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Cauchy (seed " +  Seed.ToString() + ", loc = " +  a.ToString() + ", scl = " +  b.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenCauchy( a, b ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndWeibull( ITemplateHost Host, int rows, int cols, double a, double b, int Seed ) 
        {
            const string mx = "Weibull deviates"; 
            if ( rows <= 0 | a <= 0.0 | b <= 0.0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            UniformXRNG RNG = new UniformXRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Weibull (seed " +  Seed.ToString() + ", shp = " +  a.ToString() + ", scl = " +  b.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenWeibull( a, b ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndLogit( ITemplateHost Host, int rows, int cols, double a, double b, int Seed ) 
        {
            const string mx = "Logistic deviates"; 
            if ( rows <= 0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            UniformXRNG RNG = new UniformXRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Logistic (seed " +  Seed.ToString() + ", loc = " +  a.ToString() + ", scl = " +  b.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenLogistic( a, b ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndT( ITemplateHost Host, int rows, int cols, double df, int Seed ) 
        {
            const string mx = "Student t deviates"; 
            if ( df <= 0.0 | rows <= 0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            GammaRNG RNG = new GammaRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Student t (seed " +  Seed.ToString() + ", df = " +  df.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenT( df ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndChi( ITemplateHost Host, int rows, int cols, double df, int Seed ) 
        {
            const string mx = "Chi-square deviates"; 
            if ( df <= 0.0 | rows <= 0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            GammaRNG RNG = new GammaRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Chi-square (seed " +  Seed.ToString() + ", df = " +  df.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    double e = RNG.GenChiSq( df ); 
                    if ( e == Constant.MISSING ) 
                    { 
                        Host.Error( BADPARA, mx ); 
                        return null; 
                    } 
                    v.set_Data( N, e ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndGamma( ITemplateHost Host, int rows, int cols, double a, double b, int Seed ) 
        {
            const string mx = "Gamma deviates"; 
            if ( a <= 0.0 | rows <= 0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            GammaRNG RNG = new GammaRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Gamma (seed " +  Seed.ToString() + ", A = " +  a.ToString() + ", B = " +  b.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenGamma( a, b ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndLogNorm( ITemplateHost Host, int rows, int cols, double XM, double sd, int Seed ) 
        { 
            const string mx = "Lognormal deviates"; 
            if ( sd < 0 ) 
            { 
                Host.Error( BADPARA, mx ); 
                return null; 
            } 
            
            NormalRNG RNG = new NormalRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Lognormal (seed " +  Seed.ToString() + ", log mean = " +  XM.ToString() + ", log sd = " +  sd.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                // get mean and var of lognormal
                // a = Exp(xm + sd * sd / 2#)
                // b = Exp(2# * xm + 2# * sd * sd) - Exp(2# * xm + sd * sd)
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, Formatting.SafeExp( RNG.GenNorm( XM, sd ) ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        public static DataFrame rndNorm( ITemplateHost Host, int rows, int cols, double XM, double sd, int Seed ) 
        { 
            NormalRNG RNG = new NormalRNG(); 
            RNG.Seed( Seed ); 
            string ti = "Normal (seed " +  Seed.ToString() + ", mean = " +  XM.ToString() + ", sd = " +  sd.ToString() + ")"; 
            DataFrame outputFrame = new DataFrame(); 
            for ( int C=0; C <= cols - 1; C++ ) 
            { 
                DoubleVariable v = new DoubleVariable( rows, ti ); 
                outputFrame.Variables.Add( v ); 
                for ( int N=0; N <= rows - 1; N++ ) 
                { 
                    v.set_Data( N, RNG.GenNorm( XM, sd ) ); 
                } 
            } 
            return outputFrame; 
        } 
        
        
        
        
        
    } 
    
    
} 

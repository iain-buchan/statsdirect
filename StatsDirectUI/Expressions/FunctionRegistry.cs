using System.Collections.Generic;

namespace StatsDirect.Expressions
{
    /// <remarks>Singleton.</remarks>
    public class FunctionRegistry
    {
        private readonly Dictionary<string, FunctionDefinition> functionDefinitions;

        private static FunctionRegistry soleInstance;

        public static FunctionRegistry SoleInstance
        {
            get { return soleInstance ?? (soleInstance = new FunctionRegistry()); }
        }

        private FunctionRegistry()
        {
            // A few useful parameters that can be re-used
            ArgumentDefinition df = new ArgumentDefinition("df");
            ArgumentDefinition n = new ArgumentDefinition("n");
            ArgumentDefinition p = new ArgumentDefinition("p");
            ArgumentDefinition q = new ArgumentDefinition("q");
            ArgumentDefinition r = new ArgumentDefinition("r");
            ArgumentDefinition x = new ArgumentDefinition("x");
            ArgumentDefinition logP = new ArgumentDefinition("log.p", true, "false");
            ArgumentDefinition lowerTail = new ArgumentDefinition("lower.tail", true, "true");

            functionDefinitions = new Dictionary<string, FunctionDefinition>();
            AddAll(new[]
                       {
                           new FunctionDefinition("ABS", "Math.Abs", new[] { x }),
                           new FunctionDefinition("ALOGIT", "SDMath.Alogit", new[] { x }),

                           new FunctionDefinition("ACOS", "Math.Acos", new[] { x }),
                           new FunctionDefinition("ACOSINE", "Math.Acos", new[] { x }),
                           new FunctionDefinition("ACOSH", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ACOSINEH", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOS", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOSINE", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ACOT", "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ACOTAN", "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ACOTANGENT", "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ACOTH", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ACOTANGENTH", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOT", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOTANGENT", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ACSC", "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ACOSEC", "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ACOSECANT", "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ACSCH", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ACOSECH", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ACOSECANTH", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACSC", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOSEC", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOSECANT", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ASEC", "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ASECANT", "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ASECH", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ASECANTH", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASEC", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASECANT", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ASIN", "Math.Asin", new[] { x }),
                           new FunctionDefinition("ASINE", "Math.Asin", new[] { x }),
                           new FunctionDefinition("ASINH", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ASINEH", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASIN", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASINE", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ATAN", "Math.Atan", new[] { x }),
                           new FunctionDefinition("ATANGENT", "Math.Atan", new[] { x }),
                           new FunctionDefinition("ATANH", "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("ATANGENTH", "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICATAN", "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICATANGENT", "SDMath.Atanh", new[] { x }),

                           new FunctionDefinition("ARCCOS", "Math.Acos", new[] { x }),
                           new FunctionDefinition("ARCCOSINE", "Math.Acos", new[] { x }),
                           new FunctionDefinition("ARCCOSH", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ARCCOSINEH", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOS", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOSINE", "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ARCCOT", "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ARCCOTAN", "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ARCCOTANGENT", "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ARCCOTH", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ARCCOTANGENTH", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOT", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOTANGENT", "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ARCCSC", "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ARCCOSEC", "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ARCCOSECANT", "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ARCCSCH", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ARCCOSECH", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ARCCOSECANTH", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCSC", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOSEC", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOSECANT", "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ARCSEC", "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ARCSECANT", "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ARCSECH", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ARCSECANTH", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSEC", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSECANT", "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ARCSIN", "Math.Asin", new[] { x }),
                           new FunctionDefinition("ARCSINE", "Math.Asin", new[] { x }),
                           new FunctionDefinition("ARCSINH", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ARCSINEH", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSIN", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSINE", "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ARCTAN", "Math.Atan", new[] { x }),
                           new FunctionDefinition("ARCTANGENT", "Math.Atan", new[] { x }),
                           new FunctionDefinition("ARCTANH", "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("ARCTANGENTH", "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCTAN", "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCTANGENT", "SDMath.Atanh", new[] { x }),

                           new FunctionDefinition("BINOMIAL", "SDMath.Binomial", new[] { n, r, p }),
                           new FunctionDefinition("BINOMIALP", "SDMath.Binomialp", new[] { n, r, p }),
                           new FunctionDefinition("BINOMIALTAIL", "SDMath.BinomialTail", new[] { n, r, p }),
                           new FunctionDefinition("CEXP", "SDMath.Cexp", new[] { x }),
                           new FunctionDefinition("CHI2TAIL", "SDMath.Chi2Tail", new[] { df, q }),
                           new FunctionDefinition("CINT", "SDMath.Cint", new[] { x }),
                           new FunctionDefinition("CLOG", "SDMath.Clog", new[] { x }),
                           new FunctionDefinition("COS", "Math.Cos", new[] { x }),
                           new FunctionDefinition("COSINE", "Math.Cos", new[] { x }),
                           new FunctionDefinition("COSH", "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("COSINEH", "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOS", "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOSINE", "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("COT", "SDMath.Cot", new[] { x }),
                           new FunctionDefinition("COTAN", "SDMath.Cot", new[] { x }),
                           new FunctionDefinition("COTANGENT", "SDMath.Cot", new[] { x }),
                           new FunctionDefinition("COTH", "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("COTANH", "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("COTANGENTH", "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOT", "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOTAN", "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOTANGENT", "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("CSC", "SDMath.Csc", new[] { x }),
                           new FunctionDefinition("COSEC", "SDMath.Csc", new[] { x }),
                           new FunctionDefinition("COSECANT", "SDMath.Csc", new[] { x }),
                           new FunctionDefinition("CSCH", "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("COSECH", "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("COSECANTH", "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCSC", "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOSEC", "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOSECANT", "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("DBINOM", "SDMath.Dbinom", new[] { r, n, p, new ArgumentDefinition("log", "false") }),
                           new FunctionDefinition("DEG", "SDMath.Deg", new[] { x }),
                           new FunctionDefinition("DPOIS", "SDMath.Dpois", new[] { new ArgumentDefinition("k"), new ArgumentDefinition("mean"), logP }),
                           new FunctionDefinition("EXP", "Math.Exp", new[] { x }),
                           new FunctionDefinition("FIX", "Math.Floor", new[] { x }),
                           new FunctionDefinition("FTAIL", "SDMath.Ftail", new[] { new ArgumentDefinition("dfn"), new ArgumentDefinition("dfd"), q }),
                           new FunctionDefinition("INT", "Math.Floor", new[] { x }),
                           new FunctionDefinition("INVCHI2TAIL", "SDMath.InvChi2Tail", new[] { df, p }),
                           new FunctionDefinition("INVFTAIL", "SDMath.InvFtail", new[] { new ArgumentDefinition("dfn"), new ArgumentDefinition("dfd"), p }),
                           new FunctionDefinition("INVNORMAL", "SDMath.Iz", new[] { p }),
                           new FunctionDefinition("INVPOISSONTAIL", "SDMath.InvPoissonTail", new[] { new ArgumentDefinition("mean"), p }),
                           new FunctionDefinition("INVTTAIL", "SDMath.InvTTail", new[] { df, p }),
                           new FunctionDefinition("IZ", "SDMath.Iz", new[] { p }),
                           new FunctionDefinition("LN", "Math.Ln", new[] { x }),
                           new FunctionDefinition("LNNORMAL", "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("LOG", "Math.Log", new[] { x }),
                           new FunctionDefinition("LOG!", "SDMath.LogFactorial", new[] { x }),
                           new FunctionDefinition("LOGFACTORIAL", "SDMath.LogFactorial", new[] { x }),
                           new FunctionDefinition("LOGIT", "SDMath.Logit", new[] { x }),
                           new FunctionDefinition("LOGZ", "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("LR", "SDMath.Lr", new[] { x }),
                           new FunctionDefinition("LZ", "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("NORMAL", "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("PBINOM", "SDMath.Pbinom", new[] { r, n, p, lowerTail, logP }),
                           new FunctionDefinition("PCHISQ", "SDMath.Pchisq", new[] { p, df, lowerTail, logP }),
                           new FunctionDefinition("PF", "SDMath.Pf", new[] { q, new ArgumentDefinition("df1"), new ArgumentDefinition("df2"), lowerTail, logP }),
                           new FunctionDefinition("PNORM", "SDMath.Pnorm", new[] { q, new ArgumentDefinition("mean", "0"), new ArgumentDefinition("sd", "1"), lowerTail, logP }),
                           new FunctionDefinition("POISSONP", "SDMath.Poissonp", new[] { "mean", null, "k", null }),
                           new FunctionDefinition("POISSONTAIL", "SDMath.PoissonTail", new[] { "mean", null, "k", null }),
                           new FunctionDefinition("PPOIS", "SDMath.Ppois", new[] { new ArgumentDefinition("k"), new ArgumentDefinition("mean"), lowerTail, logP }),
                           new FunctionDefinition("PT", "SDMath.Pt", new[] { q, df, new ArgumentDefinition("ncp", "StatsDirect.Numerics.Constant.MISSING"), lowerTail, logP }),
                           new FunctionDefinition("PZ", "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("QCHISQ", "SDMath.Qchisq", new[] { p, df, lowerTail, logP }),
                           new FunctionDefinition("QF", "SDMath.Qf", new[] { q, new ArgumentDefinition("df1"), new ArgumentDefinition("df2"), lowerTail, logP }),
                           new FunctionDefinition("QNORM", "SDMath.Qnorm", new[] { p, new ArgumentDefinition("mean", "0"), new ArgumentDefinition("sd", "1"), lowerTail, logP }),
                           new FunctionDefinition("QPOIS", "SDMath.Qpois", new[] { p, new ArgumentDefinition("mean"), lowerTail, logP }),
                           new FunctionDefinition("QT", "SDMath.Qt", new[] { p, df, new ArgumentDefinition("ncp"), lowerTail, logP }),
                           new FunctionDefinition("RAD", "SDMath.Rad", new[] { x }),
                           new FunctionDefinition("SEC", "SDMath.Sec", new[] { x }),
                           new FunctionDefinition("SECANT", "SDMath.Sec", new[] { x }),
                           new FunctionDefinition("SECH", "SDMath.Sech", new[] { x }),
                           new FunctionDefinition("SECANTH", "SDMath.Sech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICSEC", "SDMath.Sech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICSECANT", "SDMath.Sech", new[] { x }),
                           new FunctionDefinition("SIN", "Math.Sin", new[] { x }),
                           new FunctionDefinition("SINE", "Math.Sin", new[] { x }),
                           new FunctionDefinition("SINH", "SDMath.Sinh", new[] { x }),
                           new FunctionDefinition("SINEH", "SDMath.Sinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICSIN", "SDMath.Sinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICSINE", "SDMath.Sinh", new[] { x }),
                           new FunctionDefinition("SQR", "Math.Sqrt", new[] { x }),
                           new FunctionDefinition("SQRT", "Math.Sqrt", new[] { x }),
                           new FunctionDefinition("TAN", "Math.Tan", new[] { x }),
                           new FunctionDefinition("TANGENT", "Math.Tan", new[] { x }),
                           new FunctionDefinition("TANH", "SDMath.Tanh", new[] { x }),
                           new FunctionDefinition("TANGENTH", "SDMath.Tanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICTAN", "SDMath.Tanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICTANGENT", "SDMath.Tanh", new[] { x }),
                           new FunctionDefinition("TRUNC", "Math.Floor", new[] { x }),
                           new FunctionDefinition("TTAIL", "SDMath.TTail", new[] { df, q }),
                           new FunctionDefinition("UZ", "SDMath.Uz", new[] { q })
                       });

        }

        private void AddAll(IEnumerable<FunctionDefinition> definitions)
        {
            foreach (FunctionDefinition functionDefinition in definitions)
            {
                functionDefinitions.Add(functionDefinition.Name, functionDefinition);
            }
        }

        public FunctionDefinition FunctionNamed(string name)
        {
            FunctionDefinition functionDefinition;
            functionDefinitions.TryGetValue(name, out functionDefinition);
            return functionDefinition;
        }
    }
}

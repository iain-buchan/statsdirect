using System.Collections.Generic;

namespace StatsDirect.Expressions
{
    /// <remarks>Singleton.</remarks>
    public class FunctionRegistry
    {
        private readonly Dictionary<string, FunctionDefinition> functionDefinitions;

        private static FunctionRegistry soleInstance;

        public static FunctionRegistry SoleInstance => soleInstance ?? (soleInstance = new FunctionRegistry());

        private FunctionRegistry()
        {
            // A few useful parameters that can be re-used
            ArgumentDefinition df = new ArgumentDefinition("df", DataType.Double);
            ArgumentDefinition df1 = new ArgumentDefinition("df1", DataType.Double);
            ArgumentDefinition df2 = new ArgumentDefinition("df2", DataType.Double);
            ArgumentDefinition dfd = new ArgumentDefinition("dfd", DataType.Double);
            ArgumentDefinition dfn = new ArgumentDefinition("dfn", DataType.Double);
            ArgumentDefinition k = new ArgumentDefinition("k", DataType.Double);
            ArgumentDefinition logDefFalse = new ArgumentDefinition("log", DataType.Boolean, false, "false");
            ArgumentDefinition mean = new ArgumentDefinition("mean", DataType.Double);
            ArgumentDefinition meanDef0 = new ArgumentDefinition("mean", DataType.Double, false, "0");
            ArgumentDefinition n = new ArgumentDefinition("n", DataType.Double);
            ArgumentDefinition ncp = new ArgumentDefinition("ncp", DataType.Double);
            ArgumentDefinition ncpDefMissing = new ArgumentDefinition("ncp", DataType.Double, false, "StatsDirect.Numerics.Constant.MISSING");
            ArgumentDefinition p = new ArgumentDefinition("p", DataType.Double);
            ArgumentDefinition q = new ArgumentDefinition("q", DataType.Double);
            ArgumentDefinition r = new ArgumentDefinition("r", DataType.Double);
            ArgumentDefinition sdDef1 = new ArgumentDefinition("sd", DataType.Double, false, "1");
            ArgumentDefinition x = new ArgumentDefinition("x", DataType.Double);
            ArgumentDefinition logP = new ArgumentDefinition("log.p", DataType.Boolean, true, "false");
            ArgumentDefinition lowerTail = new ArgumentDefinition("lower.tail", DataType.Boolean, true, "true");

            functionDefinitions = new Dictionary<string, FunctionDefinition>();
            AddAll(new[]
                       {
                           new FunctionDefinition("ABS", DataType.Double, "Math.Abs", new[] { x }),
                           new FunctionDefinition("ALOGIT", DataType.Double, "SDMath.Alogit", new[] { x }),

                           new FunctionDefinition("ACOS", DataType.Double, "Math.Acos", new[] { x }),
                           new FunctionDefinition("ACOSINE", DataType.Double, "Math.Acos", new[] { x }),
                           new FunctionDefinition("ACOSH", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ACOSINEH", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOS", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOSINE", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ACOT", DataType.Double, "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ACOTAN", DataType.Double, "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ACOTANGENT", DataType.Double, "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ACOTH", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ACOTANGENTH", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOT", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOTANGENT", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ACSC", DataType.Double, "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ACOSEC", DataType.Double, "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ACOSECANT", DataType.Double, "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ACSCH", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ACOSECH", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ACOSECANTH", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACSC", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOSEC", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICACOSECANT", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ASEC", DataType.Double, "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ASECANT", DataType.Double, "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ASECH", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ASECANTH", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASEC", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASECANT", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ASIN", DataType.Double, "Math.Asin", new[] { x }),
                           new FunctionDefinition("ASINE", DataType.Double, "Math.Asin", new[] { x }),
                           new FunctionDefinition("ASINH", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ASINEH", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASIN", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICASINE", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ATAN", DataType.Double, "Math.Atan", new[] { x }),
                           new FunctionDefinition("ATANGENT", DataType.Double, "Math.Atan", new[] { x }),
                           new FunctionDefinition("ATANH", DataType.Double, "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("ATANGENTH", DataType.Double, "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICATAN", DataType.Double, "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICATANGENT", DataType.Double, "SDMath.Atanh", new[] { x }),

                           new FunctionDefinition("ARCCOS", DataType.Double, "Math.Acos", new[] { x }),
                           new FunctionDefinition("ARCCOSINE", DataType.Double, "Math.Acos", new[] { x }),
                           new FunctionDefinition("ARCCOSH", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ARCCOSINEH", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOS", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOSINE", DataType.Double, "SDMath.Acosh", new[] { x }),
                           new FunctionDefinition("ARCCOT", DataType.Double, "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ARCCOTAN", DataType.Double, "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ARCCOTANGENT", DataType.Double, "SDMath.Acot", new[] { x }),
                           new FunctionDefinition("ARCCOTH", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ARCCOTANGENTH", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOT", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOTANGENT", DataType.Double, "SDMath.Acoth", new[] { x }),
                           new FunctionDefinition("ARCCSC", DataType.Double, "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ARCCOSEC", DataType.Double, "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ARCCOSECANT", DataType.Double, "SDMath.Acsc", new[] { x }),
                           new FunctionDefinition("ARCCSCH", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ARCCOSECH", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ARCCOSECANTH", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCSC", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOSEC", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCCOSECANT", DataType.Double, "SDMath.Acsch", new[] { x }),
                           new FunctionDefinition("ARCSEC", DataType.Double, "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ARCSECANT", DataType.Double, "SDMath.Asec", new[] { x }),
                           new FunctionDefinition("ARCSECH", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ARCSECANTH", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSEC", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSECANT", DataType.Double, "SDMath.Asech", new[] { x }),
                           new FunctionDefinition("ARCSIN", DataType.Double, "Math.Asin", new[] { x }),
                           new FunctionDefinition("ARCSINE", DataType.Double, "Math.Asin", new[] { x }),
                           new FunctionDefinition("ARCSINH", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ARCSINEH", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSIN", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCSINE", DataType.Double, "SDMath.Asinh", new[] { x }),
                           new FunctionDefinition("ARCTAN", DataType.Double, "Math.Atan", new[] { x }),
                           new FunctionDefinition("ARCTANGENT", DataType.Double, "Math.Atan", new[] { x }),
                           new FunctionDefinition("ARCTANH", DataType.Double, "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("ARCTANGENTH", DataType.Double, "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCTAN", DataType.Double, "SDMath.Atanh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICARCTANGENT", DataType.Double, "SDMath.Atanh", new[] { x }),

                           new FunctionDefinition("BINOMIAL",           DataType.Double, "SDMath.Binomial", new[] { n, r, p }),
                           new FunctionDefinition("BINOMIALP",          DataType.Double, "SDMath.Binomialp", new[] { n, r, p }),
                           new FunctionDefinition("BINOMIALTAIL",       DataType.Double, "SDMath.BinomialTail", new[] { n, r, p }),
                           new FunctionDefinition("CEXP",               DataType.Double, "SDMath.Cexp", new[] { x }),
                           new FunctionDefinition("CHI2TAIL",           DataType.Double, "SDMath.Chi2Tail", new[] { df, q }),
                           new FunctionDefinition("CINT",               DataType.Double, "SDMath.Cint", new[] { x }),
                           new FunctionDefinition("CLOG",               DataType.Double, "SDMath.Clog", new[] { x }),
                           new FunctionDefinition("COS",                DataType.Double, "Math.Cos", new[] { x }),
                           new FunctionDefinition("COSINE",             DataType.Double, "Math.Cos", new[] { x }),
                           new FunctionDefinition("COSH",               DataType.Double, "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("COSINEH",            DataType.Double, "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOS",      DataType.Double, "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOSINE",   DataType.Double, "SDMath.Cosh", new[] { x }),
                           new FunctionDefinition("COT",                DataType.Double, "SDMath.Cot", new[] { x }),
                           new FunctionDefinition("COTAN",              DataType.Double, "SDMath.Cot", new[] { x }),
                           new FunctionDefinition("COTANGENT",          DataType.Double, "SDMath.Cot", new[] { x }),
                           new FunctionDefinition("COTH",               DataType.Double, "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("COTANH",             DataType.Double, "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("COTANGENTH",         DataType.Double, "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOT",      DataType.Double, "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOTAN",    DataType.Double, "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOTANGENT",DataType.Double, "SDMath.Coth", new[] { x }),
                           new FunctionDefinition("CSC",                DataType.Double, "SDMath.Csc", new[] { x }),
                           new FunctionDefinition("COSEC",              DataType.Double, "SDMath.Csc", new[] { x }),
                           new FunctionDefinition("COSECANT",           DataType.Double, "SDMath.Csc", new[] { x }),
                           new FunctionDefinition("CSCH",               DataType.Double, "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("COSECH",             DataType.Double, "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("COSECANTH",          DataType.Double, "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCSC",      DataType.Double, "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOSEC",    DataType.Double, "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("HYPERBOLICCOSECANT", DataType.Double, "SDMath.Csch", new[] { x }),
                           new FunctionDefinition("DBINOM",             DataType.Double, "SDMath.Dbinom", new[] { r, n, p, logDefFalse }),
                           new FunctionDefinition("DEG",                DataType.Double, "SDMath.Deg", new[] { x }),
                           new FunctionDefinition("DPOIS",              DataType.Double, "SDMath.Dpois", new[] { k, mean, logP }),
                           new FunctionDefinition("EXP",                DataType.Double, "Math.Exp", new[] { x }),
                           new FunctionDefinition("FIX",                DataType.Double, "Math.Floor", new[] { x }),
                           new FunctionDefinition("FTAIL",              DataType.Double, "SDMath.Ftail", new[] { dfn, dfd, q }),
                           new FunctionDefinition("INT",                DataType.Double, "Math.Floor", new[] { x }),
                           new FunctionDefinition("INVCHI2TAIL",        DataType.Double, "SDMath.InvChi2Tail", new[] { df, p }),
                           new FunctionDefinition("INVFTAIL",           DataType.Double, "SDMath.InvFtail", new[] { dfn, dfd, p }),
                           new FunctionDefinition("INVNORMAL",          DataType.Double, "SDMath.Iz", new[] { p }),
                           new FunctionDefinition("INVPOISSONTAIL",     DataType.Double, "SDMath.InvPoissonTail", new[] { mean, p }),
                           new FunctionDefinition("INVTTAIL",           DataType.Double, "SDMath.InvTTail", new[] { df, p }),
                           new FunctionDefinition("IZ",                 DataType.Double, "SDMath.Iz", new[] { p }),
                           new FunctionDefinition("LN",                 DataType.Double, "Math.Log", new[] { x }),
                           new FunctionDefinition("LNNORMAL",           DataType.Double, "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("LOG",                DataType.Double, "Math.Log", new[] { x }),
                           new FunctionDefinition("LOG!",               DataType.Double, "SDMath.LogFactorial", new[] { x }),
                           new FunctionDefinition("LOGFACTORIAL",       DataType.Double, "SDMath.LogFactorial", new[] { x }),
                           new FunctionDefinition("LOGIT",              DataType.Double, "SDMath.Logit", new[] { x }),
                           new FunctionDefinition("LOGZ",               DataType.Double, "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("LR",                 DataType.Double, "SDMath.Lr", new[] { x }),
                           new FunctionDefinition("LZ",                 DataType.Double, "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("NORMAL",             DataType.Double, "SDMath.Lz", new[] { q }),
                           new FunctionDefinition("PBINOM",             DataType.Double, "SDMath.Pbinom", new[] { r, n, p, lowerTail, logP }),
                           new FunctionDefinition("PCHISQ",             DataType.Double, "SDMath.Pchisq", new[] { p, df, lowerTail, logP }),
                           new FunctionDefinition("PF",                 DataType.Double, "SDMath.Pf", new[] { q, df1, df2, lowerTail, logP }),
                           new FunctionDefinition("PNORM",              DataType.Double, "SDMath.Pnorm", new[] { q, meanDef0, sdDef1, lowerTail, logP }),
                           new FunctionDefinition("POISSONP",           DataType.Double, "SDMath.Poissonp", new[] { mean, k }),
                           new FunctionDefinition("POISSONTAIL",        DataType.Double, "SDMath.PoissonTail", new[] { mean, k }),
                           new FunctionDefinition("PPOIS",              DataType.Double, "SDMath.Ppois",     new[] { k, mean, lowerTail, logP }),
                           new FunctionDefinition("PT",                 DataType.Double, "SDMath.Pt",        new[] { q, df, ncpDefMissing, lowerTail, logP }),
                           new FunctionDefinition("PZ",                 DataType.Double, "SDMath.Lz",        new[] { q }),
                           new FunctionDefinition("QCHISQ",             DataType.Double, "SDMath.Qchisq",    new[] { p, df, lowerTail, logP }),
                           new FunctionDefinition("QF",                 DataType.Double, "SDMath.Qf",        new[] { q, df1, df2, lowerTail, logP }),
                           new FunctionDefinition("QNORM",              DataType.Double, "SDMath.Qnorm",    new[] { p, meanDef0, sdDef1, lowerTail, logP }),
                           new FunctionDefinition("QPOIS",              DataType.Double, "SDMath.Qpois",     new[] { p, mean, lowerTail, logP }),
                           new FunctionDefinition("QT",                 DataType.Double, "SDMath.Qt",        new[] { p, df, ncp, lowerTail, logP }),
                           new FunctionDefinition("RAD",                DataType.Double, "SDMath.Rad",       new[] { x }),
                           new FunctionDefinition("SEC",                DataType.Double, "SDMath.Sec",       new[] { x }),
                           new FunctionDefinition("SECANT",             DataType.Double, "SDMath.Sec",       new[] { x }),
                           new FunctionDefinition("SECH",               DataType.Double, "SDMath.Sech",      new[] { x }),
                           new FunctionDefinition("SECANTH",            DataType.Double, "SDMath.Sech",      new[] { x }),
                           new FunctionDefinition("HYPERBOLICSEC",      DataType.Double, "SDMath.Sech",      new[] { x }),
                           new FunctionDefinition("HYPERBOLICSECANT",   DataType.Double, "SDMath.Sech",      new[] { x }),
                           new FunctionDefinition("SIN",                DataType.Double, "Math.Sin",         new[] { x }),
                           new FunctionDefinition("SINE",               DataType.Double, "Math.Sin",         new[] { x }),
                           new FunctionDefinition("SINH",               DataType.Double, "SDMath.Sinh",      new[] { x }),
                           new FunctionDefinition("SINEH",              DataType.Double, "SDMath.Sinh",      new[] { x }),
                           new FunctionDefinition("HYPERBOLICSIN",      DataType.Double, "SDMath.Sinh",      new[] { x }),
                           new FunctionDefinition("HYPERBOLICSINE",     DataType.Double, "SDMath.Sinh",      new[] { x }),
                           new FunctionDefinition("SQR",                DataType.Double, "Math.Sqrt",        new[] { x }),
                           new FunctionDefinition("SQRT",               DataType.Double, "Math.Sqrt",        new[] { x }),
                           new FunctionDefinition("TAN",                DataType.Double, "Math.Tan",         new[] { x }),
                           new FunctionDefinition("TANGENT",            DataType.Double, "Math.Tan",         new[] { x }),
                           new FunctionDefinition("TANH",               DataType.Double, "SDMath.Tanh",      new[] { x }),
                           new FunctionDefinition("TANGENTH",           DataType.Double, "SDMath.Tanh",      new[] { x }),
                           new FunctionDefinition("HYPERBOLICTAN",      DataType.Double, "SDMath.Tanh",      new[] { x }),
                           new FunctionDefinition("HYPERBOLICTANGENT",  DataType.Double, "SDMath.Tanh",      new[] { x }),
                           new FunctionDefinition("TRUNC",              DataType.Double, "Math.Floor",       new[] { x }),
                           new FunctionDefinition("TTAIL",              DataType.Double, "SDMath.TTail",     new[] { df, q }),
                           new FunctionDefinition("UZ",                 DataType.Double, "SDMath.Uz",        new[] { q })
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

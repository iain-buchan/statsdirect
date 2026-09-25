using StatsDirect.Numerics;
using StatsDirect.Numerics.SpecialFunctions;

int checkedCount = 0;
void Check(bool ok, string name)
{
    checkedCount++;
    if (!ok) throw new Exception(name);
}
void Near(double actual, double expected, string name, double relative = 2e-12, double absolute = 0)
    => Check(actual == expected || double.IsFinite(actual) && double.IsFinite(expected)
        && Math.Abs(actual - expected) <= absolute + relative * Math.Abs(expected), $"{name}: {actual:G17}, expected {expected:G17}");

// Reference values computed in 70-digit arithmetic, at arguments where the old routines were wrong, gave no answer or lost
// figures without reporting a fault (the upper rate ratio limit below was 1.92e-7 for 2.77e-7).
Near(PDF.xinbta(.1, 1000, .025, out int fault), 5.7943174492504480357e-20, "inverse beta regression");
Check(fault == 0, "inverse beta status");
Near(PDF.ffromp(1e12, 2, .001), 6.9077552790298541142, "large-df F quantile");
Near(PDF.tfromp(.49999999, 2), 2.8284271232574315061e-8, "near-zero df=2 t quantile");
Near(PDF.tvalp(1e155, 1), 3.1830988618379066925e-156, "overflow-safe Cauchy tail");
Near(PDF.FProbability(1e20, 2, 1000, false, true), -19918.546880729360971, "F log tail");
Near(PDF.tfromp(1e-20, .1), 1.6044257056665295067e196, "finite heavy-tail t quantile", 2e-12);

double alpha = (1 - .998) / 2;
double rateUpper = 1.5 * (2.0 / 50000000) * PDF.ffromp(100000000, 4, alpha);
Near(rateUpper, 2.7700242710030197463e-7, "rate-ratio upper confidence limit");

// Closed forms: the log upper tail of F on 2 numerator degrees of freedom, and the t quantile on 2 degrees of freedom.
foreach (double d in new[] { .5, 2, 30, 1e5, 1e12 })
foreach (double f in new[] { 1e-100, .001, 1, 100, 1e100 })
{
    double logTail = -d / 2 * MathSupport.Log1p(2 * f / d);
    Near(PDF.FProbability(f, 2, d, false, true), logTail, "F(2,df) identity", 1e-13, 1e-300);
}
foreach (double p in new[] { 1e-200, 1e-20, 1e-6, .025, .49999999 })
{
    double expected = (1 - 2 * p) / Math.Sqrt(2 * p * (1 - p));
    Near(PDF.tfromp(p, 2), expected, "t(df=2) inverse identity", 1e-13);
}

// Finite log probabilities must survive when the ordinary probability is zero.
foreach (double lp in new[] { -1e-20, -.7, -10, -700, -1000, -10000 })
foreach (double df in new[] { .5, 2, 30, 1000, 1e12 })
foreach (bool lower in new[] { false, true })
{
    double f = PDF.FQuantile(lp, 2, df, lower, true);
    if (f > 0 && double.IsFinite(f))
        Near(PDF.FProbability(f, 2, df, lower, true), lp, "log F roundtrip", 5e-12, 2e-13);
    double t = PDF.TQuantile(lp, df, lower, true);
    if (double.IsFinite(t) && t != 0)
        Near(PDF.TProbability(t, df, lower, true), lp, "log t roundtrip", 5e-12, 2e-13);
}

// Every inverse preserves a monotone bracket, including shapes smaller than 1
// and quantiles whose ordinary x rounds to zero or one.
foreach (double a in new[] { .01, .1, .5, 2, 40, 128, 1e5, 1e12 })
foreach (double b in new[] { .01, .5, 2, 128, 1e5, 1e12 })
{
    double previous = double.NegativeInfinity;
    foreach (double p in new[] { 1e-100, 1e-20, 1e-6, .025, .5, .975, 1-1e-6 })
    {
        var point = IncompleteBeta708.Inverse(a, b, p);
        double logit = point.LogX - point.LogY;
        Check(logit >= previous, "inverse monotonicity");
        previous = logit;
        Near(point.X + point.Y, 1, "coordinate sum", 0, 2e-16);
        Check(point.X >= 0 && point.Y >= 0, "coordinate bounds");
        if (Math.Min(a, b) < 1e10)
        {
            var actual = IncompleteBeta708.FromLogit(a, b, logit);
            double expected = p <= .5 ? Math.Log(p) : MathSupport.Log1p(-p);
            double got = p <= .5 ? actual.LogLower : actual.LogUpper;
            Near(got, expected, "inverse probability residual", 1e-11, 1e-11);
        }
    }
}

// Infinite degrees of freedom: the chi-square and normal limits. F on 2 and infinity has the upper tail exp(-f); F on infinity
// and 2 has the lower tail exp(-1 / f); t on infinity is the normal, whose tail at 2 is 0.022750131948179207; with both
// infinite all the mass is at 1.
foreach (double f in new[] { .001, .5, 1, 5, 100 })
{
    Near(PDF.FProbability(f, 2, double.PositiveInfinity), Math.Exp(-f), "F(2, infinity) tail", 1e-13);
    Near(PDF.FProbability(f, 2, double.PositiveInfinity, false, true), -f, "F(2, infinity) log tail", 1e-13);
    Near(PDF.FProbability(f, double.PositiveInfinity, 2, true), Math.Exp(-1 / f), "F(infinity, 2) lower tail", 1e-13);
    Near(PDF.FQuantile(Math.Exp(-f), 2, double.PositiveInfinity), f, "F(2, infinity) quantile", 1e-12);
}
Near(PDF.TProbability(2, double.PositiveInfinity), 0.022750131948179207, "t(infinity) tail", 1e-13);
Near(PDF.TProbability(-2, double.PositiveInfinity, true), 0.022750131948179207, "t(infinity) lower tail", 1e-13);
Near(PDF.TQuantile(.025, double.PositiveInfinity), 1.959963984540054, "t(infinity) quantile", 1e-13);
Near(PDF.FQuantile(.05, 1, double.PositiveInfinity), 3.841458820694124, "F(1, infinity) quantile", 1e-13);
Near(PDF.FProbability(.5, double.PositiveInfinity, double.PositiveInfinity), 1, "F(infinity, infinity) below 1");
Near(PDF.FProbability(1, double.PositiveInfinity, double.PositiveInfinity), .5, "F(infinity, infinity) at 1");
Near(PDF.FProbability(2, double.PositiveInfinity, double.PositiveInfinity, true), 1, "F(infinity, infinity) above 1");
Near(PDF.FQuantile(.3, double.PositiveInfinity, double.PositiveInfinity), 1, "F(infinity, infinity) quantile");
Near(PDF.FQuantile(0, double.PositiveInfinity, double.PositiveInfinity, true), 0, "F(infinity, infinity) lower endpoint");
Near(PDF.tfromp(0, double.PositiveInfinity), double.PositiveInfinity, "t(infinity) upper endpoint");

// Endpoints and faults are deliberately tested separately from convergence.
Near(PDF.ffromp(30, 2, 0), double.PositiveInfinity, "F zero upper probability");
Near(PDF.ffromp(30, 2, 1), 0, "F unit upper probability");
Near(PDF.tfromp(0, 30), double.PositiveInfinity, "t zero upper probability");
Near(PDF.tfromp(1, 30), double.NegativeInfinity, "t unit upper probability");
Near(PDF.tfromp(.5, 30), 0, "t median");
Near(PDF.tfromp2(1, 30), 0, "two-tail t endpoint");
Check(double.IsFinite(PDF.tfromp2(double.Epsilon, 2)), "two-tail subnormal probability");
Check(double.IsNaN(PDF.tfromp2(1.5, 30)), "invalid two-tail probability");
Check(double.IsNaN(PDF.fvalp(-1, 5, 10)), "raw F negative input remains invalid");
Near(PDF.FProbability(-1, 5, 10), 1, "calculator F negative argument");
Near(PDF.xinbta(2, 3, 0, out fault), 0, "beta zero probability");
Check(fault == 0, "beta zero status");
PDF.xinbta(double.NaN, 3, .1, out fault);
Check(fault == 1, "invalid first shape");
PDF.xinbta(2, double.PositiveInfinity, .1, out fault);
Check(fault == 2, "invalid second shape");
PDF.xinbta(2, 3, -.1, out fault);
Check(fault == 3, "invalid probability");
PDF.betain(.5, .25, 2, 3, out fault);
Check(fault == 2, "inconsistent coordinates");
Check(double.IsNaN(PDF.FQuantile(-1, 2, 30)), "invalid F probability");
Check(double.IsNaN(PDF.TProbability(1, 0)), "invalid t degrees of freedom");
bool failed = false;
try { IncompleteBeta708.Inverse(2, 3, .123456, true, false, 1); }
catch (ArithmeticException) { failed = true; }
Check(failed, "iteration exhaustion must fail explicitly");
Console.WriteLine($"PASS: {checkedCount} distribution regression checks");

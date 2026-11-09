// A predictor that takes one value, beside the constant, adds nothing to a logistic or Poisson model: the fit should be that of the model
// without it, with its coefficient and standard error 0, the rank one less than the parameters and the residual degrees of freedom the
// observations less the rank, as R's glm leaves such a predictor out.  A constant other than a power of 2 had left a singular value just
// above the tolerance of the rank (rank 2 and 10 df where 1 and 11 are right, with coefficients near 1e14), or stopped the decomposition.
using StatsDirect.Builtins;

internal static partial class Program
{
    private static void ConstantPredictorChecks()
    {
        Console.WriteLine();
        Console.WriteLine("a constant predictor beside the constant");
        const int records = 12;
        double[] counts = { 2, 5, 9, 14, 20, 11, 6, 4, 8, 7, 12, 16 };
        double[] exposures = { 100, 120, 140, 160, 180, 150, 110, 90, 130, 120, 150, 170 };
        double[] responders = { 2, 3, 4, 5, 6, 8, 10, 11, 12, 14, 15, 17 };
        foreach (bool poisson in new[] { true, false })
            foreach (double c in new[] { 0.7, 1.3, 2, 3 })
                foreach (bool withDose in new[] { false, true })
                {
                    string title = (poisson ? "Poisson" : "logistic") + ", constant " + c + (withDose ? " before a dose" : " alone");
                    // the design: the constant predictor, then (or not) a dose that varies
                    int predictors = withDose ? 2 : 1;
                    double[,] x = new double[records + 1, predictors + 1];
                    for (int i = 1; i <= records; i++)
                    {
                        x[i, 1] = c;
                        if (withDose)
                            x[i, 2] = 1.5 + (i - 1);
                    }
                    var fit = GlmFit(poisson, x, predictors, poisson ? counts : responders, exposures);
                    // the same model without the constant predictor: the constant alone, or the constant and the dose
                    double[,] reduced = new double[records + 1, 2];
                    for (int i = 1; i <= records; i++)
                        reduced[i, 1] = withDose ? x[i, 2] : 1.0;
                    var reference = withDose ? GlmFit(poisson, reduced, 1, poisson ? counts : responders, exposures) : default;
                    bool ok = fit.errLevel != 2 && fit.rank == predictors && fit.df == records - predictors && fit.beta[2] == 0 && fit.se[2] == 0;
                    Console.WriteLine($"      error level {fit.errLevel} {fit.errMsg}; rank {fit.rank}, df {fit.df}, deviance {fit.deviance:F6}; coefficients " + string.Join(", ", fit.beta.Skip(1).Select(v => v.ToString("G6"))));
                    Check(title + ": rank, residual df, and a coefficient and standard error of 0 for the constant", ok ? 0 : 1, 0);
                    double worst;
                    if (withDose)
                    {
                        worst = Math.Max(Math.Abs(fit.deviance - reference.deviance), Math.Max(Math.Abs(fit.beta[1] - reference.beta[1]), Math.Abs(fit.beta[3] - reference.beta[2])));
                        worst = Math.Max(worst, Math.Max(Math.Abs(fit.se[1] - reference.se[1]), Math.Abs(fit.se[3] - reference.se[2])));
                    }
                    else
                    {
                        // the constant alone, by calculation: mu = t sum(y) / sum(t) (Poisson), p = sum(r) / sum(n) (logistic)
                        double total = poisson ? exposures.Sum() : 20.0 * records, events = (poisson ? counts : responders).Sum();
                        double level = poisson ? Math.Log(events / total) : Math.Log(events / (total - events));
                        double deviance = 0;
                        for (int i = 0; i < records; i++)
                        {
                            double y = poisson ? counts[i] : responders[i], n = poisson ? exposures[i] : 20, mu = n * events / total;
                            deviance += poisson ? 2 * (y * Math.Log(y / mu) - (y - mu)) : 2 * (y * Math.Log(y / mu) + (n - y) * Math.Log((n - y) / (n - mu)));
                        }
                        worst = Math.Max(Math.Abs(fit.beta[1] - level), Math.Abs(fit.deviance - deviance));
                    }
                    Check(title + ": the fit is that of the model without the constant predictor", worst, 1e-8);
                }
    }

    private static (double[] beta, double[] se, double deviance, int df, int rank, int errLevel, string errMsg) GlmFit(bool poisson, double[,] x, int predictors, double[] y0, double[] exposures)
    {
        const int records = 12;
        int p = predictors + 1;
        double[] y = new double[records + 1], t = new double[records + 1], weight = new double[records + 1], offset = new double[records + 1];
        for (int i = 1; i <= records; i++)
        {
            y[i] = y0[i - 1];
            t[i] = poisson ? 1 : 20;
            weight[i] = 1;
            offset[i] = poisson ? Math.Log(exposures[i - 1]) : 0;
        }
        bool[] select = Enumerable.Repeat(true, predictors + 1).ToArray();
        bool useWeights = false;
        double[] beta = new double[p + 1], se = new double[p + 1], covariance = new double[p * (p + 1) / 2 + 1];
        double[] fit = new double[records + 1], residual = new double[records + 1], leverage = new double[records + 1];
        double deviance = 0; int df = 0, rank = 0, errLevel; string dropped = "", errMsg = "";
        if (poisson)
            Regress1.X_Poisson_Regression(true, true, ref useWeights, records, x, predictors, select, p, y, t, weight, ref deviance, ref df, beta, ref rank, se, covariance, 1e-12, 50, fit, residual, leverage, offset, out errLevel, ref dropped, ref errMsg);
        else
            Regress1.X_Logistic_Regression(true, false, ref useWeights, records, x, predictors, select, p, y, t, weight, out deviance, ref df, beta, ref rank, se, covariance, 1e-12, 50, fit, residual, leverage, offset, out errLevel, ref dropped, ref errMsg);
        return (beta, se, deviance, df, rank, errLevel, errMsg);
    }
}

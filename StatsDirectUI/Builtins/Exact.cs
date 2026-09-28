using System;
using System.Collections.Generic;

using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public static class Exact
    {
        /// <summary>
        /// The sign test: of n observations r are on one side, and the hypothesis is that either side is as likely.
        /// </summary>
        /// <remarks>
        /// The one sided P value is the probability of as few as the less of r and n - r being on one side, a sum of binomial
        /// probabilities, and the two sided P value is twice that, or 1 if that is more.  z is the normal deviate with the
        /// correction for continuity, (|n / 2 - r| - 1/2) / root(n / 4), or 0 if that is below 0; the report gives its P values.
        /// The limits of the proportion r / n are those of Clopper and Pearson (MathDbl.binci).  If r is given as more than n, the
        /// two are taken the other way round.
        /// </remarks>
        /// <param name="parameters">"n": the number of observations; "r": the number on one side; "cco": the confidence level,
        /// for which 0.95 is taken if it is not between 0 and 1.</param>
        /// <returns>"sample" and "sample_1": n and r; "*exact": a row with "prob_1" and "prob_2", the one sided and the two sided
        /// P value; "*large": nothing (the report has a line for a sample too large for the exact calculation, which is made for
        /// any n); "z"; "ci": the confidence level as a percentage; "lower", "prop" and "upper": the proportion and its limits;
        /// "warn": what the report says after the limits when the interval is one sided (r is 0 or n).</returns>
        public static StepOutput RptExactSign(ParameterBag parameters)
        {
            double n = parameters["n"].AsDouble;
            double r = parameters["r"].AsDouble;
            if (r > n)
            {
                double temp = r;
                r = n;
                n = temp;
            }
            ParameterBag outputParameters = new();
            if (n <= 0.0)
                throw new InvalidDataException();

            double acr = r;
            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;

            if (r > n / 2.0)
                r = n - r;

            outputParameters.AddOutput("sample", n);
            outputParameters.AddOutput("sample_1", acr);

            // The lower tail P(X <= r) is summed from its largest term, P(X = r), downwards until the terms no longer count, so that it
            // is available for any n: a sum started from 0.5^n underflows to zero above n = 1074
            long k = Convert.ToInt64(r);
            double f = Math.Exp(PDF.alogam(n + 1.0) - PDF.alogam(k + 1.0) - PDF.alogam(n - k + 1.0) - n * Math.Log(2.0));
            double p = f;
            for (long i = k; i >= 1 && f > p * Constant.EPSNEG; i--)
            {
                f *= Convert.ToDouble(i) / (n - i + 1.0);
                p += f;
            }
            double p2 = 2.0 * p;
            if (p2 > 1.0)
                p2 = 1.0;

            List<ParameterBag> exactList = new();
            outputParameters.AddOutput("*exact", exactList);
            ParameterBag exactParameters = new();
            exactList.Add(exactParameters);
            exactParameters.AddOutput("prob_2", p2);
            exactParameters.AddOutput("prob_1", p);
            outputParameters.AddOutput("*large", null);

            double d = Math.Abs(n / 2.0 - r) - 0.5;
            double x9;
            if (d < 0.0)
                x9 = 0.0;
            else
                x9 = d / Math.Sqrt(n / 4.0);

            outputParameters.AddOutput("z", x9);

            r = acr;
            outputParameters.AddOutput("ci", cco * 100);

            MathDbl.binci(r, n, out double pil, out double piu, cco, out string warn);

            outputParameters.AddOutput("lower", pil);
            outputParameters.AddOutput("prop", r / n);
            outputParameters.AddOutput("upper", piu);
            outputParameters.AddOutput("warn", warn);

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Fisher's exact test of a 2 by 2 table (Tables.SFisher).  Counts that are not whole numbers are rounded, a half to the
        /// even number.
        /// </summary>
        /// <param name="parameters">"a", "b": the first row of the table; "c", "d": the second.</param>
        /// <returns>What Tables.SFisher returns.</returns>
        public static StepOutput RptExactFisher(ParameterBag parameters)
        {
            int fault = 0;
            int a = Convert.ToInt32(parameters["a"].AsDouble);
            int b = Convert.ToInt32(parameters["b"].AsDouble);
            int c = Convert.ToInt32(parameters["c"].AsDouble);
            int d = Convert.ToInt32(parameters["d"].AsDouble);
            ParameterBag outputParameters = Tables.SFisher(ref a, ref b, ref c, ref d, ref fault);
            if (fault != 0)
                throw new TemplateOperationCancelledException();
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The expanded Fisher-Irwin test of a 2 by 2 table: Fisher's exact test, with the distribution of the first count.
        /// </summary>
        /// <remarks>
        /// The table is arranged and its P values are worked out as in Tables.SFisher.  The rows of the distribution are for the
        /// values 0 to the total of the first row of the arranged table: the probability of no more than the value, of the value,
        /// and of no less, each as text (Formatting.pr14).  A table too large for its distribution to be tabulated (the
        /// probability that the first count is 0 is below 1e-300) has no rows, and its P values are from Tables.FisherLarge.
        /// </remarks>
        /// <param name="parameters">"a", "b": the first row of the table; "c", "d": the second.  Counts that are not whole numbers
        /// are rounded, a half to the even number.</param>
        /// <returns>What Tables.SFisher returns, but for "tail_2"; "*header": a row if the distribution is tabulated; "*row":
        /// its rows, with "a", "lower", "ind_p" and "upper".</returns>
        public static StepOutput RptExactFisherX(ParameterBag parameters)
        {
            int fault = 0;

            int a = Convert.ToInt32(parameters["a"].AsDouble);
            int b = Convert.ToInt32(parameters["b"].AsDouble);
            int c = Convert.ToInt32(parameters["c"].AsDouble);
            int d = Convert.ToInt32(parameters["d"].AsDouble);

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("tab_a1", a);
            outputParameters.AddOutput("tab_b1", b);
            outputParameters.AddOutput("tab_a2", c);
            outputParameters.AddOutput("tab_b2", d);

            if (a > d)
            {
                int t = a;
                a = d;
                d = t;
            }
            if (b > c)
            {
                int t = b;
                b = c;
                c = t;
            }

            int p = a + b;
            int q = c + d;
            int r = a + c;
            int s = b + d;
            int n = p + q;

            if (p <= 0 || q <= 0 || r <= 0 || s <= 0)
            {
                throw new InvalidDataException();
            }

            outputParameters.AddOutput("tab3_a1", a);
            outputParameters.AddOutput("tab3_b1", b);
            outputParameters.AddOutput("tab3_c1", p);
            outputParameters.AddOutput("tab3_a2", c);
            outputParameters.AddOutput("tab3_b2", d);
            outputParameters.AddOutput("tab3_c2", q);
            outputParameters.AddOutput("tab3_a3", r);
            outputParameters.AddOutput("tab3_b3", s);
            outputParameters.AddOutput("tab3_c3", n);

            double b0 = 1.0;
            double n1 = n;
            double s1 = s;
            do
            {
                if (b0 > 1.0E+300 | s1 <= 0.0)
                {
                    fault = 1;
                    break;
                }
                b0 = b0 * n1 / s1;
                s1 -= 1.0;
                n1 -= 1.0;
            }
            while (n1 > Convert.ToDouble(q));

            double e1 = Convert.ToDouble(p) * Convert.ToDouble(r) / Convert.ToDouble(n);

            outputParameters.AddOutput("exp_a", e1);

            List<ParameterBag> headerList = new();
            outputParameters.AddOutput("*header", headerList);
            List<ParameterBag> rowList = new();
            outputParameters.AddOutput("*row", rowList);
            if (fault != 0)
            {
                // Too large a table to tabulate: the P values are found without it
                Tables.FisherLarge(a, b, c, d, e1, out string tail1, out double p1, out double p2, out double midP1);
                outputParameters.AddOutput("tail_1", tail1);
                outputParameters.AddOutput("p_1", p1);
                outputParameters.AddOutput("p_1d", Math.Min(p1 * 2.0, 1.0));
                outputParameters.AddOutput("p_2", p2);
                outputParameters.AddOutput("mid_p", midP1);
                outputParameters.AddOutput("mid_p_2", Math.Min(midP1 * 2.0, 1.0));
            }
            else
            {
                double[] f1 = new double[p + 2];
                double[] g1 = new double[p + 2];
                double[] h1 = new double[p + 2];
                int a1 = 0;
                int q1 = q - r;
                int p1 = p;
                int r1 = r;
                double h = 1.0 / b0;
                double f = h;
                f1[1] = f;
                h1[1] = h;
                double g = 1.0;
                g1[1] = 1.0;

                headerList.Add(new ParameterBag());
                ParameterBag rowParameters = new();
                rowList.Add(rowParameters);
                rowParameters.AddOutput("a", a1);
                rowParameters.AddOutput("lower", Formatting.pr14(f));
                rowParameters.AddOutput("ind_p", Formatting.pr14(h));
                rowParameters.AddOutput("upper", Formatting.pr14(g));
                int a2;
                do
                {
                    a1++;
                    q1++;
                    h *= Convert.ToDouble(p1) / Convert.ToDouble(a1) * Convert.ToDouble(r1) / Convert.ToDouble(q1);
                    f += h;
                    a2 = a1 + 1;
                    f1[a2] = f;
                    h1[a2] = h;
                    --p1;
                    --r1;
                }
                while (p1 > 0L);

                //   UPPER TAIL PROBABILITIES WOULD BE SUBJECT TO SUBTRACTION ERRORS
                //   IF CALCULATED BY 1 - F. THEREFORE ......

                g = 0.0;
                for (int j = a2; j >= 2; j--)
                {
                    g += h1[j];
                    g1[j] = g;
                }
                // int Start = 1; 
                for (int j = 2; j <= a2; j++)
                {
                    rowParameters = new ParameterBag();
                    rowList.Add(rowParameters);
                    rowParameters.AddOutput("a", j - 1);
                    rowParameters.AddOutput("lower", Formatting.pr14(f1[j]));
                    rowParameters.AddOutput("ind_p", Formatting.pr14(h1[j]));
                    rowParameters.AddOutput("upper", Formatting.pr14(g1[j]));
                }

                a1 = a + 1;
                h = 1.00000000000001 * h1[a1];
                double midP;
                if (a > e1)
                {

                    g = g1[a1];
                    f = 0.0;
                    for (int j = 1; j <= a2; j++)
                    {
                        if (h1[j] > h)
                            break;
                        f = f1[j];
                    }

                    double g2 = 2.0 * g;
                    if (g2 > 1.0)
                        g2 = 1.0;
                    outputParameters.AddOutput("tail_1", "(upper tail)");
                    outputParameters.AddOutput("p_1", g);
                    outputParameters.AddOutput("p_1d", g2);
                    midP = g - h1[a1] / 2.0;

                }
                else
                {
                    f = f1[a1];
                    g = 0.0;
                    for (int j = a2; j >= 1; j--)
                    {
                        if (h1[j] > h)
                            break;
                        g = g1[j];
                    }
                    double f2 = 2.0 * f;
                    if (f2 > 1.0)
                        f2 = 1.0;

                    outputParameters.AddOutput("tail_1", "(lower tail)");
                    outputParameters.AddOutput("p_1", f);
                    outputParameters.AddOutput("p_1d", f2);
                    midP = f - h1[a1] / 2.0;

                }

                double z = f + g;
                if (z > 1.0)
                    z = 1.0;

                outputParameters.AddOutput("p_2", z);
                outputParameters.AddOutput("mid_p", midP);
                outputParameters.AddOutput("mid_p_2", Math.Min(midP * 2.0, 1.0));

            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Matched pairs: McNemar's chi-square test, and the exact test and confidence interval of the ratio of the two numbers of
        /// pairs that differ.
        /// </summary>
        /// <remarks>
        /// Of the pairs, b differ one way and c the other; a and d, the pairs that do not differ, are in the report and in nothing
        /// else.  Chi-square is (b - c)^2 / (b + c), and with the correction for continuity (|b - c| - 1)^2 / (b + c), in which
        /// |b - c| - 1 is taken as 0 if it is below 0; each has 1 degree of freedom.
        /// The ratio R' is b / c.  Its limits are from quantiles of the F distribution, and are the limits of Clopper and Pearson
        /// for the proportion b / (b + c), each as the odds p / (1 - p).  With r the greater of b and c, and s the less, F is
        /// r / (s + 1), and the two sided P value is twice the probability that F with 2 (s + 1) and 2 r degrees of freedom is
        /// above it, or 1 if that is more: twice the probability of r or more of the b + c pairs being one way, when either way
        /// is as likely.
        /// </remarks>
        /// <param name="parameters">"a", "b": the first row of the table of pairs; "c", "d": the second; "gamma": the confidence
        /// level, for which 0.95 is taken if it is not between 0 and 1.</param>
        /// <returns>"tab_a1" to "tab_b2": the table; "chi", "chi_p", "yates_chi", "yates_chi_p": the two chi-squares and their
        /// P values; "risk": R', infinity if c is 0; "pc": the confidence level as a percentage; "from" and "to": the limits of
        /// R', of which the upper is infinity if c is 0; "f" and "tail_2": F and the two sided P value; "*r_prime": a row if the P
        /// value is below 0.05, for which the report says that R' differs from 1.</returns>
        public static StepOutput RptExactMcNamar(ParameterBag parameters)
        {
            double ba = parameters["a"].AsDouble;
            double bb = parameters["b"].AsDouble;
            double bc = parameters["c"].AsDouble;
            double bd = parameters["d"].AsDouble;
            double gamma = parameters["gamma"].AsDouble;
            if (gamma <= 0.0 || gamma >= 1.0)
                gamma = 0.95;

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("tab_a1", ba);
            outputParameters.AddOutput("tab_b1", bb);
            outputParameters.AddOutput("tab_a2", bc);
            outputParameters.AddOutput("tab_b2", bd);

            if (bb + bc <= 0.0)
                throw new InvalidDataException();

            double x2 = Math.Abs(bb - bc) * Math.Abs(bb - bc) / (bb + bc);
            outputParameters.AddOutput("chi", x2);
            outputParameters.AddOutput("chi_p", PDF.chivalp(x2, 1.0));

            x2 = Math.Max(Math.Abs(bb - bc) - 1.0, 0.0);   // the continuity correction cannot take the difference past zero
            double n = bb + bc;
            x2 = x2 * x2 / n;
            outputParameters.AddOutput("yates_chi", x2);
            outputParameters.AddOutput("yates_chi_p", PDF.chivalp(x2, 1.0));

            double rr = bc > 0.0
                ? bb / bc
                : double.PositiveInfinity;
            outputParameters.AddOutput("risk", rr);

            double r = bb;
            double s = bc;

            if (r < s)
                Utilities.Utilities.Swap(ref r, ref s);
            double p = (1 - gamma) / 2.0;
            double dfn = 2.0 * (s + 1.0);
            double dfd = 2.0 * r;
            double llf = PDF.ffromp(dfd, dfn, p);
            dfn = 2.0 * (r + 1.0);
            dfd = 2.0 * s;
            double ulf = PDF.ffromp(dfd, dfn, p);
            double ll = llf > 0.0
                ? r / ((s + 1.0) * llf)
                : Constant.MISSING;
            double ul = s > 0.0
                ? (r + 1.0) * ulf / s
                : Constant.MISSING;

            if (bc > bb)
            {
                // R' = b/c is the reciprocal of r/s, so its limits are the reciprocals of these the other way round; a missing upper limit
                // is infinity, whose reciprocal is zero
                double t = ll;
                ll = ul != Constant.MISSING
                    ? 1.0 / ul
                    : 0.0;
                ul = t != Constant.MISSING
                    ? 1.0 / t
                    : Constant.MISSING;
            }
            if (ll != Constant.MISSING && ul != Constant.MISSING)
            {
                if (ul < ll)
                    Utilities.Utilities.Swap(ref ll, ref ul);
            }
            outputParameters.AddOutput("pc", gamma * 100);
            if (ll == Constant.MISSING)
                ll = double.NegativeInfinity;
            outputParameters.AddOutput("from", ll);
            if (ul == Constant.MISSING)
                ul = double.PositiveInfinity;
            outputParameters.AddOutput("to", ul);

            double f = r / (s + 1.0);
            p = PDF.fvalp(f, 2.0 * (s + 1.0), 2.0 * r) * 2.0;
            if (p > 1.0)
                p = 1.0;

            outputParameters.AddOutput("f", f);
            outputParameters.AddOutput("tail_2", p);
            List<ParameterBag> rPrimeList = new();
            outputParameters.AddOutput("*r_prime", rPrimeList);
            if (p < 0.05)
                rPrimeList.Add(new ParameterBag());

            return new StepOutput(outputParameters);
        }


        /// <summary>
        /// The odds ratio of a 2 by 2 table with its conditional maximum likelihood estimate, exact confidence limits and exact
        /// P values, Fisher and mid-P (ExactBB.OddsRatioCMLE).
        /// </summary>
        /// <param name="host">Where progress is shown.</param>
        /// <param name="parameters">"a", "b": the first row of the table; "c", "d": the second; "gamma": the confidence level,
        /// for which 0.95 is taken if it is not between 0 and 1.</param>
        /// <returns>"tab_a1" to "tab_b2": the table that is analysed; "odds": its odds ratio a d / (b c), missing if both
        /// products are 0; "eor": the conditional maximum likelihood estimate; "pc": the confidence level as a percentage; "llf"
        /// and "ulf": Fisher's limits; "p1f" and "p2f": Fisher's one sided and two sided P value; "llm", "ulm", "p1m" and "p2m":
        /// the same with mid-P; "*note": a row with "note", what the report says if the method gave no figures.</returns>
        public static StepOutput RptExactORCML(IProgressBarHost host, ParameterBag parameters)
        {
            // Gart replaced by CML in May 2001

            double cco = parameters["gamma"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;

            // The table that is analysed has whole numbers: counts that are not are rounded, a half to the even number, as they
            // are for Fisher's exact test
            double a = Math.Round(parameters["a"].AsDouble);
            double b = Math.Round(parameters["b"].AsDouble);
            double c = Math.Round(parameters["c"].AsDouble);
            double d = Math.Round(parameters["d"].AsDouble);

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("tab_a1", a);
            outputParameters.AddOutput("tab_b1", b);
            outputParameters.AddOutput("tab_a2", c);
            outputParameters.AddOutput("tab_b2", d);

            ExactBB.OddsRatioCMLE(host, cco, a, b, c, d, out double eor, out double llf, out double ulf, out double llm, out double ulm, out double p1f, out double p2f, out double p1m, out double p2m, out int ierr);
            double odr = ExactBB.OddsRatio(a, b, c, d);
            outputParameters.AddOutput("odds", odr);

            outputParameters.AddOutput("eor", eor);
            outputParameters.AddOutput("pc", cco * 100);
            outputParameters.AddOutput("llf", llf);
            outputParameters.AddOutput("ulf", ulf);
            outputParameters.AddOutput("p1f", p1f);
            outputParameters.AddOutput("p2f", p2f);
            outputParameters.AddOutput("llm", llm);
            outputParameters.AddOutput("ulm", ulm);
            outputParameters.AddOutput("p1m", p1m);
            outputParameters.AddOutput("p2m", p2m);

            // Why the report has no figures, if it has none
            List<ParameterBag> noteList = new();
            outputParameters.AddOutput("*note", noteList);
            if (ierr != 0)
            {
                ParameterBag noteParameters = new();
                noteList.Add(noteParameters);
                noteParameters.AddOutput("note", ierr == -1
                    ? "The table is too large for the exact method, which considers no more than a million values of the first count."
                    : "The exact method could not be completed for this table.");
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// A rate, the number of events over the time at risk, with the exact confidence limits of a Poisson count over the same
        /// time (Rates.poisson_ci).
        /// </summary>
        /// <param name="parameters">"revents": the number of events, which need not be a whole number; "tar": the time at risk,
        /// which is to be above 0; "cco": the confidence level, for which 0.95 is taken if it is not between 0 and 1.</param>
        /// <returns>"events", "time", "rate"; "pc": the confidence level as a percentage; "from" and "to": the limits of the
        /// rate.</returns>
        public static StepOutput RptRatePoissonCI(ParameterBag parameters)
        {
            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;
            double alpha = 1.0 - cco;

            double revents = parameters["revents"].AsDouble;
            double tar = parameters["tar"].AsDouble;
            if (tar <= 0.0)
                throw new TemplateOperationCancelledException("The time at risk must be greater than zero", "Poisson rate confidence interval");

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("events", revents);
            outputParameters.AddOutput("time", tar);
            outputParameters.AddOutput("rate", revents / tar);

            outputParameters.AddOutput("pc", cco * 100);

            Rates.poisson_ci(alpha, revents, tar, out double xl, out double xu);
            outputParameters.AddOutput("from", xl);
            outputParameters.AddOutput("to", xu);
            return new StepOutput(outputParameters);
        }
    }
}

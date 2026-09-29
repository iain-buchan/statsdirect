using System;
using System.Collections.Generic;

using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using static StatsDirect.Builtins.ExactBB;

namespace StatsDirect.Builtins
{
    public static class Analysis
    {
        /// <summary>
        /// Direct standardization, from the grid of the screen form: the three columns of the grid are the events and the
        /// person-time of each stratum of the index population and the size of the stratum in the reference population.  They
        /// are handed to Rates.DirectStandardization, without labels.
        /// </summary>
        /// <param name="parameters">"data": the grid; "nunit" and "cco": see Rates.DirectStandardization.</param>
        /// <returns>The figures of Rates.DirectStandardization.</returns>
        public static StepOutput RptRateDirectStd(ParameterBag parameters)
        {
            // The screen form gives the three columns in one grid: events, person-time, reference group size
            DataFrame datFrame = parameters["data"].AsDataFrame;
            if (datFrame.VariableCount < 3)
                throw new InvalidDataException("Enter the events, the person-time and the reference group size for every stratum");
            DoubleVariable datV0 = (DoubleVariable) datFrame.Variables[0];
            DoubleVariable datV1 = (DoubleVariable) datFrame.Variables[1];
            DoubleVariable datV2 = (DoubleVariable) datFrame.Variables[2];
            int rows = datFrame.MaxRows;
            double[] idxy = new double[rows + 1];
            double[] idxn = new double[rows + 1];
            double[] refn = new double[rows + 1];
            for (int j = 1; j <= rows; j++)
            {
                idxy[j] = datV0.Data[j - 1];
                idxn[j] = datV1.Data[j - 1];
                refn[j] = datV2.Data[j - 1];
            }
            return Rates.DirectStandardization(parameters, idxy, idxn, refn, null, rows);
        }

        /// <summary>
        /// Two crude rates compared: a cases in the person-time pt1 of an exposed group against b cases in the person-time pt2
        /// of a group that is not exposed, by the difference and by the ratio of the two rates.
        /// </summary>
        /// <remarks>
        /// The rates are a / pt1 and b / pt2.  The numbers of cases are taken to be Poisson counts, so that a rate has the
        /// variance of its count over the person-time squared: the standard error of the difference of the rates is the square
        /// root of a / pt1^2 + b / pt2^2, and the limits of the difference are the difference plus and minus the normal deviate
        /// of the confidence level times it.
        /// With the total of the cases given, m = a + b, the cases of the first group are a binomial count of m, and if the two
        /// rates are the same the probability that a case is of the first group is pt1 / pt, where pt = pt1 + pt2.  Chi-square
        /// is the square of a less what is expected of it, m pt1 / pt, over its variance, m pt1 pt2 / pt^2, and has 1 degree
        /// of freedom.
        /// The ratio of the rates has exact limits: the lower limit is the ratio with which a or more of the m cases have the
        /// probability (1 - gamma) / 2 of being of the first group, and the upper limit the ratio with which a or fewer have
        /// it; they are from quantiles of the F distribution.  With no cases in the first group the lower limit is 0, and
        /// with none in the second the ratio and the upper limit are infinite.
        /// The conditional analysis (ExactBB.Exact22K) has the same limits by another method, and the limits and P values in
        /// which the number of cases observed has half its probability (mid-P).
        /// </remarks>
        /// <param name="host">What shows the progress of the conditional analysis, and is told if it fails.</param>
        /// <param name="parameters">"a", "b": the cases of the two groups; "pt1", "pt2": their person-times; "gamma": the
        /// confidence level, for which 0.95 is taken if it is not between 0 and 1; "do_cml": whether the conditional analysis
        /// is made, which needs whole numbers of cases.</param>
        /// <returns>"a_out", "b_out", "m", "pt1_out", "pt2_out", "pt": the numbers and their totals; "ir1", "ir2": the rates;
        /// "ird", "ird_from", "ird_to": their difference and its limits; "pc": the confidence level as a percentage; "xmh" and
        /// "p": chi-square and its P value; "irr", "irr_from", "irr_to": the ratio of the rates and its limits; "*exact": a row
        /// of the conditional analysis, if it is made, with "eor", the estimate, "llf" and "ulf", the exact limits, "p1f" and
        /// "p2f", the one sided and the two sided exact P value, and "llm", "ulm", "p1m" and "p2m", the same with
        /// mid-P.</returns>
        public static StepOutput RptRateCompareTwo(ITemplateHost host, ParameterBag parameters)
        {
            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            double pt1 = parameters["pt1"].AsDouble;
            double pt2 = parameters["pt2"].AsDouble;
            bool doCml = parameters["do_cml"].AsBoolean;
            double pt = pt1 + pt2;
            double m = a + b;
            double gamma = parameters["gamma"].AsDouble;
            if (gamma <= 0.0 || gamma >= 1.0)
                gamma = 0.95;

            if (a < 0.0 || b < 0.0)
                throw new InvalidDataException("The numbers of cases must not be negative");
            if (pt1 <= 0.0 || pt2 <= 0.0)
                throw new InvalidDataException("Person-time must be greater than zero");
            if (a + b <= 0.0)
                throw new InvalidDataException("There are no cases in either group: the two rates cannot be compared");
            // The conditional analysis is of whole numbers of cases; a fraction would be rounded there and the two parts of the report
            // would then describe different data
            if (doCml && (a != Math.Floor(a) || b != Math.Floor(b)))
                throw new InvalidDataException("The conditional maximum likelihood analysis needs whole numbers of cases: enter whole numbers or leave that analysis unticked");

            double ir1 = a / pt1;
            double ir2 = b / pt2;
            double ird = ir1 - ir2;
            // the cases of the first group, of all the cases, against what the person-times expect of them
            double xmh = (a - m * pt1 / pt) * (a - m * pt1 / pt) / (m * pt1 * pt2 / (pt * pt));
            double pxmh = PDF.chivalp(xmh, 1.0);

            double p = 1.0 - (1.0 - gamma) / 2.0;
            double z = PDF.gauinv(p, out int fault);

            // The standard error of the difference is the square root of the sum of the variances of the two rates, each that of
            // a Poisson count over its person-time; R and SAS give the same limits
            double seIrd = Math.Sqrt(a / (pt1 * pt1) + b / (pt2 * pt2));
            double ird1 = ird - z * seIrd;
            double ird2 = ird + z * seIrd;

            // the ratio and its exact limits; PDF.ffromp is given the degrees of freedom of the denominator first
            double irr0, irr1, irr2;
            if (a == 0.0)
            {
                irr1 = 0.0;
            }
            else
            {
                double f = PDF.ffromp(2.0 * a, 2.0 * (b + 1), 1.0 - p);
                irr1 = pt2 / pt1 * (a / (b + 1.0)) * (1.0 / f);
            }
            if (b == 0.0)
            {
                irr0 = Constant.MISSING;
                irr2 = Constant.MISSING;
            }
            else
            {
                irr0 = a / pt1 / (b / pt2);
                double f = PDF.ffromp(2.0 * b, 2.0 * (a + 1), 1.0 - p);
                irr2 = pt2 / pt1 * ((a + 1.0) / b) * f;
            }

            if (fault != 0)
            {
                // TODO: Error
                return null;
            }

            ParameterBag outputParameters = new();

            outputParameters.AddOutput("a_out", a);
            outputParameters.AddOutput("b_out", b);
            outputParameters.AddOutput("m", m);
            outputParameters.AddOutput("pt1_out", pt1);
            outputParameters.AddOutput("pt2_out", pt2);
            outputParameters.AddOutput("pt", pt);

            outputParameters.AddOutput("ir1", ir1);
            outputParameters.AddOutput("ir2", ir2);

            outputParameters.AddOutput("ird", ird);
            outputParameters.AddOutput("pc", gamma * 100);
            outputParameters.AddOutput("ird_from", ird1);
            outputParameters.AddOutput("ird_to", ird2);

            outputParameters.AddOutput("xmh", xmh);
            outputParameters.AddOutput("p", pxmh);

            outputParameters.AddOutput("irr", irr0 == Constant.MISSING ? double.PositiveInfinity : irr0);
            outputParameters.AddOutput("irr_from", irr1 == Constant.MISSING ? double.NegativeInfinity : irr1);
            outputParameters.AddOutput("irr_to", irr2 == Constant.MISSING ? double.PositiveInfinity : irr2);

            if (doCml)
            {
                Rec2X2[] tabl = new Rec2X2[1];
                tabl[0].Freq = 1;
                tabl[0].A = a;
                tabl[0].M1 = b + a;
                tabl[0].N1 = pt1;
                tabl[0].N0 = pt2;
                tabl[0].IsInformative = a * pt1 != 0 || b * pt2 != 0;
                bool useLogScale = false;
                new ExactBB().Exact22K(host, 0, 1, Exact22KDataType.Type3, tabl, gamma, out double eor, out double ulf, out double llf, out double ulm, out double llm, out double p1F, out double p2F, out double p1M, out double p2M, ref useLogScale, out int ierr);
                if (ierr != 0)
                    host.Error(Formatting.ERRCOLON + "Error in calculation", "StatsDirect");

                List<ParameterBag> exactList = new();
                outputParameters.AddOutput("*exact", exactList);
                ParameterBag exactParameters = new();
                exactList.Add(exactParameters);
                exactParameters.AddOutput("eor", eor);
                exactParameters.AddOutput("llf", llf);
                exactParameters.AddOutput("ulf", ulf);
                exactParameters.AddOutput("p1f", p1F);
                exactParameters.AddOutput("p2f", p2F);
                exactParameters.AddOutput("llm", llm);
                exactParameters.AddOutput("ulm", ulm);
                exactParameters.AddOutput("p1m", p1M);
                exactParameters.AddOutput("p2m", p2M);
            }

            return new StepOutput(outputParameters);
        }

        // The exact two sided mid-P value of a 2 by 2 table: with the totals of the table given, the probability of a first count
        // that is no more than the one observed, and of one that is no less, each less half the probability of the count
        // observed; twice the less of the two, or 1 if that is more.  Missing for a table with an empty row or column.
        private static double PropMidPFisher2(int a, int b, int c, int d)
        {
            double p = (double)a + b;
            double q = (double)c + d;
            double r = (double)a + c;
            double s = (double)b + d;
            if (!(p > 0 && q > 0 && r > 0 && s > 0))
                return Constant.MISSING;
            // the first count can be from what the first column has more than the second row, or 0, to the less of the first
            // row and the first column
            long first = (long)Math.Max(0.0, r - q);
            long last = (long)Math.Min(p, r);
            long mode = (long)Math.Floor((p + 1.0) * (r + 1.0) / (p + q + 2.0));
            DiscreteTails.Sum(first, last, mode, a, k => (p - k) * (r - k) / ((k + 1.0) * (q - r + k + 1.0)), k => k * (q - r + k) / ((p - k + 1.0) * (r - k + 1.0)),
                out double lower, out double upper, out double point, out double _);
            // The two sided mid-P is twice the smaller of the two one sided mid-P values (the central convention, as the
            // Fisher's exact test report prints it), each tail's mid-P being its cumulative probability up to and including
            // the observed table less half the probability of that table
            return Math.Min(1.0, 2.0 * (Math.Min(lower, upper) - point / 2.0));
        }

        /// <summary>
        /// The four counts of a table that is typed in, as the analyses of the Clinical Epidemiology menu take them: whole numbers
        /// that are not below 0. A count that is not a whole number is rounded, a half to the even number, as the exact methods
        /// round it, so that every figure of a report is of the one table. Counts that come to more than a whole number of 32
        /// bits holds are refused: the limits of a difference of proportions take whole numbers of 32 bits.
        /// </summary>
        private static void XCounts(ref double a, ref double b, ref double c, ref double d)
        {
            foreach (double count in new[] { a, b, c, d })
            {
                if (count == Constant.MISSING || double.IsNaN(count) || count < 0.0)
                    throw new InvalidDataException("The counts must be numbers that are not below 0.");
            }
            a = Math.Round(a);
            b = Math.Round(b);
            c = Math.Round(c);
            d = Math.Round(d);
            if (a + b + c + d > int.MaxValue)
                throw new InvalidDataException("The counts come to more than 2,147,483,647, which is more than this analysis can take.");
        }

        public static StepOutput RptMiscRetroRisk(IProgressBarHost host, ParameterBag parameters)
        {
            double pe = Constant.MISSING;
            bool peFromControls = false;

            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            double c = parameters["c"].AsDouble;
            double d = parameters["d"].AsDouble;
            XCounts(ref a, ref b, ref c, ref d);
            double m1 = a + b;
            double m2 = c + d;
            double n1 = a + c;
            double n2 = b + d;
            double n = m1 + m2;

            double gamma = parameters["cco"].AsDouble;
            if (gamma <= 0.0 | gamma >= 1.0)
                gamma = 0.95;

            double odr = OddsRatio(a, b, c, d);

            double p = 1.0 - (1.0 - gamma) / 2.0;
            double zp = PDF.gauinv(p, out int fault);
            if (fault != 0)
                return null;

            //  only calculate PAR for ODR > 1 because -ve PAR is meaningless
            double parUl;
            double parLl;
            double par;
            if (odr > 1.0 && !double.IsInfinity(odr))
            {
                if (parameters.ContainsKey("pe") && null != parameters["pe"] && parameters["pe"].HasData)
                    pe = parameters["pe"].AsDouble;
                // Without a population figure, exposure is estimated from the controls, who stand for the population when the
                // outcome is rare. The pooled sample (a + c) / n, used before version 5, moves with the number of controls the
                // investigator chose to sample. With this default PAR = 1 - (b / m1) / (d / m2), the estimator whose
                // variance (Walter) is used below.
                if (pe == Constant.MISSING || pe < 0.0 || pe > 1.0)
                {
                    pe = c / m2;
                    peFromControls = true;
                }
                par = pe * (odr - 1.0) / (1.0 + pe * (odr - 1.0));
                if (peFromControls)
                {
                    double varPar = b * m2 / (d * m1) * (b * m2 / (d * m1)) * (a / (b * m1) + c / (d * m2));
                    parLl = par - zp * Math.Sqrt(varPar);
                    parUl = par + zp * Math.Sqrt(varPar);
                }
                else
                {
                    // An entered exposure is a fixed number, so the only sampling error is the odds ratio's. PAR rises with the
                    // odds ratio, so its limits are those of the (Woolf, logit) interval for the odds ratio carried through the
                    // same formula. Walter's variance belongs to the controls-based estimator above and was wrong here.
                    double seLogOdr = Math.Sqrt(1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d);
                    double odrL = Math.Exp(Math.Log(odr) - zp * seLogOdr);
                    double odrU = Math.Exp(Math.Log(odr) + zp * seLogOdr);
                    parLl = pe * (odrL - 1.0) / (1.0 + pe * (odrL - 1.0));
                    parUl = pe * (odrU - 1.0) / (1.0 + pe * (odrU - 1.0));
                }
            }
            else
            {
                par = Constant.MISSING;
                parLl = Constant.MISSING;
                parUl = Constant.MISSING;
            }

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("aa", a);
            outputParameters.AddOutput("bb", b);
            outputParameters.AddOutput("cc", c);
            outputParameters.AddOutput("dd", d);

            //odr = b * c > 0 ? (a * d) / (b * c) : Constant.MISSING;
            outputParameters.AddOutput("odds", odr);
            bool dofish = true;
            double power = Power.fishpower(1.0 - gamma, a, b, n1, n2, ref dofish);
            outputParameters.AddOutput("pwr", Formatting.pwr(power, 1.0 - gamma));

            List<ParameterBag> powerList = new();
            outputParameters.AddOutput("*power", powerList);
            if (b * c > 0 && a * d > 0)
            {
                ParameterBag powerParameters = new();
                powerList.Add(powerParameters);
                double seodr = Math.Sqrt(1 / a + 1 / b + 1 / c + 1 / d);
                double yodr = Math.Log(odr) - zp * seodr;
                double xodr = Math.Log(odr) + zp * seodr;
                powerParameters.AddOutput("ci", gamma * 100);
                powerParameters.AddOutput("ci_1", Math.Exp(yodr));
                powerParameters.AddOutput("ci_2", Math.Exp(xodr));
            }

            //if ((a * d != 0) || (b * c != 0))
            //{
            //    ExactBB.Rec2X2[] tabl = new ExactBB.Rec2X2[1 + 1 /* VB to C# conversion */];
            //    tabl[1].Freq = 1;
            //    tabl[1].A = a;
            //    tabl[1].M1 = a + b;
            //    tabl[1].N1 = a + c;
            //    tabl[1].N0 = b + d;
            //    tabl[1].Informative = (a * d != 0) | (b * c != 0);
            //    bool useLogScale = false;
            //    int ierr;
            //    new ExactBB().Exact22K(host, 1, 1, tabl, gamma, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
            //}
            //else
            //{
            //    eor = Constant.MISSING;
            //    llf = Constant.MISSING;
            //    ulf = Constant.MISSING;
            //    p1F = Constant.MISSING;
            //    p2F = Constant.MISSING;
            //    p1M = Constant.MISSING;
            //    p2M = Constant.MISSING;
            //}
            OddsRatioCMLE(host, gamma, a, b, c, d, out double eor, out double llf, out double ulf, out double llm, out double ulm, out double p1f, out double p2f, out double p1m, out double p2m, out _);
            outputParameters.AddOutput("eor", eor);
            outputParameters.AddOutput("pc", gamma * 100.0);
            outputParameters.AddOutput("llf", llf);
            outputParameters.AddOutput("ulf", ulf);
            outputParameters.AddOutput("p1f", p1f);
            outputParameters.AddOutput("p2f", p2f);
            outputParameters.AddOutput("llm", llm);
            outputParameters.AddOutput("ulm", ulm);
            outputParameters.AddOutput("p1m", p1m);
            outputParameters.AddOutput("p2m", p2m);

            List<ParameterBag> riskList = new();
            outputParameters.AddOutput("*risk", riskList);
            if (par != Constant.MISSING)
            {
                ParameterBag riskParameters = new();
                riskList.Add(riskParameters);
                riskParameters.AddOutput("pe", pe * 100.0);
                riskParameters.AddOutput("pe_note", peFromControls ? " (exposure among the controls)" : " (as entered)");
                riskParameters.AddOutput("par", par * 100.0);
                riskParameters.AddOutput("from", parLl * 100.0);
                riskParameters.AddOutput("to", parUl * 100.0);
            }
            return new StepOutput(outputParameters);
        }

        private static string XBenHarm(IFormatting host, double x, bool roundup)
        {
            // An undefined NNT has no direction; an infinite one is printed as such on the rounded line too
            if (x == Constant.MISSING || double.IsNaN(x))
                return Formatting.ASTERISK;
            return (roundup && double.IsFinite(x) ? Formatting.RoundUp(Math.Abs(x)) : host.RoundU(Math.Abs(x))) + (x < 0 ? "_harm" : "_benefit");
        }

        /// <summary>
        /// An NNT of the form 1 / (factor * measure): infinite when the denominator is 0, undefined when the relative measure is
        /// missing or infinite (a relative risk with no events, or none, in a group).
        /// </summary>
        private static double XNntFrom(double factor, double measure)
        {
            if (measure == Constant.MISSING || !double.IsFinite(measure))
                return Constant.MISSING;
            double d = factor * measure;
            return d != 0.0 ? 1.0 / d : double.PositiveInfinity;
        }

        private static double XNntFromOddsRatio(double brr, double oddsRatio)
        {
            if (oddsRatio == Constant.MISSING || !double.IsFinite(oddsRatio))
                return Constant.MISSING;
            double d = (1.0 - brr) * brr * (1.0 - oddsRatio);
            return d != 0.0 ? (1.0 - brr * (1.0 - oddsRatio)) / d : double.PositiveInfinity;
        }

        private static void XNnSwap(ref double nnl, ref double nnu)
        {
            if (nnl < 0.0 && nnu < 0.0)
            {
                if (nnl < nnu)
                {
                    double tmp = nnl;
                    nnl = nnu;
                    nnu = tmp;
                }
            }
            else
            {
                if (nnl > nnu)
                {
                    double tmp = nnl;
                    nnl = nnu;
                    nnu = tmp;
                }
            }
        }

        public static StepOutput RptMiscDiagnostic(IProgressBarHost host, ParameterBag parameters)
        {
            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            double c = parameters["c"].AsDouble;
            double d = parameters["d"].AsDouble;
            XCounts(ref a, ref b, ref c, ref d);
            double n = a + b + c + d;
            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;

            if (n <= 0.0)
                throw new InvalidDataException("The table has no subjects.");

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("aa", a);
            outputParameters.AddOutput("bb", b);
            outputParameters.AddOutput("ab", a + b);

            outputParameters.AddOutput("cc", c);
            outputParameters.AddOutput("dd", d);
            outputParameters.AddOutput("cd", c + d);

            outputParameters.AddOutput("ac", a + c);
            outputParameters.AddOutput("bd", b + d);
            outputParameters.AddOutput("tot", n);

            // CI level
            outputParameters.AddOutput("pc", cco * 100.0);

            // prevalence
            double prevel = (a + c) / n;
            outputParameters.AddOutput("prevalence", prevel);

            // Clopper-Pearson CI
            MathDbl.binci(a + c, n, out double pil, out double piu, cco, out string warn);
            outputParameters.AddOutput("prevalence_from", pil);
            outputParameters.AddOutput("prevalence_to", piu);
            outputParameters.AddOutput("prevalence_warn", warn);
            // as percentage
            outputParameters.AddOutput("prevalence_pc", prevel * 100.0);
            outputParameters.AddOutput("prevalence_from_pc", pil != Constant.MISSING ? 100.0 * pil : Constant.MISSING);
            outputParameters.AddOutput("prevalence_to_pc", piu != Constant.MISSING ? 100.0 * piu : Constant.MISSING);

            // ppv
            double ptld;
            double temp2;
            double temp1;
            if (a + b > 0.0)
            {
                ptld = a / (a + b);
                temp1 = ptld * 100.0;
                // the change from the likelihood before the test, in points of a percentage: the difference itself, not that
                // of the two percentages rounded to whole numbers
                temp2 = 100.0 * (ptld - prevel);
            }
            else
            {
                ptld = Constant.MISSING;
                temp1 = Constant.MISSING;
                temp2 = Constant.MISSING;
            }
            outputParameters.AddOutput("likely", ptld);

            // Clopper-Pearson CI
            MathDbl.binci(a, a + b, out pil, out piu, cco, out warn);
            outputParameters.AddOutput("likely_from", pil);
            outputParameters.AddOutput("likely_to", piu);
            outputParameters.AddOutput("likely_warn", warn);
            // as percentage
            outputParameters.AddOutput("likely_pc", temp1);
            outputParameters.AddOutput("likely_from_pc", pil != Constant.MISSING ? 100.0 * pil : Constant.MISSING);
            outputParameters.AddOutput("likely_to_pc", piu != Constant.MISSING ? 100.0 * piu : Constant.MISSING);
            // change
            outputParameters.AddOutput("likely_change", temp2);

            // npv
            double ptlng;
            if (d + c > 0.0)
            {
                ptlng = d / (d + c);
                temp1 = ptlng * 100.0;
                temp2 = 100.0 * (ptlng - (b + d) / n);
            }
            else
            {
                ptlng = Constant.MISSING;
                temp1 = Constant.MISSING;
                temp2 = Constant.MISSING;
            }
            outputParameters.AddOutput("likely_negative", ptlng);
            // Clopper-Pearson CI
            MathDbl.binci(d, d + c, out pil, out piu, cco, out warn);
            outputParameters.AddOutput("likely_negative_from", pil);
            outputParameters.AddOutput("likely_negative_to", piu);
            outputParameters.AddOutput("likely_negative_warn", warn);
            // as percentage
            outputParameters.AddOutput("likely_negative_pc", temp1);
            outputParameters.AddOutput("likely_negative_from_pc", pil != Constant.MISSING ? 100.0 * pil : Constant.MISSING);
            outputParameters.AddOutput("likely_negative_to_pc", piu != Constant.MISSING ? 100.0 * piu : Constant.MISSING);
            // change
            outputParameters.AddOutput("likely_negative_change", temp2);

            // p[dx] despite -ve test
            double ptlnd;
            if (d + c > 0.0)
            {
                ptlnd = 1.0 - d / (d + c);
                temp1 = ptlnd * 100.0;
                temp2 = 100.0 * (ptlnd - prevel);
            }
            else
            {
                ptlnd = Constant.MISSING;
                temp1 = Constant.MISSING;
                temp2 = Constant.MISSING;
            }
            outputParameters.AddOutput("likely_despite", ptlnd);
            // Clopper-Pearson CI
            MathDbl.binci(d, d + c, out pil, out piu, cco, out warn);
            outputParameters.AddOutput("likely_despite_from", Math.Min(1.0 - pil, 1.0 - piu));
            outputParameters.AddOutput("likely_despite_to", Math.Max(1.0 - pil, 1.0 - piu));
            outputParameters.AddOutput("likely_despite_warn", warn);
            // as percentage
            outputParameters.AddOutput("likely_despite_pc", temp1);
            pil = pil != Constant.MISSING ? 100.0 * (1.0 - pil) : Constant.MISSING;
            piu = piu != Constant.MISSING ? 100.0 * (1.0 - piu) : Constant.MISSING;
            outputParameters.AddOutput("likely_despite_from_pc", Math.Min(pil, piu));
            outputParameters.AddOutput("likely_despite_to_pc", Math.Max(pil, piu));
            // change
            outputParameters.AddOutput("likely_despite_change", temp2);

            // sensitivity
            double sensi;
            if (a + c > 0.0)
            {
                sensi = a / (a + c);
                temp1 = 100.0 * sensi;
            }
            else
            {
                sensi = Constant.MISSING;
                temp1 = Constant.MISSING;
            }
            // Clopper-Pearson CI for sensitivity
            outputParameters.AddOutput("sensitive", sensi);
            MathDbl.binci(a, a + c, out pil, out piu, cco, out warn);
            outputParameters.AddOutput("sensitive_from", pil);
            outputParameters.AddOutput("sensitive_to", piu);
            outputParameters.AddOutput("sensitive_warn", warn);
            // as percentage
            outputParameters.AddOutput("sensitive_pc", temp1);
            outputParameters.AddOutput("sensitive_from_pc", pil != Constant.MISSING ? 100.0 * pil : Constant.MISSING);
            outputParameters.AddOutput("sensitive_to_pc", piu != Constant.MISSING ? 100.0 * piu : Constant.MISSING);

            // specificity
            double speci;
            if (d + b > 0.0)
            {
                speci = d / (d + b);
                temp1 = 100.0 * speci;
            }
            else
            {
                speci = Constant.MISSING;
                temp1 = Constant.MISSING;
            }
            outputParameters.AddOutput("specific", speci);
            // Clopper-Pearson CI for specificity
            MathDbl.binci(d, d + b, out pil, out piu, cco, out warn);
            outputParameters.AddOutput("specific_from", pil);
            outputParameters.AddOutput("specific_to", piu);
            outputParameters.AddOutput("specific_warn", warn);
            // as percentage
            outputParameters.AddOutput("specific_pc", temp1);
            outputParameters.AddOutput("specific_from_pc", pil != Constant.MISSING ? 100.0 * pil : Constant.MISSING);
            outputParameters.AddOutput("specific_to_pc", piu != Constant.MISSING ? 100.0 * piu : Constant.MISSING);

            // + likelihood ratio with CI: 0 when no diseased subject tests positive, infinite when no disease-free subject does
            double lrpos;
            double zc = 1.0 - (1.0 - cco) / 2.0;
            // fault = 0; 
            zc = PDF.gauinv(zc);
            if (b + d > 0.0 && a + c > 0.0 && (a > 0.0 || b > 0.0))
            {
                double abpos = b / (b + d);
                lrpos = b > 0.0 ? sensi / abpos : double.PositiveInfinity;
            }
            else
            {
                lrpos = Constant.MISSING;
            }
            MathDbl.lr_ci(b, a, b + d, a + c, zc, out double thetal, out double thetau);
            outputParameters.AddOutput("lr_pos", lrpos);
            outputParameters.AddOutput("lr_pos_from", thetal);
            outputParameters.AddOutput("lr_pos_to", thetau);

            // - likelihood ratio with CI: 0 when no diseased subject tests negative, infinite when no disease-free subject does
            double lrneg;
            if (b + d > 0.0 && a + c > 0.0 && (c > 0.0 || d > 0.0))
            {
                double presneg = c / (a + c);
                lrneg = d > 0.0 ? presneg / speci : double.PositiveInfinity;
            }
            else
            {
                lrneg = Constant.MISSING;
            }
            MathDbl.lr_ci(d, c, b + d, a + c, zc, out thetal, out thetau);
            outputParameters.AddOutput("lr_neg", lrneg);
            outputParameters.AddOutput("lr_neg_from", thetal);
            outputParameters.AddOutput("lr_neg_to", thetau);

            // diagnostic odds ratio: 0 or infinite with one empty cell, undefined with an empty cell on each diagonal
            double odr = b * c > 0.0 && a * d > 0.0
                ? a * d / (b * c)
                : b * c > 0.0 ? 0.0 : a * d > 0.0 ? double.PositiveInfinity : Constant.MISSING;
            outputParameters.AddOutput("odr", odr);

            OddsRatioCMLE(host, cco, a, b, c, d, out double eor, out double llf, out double ulf, out double _, out double _, out double _, out double _, out double _, out double _, out int _);
            outputParameters.AddOutput("cmle", eor);
            outputParameters.AddOutput("cmle_from", llf);
            outputParameters.AddOutput("cmle_to", ulf);

            return new StepOutput(outputParameters);
        }

        public static StepOutput RptMiscFalseResult(ParameterBag parameters)
        {
            // A sensitivity or false positive rate of exactly 0 or 1 is a legitimate (perfect or useless) test; only both 0 or both 1
            // leave a probability undefined, and that is printed as such
            double pt = parameters["pt"].AsDouble;
            if (pt < 0.0 || pt > 1.0)
                throw new InvalidDataException("Sensitivity must be between 0 and 1");
            double pf = parameters["pf"].AsDouble;
            if (pf < 0.0 || pf > 1.0)
                throw new InvalidDataException("1 - specificity must be between 0 and 1");

            // The case rate is 1 in n, so n below 1 would be a prevalence above 1
            if (parameters["pd"].AsDouble < 1.0)
                throw new InvalidDataException("The population case rate is 1 in n, so n must be at least 1");
            double pd = 1.0 / parameters["pd"].AsDouble;

            ParameterBag outputParameters = new();

            outputParameters.AddOutput("population", pd * 10000);

            double pp = pf * (1 - pd) / (pf + pd * (pt - pf));
            double pn = (1.0 - pt) * pd / (1.0 - pf - pd * (pt - pf));

            outputParameters.AddOutput("sensitive", pt * 100);
            outputParameters.AddOutput("positive", pp);

            outputParameters.AddOutput("specific", (1 - pf) * 100);
            outputParameters.AddOutput("negative", pn);

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Agreement between two raters from the table of their ratings, typed in: row i and column j hold the number of subjects whom
        /// the first rater put in category i and the second in category j.  The report is that of Tables.RptKappa for two raters:
        /// Cohen's kappa, weighted kappa, Scott's pi and Gwet's AC1 (Tables.Kappa), the interval for a 2 by 2 table
        /// (Tables.XKappaCI22), and the tests of Maxwell and of McNemar generalised (Tables.Maxwell).
        /// </summary>
        /// <param name="host">The preferences for the display of numbers.</param>
        /// <param name="parameters">"responsesCrosstab": the table; "ci": the confidence level; "method": the weights of weighted kappa
        /// (1 linear, 2 quadratic, 3 given in "weights", a table laid out as the table of ratings is).</param>
        public static StepOutput RptKappaScreen(IPreferences host, ParameterBag parameters)
        {
            double cco = parameters["ci"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;

            int wtype = Parsing.Cint_Txt(parameters["method"].AsString);
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out int fault);
            if (fault != 0)
                return null;
            DataFrame datFrame = parameters["responsesCrosstab"].AsDataFrame;
            int rows = datFrame.MaxRows;
            int cols = datFrame.VariableCount;
            int g = Math.Max(rows, cols);
            double[,] o = new double[g, g];
            double[,] w = new double[g, g];

            for (int i = 0; i < g; i++)
            {
                for (int j = 0; j < g; j++)
                {
                    o[i, j] = 0.0;
                    w[i, j] = 0.0;
                }
            }

            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    o[i, j] = ((DoubleVariable) datFrame.Variables[j]).Data[i];

            switch (wtype)
            {
                case 3:
                    DataFrame weights = parameters["weights"].AsDataFrame;
                    // column i of the table of weights holds the weights of column i of the table of ratings, a row to a row
                    for (int i = 0; i < weights.VariableCount; i++)
                    {
                        DoubleVariable v = (DoubleVariable) weights.Variables[i];
                        for (int j = 0; j < v.Length; j++)
                        {
                            w[j, i] = v.Data[j];
                            if (w[j, i] == Constant.MISSING)
                                w[j, i] = 0.0;
                        }
                    }
                    break;
                case 2:
                    for (int i = 0; i < g; i++)
                        for (int j = 0; j < g; j++)
                            w[i, j] = 1 - Math.Pow(Convert.ToDouble(i - j) / Convert.ToDouble(g - 1), 2.0);
                    break;
                case 1:
                    for (int i = 0; i < g; i++)
                        for (int j = 0; j < g; j++)
                            w[i, j] = 1 - Convert.ToDouble(Math.Abs(i - j)) / Convert.ToDouble(g - 1);
                    break;
                default:
                    throw new Exception("Unknown weight type");
            }

            Tables.Kappa(o, w, g, out double k, out double sek, out double sekci, out double kcil, out double kciu, out double kw, out double sekw, out double sekwci, out double kwcil, out double kwciu, out double po, out double pe, out double pow, out double pew, cit, out double spe, out double spi, out double gama, out double segama, out double gamacil, out double gamaciu, out double pegama, out bool ierror);
            if (ierror)
                return null;

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("po", po * 100);
            outputParameters.AddOutput("pe", pe * 100);
            outputParameters.AddOutput("kappa", k);
            outputParameters.AddInput("kDouble", k);
            outputParameters.AddOutput("se", sek);
            outputParameters.AddOutput("seci", sekci);
            outputParameters.AddOutput("pc", cco * 100);
            outputParameters.AddOutput("from", kcil);
            outputParameters.AddOutput("to", kciu);
            double z = sek != 0.0 ? k / sek : Constant.MISSING;
            outputParameters.AddOutput("z", z);
            outputParameters.AddOutput("p", z != Constant.MISSING ? 1.0 - PDF.alnorm(z) : Constant.MISSING);
            switch (wtype)
            {
                case 3:
                    outputParameters.AddOutput("methodName", "user defined");
                    break;
                case 2:
                    outputParameters.AddOutput("methodName", "1-[(i-j)/(k-1)]\u00b2");
                    break;
                case 1:
                    outputParameters.AddOutput("methodName", "1-abs(i-j)/(k-1)");
                    break;
            }

            List<ParameterBag> weightsList = new();
            outputParameters.AddOutput("*weights", weightsList);
            for (int i = 1; i <= g; i++)
            {
                ParameterBag weightsParameters = new();
                weightsList.Add(weightsParameters);
                List<ParameterBag> totList = new();
                weightsParameters.AddOutput("*tot", totList);
                for (int j = 1; j <= g; j++)
                {
                    ParameterBag totParameters = new();
                    totList.Add(totParameters);
                    totParameters.AddOutput("tot", Formatting.XRound(w[i - 1, j - 1], host.Preferences.PDecimalPlaces));
                }
            }
            outputParameters.AddOutput("pow", pow * 100);
            outputParameters.AddOutput("pew", pew * 100);
            outputParameters.AddOutput("kappaw", kw);
            outputParameters.AddInput("kwDouble", kw);
            outputParameters.AddOutput("sekw", sekw);
            outputParameters.AddOutput("sekwci", sekwci);
            outputParameters.AddOutput("pcw", cco * 100);
            outputParameters.AddOutput("fromw", kwcil);
            outputParameters.AddOutput("tow", kwciu);
            double zw = sekw != 0.0 ? kw / sekw : Constant.MISSING;
            outputParameters.AddOutput("zw", zw);
            outputParameters.AddOutput("pw", zw != Constant.MISSING ? 1.0 - PDF.alnorm(zw) : Constant.MISSING);

            outputParameters.AddOutput("spe", spe * 100);
            outputParameters.AddOutput("spi", spi);
            List<ParameterBag> deciList = new();
            outputParameters.AddOutput("*deci", deciList);
            if (g == 2)
            {
                Tables.XKappaCI22(Convert.ToInt32(o[0, 0]), Convert.ToInt32(o[0, 1] + o[1, 0]), Convert.ToInt32(o[1, 1]), cit, out double _, out double lwr, out double upr, out fault);
                if (fault == 0)
                {
                    ParameterBag deciParameters = new();
                    deciList.Add(deciParameters);
                    deciParameters.AddOutput("pc", cco * 100);
                    deciParameters.AddOutput("lwr", lwr);
                    deciParameters.AddOutput("upr", upr);
                }
            }

            // Maxwell's test
            Tables.Maxwell(o, g, out double x2, out int dfMaxwell, out double x2M, out int dfm);
            if (x2 == Constant.MISSING)
            {
                outputParameters.AddOutput("x2", Constant.MISSING);
                outputParameters.AddOutput("df", Constant.MISSING);
                outputParameters.AddOutput("pmaxwell", Constant.MISSING);
            }
            else
            {
                outputParameters.AddOutput("x2", x2);
                outputParameters.AddOutput("df", Convert.ToDouble(dfMaxwell));
                outputParameters.AddOutput("pmaxwell", PDF.chivalp(x2, dfMaxwell));
            }
            // general McNemar
            if (x2M == Constant.MISSING)
            {
                outputParameters.AddOutput("x2m", Constant.MISSING);
                outputParameters.AddOutput("dfmcnemar", Constant.MISSING);
                outputParameters.AddOutput("pmcnemar", Constant.MISSING);
            }
            else
            {
                outputParameters.AddOutput("x2m", x2M);
                outputParameters.AddOutput("dfmcnemar", dfm);
                outputParameters.AddOutput("pmcnemar", PDF.chivalp(x2M, dfm));
            }

            // Gwet's AC1
            outputParameters.AddOutput("gama", gama);
            outputParameters.AddOutput("gamapc", Math.Round(po * 100.0, 2));
            outputParameters.AddOutput("segama", segama);
            outputParameters.AddOutput("gamacil", gamacil);
            outputParameters.AddOutput("gamaciu", gamaciu);
            outputParameters.AddOutput("pegama", pegama);
            outputParameters.AddOutput("pegamapc", Math.Round(pegama * 100.0, 2));

            return new StepOutput(outputParameters);
        }

        public static StepOutput RptMiscLikely(ParameterBag parameters)
        {
            DataFrame datFrame = parameters["data"].AsDataFrame;
            DoubleVariable datV0 = (DoubleVariable) datFrame.Variables[0];
            DoubleVariable datV1 = (DoubleVariable) datFrame.Variables[1];
            int rows = datFrame.MaxRows;

            double[] c1 = new double[rows + 1];
            double[] c2 = new double[rows + 1];

            double c1Tot = 0;
            double c2Tot = 0;
            for (int i = 1; i <= rows; i++)
            {
                c1[i] = datV0.Data[i - 1];
                c2[i] = datV1.Data[i - 1];
                if (c1[i] < 0 || c2[i] < 0 || c1[i] == Constant.MISSING || c2[i] == Constant.MISSING)
                    throw new InvalidDataException("All values must be >= 0");
                // a count that is not a whole number is rounded, a half to the even number
                c1[i] = Math.Round(c1[i]);
                c2[i] = Math.Round(c2[i]);

                c1Tot += c1[i];
                c2Tot += c2[i];
            }

            if (c1Tot <= 0 || c2Tot <= 0)
                throw new InvalidDataException("Total of -feature and total of +feature must both be > 0");

            double zl = parameters["z1"].AsDouble;
            if (zl <= 0.0 || zl >= 1.0)
                zl = 0.95;
            double zc = 1.0 - (1.0 - zl) / 2.0;
            zc = PDF.gauinv(zc);

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("pc", 100 * zl);

            List<ParameterBag> rowList = new();
            outputParameters.AddOutput("*row", rowList);
            for (int i = 1; i <= rows; i++)
            {
                ParameterBag rowParameters = new();
                rowList.Add(rowParameters);
                rowParameters.AddOutput("result", i);
                rowParameters.AddOutput("plusfeature", c1[i]);
                rowParameters.AddOutput("minusfeature", c2[i]);

                double li;
                // without end if no subject without the feature has the result, and none if no subject at all has it
                if (c2[i] <= 0.0)
                    li = c1[i] > 0.0 ? double.PositiveInfinity : Constant.MISSING;
                else
                    li = c1[i] / c1Tot / (c2[i] / c2Tot);

                rowParameters.AddOutput("likely", li);

                MathDbl.lr_ci(c2[i], c1[i], c2Tot, c1Tot, zc, out double thetal, out double thetau);
                rowParameters.AddOutput("from", thetal);
                rowParameters.AddOutput("to", thetau);
            }
            return new StepOutput(outputParameters);
        }

        public static StepOutput RptMiscNumberNeededToTreat(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double tmp;
            double nt = parameters["nt"].AsDouble;
            double xt = parameters["xt"].AsDouble;
            double nc = parameters["nc"].AsDouble;
            double xc = parameters["xc"].AsDouble;
            XCounts(ref nt, ref xt, ref nc, ref xc);

            if (nt < xt)
            {
                tmp = xt;
                xt = nt;
                nt = tmp;
            }
            if (nc < xc)
            {
                tmp = xc;
                xc = nc;
                nc = tmp;
            }

            double t1 = xt;
            double t2 = nt - xt;
            double t3 = xc;
            double t4 = nc - xc;

            if (nc < 1.0 || nt < 1.0)
                throw new InvalidDataException("There must be at least one treated subject and one control.");

            double zl = parameters["cco"].AsDouble;
            if (zl <= 0.0 || zl >= 1.0)
                zl = 0.95;

            double zc = 1.0 - (1.0 - zl) / 2.0;
            zc = PDF.gauinv(zc);
            if (xc > nc)
            {
                tmp = xc;
                xc = nc;
                nc = tmp;
                parameters["nc"] = FilledParameterFactory.Input(nc);
                parameters["xc"] = FilledParameterFactory.Input(xc);
            }
            double pc = xc / nc;
            if (xt > nt)
            {
                tmp = xt;
                xt = nt;
                nt = tmp;
                parameters["nt"] = FilledParameterFactory.Input(nt);
                parameters["xt"] = FilledParameterFactory.Input(xt);
            }
            double pt = xt / nt;

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("pc", 100.0 * zl);

            outputParameters.AddOutput("ce", xc.ToString() + "/" + nc.ToString() + " = " + host.RoundU(pc));
            MathDbl.binci(xc, nc, out double cl, out double cu, zl, out string warn);
            outputParameters.AddOutput("ce_from", cl);
            outputParameters.AddOutput("ce_to", host.RoundU(cu) + warn);
            outputParameters.AddOutput("te", xt.ToString() + "/" + nt.ToString() + " = " + host.RoundU(pt));
            MathDbl.binci(xt, nt, out cl, out cu, zl, out warn);
            outputParameters.AddOutput("te_from", cl);
            outputParameters.AddOutput("te_to", host.RoundU(cu) + warn);
            MathDbl.lr_ci(xc, xt, nc, nt, zc, out double rrel, out double rreu);
            if (rrel > rreu)
            {
                tmp = rrel;
                rrel = rreu;
                rreu = tmp;
            }
            // Infinite with events among the treated only; undefined (0 / 0) with none in either group
            double rre = pc != 0 ? pt / pc : pt != 0 ? double.PositiveInfinity : Constant.MISSING;
            outputParameters.AddOutput("rre", rre);
            outputParameters.AddOutput("rre_from", rrel);
            outputParameters.AddOutput("rre_to", rreu);

            outputParameters.AddOutput("cne", (nc - xc).ToString() + "/" + nc.ToString() + " = " + host.RoundU(1.0 - pc));
            MathDbl.binci(nc - xc, nc, out cl, out cu, zl, out warn);
            outputParameters.AddOutput("cne_from", cl);
            outputParameters.AddOutput("cne_to", host.RoundU(cu) + warn);
            outputParameters.AddOutput("tne", (nt - xt).ToString() + "/" + nt.ToString() + " = " + host.RoundU(1.0 - pt));
            MathDbl.binci(nt - xt, nt, out cl, out cu, zl, out warn);
            outputParameters.AddOutput("tne_from", cl);
            outputParameters.AddOutput("tne_to", host.RoundU(cu) + warn);
            MathDbl.lr_ci(nc - xc, nt - xt, nc, nt, zc, out double rrnel, out double rrneu);
            if (rrnel > rrneu)
            {
                tmp = rrnel;
                rrnel = rrneu;
                rrneu = tmp;
            }
            double rrne = pc != 1.0 ? (1.0 - pt) / (1.0 - pc) : pt != 1.0 ? double.PositiveInfinity : Constant.MISSING;
            outputParameters.AddOutput("rrne", rrne);
            outputParameters.AddOutput("rrne_from", rrnel);
            outputParameters.AddOutput("rrne_to", rrneu);

            //ExactBB.Rec2X2[] tabl = new ExactBB.Rec2X2[2];
            //tabl[1].Freq = 1;
            //tabl[1].A = t1;
            //tabl[1].M1 = t1 + t2;
            //tabl[1].N1 = t1 + t3;
            //tabl[1].N0 = t2 + t4;
            //tabl[1].Informative = (t1 * t4 != 0) || (t2 * t3 != 0);
            //bool useLogScale = false;
            //new ExactBB().Exact22K(host,1, 1, tabl, zl, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
            // 0 / 0 (no events in either group, or every subject with the event) has no odds ratio
            double oor = t1 * t4 == 0.0 && t2 * t3 == 0.0 ? Constant.MISSING : OddsRatio(t1, t2, t3, t4);
            OddsRatioCMLE(host, zl, t1, t2, t3, t4, out double _, out double llf, out double ulf, out double _, out double _, out double _, out double _, out double _, out double _, out int _);
            //if (ierr != 0)
            //    eor = Constant.MISSING;
            //double oor = t2 * t3 == 0.0 ? Constant.MISSING : (t1 * t4) / (t2 * t3);
            outputParameters.AddOutput("oor", oor);
            outputParameters.AddOutput("oor_from", llf);
            outputParameters.AddOutput("oor_to", ulf);

            // 1 - the relative risk: minus infinity when only the treated have events, undefined when neither group has any
            double rrr = pc != 0.0 ? (pc - pt) / pc : pt != 0.0 ? double.NegativeInfinity : Constant.MISSING;
            double rrrl = rrel != Constant.MISSING ? 1.0 - rrel : Constant.MISSING;
            double rrru = rreu != Constant.MISSING ? 1.0 - rreu : Constant.MISSING;
            if (rrrl > rrru)
            {
                tmp = rrrl;
                rrrl = rrru;
                rrru = tmp;
            }
            outputParameters.AddOutput("rrr", rrr);
            outputParameters.AddOutput("rrr_from", rrrl);
            outputParameters.AddOutput("rrr_to", rrru);

            double rd = pc - pt;
            MathDbl.uppci(Convert.ToInt32(xc), Convert.ToInt32(nc), Convert.ToInt32(xt), Convert.ToInt32(nt), out cl, out cu, zc, 100.0 * zl);
            double rdl = cl;
            double rdu = cu;
            if (rdl > rdu)
            {
                tmp = rdl;
                rdl = rdu;
                rdu = tmp;
            }
            outputParameters.AddOutput("rd", rd);
            outputParameters.AddOutput("rd_from", rdl);
            outputParameters.AddOutput("rd_to", rdu);

            // NNT_risk difference
            double nnt = rd != 0.0 ? 1.0 / rd : double.PositiveInfinity;
            double nnl = rdl != 0.0 ? 1.0 / rdl : double.PositiveInfinity;
            double nnu = rdu != 0.0 ? 1.0 / rdu : double.PositiveInfinity;

            // Jan 02 change to benefit/harm notation
            // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
            XNnSwap(ref nnl, ref nnu);
            outputParameters.AddOutput("treat", XBenHarm(host, nnt, false));
            outputParameters.AddOutput("treat_from", XBenHarm(host, nnl, false));
            outputParameters.AddOutput("treat_to", XBenHarm(host, nnu, false));
            outputParameters.AddOutput("treat_round", XBenHarm(host, nnt, true));
            outputParameters.AddOutput("treat_round_from", XBenHarm(host, nnl, true));
            outputParameters.AddOutput("treat_round_to", XBenHarm(host, nnu, true));
            // <--

            // **************************************************************************************
            // substitute external baseline event rate (brr) for control event rate (pc) if brr given
            bool hasBrr = parameters.ContainsKey("brr") && parameters["brr"] != null;
            List<ParameterBag> adjustedList = new();
            outputParameters.AddOutput("*adjusted", adjustedList);
            if (hasBrr)
            {
                ParameterBag adjustedParameters = new();
                adjustedList.Add(adjustedParameters);
                double brr = parameters["brr"].AsDouble;
                string brt;
                // An expected risk of 1 to 100 is read as a percentage (1 is 1%, not certainty), and the report says so
                if (brr < 0.0 | brr >= 1.0)
                {
                    if (brr >= 1.0 & brr <= 100.0)
                    {
                        brr /= 100.0;
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
                    brt = string.Empty;
                }
                adjustedParameters.AddOutput("type", brt);
                adjustedParameters.AddOutput("brr", 100.0 * brr);

                adjustedParameters.AddOutput("pc", 100.0 * zl);

                // NNT_risk difference
                nnt = rd != 0.0 ? 1.0 / rd : double.PositiveInfinity;
                nnl = rdl != 0.0 ? 1.0 / rdl : double.PositiveInfinity;
                nnu = rdu != 0.0 ? 1.0 / rdu : double.PositiveInfinity;
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                XNnSwap(ref nnl, ref nnu);
                adjustedParameters.AddOutput("rd_treat", XBenHarm(host, nnt, false));
                adjustedParameters.AddOutput("rd_treat_from", XBenHarm(host, nnl, false));
                adjustedParameters.AddOutput("rd_treat_to", XBenHarm(host, nnu, false));
                adjustedParameters.AddOutput("rd_treat_round", XBenHarm(host, nnt, true));
                adjustedParameters.AddOutput("rd_treat_round_from", XBenHarm(host, nnl, true));
                adjustedParameters.AddOutput("rd_treat_round_to", XBenHarm(host, nnu, true));
                // <--

                // NNT_risk ratio of event
                nnt = XNntFrom(brr, rrr);
                nnl = XNntFrom(brr, rrrl);
                nnu = XNntFrom(brr, rrru);
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                XNnSwap(ref nnl, ref nnu);
                adjustedParameters.AddOutput("rr_treat", XBenHarm(host, nnt, false));
                adjustedParameters.AddOutput("rr_treat_from", XBenHarm(host, nnl, false));
                adjustedParameters.AddOutput("rr_treat_to", XBenHarm(host, nnu, false));
                adjustedParameters.AddOutput("rr_treat_round", XBenHarm(host, nnt, true));
                adjustedParameters.AddOutput("rr_treat_round_from", XBenHarm(host, nnl, true));
                adjustedParameters.AddOutput("rr_treat_round_to", XBenHarm(host, nnu, true));
                // <--

                // NNT_risk ratio of no event
                // Sally Hollis pointed out not (1-brr) * (1-rrr) as given by Jon Deeks
                nnt = XNntFrom(1.0 - brr, rrne == Constant.MISSING ? rrne : rrne - 1.0);
                nnl = XNntFrom(1.0 - brr, rrnel == Constant.MISSING ? rrnel : rrnel - 1.0);
                nnu = XNntFrom(1.0 - brr, rrneu == Constant.MISSING ? rrneu : rrneu - 1.0);
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                XNnSwap(ref nnl, ref nnu);
                adjustedParameters.AddOutput("rrn_treat", XBenHarm(host, nnt, false));
                adjustedParameters.AddOutput("rrn_treat_from", XBenHarm(host, nnl, false));
                adjustedParameters.AddOutput("rrn_treat_to", XBenHarm(host, nnu, false));
                adjustedParameters.AddOutput("rrn_treat_round", XBenHarm(host, nnt, true));
                adjustedParameters.AddOutput("rrn_treat_round_from", XBenHarm(host, nnl, true));
                adjustedParameters.AddOutput("rrn_treat_round_to", XBenHarm(host, nnu, true));
                // <--

                // NNT_odds ratio: undefined when the odds ratio, or the limit, is missing or infinite
                nnt = XNntFromOddsRatio(brr, oor);
                nnl = XNntFromOddsRatio(brr, llf);
                nnu = XNntFromOddsRatio(brr, ulf);
                // Jan 02 change to benefit/harm notation
                // Altman DG. Confidence intervals for the number needed to treat. BMJ 1998;317:1309-12
                XNnSwap(ref nnl, ref nnu);
                adjustedParameters.AddOutput("or_treat", XBenHarm(host, nnt, false));
                adjustedParameters.AddOutput("or_treat_from", XBenHarm(host, nnl, false));
                adjustedParameters.AddOutput("or_treat_to", XBenHarm(host, nnu, false));
                adjustedParameters.AddOutput("or_treat_round", XBenHarm(host, nnt, true));
                adjustedParameters.AddOutput("or_treat_round_from", XBenHarm(host, nnl, true));
                adjustedParameters.AddOutput("or_treat_round_to", XBenHarm(host, nnu, true));
                // <--
            }

            return new StepOutput(outputParameters);
        }

        public static StepOutput RptMiscRelRisk(ParameterBag parameters)
        {
            double pe = Constant.MISSING;

            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            double c = parameters["c"].AsDouble;
            double d = parameters["d"].AsDouble;
            XCounts(ref a, ref b, ref c, ref d);
            double m1 = a + b;
            double m2 = c + d;
            double n1 = a + c;
            double n2 = b + d;
            double n = m1 + m2;

            double gamma = parameters["cco"].AsDouble;
            if (gamma <= 0.0 || gamma >= 1.0)
                gamma = 0.95;

            if (a + c <= 0 || b + d <= 0)
                throw new InvalidDataException("Relative risk can not be calculated for these data: there must be subjects who were exposed and subjects who were not.");
            if (a <= 0 && b <= 0)
                throw new InvalidDataException("Relative risk can not be calculated for these data: no subject has the outcome.");

            // with no outcome among those who were not exposed the ratio is without end: it has a lower limit, and no
            // population attributable risk is given
            double rr = b > 0 ? a / (a + c) / (b / (b + d)) : double.PositiveInfinity;
            double p = 1.0 - (1.0 - gamma) / 2.0;
            double zp = PDF.gauinv(p, out int fault);

            double p1 = a / n1;
            double p2 = b / n2;
            double dif = p1 - p2;
            MathDbl.uppci(Convert.ToInt32(a), Convert.ToInt32(n1), Convert.ToInt32(b), Convert.ToInt32(n2), out double difLl, out double difUl, zp, 100.0 * (1.0 - gamma));

            bool dofish = true;
            double power = Power.fishpower(1.0 - gamma, a, b, n1, n2, ref dofish);

            MathDbl.lr_ci(b, a, b + d, a + c, zp, out double ll, out double ul);

            //  only calculate PAR for RR > 1 because -ve PAR is meaningless
            double parUl;
            double parLl;
            double par;
            bool peEntered = false;
            if (rr > 1.0 && !double.IsInfinity(rr))
            {
                if (parameters.ContainsKey("pe") && null != parameters["pe"] && parameters["pe"].HasData)
                    pe = parameters["pe"].AsDouble;
                if (pe == Constant.MISSING || pe < 0.0 || pe > 1.0)
                    pe = (a + c) / n;
                else
                    peEntered = true;
                par = pe * (rr - 1.0) / (1.0 + pe * (rr - 1.0));
                if (peEntered)
                {
                    // With the population exposure given, the relative risk is the only estimate in the PAR, so its Koopman limits
                    // carry through the PAR formula; Walter's variance below is that of the PAR estimated from the cohort, whose
                    // exposure is then part of the sampling error
                    parLl = pe * (ll - 1.0) / (1.0 + pe * (ll - 1.0));
                    parUl = double.IsInfinity(ul) ? (pe > 0.0 ? 1.0 : 0.0) : pe * (ul - 1.0) / (1.0 + pe * (ul - 1.0));
                }
                else
                {
                    double varPar = b * n / (Math.Pow(m1, 3.0) * Math.Pow(n2, 3.0)) * (a * d * (n - b) + b * b * c);
                    parLl = par - zp * Math.Sqrt(varPar);
                    parUl = par + zp * Math.Sqrt(varPar);
                }
            }
            else
            {
                par = Constant.MISSING;
                parLl = Constant.MISSING;
                parUl = Constant.MISSING;
            }

            if (fault == 0)
            {
                ParameterBag outputParameters = new();
                outputParameters.AddOutput("a_out", a);
                outputParameters.AddOutput("b_out", b);
                outputParameters.AddOutput("c_out", c);
                outputParameters.AddOutput("d_out", d);

                outputParameters.AddOutput("ratio", rr);
                outputParameters.AddOutput("pc", gamma * 100);
                outputParameters.AddOutput("koopman_from", ll);
                outputParameters.AddOutput("koopman_to", ul);
                outputParameters.AddOutput("pwr", Formatting.pwr(power, 1.0 - gamma));

                outputParameters.AddOutput("dif", dif);
                outputParameters.AddOutput("miettinen_from", difLl);
                outputParameters.AddOutput("miettinen_to", difUl);

                List<ParameterBag> exposureList = new();
                outputParameters.AddOutput("*exposure", exposureList);
                if (par != Constant.MISSING)
                {
                    ParameterBag exposureParameters = new();
                    exposureList.Add(exposureParameters);
                    exposureParameters.AddOutput("pe", pe * 100.0);
                    exposureParameters.AddOutput("pe_note", peEntered ? " (as entered)" : string.Empty);
                    exposureParameters.AddOutput("par", par * 100.0);
                    exposureParameters.AddOutput("method", peEntered ? "from the Koopman limits of the risk ratio" : "Walter");
                    exposureParameters.AddOutput("walter_from", parLl * 100.0);
                    exposureParameters.AddOutput("walter_to", parUl * 100.0);
                }

                return new StepOutput(outputParameters);
            }
            return null;
        }

        /// <summary>
        /// Paired proportions: each of n pairs has two responses, and the proportion of the first responses is compared with
        /// that of the second.
        /// </summary>
        /// <remarks>
        /// Of the pairs, r respond both times, s the first time only and t the second time only.  The two proportions are
        /// (r + s) / n and (r + t) / n, and their difference is (s - t) / n: the pairs that respond alike have no part in it.
        /// The exact test is of the pairs that differ.  If the two proportions are the same in the population a pair that
        /// differs is as likely to differ one way as the other, and the less of s and t is a binomial count of s + t trials
        /// with the probability of a half.  The one sided P value is the probability of a count that is no more than the one
        /// observed, and the two sided P value is twice that, or 1 if that is more.  For the mid-P values half the probability
        /// of the count observed is taken from the one sided P value first.
        /// The confidence limits of the difference are those of MathDbl.Wilson.
        /// Numbers that are not whole numbers are rounded, a half to the even number.
        /// </remarks>
        /// <param name="parameters">"n": the number of pairs; "r": the number that respond both times; "s": the first time
        /// only; "t": the second time only; "cco": the confidence level, for which 0.95 is taken if it is not between 0 and
        /// 1.</param>
        /// <returns>"n_out", "r_out", "s_out", "t_out": the numbers, rounded; "prop_1", "prop_2", "prop_diff": the two
        /// proportions and their difference; "*exact": a row with "cum_1" and "cum_2", the one sided and the two sided P
        /// value, and "cum_1_mid" and "cum_2_mid", the mid-P values; "qcl": the name of the method of the limits; "pc": the
        /// confidence level as a percentage; "lower" and "upper": the limits of the difference.</returns>
        public static StepOutput RptPropPairs(ParameterBag parameters)
        {
            // numbers that are not whole numbers are rounded, a half to the even number
            double n = Math.Round(parameters["n"].AsDouble);

            if (n <= 0)
                throw new InvalidDataException("Total number in study must be at least 1");
            double r = Math.Round(parameters["r"].AsDouble);
            double s = Math.Round(parameters["s"].AsDouble);
            double t = Math.Round(parameters["t"].AsDouble);

            if (r < 0 || s < 0 || t < 0)
                throw new InvalidDataException("A number responding can not be below 0");
            if (n < r + s + t)
                throw new InvalidDataException("Total number in study must be at least the sum of the number responding in both categories + first category only + second category only");

            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;
            double pt = (1.0 - cco) / 2.0;

            double cit = PDF.gauinv(cco + pt, out int ifault);
            if (ifault != 0)
                return null;

            double p1 = (r + s) / n;
            double p2 = (r + t) / n;
            double p3 = (s - t) / n;

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("n_out", n);
            outputParameters.AddOutput("r_out", r);
            outputParameters.AddOutput("s_out", s);
            outputParameters.AddOutput("t_out", t);
            outputParameters.AddOutput("prop_1", p1);
            outputParameters.AddOutput("prop_2", p2);
            outputParameters.AddOutput("prop_diff", p3);

            // The exact test is of the pairs that differ: of the s + t of them the less of s and t are one way, and either way
            // is as likely.  It is made for any number of pairs
            double nx = s + t;
            double rx = Math.Min(s, t);
            List<ParameterBag> exactList = new();
            outputParameters.AddOutput("*exact", exactList);
            {
                ParameterBag exactParameters = new();
                exactList.Add(exactParameters);
                PDF.BinomialTails(Convert.ToInt64(nx), 0.5, Convert.ToInt64(rx), out double pl, out double _, out double fl, out double _);
                exactParameters.AddOutput("cum_2", Math.Min(1.0, 2.0 * pl));
                exactParameters.AddOutput("cum_1", pl);
                pl -= fl / 2.0;
                exactParameters.AddOutput("cum_2_mid", Math.Min(1.0, 2.0 * pl));
                exactParameters.AddOutput("cum_1_mid", pl);
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
            // the limits of the difference, from the four numbers of the table of the first response by the second
            int ial = Convert.ToInt32(r);
            int ibl = Convert.ToInt32(s);
            int icl = Convert.ToInt32(t);
            int idl = Convert.ToInt32(n - r - s - t);
            MathDbl.Wilson(ial, ibl, icl, idl, out double cl, out double cu, cit, out bool fault);
            const string q = "Score based (Newcombe)";
            // End If
            double piu;
            double pil;
            if (fault == false)
            {
                pil = cl;
                piu = cu;
            }
            else
            {
                pil = Constant.MISSING;
                piu = Constant.MISSING;
            }

            outputParameters.AddOutput("qcl", q);
            outputParameters.AddOutput("pc", cco * 100);
            outputParameters.AddOutput("lower", pil);
            outputParameters.AddOutput("upper", piu);

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// A proportion: r of n observations have the characteristic.  The proportion has confidence limits, and is compared with
        /// the proportion of a null hypothesis.
        /// </summary>
        /// <remarks>
        /// The exact limits are those of Clopper and Pearson (MathDbl.binci).
        /// The P values are from the binomial distribution of the count of n trials with the null proportion (PDF.BinomialTails),
        /// whatever the size of the sample.  The one sided P value is the less of the probability of a count that is no more
        /// than r and that of a count that is no less than r.  The two sided P value is the sum of the probabilities of the
        /// counts that are no more probable than r.  With a null proportion of a half the distribution is the same each side
        /// of its middle, and the two sided P value is twice the one sided P value, or 1 if that is more; with another null
        /// proportion it is not, as a rule.
        /// The limits of Wilson are the two proportions p that are z of their own standard errors from the proportion observed,
        /// which are the roots of (r / n - p)^2 = z^2 p (1 - p) / n:
        /// (2 r + z^2 -/+ z root(z^2 + 4 r (1 - r / n))) / (2 (n + z^2)), where z is the normal deviate of the confidence level.
        /// The one sided mid-P value is the one sided P value less half the probability of the count r, and the two sided
        /// mid-P value is twice that, or 1 if that is more.
        /// With a null proportion of 0 the count can be nothing but 0, and with one of 1 nothing but n: a P value is then 1 if
        /// that is the count observed and 0 if it is not, and the one sided mid-P value is a half or 0.
        /// Numbers that are not whole numbers are rounded, a half to the even number.
        /// </remarks>
        /// <param name="parameters">"n": the number of observations; "r": the number that have the characteristic; "qpi": the
        /// proportion of the null hypothesis, for which 0 is taken if it is below 0 and 1 if it is above 1; "cco": the
        /// confidence level, for which 0.95 is taken if it is not between 0 and 1.</param>
        /// <returns>"prop": the proportion; "ci_exact": the confidence level as a percentage, "lower_exact" and "upper_exact":
        /// the limits of Clopper and Pearson, and "warn_exact": what the report says after them if the interval is one sided;
        /// "null": the null proportion; "p_1_exact" and "p_2_exact": the one sided and the two sided P value; "ci_approx",
        /// "lower_approx" and "upper_approx": the confidence level and the limits of Wilson; "p_1_approx" and "p_2_approx":
        /// the one sided and the two sided mid-P value; "ap_1_exact", "ap_2_exact", "ap_1_approx" and "ap_2_approx": the name
        /// of the distribution of each P value, which is "Binomial".</returns>
        public static StepOutput RptPropSingle(ParameterBag parameters)
        {
            // counts that are not whole numbers are rounded, a half to the even number
            double n = Math.Round(parameters["n"].AsDouble);
            double r = Math.Round(parameters["r"].AsDouble);
            if (r > n)
                throw new InvalidDataException("The number responding can not be more than the total number of observations");

            if (n <= 0)
                throw new InvalidDataException();
            if (r < 0)
                throw new InvalidDataException("The number responding can not be below 0");
            double qpi = parameters["qpi"].AsDouble;
            if (qpi < 0)
                qpi = 0;
            if (qpi > 1)
                qpi = 1;
            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;
            double cit = PDF.gauinv(cco + (1 - cco) / 2, out int fault);
            double p = r / n;
            ParameterBag outputParameters = new();
            outputParameters.AddOutput("prop", p);

            // Clopper Pearson by F distribution
            outputParameters.AddOutput("ci_exact", cco * 100);
            MathDbl.binci(r, n, out double pil, out double piu, cco, out string warn);
            outputParameters.AddOutput("lower_exact", pil);
            outputParameters.AddOutput("upper_exact", piu);
            outputParameters.AddOutput("warn_exact", warn);

            // binomial exact P: the less of the two tails, and the sum of the probabilities of the counts that are no more
            // probable than the count observed
            const string aprx = "Binomial";
            outputParameters.AddOutput("null", qpi);
            PDF.BinomialTails(Convert.ToInt64(n), qpi, Convert.ToInt64(r), out double lowerTail, out double upperTail, out double point, out double p2);
            p = Math.Min(lowerTail, upperTail);
            if (fault != 0)
            {
                p = Constant.MISSING;
                p2 = Constant.MISSING;
            }
            outputParameters.AddOutput("ap_1_exact", aprx);
            outputParameters.AddOutput("p_1_exact", p);
            outputParameters.AddOutput("ap_2_exact", aprx);
            outputParameters.AddOutput("p_2_exact", p2);

            // Wilson approximate mid-P
            double t1 = 2.0 * r + cit * cit;
            double t2 = cit * Math.Sqrt(cit * cit + 4.0 * r * (1.0 - r / n));
            double t3 = 2.0 * (n + cit * cit);
            pil = (t1 - t2) / t3;
            piu = (t1 + t2) / t3;
            outputParameters.AddOutput("ci_approx", cco * 100);
            outputParameters.AddOutput("lower_approx", pil);
            outputParameters.AddOutput("upper_approx", piu);

            // binomial mid P: the tail less half the probability of the count observed, and twice that
            p = Math.Min(lowerTail, upperTail) - point / 2.0;
            p2 = Math.Min(1.0, 2.0 * p);
            if (fault != 0)
            {
                p = Constant.MISSING;
                p2 = Constant.MISSING;
            }
            outputParameters.AddOutput("ap_1_approx", aprx);
            outputParameters.AddOutput("p_1_approx", p);
            outputParameters.AddOutput("ap_2_approx", aprx);
            outputParameters.AddOutput("p_2_approx", p2);

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Two independent proportions: r1 of n1 observations of one sample, and r2 of n2 of another, have the characteristic.
        /// </summary>
        /// <remarks>
        /// The difference is that of the proportion of the first sample less that of the second, r1 / n1 - r2 / n2.  Its
        /// confidence limits are those of MathDbl.uppci.
        /// The exact two sided mid-P value is that of the 2 by 2 table of the two samples by the characteristic
        /// (PropMidPFisher2); there is none if none, or all, of both samples have the characteristic.
        /// The normal deviate z is the difference over its standard error under the null hypothesis that the two proportions are
        /// the same, root(p (1 - p) (1 / n1 + 1 / n2)), where p is the proportion of the two samples together,
        /// (r1 + r2) / (n1 + n2).  If p is 0 or 1 the standard error is 0: there is then no z, and the report has no
        /// approximate P values.  The P values of z are made where the report is shown.
        /// Numbers that are not whole numbers are rounded, a half to the even number.
        /// </remarks>
        /// <param name="parameters">"n1" and "r1": the size of the first sample and the number of it with the characteristic;
        /// "n2" and "r2": those of the second; "cco": the confidence level, for which 0.95 is taken if it is not between 0
        /// and 1.</param>
        /// <returns>"n_1", "r_1", "n_2", "r_2": the numbers, rounded; "prop_1", "prop_2" and "prop_diff": the two proportions
        /// and their difference; "ci": the confidence level as a percentage, and "from" and "to": the limits of the
        /// difference; "*exact2": a row with "mp", the exact two sided mid-P value, if there is one; "se" and "z": the
        /// standard error and the normal deviate, or missing; "*approx2": a row with "z", if there is one.</returns>
        public static StepOutput RptPropUnPaired(ParameterBag parameters)
        {
            // numbers that are not whole numbers are rounded, a half to the even number
            double n1 = Math.Round(parameters["n1"].AsDouble);
            double r1 = Math.Round(parameters["r1"].AsDouble);

            if (r1 > n1)
                throw new InvalidDataException("The number responding in group 1 can not be more than its total");
            if (n1 <= 0)
                throw new InvalidDataException();

            double n2 = Math.Round(parameters["n2"].AsDouble);
            double r2 = Math.Round(parameters["r2"].AsDouble);
            if (r2 > n2)
                throw new InvalidDataException("The number responding in group 2 can not be more than its total");

            if (n2 <= 0)
                throw new InvalidDataException();
            if (r1 < 0 || r2 < 0)
                throw new InvalidDataException("A number responding can not be below 0");

            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 | cco >= 1.0)
                cco = 0.95;
            double cit = PDF.gauinv(cco + (1 - cco) / 2, out int fault);
            if (fault != 0)
                return null;

            double p1 = r1 / n1;
            double p2 = r2 / n2;
            double p = (r1 + r2) / (n1 + n2);

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("n_1", n1);
            outputParameters.AddOutput("r_1", r1);
            outputParameters.AddOutput("prop_1", p1);
            outputParameters.AddOutput("n_2", n2);
            outputParameters.AddOutput("r_2", r2);
            outputParameters.AddOutput("prop_2", p2);
            outputParameters.AddOutput("prop_diff", p1 - p2);

            MathDbl.uppci(Convert.ToInt32(r1), Convert.ToInt32(n1), Convert.ToInt32(r2), Convert.ToInt32(n2), out double cl, out double cu, cit, 100.0 * cco);

            outputParameters.AddOutput("ci", 100 * cco);
            outputParameters.AddOutput("from", cl);
            outputParameters.AddOutput("to", cu);

            double mp = PropMidPFisher2(Convert.ToInt32(r1), Convert.ToInt32(n1 - r1), Convert.ToInt32(r2), Convert.ToInt32(n2 - r2));
            List<ParameterBag> exact2List = new();
            outputParameters.AddOutput("*exact2", exact2List);
            if (mp != Constant.MISSING)
            {
                ParameterBag exact2Parameters = new();
                exact2List.Add(exact2Parameters);
                exact2Parameters.AddOutput("mp", mp);
            }

            // the standard error of the difference if the two proportions are the same, from the proportion of both samples
            double sepest = Math.Sqrt(p * (1 - p) * (1 / n1 + 1 / n2));
            double z;
            if (sepest == 0)
            {
                sepest = Constant.MISSING;
                z = Constant.MISSING;
            }
            else
            {
                z = (p1 - p2) / sepest;
            }

            outputParameters.AddOutput("se", sepest);
            outputParameters.AddOutput("z", z);

            List<ParameterBag> approx2List = new();
            outputParameters.AddOutput("*approx2", approx2List);
            if (z != Constant.MISSING)
            {
                ParameterBag approx2Parameters = new();
                approx2List.Add(approx2Parameters);
                // Duplicate - we could use the outside version, but that would leave this bag puzzlingly empty for anyone who doesn't read it along with the report and realise that it's solely there to trigger display of a section.
                approx2Parameters.AddOutput("z", z);
            }
            return new StepOutput(outputParameters);
        }
    }
}

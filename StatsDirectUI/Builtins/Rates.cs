using System;
using System.Collections.Generic;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public static class Rates
    {

        ///  <summary>
        ///  relates chi-sq to Poisson
        ///  </summary>
        ///  <param name="alpha"></param>
        ///  <param name="events"></param>
        ///  <param name="tar"></param>
        ///  <param name="xl"></param>
        ///  <param name="xu"></param>
        ///  <remarks>Johnson &amp; Kotz 1969, Ulm in Am J Epidemiol 1990 (131) 373-
        ///  comments by Dobson Stats in Med 1991 (10) 457-</remarks>
        ///  <remarks>
        ///  The limits are of the mean of a Poisson count, each over the time at risk: the lower limit is the mean with which as
        ///  many events or more have the probability alpha / 2, and the upper limit that with which as many or fewer have it.  The
        ///  lower limit is half the value that chi-square with twice as many degrees of freedom as there are events is below with
        ///  probability alpha / 2, and 0 if there are no events; the upper limit is half the value that chi-square with 2 more
        ///  degrees of freedom is below with probability 1 - alpha / 2.  The number of events need not be a whole number.  Both
        ///  limits are missing if it is below 0.
        ///  </remarks>
        public static void poisson_ci(double alpha, double events, double tar, out double xl, out double xu)
        {
            int fault;
            if (events < 0.0)
            {
                xl = Constant.MISSING;
                xu = Constant.MISSING;
            }
            else if (events == 0.0)
            {
                xl = 0.0;
                xu = PDF.ppchi2(1.0 - alpha / 2.0, 2.0, out fault) / 2.0;
                if (fault != 0)
                    xu = Constant.MISSING;
                else
                    xu /= tar;
            }
            else
            {
                xl = PDF.ppchi2(alpha / 2.0, 2.0 * events, out fault) / 2.0;
                if (fault != 0)
                    xl = Constant.MISSING;
                else
                    xl /= tar;
                xu = PDF.ppchi2(1.0 - alpha / 2.0, 2.0 * (events + 1.0), out fault) / 2.0;
                if (fault != 0)
                    xu = Constant.MISSING;
                else
                    xu /= tar;
            }
        }

        public static StepOutput RptRateSmr(ParameterBag parameters)
        {
            double cco = parameters["cco"].AsDouble;
            if (cco >= 1.0 || cco <= 0.0)
                cco = 0.95;

            DoubleVariable ratesVariable;
            DoubleVariable timesVariable;
            if (parameters.ContainsKey("data"))
            {
                // The screen form gives both columns in one grid: reference rate, index person-time
                DataFrame dataFrame = parameters["data"].AsDataFrame;
                if (dataFrame.VariableCount < 2)
                    throw new InvalidDataException("Enter the reference rate and the index person-time for every stratum");
                ratesVariable = (DoubleVariable) dataFrame.Variables[0];
                timesVariable = (DoubleVariable) dataFrame.Variables[1];
            }
            else
            {
                DataFrame ratesFrame = parameters["rates"].AsDataFrame;
                ratesVariable = (DoubleVariable) ratesFrame.Variables[0];
                DataFrame timesFrame = parameters["times"].AsDataFrame;
                timesVariable = (DoubleVariable) timesFrame.Variables[0];
            }
            int rawRows = ratesVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { ratesVariable.Data, timesVariable.Data }, 0, rawRows, 1);
            double[] asm = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] spop = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];

            double nunit = Parsing.Cdbl_Txt(parameters["nunit"].AsString);
            if (nunit <= 0.0)
                nunit = 1.0;
            int rows = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            if (rows < 1)
                throw new InvalidDataException("There is no stratum that has both a reference rate and a person-time");
            for (int i = 1; i <= rows; i++)
            {
                if (asm[i] < 0.0 || spop[i] < 0.0)
                    throw new InvalidDataException("Reference rates and person-times must not be negative");
                asm[i] /= nunit;
            }

            double etot = 0.0;
            for (int i = 1; i <= rows; i++)
                etot += asm[i] * spop[i];
            if (!(etot > 0.0))
                throw new InvalidDataException("No deaths are expected: a stratum must have a reference rate and a person-time above zero");

            string[] title = Meta.MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, rows);

            double dead = parameters["dead"].AsInt32;
            if (dead < 0.0)
                throw new InvalidDataException("The number of deaths observed must not be negative");

            ParameterBag outputParameters = new();
            List<ParameterBag> groupsList = new();
            outputParameters.AddOutput("*groups", groupsList);
            for (int j = 1; j <= rows; j++)
            {
                ParameterBag groupsParameters = new();
                groupsList.Add(groupsParameters);
                groupsParameters.AddOutput("group", asm[j]);
                groupsParameters.AddOutput("observed", spop[j]);
                groupsParameters.AddOutput("expected", spop[j] * asm[j]);
                groupsParameters.AddOutput("lb", hasUserSuppliedLabels ? title[j] : string.Empty);
            }
            outputParameters.AddOutput("total", etot);

            PDF.gauinv(cco + (1.0 - cco) / 2.0, out int fault);
            if (fault == 0)
            {
                outputParameters.AddOutput("ratio", dead / etot);
                // the ratio and its limits times 100, as whole numbers: they are held as numbers with a fraction, which have room
                // for a ratio of any size
                outputParameters.AddOutput("smr", Math.Round(dead / etot * 100, MidpointRounding.AwayFromZero));

                poisson_ci(1.0 - cco, dead, 1.0, out double xl, out double xu);

                if (xl != Constant.MISSING)
                    xl /= etot;
                if (xu != Constant.MISSING)
                    xu /= etot;
                outputParameters.AddOutput("pc", 100 * cco);
                outputParameters.AddOutput("from", xl);
                outputParameters.AddOutput("to", xu);
                outputParameters.AddOutput("from100", xl == Constant.MISSING ? xl : Math.Round(100 * xl));
                outputParameters.AddOutput("to100", xu == Constant.MISSING ? xu : Math.Round(100 * xu));

                ExFortran.poisson(etot, Convert.ToInt32(dead), out double phi, out double plo, out double _, out fault);
                if (fault != 0)
                    phi = Constant.MISSING;

                outputParameters.AddOutput("qty", Convert.ToInt64(dead));
                outputParameters.AddOutput("p_hi", phi);
                outputParameters.AddOutput("p_lo", plo);
            }
            return new StepOutput(outputParameters);
        }


        public static StepOutput RptRateDirect(ParameterBag parameters)
        {
            DataFrame idxnFrame = parameters["idxn"].AsDataFrame;
            DoubleVariable idxnVariable = (DoubleVariable) idxnFrame.Variables[0];
            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = (DoubleVariable) timesFrame.Variables[0];
            DataFrame refnFrame = parameters["refn"].AsDataFrame;
            DoubleVariable refnVariable = (DoubleVariable) refnFrame.Variables[0];
            int rawRows = idxnVariable.Length;

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { idxnVariable.Data, timesVariable.Data, refnVariable.Data }, 0, rawRows, 1);
            double[] idxy = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] idxn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] refn = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];

            int rows = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */

            string[] title = Meta.MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool _);
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, rows);

            return DirectStandardization(parameters, idxy, idxn, refn, title, rows);
        }

        internal static StepOutput DirectStandardization(ParameterBag parameters, double[] idxy, double[] idxn, double[] refn, string[] title, int rows)
        {
            double cco = parameters["cco"].AsDouble;
            if (cco >= 1.0 || cco <= 0.0)
                cco = 0.95;
            double alpha = 1.0 - cco;

            double nunit = Parsing.Cdbl_Txt(parameters["nunit"].AsString);
            if (nunit <= 0.0)
                nunit = 1.0;

            if (rows < 1)
                throw new InvalidDataException("There is no stratum that has events, a person-time and a reference group size");
            double events = 0.0;
            double ntot = 0.0;
            double refntot = 0.0;
            // whether the binomial model has a place for every stratum
            bool binomial = true;
            for (int i = 1; i <= rows; i++)
            {
                if (idxn[i] <= 0.0)
                    throw new InvalidDataException("Person-time must be greater than zero");
                if (idxy[i] < 0.0 || refn[i] < 0.0)
                    throw new InvalidDataException("Events and reference group sizes must not be negative");
                // more events than person-time are a rate above 1, which a Poisson count can have and a proportion can not
                if (idxy[i] > idxn[i])
                    binomial = false;
                events += idxy[i];
                ntot += idxn[i];
                refntot += refn[i];
            }
            if (refntot <= 0.0)
                throw new InvalidDataException("Total reference group size must be greater than zero");

            ParameterBag outputParameters = new();
            double[] refw = new double[rows + 1];
            for (int j = 1; j <= rows; j++)
                refw[j] = refn[j] / refntot;
            double stdr = 0.0;
            double pois_var = 0.0;
            double bino_var = 0.0;
            double[] idxr = new double[rows + 1];
            for (int j = 1; j <= rows; j++)
            {
                idxr[j] = idxy[j] / idxn[j];
                stdr += idxr[j] * refn[j];
                pois_var += refn[j] * refn[j] * idxr[j] / idxn[j];
                bino_var += refn[j] * refn[j] * idxr[j] * (1.0 - idxr[j]) / idxn[j];
            }
            stdr /= refntot;
            pois_var /= (refntot * refntot);
            bino_var /= (refntot * refntot);
            if (nunit == 1.0)
                outputParameters.AddOutput("units", "1 unit");
            else
                outputParameters.AddOutput("units", nunit.ToString("0") + " units");
            List<ParameterBag> inputsList = new();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int j = 1; j <= rows; j++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("idxy", idxy[j]);
                inputsParameters.AddOutput("idxn", idxn[j]);
                inputsParameters.AddOutput("idxr", idxr[j] * nunit);
                inputsParameters.AddOutput("refn", refn[j]);
                inputsParameters.AddOutput("refw", refw[j]);
            }
            double xu; double xl;
            // CIs for the single Poisson parameter (stratum specific rate)
            outputParameters.AddOutput("pc", cco * 100.0);
            List<ParameterBag> cisList = new();
            outputParameters.AddOutput("*cis", cisList);
            for (int j = 1; j <= rows; j++)
            {
                ParameterBag cisParameters = new();
                cisList.Add(cisParameters);
                cisParameters.AddOutput("idxr", idxr[j] * nunit);
                poisson_ci(alpha, idxy[j], idxn[j], out xl, out xu);
                cisParameters.AddOutput("from", xl * nunit);
                cisParameters.AddOutput("to", xu * nunit);
                cisParameters.AddOutput("label", title == null ? string.Empty : title[j]);
            }

            // pooled
            outputParameters.AddOutput("events", events);
            outputParameters.AddOutput("stde", stdr * ntot);

            outputParameters.AddOutput("crude", nunit * events / ntot);
            outputParameters.AddOutput("stdr", nunit * stdr);
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0);

            // Binomial approx CI - see Armitage
            // A zero variance (no events, or every stratum rate 1) gives a zero standard error and limits equal to the rate
            double ser;
            List<ParameterBag> noteList = new();
            outputParameters.AddOutput("*note", noteList);
            if (binomial && bino_var >= 0.0)
            {
                ser = Math.Sqrt(bino_var);
                outputParameters.AddOutput("ser_any", nunit * ser);
                xl = stdr - cit * ser;
                xu = stdr + cit * ser;
                outputParameters.AddOutput("from_any", nunit * xl);
                outputParameters.AddOutput("to_any", nunit * xu);
            }
            else
            {
                outputParameters.AddOutput("ser_any", Constant.MISSING);
                outputParameters.AddOutput("from_any", Constant.MISSING);
                outputParameters.AddOutput("to_any", Constant.MISSING);
                ParameterBag noteParameters = new();
                noteList.Add(noteParameters);
                noteParameters.AddOutput("note", "The binomial model is not given: a stratum has more events than person-time, which is a rate above 1. The binomial model needs the person-time as a number of persons, not scaled (not in thousands).");
            }

            // Poisson approx CI
            ser = pois_var >= 0.0 ? Math.Sqrt(pois_var) : Constant.MISSING;
            outputParameters.AddOutput("ser_small", nunit * ser);

            xl = stdr - cit * ser;
            xu = stdr + cit * ser;
            outputParameters.AddOutput("from_small", nunit * xl);
            outputParameters.AddOutput("to_small", nunit * xu);

            // Dobson et al. improved approx Poisson CI - Stats in Medicine 1991 (10)457
            poisson_ci(alpha, events, 1.0, out xl, out xu);
            if (xl != Constant.MISSING && pois_var >= 0.0 && events > 0.0)
                xl = stdr + Math.Sqrt(pois_var / events) * (xl - events);
            else
                xl = Constant.MISSING;
            if (xu != Constant.MISSING && pois_var >= 0.0 && events > 0.0)
                xu = stdr + Math.Sqrt(pois_var / events) * (xu - events);
            else
                xu = Constant.MISSING;
            outputParameters.AddOutput("from_dobson", xl == Constant.MISSING ? xl : nunit * xl);
            outputParameters.AddOutput("to_dobson", xu == Constant.MISSING ? xu : nunit * xu);

            return new StepOutput(outputParameters);
        }

        // The ratio of the rate of x events in the time t1 to that of y events in the time t2, and its confidence limits: with
        // the Poisson model (1) the exact limits from quantiles of the F distribution, p being the confidence level and half of
        // what is left of 1; with the binomial model the limits of Koopman, z being the normal deviate of the confidence level.
        // With no events in the second population the ratio and its upper limit are infinite; with none in either population
        // nothing is given.
        private static void RateRatio(int model, double p, double z, double x, double t1, double y, double t2, out double ratio, out double lower, out double upper)
        {
            if (x + y <= 0.0)
            {
                ratio = Constant.MISSING;
                lower = Constant.MISSING;
                upper = Constant.MISSING;
                return;
            }
            ratio = y == 0.0 ? double.PositiveInfinity : x / t1 / (y / t2);
            if (model == 1)
            {
                // Poisson
                if (x == 0.0)
                {
                    lower = 0.0;
                }
                else
                {
                    double f = PDF.ffromp(2.0 * x, 2.0 * (y + 1.0), 1.0 - p);
                    lower = t2 / t1 * (x / (y + 1.0)) * (1.0 / f);
                }
                if (y == 0.0)
                {
                    upper = double.PositiveInfinity;
                }
                else
                {
                    double f = PDF.ffromp(2.0 * y, 2.0 * (x + 1.0), 1.0 - p);
                    upper = t2 / t1 * ((x + 1.0) / y) * f;
                }
            }
            else
            {
                // Binomial like relative risk
                MathDbl.lr_ci(y, x, t2, t1, z, out lower, out upper);
            }
        }

        public static StepOutput RptStdrr(ParameterBag parameters)
        {
            double cco = parameters["cco"].AsDouble;
            if (cco >= 1.0 || cco <= 0.0)
                cco = 0.95;
            double cit = PDF.gauinv(1.0 - (1.0 - cco) / 2.0);

            DataFrame aFrame = parameters["a"].AsDataFrame;
            DoubleVariable aVariable = (DoubleVariable) aFrame.Variables[0];
            int rawRows = aVariable.Length;
            DataFrame pt1Frame = parameters["pt1"].AsDataFrame;
            DoubleVariable pt1Variable = (DoubleVariable) pt1Frame.Variables[0];
            DataFrame bFrame = parameters["b"].AsDataFrame;
            DoubleVariable bVariable = (DoubleVariable) bFrame.Variables[0];
            DataFrame pt2Frame = parameters["pt2"].AsDataFrame;
            DoubleVariable pt2Variable = (DoubleVariable) pt2Frame.Variables[0];
            DataFrame refFrame = parameters["ref"].AsDataFrame;
            DoubleVariable refVariable = (DoubleVariable) refFrame.Variables[0];

            DoubleArraysAndBooleans copiesRemovingMissingRows = Numerics.Utilities.RemoveMissingRows(new[] { aVariable.Data, pt1Variable.Data, bVariable.Data, pt2Variable.Data, refVariable.Data }, 0, rawRows, 1);
            double[] a = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0];
            double[] pt1 = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[1];
            double[] b = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[2];
            double[] pt2 = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[3];
            double[] refIdent = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[4];

            string[] title = Meta.MakeTitles(parameters, "strata", "stratum {0}", rawRows, out bool hasUserSuppliedLabels);

            int k = copiesRemovingMissingRows.ArraysWithMissingRowsRemoved[0].Length - 1; /* 1-based */
            title = Numerics.Utilities.CopyValidRows(title, copiesRemovingMissingRows.ValidRowsInOriginal, 0, rawRows, 1, k, 2);

            string modelString = parameters["model"].AsString;
            int model = "poisson".Equals(modelString) ? 1 : 2;

            double nunit = Parsing.Cdbl_Txt(parameters["nunit"].AsString);
            if (nunit <= 0.0)
                nunit = 1.0;

            if (k < 1)
                throw new InvalidDataException("There is no stratum that has the events and the person-time of both populations and a reference group size");
            for (int i = 1; i <= k; i++)
            {
                if (pt1[i] <= 0.0 || pt2[i] <= 0.0)
                    throw new InvalidDataException("Person-time must be greater than zero");
                if (a[i] < 0.0 || b[i] < 0.0 || refIdent[i] < 0.0)
                    throw new InvalidDataException("Events and reference group sizes must not be negative");
                if (model != 1 && (a[i] > pt1[i] || b[i] > pt2[i]))
                    throw new InvalidDataException("With the binomial model the number of events must not exceed the person-time (do not scale the person-time)");
            }

            double[] rkr = new double[k + 3];
            double[] rkw = new double[k + 3];
            double[] rkrl = new double[k + 3];
            double[] rkru = new double[k + 3];

            double refsum = 0.0;
            double asum = 0.0;
            double bsum = 0.0;
            double pt1Sum = 0.0;
            double pt2Sum = 0.0;
            for (int i = 1; i <= k; i++)
            {
                refsum += refIdent[i];
                asum += a[i];
                bsum += b[i];
                pt1Sum += pt1[i];
                pt2Sum += pt2[i];
            }
            if (refsum <= 0.0)
                throw new InvalidDataException("Total reference group size must be greater than zero");

            double alpha = 1.0 - cco;
            if (alpha <= 0.0 || alpha >= 1.0)
                alpha = 0.05;
            double p = cco + (1.0 - cco) / 2.0;

            double cre = asum / pt1Sum;
            double crne = bsum / pt2Sum;
            string warn1;
            string warn2;
            double crneu; double crnel; double creu; double crel;

            if (model == 1)
            {
                // Poisson rate CI
                poisson_ci(alpha, asum, pt1Sum, out crel, out creu);
                poisson_ci(alpha, bsum, pt2Sum, out crnel, out crneu);
                warn1 = string.Empty;
                warn2 = string.Empty;
            }
            else
            {
                // Binomial like single proportion
                MathDbl.binci(asum, pt1Sum, out crel, out creu, cco, out warn1);
                MathDbl.binci(bsum, pt2Sum, out crnel, out crneu, cco, out warn2);
            }

            RateRatio(model, p, cit, asum, pt1Sum, bsum, pt2Sum, out double crr, out double crrl, out double crru);

            rkr[k + 1] = crr;
            rkrl[k + 1] = crrl;
            rkru[k + 1] = crru;
            rkw[k + 1] = 1.0;
            title[k + 1] = "All (crude)";

            double sre = 0.0;
            double srne = 0.0;
            double vsre = 0.0;
            double vsrne = 0.0;
            double vsre_bino = 0.0;
            double vsrne_bino = 0.0;

            for (int i = 1; i <= k; i++)
            {
                // rr and ci for stratum
                RateRatio(model, p, cit, a[i], pt1[i], b[i], pt2[i], out rkr[i], out rkrl[i], out rkru[i]);
                rkw[i] = refIdent[i] / refsum;
                // pooled
                if (refIdent[i] != 0.0)
                {
                    rkw[i] = refIdent[i] / refsum;
                    double pa = a[i] / pt1[i];
                    double qa = 1.0 - pa;
                    sre += refIdent[i] * pa;
                    vsre += rkw[i] * rkw[i] * (a[i] / (pt1[i] * pt1[i]));
                    vsre_bino += rkw[i] * rkw[i] * pa * qa / pt1[i];
                    double pb = b[i] / pt2[i];
                    double qb = 1.0 - pb;
                    srne += refIdent[i] * pb;
                    vsrne += rkw[i] * rkw[i] * (b[i] / (pt2[i] * pt2[i]));
                    vsrne_bino += rkw[i] * rkw[i] * pb * qb / pt2[i];
                }
            }
            sre /= refsum;
            srne /= refsum;

            double sreu_bino; double srel_bino; double sreu; double srel;
            if (vsre < 0.0)
            {
                srel = Constant.MISSING;
                sreu = Constant.MISSING;
            }
            else
            {
                srel = sre - cit * Math.Sqrt(vsre);
                sreu = sre + cit * Math.Sqrt(vsre);
            }

            if (vsre_bino < 0.0)
            {
                srel_bino = Constant.MISSING;
                sreu_bino = Constant.MISSING;
            }
            else
            {
                srel_bino = sre - cit * Math.Sqrt(vsre_bino);
                sreu_bino = sre + cit * Math.Sqrt(vsre_bino);
            }

            double srneu_bino; double srnel_bino; double srneu; double srnel;
            if (vsrne < 0.0)
            {
                srnel = Constant.MISSING;
                srneu = Constant.MISSING;
            }
            else
            {
                srnel = srne - cit * Math.Sqrt(vsrne);
                srneu = srne + cit * Math.Sqrt(vsrne);
            }

            if (vsrne_bino < 0.0)
            {
                srnel_bino = Constant.MISSING;
                srneu_bino = Constant.MISSING;
            }
            else
            {
                srnel_bino = srne - cit * Math.Sqrt(vsrne_bino);
                srneu_bino = srne + cit * Math.Sqrt(vsrne_bino);
            }

            double srru_bino; double srrl_bino;
            double srru; double srrl;
            double srr;
            if (srne > 0.0 && sre > 0.0)
            {
                srr = sre / srne;
                double vsrr = vsre / (sre * sre) + vsrne / (srne * srne);
                double vsrr_bino = vsre_bino / (sre * sre) + vsrne_bino / (srne * srne);
                srrl = Math.Exp(Math.Log(srr) - cit * Math.Sqrt(vsrr));
                srru = Math.Exp(Math.Log(srr) + cit * Math.Sqrt(vsrr));
                double dtmp;
                if (srrl > srru)
                {
                    dtmp = srrl;
                    srrl = srru;
                    srru = dtmp;
                }
                srrl_bino = Math.Exp(Math.Log(srr) - cit * Math.Sqrt(vsrr_bino));
                srru_bino = Math.Exp(Math.Log(srr) + cit * Math.Sqrt(vsrr_bino));
                if (srrl_bino > srru_bino)
                {
                    dtmp = srrl_bino;
                    srrl_bino = srru_bino;
                    srru_bino = dtmp;
                }
            }
            else
            {
                // a standardized rate of nothing: the ratio is 0, or it is infinite, or with both rates nothing it is not known;
                // the limits are from the logarithm of the ratio, which then has none to give
                srr = srne > 0.0 ? 0.0 : sre > 0.0 ? double.PositiveInfinity : Constant.MISSING;
                srrl = Constant.MISSING;
                srru = Constant.MISSING;
                srrl_bino = Constant.MISSING;
                srru_bino = Constant.MISSING;
            }

            ParameterBag outputParameters = new();
            List<ParameterBag> strataList = new();
            outputParameters.AddOutput("*strata", strataList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag strataParameters = new();
                strataList.Add(strataParameters);
                strataParameters.AddOutput("st", i);
                strataParameters.AddOutput("a", a[i]);
                strataParameters.AddOutput("pt1", pt1[i]);
                strataParameters.AddOutput("b", b[i]);
                strataParameters.AddOutput("pt2", pt2[i]);
                strataParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
            }
            outputParameters.AddOutput("pc", cco * 100.0);
            string meth = model == 1 ? "exact Poisson" : "Koopman";
            outputParameters.AddOutput("method", meth);
            List<ParameterBag> ratesList = new();
            outputParameters.AddOutput("*rates", ratesList);
            for (int i = 1; i <= k + 1; i++)
            {
                ParameterBag ratesParameters = new();
                ratesList.Add(ratesParameters);
                ratesParameters.AddOutput("st", i <= k ? i.ToString() : "All");
                ratesParameters.AddOutput("rr", rkr[i]);
                ratesParameters.AddOutput("lci", rkrl[i]);
                ratesParameters.AddOutput("uci", rkru[i]);
                ratesParameters.AddOutput("wt", rkw[i]);
                ratesParameters.AddOutput("lb", hasUserSuppliedLabels ? title[i] : string.Empty);
            }

            outputParameters.AddOutput("model_out", model == 1 ? "Poisson (small rates)" : "Binomial");
            if (nunit == 1.0)
                outputParameters.AddOutput("units", "1 unit");
            else
                outputParameters.AddOutput("units", nunit.ToString("0") + " units");

            outputParameters.AddOutput("cre", cre * nunit);
            outputParameters.AddOutput("cre_from", crel * nunit);
            outputParameters.AddOutput("cre_to", creu * nunit);
            outputParameters.AddOutput("cre_warn", warn1);

            outputParameters.AddOutput("crne", crne * nunit);
            outputParameters.AddOutput("crne_from", crnel * nunit);
            outputParameters.AddOutput("crne_to", crneu * nunit);
            outputParameters.AddOutput("crne_warn", warn2);

            outputParameters.AddOutput("sre", sre * nunit);
            if (model == 1)
            {
                outputParameters.AddOutput("sre_from", srel * nunit);
                outputParameters.AddOutput("sre_to", sreu * nunit);
            }
            else
            {
                outputParameters.AddOutput("sre_from", srel_bino * nunit);
                outputParameters.AddOutput("sre_to", sreu_bino * nunit);
            }

            outputParameters.AddOutput("srne", srne * nunit);
            if (model == 1)
            {
                outputParameters.AddOutput("srne_from", srnel * nunit);
                outputParameters.AddOutput("srne_to", srneu * nunit);
            }
            else
            {
                outputParameters.AddOutput("srne_from", srnel_bino * nunit);
                outputParameters.AddOutput("srne_to", srneu_bino * nunit);
            }

            outputParameters.AddOutput("srr", srr);
            if (model == 1)
            {
                outputParameters.AddOutput("srr_from", srrl);
                outputParameters.AddOutput("srr_to", srru);
            }
            else
            {
                outputParameters.AddOutput("srr_from", srrl_bino);
                outputParameters.AddOutput("srr_to", srru_bino);
            }

            CorrelationRowType[] pg = new CorrelationRowType[k + 2 + 1 /* VB to C# conversion */ ];
            pg[k + 1] = CorrelationRowType.Subgroup;
            pg[k + 2] = CorrelationRowType.Pooled;

            rkr[k + 2] = srr;
            rkrl[k + 2] = model == 1 ? srrl : srrl_bino;
            rkru[k + 2] = model == 1 ? srru : srru_bino;
            rkw[k + 2] = Constant.MISSING;
            rkw[k + 1] = Constant.MISSING;
            title[k + 2] = "Standardized";

            // the plot has a scale of logarithms: a row with an infinite ratio, or a standardized ratio of 0, is left out of it,
            // as a row without a ratio is
            for (int i = 1; i <= k + 2; i++)
            {
                if (double.IsInfinity(rkr[i]) || i == k + 2 && rkr[i] == 0.0)
                {
                    rkr[i] = Constant.MISSING;
                    rkrl[i] = Constant.MISSING;
                    rkru[i] = Constant.MISSING;
                }
            }

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            ParameterBag chartParameters = new();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Correlation, new CorrelationOptions(k + 2, title, rkr, rkrl, rkru, rkw, pg, "Stratified rate ratio plot (direct standardization)", "rate ratio (" + Formatting.XRound(cco * 100, 1) + "% confidence interval)", Transformation.Log, false)));

            return new StepOutput(outputParameters);
        }
    }
}

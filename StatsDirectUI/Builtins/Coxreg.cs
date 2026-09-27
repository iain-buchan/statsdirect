using System;
using System.Collections.Generic;
using System.Diagnostics;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.Numerics;
using StatsDirect.Utilities;
using System.Globalization;

namespace StatsDirect.Builtins
{
    /// <summary>
    /// Cox (proportional hazards) regression: the fit, its report, and the reports that follow it.
    /// </summary>
    /// <remarks>
    /// The model is that the hazard of a subject with predictors z is h0(t) exp(z'b).  The baseline hazard h0 is left unspecified, and each stratum
    /// has its own.  The coefficients b are those that make the partial likelihood greatest.  The partial likelihood is a product over the events:
    /// for each, the probability that the event was that of the subject who had it and not of another who was at risk at that time in the same
    /// stratum, which is exp(z'b) of the subject over the sum of exp(z'b) of those at risk.  Those at risk at a time are the subjects whose own
    /// times are no earlier.  When d subjects have events at the same time each of the d is given the same sum, that of all who were at risk at
    /// that time (Breslow's approximation).
    ///
    /// RptCoxRegression reads the data and makes the report.  coxreg, coxest and coxiter check the data, put them in order and iterate; CoxHessian
    /// makes one pass over the data for the log likelihood, its gradient and its matrix of second derivatives, and solves for the step.  The
    /// reports that follow a regression (the baseline survival and cumulative hazard, the residuals, the hazard ratios, the model analysis and the
    /// plots) work from the arrays that RptCoxRegression passes on.
    ///
    /// The arrays of the numerical routines are used from element 1.
    /// </remarks>
    public static class Coxreg
    {
        /// <summary>
        /// Orders records by stratum, then by time, then by hazard ratio from the greatest down: the order in which the baseline survival and
        /// cumulative hazard are worked out.
        /// </summary>
        private class CoxpByStratumTimeThenExb : IComparer<CoxP>
        {
            private static int Compare(CoxP x, CoxP y)
            {
                //  First check Stratum
                if (x.Stratum > y.Stratum)
                    return 1;
                if (x.Stratum < y.Stratum)
                    return -1;

                //  Next check Time
                if (x.Time > y.Time)
                    return 1;
                if (x.Time < y.Time)
                    return -1;

                //  Next check Exb (negated)
                if (x.Exb > y.Exb)
                    return -1;
                if (x.Exb < y.Exb)
                    return 1;

                //  If we get here, there are no meaningful differences
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<CoxP>.Compare(CoxP x, CoxP y) => Compare(x, y);
        }

        /// <summary>Orders records by stratum, then by time: the order of the plots that have a line for each stratum.</summary>
        private class CoxpByStratumThenTime : IComparer<CoxP>
        {
            private static int Compare(CoxP x, CoxP y)
            {
                //  First check stratum
                if (x.Stratum > y.Stratum)
                    return 1;
                if (x.Stratum < y.Stratum)
                    return -1;

                //  Next check time
                return x.Time.CompareTo(y.Time);
            }

            int IComparer<CoxP>.Compare(CoxP x, CoxP y) => Compare(x, y);
        }

        /// <summary>Orders records by group, then by time: the order of the plots that have a line for each value of a binary predictor.</summary>
        private class CoxpByIdThenTm : IComparer<CoxP>
        {
            private static int Compare(CoxP x, CoxP y)
            {
                //  First check id
                if (x.Id > y.Id)
                    return 1;
                if (x.Id < y.Id)
                    return -1;

                //  Next check TM
                return x.Time.CompareTo(y.Time);
            }
            // interface methods implemented by Compare
            int IComparer<CoxP>.Compare(CoxP x, CoxP y) => Compare(x, y);

        }

        /// <summary>Puts records back in the order in which they were given.</summary>
        private class CoxpByIndex : IComparer<CoxP>
        {
            private static int Compare(CoxP x, CoxP y) => x.Index - y.Index;

            // interface methods implemented by Compare
            int IComparer<CoxP>.Compare(CoxP x, CoxP y) => Compare(x, y);
        }

        /// <summary>Orders records by time alone.</summary>
        private class CoxpByTm : IComparer<CoxP>
        {
            private static int Compare(CoxP x, CoxP y) => x.Time.CompareTo(y.Time);

            // interface methods implemented by Compare
            int IComparer<CoxP>.Compare(CoxP x, CoxP y) => Compare(x, y);
        }

        /// <summary>The rows, counted from 0, of the records in which nothing is missing: the time, the event code, each predictor and the stratum.</summary>
        /// <param name="parameters">The frames that were selected: times, events, predictors and, if there are any, strata.</param>
        /// <param name="records">On return, the number of records that were selected.</param>
        private static int[] CompleteRecords(ParameterBag parameters, out int records)
        {
            List<double[]> columns = new();
            foreach (string name in new[] { "times", "events", "predictors", "strata" })
                if (parameters.ContainsKey(name) && parameters[name] != null && parameters[name].AsDataFrame != null)
                    foreach (IVariable variable in parameters[name].AsDataFrame.Variables)
                        columns.Add(((GenericVariable<double>)variable).Data);
            records = columns.Count > 0 ? columns[0].Length : 0;
            List<int> complete = new();
            for (int r = 0; r < records; r++)
            {
                bool nothingMissing = true;
                foreach (double[] column in columns)
                    if (r >= column.Length || column[r] == Constant.MISSING)
                        nothingMissing = false;
                if (nothingMissing)
                    complete.Add(r);
            }
            return complete.ToArray();
        }

        /// <summary>A variable of the given length in which every value is missing.</summary>
        private static DoubleVariable MissingVariable(int length, string title)
        {
            DoubleVariable variable = new(length, title);
            for (int i = 0; i < length; i++)
                variable.SetData(i, Constant.MISSING);
            return variable;
        }

        /// <summary>
        /// Looks for a time that is zero or negative.  If there is one, the amount that would have to be added to every time to make the least
        /// of them 1 is given as timesAdjustment, and the operation asks whether to add it.
        /// </summary>
        public static StepOutput RptCoxRegressionPreprocess(ParameterBag parameters)
        {
            DataFrame timesFrame = parameters["times"].AsDataFrame;
            double[] times = ((DoubleVariable)timesFrame.Variables[0]).Data;
            double adjustment = 0.0;
            // a record with a missing value is left out of the regression, so its time does not count here
            foreach (int r in CompleteRecords(parameters, out int _))
                if (times[r] <= 0.0)
                    if (Math.Abs(times[r]) + 1 > adjustment)
                        adjustment = Math.Abs(times[r]) + 1;
            ParameterBag outputParameters = new();
            if (adjustment > 0.0)
                outputParameters.AddOutput("timesAdjustment", adjustment);
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The report of a Cox regression: the coefficients with their z and P values, and the likelihood ratio chi-square of the model.
        /// </summary>
        /// <remarks>
        /// The data are put into one array, x, a column at a time: the times, the censoring codes, the frequencies if there are any, the predictors,
        /// and the strata if there are any.  irt, icen, ifrq and istrat are the numbers of those columns (0 for one that is absent), and indef holds
        /// those of the predictors.  Element r of column c is x[rows * (c - 1) + r].
        ///
        /// The event code that was selected is 0 for a censored record and 1 for an event; a code above 1 is a number of subjects, all with the
        /// event, the time and the predictors of the record.  The censoring code that the fit is given is the other way round: 0 for an event and
        /// 1 for a censored record.  If any code is above 1 there is a column of frequencies, in which every other record has 1.
        ///
        /// The fit is made twice.  The second fit, with a single predictor that is 1 for everybody, gives the log likelihood of no effect at all,
        /// from which the likelihood ratio chi-square of the model is taken.
        ///
        /// What is passed on to the reports that follow:
        ///     ARR2[0..5, 0]  the number of records, the number of coefficients, the log likelihood, the log likelihood of no effect, the number
        ///                    of events, and the degrees of freedom (the coefficients less those of predictors that were dropped)
        ///     ARR2[i, 1..5]  for record i, what the fit leaves in columns 1 to 5 of caze (see coxiter): the survival of a subject at the means
        ///                    of the predictors, the leverage, the residual, the cumulative hazard of that subject, and the proportionality
        ///                    constant
        ///     ARR2[i, 6]     its time;  ARR2[i, 7]  1 if it had the event and 0 if it was censored
        ///     ARR2[i, 9]     its stratum, as the fit numbered the strata;  ARR2[i, 10]  its hazard ratio, exp(z'b)
        ///     ARR2[i, 11]    the number of subjects that it stands for
        ///     ARR3[1, j, 1..3]  for coefficient j, its estimate, its standard error and their ratio z
        ///     CDAT1[j]       the title of predictor j, and its two values if it is binary
        ///     holdx[i, j]    predictor j of record i, centred if it was centred for the fit
        /// </remarks>
        public static StepOutput RptCoxRegression(ParameterBag parameters)
        {
            // We don't have a clean way in the operation code to fail an operation if a user answers "no" to a question - in this case, whether they want to apply a calculated adjustment.
            // So this early code is simply a way of detecting a requirement to bug out.
            if (parameters.ContainsKey("useTimesAdjustment") && !parameters["useTimesAdjustment"].AsBoolean)
                throw new TemplateOperationCancelledException();

            // A record in which the time, the event code, a predictor or the stratum is missing is left out of the regression and of everything
            // that follows it.  recordOf gives the row, counted from 0, of each record that is kept.
            int[] recordOf = CompleteRecords(parameters, out int records);
            // ic counts the columns of x as they are added, and ik the elements
            int ic = 0;
            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = (DoubleVariable)timesFrame.Variables[0];
            ic++;
            int irt = ic;
            int rows = recordOf.Length;
            double[] x = new double[rows * ic + 1];
            int ik = 0;
            for (int r = 0; r < rows; r++)
            {
                ik++;
                x[ik] = timesVariable.Data[recordOf[r]];
            }

            if (parameters.ContainsKey("timesAdjustment"))
            {
                double adjustment = parameters["timesAdjustment"].AsDouble;
                for (int r = 1; r <= rows; r++)
                    x[r] += adjustment;
            }

            DataFrame eventsFrame = parameters["events"].AsDataFrame;
            DoubleVariable eventsVariable = eventsFrame.Variables[0] as DoubleVariable;
            ic++;
            int icen = ic;
            // create temp variable for copying values 
            double[] transTemp3 = new double[rows * ic + 1];
            Array.Copy(x, transTemp3, Math.Min(x.Length, transTemp3.Length));
            x = transTemp3;
            // The event codes are turned into censoring codes: 0 for an event, 1 for a censored record.  dead is the number of events, and ok is set
            // if any code is above 1, which makes a column of frequencies necessary.
            bool ok = false;
            bool anyEvents = false;
            double dead = 0;
            for (int r = 0; r < rows; r++)
            {
                ik++;
                x[ik] = eventsVariable.Data[recordOf[r]];
                dead += x[ik];
                if (x[ik] > 1)
                    ok = true;
                if (x[ik] > 0)
                {
                    anyEvents = true;
                    x[ik] = 0;
                }
                else if (x[ik] <= 0)
                    x[ik] = 1;
            }
            // the partial likelihood is a product over the event times, so without an event there is nothing to fit
            if (!anyEvents)
                throw new TemplateOperationCancelledException("There are no events (every observation is censored), so a Cox regression model cannot be fitted.", "Cox Regression");
            int ifrq;
            // use the frequency variable if data are grouped
            if (ok)
            {
                ic += 1;
                ifrq = ic;
                // create temp variable for copying values 
                double[] transTemp4 = new double[rows * ic + 1];
                Array.Copy(x, transTemp4, Math.Min(x.Length, transTemp4.Length));
                x = transTemp4;
                for (int r = 0; r < rows; r++)
                {
                    ik++;
                    x[ik] = eventsVariable.Data[recordOf[r]] > 1
                        ? eventsVariable.Data[recordOf[r]]
                        : 1;
                }
            }
            else
            {
                ifrq = 0;
            }

            DataFrame predictorsFrame = null;
            int[] indef;
            int icov = 0;
            int ncov; if (parameters.ContainsKey("predictors") && parameters["predictors"] != null)
            {
                predictorsFrame = parameters["predictors"].AsDataFrame;

                // load predictors into the master matrix
                ncov = predictorsFrame.VariableCount;
                // create temp variable for copying values 
                double[] transTemp5 = new double[rows * (ic + ncov) + 1];
                Array.Copy(x, transTemp5, Math.Min(x.Length, transTemp5.Length));
                x = transTemp5;
                // icov is the number of elements of x before the first predictor
                indef = new int[ncov + 1];
                icov = ik;
                for (int c = 0; c < predictorsFrame.VariableCount; c++)
                {
                    indef[c + 1] = ic + c + 1;
                    for (int r = 0; r < rows; r++)
                    {
                        ik += 1;
                        x[ik] = (predictorsFrame.Variables[c] as DoubleVariable).Data[recordOf[r]];
                    }
                }
                ic += ncov;
            }
            else
            {
                ncov = 0;
                indef = new int[1 + 1];
            }

            // start to fill the holdx matrix needed for the plot function
            double[,] holdx = new double[rows + 2, ncov + 2];
            for (int c = 1; c <= ncov; c++)
            {
                // If we get here, ncov must be at least 1, so predictorsFrame cannot be null.
                Debug.Assert(null != predictorsFrame);
                for (int r = 1; r <= rows; r++)
                    holdx[r, c] = (predictorsFrame.Variables[c - 1] as DoubleVariable).Data[recordOf[r - 1]];
            }

            // identify the binary covariates
            bool[] bincov = new bool[ncov + 1];
            ColumnData[] xd = new ColumnData[predictorsFrame.VariableCount];
            int binaries = 0;
            if (ncov > 0)
            {
                for (int c = 0; c < predictorsFrame.VariableCount; c++)
                {
                    xd[c] = new ColumnData();
                    // whether a predictor is binary is judged from the records that are kept
                    DoubleVariable kept = new(rows, predictorsFrame.Variables[c].Title);
                    for (int r = 1; r <= rows; r++)
                        kept.SetData(r - 1, holdx[r, c + 1]);
                    bincov[c + 1] = IsBinary(kept, xd[c]);
                    if (bincov[c + 1])
                        binaries += 1;
                }
            }

            // store predictor meta-data
            for (int c = 0; c < predictorsFrame.VariableCount; c++)
            {
                if (xd[c] == null)
                    xd[c] = new ColumnData();
                xd[c].Title = predictorsFrame.Variables[c].Title;
            }

            int istrat;             // get strata
            if (parameters.ContainsKey("strata") && parameters["strata"] != null)
            {
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                ClassifierVariable strataVariable = strataFrame.Variables[0] as ClassifierVariable;
                ic += 1;
                istrat = ic;
                // create temp variable for copying values 
                double[] transTemp6 = new double[rows * ic + 1];
                Array.Copy(x, transTemp6, Math.Min(x.Length, transTemp6.Length));
                x = transTemp6;
                for (int r = 0; r < rows; r++)
                {
                    ik += 1;
                    x[ik] = strataVariable.Data[recordOf[r]];
                }
            }
            else
            {
                istrat = 0;
            }

            int nCol = ic;

            // The fit allows an effect to be the product of several columns.  Here every effect is one predictor: nef effects, of nvef[i] = 1 column
            // each, the columns being listed in indef.
            int nef = ncov;
            if (nef < 1)
                throw new TemplateOperationCancelledException();
            int[] nvef = new int[nef + 1];
            for (int r = 1; r <= nef; r++)
                nvef[r] = 1;

            // ifix would be the column of a term with a fixed coefficient of 1 and itie = 1 would say that the records are already in order;
            // neither is used.  No more than maxit iterations are made.
            double eps = parameters["accuracy"].AsDouble;
            int ifix = 0;
            int itie = 0;
            int maxit = 30;
            double ratio = parameters["splitting-ratio"].AsDouble;
            if (ratio <= 0)
                ratio = -1.0;
            bool centre = parameters["centre-continuous-covariates"].AsBoolean;
            int nobs = rows;
            int ldcoef = nef;

            // A predictor that is not binary has its mean taken away, if that was asked for.  The coefficients are the same either way; what changes
            // is the subject to whom the baseline survival and hazard belong, who has each centred predictor at its mean and not at 0.
            if (centre)
            {
                for (int i = 1; i <= nef; i++)
                {
                    if (xd[i - 1].Groups == null || xd[i - 1].Groups.Count > 2)
                    {
                        // the mean over the subjects: a record counts as many times as the subjects it stands for
                        double xbar = 0;
                        double inMean = 0;
                        for (int r = 1; r <= rows; r++)
                        {
                            double frequency = ifrq > 0 ? x[rows * (ifrq - 1) + r] : 1.0;
                            xbar += frequency * holdx[r, i];
                            inMean += frequency;
                        }
                        xbar /= inMean;
                        for (int r = 1; r <= rows; r++)
                        {
                            holdx[r, i] = holdx[r, i] - xbar;
                            x[icov + (i - 1) * rows + r] = holdx[r, i];
                        }
                    }
                }
            }

            int[] igrp = new int[nobs + 1];
            double[,] ccase = new double[nobs + 1, 6 + 1];
            double[,] coef = new double[ldcoef + 1, 4 + 1];
            double[,] cov = new double[ldcoef + 1, ldcoef + 1];
            double[] GR = new double[ldcoef + 1];
            double[] xmean = new double[ldcoef + 1];
            int ifault = 0; int ncoef = 0;
            int nrmiss = 0;
            double algl = 0;
            coxreg(nobs, nCol, ref x, ref nobs, ref irt, ref ifrq, ref ifix, ref icen, ref istrat, ref maxit, ref eps, ref ratio, ref nef, ref nvef, ref indef, ref itie, ref ncoef, ref coef, ref ldcoef, ref algl, ref cov, ref ldcoef, ref xmean, ref ccase, ref nobs, ref GR, ref igrp, ref nrmiss, ref ifault);
            if (ifault != 0)
            {
                // faults 3 and 5 are a fit that has not converged: the log likelihood still fell when no more than 1/512 of the step was taken, or
                // the iterations ran out.  Faults above 100 name a predictor; fault 100 is a matrix that could not be factorised, with none to name.
                if (ifault == 3 || ifault == 5)
                    throw new TemplateOperationCancelledException("Calculation failed to converge, try again with a lower precision or fewer predictors.", "Cox Regression");
                else if (ifault == 100)
                    throw new TemplateOperationCancelledException("Singularity in Hessian: try again with fewer predictors.", "Cox Regression");
                else if (ifault > 99)
                    throw new TemplateOperationCancelledException("Singularity in Hessian: try dropping predictor " + (ifault - 100).ToString() + ": " + predictorsFrame.Variables[Math.Max(ifault - 101, 0)].Title, "Cox Regression");
                else
                    throw new TemplateOperationCancelledException("Error in calculation (" + ifault.ToString() + ")", "Cox Regression");
            }
            ColumnData[] CDAT1 = new ColumnData[ncoef + 1];
            double[,,] ARR3 = new double[1 + 1, ncoef + 1, 3 + 1];
            // column 0 of rows 0 to 5 holds n, the number of coefficients, the two log likelihoods, the number of events and the degrees of freedom,
            // so there are at least six rows
            double[,] ARR2 = new double[Math.Max(nobs, 5) + 1, 11 + 1];
            for (int i = 1; i <= ncoef; i++)
            {
                ARR3[1, i, 1] = coef[i, 1];
                ARR3[1, i, 2] = coef[i, 2];
                ARR3[1, i, 3] = coef[i, 3];
                CDAT1[i] = xd[i - 1];
            }

            double subjects = 0.0;
            for (int i = 1; i <= nobs; i++)
            {
                ARR2[i, 1] = ccase[i, 1];
                ARR2[i, 2] = ccase[i, 2];
                ARR2[i, 3] = ccase[i, 3];
                ARR2[i, 4] = ccase[i, 4];
                ARR2[i, 5] = ccase[i, 5];
                ARR2[i, 6] = x[nobs * (irt - 1) + i];
                if (x[nobs * (icen - 1) + i] == 0.0)
                {
                    ARR2[i, 7] = 1.0;
                }
                else { ARR2[i, 7] = 0.0; }
                // leave 8 for later assignment of plotting group
                ARR2[i, 9] = igrp[i];
                ARR2[i, 10] = ccase[i, 6];
                // the number of subjects that the record stands for
                ARR2[i, 11] = ifrq > 0 ? x[nobs * (ifrq - 1) + i] : 1.0;
                subjects += ARR2[i, 11];
            }
            ARR2[0, 0] = nobs;
            ARR2[1, 0] = ncoef;
            ARR2[2, 0] = algl;
            ARR2[4, 0] = dead;
            // a predictor that does not vary, or that is determined by others in the model, is dropped by the fit: its coefficient and its standard
            // error are zero, and it does not count in the degrees of freedom
            List<string> droppedPredictors = new();
            for (int i = 1; i <= ncoef; i++)
                if (coef[i, 2] == 0.0)
                    droppedPredictors.Add(xd[i - 1].Title);
            ARR2[5, 0] = ncoef - droppedPredictors.Count;

            // run a second time with a dummy var = 1 to get LL(0)
            nef = 1;
            ncov = 1;
            // int ldcov = 1; Never used.  PJC 2012/04/09.
            ldcoef = 1;
            // the first predictor's column is filled with ones, and is the only predictor of the second fit
            for (int i = icov + 1; i <= icov + nobs; i++)
                x[i] = 1.0;
            // the dummy variable is in the first covariate column: column 3, or 4 when grouped data put a frequency column before it
            indef[1] = icov / nobs + 1;
            igrp = new int[nobs + 1];
            ccase = new double[nobs + 1, 6 + 1];
            coef = new double[ldcoef + 1, 4 + 1];
            cov = new double[ldcoef + 1, ldcoef + 1];
            GR = new double[ldcoef + 1];
            xmean = new double[ldcoef + 1];
            coxreg(nobs, nCol, ref x, ref nobs, ref irt, ref ifrq, ref ifix, ref icen, ref istrat, ref maxit, ref eps, ref ratio, ref nef, ref nvef, ref indef, ref itie, ref ncoef, ref coef, ref ldcoef, ref algl, ref cov, ref ldcoef, ref xmean, ref ccase, ref nobs, ref GR, ref igrp, ref nrmiss, ref ifault);
            ARR2[3, 0] = algl;

            // What the plots can be split by: the strata if there are any, otherwise any one of the binary predictors, or nothing
            List<string> subgroups = new();
            if (istrat > 0)
            {
                subgroups.Add("Strata");
            }
            else
            {
                if (binaries > 0)
                {
                    for (int i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
                        if (bincov[i])
                            subgroups.Add(CDAT1[i].Title);
                    subgroups.Add("None");
                }
                else
                {
                    subgroups.Add("None");
                }
            }

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("n", subjects);
            outputParameters.AddOutput("d", ARR2[4, 0]);
            // The likelihood ratio chi-square: twice the rise in the log likelihood from that of no effect
            double x2dev = -2.0 * (ARR2[3, 0] - ARR2[2, 0]);
            outputParameters.AddOutput("x2", x2dev);
            outputParameters.AddOutput("df", ARR2[5, 0]);
            outputParameters.AddOutput("p_dev", ARR2[5, 0] > 0 ? PDF.chivalp(Math.Abs(x2dev), ARR2[5, 0]) : Constant.MISSING);
            IList<ParameterBag> warnList = new List<ParameterBag>();
            outputParameters.AddOutput("*warn", warnList);
            if (records > rows)
                warnList.Add(new ParameterBag("warn", new FilledStringParameter(FilledParameterDirection.Output, (records - rows).ToString() + " observations dropped due to missing data. Make sure that observations with missing data are not a subgroup.")));
            if (droppedPredictors.Count > 0)
                warnList.Add(new ParameterBag("warn", new FilledStringParameter(FilledParameterDirection.Output, string.Join(", ", droppedPredictors) + " dropped from the model because " + (droppedPredictors.Count > 1 ? "they do" : "it does") + " not vary or " + (droppedPredictors.Count > 1 ? "are" : "is") + " determined by other variable(s) included.")));
            IList<ParameterBag> predList = new List<ParameterBag>();
            outputParameters.AddOutput("*pred", predList);
            for (int i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
            {
                ParameterBag predParameters = new();
                predList.Add(predParameters);
                predParameters.AddOutput("lab", CDAT1[i].Title);
                predParameters.AddOutput("i", i);
                // a predictor that was dropped has no coefficient to show
                bool dropped = ARR3[1, i, 2] == 0.0;
                predParameters.AddOutput("b", dropped ? Constant.MISSING : ARR3[1, i, 1]);
                predParameters.AddOutput("z", dropped ? Constant.MISSING : ARR3[1, i, 3]);
                predParameters.AddOutput("p", dropped ? Constant.MISSING : MathDbl.zvalp2(ARR3[1, i, 3]));
            }
            outputParameters.AddInput("subgroups", new DataFrame(new StringVariable(subgroups.ToArray())));
            outputParameters.AddInput("ARR2", ARR2);
            outputParameters.AddInput("ARR3", ARR3);
            outputParameters.AddInput("CDAT1", CDAT1);
            outputParameters.AddInput("holdx", holdx);
            // for the values that the reports that follow save to the worksheet: the number of records selected, and the row of each that was kept
            outputParameters.AddInput("coxRecords", records);
            outputParameters.AddInput("coxRecordOf", recordOf);
            return new StepOutput(outputParameters);
        }

        ///  <summary>
        ///  Generate regressors
        ///  </summary>
        ///  <param name="nCol">The number of columns of the data.</param>
        ///  <param name="x">The data, a record at a time.</param>
        ///  <param name="ix1">The place in x of the first column of the record.</param>
        ///  <param name="nef">The number of effects.</param>
        ///  <param name="nvef">For each effect, the number of columns of which it is the product.</param>
        ///  <param name="indef">The columns of the first effect, then those of the second, and so on.</param>
        ///  <param name="idummy">Less than 0 to count the regressors and do no more.</param>
        ///  <param name="nreg">On return, the number of regressors.</param>
        ///  <param name="reg">On return, the regressors of the record: for each effect the product of its columns, or the missing value.</param>
        ///  <param name="nrmiss">On return, 1 if any column of any effect is missing in the record, and otherwise 0.</param>
        ///  <param name="ifault">On return 2 if an effect has no columns, and 3 if a column is out of range; otherwise as it was.</param>
        ///  <remarks>
        ///  In this program every effect is a single column, so that the regressors of a record are its predictors.
        ///  </remarks>
        private static void genregs(int nCol, double[] x, int ix1, int nef, int[] nvef, int[] indef, int idummy, ref int nreg, double[] reg, ref int nrmiss, ref int ifault)
        {
            int lindef = 0;

            for (int i = 1; i <= nef; i++)
            {
                if (nvef[i] <= 0)
                    ifault = 2;
                else
                    lindef += nvef[i];
            }
            if (ifault != 0)
                return;
            for (int i = 1; i <= lindef; i++)
                if (indef[i] <= 0 || indef[i] > nCol)
                    ifault = 3;
            if (ifault != 0)
                return;
            nrmiss = 0;
            if (idummy < 0)
            {
                // nreg = 0; never used
                // indefx = 1; never used
                nreg = nef;
                return;
            }
            nreg = 0;
            int indefx = 0;
            int misef = 0;
            int misval = 0;
            for (int i = 1; i <= nef; i++)
            {
                double xprod = 1.0;
                int nlast = 0;
                for (int L = nvef[i]; L >= 1; L--)
                {
                    int lndef = indef[indefx + L];
                    double xvar = x[ix1 - 1 + (lndef - 1) + 1];
                    if (xvar == Constant.MISSING)
                        misef = 1;
                    if (misef == 0)
                        xprod *= xvar;
                }
                int kpos = nreg;
                nreg += 1;
                int ik;
                if (misef == 1)
                {
                    misval = 1;
                    for (ik = kpos + 1; ik <= 1 + kpos + 1; ik++)
                        reg[ik] = Constant.MISSING;
                }
                else
                {
                    if (nlast == 0)
                    {
                        for (ik = kpos + 1; ik <= 1 + kpos + 1; ik++)
                            reg[ik] = 0.0;
                        reg[kpos + 1] = xprod;
                    }
                    else if (idummy == 2)
                    {
                        for (ik = kpos + 1; ik <= 1 + kpos + 1; ik++)
                            reg[ik] = 0.0;
                    }
                }
                indefx += nvef[i];
                misef = 0;
            }
            nrmiss += misval;
        }

        /// <summary>
        /// Checks the dimensions and the columns of the effects, counts the coefficients, makes the working arrays and calls coxest.
        /// </summary>
        /// <remarks>
        /// The arguments are those of coxest.  Faults: 1 and 2 if x or caze has too few rows; 5 if an effect has no columns; 6 if a column of an
        /// effect is out of range; 10 if there are no coefficients; 11 and 12 if cov or coef is too small; otherwise those of coxest.
        /// </remarks>
        private static void coxreg(int nRow, int nCol, ref double[] x, ref int ldx, ref int irt, ref int IFRQ, ref int ifix, ref int icen, ref int istrat, ref int maxit, ref double eps, ref double ratio, ref int nef, ref int[] nvef, ref int[] indef, ref int itie, ref int ncoef, ref double[,] coef, ref int ldcoef, ref double algl, ref double[,] cov, ref int ldcov, ref double[] xmean, ref double[,] caze, ref int ldcase, ref double[] GR, ref int[] igrp, ref int nrmiss, ref int ifault)
        {
            int i;
            double[] obz = new double[1 + 1];
            if (nRow > 1)
            {
                if (ldx < nRow)
                {
                    ifault = 1;
                    return;
                }
                if (ldcase < nRow)
                {
                    ifault = 2;
                    return;
                }
            }
            int ntrm = 0;
            for (i = 1; i <= nef; i++)
            {
                if (nvef[i] <= 0)
                {
                    ifault = 5;
                    return;
                }
                int j;
                for (j = 1; j <= nvef[i]; j++)
                {
                    ntrm += 1;
                    if (indef[ntrm] > nCol | indef[ntrm] <= 0)
                    {
                        ifault = 6;
                        return;
                    }
                }
            }
            genregs(nCol, x, 1, nef, nvef, indef, -2, ref ncoef, obz, ref nrmiss, ref ifault);
            if (ncoef <= 0)
            {
                ifault = 10;
            }
            else
            {
                if (ldcov < ncoef)
                    ifault = 11;
                if (ldcoef < ncoef)
                    ifault = 12;
            }
            if (ifault != 0)
                return;
            double[] OBS = new double[2 * (ncoef + 1) + 1];
            double[] smg = new double[2 * ncoef + 1];
            double[] smh = new double[2 * Math.Max(ncoef * ncoef, 2) + 1];
            int[] iptr = new int[nRow + ncoef + 1];
            int[] idt = new int[nRow + 1];
            coxest(nRow, nCol, ref x, ldx, irt, IFRQ, ifix, icen, ref istrat, ref maxit, ref eps, ref ratio, ref nef, ref nvef, ref indef, ref itie, ref ncoef, ref coef, ref ldcoef, ref algl, ref cov, ref ldcov, ref xmean, ref caze, ref ldcase, ref GR, ref igrp, ref nrmiss, ref OBS, ref smg, ref smh, ref iptr, ref idt, ref ifault);
        }

        /// <summary>
        /// ESTIMATES FOR PARAMETERS IN PROPORTIONAL HAZARDS MODEL
        /// </summary>
        /// <param name="nRow">The number of records.</param>
        /// <param name="nCol">The number of columns of x.</param>
        /// <param name="x">The data, a column at a time, as RptCoxRegression makes them.  They are rearranged for the fit and put back after it.</param>
        /// <param name="ldx">The number of rows that x has, which must be at least nRow.</param>
        /// <param name="irt">The column of the times.</param>
        /// <param name="IFRQ">The column of the frequencies, or 0.</param>
        /// <param name="ifix">The column of a term with a fixed coefficient of 1, or 0.</param>
        /// <param name="icen">The column of the censoring codes: 0 for an event, 1 for a censored record.</param>
        /// <param name="istrat">The column of the strata, or 0.</param>
        /// <param name="maxit">The most iterations that may be made.</param>
        /// <param name="eps">The fit has converged when the log likelihood changes by no more than eps of itself.</param>
        /// <param name="ratio">The ratio for the splitting of a stratum (see CoxHessian), or -1 for none.</param>
        /// <param name="nef">The number of effects;  nvef  the number of columns of each;  indef  those columns (see genregs).</param>
        /// <param name="itie">1 if the records are already in order, from the latest time to the earliest within each stratum.</param>
        /// <param name="ncoef">On return, the number of coefficients.</param>
        /// <param name="coef">On return, for coefficient j: coef[j, 1] its estimate, coef[j, 2] its standard error, coef[j, 3] their ratio.</param>
        /// <param name="algl">On return, the log likelihood.</param>
        /// <param name="cov">On return, the covariance matrix of the coefficients.</param>
        /// <param name="xmean">On return, the mean of each regressor.</param>
        /// <param name="caze">On return, the figures for each record (see coxiter).</param>
        /// <param name="GR">Working space: the gradient, and then the step.</param>
        /// <param name="igrp">On return, the number of the stratum of each record, or -1 for a record that was left out.</param>
        /// <param name="nrmiss">On return, the number of records that were left out.</param>
        /// <param name="ifault">
        /// On return 0 if all is well.  1 and 2 if x or caze has too few rows; 3 if eps is negative; 4 if ratio is negative and not -1; 6 and 7 if
        /// an effect has no columns or a column that is out of range; 10 if a censoring code is out of range (which is looked at only when there
        /// is a fixed term); 14 if there are no coefficients; 15 if a frequency is negative, or coef is
        /// too small; 16 if cov is too small, or the records were said to be in order and are not; otherwise the faults of coxiter.
        /// </param>
        /// <remarks>
        /// The records are looked over, the data are rearranged so that the columns of a record lie together (the routines that follow take a
        /// record at a time), and the order in which the records are to be taken is found: iptr lists them by stratum and, within a stratum, from
        /// the latest time to the earliest, a censored record before an event of the same time.  Taken in that order, the records met so far in a
        /// stratum are at every step the ones that are at risk at the time that has been reached.
        /// </remarks>
        private static void coxest(int nRow, int nCol, ref double[] x, int ldx, int irt, int IFRQ, int ifix, int icen, ref int istrat, ref int maxit, ref double eps, ref double ratio, ref int nef, ref int[] nvef, ref int[] indef, ref int itie, ref int ncoef, ref double[,] coef, ref int ldcoef, ref double algl, ref double[,] cov, ref int ldcov, ref double[] xmean, ref double[,] caze, ref int ldcase, ref double[] GR, ref int[] igrp, ref int nrmiss, ref double[] OBS, ref double[]
        smg, ref double[] smh, ref int[] iptr, ref int[] idt, ref int ifault)
        {
            int nidt = 0; int ik;
            int i;
            double xx = 0;

            int[] indkey = new int[3 + 1];
            if (nRow >= 1)
            {
                if (ldx < nRow)
                    ifault = 1;
                if (ldcase < nRow)
                    ifault = 2;
            }
            if (eps < 0.0)
                ifault = 3;
            if (ratio < 0.0 && ratio != -1.0)
                ifault = 4;
            if (ifault != 0)
                return;
            int ntrm = 0;
            for (i = 1; i <= nef; i++)
            {
                if (nvef[i] <= 0)
                {
                    ifault = 6;
                    return;
                }
                int j;
                for (j = 1; j <= nvef[i]; j++)
                {
                    ntrm += 1;
                    int ii = indef[ntrm];
                    if (ii > nCol | ii <= 0)
                    {
                        ifault = 7;
                        return;
                    }
                }
            }
            if (ifault != 0)
                return;
            // A record whose frequency is missing or zero is marked, with -1 in igrp, to be left out.  The checks of the fixed term, the censoring
            // code and the time are made only when there is a fixed term: RptCoxRegression, which has none, leaves out the records with a missing
            // value before it calls the fit.
            int mc = (icen - 1) * ldx;
            int mf = (IFRQ - 1) * ldx;
            int ms = (istrat - 1) * ldx;
            int mr = (irt - 1) * ldx;
            int mi = (ifix - 1) * ldx;
            int ier = 0;
            nrmiss = 0;
            for (i = 1; i <= nRow; i++)
            {
                igrp[i] = 0;
                if (IFRQ > 0)
                {
                    if (x[mf + i] == Constant.MISSING)
                    {
                        igrp[i] = -1;
                    }
                    else if (x[mf + i] < 0.0)
                    {
                        ier += 1;
                        ifault = 15;
                        ier += 1;
                        if (ier > 10)
                            return;
                    }
                    else if (x[mf + i] == 0.0)
                    {
                        igrp[i] = -1;
                    }
                }
                if (ifix > 0)
                {
                    if (x[mi + i] == Constant.MISSING)
                        igrp[i] = -1;
                    if (icen > 0)
                    {
                        if (x[mc + i] == Constant.MISSING)
                        {
                            igrp[i] = -1;
                        }
                        else if (Convert.ToInt64(x[mc + i]) > 3 | Convert.ToInt64(x[mc + i]) < 0.0)
                        {
                            ifault = 10;
                            ier += 1;
                            if (ier > 10)
                                return;
                        }
                        else if (Convert.ToInt64(x[mc + i]) > 1)
                        {
                            igrp[i] = -1;
                        }
                    }
                    if (x[mr + i] == Constant.MISSING)
                        igrp[i] = -1;
                }
            }
            if (ier > 0)
                return;

            genregs(nCol, x, 1, nef, nvef, indef, -2, ref ncoef, OBS, ref nrmiss, ref ifault);
            if (ncoef <= 0)
            {
                ifault = 14;
            }
            else
            {
                if (ldcoef < ncoef)
                    ifault = 15;
                if (ldcov < ncoef)
                    ifault = 16;
            }
            if (ifault > 0)
                return;

            // From here column c of record k is x[c + (k - 1) * nCol]
            MatrixTranspose1D(ldx, nCol, x, out ifault);
            if (itie != 1)
            {
                // The records are sorted by stratum, then by time, then by censoring code.  The sort is from the least up, so the times and the
                // codes have their signs changed for it, and changed back after it, to have the latest time first and, at one time, the censored
                // records (code 1) before the events (code 0).  The records themselves stay where they are: iptr[1] is the record to take first,
                // iptr[2] the next, and so on.
                int nkey = 0;
                if (istrat > 0)
                {
                    nkey += 1;
                    indkey[nkey] = istrat;
                }
                nkey += 1;
                indkey[nkey] = irt;
                if (icen > 0)
                {
                    nkey += 1;
                    indkey[nkey] = icen;
                    for (ik = icen; ik <= nRow * nCol; ik += nCol)
                        x[ik] = -1.0 * x[ik];
                }
                for (ik = irt; ik <= nRow * nCol; ik += nCol)
                    x[ik] = -1.0 * x[ik];
                Matrix.MXSRT(nCol, nRow, x, nkey, indkey, iptr, ref nidt, idt, ref ifault);
                for (ik = irt; ik <= nRow * nCol; ik += nCol)
                    x[ik] = -1.0 * x[ik];
                if (icen > 0)
                    for (ik = icen; ik <= nRow * nCol; ik += nCol)
                        x[ik] = -1.0 * x[ik];
            }
            else
            {
                // The records are said to be in order already.  They are taken as they come, after a check that within each stratum no time is
                // later than the one before it.
                mr = irt - nCol;
                ms = istrat - nCol;
                double xxg;
                if (istrat > 0)
                {
                    xxg = x[ms + nCol];
                    xx = x[mr + nCol];
                }
                else
                {
                    xxg = 0.0;
                }
                for (i = 1; i <= nRow; i++)
                {
                    iptr[i] = i;
                    mr += nCol;
                    if (istrat > 0)
                    {
                        ms += nCol;
                    }
                    if (igrp[i] >= 0)
                    {
                        if (istrat > 0)
                        {
                            if (xxg != x[ms])
                            {
                                xxg = x[ms];
                                xx = x[mr];
                            }
                            else
                            {
                                if (xx < x[mr])
                                {
                                    ifault = 16;
                                    return;
                                }
                                xx = x[mr];
                            }
                        }
                        else if (xxg == 0.0)
                        {
                            xx = x[mr];
                            xxg = 1.0;
                            if (xx < x[mr])
                            {
                                ifault = 16;
                                return;
                            }
                            xx = x[mr];
                        }
                        else
                        {
                            if (xx < x[mr])
                            {
                                ifault = 16;
                                return;
                            }
                            xx = x[mr];
                        }
                    }
                }
            }
            if (ncoef > 0)
                for (ik = 1; ik <= ncoef; ik++)
                    coef[ik, 1] = 0.0;
            coxiter(nRow, nCol, x, irt, IFRQ, ifix, icen, istrat, maxit, eps, ratio, nef, nvef, indef, itie, ref ncoef, coef, ref algl, cov, ldcov, xmean, caze, ldcase, GR, igrp, ref nrmiss, OBS, smg, smh, iptr, idt, ref ifault);
            if (ifault != 0)
                return;

            MatrixTranspose1D(nCol, ldx, x, out ifault);
            // x is a column at a time again, as it was given
        }

        /// <summary>
        /// The iterations of the fit, and the figures that are worked out for each record once it has converged.
        /// </summary>
        /// <remarks>
        /// The arguments are those of coxest, with its working arrays.  x is a record at a time here.  iptr is the order in which the records are
        /// taken (see coxest); its elements nobs + 1 to nobs + ncoef are set here, to 1 for a regressor that varies within a stratum and to 0 for
        /// one that does not.  idt is set here: the last of the records that have events at one time in one stratum is given their number, and
        /// every other record 0.
        ///
        /// Faults: 1 if the frequencies add up to nothing; 2 if fewer than two records can be used; 3 if the log likelihood still falls when no
        /// more than 1/512 of the step is taken; 5 if the iterations run out; 100 and above from CoxHessian.
        ///
        /// The iteration is Newton's.  CoxHessian gives, at the coefficients of the moment, the log likelihood and the step to take from them.
        /// The step is tried whole.  If the log likelihood falls by more than eps of itself the step is halved, and halved again, until it does
        /// not.  The fit has converged when the log likelihood changes by no more than eps of itself.  The coefficients start at 0.
        ///
        /// On return, for record k:
        ///     caze[k, 1]  exp(-caze[k, 4]): the survival, at the time of the record, of a subject whose predictors are at their means
        ///     caze[k, 2]  for a record with an event its leverage, d'Vd, where d is what its regressors differ by from their mean over those at
        ///                 risk at its time, each of them weighted by its hazard, and V is the covariance matrix of the coefficients; for a
        ///                 censored record the missing value
        ///     caze[k, 3]  caze[k, 4] caze[k, 5]: the cumulative hazard of the record itself at its time, which is its Cox-Snell residual
        ///     caze[k, 4]  the cumulative hazard, at the time of the record, of a subject whose predictors are at their means: the sum, over the
        ///                 times with events up to then, of the number of events over the sum of caze[ , 5] for those at risk
        ///     caze[k, 5]  the proportionality constant exp((z - m)'b): the hazard of the record relative to that of a subject at the means m
        ///     caze[k, 6]  exp(z'b): its hazard relative to that of a subject whose predictors are all 0
        /// While the work is going on the columns of caze hold other things, which are said where they are used.
        /// </remarks>
        private static void coxiter(int nobs, int nCol, double[] x, int irt, int IFRQ, int ifix, int icen, int istrat, int maxit, double eps, double ratio, int nef, int[] nvef, int[] indef, int itie, ref /* yes, really */ int ncoef, double[,] coef, ref double algl, double[,] cov, int ldcov, double[] xmean, double[,] caze, int ldcase, double[] GR, int[] igrp, ref int nrmiss, double[] OBS, double[] smg, double[] smh, int[] iptr, int[] idt, ref int ifault)
        {
            //   NEWTON-RAPHSON ITERATIONS
            double[] smd = new double[1 + 1];
            for (int i = 1; i <= ncoef; i++)
                xmean[i] = 0.0;
            // The means of the regressors, a record counting as many times as its frequency, and the numbers of the strata: igrp[k] becomes the
            // number of the stratum of record k, from 1 in the order in which the strata are met, or -1 if a regressor of the record is missing
            int nob1 = 0;
            double smfrq = 0.0;
            double strato = 1.23457E-27;
            bool strat = false;
            int igr = istrat > 0 ? 0 : 1;
            int ii = 0;
            int imiss = 0;
            double xfrq; double xx;
            double xcen;
            int j; int k;
            for (int i = 1; i <= nobs; i++)
            {
                k = iptr[i];
                if (igrp[k] >= 0)
                {
                    coxvars(x, (k - 1) * nCol, irt, 0, IFRQ, ifix, 0, icen, out double xrt, out double xlt, out xfrq, out double xfix, out double xpar, out xcen, out imiss);
                    if (istrat > 0)
                    {
                        xx = x[istrat + (k - 1) * nCol];
                        if (xx != strato)
                        {
                            igr += 1;
                            strato = xx;
                        }
                    }
                    genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                    if (imiss == 0)
                    {
                        for (j = 1; j <= ncoef; j++)
                            xmean[j] = xmean[j] + OBS[j] * xfrq;
                        smfrq += xfrq;
                        nob1 += 1;
                        igrp[k] = igr;
                        ii += 1;
                    }
                    else
                    {
                        igrp[k] = -1;
                        nrmiss += 1;
                    }
                }
            }
            if (smfrq == 0.0)
            {
                ifault = 1;
                return;
            }
            if (ii <= 1)
            {
                ifault = 2;
                return;
            }
            for (int i = 1; i <= ncoef; i++)
                xmean[i] = 1.0 / smfrq * xmean[i];
            // idt marks the times with events.  The records that have events at one time in one stratum lie together in the order; dt, igr and icncd
            // are the time, the stratum and the censoring code of the run of records that is being counted, itdt is the count, and j1 is the record
            // before the one in hand.  When a run of events ends, its last record is given the count.
            for (int i = 1; i <= nobs; i++)
                idt[i] = 0;
            int icncd; int icnn;
            int j1;
            if (itie != 1)
            {
                double dt = 1.23476E+34;
                igr = 0;
                icnn = 0;
                icncd = 1;
                j1 = 0;
                int itdt = 0;
                for (int i = 1; i <= nobs; i++)
                {
                    j = iptr[i];
                    if (igrp[j] >= 0)
                    {
                        if (icen > 0)
                            icnn = Convert.ToInt32(x[icen + (j - 1) * nCol]);
                        double dtn = x[irt + (j - 1) * nCol];
                        if (j1 != 0)
                            idt[j1] = 0;
                        if (dtn != dt || igrp[j] != igr || icncd != icnn)
                        {
                            if (icncd == 0)
                                idt[j1] = itdt;
                            itdt = 1;
                            icncd = icnn;
                            igr = igrp[j];
                            dt = dtn;
                        }
                        else
                        {
                            itdt += 1;
                        }
                        j1 = j;
                    }
                }
                idt[j1] = icnn == 0 ? itdt : 0;
            }
            else
            {
                // records that were given in order are taken to have no ties: every event is a time of its own
                icnn = 0;
                for (int i = 1; i <= nobs; i++)
                {
                    j = iptr[i];
                    if (igrp[j] >= 0)
                    {
                        if (icen > 0)
                            icnn = Convert.ToInt32(x[icen + (j - 1) * nCol]);
                        idt[j] = icnn == 0 ? 1 : 0;
                    }
                }
            }
            // Which regressors vary.  smg holds the regressors of the first record of the stratum, and iptr[nobs + j] is set to 1 when a later
            // record of the same stratum is found to differ from it in regressor j.  A regressor that is the same throughout every stratum tells
            // nothing, and CoxHessian leaves it out.  The search ends as soon as every regressor has been found to vary.
            bool ihess = false;
            icncd = ncoef;
            igr = 0;
            icnn = 0;
            bool zero = false;
            double smu = 0;
            for (ii = nobs + 1; ii <= ncoef + nobs; ii++)
                iptr[ii] = 0;
            int iq;
            for (int i = 1; i <= nobs; i++)
            {
                k = iptr[i];
                if (igrp[k] >= 0)
                {
                    imiss = 0;
                    genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                    if (igrp[k] != igr)
                    {
                        for (iq = 1; iq <= ncoef; iq++)
                            smg[iq] = OBS[iq];
                        igr = igrp[k];
                    }
                    else
                    {
                        for (j = 1; j <= ncoef; j++)
                        {
                            if (iptr[nobs + j] == 0)
                            {
                                if (OBS[j] != smg[j])
                                {
                                    iptr[nobs + j] = 1;
                                    icnn += 1;
                                }
                            }
                        }
                        if (icnn == icncd)
                            break;
                    }
                }
            }
            icncd = icnn;
            // The log likelihood at the starting values, and the first step.  ihess is not yet set: until the iterations are near the maximum the
            // matrix of second derivatives is replaced by one that is quicker to form (see CoxHessian).
            CoxHessian(nobs, nCol, x, irt, IFRQ, ifix, icen, ratio, nef, nvef, indef, ncoef, coef, 1, ihess, out double alglo, cov, ldcov, xmean, caze, ldcase, GR, OBS, smg, smh, iptr, idt, igrp, out bool change, zero, ref ifault);
            if (ifault != 0)
                return;
            double div;
            int iter; for (iter = 1; iter <= maxit; iter++)
            {
                // coef[ , 1] holds the coefficients, coef[ , 3] the step from them that CoxHessian has just given, and coef[ , 2] the coefficients
                // that are tried.  crit1, the size of the step beside that of the coefficients, is worked out but not used.
                for (ii = 1; ii <= ncoef; ii++)
                    coef[ii, 3] = GR[ii];
                double crit1 = 0.0;
                for (int i = 1; i <= ncoef; i++)
                {
                    double t = Math.Abs(coef[i, 1]);
                    crit1 = Math.Max(crit1, t > 1.0 ? Math.Abs(coef[i, 3] / coef[i, 1]) : Math.Abs(coef[i, 3]));
                }
                // div is the part of the step that is taken: all of it, then a half, a quarter and so on for as long as the log likelihood at the
                // coefficients tried is lower than it was by more than eps of itself.  crit is the change in the log likelihood as a proportion
                // of the log likelihood.
                div = 1.0;
                double crit;
                do
                {
                    for (int i = 1; i <= ncoef; i++)
                        coef[i, 2] = coef[i, 1] + div * coef[i, 3];
                    CoxHessian(nobs, nCol, x, irt, IFRQ, ifix, icen, ratio, nef, nvef, indef, ncoef, coef, 2, ihess, out algl, cov, ldcov, xmean, caze, ldcase, GR, OBS, smg, smh, iptr, idt, igrp, out change, zero, ref ifault);
                    if (ifault != 0)
                        return;
                    if (change)
                        strat = true;
                    crit = algl - alglo;
                    if (crit < 1.0E+20 * Math.Abs(alglo) && crit != 0.0)
                        crit /= Math.Abs(algl);
                    if (crit < -eps)
                    {
                        div /= 2.0;
                        if (div <= 0.001)
                        {
                            ifault = 3;
                            algl = alglo;
                            break; // Should break out of two levels of loop - see code below that tests for ifault==3 and breaks out of the outer level if found.
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);
                if (3 == ifault)
                {
                    // We broke out of an inner loop, but need to break out of the outer one as well in this fault case
                    break;
                }
                // near enough to the maximum for the matrix of second derivatives itself to be used from now on
                if (crit < 0.1)
                    ihess = true;
                for (ii = 1; ii <= ncoef; ii++)
                    coef[ii, 1] = coef[ii, 2];
                alglo = algl;
                if (Math.Abs(crit) <= eps)
                    break;
            }
            // the loop has run out, without convergence, only if the count has passed the limit
            if (iter > maxit)
                ifault = 5;
            // a fit that has not converged is reported as that: what follows is for a fit that has
            if (ifault != 0)
                return;
            // What follows, down to the next call of CoxHessian, is done only if CoxHessian split a stratum during the iterations (see the note on
            // splitting there).  A regressor may then have next to nothing left to vary by within the strata as they now stand.  To find out, the
            // linear predictor z'b of each record, less its mean in the stratum, is regressed by least squares on the regressors, less theirs
            // (glsqr1, a record at a time), which leaves in cov the triangular factor of that regression.  A regressor whose diagonal element of
            // the factor is less than 0.0001 of its standard deviation over all the records has its row and column of cov cleared, and zero is
            // set, which tells CoxHessian to leave out a regressor whose coefficient is 0.  The coefficients of the regression are not kept.
            // caze is used as working space: for regressor j, caze[j, 1] is its sum in the stratum, and caze[j, 2] and caze[j, 3] its sum of
            // squares and its sum over all the records, which become its standard deviation.
            double zdot;
            int irank; int kk; if (strat)
            {
                ifault = 6;
                int indy = ncoef + 1;
                int indep = indy;
                // Call GLREG(1, 0, INDY, X(1), -NCOEF, INDEF(1), INDEP, COEF(1, 1), NCOEF, COV(1, 1), LDCOV, SMG(1), irank, SMU, SMD(1), IMISS, SMH(1), SMH(1), NCOEF, OBS(1), ifault)
                //  Can't do calls with array offsets into VB, so this separates the top half of smh into its own array for the call
                double[] smhmax = new double[ncoef + 1];
                double[] coef1 = new double[ncoef + 1];
                for (int i = 0; i <= ncoef; i++)
                {
                    smhmax[i] = smh[i + ncoef];
                    coef1[i] = coef[i, 1];
                }
                irank = 0;
                imiss = 0;
                Regress1.glsqr1(1, 0, 0, 0, indy, x, 1, -ncoef, indef, indep, indef, 0, 0, coef1, cov, smg, ref irank, ref smu, ref smd[1], ref imiss, smh, smhmax, OBS, ref ifault);
                for (int i = 0; i <= ncoef; i++)
                {
                    smh[i + ncoef] = smhmax[i];
                    coef[i, 1] = coef1[i];
                }

                if (ifault != 0)
                    return;
                igr = 0;
                for (ii = 1; ii <= ncoef; ii++)
                {
                    caze[ii, 2] = 0.0;
                    caze[ii, 3] = 0.0;
                }
                xx = 0;
                double ymean = 0;
                for (int i = 1; i <= nobs; i++)
                {
                    k = iptr[i];
                    if (igrp[k] >= 0)
                    {
                        if (igr != igrp[k])
                        {
                            igr = igrp[k];
                            ymean = 0.0;
                            xx = 0.0;
                            for (j = 1; j <= ncoef; j++)
                                caze[j, 1] = 0.0;
                            for (j = i; j <= nobs; j++)
                            {
                                kk = iptr[j];
                                if (igrp[kk] >= 0)
                                {
                                    if (igrp[kk] != igr)
                                        break;
                                    genregs(nCol, x, 1 + (kk - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                                    zdot = 0.0;
                                    for (iq = 1; iq <= ncoef; iq++)
                                        zdot += OBS[iq] * coef[iq, 1];
                                    ymean += zdot;
                                    for (iq = 1; iq <= ncoef; iq++)
                                        caze[iq, 1] = caze[iq, 1] + OBS[iq] * 1.0;
                                    xx += 1.0;
                                }
                            }
                            if (xx > 0.0)
                                xx = 1.0 / xx;
                            ymean *= xx;
                        }
                        genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                        OBS[indy] = 0.0;
                        for (j = 1; j <= ncoef; j++)
                        {
                            caze[j, 2] = caze[j, 2] + OBS[j] * OBS[j];
                            caze[j, 3] = caze[j, 3] + OBS[j];
                            OBS[indy] = OBS[indy] + OBS[j] * coef[j, 1];
                            OBS[j] = OBS[j] - xx * caze[j, 1];
                        }
                        OBS[indy] = OBS[indy] - ymean;
                        // Call GLREG(2, 1, INDY, OBS(1), -NCOEF, INDEF(1), INDEP, COEF(1, 1), NCOEF, COV(1, 1), LDCOV, SMG(1), irank, SMU, SMD(1), IMISS, SMH(1), SMH(1), NCOEF, OBS(1), ifault)
                        //  Can't do calls with array offsets into VB, so this separates the top half of smh into its own array for the call
                        smhmax = new double[ncoef + 1];
                        coef1 = new double[ncoef + 1];
                        for (ii = 0; ii <= ncoef; ii++)
                        {
                            smhmax[ii] = smh[ii + ncoef];
                            coef1[ii] = coef[ii, 1];
                        }
                        Regress1.glsqr1(2, 0, 0, 1, indy, OBS, 1, -ncoef, indef, indep, indef, 0, 0, coef1, cov, smg, ref irank, ref smu, ref smd[1], ref imiss, smh, smhmax, OBS, ref ifault);
                        for (ii = 0; ii <= ncoef; ii++)
                        {
                            smh[ii + ncoef] = smhmax[ii];
                            coef[ii, 1] = coef1[ii];
                        }
                    }
                }
                xx = nobs - nrmiss;
                if (xx > 1.0)
                {
                    for (int i = 1; i <= ncoef; i++)
                    {
                        caze[i, 3] = caze[i, 3] * caze[i, 3] / xx;
                        caze[i, 2] = (caze[i, 2] - caze[i, 3]) / (xx - 1.0);
                        div = 0.0;
                        if (caze[i, 2] > 0.0)
                        {
                            caze[i, 2] = Math.Sqrt(caze[i, 2]);
                            div = Math.Abs(cov[i, i]) / caze[i, 2];
                        }
                        if (div < 0.0001)
                        {
                            zero = true;
                            for (ii = 1; ii <= i; ii++)
                                cov[ii, i] = 0.0;
                            for (ii = i + 1; ii <= ncoef; ii++)
                                cov[i, ii] = 0.0;
                        }
                    }
                    // Call GLREG(3, 0, INDY, OBS(1), -NCOEF, INDEF(1), INDEP, COEF(1, 1), NCOEF, COV(1, 1), LDCOV, SMG(1), irank, SMU, SMD(1), IMISS, SMH(1), SMH(1), NCOEF, OBS(1), ifault)
                    // Declare Sub GLSQR Lib "StatsDirect" (ByVal ido As Long, ByVal intcep As Long, ByVal isub As Long, ByVal nRow As Long, ByVal nvar As Long, ByVal x As Double, !!ByVal ldx As Long!!, ByVal iind As Long, ByVal indind As Long, ByVal idep As Long, ByVal inddep As Long, ByVal IFRQ As Long, ByVal iwt As Long, ByVal b As Double, !!ByVal ldb As Long!!, ByVal r As Double, !!ByVal ldr As Long!!, ByVal D As Double, ByVal irank As Long, ByVal rdf As Double, ByVal rss As Double, ByVal nrmiss As Long, ByVal xmin As Double, ByVal XMax As Double, ByVal WK As Double, ByVal ifault As Long)
                    //  Can't do calls with array offsets into VB, so this separates the top half of smh into its own array for the call
                    smhmax = new double[ncoef + 1];
                    coef1 = new double[ncoef + 1];
                    for (ii = 0; ii <= ncoef; ii++)
                    {
                        smhmax[ii] = smh[ii + ncoef];
                        coef1[ii] = coef[ii, 1];
                    }
                    Regress1.glsqr1(3, 0, 0, 0, indy, OBS, 1, -ncoef, indef, indep, indef, 0, 0, coef1, cov, smg, ref irank, ref smu, ref smd[1], ref imiss, smh, smhmax, OBS, ref ifault);
                    for (ii = 0; ii <= ncoef; ii++)
                    {
                        smh[ii + ncoef] = smhmax[ii];
                        coef[ii, 1] = coef1[ii];
                    }
                    for (ii = 1; ii <= ncoef; ii++)
                        coef[ii, 1] = coef[ii, 2];
                }
            }
            // The last pass, at the coefficients that were found, with the matrix of second derivatives itself.  It leaves in cov the triangular
            // factor R of that matrix, and in column 1 of caze the proportionality constant of each record.
            CoxHessian(nobs, nCol, x, irt, IFRQ, ifix, icen, ratio, nef, nvef, indef, ncoef, coef, 1, true, out algl, cov, ldcov, xmean, caze, ldcase, GR, OBS, smg, smh, iptr, idt, igrp, out change, zero, ref ifault);
            if (ifault != 0)
                return;
            // The proportionality constants are moved to column 5, and columns 1 to 4 are made ready for the figures of each record
            for (ii = 1; ii <= nobs; ii++)
                caze[ii, 5] = caze[ii, 1];
            igr = 0;
            for (int i = 1; i <= 4; i++)
                for (ii = 1; ii <= nobs; ii++)
                    caze[ii, i] = Constant.MISSING;
            // The figures of each record.  The records are taken in the other direction now, from the earliest time of a stratum to the latest.
            // At the start of a stratum smu is the sum of the proportionality constants of all its records, each times its frequency, and smg the
            // like sum of their regressors less the means: the sums for those at risk at the earliest time.  As the times are passed, the records
            // that are no longer at risk are taken out of both.  smd[1] is the cumulative hazard so far.  Until a record's own figures are worked
            // out, column 2 of caze holds its frequency and column 4 its censoring code.
            icnn = 0;
            for (int i = nobs; i >= 1; i--)
            {
                k = iptr[i];
                if (igrp[k] >= 0)
                {
                    if (igrp[k] != igr)
                    {
                        igr = igrp[k];
                        smu = 0.0;
                        for (ii = 1; ii <= ncoef; ii++)
                            smg[ii] = 0.0;
                        for (ii = i; ii >= 1; ii--)
                        {
                            kk = iptr[ii];
                            if (igrp[kk] >= 0)
                            {
                                if (igrp[kk] != igr)
                                    break;
                                coxvars(x, (kk - 1) * nCol, irt, 0, IFRQ, ifix, 0, icen, out _, out _, out xfrq, out _, out _, out xcen, out imiss);
                                caze[kk, 2] = xfrq;
                                smu += xfrq * caze[kk, 5];
                                caze[kk, 4] = xcen;
                                genregs(nCol, x, 1 + (kk - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                                for (iq = 1; iq <= ncoef; iq++)
                                {
                                    OBS[iq] = OBS[iq] + -1.0 * xmean[iq];
                                    smg[iq] = smg[iq] + xfrq * caze[kk, 5] * OBS[iq];
                                }
                            }
                        }
                        smd[1] = 0.0;
                    }
                    if (icen > 0)
                        icnn = Convert.ToInt32(x[icen + (k - 1) * nCol]);
                    // A censored record: it leaves those at risk, and its figures are those of the latest time with events that is not after its own
                    if (icnn == 1)
                    {
                        smu -= caze[k, 2] * caze[k, 5];
                        genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                        for (iq = 1; iq <= ncoef; iq++)
                        {
                            OBS[iq] = OBS[iq] + -1.0 * xmean[iq];
                            smg[iq] = smg[iq] + -caze[k, 2] * caze[k, 5] * OBS[iq];
                        }
                        caze[k, 1] = Math.Exp(-smd[1]);
                        caze[k, 2] = Constant.MISSING;
                        caze[k, 3] = smd[1] * caze[k, 5];
                        caze[k, 4] = smd[1];
                    }
                    // The first to be met of the records with events at this time, which is the one that carries their number.  xfd is the number
                    // of events, by frequency, and tmps the sum of their proportionality constants.  The cumulative hazard rises by the number of
                    // events over the sum for those at risk.
                    else if (idt[k] > 0)
                    {
                        ii = i + 1;
                        double xfd = 0.0;
                        double tmps = 0.0;
                        for (j = 1; j <= idt[k]; j++)
                        {
                            do
                            {
                                ii -= 1;
                                kk = iptr[ii];
                            }
                            while (igrp[kk] < 0);
                            xfd += caze[kk, 2];
                            tmps += caze[kk, 2] * caze[kk, 5];
                        }
                        smd[1] = smd[1] + xfd / smu;
                        ii = i;
                        for (j = 1; j <= ncoef; j++)
                            smh[j] = 0.0;
                        j1 = idt[k];
                        // Each of the records with events at this time in turn.  OBS becomes d, what its regressors differ by from smg / smu, their
                        // mean over those at risk weighted by the hazards; a regressor that was left out of the fit is given 0.  With R'R the
                        // matrix of second derivatives, R'y = d is solved for y, and y'y is d'Vd, the leverage.  smh gathers the sums for the
                        // records that leave those at risk at this time.
                        for (j = 1; j <= j1; j++)
                        {
                            caze[k, 1] = Math.Exp(-smd[1]);
                            caze[k, 3] = caze[k, 5] * smd[1];
                            caze[k, 4] = smd[1];
                            genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                            for (iq = 1; iq <= ncoef; iq++)
                            {
                                OBS[iq] = OBS[iq] + -1.0 * xmean[iq];
                                smh[iq] = smh[iq] + caze[k, 2] * caze[k, 5] * OBS[iq];
                                OBS[iq] = OBS[iq] + -1.0 / smu * smg[iq];
                            }
                            int M;
                            for (M = 1; M <= ncoef; M++)
                                if (cov[M, M] == 0.0)
                                    OBS[M] = 0.0;
                            Regress1.mxinv2(ncoef, cov, OBS, true, true, false, cov, out irank, ref ifault);
                            if (ifault != 0)
                                return;
                            zdot = 0.0;
                            for (iq = 1; iq <= ncoef; iq++)
                                zdot += OBS[iq] * OBS[iq];
                            caze[k, 2] = zdot;
                            if (j != j1)
                            {
                                do
                                {
                                    ii -= 1;
                                    k = iptr[ii];
                                }
                                while (igrp[k] < 0);
                            }
                        }
                        smu -= tmps;
                        for (j = 1; j <= ncoef; j++)
                        {
                            smg[j] = smg[j] + -1.0 * smh[j];
                        }
                    }
                }
            }
            // The covariance matrix of the coefficients is the inverse of the matrix of second derivatives, and is made from R.  The standard errors
            // are the square roots of its diagonal.  A regressor that was left out has a row and column of zeros, and so a standard error of 0.
            Regress1.rcovarb(ncoef, cov, 1.0, cov, ref ifault);
            for (int i = 1; i <= ncoef; i++)
            {
                coef[i, 2] = Math.Sqrt(cov[i, i]);
                coef[i, 3] = UOverD(coef[i, 1], coef[i, 2]);
            }
        }

        ///  <summary>
        ///  COMPUTE HESSIAN, GRADIENT, AND PARAMETER UPDATES
        ///  </summary>
        ///  <param name="nobs">The number of records.</param>
        ///  <param name="nCol">The number of columns of x.</param>
        ///  <param name="x">The data, a record at a time.</param>
        ///  <param name="irt">The column of the times.</param>
        ///  <param name="IFRQ">The column of the frequencies, or 0.</param>
        ///  <param name="ifix">The column of a term with a fixed coefficient of 1, or 0.</param>
        ///  <param name="icen">The column of the censoring codes: 0 for an event, 1 for a censored record.</param>
        ///  <param name="ratio">The ratio for the splitting of a stratum, or -1 for none.</param>
        ///  <param name="nef">The number of effects.</param>
        ///  <param name="nvef">The number of columns of each effect.</param>
        ///  <param name="indef">The columns of the effects.</param>
        ///  <param name="ncoef">The number of coefficients.</param>
        ///  <param name="coef">The coefficients, in column icoef.</param>
        ///  <param name="icoef">The column of coef to use: 1 for the coefficients as they stand, 2 for those that are being tried.</param>
        ///  <param name="ihess">Set for the matrix of second derivatives itself; not set for the one that is quicker to form.</param>
        ///  <param name="algl">On return, the log likelihood.</param>
        ///  <param name="cov">On return, in its upper triangle, the triangular factor R of the matrix: R'R is the matrix.</param>
        ///  <param name="ldcov">No longer used now that MXFAC is never called inside here.</param>
        ///  <param name="xmean">The means of the regressors.</param>
        ///  <param name="caze">On return caze[k, 1] is exp((z - m)'b) for record k, and caze[k, 6] is exp(z'b); columns 2 to 4 are working space.</param>
        ///  <param name="ldcase">Not used.</param>
        ///  <param name="GR">On return, the step: the solution s of R'R s = g, where g is the gradient.</param>
        ///  <param name="OBS">Working space: the regressors of a record.</param>
        ///  <param name="smg">Working space: the sum of u (z - m) over those at risk.</param>
        ///  <param name="smh">Working space: the sum of u (z - m)(z - m)' over those at risk, its upper triangle, a column at a time.</param>
        ///  <param name="iptr">The order in which the records are taken, and then for each regressor 1 if it varies (see coxiter).</param>
        ///  <param name="idt">The marks of the times with events (see coxiter).</param>
        ///  <param name="igrp">The stratum of each record, or -1 for a record that is left out.  A stratum that is split is renumbered here.</param>
        ///  <param name="change">On return, true if a stratum was split.</param>
        ///  <param name="Zero">If set, a regressor whose coefficient is 0 is left out.</param>
        ///  <param name="ifault">
        ///  On return 100 if the matrix cannot be factorised, with the number of the first regressor whose row of it adds up to nothing added to
        ///  the 100 if there is one; the faults of mxinv2; otherwise as it was.  If it is not 0 on entry, nothing is factorised and 100 is returned.
        ///  </param>
        ///  <remarks>
        ///  One pass over the data, at the coefficients b in column icoef of coef.  For each record u = exp((z - m)'b), where m holds the means of
        ///  the regressors: to take the means away changes nothing in the log likelihood, the gradient or the matrix, and keeps the numbers
        ///  small.  (z - m)'b is held between -30 and 30 before its exponential is taken.  The records are taken in the order of iptr, in which
        ///  the records met so far in a stratum are the ones at risk, and three sums are kept over them, each record counting as many times as
        ///  its frequency: smu, of u; smg, of u (z - m); and smh, of u (z - m)(z - m)'.  At a time with d events, in records with regressors
        ///  z1 to zd,
        ///      the log likelihood gains   (z1 - m)'b + ... + (zd - m)'b  -  d ln(smu)
        ///      the gradient gains         (z1 - m) + ... + (zd - m)  -  d smg / smu
        ///      the matrix gains           d (smh / smu  -  (smg / smu)(smg / smu)')
        ///  The matrix is that of the second derivatives of the log likelihood with its sign changed, and is used if ihess is set.  If it is not,
        ///  the matrix gains instead e1 e1' + ... + ed ed', where e = (z - m) - smg / smu is what the regressors of the record with the event
        ///  differ by from their mean over those at risk.  That needs no smh, and serves until the iterations are near the maximum.
        ///
        ///  The matrix is then factorised as R'R (CholeskiFactor) and R'R s = g is solved for the step s, in two stages (mxinv2).  A regressor
        ///  that does not vary within any stratum, or that the factorisation finds to be determined by the regressors before it, is left out:
        ///  its element of the gradient and its row and column of the matrix are cleared, and its element of the step is 0.
        ///
        ///  Splitting.  If ratio is not -1, each stratum is looked over for a time that divides it: one at which every event at that time and
        ///  before it has a proportionality constant more than ratio times the greatest among the records after it.  The records of the earlier
        ///  part (its events, and the censored records whose constants are as great) are then made a stratum of their own, and the pass is made
        ///  again.  The constants are read from column 5 of caze.  During the iterations that column is filled only when there is a fixed term
        ///  (ifix above 0); without one, as in this program, it holds zeros until the fit is over, and no stratum is split.
        ///  </remarks>
        private static void CoxHessian(int nobs, int nCol, double[] x, int irt, int IFRQ, int ifix, int icen, double ratio, int nef, int[] nvef, int[] indef, int ncoef, double[,] coef, int icoef, bool ihess, out double algl, double[,] cov, int ldcov, double[] xmean, double[,] caze, int ldcase, double[] GR, double[] OBS, double[] smg, double[] smh, int[] iptr, int[] idt, int[] igrp, out bool change, bool Zero, ref int ifault)
        {
            int irank = 0;
            int kk = 0;
            int ncoef1 = 0;
            double xmin = 0; double XMax = 0;
            double smu = 0;

            // tolerance
            const double tol = 0.000000000001;
            change = false;
            do
            {
                // the matrix, of which only the upper triangle is formed, the gradient and the log likelihood start at nothing
                int igr = 0;
                for (int i = 1; i <= ncoef; i++)
                    for (int ii = 1; ii <= i; ii++)
                        cov[ii, i] = 0.0;
                for (int i = 1; i <= ncoef; i++)
                    GR[i] = 0.0;
                algl = 0.0;
                for (int i = 1; i <= nobs; i++)
                {
                    int k = iptr[i];
                    if (igrp[k] >= 0)
                    {
                        // a new stratum: nobody is at risk yet
                        if (igrp[k] != igr)
                        {
                            for (int iq = 1; iq <= ncoef; iq++)
                                smg[iq] = 0.0;
                            for (int iq = 1; iq <= ncoef * ncoef; iq++)
                                smh[iq] = 0.0;
                            smu = 0.0;
                            igr = igrp[k];
                        }
                        coxvars(x, (k - 1) * nCol, irt, 0, IFRQ, ifix, 0, icen, out double _, out double _, out double xfrq, out double xfix, out double _, out double xcen, out int nrmiss);
                        if (xfrq >= 0.0)
                        {
                            int icnn = Convert.ToInt32(xcen);
                            genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef1, OBS, ref nrmiss, ref ifault);
                            double zdot = 0.0;
                            double zdot_base = 0.0;
                            for (int iq = 1; iq <= ncoef; iq++)
                            {
                                zdot_base += coef[iq, icoef] * OBS[iq];
                                OBS[iq] = OBS[iq] - xmean[iq];
                                zdot += coef[iq, icoef] * OBS[iq];
                            }
                            double xx = xfix + zdot;
                            double xx_base = xfix + zdot_base;
                            // a record with an event: its own part of the gradient and of the log likelihood
                            if (icnn == 0)
                            {
                                for (int iq = 1; iq <= ncoef; iq++)
                                    GR[iq] = GR[iq] + xfrq * OBS[iq];
                                algl += xfrq * Math.Min(xx, 30.0);
                            }
                            xx = Math.Max(-30.0, Math.Min(xx, 30.0));
                            xx_base = Math.Max(-30.0, Math.Min(xx_base, 30.0));
                            double u = Math.Exp(xx);
                            caze[k, 1] = u;
                            caze[k, 4] = xcen;
                            if (ifix > 0)
                            {
                                caze[k, 5] = caze[k, 1] / Math.Exp(xfix);
                                caze[k, 6] = Math.Exp(xx_base) / Math.Exp(xfix);
                            }
                            else
                            {
                                caze[k, 6] = Math.Exp(xx_base);
                            }
                            // the record joins those at risk
                            smu += xfrq * u;
                            for (int iq = 1; iq <= ncoef; iq++)
                                smg[iq] = smg[iq] + xfrq * u * OBS[iq];
                            double xtmp;
                            if (ihess)
                            {
                                for (int j = 1; j <= ncoef; j++)
                                {
                                    xtmp = xfrq * u * OBS[j];
                                    for (int ii = 1; ii <= j; ii++)
                                        smh[ii + (j - 1) * ncoef] = smh[ii + (j - 1) * ncoef] + OBS[ii] * xtmp;
                                }
                            }
                            // The last of the records with events at this time: everybody who is at risk at this time has now been met.  Each of
                            // the jj records with events is taken in turn, this one first and then those before it in the order, for the part
                            // that the sums over those at risk play in the log likelihood, the gradient and the matrix.
                            if (idt[k] > 0)
                            {
                                int M = i;
                                int jj = idt[k];
                                for (int j = 1; j <= jj; j++)
                                {
                                    algl -= xfrq * Math.Log(smu);
                                    for (int iq = 1; iq <= ncoef; iq++)
                                        GR[iq] = GR[iq] + -xfrq / smu * smg[iq];
                                    if (!ihess)
                                    {
                                        for (int iq = 1; iq <= ncoef; iq++)
                                            OBS[iq] = OBS[iq] + -1.0 / smu * smg[iq];
                                        for (int L = 1; L <= ncoef; L++)
                                        {
                                            xtmp = xfrq * OBS[L];
                                            for (int ii = 1; ii <= L; ii++)
                                                cov[ii, L] = cov[ii, L] + OBS[ii] * xtmp;
                                        }
                                    }
                                    else
                                    {
                                        double tmp = xfrq / smu;
                                        for (int L = 1; L <= ncoef; L++)
                                        {
                                            xtmp = -tmp * smg[L] / smu;
                                            for (int ii = 1; ii <= L; ii++)
                                            {
                                                cov[ii, L] = cov[ii, L] + smh[ii + (L - 1) * ncoef] * tmp;
                                                cov[ii, L] = cov[ii, L] + smg[ii] * xtmp;
                                            }
                                        }
                                    }
                                    if (j != jj)
                                    {
                                        do
                                        {
                                            M -= 1;
                                            k = iptr[M];
                                        }
                                        while (igrp[k] < 0);
                                        if (IFRQ > 0)
                                            xfrq = x[IFRQ + (k - 1) * nCol];
                                        genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef1, OBS, ref nrmiss, ref ifault);
                                        for (int iq = 1; iq <= ncoef; iq++)
                                            OBS[iq] = OBS[iq] - xmean[iq];
                                    }
                                }
                            }
                        }
                    }
                }
                // Splitting (see the remarks): column 2 of caze becomes the greatest constant so far in the stratum, and column 3 the least
                // among the events from here on
                igr = 0;
                bool strat = false;
                if (ratio != -1.0)
                {
                    for (int i = 1; i <= nobs; i++)
                    {
                        int k = iptr[i];
                        if (igrp[k] >= 0)
                        {
                            if (igr != igrp[k])
                            {
                                igr = igrp[k];
                                XMax = -1.0E+30;
                            }
                            XMax = Math.Max(XMax, caze[k, 5]);
                            caze[k, 2] = XMax;
                        }
                    }
                    igr = 0;
                    for (int i = nobs; i >= 1; i--)
                    {
                        int k = iptr[i];
                        if (igrp[k] >= 0 & Convert.ToInt64(caze[k, 4]) == 0)
                        {
                            if (igr != igrp[k])
                            {
                                igr = igrp[k];
                                xmin = 1.0E+30;
                            }
                            xmin = Math.Min(xmin, caze[k, 5]);
                            caze[k, 3] = xmin;
                        }
                    }
                    igr = 0;
                    for (int i = 1; i < nobs; i++)
                    {
                        int k = iptr[i];
                        if (igrp[k] >= 0 & Convert.ToInt64(caze[k, 4]) == 0)
                        {
                            igr = igrp[k];
                            int j = i;
                            bool ok = true;
                            do
                            {
                                j++;
                                if (j > nobs)
                                {
                                    ok = false;
                                    break;
                                }
                                kk = iptr[j];
                            }
                            while (igrp[kk] < 0 || Convert.ToInt64(caze[kk, 4]) != 0);
                            if (igrp[kk] != igr)
                                ok = false;
                            if (ok)
                            {
                                if (caze[kk, 3] > ratio * caze[k, 2])
                                {
                                    XMax = caze[k, 2];
                                    int iimax = 1;
                                    int imax = igrp[1];
                                    for (int iq = 1; iq <= nobs; iq++)
                                    {
                                        if (igrp[iq] > imax)
                                        {
                                            iimax = iq;
                                            imax = igrp[iq];
                                        }
                                    }
                                    int ngrp = igrp[iimax] + 1;
                                    strat = true;
                                    change = true;
                                    j = i + 1;
                                    for (int ii = j; ii <= nobs; ii++)
                                    {
                                        k = iptr[ii];
                                        if (igrp[k] >= 0 & igrp[k] == igr)
                                        {
                                            if (Convert.ToInt64(caze[k, 4]) == 0)
                                            {
                                                igrp[k] = ngrp;
                                            }
                                            else
                                            {
                                                if (caze[k, 5] >= ratio * XMax)
                                                {
                                                    igrp[k] = ngrp;
                                                }
                                                else
                                                {
                                                    int it = iptr[j];
                                                    iptr[j] = k;
                                                    for (int jj = j + 1; jj <= ii; jj++)
                                                    {
                                                        int jt = iptr[jj];
                                                        iptr[jj] = it;
                                                        it = jt;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (!strat)
                    break;
            }
            while (true);
            // a regressor that does not vary within any stratum is left out
            for (int i = 1; i <= ncoef; i++)
            {
                if (iptr[nobs + i] == 0 || (Zero && (coef[i, icoef] == 0.0)))
                {
                    GR[i] = 0.0;
                    for (int ii = 1; ii <= i; ii++)
                        cov[ii, i] = 0.0;
                    for (int ii = i + 1; ii <= ncoef; ii++)
                        cov[i, ii] = 0.0;
                }
            }
            // Call MXFAC(ncoef, cov(1, 1), ldcov, 100# * 0.000000119237, irank, cov(1, 1), ldcov, ifault)
            CholeskiFactor(ncoef, cov, cov, tol, ref irank, ref ifault);
            if (ifault != 0)
            {
                // force a convergence failure if the matrix does not decompose fully
                // find the predictor that caused singularity and add it to the error code for reporting
                ifault = 100;
                for (int i = 1; i <= ncoef; i++)
                {
                    double xcovx = 0.0;
                    for (int ii = 1; ii <= ncoef; ii++)
                        xcovx += cov[i, ii];
                    if (Math.Abs(xcovx) < tol)
                    {
                        ifault += i;
                        break;
                    }
                }
                return;
            }
            // so is a regressor that varies, but that the factorisation has found to be determined by the regressors before it
            for (int i = 1; i <= ncoef; i++)
            {
                if (cov[i, i] == 0.0 & iptr[nobs + i] == 1)
                {
                    GR[i] = 0.0;
                    for (int ii = 1; ii <= i; ii++)
                        cov[ii, i] = 0.0;
                    for (int ii = i + 1; ii <= ncoef; ii++)
                        cov[i, ii] = 0.0;
                }
            }
            // R'y = g is solved for y, and then R s = y for the step s
            Regress1.mxinv2(ncoef, cov, GR, true, true, false, cov, out irank, ref ifault);
            if (ifault != 0)
                return;
            Regress1.mxinv2(ncoef, cov, GR, true, false, false, cov, out irank, ref ifault);
        }

        ///  <summary>
        ///  Whether a variable takes two values and no more.
        ///  </summary>
        ///  <param name="Variable">The variable.</param>
        ///  <param name="cd">A ColumnData structure whose Groups field is filled in with the groups if the variable is binary, and ignored otherwise</param>
        ///  <returns></returns>
        ///  <remarks>TODO: This used to set bins to 1 if only 1 bin, 99 if >2 bins.  Was this ever used?</remarks>
        private static bool IsBinary(DoubleVariable Variable, ColumnData cd)
        {

            if (Variable.Length < 2)
                return false;

            double x1 = Variable.Data[0];
            double x2 = 0;
            int i;
            for (i = 1; i < Variable.Length; i++)
            {
                if (Variable.Data[i] != x1)
                {
                    x2 = Variable.Data[i];
                    break;
                }
            }
            if (i >= Variable.Length)
                return false;

            bool ok = true;
            for (i = 1; i < Variable.Length; i++)
            {
                if (Variable.Data[i] != x1 && Variable.Data[i] != x2)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                // need to sort x1 and x2 in alphanumeric order as the encoded group identifier gets sorted before plotting
                if (x2 < x1)
                    Utilities.Utilities.Swap(ref x2, ref x1);
                cd.Groups = new List<Group> { new Group(x1.ToString(), x1), new Group(x2.ToString(), x2) };
            }
            return ok;
        }

        ///  <summary>
        ///  upper triangular factorization of a positive real definite symmetric matrix by Choleski square root method
        ///  </summary>
        ///  <param name="n">The order of the matrix.</param>
        ///  <param name="a">The matrix, of which the upper triangle is read.</param>
        ///  <param name="r">On return R, upper triangular with zeros below the diagonal: R'R is the matrix.  It may be the same array as a.</param>
        ///  <param name="tol">A diagonal element of R whose square is no more than tol of the element of the matrix that it came from is taken as 0.</param>
        ///  <param name="irank">On return, the number of diagonal elements of R that are not 0.</param>
        ///  <param name="ifault">
        ///  On entry not 0, nothing is done.  On return 1 if tol is not between 0 and 1, and 2 if the matrix is not one that such a factor can
        ///  be found for: the square of a diagonal element comes out below 0, or an element beside a diagonal element of 0 is not itself next to 0.
        ///  </param>
        ///  <remarks>
        ///  R is formed a column at a time: element (k, j) is what is left of element (k, j) of the matrix, when the products of the elements of
        ///  columns k and j above row k have been taken from it, divided by the diagonal element of column k; and the square of the diagonal
        ///  element of column j is what is left of the diagonal element of the matrix when the squares of the elements above it have been taken
        ///  from it.  LAPACK's DPOTRF does this for a matrix that is positive definite, and stops at a diagonal element that is not above 0.  This
        ///  routine goes on: a diagonal element of 0 belongs to a regressor that is determined by those before it, whose row of R is left as
        ///  zeros, so that the solution has 0 for it (see mxinv2 in Regress1.cs).
        ///  </remarks>
        private static void CholeskiFactor(int n, double[,] a, double[,] r, double tol, ref int irank, ref int ifault)
        {
            // check tolerance
            if (tol < 0.0 || tol > 1.0)
                ifault = 1;
            if (ifault != 0)
                return;

            // take a copy of the upper triangle of the symmetric matrix
            for (int j = 1; j <= n; j++)
                for (int ii = 1; ii <= j; ii++)
                    r[ii, j] = a[ii, j];

            // decompose by Choleski's square root method
            // s gathers the squares of the elements of column j above the diagonal, and x is the allowance for an element that should be 0
            int info = 0;
            irank = 0;
            for (int j = 1; j <= n; j++)
            {
                double s = 0.0;
                double x = tol * Math.Sqrt(Math.Abs(r[j, j]));
                for (int k = 1; k < j; k++)
                {
                    double vvdot = 0.0;
                    for (int ii = 1; ii < k; ii++)
                        vvdot += r[ii, k] * r[ii, j];
                    double t = r[k, j] - vvdot;
                    if (r[k, k] != 0.0)
                    {
                        t /= r[k, k];
                        r[k, j] = t;
                        s += t * t;
                    }
                    else
                    {
                        // the diagonal element of column k is 0, and the whole of its row must be
                        if (info == 0)
                        {
                            if (Math.Abs(t) > x * EuclideanNorm(k - 1, r))
                                info = j;
                        }
                        r[k, j] = 0.0;
                    }
                }
                s = r[j, j] - s;
                if (Math.Abs(s) <= tol * Math.Abs(r[j, j]))
                {
                    s = 0.0;
                }
                else if (s < 0.0)
                {
                    s = 0.0;
                    if (info == 0)
                        info = j;
                }
                else
                {
                    irank += 1;
                }
                r[j, j] = Math.Sqrt(s);
            }
            if (info != 0)
                ifault = 2;

            // fill the lower triangle with zeros
            for (int i = 1; i < n; i++)
                for (int ii = i + 1; ii <= n; ii++)
                    r[ii, i] = 0.0;
        }

        ///  <summary>
        ///  euclidean norm of a vector in a matrix
        ///  = sqr(x'*x)
        ///  </summary>
        ///  <param name="idx">The number of elements.</param>
        ///  <param name="x">The matrix: the vector is the first idx elements of its column idx + 1.</param>
        ///  <returns>The square root of the sum of the squares of the elements.</returns>
        ///  <remarks>
        ///  The elements are scaled by the largest of them as the sum is formed, so that the squares cannot overflow; the BLAS routine DNRM2 is of
        ///  this kind.
        ///  </remarks>
        private static double EuclideanNorm(int idx, double[,] x)
        {
            double norm;

            int col = idx + 1;
            if (idx < 1)
            {
                norm = 0.0;
            }
            else if (idx == 1)
            {
                norm = Math.Abs(x[1, col]);
            }
            else
            {
                double scal = 0.0;
                double ssq = 1.0;
                int i;
                for (i = 1; i <= idx; i++)
                {
                    if (x[i, col] != 0.0)
                    {
                        double absxi = Math.Abs(x[i, col]);
                        if (scal < absxi)
                        {
                            ssq = 1.0 + ssq * Math.Pow(scal / absxi, 2.0);
                            scal = absxi;
                        }
                        else
                        {
                            ssq += Math.Pow(absxi / scal, 2.0);
                        }
                    }
                }
                norm = scal * Math.Sqrt(ssq);
            }
            return norm;
        }

        ///  <summary>
        ///  GET QUOTIENT U/D
        ///  </summary>
        ///  <param name="u">The numerator.</param>
        ///  <param name="d">The denominator.</param>
        ///  <remarks>
        ///  The quotient is the missing value if either is missing, or if both are 0.  A quotient too great to be held is given as the greatest
        ///  number that can be, with its sign, and one too small as 0.
        ///  </remarks>
        private static double UOverD(double u, double d)
        {
            if (u == Constant.MISSING || d == Constant.MISSING)
                return Constant.MISSING;

            double absden = Math.Abs(d);
            if (absden <= 1.0)
            {
                const double BIG = double.MaxValue;
                if (Math.Abs(u) < BIG * absden)
                    return u / d;
                if (u == 0.0)
                    return Constant.MISSING;
                if (d >= 0.0)
                    return u >= 0.0
                        ? double.MaxValue
                        : -double.MaxValue;
                return u >= 0.0
                    ? -double.MaxValue
                    : double.MaxValue;
            }
            const double small = Constant.SPREAL;
            if (Math.Abs(u) >= small * absden)
                return u / d;
            return 0.0;
        }

        /// <summary>
        /// Picks out of a record its time, its frequency, its censoring code and the other quantities that have columns of their own.
        /// </summary>
        /// <param name="x">The data, a record at a time.</param>
        /// <param name="ix1">The number of elements of x before the record.</param>
        /// <remarks>
        /// irt, ilt, IFRQ, ifix, ipar and icen are the columns of the time, a second time, the frequency, the fixed term, a parameter and the
        /// censoring code, or 0 for one that there is not, which is then given as 0 (the frequency and the parameter as 1).  nrmiss is the number
        /// of them that are missing in the record.  This program has columns for the time, the censoring code and, sometimes, the frequency.
        /// </remarks>
        private static void coxvars(double[] x, int ix1, int irt, int ilt, int IFRQ, int ifix, int ipar, int icen, out double xrt, out double xlt, out double xfrq, out double xfix, out double xpar, out double xcen, out int nrmiss)
        {
            //  GET SPECIAL VARIABLES
            nrmiss = 0;
            if (ipar > 0)
            {
                xpar = x[ix1 + ipar];
                if (xpar == Constant.MISSING)
                    nrmiss += 1;
            }
            else
            {
                xpar = 1.0;
            }
            if (IFRQ > 0)
            {
                xfrq = x[ix1 + IFRQ];
                if (xfrq == Constant.MISSING)
                    nrmiss += 1;
            }
            else
            {
                xfrq = 1.0;
            }
            if (ifix > 0)
            {
                xfix = x[ix1 + ifix];
                if (xfix == Constant.MISSING)
                    nrmiss += 1;
            }
            else
            {
                xfix = 0.0;
            }
            if (icen > 0)
            {
                xcen = x[ix1 + icen];
                if (xcen == Constant.MISSING)
                    nrmiss += 1;
            }
            else
            {
                xcen = 0.0;
            }
            if (ilt > 0)
            {
                xlt = x[ix1 + ilt];
                if (xlt == Constant.MISSING)
                    nrmiss += 1;
            }
            else
            {
                xlt = 0.0;
            }
            if (irt > 0)
            {
                xrt = x[ix1 + irt];
                if (xrt == Constant.MISSING)
                    nrmiss += 1;
            }
            else
            {
                xrt = 0.0;
            }
        }

        /// <summary>
        /// Transposes, in place, a matrix of m rows and n columns that is held a column at a time, from element 1 of a: the data held a column
        /// at a time become the data held a record at a time, and the other way about.
        /// </summary>
        /// <remarks>
        /// To transpose in place is to send each element to the place of another, which sends the elements round in loops.  Each loop is
        /// followed with its companion, the loop of the places counted from the other end; move records the places that have been dealt with.
        /// ifault is 3 if the search for a loop not yet followed runs out.
        /// </remarks>
        private static void MatrixTranspose1D(int m, int n, double[] a, out int ifault)
        {
            //      adapted from CACM Algorithm 380 - in situ transpose of a rectangular matrix

            //      a is a one-dimensional array of length mn = m*n, which
            //      contains the m x n matrix to be stored columnwise.

            int i; // TODO: Refactor, this is not trivial.
            ifault = 0;
            if (m < 2 || n < 2)
                return;
            int mn = m * n;
            int iwrk = (int)Math.Floor((double)(m + n) / 2);
            int[] move = new int[iwrk + 1];

            if (m == n)
            {
                //  if matrix is square, exchange elements a(i,j) and a(j,i).
                int n1 = n - 1;
                for (i = 1; i <= n1; i++)
                {
                    int J1 = i + 1;
                    for (int j = J1; j <= n; j++)
                    {
                        int i1 = i + (j - 1) * n;
                        int i2 = j + (i - 1) * m;
                        double b = a[i1];
                        a[i1] = a[i2];
                        a[i2] = b;
                    }
                }
                return;
            }

            int nCount = 2;
            int k = mn - 1;
            for (i = 1; i <= iwrk; i++)
                move[i] = 0;

            if (m >= 3 && n >= 3)
            {
                //  calculate the number of fixed points via Euclid's algorithm
                int ir2 = m - 1;
                int ir1 = n - 1;
                do
                {
                    int ir0 = ir2 % ir1;
                    ir2 = ir1;
                    ir1 = ir0;
                    if (ir0 == 0)
                        break;
                }
                while (true);
                nCount = nCount + ir2 - 1;
            }

            //  set initial values for search
            i = 1;
            int im = m;

            //  at least one loop must be re-arranged - so jump in at rearrangement point
            bool jump = true;
            bool rearrange = true;

            //  search for loops to rearrange
            do
            {
                int i1divn;
                if (jump == false)
                {
                    int max = k - i;
                    i += 1;
                    if (i > max)
                    {
                        ifault = 3;
                        return;
                    }
                    im += m;
                    if (im > k)
                    {
                        im -= k;
                    }
                    int i2 = im;
                    if (i == i2)
                    {
                        rearrange = false;
                    }
                    else if (i > iwrk)
                    {
                        if (i2 > i && i2 < max)
                        {
                            int i1 = i2;
                            do
                            {
                                i1divn = (int)Math.Floor((double)i1 / n);
                                i2 = m * (i1 - n * i1divn) + i1divn;
                                if (i2 <= i || i2 >= max)
                                {
                                    break;
                                }
                                i1 = i2;
                            }
                            while (true);
                        }
                        rearrange = i2 == i;
                    }
                    else
                    {
                        rearrange = move[i] == 0;
                    }
                }
                else
                {
                    jump = false;
                }
                //  rearrange the elements of a loop and its companion loop
                if (rearrange)
                {
                    int i1 = i;
                    int kmi = k - i;
                    double b = a[i1 + 1];
                    int i1c = kmi;
                    double C = a[i1c + 1];
                    do
                    {
                        i1divn = (int)Math.Floor((double)i1 / n);
                        int i2 = m * (i1 - n * i1divn) + i1divn;
                        int i2c = k - i2;
                        if (i1 <= iwrk)
                            move[i1] = 2;
                        if (i1c <= iwrk)
                            move[i1c] = 2;
                        nCount += 2;
                        if (i2 == i)
                        {
                            a[i1 + 1] = b;
                            a[i1c + 1] = C;
                            break;
                        }
                        if (i2 == kmi)
                        {
                            double D = b;
                            b = C;
                            C = D;
                            a[i1 + 1] = b;
                            a[i1c + 1] = C;
                            break;
                        }
                        a[i1 + 1] = a[i2 + 1];
                        a[i1c + 1] = a[i2c + 1];
                        i1 = i2;
                        i1c = i2c;
                    }
                    while (true);
                }
                //  check for finish
                if (nCount >= mn)
                    break;
            }
            while (true);
        }

        ///  <summary>
        ///  solve for alpha by Newton Raphson iteration - see Kalbfleisch and Prentice P293
        ///  </summary>
        ///  <param name="dead_theta">The hazard ratio of each record with an event at the time.</param>
        ///  <param name="dead_frequency">The number of subjects that each of those records stands for.</param>
        ///  <param name="dead">The number of deaths: the sum of dead_frequency.</param>
        ///  <param name="risk_theta">The sum of the hazard ratios of the subjects at risk.</param>
        ///  <returns>alpha, or the missing value if it was not found in 300 iterations.</returns>
        ///  <remarks>
        ///  The iteration is on h = -ln(alpha), which is the rise in the cumulative hazard at this time.  The sum over the deaths of
        ///  theta / (1 - exp(-theta h)) falls as h rises and is convex, and it is no less than dead / h, so that at h = dead / risk_theta it is
        ///  no less than risk_theta: from there the iteration climbs to the root without passing it, and alpha is never more than exp(-h).
        ///  Carried out on alpha itself, as it was, the iteration failed when few were left at risk beside those who died.
        ///  </remarks>
        private static double AlphaSolve(List<double> dead_theta, List<double> dead_frequency, double dead, double risk_theta)
        {
            const double tol = 0.000000001;
            double h = dead / risk_theta;
            double stp = double.MaxValue;

            for (int iter = 0; iter < 300; iter++)
            {
                // once exp(-h) is nothing beside 1, so is alpha
                if (Math.Exp(-h) < 1.0E-16)
                    return Math.Exp(-h);
                double gi = 0.0;
                double gi1 = 0.0;
                for (int i = 0; i < dead_theta.Count; i++)
                {
                    double xii = Math.Exp(-dead_theta[i] * h);
                    gi += dead_frequency[i] * dead_theta[i] / (1.0 - xii);
                    gi1 += dead_frequency[i] * xii * Math.Pow(dead_theta[i] / (1.0 - xii), 2.0);
                }
                double next = (gi - risk_theta) / gi1;
                if (double.IsNaN(next) || double.IsInfinity(next) || h + next <= 0.0)
                    break;
                stp = next;
                h += stp;
                // the step is small beside h, or the change that it makes to alpha is nothing beside 1
                if (Math.Abs(stp) <= tol * Math.Max(1.0, h) || Math.Exp(-h) * Math.Abs(stp) <= 1.0E-12)
                    return Math.Exp(-h);
            }
            // where the sum is almost level, rounding in it can keep the steps from becoming as small as that: if the last was small all the
            // same, h is as good as the arithmetic allows
            return Math.Abs(stp) <= 0.00001 * Math.Max(1.0, h) ? Math.Exp(-h) : Constant.MISSING;
        }

        /// <summary>
        /// The baseline survival and the baseline cumulative hazard at the time of each record, put into S and H of the records.
        /// </summary>
        /// <param name="z">The records, from element 1.  On return they are in order of stratum, then time, then hazard ratio from the greatest down.</param>
        /// <param name="iobs">The number of records.</param>
        /// <returns>The number of strata.</returns>
        private static int BaselineSurvivalAndHazard(CoxP[] z, int iobs)
        {
            Array.Sort(z, 1, iobs, new CoxpByStratumTimeThenExb());
            int lastStratum = z[1].Stratum;
            double alpha_product = 1.0;
            double alpha_productx = 1.0;
            int istrata = 1;

            for (int i = 1; i <= iobs; i++)
            {
                if (z[i].Stratum != lastStratum)
                {
                    //  new stratum
                    alpha_product = 1.0;
                    alpha_productx = 1.0;
                    lastStratum = z[i].Stratum;
                    istrata += 1;
                }
                // the records at this time, iinc of them; of those with an event, the hazard ratios and the numbers of subjects they stand for
                double watch_time = z[i].Time;
                List<double> dead_theta = new();
                List<double> dead_frequency = new();
                double dead = 0.0;
                int iinc = 0;
                for (int j = i; j <= iobs; j++)
                {
                    if (z[i].Stratum != z[j].Stratum)
                        break;
                    if (z[j].Time != watch_time)
                        break;
                    if (z[j].Censor != 0.0)
                    {
                        dead += z[j].Frequency;
                        dead_theta.Add(z[j].Exb);
                        dead_frequency.Add(z[j].Frequency);
                    }
                    iinc += 1;
                }
                // those at risk are the records of the stratum from here on, which have this time or a later one
                double risk_theta = 0.0;
                double atRisk = 0.0;
                for (int j = i; j <= iobs; j++)
                {
                    if (z[i].Stratum != z[j].Stratum)
                        break;
                    risk_theta += z[j].Frequency * z[j].Exb;
                    atRisk += z[j].Frequency;
                }
                bool erra = false;
                double alpha_i;
                double alpha_ix;
                if (dead == 0.0)
                {
                    alpha_i = 1.0;
                    alpha_ix = alpha_i;
                }
                else if (dead == atRisk)
                {
                    // everyone still at risk dies at this time: the product-limit survival falls to 0 (the tied-death equation has no root in (0, 1))
                    alpha_i = 0.0;
                    alpha_ix = Math.Exp(-dead / risk_theta);
                }
                else if (dead == 1.0)
                {
                    // one death: the hazard ratio is that of the subject who died, who need not be the first of the records at this time
                    alpha_i = Math.Pow(1.0 - dead_theta[0] / risk_theta, 1.0 / dead_theta[0]);
                    alpha_ix = Math.Exp(-dead / risk_theta);
                }
                else
                {
                    alpha_ix = Math.Exp(-dead / risk_theta);
                    alpha_i = AlphaSolve(dead_theta, dead_frequency, dead, risk_theta);
                    erra = alpha_i == Constant.MISSING;
                }
                //  use a non-iterative solution for the hazard - see Stata manual
                alpha_productx *= alpha_ix;
                if (erra == false)
                {
                    alpha_product *= alpha_i;
                    for (int j = i; j < i + iinc; j++)
                    {
                        z[j].S = alpha_product;
                        z[j].H = -Math.Log(alpha_productx);
                    }
                }
                i += iinc - 1;
            }
            return istrata;
        }

        /// <summary>The baseline survival and cumulative hazard at each time with an event, as a report.</summary>
        public static StepOutput RptCoxBaselineToReport(ParameterBag parameters)
        {
            return RptCoxBaseline(parameters, false, string.Empty, false);
        }

        /// <summary>The baseline survival and cumulative hazard at the time of each record, and its hazard ratio, saved to the worksheet.</summary>
        public static StepOutput RptCoxBaselineToWorksheet(ParameterBag parameters)
        {
            return RptCoxBaseline(parameters, false, string.Empty, true);
        }

        /// <summary>
        /// The plots of survival and of cumulative hazard against time, with a line for each stratum or for each value of the binary predictor
        /// that was chosen, or a single line.
        /// </summary>
        public static StepOutput RptCoxHazardPlots(ParameterBag parameters)
        {
            bool[] selectedGroups = (bool[])parameters["group"].AsObject;
            DataFrame subgroupsFrame = parameters["subgroups"].AsDataFrame;
            StringVariable subgroupsVariable = (StringVariable)subgroupsFrame.Variables[0];
            for (int i = 0; i < selectedGroups.Length; i++)
            {
                if (selectedGroups[i])
                {
                    string selectedGroup = subgroupsVariable.Data[i];
                    return RptCoxBaseline(parameters, true, selectedGroup, false);
                }
            }
            return StepOutput.Empty();
        }

        /// <summary>
        /// The baseline survival and cumulative hazard, for the report, for the worksheet or for the plots.
        /// </summary>
        /// <param name="parameters">What RptCoxRegression passed on.</param>
        /// <param name="plot">True to make the plots.</param>
        /// <param name="groupVar">For the plots: "Strata", the title of a binary predictor, or "None".</param>
        /// <param name="createGrid">True to save the figures to the worksheet.</param>
        /// <remarks>
        /// The baseline is a subject whose predictors are all 0, which for a predictor that was centred is its mean.  The report has a row for
        /// each time at which there is an event, stratum by stratum, and gives with it the hazard ratio of the record that has the greatest
        /// among those with events at that time.
        /// </remarks>
        private static StepOutput RptCoxBaseline(ParameterBag parameters, bool plot, string groupVar, bool createGrid)
        {
            int i;
            double watch_time;

            double[,] ARR2 = (double[,])parameters["ARR2"].AsObject;

            //  baseline S and H and S and H values at mean covariate
            int iobs = Convert.ToInt32(ARR2[0, 0]);
            CoxP[] z = new CoxP[iobs + 2];
            z[0] = new CoxP();
            for (i = 1; i <= iobs; i++)
            {
                //  use estimates as starting values if needed
                z[i] = new CoxP
                {
                    Stratum = Convert.ToInt32(ARR2[i, 9]),
                    Time = ARR2[i, 6],
                    Censor = Convert.ToInt32(ARR2[i, 7]),
                    S = ARR2[i, 1],
                    H = ARR2[i, 4],
                    Exb = ARR2[i, 10],
                    Frequency = ARR2[i, 11],
                    Index = i
                };
            }

            int istrata = BaselineSurvivalAndHazard(z, iobs);

            ParameterBag outputParameters = new();
            if (!plot)
            {
                // write to report in time-sorted order
                watch_time = Constant.MISSING;
                int watchStratum = 0;
                IList<ParameterBag> timeList = new List<ParameterBag>();
                outputParameters.AddOutput("*time", timeList);
                for (i = 1; i <= iobs; i++)
                {
                    // a row for each time with an event, in each stratum
                    if (z[i].Censor != 0.0 & (watch_time != z[i].Time || watchStratum != z[i].Stratum))
                    {
                        watch_time = z[i].Time;
                        watchStratum = z[i].Stratum;
                        ParameterBag timeParameters = new();
                        timeList.Add(timeParameters);
                        timeParameters.AddOutput("time", z[i].Time);
                        timeParameters.AddOutput("sur", z[i].S);
                        timeParameters.AddOutput("haz", z[i].H);
                        timeParameters.AddOutput("hr", z[i].Exb);
                    }
                }
            }

            // restore the original record order if calling plot function or output to worksheet
            if (plot || createGrid)
                Array.Sort(z, 1, iobs, new CoxpByIndex());

            if (plot)
            {
                // bypass reporting and plot if called by the plot function
                CoxPlot(parameters, z, iobs, istrata, groupVar, outputParameters);
            }
            else if (createGrid)
            {
                // save to worksheet if requested: each value in the row of its record, the row of a record that was left out of the regression
                // for a missing value being left empty
                int records = parameters["coxRecords"].AsInt32;
                int[] recordOf = (int[])parameters["coxRecordOf"].AsObject;
                DoubleVariable survivalVariable = MissingVariable(records, "Survival (baseline)");
                DoubleVariable hazardVariable = MissingVariable(records, "Hazard (baseline cumulative)");
                DoubleVariable hazardRatioVariable = MissingVariable(records, "Hazard ratio");
                DataFrame resultsFrame = new();
                resultsFrame.Variables.Add(survivalVariable);
                resultsFrame.Variables.Add(hazardVariable);
                resultsFrame.Variables.Add(hazardRatioVariable);
                for (i = 1; i <= iobs; i++)
                {
                    survivalVariable.SetData(recordOf[i - 1], z[i].S);
                    hazardVariable.SetData(recordOf[i - 1], z[i].H);
                    hazardRatioVariable.SetData(recordOf[i - 1], z[i].Exb);
                }
                outputParameters.AddOutput("results", resultsFrame);
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Makes the plots of survival and of cumulative hazard and, when they are split by a binary predictor, the plot of -ln(-ln(survival))
        /// against ln(time), in which the lines of the two groups run side by side if their hazards are proportional.
        /// </summary>
        /// <remarks>
        /// When the plots are split by a binary predictor, the survival of a group is the baseline survival to the power exp(b v), where v is
        /// the value that the predictor has in the group and b is its coefficient.
        /// </remarks>
        private static void CoxPlot(ParameterBag parameters, CoxP[] z, int iobs, int istrata, string groupVar, ParameterBag outputParameters)
        {
            int igroups;
            int groupid = 0;
            bool grouped; bool stratified;

            double[,] ARR2 = (double[,])parameters["ARR2"].AsObject;
            double[,,] ARR3 = (double[,,])parameters["ARR3"].AsObject;
            ColumnData[] CDAT1 = (ColumnData[])parameters["CDAT1"].AsObject;
            double[,] holdx = (double[,])parameters["holdx"].AsObject;
            bool use_tic = parameters["use-tics"].AsBoolean;
            bool use_marker = parameters["use-markers"].AsBoolean;

            int ncoef = Convert.ToInt32(ARR2[1, 0]);
            IComparer<CoxP> comparer;
            switch (groupVar.ToLower(CultureInfo.InvariantCulture))
            {
                case "none":
                case "":
                    stratified = false;
                    grouped = false;
                    igroups = 0;
                    comparer = new CoxpByTm();
                    break;
                case "strata":
                    stratified = true;
                    grouped = false;
                    igroups = 0;
                    comparer = new CoxpByStratumThenTime();
                    break;
                default:
                    stratified = false;
                    groupid = -1;
                    igroups = 0;
                    for (int i = 1; i <= ncoef; i++)
                    {
                        if (CDAT1[i].Title.Trim().ToLower(CultureInfo.CurrentCulture).Equals(groupVar.ToLower(CultureInfo.CurrentCulture)))
                        {
                            groupid = i;
                            igroups = CDAT1[i].Groups.Count;
                            break;
                        }
                    }
                    if (groupid >= 0)
                    {
                        grouped = true;
                        comparer = new CoxpByIdThenTm();
                    }
                    else
                    {
                        grouped = false;
                        comparer = new CoxpByTm();
                    }
                    break;
            }


            // set group indicator
            if (grouped)
            {
                for (int i = 1; i <= iobs; i++)
                    z[i].Id = Convert.ToInt32(holdx[i, groupid]);
            }
            else
            {
                for (int i = 1; i <= iobs; i++)
                    z[i].Id = 1;
            }

            //  TODO: The original SD2 code removed anything other than the first sort in the order - should we also do that?
            Array.Sort(z, 1, iobs, comparer);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            ParameterBag cox1Parameters = new();
            chartList.Add(cox1Parameters);
            cox1Parameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.CoxSurvivalOrHazard, new CoxSurvivalOrHazardOptions(z, iobs, istrata, CoxPlotMode.Survival, igroups, groupid, grouped, stratified, ARR3, CDAT1, use_tic, use_marker)));
            cox1Parameters = new ParameterBag();
            chartList.Add(cox1Parameters);
            cox1Parameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.CoxSurvivalOrHazard, new CoxSurvivalOrHazardOptions(z, iobs, istrata, CoxPlotMode.Hazard, igroups, groupid, grouped, stratified, ARR3, CDAT1, use_tic, use_marker)));

            // do a -ln(-ln(s)) vs. ln(t) plot to check for parallel categories/proportional hazards
            if (grouped)
            {
                double[] xp = new double[iobs + 1];
                double[] yp = new double[iobs + 1];
                int[] gn = new int[3 + 1];
                xp[0] = Constant.MISSING;
                yp[0] = Constant.MISSING;
                int igp = 1;
                for (int i = 1; i <= iobs; i++)
                {
                    double surv = Math.Pow(z[i].S, Math.Exp(Convert.ToDouble(z[i].Id) * ARR3[1, groupid, 1]));
                    // -ln(-ln S) is not finite at S = 0 (everyone still at risk died at that time) or at S = 1: the point is left out
                    double y = -Math.Log(-Math.Log(surv));
                    xp[i] = double.IsFinite(y) ? Math.Log(z[i].Time) : Constant.MISSING;
                    yp[i] = double.IsFinite(y) ? y : Constant.MISSING;
                    if (i > 1 && z[i].Id != z[i - 1].Id)
                        igp++;
                    gn[igp]++;
                }
                // Plot a metafile version
                ParameterBag cox2Parameters = new();
                chartList.Add(cox2Parameters);
                cox2Parameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Cox2, new Cox2Options(gn, igroups, xp, yp, CDAT1, groupid)));
            }
        }

        /// <summary>The deviance residual of a record, from whether it had the event (1) or was censored (0) and from its Cox-Snell residual.</summary>
        /// <remarks>
        /// With rm the martingale residual, which is the event less the Cox-Snell residual rc, the deviance residual is
        /// sign(rm) sqrt(-2 (rm + event ln(rc))).  The term with the logarithm is nothing for a censored record, whose residual is therefore
        /// -sqrt(2 rc), and 0 if its time is before the first event.
        /// </remarks>
        private static double DevianceResidual(int censor, double rc)
        {
            if (double.IsNaN(rc))
                return Constant.MISSING;
            double rm = censor - rc;
            double logTerm = censor == 0 ? 0.0 : censor * Math.Log(rc);
            return Math.Sign(rm) * Math.Sqrt(Math.Max(0.0, -2.0 * (rm + logTerm)));
        }

        /// <summary>
        /// The residuals: plots of the deviance residuals against time and against the rank of time and, if it is asked for, the residuals and
        /// diagnostics of each record saved to the worksheet.
        /// </summary>
        /// <remarks>
        /// With H the baseline cumulative hazard at the time of a record and r its hazard ratio, its Cox-Snell residual is r H, the cumulative
        /// hazard of the record itself; its martingale residual is 1 for an event, or 0 for a censored record, less the Cox-Snell residual; and
        /// its deviance residual is the martingale residual made more nearly symmetrical about 0 (see DevianceResidual).  The leverage, the
        /// proportionality constant and the residual that is saved as Cox-Oakes are those that the fit worked out (see coxiter).
        /// </remarks>
        public static StepOutput RptCoxResiduals(ParameterBag parameters)
        {
            double[,] ARR2 = (double[,])parameters["ARR2"].AsObject;

            bool save = parameters["save"].AsBoolean;

            int iobs = Convert.ToInt32(ARR2[0, 0]);
            CoxP[] z = new CoxP[iobs + 2];
            for (int i = 1; i <= iobs; i++)
            {
                z[i] = new CoxP
                {
                    Stratum = Convert.ToInt32(ARR2[i, 9]),
                    Time = ARR2[i, 6],
                    Id = Convert.ToInt32(ARR2[i, 8]),
                    Censor = Convert.ToInt32(ARR2[i, 7]),
                    //  use estimates as starting values if needed
                    S = ARR2[i, 1],
                    H = ARR2[i, 4],
                    //  baseline sum(exp(bz))
                    Exb = ARR2[i, 10],
                    Frequency = ARR2[i, 11],
                    Index = i
                };
            }

            BaselineSurvivalAndHazard(z, iobs);

            // the deviance residual of each record, to be plotted against its time and against the rank of its time
            double[] xp = new double[iobs];
            double[] yp = new double[iobs];
            double[] xr = new double[iobs];
            for (int i = 1; i <= iobs; i++)
            {
                yp[i - 1] = DevianceResidual(z[i].Censor, z[i].Exb * z[i].H);
                xp[i - 1] = z[i].Time;
            }

            ExFortran.Rank(xp, xr, 0, iobs, 1, out double _);
            ParameterBag outputParameters = new();
            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            ParameterBag chartParameters = new();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Xy, new XyOptions(xp, yp, "Time to event", "Deviance residual", "Deviance residuals vs. times", false, DataMinMax.XCalc_YCalc)));

            chartParameters = new ParameterBag();
            chartList.Add(chartParameters);
            chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.Xy, new XyOptions(xr, yp, "Rank of time to event", "Deviance residual", "Deviance residuals vs. ranks of times", false, DataMinMax.XCalc_YCalc)));

            // save to worksheet if requested
            if (save)
            {
                // restore the original record order if calling plot function or output to worksheet
                Array.Sort(z, 1, iobs, new CoxpByIndex());

                // each value goes in the row of its record, the row of a record that was left out of the regression for a missing value being left empty
                int records = parameters["coxRecords"].AsInt32;
                int[] recordOf = (int[])parameters["coxRecordOf"].AsObject;
                DoubleVariable leverageVariable = MissingVariable(records, "Leverage");
                DoubleVariable proportionalityVariable = MissingVariable(records, "Proportionality");
                DoubleVariable coxOakesResidualVariable = MissingVariable(records, "Cox-Oakes residual");
                DoubleVariable coxSnellResidualVariable = MissingVariable(records, "Cox-Snell residual");
                DoubleVariable martingaleResidualVariable = MissingVariable(records, "Martingale residual");
                DoubleVariable devianceResidualVariable = MissingVariable(records, "Deviance residual");
                DataFrame resultsFrame = new();
                resultsFrame.Variables.Add(leverageVariable);
                resultsFrame.Variables.Add(proportionalityVariable);
                resultsFrame.Variables.Add(coxOakesResidualVariable);
                resultsFrame.Variables.Add(coxSnellResidualVariable);
                resultsFrame.Variables.Add(martingaleResidualVariable);
                resultsFrame.Variables.Add(devianceResidualVariable);
                for (int i = 1; i <= iobs; i++)
                {
                    leverageVariable.SetData(recordOf[i - 1], ARR2[i, 2]);
                    proportionalityVariable.SetData(recordOf[i - 1], ARR2[i, 5]);
                    coxOakesResidualVariable.SetData(recordOf[i - 1], ARR2[i, 3]);
                    double rc = z[i].Exb * z[i].H;
                    double rm = z[i].Censor - rc;
                    double rd = DevianceResidual(z[i].Censor, rc);
                    coxSnellResidualVariable.SetData(recordOf[i - 1], rc);
                    martingaleResidualVariable.SetData(recordOf[i - 1], rm);
                    devianceResidualVariable.SetData(recordOf[i - 1], rd);
                }
                outputParameters.AddOutput("results", resultsFrame);
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The hazard ratio of each predictor, exp(b), with its confidence interval, exp(b - c se) to exp(b + c se), where c is the normal
        /// deviate for the confidence that was asked for; and the coefficients with their standard errors.
        /// </summary>
        public static StepOutput RptCoxHazardRatios(ParameterBag parameters)
        {
            double[,] ARR2 = (double[,])parameters["ARR2"].AsObject;
            double[,,] ARR3 = (double[,,])parameters["ARR3"].AsObject;
            ColumnData[] CDAT1 = (ColumnData[])parameters["CDAT1"].AsObject;

            double GAMMA = parameters["gamma"].AsDouble;
            MathDbl.civ(0, out double cit, GAMMA, out double _);
            ParameterBag outputParameters = new();
            outputParameters.AddOutput("pc", 100 * GAMMA);
            outputParameters.AddOutput("pc2", 100 * GAMMA);

            IList<ParameterBag> hazardList = new List<ParameterBag>();
            outputParameters.AddOutput("*hazard", hazardList);
            for (int i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
            {
                ParameterBag hazardParameters = new();
                hazardList.Add(hazardParameters);
                hazardParameters.AddOutput("par", CDAT1[i].Title);
                // a predictor that was dropped from the model (its standard error is zero) has no hazard ratio
                bool dropped = ARR3[1, i, 2] == 0.0;
                hazardParameters.AddOutput("ec", dropped ? Constant.MISSING : Formatting.SafeExp(ARR3[1, i, 1]));
                hazardParameters.AddOutput("ell", dropped ? Constant.MISSING : Formatting.SafeExp(ARR3[1, i, 1] - cit * ARR3[1, i, 2]));
                hazardParameters.AddOutput("eul", dropped ? Constant.MISSING : Formatting.SafeExp(ARR3[1, i, 1] + cit * ARR3[1, i, 2]));
            }
            IList<ParameterBag> parameterList = new List<ParameterBag>();
            outputParameters.AddOutput("*parameter", parameterList);
            for (int i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
            {
                ParameterBag parameterParameters = new();
                parameterList.Add(parameterParameters);
                parameterParameters.AddOutput("par", CDAT1[i].Title);
                bool dropped = ARR3[1, i, 2] == 0.0;
                parameterParameters.AddOutput("coef", dropped ? Constant.MISSING : ARR3[1, i, 1]);
                parameterParameters.AddOutput("se", dropped ? Constant.MISSING : ARR3[1, i, 2]);
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The log likelihood with no covariates and with those of the model, and the likelihood ratio chi-square, which is twice the difference,
        /// with as many degrees of freedom as there are predictors in the model.
        /// </summary>
        public static StepOutput RptCoxModelAnalysis(ParameterBag parameters)
        {
            double[,] ARR2 = (double[,])parameters["ARR2"].AsObject;
            ParameterBag outputParameters = new();
            outputParameters.AddOutput("ll0", ARR2[3, 0]);
            outputParameters.AddOutput("ll", ARR2[2, 0]);
            double x2dev = -2.0 * (ARR2[3, 0] - ARR2[2, 0]);
            outputParameters.AddOutput("x2", x2dev);
            // the degrees of freedom are the predictors less those that were dropped from the model
            outputParameters.AddOutput("df", ARR2[5, 0]);
            outputParameters.AddOutput("p", ARR2[5, 0] > 0 ? PDF.chivalp(Math.Abs(x2dev), ARR2[5, 0]) : Constant.MISSING);
            return new StepOutput(outputParameters);
        }
    }
}

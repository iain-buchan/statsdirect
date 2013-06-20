using System;
using System.Collections.Generic;
using System.Diagnostics;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.Numerics;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public class Coxreg
    {
        private class CoxpByStratTmThenExb : IComparer<coxp>
        {
            private int Compare(coxp x, coxp y)
            {
                //  First check strat
                if (x.strat > y.strat)
                    return 1;
                if (x.strat < y.strat)
                    return -1;

                //  Next check TM
                if (x.TM > y.TM)
                    return 1;
                if (x.TM < y.TM)
                    return -1;

                //  Next check Exb (negated)
                if (x.exb > y.exb)
                    return -1;
                if (x.exb < y.exb)
                    return 1;

                //  If we get here, there are no meaningful differences
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<coxp>.Compare(coxp x, coxp y)
            {
                return Compare(x, y);
            }

        }


        private class CoxpByStratThenTm : IComparer<coxp>
        {
            private int Compare(coxp x, coxp y)
            {
                //  First check strat
                if (x.strat > y.strat)
                    return 1;
                if (x.strat < y.strat)
                    return -1;

                //  Next check TM
                if (x.TM > y.TM)
                    return 1;
                if (x.TM < y.TM)
                    return -1;

                //  If we get here, there are no meaningful differences
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<coxp>.Compare(coxp x, coxp y)
            {
                return Compare(x, y);
            }

        }


        private class CoxpByIdThenTm : IComparer<coxp>
        {
            private int Compare(coxp x, coxp y)
            {
                //  First check id
                if (x.id > y.id)
                    return 1;
                if (x.id < y.id)
                    return -1;

                //  Next check TM
                if (x.TM > y.TM)
                    return 1;
                if (x.TM < y.TM)
                    return -1;

                //  If we get here, there are no meaningful differences
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<coxp>.Compare(coxp x, coxp y)
            {
                return Compare(x, y);
            }

        }

        private class CoxpByIndex : IComparer<coxp>
        {
            private int Compare(coxp x, coxp y)
            {
                return x.Index - y.Index;
            }
            // interface methods implemented by Compare
            int IComparer<coxp>.Compare(coxp x, coxp y)
            {
                return Compare(x, y);
            }
        }

        private class CoxpByTm : IComparer<coxp>
        {
            private int Compare(coxp x, coxp y)
            {
                if (x.TM > y.TM)
                    return 1;
                if (x.TM < y.TM)
                    return -1;
                return 0;
            }
            // interface methods implemented by Compare
            int IComparer<coxp>.Compare(coxp x, coxp y)
            {
                return Compare(x, y);
            }
        }

        public static StepResult RptCoxRegression(ITemplateHost host, ParameterBag parameters)
        {
            int i; int ifault = 0; int ncoef = 0;
            int istrat; int icov = 0;
            int ncov; int nrmiss = 0;
            int[] indef;
            int r; int c;
            int ifrq;
            double algl = 0;
            double xbar = 0;

            int ic = 0;
            DataFrame timesFrame = parameters["times"].AsDataFrame;
            DoubleVariable timesVariable = timesFrame.Variables[0].AsDoubleVariable;
            ic = ic + 1;
            int irt = ic;
            int rows = timesVariable.Length;
            double[] x = new double[rows * ic + 1 ];
            bool ok = true;
            int ik = 0;
            double adjt = 0.0;
            for (r = 1; r <= rows; r++)
            {
                ik = ik + 1;
                x[ik] = timesVariable.Data[r - 1];
                if (x[ik] <= 0.0)
                {
                    ok = false;
                    if (Math.Abs(x[ik]) + 1 > adjt)
                    {
                        adjt = Math.Abs(x[ik]) + 1;
                    }
                }
            }
            if (!(ok))
            {
                if (host.Query("Times must be >0: Do you want to add " + adjt.ToString() + " to all of your times?", "Cox Regression"))
                {
                    for (r = 1; r <= rows; r++)
                    {
                        x[r] = x[r] + adjt;
                    }
                    ok = true;
                }
                else
                {
                    throw new TemplateOperationCancelledException();
                }
            }

            DataFrame eventsFrame = parameters["events"].AsDataFrame;
            DoubleVariable eventsVariable = eventsFrame.Variables[0].AsDoubleVariable;
            ic++;
            int icen = ic;
            // create temp variable for copying values 
            double[] transTemp3 = new double[rows * ic + 1 ];
            Array.Copy(x, transTemp3, Math.Min(x.Length, transTemp3.Length));
            x = transTemp3;
            ok = false;
            double dead = 0;
            for (r = 1; r <= rows; r++)
            {
                ik = ik + 1;
                x[ik] = eventsVariable.Data[r - 1];
                dead = dead + x[ik];
                if (x[ik] > 1)
                {
                    ok = true;
                }
                if (x[ik] > 0)
                {
                    x[ik] = 0;
                }
                else if (x[ik] <= 0)
                {
                    x[ik] = 1;
                }
            }
            // use the frequency variable if data are grouped
            if (ok)
            {
                ic = ic + 1;
                ifrq = ic;
                // create temp variable for copying values 
                double[] transTemp4 = new double[rows * ic + 1 ];
                Array.Copy(x, transTemp4, Math.Min(x.Length, transTemp4.Length));
                x = transTemp4;
                for (r = 1; r <= rows; r++)
                {
                    ik = ik + 1;
                    if (eventsVariable.Data[r - 1] > 1)
                    {
                        x[ik] = eventsVariable.Data[r - 1];
                    }
                    else
                    {
                        x[ik] = 1;
                    }
                }
            }
            else
            {
                ifrq = 0;
            }

            DataFrame predictorsFrame = null;
            if (parameters.ContainsKey("predictors") && parameters["predictors"] != null)
            {
                predictorsFrame = parameters["predictors"].AsDataFrame;
                // Store the predictor Data
                double[,] xx = new double[predictorsFrame.VariableCount - 1 + 1, rows + 1];
                for (c = 0; c <= predictorsFrame.VariableCount - 1; c++)
                {
                    for (r = 1; r <= rows; r++)
                    {
                        xx[c, r] = predictorsFrame.Variables[c].AsDoubleVariable.Data[r - 1];
                    }
                }
                // load predictors into the master matrix
                ncov = predictorsFrame.VariableCount;
                // create temp variable for copying values 
                double[] transTemp5 = new double[rows * (ic + ncov) + 1 ];
                Array.Copy(x, transTemp5, Math.Min(x.Length, transTemp5.Length));
                x = transTemp5;
                indef = new int[ncov + 1];
                icov = ik;
                for (c = 0; c <= predictorsFrame.VariableCount - 1; c++)
                {
                    indef[c + 1] = ic + c + 1;
                    for (r = 1; r <= rows; r++)
                    {
                        ik = ik + 1;
                        x[ik] = predictorsFrame.Variables[c].AsDoubleVariable.Data[r - 1];
                    }
                }
                ic = ic + ncov;
            }
            else
            {
                ncov = 0;
                indef = new int[1 + 1];
            }

            // start to fill the holdx matrix needed for the plot function
            double[,] holdx = new double[rows + 2, ncov + 2];
            for (c = 1; c <= ncov; c++)
            {
                // If we get here, ncov must be at least 1, so predictorsFrame cannot be null.
                Debug.Assert(null != predictorsFrame);
                for (r = 1; r <= rows; r++)
                {
                    holdx[r, c] = predictorsFrame.Variables[c - 1].AsDoubleVariable.Data[r - 1];
                }
            }

            // identify the binary covariates
            bool[] bincov = new bool[ncov + 1];
            ColumnData[] xd = new ColumnData[predictorsFrame.VariableCount - 1 + 1];
            int binaries = 0;
            if (ncov > 0)
            {
                for (c = 0; c <= predictorsFrame.VariableCount - 1; c++)
                {
                    xd[c] = new ColumnData();
                    bincov[c + 1] = is_binary(predictorsFrame.Variables[c].AsDoubleVariable, xd[c]);
                    if (bincov[c + 1])
                    {
                        binaries = binaries + 1;
                    }
                }
            }

            // store predictor meta-data
            for (c = 0; c <= predictorsFrame.VariableCount - 1; c++)
            {
                if (xd[c] == null)
                {
                    xd[c] = new ColumnData();
                }
                xd[c].Title = predictorsFrame.Variables[c].Title;
            }

            // get strata
            if (parameters.ContainsKey("strata") && parameters["strata"] != null)
            {
                DataFrame strataFrame = parameters["strata"].AsDataFrame;
                ClassifierVariable strataVariable = strataFrame.Variables[0].AsClassifierVariable;
                ic = ic + 1;
                istrat = ic;
                // create temp variable for copying values 
                double[] transTemp6 = new double[rows * ic + 1 ];
                Array.Copy(x, transTemp6, Math.Min(x.Length, transTemp6.Length));
                x = transTemp6;
                for (r = 1; r <= rows; r++)
                {
                    ik = ik + 1;
                    x[ik] = strataVariable.Data[r - 1];
                }
            }
            else
            {
                istrat = 0;
            }

            int nCol = ic;

            int nef = ncov;
            if (nef < 1)
            {
                throw new TemplateOperationCancelledException();
            }
            int[] nvef = new int[nef + 1];
            for (r = 1; r <= nef; r++)
            {
                nvef[r] = 1;
            }

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

            if (centre)
            {
                for (i = 1; i <= nef; i++)
                {
                    if (xd[i - 1].Groups == null || xd[i - 1].Groups.Count > 2)
                    {
                        for (r = 1; r <= rows; r++)
                        {
                            xbar = xbar + holdx[r, i];
                        }
                        xbar = xbar / Convert.ToDouble(rows);
                        for (r = 1; r <= rows; r++)
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
            double[] GR = new double[ldcoef + 1 ];
            double[] xmean = new double[ldcoef + 1];
            coxreg(ref nobs, ref nCol, ref x, ref nobs, ref irt, ref ifrq, ref ifix, ref icen, ref istrat, ref maxit, ref eps, ref ratio, ref nef, ref nvef, ref indef, ref itie, ref ncoef, ref coef, ref ldcoef, ref algl, ref cov, ref ldcoef, ref xmean, ref ccase, ref nobs, ref GR, ref igrp, ref nrmiss, ref ifault);
            // double[,] ARR2 = new double[nobs + 2, 6 + 1];
            // double[, ,] ARR3 = new double[2 + 1, ncoef + 2, 4 + 1]; 
            if (ifault != 0)
            {
                if (ifault == 3)
                {
                    host.Error("Calculation failed to converge, try again with a lower precision or fewer predictors.", "Cox Regression");
                }
                else if (ifault > 99)
                {
                    host.Error("Singularity in Hessian: try dropping predictor " + (ifault - 100).ToString() + ": " + predictorsFrame.Variables[Math.Max(ifault - 101, 0)].Title, "Cox Regression");
                }
                else
                {
                    host.Error("Error in calculation (" + ifault.ToString() + ")", "Cox Regression");
                }
                throw new TemplateOperationCancelledException();
            }
            ColumnData[] CDAT1 = new ColumnData[ncoef + 1 ];
            double[, ,] ARR3 = new double[1 + 1, ncoef + 1, 3 + 1];
            double[,] ARR2 = new double[nobs + 1, 10 + 1];
            for (i = 1; i <= ncoef; i++)
            {
                ARR3[1, i, 1] = coef[i, 1];
                ARR3[1, i, 2] = coef[i, 2];
                ARR3[1, i, 3] = coef[i, 3];
                CDAT1[i] = xd[i - 1];
            }

            for (i = 1; i <= nobs; i++)
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
            }
            ARR2[0, 0] = nobs;
            ARR2[1, 0] = ncoef;
            ARR2[2, 0] = algl;
            ARR2[4, 0] = dead;

            // run a second time with a dummy var = 1 to get LL(0)
            nef = 1;
            ncov = 1;
            // int ldcov = 1; Never used.  PJC 2012/04/09.
            ldcoef = 1;
            for (i = icov + 1; i <= icov + nobs; i++)
            {
                x[i] = 1.0;
            }
            indef[1] = 3;
            igrp = new int[nobs + 1 ];
            ccase = new double[nobs + 1, 6 + 1];
            coef = new double[ldcoef + 1, 4 + 1];
            cov = new double[ldcoef + 1, ldcoef + 1];
            GR = new double[ldcoef + 1 ];
            xmean = new double[ldcoef + 1 ];
            coxreg(ref nobs, ref nCol, ref x, ref nobs, ref irt, ref ifrq, ref ifix, ref icen, ref istrat, ref maxit, ref eps, ref ratio, ref nef, ref nvef, ref indef, ref itie, ref ncoef, ref coef, ref ldcoef, ref algl, ref cov, ref ldcoef, ref xmean, ref ccase, ref nobs, ref GR, ref igrp, ref nrmiss, ref ifault);
            ARR2[3, 0] = algl;

            List<string> subgroups = new List<string>();
            if (istrat > 0)
            {
                subgroups.Add("Strata");
            }
            else
            {
                if (binaries > 0)
                {
                    for (i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
                    {
                        if (bincov[i])
                        {
                            subgroups.Add(CDAT1[i].Title);
                        }
                    }
                    subgroups.Add("None");
                }
                else
                {
                    subgroups.Add("None");
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("n", ARR2[0, 0].ToString());
            outputParameters.AddOutput("d", ARR2[4, 0].ToString());
            double x2dev = -2.0 * (ARR2[3, 0] - ARR2[2, 0]);
            outputParameters.AddOutput("x2", host.RoundU(x2dev));
            outputParameters.AddOutput("df", ARR2[1, 0].ToString());
            outputParameters.AddOutput("p_dev", host.pval(PDF.chivalp(Math.Abs(x2dev), ARR2[1, 0])));
            IList<ParameterBag> predList = new List<ParameterBag>();
            outputParameters.AddOutput("*pred", predList);
            for (i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
            {
                ParameterBag predParameters = new ParameterBag();
                predList.Add(predParameters);
                predParameters.AddOutput("lab", CDAT1[i].Title);
                predParameters.AddOutput("i", i.ToString());
                predParameters.AddOutput("b", host.RoundU(ARR3[1, i, 1]));
                predParameters.AddOutput("z", host.RoundU(ARR3[1, i, 3]));
                predParameters.AddOutput("p", host.pval(MathDbl.zvalp2(ARR3[1, i, 3])));
            }
            outputParameters.Add("subgroups", new FilledParameter(true, new DataFrame(new StringVariable(subgroups.ToArray()))));
            outputParameters.Add("ARR2", new FilledParameter(true, ARR2));
            outputParameters.Add("ARR3", new FilledParameter(true, ARR3));
            outputParameters.Add("CDAT1", new FilledParameter(true, CDAT1));
            outputParameters.Add("holdx", new FilledParameter(true, holdx));
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        ///  <summary>
        ///  Generate regressors
        ///  </summary>
        ///  <param name="nCol"></param>
        ///  <param name="x"></param>
        ///  <param name="ix1"></param>
        ///  <param name="nef"></param>
        ///  <param name="nvef"></param>
        ///  <param name="indef"></param>
        ///  <param name="idummy"></param>
        ///  <param name="nreg"></param>
        ///  <param name="reg"></param>
        ///  <param name="nrmiss"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        private static void genregs(int nCol, double[] x, int ix1, int nef, int[] nvef, int[] indef, int idummy, ref int nreg, double[] reg, ref int nrmiss, ref int ifault)
        {
            int i;
            int lindef = 0;

            for (i = 1; i <= nef; i++)
            {
                if (nvef[i] <= 0)
                {
                    ifault = 2;
                }
                else
                {
                    lindef = lindef + nvef[i];
                }
            }
            if (ifault != 0)
            {
                return;
            }
            for (i = 1; i <= lindef; i++)
            {
                if (indef[i] <= 0 | indef[i] > nCol)
                {
                    ifault = 3;
                }
            }
            if (ifault != 0)
            {
                return;
            }
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
            for (i = 1; i <= nef; i++)
            {
                double xprod = 1.0;
                int nlast = 0;
                int L;
                for (L = nvef[i]; L >= 1; L--)
                {
                    int lndef = indef[indefx + L];
                    double xvar = x[ix1 - 1 + (lndef - 1) + 1];
                    if (xvar == Constant.MISSING)
                    {
                        misef = 1;
                    }
                    if (misef == 0)
                    {
                        xprod = xprod * xvar;
                    }
                }
                int kpos = nreg;
                nreg = nreg + 1;
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
                indefx = indefx + nvef[i];
                misef = 0;
            }
            nrmiss = nrmiss + misval;
        }


        private static void coxreg(ref int nRow, ref int nCol, ref double[] x, ref int ldx, ref int irt, ref int IFRQ, ref int ifix, ref int icen, ref int istrat, ref int maxit, ref double eps, ref double ratio, ref int nef, ref int[] nvef, ref int[] indef, ref int itie, ref int ncoef, ref double[,] coef, ref int ldcoef, ref double algl, ref double[,] cov, ref int ldcov, ref double[] xmean, ref double[,] caze, ref int ldcase, ref double[] GR, ref int[] igrp, ref int nrmiss, ref int ifault)
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
                    ntrm = ntrm + 1;
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
                {
                    ifault = 11;
                }
                if (ldcoef < ncoef)
                {
                    ifault = 12;
                }
            }
            if (ifault != 0)
            {
                return;
            }
            double[] OBS = new double[2 * (ncoef + 1) + 1];
            double[] smg = new double[2 * ncoef + 1];
            double[] smh = new double[2 * Math.Max(ncoef * ncoef, 2) + 1];
            int[] iptr = new int[nRow + ncoef + 1];
            int[] idt = new int[nRow + 1];
            int[] iwk = new int[3 * Math.Max(nRow, nCol) + 1 ];
            coxest(nRow, nCol, ref x, ref ldx, ref irt, ref IFRQ, ref ifix, ref icen, ref istrat, ref maxit, ref eps, ref ratio, ref nef, ref nvef, ref indef, ref itie, ref ncoef, ref coef, ref ldcoef, ref algl, ref cov, ref ldcov, ref xmean, ref caze, ref ldcase, ref GR, ref igrp, ref nrmiss, ref OBS, ref smg, ref smh, ref iptr, ref idt, ref iwk, ref ifault);
        }

        private static void coxest(int nRow, int nCol, ref double[] x, ref int ldx, ref int irt, ref int IFRQ, ref int ifix, ref int icen, ref int istrat, ref int maxit, ref double eps, ref double ratio, ref int nef, ref int[] nvef, ref int[] indef, ref int itie, ref int ncoef, ref double[,] coef, ref int ldcoef, ref double algl, ref double[,] cov, ref int ldcov, ref double[] xmean, ref double[,] caze, ref int ldcase, ref double[] GR, ref int[] igrp, ref int nrmiss, ref double[] OBS, ref double[]
        smg, ref double[] smh, ref int[] iptr, ref int[] idt, ref int[] iwk, ref int ifault)
        {
            int nidt = 0; int ik;
            int i;
            double xx = 0;

            //   ESTIMATES FOR PARAMETERS IN PROPORTIONAL HAZARDS MODEL
            int[] indkey = new int[3 + 1];
            if (nRow >= 1)
            {
                if (ldx < nRow)
                {
                    ifault = 1;
                }
                if (ldcase < nRow)
                {
                    ifault = 2;
                }
            }
            if (eps < 0.0)
            {
                ifault = 3;
            }
            if (ratio < 0.0 & ratio != -1.0)
            {
                ifault = 4;
            }
            if (ifault != 0)
            {
                return;
            }
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
                    ntrm = ntrm + 1;
                    int ii = indef[ntrm];
                    if (ii > nCol | ii <= 0)
                    {
                        ifault = 7;
                        return;
                    }
                }
            }
            if (ifault != 0)
            {
                return;
            }
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
                        ier = ier + 1;
                        ifault = 15;
                        ier = ier + 1;
                        if (ier > 10)
                        {
                            return;
                        }
                    }
                    else if (x[mf + i] == 0.0)
                    {
                        igrp[i] = -1;
                    }
                }
                if (ifix > 0)
                {
                    if (x[mi + i] == Constant.MISSING)
                    {
                        igrp[i] = -1;
                    }
                    if (icen > 0)
                    {
                        if (x[mc + i] == Constant.MISSING)
                        {
                            igrp[i] = -1;
                        }
                        else if (Convert.ToInt64(x[mc + i]) > 3 | Convert.ToInt64(x[mc + i]) < 0.0)
                        {
                            ifault = 10;
                            ier = ier + 1;
                            if (ier > 10)
                            {
                                return;
                            }
                        }
                        else if (Convert.ToInt64(x[mc + i]) > 1)
                        {
                            igrp[i] = -1;
                        }
                    }
                    if (x[mr + i] == Constant.MISSING)
                    {
                        igrp[i] = -1;
                    }
                }
            }
            if (ier > 0)
            {
                return;
            }
            genregs(nCol, x, 1, nef, nvef, indef, -2, ref ncoef, OBS, ref nrmiss, ref ifault);
            if (ncoef <= 0)
            {
                ifault = 14;
            }
            else
            {
                if (ldcoef < ncoef)
                {
                    ifault = 15;
                }
                if (ldcov < ncoef)
                {
                    ifault = 16;
                }
            }
            if (ifault > 0)
            {
                return;
            }
            MXPOSE(ldx, nCol, x, out ifault);
            if (itie != 1)
            {
                int nkey = 0;
                if (istrat > 0)
                {
                    nkey = nkey + 1;
                    indkey[nkey] = istrat;
                }
                nkey = nkey + 1;
                indkey[nkey] = irt;
                if (icen > 0)
                {
                    nkey = nkey + 1;
                    indkey[nkey] = icen;
                    for (ik = icen; ik <= nRow * nCol; ik += nCol)
                    {
                        x[ik] = -1.0 * x[ik];
                    }
                }
                for (ik = irt; ik <= nRow * nCol; ik += nCol)
                {
                    x[ik] = -1.0 * x[ik];
                }
                Matrix.MXSRT(ref nCol, ref nRow, ref x, ref nkey, ref indkey, ref iptr, ref nidt, ref idt, ref ifault);
                for (ik = irt; ik <= nRow * nCol; ik += nCol)
                {
                    x[ik] = -1.0 * x[ik];
                }
                if (icen > 0)
                {
                    for (ik = icen; ik <= nRow * nCol; ik += nCol)
                    {
                        x[ik] = -1.0 * x[ik];
                    }
                }
            }
            else
            {
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
                    mr = mr + nCol;
                    if (istrat > 0)
                    {
                        ms = ms + nCol;
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
            {
                for (ik = 1; ik <= ncoef; ik++)
                {
                    coef[ik, 1] = 0.0;
                }
            }
            coxiter(nRow, nCol, x, irt, IFRQ, ifix, icen, ref istrat, ref maxit, ref eps, ref ratio, ref nef, ref nvef, ref indef, ref itie, ref ncoef, ref coef, ref ldcoef, ref algl, ref cov, ref ldcov, ref xmean, ref caze, ref ldcase, ref GR, ref igrp, ref nrmiss, ref OBS, ref smg, ref smh, ref iptr, ref idt, ref ifault);
            if (ifault != 0)
            {
                return;
            }
            MXPOSE(nCol, ldx, x, out ifault);
        }


        private static void coxiter(int nobs, int nCol, double[] x, int irt, int IFRQ, int ifix, int icen, ref int istrat, ref int maxit, ref double eps, ref double ratio, ref int nef, ref int[] nvef, ref int[] indef, ref int itie, ref int ncoef, ref double[,] coef, ref int ldcoef, ref double algl, ref double[,] cov, ref int ldcov, ref double[] xmean, ref double[,] caze, ref int ldcase, ref double[] GR, ref int[] igrp, ref int nrmiss, ref double[] OBS, ref double[] smg, ref double[]
         smh, ref int[] iptr, ref int[] idt, ref int ifault)
        {
            int icncd; int icnn;
            int iq;
            int irank; int j; int kk; int k;
            int iter; int J1;
            double alglo;
            double div;
            double xcen;
            double xfix; double xfrq; double xlt; double xpar; double xrt; double xx;
            double zdot;
            bool change;

            //   NEWTON-RAPHSON ITERATIONS
            double[] smd = new double[1 + 1 ];
            for (int i = 1; i <= ncoef; i++)
            {
                xmean[i] = 0.0;
            }
            int nob1 = 0;
            double smfrq = 0.0;
            double strato = 1.23457E-27;
            bool strat = false;
            int igr = istrat > 0 ? 0 : 1;
            int ii = 0;
            int imiss = 0;
            for (int i = 1; i <= nobs; i++)
            {
                k = iptr[i];
                if (igrp[k] >= 0)
                {
                    coxvars(x, (k - 1) * nCol, irt, 0, IFRQ, ifix, 0, icen, out xrt, out xlt, out xfrq, out xfix, out xpar, out xcen, out imiss);
                    if (istrat > 0)
                    {
                        xx = x[istrat + (k - 1) * nCol];
                        if (xx != strato)
                        {
                            igr = igr + 1;
                            strato = xx;
                        }
                    }
                    genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                    if (imiss == 0)
                    {
                        for (j = 1; j <= ncoef; j++)
                        {
                            xmean[j] = xmean[j] + OBS[j] * xfrq;
                        }
                        smfrq = smfrq + xfrq;
                        nob1 = nob1 + 1;
                        igrp[k] = igr;
                        ii = ii + 1;
                    }
                    else
                    {
                        igrp[k] = -1;
                        nrmiss = nrmiss + 1;
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
            {
                xmean[i] = 1.0 / smfrq * xmean[i];
            }
            for (int i = 1; i <= nobs; i++)
            {
                idt[i] = 0;
            }
            if (itie != 1)
            {
                double dt = 1.23476E+34;
                igr = 0;
                icnn = 0;
                icncd = 1;
                J1 = 0;
                int itdt = 0;
                for (int i = 1; i <= nobs; i++)
                {
                    j = iptr[i];
                    if (igrp[j] >= 0)
                    {
                        if (icen > 0)
                        {
                            icnn = Convert.ToInt32(x[icen + (j - 1) * nCol]);
                        }
                        double dtn = x[irt + (j - 1) * nCol];
                        if (J1 != 0)
                        {
                            idt[J1] = 0;
                        }
                        if (dtn != dt | igrp[j] != igr | icncd != icnn)
                        {
                            if (icncd == 0)
                            {
                                idt[J1] = itdt;
                            }
                            itdt = 1;
                            icncd = icnn;
                            igr = igrp[j];
                            dt = dtn;
                        }
                        else
                        {
                            itdt = itdt + 1;
                        }
                        J1 = j;
                    }
                }
                if (icnn == 0)
                {
                    idt[J1] = itdt;
                }
                else
                {
                    idt[J1] = 0;
                }
            }
            else
            {
                icnn = 0;
                for (int i = 1; i <= nobs; i++)
                {
                    j = iptr[i];
                    if (igrp[j] >= 0)
                    {
                        if (icen > 0)
                        {
                            icnn = Convert.ToInt32(x[icen + (j - 1) * nCol]);
                        }
                        idt[j] = icnn == 0 ? 1 : 0;
                    }
                }
            }
            bool ihess = false;
            icncd = ncoef;
            igr = 0;
            icnn = 0;
            bool Zero = false;
            double smu = 0;
            for (ii = nobs + 1; ii <= ncoef + nobs; ii++)
            {
                iptr[ii] = 0;
            }
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
                        {
                            smg[iq] = OBS[iq];
                        }
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
                                    icnn = icnn + 1;
                                }
                            }
                        }
                        if (icnn == icncd)
                        {
                            break;
                        }
                    }
                }
            }
            icncd = icnn;
            coxhess(nobs, nCol, x, irt, IFRQ, ifix, icen, ratio, nef, nvef, indef, ncoef, coef, 1, ihess, out alglo, cov, ldcov, xmean, caze, ldcase, GR, OBS, smg, smh, iptr, idt, igrp, out change, Zero, ref ifault);
            if (ifault != 0)
            {
                return;
            }
            for (iter = 1; iter <= maxit; iter++)
            {
                for (ii = 1; ii <= ncoef; ii++)
                {
                    coef[ii, 3] = GR[ii];
                }
                double crit1 = 0.0;
                for (int i = 1; i <= ncoef; i++)
                {
                    double t = Math.Abs(coef[i, 1]);
                    crit1 = Math.Max(crit1, t > 1.0 ? Math.Abs(coef[i, 3] / coef[i, 1]) : Math.Abs(coef[i, 3]));
                }
                div = 1.0;
                double crit;
                do
                {
                    for (int i = 1; i <= ncoef; i++)
                    {
                        coef[i, 2] = coef[i, 1] + div * coef[i, 3];
                    }
                    coxhess(nobs, nCol, x, irt, IFRQ, ifix, icen, ratio, nef, nvef, indef, ncoef, coef, 2, ihess, out algl, cov, ldcov, xmean, caze, ldcase, GR, OBS, smg, smh, iptr, idt, igrp, out change, Zero, ref ifault);
                    if (ifault != 0)
                        return;
                    if (change)
                        strat = true;
                    crit = algl - alglo;
                    if (crit < 1.0E+20 * Math.Abs(alglo) & crit != 0.0)
                    {
                        crit = crit / Math.Abs(algl);
                    }
                    if (crit < -eps)
                    {
                        div = div / 2.0;
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
                if (crit < 0.1)
                {
                    ihess = true;
                }
                for (ii = 1; ii <= ncoef; ii++)
                {
                    coef[ii, 1] = coef[ii, 2];
                }
                alglo = algl;
                if (Math.Abs(crit) <= eps)
                {
                    break;
                }
            }
            if (iter >= maxit)
            {
                ifault = 5;
            }
            if (strat)
            {
                ifault = 6;
                int indy = ncoef + 1;
                int indep = indy;
                // Call GLREG(1, 0, INDY, X(1), -NCOEF, INDEF(1), INDEP, COEF(1, 1), NCOEF, COV(1, 1), LDCOV, SMG(1), irank, SMU, SMD(1), IMISS, SMH(1), SMH(1), NCOEF, OBS(1), ifault)
                //  Can't do calls with array offsets into VB, so this separates the top half of smh into its own array for the call
                double[] smhmax = new double[ncoef + 1 ];
                double[] coef1 = new double[ncoef + 1 ];
                for (int i = 0; i <= ncoef; i++)
                {
                    smhmax[i] = smh[i + ncoef];
                    coef1[i] = coef[i, 1];
                }
                irank = 0;
                imiss = 0;
                Regress1.glsqr1(1, 0, 0, 0, indy, x, 1, -ncoef, indef, indep, indef, 0, 0, ref coef1, ref cov, ref smg, ref irank, ref smu, ref smd[1], ref imiss, ref smh, ref smhmax, ref OBS, ref ifault);
                for (int i = 0; i <= ncoef; i++)
                {
                    smh[i + ncoef] = smhmax[i];
                    coef[i, 1] = coef1[i];
                }

                if (ifault != 0)
                {
                    return;
                }
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
                            {
                                caze[j, 1] = 0.0;
                            }
                            for (j = i; j <= nobs; j++)
                            {
                                kk = iptr[j];
                                if (igrp[kk] >= 0)
                                {
                                    if (igrp[kk] != igr)
                                    {
                                        break;
                                    }
                                    genregs(nCol, x, 1 + (kk - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                                    zdot = 0.0;
                                    for (iq = 1; iq <= ncoef; iq++)
                                    {
                                        zdot = zdot + OBS[iq] * coef[iq, 1];
                                    }
                                    ymean = ymean + zdot;
                                    for (iq = 1; iq <= ncoef; iq++)
                                    {
                                        caze[iq, 1] = caze[iq, 1] + OBS[iq] * 1.0;
                                    }
                                    xx = xx + 1.0;
                                }
                            }
                            if (xx > 0.0)
                            {
                                xx = 1.0 / xx;
                            }
                            ymean = ymean * xx;
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
                        smhmax = new double[ncoef + 1 ];
                        coef1 = new double[ncoef + 1 ];
                        for (ii = 0; ii <= ncoef; ii++)
                        {
                            smhmax[ii] = smh[ii + ncoef];
                            coef1[ii] = coef[ii, 1];
                        }
                        Regress1.glsqr1(2, 0, 0, 1, indy, OBS, 1, -ncoef, indef, indep, indef, 0, 0, ref coef1, ref cov, ref smg, ref irank, ref smu, ref smd[1], ref imiss, ref smh, ref smhmax, ref OBS, ref ifault);
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
                            Zero = true;
                            for (ii = 1; ii <= i; ii++)
                            {
                                cov[ii, i] = 0.0;
                            }
                            for (ii = i + 1; ii <= ncoef - i; ii++)
                            {
                                cov[i, ii] = 0.0;
                            }
                        }
                    }
                    // Call GLREG(3, 0, INDY, OBS(1), -NCOEF, INDEF(1), INDEP, COEF(1, 1), NCOEF, COV(1, 1), LDCOV, SMG(1), irank, SMU, SMD(1), IMISS, SMH(1), SMH(1), NCOEF, OBS(1), ifault)
                    // Declare Sub GLSQR Lib "StatsDirect" (ByVal ido As Long, ByVal intcep As Long, ByVal isub As Long, ByVal nRow As Long, ByVal nvar As Long, ByVal x As Double, !!ByVal ldx As Long!!, ByVal iind As Long, ByVal indind As Long, ByVal idep As Long, ByVal inddep As Long, ByVal IFRQ As Long, ByVal iwt As Long, ByVal b As Double, !!ByVal ldb As Long!!, ByVal r As Double, !!ByVal ldr As Long!!, ByVal D As Double, ByVal irank As Long, ByVal rdf As Double, ByVal rss As Double, ByVal nrmiss As Long, ByVal xmin As Double, ByVal XMax As Double, ByVal WK As Double, ByVal ifault As Long)
                    //  Can't do calls with array offsets into VB, so this separates the top half of smh into its own array for the call
                    smhmax = new double[ncoef + 1 ];
                    coef1 = new double[ncoef + 1 ];
                    for (ii = 0; ii <= ncoef; ii++)
                    {
                        smhmax[ii] = smh[ii + ncoef];
                        coef1[ii] = coef[ii, 1];
                    }
                    Regress1.glsqr1(3, 0, 0, 0, indy, OBS, 1, -ncoef, indef, indep, indef, 0, 0, ref coef1, ref cov, ref smg, ref irank, ref smu, ref smd[1], ref imiss, ref smh, ref smhmax, ref OBS, ref ifault);
                    for (ii = 0; ii <= ncoef; ii++)
                    {
                        smh[ii + ncoef] = smhmax[ii];
                        coef[ii, 1] = coef1[ii];
                    }
                    for (ii = 1; ii <= ncoef; ii++)
                    {
                        coef[ii, 1] = coef[ii, 2];
                    }
                }
            }
            coxhess(nobs, nCol, x, irt, IFRQ, ifix, icen, ratio, nef, nvef, indef, ncoef, coef, 1, true, out algl, cov, ldcov, xmean, caze, ldcase, GR, OBS, smg, smh, iptr, idt, igrp, out change, Zero, ref ifault);
            if (ifault != 0)
            {
                return;
            }
            for (ii = 1; ii <= nobs; ii++)
            {
                caze[ii, 5] = caze[ii, 1];
            }
            igr = 0;
            for (int i = 1; i <= 4; i++)
            {
                for (ii = 1; ii <= nobs; ii++)
                {
                    caze[ii, 1] = Constant.MISSING;
                }
            }
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
                        {
                            smg[ii] = 0.0;
                        }
                        for (ii = i; ii >= 1; ii--)
                        {
                            kk = iptr[ii];
                            if (igrp[kk] >= 0)
                            {
                                if (igrp[kk] != igr)
                                {
                                    break;
                                }
                                coxvars(x, (kk - 1) * nCol, irt, 0, IFRQ, ifix, 0, icen, out xrt, out xlt, out xfrq, out xfix, out xpar, out xcen, out imiss);
                                caze[kk, 2] = xfrq;
                                smu = smu + xfrq * caze[kk, 5];
                                caze[kk, 4] = xcen;
                                genregs(nCol, x, 1 + (kk - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                                for (iq = 1; iq <= ncoef; iq++)
                                {
                                    OBS[iq] = OBS[iq] + -1.0 * xmean[iq];
                                    smg[iq] = smg[iq] + (xfrq * caze[kk, 5]) * OBS[iq];
                                }
                            }
                        }
                        smd[1] = 0.0;
                    }
                    if (icen > 0)
                    {
                        icnn = Convert.ToInt32(x[icen + (k - 1) * nCol]);
                    }
                    if (icnn == 1)
                    {
                        smu = smu - caze[k, 2] * caze[k, 5];
                        genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                        for (iq = 1; iq <= ncoef; iq++)
                        {
                            OBS[iq] = OBS[iq] + -1.0 * xmean[iq];
                            smg[iq] = smg[iq] + (-caze[k, 2] * caze[k, 5]) * OBS[iq];
                        }
                        caze[k, 1] = Math.Exp(-smd[1]);
                        caze[k, 2] = Constant.MISSING;
                        caze[k, 3] = smd[1] * caze[k, 5];
                        caze[k, 4] = smd[1];
                    }
                    else if (idt[k] > 0)
                    {
                        ii = i + 1;
                        double xfd = 0.0;
                        double tmps = 0.0;
                        for (j = 1; j <= idt[k]; j++)
                        {
                            do
                            {
                                ii = ii - 1;
                                kk = iptr[ii];
                            }
                            while (igrp[kk] < 0);
                            xfd = xfd + caze[kk, 2];
                            tmps = tmps + caze[kk, 2] * caze[kk, 5];
                        }
                        smd[1] = smd[1] + xfd / smu;
                        ii = i;
                        for (j = 1; j <= ncoef; j++)
                        {
                            smh[j] = 0.0;
                        }
                        J1 = idt[k];
                        for (j = 1; j <= J1; j++)
                        {
                            caze[k, 1] = Math.Exp(-smd[1]);
                            caze[k, 3] = caze[k, 5] * smd[1];
                            caze[k, 4] = smd[1];
                            genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef, OBS, ref imiss, ref ifault);
                            for (iq = 1; iq <= ncoef; iq++)
                            {
                                OBS[iq] = OBS[iq] + -1.0 * xmean[iq];
                                smh[iq] = smh[iq] + (caze[k, 2] * caze[k, 5]) * OBS[iq];
                                OBS[iq] = OBS[iq] + (-1.0 / smu) * smg[iq];
                            }
                            int M;
                            for (M = 1; M <= ncoef; M++)
                            {
                                if (cov[M, M] == 0.0)
                                {
                                    OBS[M] = 0.0;
                                }
                            }
                            Regress1.mxinv2(ncoef, cov, OBS, true, true, false, cov, out irank, ref ifault);
                            if (ifault != 0)
                            {
                                return;
                            }
                            zdot = 0.0;
                            for (iq = 1; iq <= ncoef; iq++)
                            {
                                zdot = zdot + OBS[iq] * OBS[iq];
                            }
                            caze[k, 2] = zdot;
                            if (j != J1)
                            {
                                do
                                {
                                    ii = ii - 1;
                                    k = iptr[ii];
                                }
                                while (igrp[k] < 0);
                            }
                        }
                        smu = smu - tmps;
                        for (j = 1; j <= ncoef; j++)
                        {
                            smg[j] = smg[j] + -1.0 * smh[j];
                        }
                    }
                }
            }
            Regress1.rcovarb(ncoef, cov, 1.0, cov, ref ifault);
            for (int i = 1; i <= ncoef; i++)
            {
                coef[i, 2] = Math.Sqrt(cov[i, i]);
                uoverd(coef[i, 1], coef[i, 2], out coef[i, 3]);
            }
        }


        ///  <summary>
        ///  COMPUTE HESSIAN, GRADIENT, AND PARAMETER UPDATES
        ///  </summary>
        ///  <param name="nobs"></param>
        ///  <param name="nCol"></param>
        ///  <param name="x"></param>
        ///  <param name="irt"></param>
        ///  <param name="IFRQ"></param>
        ///  <param name="ifix"></param>
        ///  <param name="icen"></param>
        ///  <param name="ratio"></param>
        ///  <param name="nef"></param>
        ///  <param name="nvef"></param>
        ///  <param name="indef"></param>
        ///  <param name="ncoef"></param>
        ///  <param name="coef"></param>
        ///  <param name="icoef"></param>
        ///  <param name="ihess"></param>
        ///  <param name="algl"></param>
        ///  <param name="cov"></param>
        ///  <param name="ldcov">No longer used now that MXFAC is never called inside here.</param>
        ///  <param name="xmean"></param>
        ///  <param name="caze"></param>
        ///  <param name="ldcase"></param>
        ///  <param name="GR"></param>
        ///  <param name="OBS"></param>
        ///  <param name="smg"></param>
        ///  <param name="smh"></param>
        ///  <param name="iptr"></param>
        ///  <param name="idt"></param>
        ///  <param name="igrp"></param>
        ///  <param name="change"></param>
        ///  <param name="Zero"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        private static void coxhess(int nobs, int nCol, double[] x, int irt, int IFRQ, int ifix, int icen, double ratio, int nef, int[] nvef, int[] indef, int ncoef, double[,] coef, int icoef, bool ihess, out double algl, double[,] cov, int ldcov, double[] xmean, double[,] caze, int ldcase, double[] GR, double[] OBS, double[] smg, double[] smh, int[] iptr, int[] idt, int[] igrp, out bool change, bool Zero, ref int ifault)
        {
            int irank = 0;
            int kk = 0; int i;
            int ii;
            int ncoef1 = 0;
            double xmin = 0; double XMax = 0;
            double smu = 0;

            // tolerance
            const double tol = 0.000000000001;
            change = false;
            do
            {
                int igr = 0;
                for (i = 1; i <= ncoef; i++)
                {
                    for (ii = 1; ii <= i; ii++)
                    {
                        cov[ii, i] = 0.0;
                    }
                }
                for (i = 1; i <= ncoef; i++)
                {
                    GR[i] = 0.0;
                }
                algl = 0.0;
                int jj;
                int k;
                int iq;
                int j;
                for (i = 1; i <= nobs; i++)
                {
                    k = iptr[i];
                    if (igrp[k] >= 0)
                    {
                        if (igrp[k] != igr)
                        {
                            for (iq = 1; iq <= ncoef; iq++)
                            {
                                smg[iq] = 0.0;
                            }
                            for (iq = 1; iq <= ncoef * ncoef; iq++)
                            {
                                smh[iq] = 0.0;
                            }
                            smu = 0.0;
                            igr = igrp[k];
                        }
                        double xfix;
                        double xcen;
                        double xpar;
                        double xlt;
                        double xfrq;
                        double xrt;
                        int nrmiss;
                        coxvars(x, (k - 1) * nCol, irt, 0, IFRQ, ifix, 0, icen, out xrt, out xlt, out xfrq, out xfix, out xpar, out xcen, out nrmiss);
                        if (xfrq >= 0.0)
                        {
                            int icnn = Convert.ToInt32(xcen);
                            genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef1, OBS, ref nrmiss, ref ifault);
                            double zdot = 0.0;
                            double zdot_base = 0.0;
                            for (iq = 1; iq <= ncoef; iq++)
                            {
                                zdot_base = zdot_base + coef[iq, icoef] * OBS[iq];
                                OBS[iq] = OBS[iq] - xmean[iq];
                                zdot = zdot + coef[iq, icoef] * OBS[iq];
                            }
                            double xx = xfix + zdot;
                            double xx_base = xfix + zdot_base;
                            if (icnn == 0)
                            {
                                for (iq = 1; iq <= ncoef; iq++)
                                {
                                    GR[iq] = GR[iq] + xfrq * OBS[iq];
                                }
                                algl = algl + xfrq * Math.Min(xx, 30.0);
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
                            smu = smu + xfrq * u;
                            for (iq = 1; iq <= ncoef; iq++)
                            {
                                smg[iq] = smg[iq] + (xfrq * u) * OBS[iq];
                            }
                            double xtmp;
                            if (ihess)
                            {
                                for (j = 1; j <= ncoef; j++)
                                {
                                    xtmp = xfrq * u * OBS[j];
                                    for (ii = 1; ii <= j; ii++)
                                    {
                                        smh[ii + (j - 1) * ncoef] = smh[ii + (j - 1) * ncoef] + OBS[ii] * xtmp;
                                    }
                                }
                            }
                            if (idt[k] > 0)
                            {
                                int M = i;
                                jj = idt[k];
                                for (j = 1; j <= jj; j++)
                                {
                                    algl = algl - xfrq * Math.Log(smu);
                                    for (iq = 1; iq <= ncoef; iq++)
                                    {
                                        GR[iq] = GR[iq] + (-xfrq / smu) * smg[iq];
                                    }
                                    int L;
                                    if (!(ihess))
                                    {
                                        for (iq = 1; iq <= ncoef; iq++)
                                        {
                                            OBS[iq] = OBS[iq] + (-1.0 / smu) * smg[iq];
                                        }
                                        for (L = 1; L <= ncoef; L++)
                                        {
                                            xtmp = xfrq * OBS[L];
                                            for (ii = 1; ii <= L; ii++)
                                            {
                                                cov[ii, L] = cov[ii, L] + OBS[ii] * xtmp;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        double tmp = xfrq / smu;
                                        for (L = 1; L <= ncoef; L++)
                                        {
                                            xtmp = -tmp * smg[L] / smu;
                                            for (ii = 1; ii <= L; ii++)
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
                                            M = M - 1;
                                            k = iptr[M];
                                        }
                                        while (igrp[k] < 0);
                                        if (IFRQ > 0)
                                        {
                                            xfrq = x[IFRQ + (k - 1) * nCol];
                                        }
                                        genregs(nCol, x, 1 + (k - 1) * nCol, nef, nvef, indef, 2, ref ncoef1, OBS, ref nrmiss, ref ifault);
                                        for (iq = 1; iq <= ncoef; iq++)
                                        {
                                            OBS[iq] = OBS[iq] - xmean[iq];
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                igr = 0;
                bool strat = false;
                if (ratio != -1.0)
                {
                    for (i = 1; i <= nobs; i++)
                    {
                        k = iptr[i];
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
                    for (i = nobs; i >= 1; i--)
                    {
                        k = iptr[i];
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
                    for (i = 1; i <= nobs - 1; i++)
                    {
                        k = iptr[i];
                        if (igrp[k] >= 0 & Convert.ToInt64(caze[k, 4]) == 0)
                        {
                            if (igr != igrp[k])
                            {
                                igr = igrp[k];
                            }
                            j = i;
                            bool OK = true;
                            do
                            {
                                j = j + 1;
                                if (j > nobs)
                                {
                                    OK = false;
                                    break; /* TRANSWARNING: check that break is in correct scope */
                                }
                                kk = iptr[j];
                            }
                            while (igrp[kk] < 0 | Convert.ToInt64(caze[kk, 4]) != 0);
                            if (igrp[kk] != igr)
                            {
                                OK = false;
                            }
                            if (OK)
                            {
                                if (caze[kk, 3] > ratio * caze[k, 2])
                                {
                                    XMax = caze[k, 2];
                                    int iimax = 1;
                                    int imax = igrp[1];
                                    for (iq = 1; iq <= nobs; iq++)
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
                                    for (ii = j; ii <= nobs; ii++)
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
                                                    for (jj = j + 1; jj <= ii; jj++)
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
                if (!(strat))
                {
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }
            while (true);
            for (i = 1; i <= ncoef; i++)
            {
                if (iptr[nobs + i] == 0 | (Zero & (coef[i, icoef] == 0.0)))
                {
                    GR[i] = 0.0;
                    for (ii = 1; ii <= i; ii++)
                    {
                        cov[ii, i] = 0.0;
                    }
                    for (ii = i + 1; ii <= ncoef - i; ii++)
                    {
                        cov[i, ii] = 0.0;
                    }
                }
            }
            // Call MXFAC(ncoef, cov(1, 1), ldcov, 100# * 0.000000119237, irank, cov(1, 1), ldcov, ifault)
            cholfac(ncoef, cov, cov, tol, ref irank, ref ifault);
            if (ifault != 0)
            {
                // force a convergence failure if the matrix does not decompose fully
                // find the predictor that caused singularity and add it to the error code for reporting
                ifault = 100;
                for (i = 1; i <= ncoef; i++)
                {
                    double xcovx = 0.0;
                    for (ii = 1; ii <= ncoef; ii++)
                    {
                        xcovx = xcovx + cov[i, ii];
                    }
                    if (Math.Abs(xcovx) < tol)
                    {
                        ifault = ifault + i;
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                return;
            }
            for (i = 1; i <= ncoef; i++)
            {
                if (cov[i, i] == 0.0 & iptr[nobs + i] == 1)
                {
                    GR[i] = 0.0;
                    for (ii = 1; ii <= i; ii++)
                    {
                        cov[ii, i] = 0.0;
                    }
                    for (ii = i + 1; ii <= ncoef - i; ii++)
                    {
                        cov[i, ii] = 0.0;
                    }
                }
            }
            Regress1.mxinv2(ncoef, cov, GR, true, true, false, cov, out irank, ref ifault);
            if (ifault != 0)
            {
                return;
            }
            Regress1.mxinv2(ncoef, cov, GR, true, false, false, cov, out irank, ref ifault);
        }


        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="Variable"></param>
        ///  <param name="cd">A ColumnData structure whose Groups field is filled in with the groups if the variable is binary, and ignored otherwise</param>
        ///  <returns></returns>
        ///  <remarks>TODO: This used to set bins to 1 if only 1 bin, 99 if >2 bins.  Was this ever used?</remarks>
        private static bool is_binary(DoubleVariable Variable, ColumnData cd)
        {

            if (Variable.Length < 2)
            {
                return false;
            }

            double x1 = Variable.Data[0];
            double x2 = 0;
            int i;
            for (i = 1; i <= Variable.Length - 1; i++)
            {
                if (Variable.Data[i] != x1)
                {
                    x2 = Variable.Data[i];
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }
            if (i >= Variable.Length)
            {
                return false;
            }

            bool OK = true;
            for (i = 1; i <= Variable.Length - 1; i++)
            {
                if (Variable.Data[i] != x1 & Variable.Data[i] != x2)
                {
                    OK = false;
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }

            if (OK)
            {
                // need to sort x1 and x2 in alphanumeric order as the encoded group identifier gets sorted before plotting
                if (x2 < x1)
                {
                    double xx = x2;
                    x2 = x1;
                    x1 = xx;
                }
                cd.Groups = new List<Group> { new Group(x1.ToString(), x1), new Group(x2.ToString(), x2) };
            }
            return OK;
        }


        ///  <summary>
        ///  upper triangular factorization of a positive real definite symmetric matrix by Choleski square root method
        ///  </summary>
        ///  <param name="N"></param>
        ///  <param name="a"></param>
        ///  <param name="r"></param>
        ///  <param name="tol"></param>
        ///  <param name="irank"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        private static void cholfac(int N, double[,] a, double[,] r, double tol, ref int irank, ref int ifault)
        {
            int i; int ii;
            int j;

            // check tolerance
            if (tol < 0.0 | tol > 1.0)
            {
                ifault = 1;
            }
            if (ifault != 0)
            {
                return;
            }

            // take a copy of the upper triangle of the symmetric matrix
            for (j = 1; j <= N; j++)
            {
                for (ii = 1; ii <= j; ii++)
                {
                    r[ii, j] = a[ii, j];
                }
            }

            // decompose by Choleski's square root method
            int info = 0;
            irank = 0;
            for (j = 1; j <= N; j++)
            {
                double s = 0.0;
                double x = tol * Math.Sqrt(Math.Abs(r[j, j]));
                int k;
                for (k = 1; k <= j - 1; k++)
                {
                    double vvdot = 0.0;
                    for (ii = 1; ii <= k - 1; ii++)
                    {
                        vvdot = vvdot + r[ii, k] * r[ii, j];
                    }
                    double t = r[k, j] - vvdot;
                    if (r[k, k] != 0.0)
                    {
                        t = t / r[k, k];
                        r[k, j] = t;
                        s = s + t * t;
                    }
                    else
                    {
                        if (info == 0)
                        {
                            if (Math.Abs(t) > x * eucnrm(k - 1, r))
                            {
                                info = j;
                            }
                        }
                        r[j, k] = 0.0;
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
                    {
                        info = j;
                    }
                }
                else
                {
                    irank = irank + 1;
                }
                r[j, j] = Math.Sqrt(s);
            }
            if (info != 0)
            {
                ifault = 2;
            }

            // fill the lower triangle with zeros
            for (i = 1; i <= N - 1; i++)
            {
                for (ii = i + 1; ii <= N; ii++)
                {
                    r[ii, i] = 0.0;
                }
            }

        }


        ///  <summary>
        ///  euclidean norm of a vector in a matrix
        ///  = sqr(x'*x)
        ///  </summary>
        ///  <param name="idx"></param>
        ///  <param name="x"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static double eucnrm(int idx, double[,] x)
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
                            ssq = 1.0 + ssq * Math.Pow((scal / absxi), 2.0);
                            scal = absxi;
                        }
                        else
                        {
                            ssq = ssq + Math.Pow((absxi / scal), 2.0);
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
        ///  <param name="u"></param>
        ///  <param name="d"></param>
        ///  <param name="Q"></param>
        ///  <remarks></remarks>
        private static void uoverd(double u, double d, out double Q)
        {
            if (u == Constant.MISSING | d == Constant.MISSING)
            {
                Q = Constant.MISSING;
                return;
            }
            double absden = Math.Abs(d);
            if (absden <= 1.0)
            {
                const double BIG = Constant.LMREAL;
                if (Math.Abs(u) < BIG * absden)
                {
                    Q = u / d;
                    return;
                }
                if (u == 0.0)
                {
                    Q = Constant.MISSING;
                }
                else if (d >= 0.0)
                {
                    if (u >= 0.0)
                    {
                        Q = Constant.LMREAL;
                    }
                    else
                    {
                        Q = -Constant.LMREAL;
                    }
                }
                else
                {
                    if (u >= 0.0)
                    {
                        Q = -Constant.LMREAL;
                    }
                    else
                    {
                        Q = Constant.LMREAL;
                    }
                }
                return;
            }
            const double small = Constant.SPREAL;
            if (Math.Abs(u) >= small * absden)
            {
                Q = u / d;
                return;
            }
            Q = 0.0;
            return;
            // TRANSWARNING: Unreachable code detected and removed 
        }


        private static void coxvars(double[] x, int ix1, int irt, int ilt, int IFRQ, int ifix, int ipar, int icen, out double xrt, out double xlt, out double xfrq, out double xfix, out double xpar, out double xcen, out int nrmiss)
        {
            //  GET SPECIAL VARIABLES
            nrmiss = 0;
            if (ipar > 0)
            {
                xpar = x[ix1 + ipar];
                if (xpar == Constant.MISSING)
                {
                    nrmiss = nrmiss + 1;
                }
            }
            else
            {
                xpar = 1.0;
            }
            if (IFRQ > 0)
            {
                xfrq = x[ix1 + IFRQ];
                if (xfrq == Constant.MISSING)
                {
                    nrmiss = nrmiss + 1;
                }
            }
            else
            {
                xfrq = 1.0;
            }
            if (ifix > 0)
            {
                xfix = x[ix1 + ifix];
                if (xfix == Constant.MISSING)
                {
                    nrmiss = nrmiss + 1;
                }
            }
            else
            {
                xfix = 0.0;
            }
            if (icen > 0)
            {
                xcen = x[ix1 + icen];
                if (xcen == Constant.MISSING)
                {
                    nrmiss = nrmiss + 1;
                }
            }
            else
            {
                xcen = 0.0;
            }
            if (ilt > 0)
            {
                xlt = x[ix1 + ilt];
                if (xlt == Constant.MISSING)
                {
                    nrmiss = nrmiss + 1;
                }
            }
            else
            {
                xlt = 0.0;
            }
            if (irt > 0)
            {
                xrt = x[ix1 + irt];
                if (xrt == Constant.MISSING)
                {
                    nrmiss = nrmiss + 1;
                }
            }
            else
            {
                xrt = 0.0;
            }
        }


        private static void MXPOSE(int m, int n, double[] a, out int ifault)
        {

            //      adapted from CACM Algorithm 380 - in situ transpose of a rectangular matrix

            //      a is a one-dimensional array of length mn = m*n, which
            //      contains the m x n matrix to be stored columnwise.
            //      move is a one-dimensional array of length iwrk used
            //      to store information to speed up the process.

            double b;
            int i; int i1;
            int i2;

            ifault = 0;
            if (m < 2 | n < 2)
            {
                return;
            }
            int mn = m * n;
            int iwrk = ((int)(Math.Floor((double)(m + n) / 2)));
            int[] move = new int[iwrk + 1];

            if (m == n)
            {
                //  if matrix is square, exchange elements a(i,j) and a(j,i).
                int n1 = n - 1;
                for (i = 1; i <= n1; i++)
                {
                    int J1 = i + 1;
                    int j;
                    for (j = J1; j <= n; j++)
                    {
                        i1 = i + (j - 1) * n;
                        i2 = j + (i - 1) * m;
                        b = a[i1];
                        a[i1] = a[i2];
                        a[i2] = b;
                    }
                }
                return;
            }

            int nCount = 2;
            int k = mn - 1;
            for (i = 1; i <= iwrk; i++)
            {
                move[i] = 0;
            }

            if (m >= 3 & n >= 3)
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
                    {
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                while (true);
                nCount = nCount + ir2 - 1;
            }

            //  set initial values for search
            i = 1;
            int im = m;

            //  at least one loop must be re-arranged - so jump in at rearragement point
            bool jump = true;
            bool rearrange = true;

            //  search for loops to rearrange
            do
            {
                int i1divn;
                if (jump == false)
                {
                    int max = k - i;
                    i = i + 1;
                    if (i > max)
                    {
                        ifault = 3;
                        return;
                    }
                    im = im + m;
                    if (im > k)
                    {
                        im = im - k;
                    }
                    i2 = im;
                    if (i == i2)
                    {
                        rearrange = false;
                    }
                    else if (i > iwrk)
                    {
                        if (i2 > i & i2 < max)
                        {
                            i1 = i2;
                            do
                            {
                                i1divn = ((int)(Math.Floor((double)i1 / n)));
                                i2 = m * (i1 - n * i1divn) + i1divn;
                                if (i2 <= i | i2 >= max)
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
                    i1 = i;
                    int kmi = k - i;
                    b = a[i1 + 1];
                    int i1c = kmi;
                    double C = a[i1c + 1];
                    do
                    {
                        i1divn = ((int)(Math.Floor((double)i1 / n)));
                        i2 = m * (i1 - n * i1divn) + i1divn;
                        int i2c = k - i2;
                        if (i1 <= iwrk)
                        {
                            move[i1] = 2;
                        }
                        if (i1c <= iwrk)
                        {
                            move[i1c] = 2;
                        }
                        nCount = nCount + 2;
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
                {
                    break;
                }
            }
            while (true);

        }


        ///  <summary>
        ///  solve for alpha by Newton Raphson iteration - see Kalbfleisch and Prentice P293
        ///  </summary>
        ///  <param name="dead"></param>
        ///  <param name="dead_theta"></param>
        ///  <param name="risk_theta"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        private static double alpha_solve(double dead, double[] dead_theta, double risk_theta)
        {
            const double tol = 0.0000001;
            double alpha = Math.Exp(-dead / risk_theta);
            int iter = 0;

            do
            {
                double gi = 0.0;
                double gi1 = 0.0;
                int i;
                for (i = 1; i <= Convert.ToInt32(dead); i++)
                {
                    double xii = Math.Pow(alpha, dead_theta[i]);
                    gi = gi + dead_theta[i] / (1.0 - xii);
                    gi1 = gi1 + xii * Math.Pow(dead_theta[i], 2.0) / (alpha * Math.Pow((1.0 - xii), 2.0));
                }
                double stp = (gi - risk_theta) / gi1;
                alpha = alpha - stp;
                if (Math.Abs(stp) <= tol)
                {
                    break;
                }
                iter = iter + 1;
                if (iter > 300)
                {
                    break;
                }
            }
            while (true);
            return iter < 300 ? alpha : Constant.MISSING;
        }


        public static StepResult RptCoxBaselineToReport(ITemplateHost host, ParameterBag parameters)
        {
            return RptCoxBaseline(host, parameters, false, "", false);
        }


        public static StepResult RptCoxBaselineToWorksheet(ITemplateHost host, ParameterBag parameters)
        {
            return RptCoxBaseline(host, parameters, false, "", true);
        }


        public static StepResult RptCoxHazardPlots(ITemplateHost host, ParameterBag parameters)
        {
            bool[] selectedGroups = ((bool[])(parameters["group"].Data));
            DataFrame subgroupsFrame = parameters["subgroups"].AsDataFrame;
            StringVariable subgroupsVariable = subgroupsFrame.Variables[0].AsStringVariable;
            for (int i = 0; i <= selectedGroups.Length - 1; i++)
            {
                if (selectedGroups[i])
                {
                    string selectedGroup = subgroupsVariable.Data[i];
                    return RptCoxBaseline(host, parameters, true, selectedGroup, false);
                }
            }
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }


        private static StepResult RptCoxBaseline(ITemplateHost host, ParameterBag parameters, bool plot, string groupVar, bool createGrid)
        {
            int i;
            double watch_time;

            double[,] ARR2 = ((double[,])(parameters["ARR2"].Data));

            //  baseline S and H and S and H values at mean covariate
            int iobs = Convert.ToInt32(ARR2[0, 0]);
            coxp[] z = new coxp[iobs + 2 ];
            z[0] = new coxp();
            for (i = 1; i <= iobs; i++)
            {
                //  use estimates as starting values if needed
                z[i] = new coxp
                           {
                               strat = Convert.ToInt32(ARR2[i, 9]),
                               TM = ARR2[i, 6],
                               cens = Convert.ToInt32(ARR2[i, 7]),
                               s = ARR2[i, 1],
                               h = ARR2[i, 4],
                               exb = ARR2[i, 10],
                               Index = i
                           };
            }

            Array.Sort(z, 1, iobs, new CoxpByStratTmThenExb());
            // int istart = 1; 
            int lastStratum = z[1].strat;
            double alpha_product = 1.0;
            double alpha_productx = 1.0;
            int istrata = 1;

            for (i = 1; i <= iobs; i++)
            {
                if (z[i].strat != lastStratum)
                {
                    //  new stratum
                    alpha_product = 1.0;
                    alpha_productx = 1.0;
                    lastStratum = z[i].strat;
                    istrata = istrata + 1;
                }
                watch_time = z[i].TM;
                double[] dead_theta = new double[30 + 1 ];
                double dead = 0.0;
                int iinc = 0;
                for (int j = i; j <= iobs; j++)
                {
                    if (z[i].strat != z[j].strat)
                        break;
                    if (z[j].TM != watch_time)
                        break;
                    if (z[j].cens != 0.0)
                    {
                        dead = dead + 1.0;
                        Array transTemp0 = dead_theta;
                        if (dead > transTemp0.GetUpperBound(0))
                        {
                            // create temp variable for copying values 
                            double[] transTemp7 = new double[Convert.ToInt32(dead) + 1 ];
                            Array.Copy(dead_theta, transTemp7, Math.Min(dead_theta.Length, transTemp7.Length));
                            dead_theta = transTemp7;
                        }
                        dead_theta[Convert.ToInt32(dead)] = z[j].exb;
                    }
                    iinc = iinc + 1;
                }
                // .exb must be sorted in reverse order for risk_theta to start with the correct value when d>1
                double risk_theta = 0.0;
                for (int j = i; j <= iobs; j++)
                {
                    if (z[i].strat != z[j].strat)
                    {
                        break;
                    }
                    risk_theta = risk_theta + z[j].exb;
                }
                bool erra = false;
                double alpha_i;
                double alpha_ix;
                if (dead == 0.0)
                {
                    alpha_i = 1.0;
                    alpha_ix = alpha_i;
                }
                else if (dead == 1.0)
                {
                    alpha_i = Math.Pow((1.0 - z[i].exb / risk_theta), (1.0 / z[i].exb));
                    alpha_ix = Math.Exp(-dead / risk_theta);
                }
                else
                {
                    alpha_ix = Math.Exp(-dead / risk_theta);
                    alpha_i = alpha_solve(dead, dead_theta, risk_theta);
                    erra = (alpha_i == Constant.MISSING);
                }
                alpha_product = alpha_product * alpha_i;
                //  use a non-iterative solution for the hazard - see Stata manual
                alpha_productx = alpha_productx * alpha_ix;
                if (erra == false)
                {
                    for (int j = i; j <= i + iinc; j++)
                    {
                        if (z[j] == null)
                        {
                            z[j] = new coxp();
                        }
                        z[j].s = alpha_product;
                        z[j].h = -Math.Log(alpha_productx);
                    }
                }
                i = i + iinc - 1;
            }

            ParameterBag outputParameters = new ParameterBag();
            if (!(plot))
            {
                // write to report in time-sorted order
                //  RTF_LoadTemplate("cox_s.rtf")
                watch_time = Constant.MISSING;
                IList<ParameterBag> timeList = new List<ParameterBag>();
                outputParameters.AddOutput("*time", timeList);
                for (i = 1; i <= iobs; i++)
                {
                    if (z[i].cens != 0.0 & watch_time != z[i].TM)
                    {
                        watch_time = z[i].TM;
                        ParameterBag timeParameters = new ParameterBag();
                        timeList.Add(timeParameters);
                        timeParameters.AddOutput("time", host.RoundU(z[i].TM));
                        timeParameters.AddOutput("sur", host.RoundU(z[i].s));
                        timeParameters.AddOutput("haz", host.RoundU(z[i].h));
                        timeParameters.AddOutput("hr", host.RoundU(z[i].exb));
                    }
                }
            }

            // restore the original record order if calling plot function or output to worksheet
            if (plot || createGrid)
            {
                Array.Sort(z, 1, iobs, new CoxpByIndex());
            }

            if (plot)
            {
                // bypass reporting and plot if called by the plot function
                CoxPlot(host, parameters, z, iobs, istrata, groupVar, outputParameters);
            }
            else if (createGrid)
            {
                // save to worksheet if requested
                DoubleVariable survivalVariable = new DoubleVariable(iobs, "Survival (baseline)");
                DoubleVariable hazardVariable = new DoubleVariable(iobs, "Hazard (baseline cumulative)");
                DoubleVariable hazardRatioVariable = new DoubleVariable(iobs, "Hazard ratio");
                DataFrame resultsFrame = new DataFrame();
                resultsFrame.Variables.Add(survivalVariable);
                resultsFrame.Variables.Add(hazardVariable);
                resultsFrame.Variables.Add(hazardRatioVariable);
                for (i = 1; i <= iobs; i++)
                {
                    survivalVariable.set_Data(i - 1, z[i].s);
                    hazardVariable.set_Data(i - 1, z[i].h);
                    hazardRatioVariable.set_Data(i - 1, z[i].exb);
                }
                outputParameters.AddOutput("results", resultsFrame);
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        private static void CoxPlot(ITemplateHost host, ParameterBag parameters, coxp[] z, int iobs, int istrata, string groupVar, ParameterBag outputParameters)
        {
            int j3; int i;
            int igroups;
            int groupid = 0;
            bool grouped; bool stratified;

            double[,] ARR2 = ((double[,])(parameters["ARR2"].Data));
            double[, ,] ARR3 = ((double[, ,])(parameters["ARR3"].Data));
            ColumnData[] CDAT1 = ((ColumnData[])(parameters["CDAT1"].Data));
            double[,] holdx = ((double[,])(parameters["holdx"].Data));
            bool use_tic = parameters["use-tics"].AsBoolean;
            bool use_marker = parameters["use-markers"].AsBoolean;

            int[] gn = new int[3 + 1];

            int ncoef = Convert.ToInt32(ARR2[1, 0]);
            IComparer<coxp> comparer;
            switch (groupVar.ToLower())
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
                    comparer = new CoxpByStratThenTm();
                    break;
                default:
                    stratified = false;
                    groupid = -1;
                    igroups = 0;
                    for (i = 1; i <= ncoef; i++)
                    {
                        if ((CDAT1[i].Title).Trim().ToLower().Equals(groupVar.ToLower()))
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
                for (i = 1; i <= iobs; i++)
                {
                    z[i].id = Convert.ToInt32(holdx[i, groupid]);
                }
            }
            else
            {
                for (i = 1; i <= iobs; i++)
                {
                    z[i].id = 1;
                }
            }

            //  TODO: The original SD2 code removed anything other than the first sort in the order - should we also do that?
            Array.Sort(z, 1, iobs, comparer);

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            // loop twice - first survival then hazard plot
            for (j3 = 1; j3 <= 2; j3++)
            {

                using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
                {
                    ch.DataMaxX = double.MinValue;
                    ch.DataMaxY = double.MinValue;
                    ch.DataMinX = double.MaxValue;
                    ch.DataMinY = double.MaxValue;
                    const string tim = "Time";
                    string vq = grouped ? "(individual)" : "(baseline)";
                    string vt;
                    string vx;
                    string vy;
                    switch (j3)
                    {
                        case 1:
                            vx = tim;
                            vy = "Survival Probability " + vq;
                            vt = "Survival Plot (Cox regression)";
                            ch.DataMaxY = 1;
                            ch.DataMinY = 0;
                            for (i = 1; i <= iobs; i++)
                            {
                                if (z[i].TM > ch.DataMaxX)
                                    ch.DataMaxX = z[i].TM;
                                if (z[i].TM < ch.DataMinX)
                                    ch.DataMinX = z[i].TM;
                            }
                            break;
                        case 2:
                            vx = tim;
                            vy = "Cumulative Hazard " + vq;
                            vt = "Hazard Plot (Cox regression)";
                            for (i = 1; i <= iobs; i++)
                            {
                                if (z[i].TM > ch.DataMaxX)
                                {
                                    ch.DataMaxX = z[i].TM;
                                }
                                if (z[i].TM < ch.DataMinX)
                                {
                                    ch.DataMinX = z[i].TM;
                                }
                                double haz;
                                if (grouped)
                                {
                                    haz = Math.Pow(z[i].s, Math.Exp(Convert.ToDouble(z[i].id) * ARR3[1, groupid, 1]));
                                    haz = haz > 0.0 ? -Math.Log(haz) : Constant.MISSING;
                                }
                                else
                                    haz = z[i].h;
                                if (haz != Constant.MISSING & haz > ch.DataMaxY & z[i].cens != 0)
                                    ch.DataMaxY = haz;
                                if (haz != Constant.MISSING & haz < ch.DataMinY & z[i].cens != 0)
                                    ch.DataMinY = haz;
                            }
                            break;
                        default:
                            throw new Exception("Unexpected case");
                    }

                    ParameterBag cox1Parameters = new ParameterBag();
                    chartList.Add(cox1Parameters);
                    string rtf = ch.PlotCox1AndReturnRtf(host, vt, z, iobs, stratified, grouped, istrata, igroups, CDAT1, groupid, use_marker, use_tic, ARR3, j3, vx, vy, ref gn);
                    cox1Parameters.AddOutput("chart", rtf);
                }
            }

            // do a -ln(-ln(s)) vs. ln(t) plot to check for parallel categories/proportional hazards
            if (grouped)
            {
                double[] xp = new double[iobs + 1];
                double[] yp = new double[iobs + 1];
                xp[0] = Constant.MISSING;
                yp[0] = Constant.MISSING;
                for (i = 1; i <= iobs; i++)
                {
                    double surv = Math.Pow(z[i].s, Math.Exp(Convert.ToDouble(z[i].id) * ARR3[1, groupid, 1]));
                    xp[i] = Math.Log(z[i].TM);
                    yp[i] = -Math.Log(-Math.Log(surv));
                }
                // Plot a metafile version
                ChartDefinition cd = new ChartDefinition();
                cd.AddXSeries(xp, null);
                cd.AddYSeries(yp, null);
                ParameterBag cox2Parameters = new ParameterBag();
                chartList.Add(cox2Parameters);
                using (ChartRenderer ch = new ChartRenderer(cd))
                {
                    string rtf = ch.PlotCox2AndReturnRtf(host, gn, igroups, xp, yp, CDAT1, groupid);
                    cox2Parameters.AddOutput("chart", rtf);
                }
            }
        }


        public static StepResult RptCoxResiduals(ITemplateHost host, ParameterBag parameters)
        {
            int jcoef = 0; int i;
            int istrata = 0;
            int lastStratum = 0;
            double alpha_product = 0; double alpha_productx = 0;
            double xf; double rd; double rm; double rc;

            double[,] ARR2 = ((double[,])(parameters["ARR2"].Data));

            bool save = parameters["save"].AsBoolean;

            int iobs = Convert.ToInt32(ARR2[0, 0]);
            jcoef = Convert.ToInt32(ARR2[1, 0]);
            coxp[] z = new coxp[iobs + 2];
            for (i = 1; i <= iobs; i++)
            {
                z[i] = new coxp
                           {
                               strat = Convert.ToInt32(ARR2[i, 9]),
                               TM = ARR2[i, 6],
                               id = Convert.ToInt32(ARR2[i, 8]),
                               cens = Convert.ToInt32(ARR2[i, 7]),
                               //  use estimates as starting values if needed
                               s = ARR2[i, 1],
                               h = ARR2[i, 4],
                               //  baseline sum(exp(bz))
                               exb = ARR2[i, 10],
                               Index = i
                           };
            }

            Array.Sort(z, 1, iobs, new CoxpByStratTmThenExb());

            for (i = 1; i <= iobs; i++)
            {
                if (z[i].strat != lastStratum)
                {
                    //  new stratum
                    alpha_product = 1.0;
                    alpha_productx = 1.0;
                    lastStratum = z[i].strat;
                    istrata = istrata + 1;
                }
                double watch_time = z[i].TM;
                double[] dead_theta = new double[30 + 1];
                double dead = 0.0;
                int iinc = 0;
                int j;
                for (j = i; j <= iobs; j++)
                {
                    if (z[i].strat != z[j].strat)
                        break;
                    if (z[j].TM != watch_time)
                        break;
                    if (z[j].cens != 0.0)
                    {
                        dead = dead + 1.0;
                        if (dead > dead_theta.GetUpperBound(0))
                        {
                            // create temp variable for copying values 
                            double[] transTemp8 = new double[Convert.ToInt32(dead) + 1];
                            Array.Copy(dead_theta, transTemp8, Math.Min(dead_theta.Length, transTemp8.Length));
                            dead_theta = transTemp8;
                        }
                        dead_theta[Convert.ToInt32(dead)] = z[j].exb;
                    }
                    iinc = iinc + 1;
                }
                // .exb must be sorted in reverse order for risk_theta to start with the correct value when d>1
                double risk_theta = 0.0;
                for (j = i; j <= iobs; j++)
                {
                    if (z[i].strat != z[j].strat)
                    {
                        break;
                    }
                    risk_theta = risk_theta + z[j].exb;
                }
                bool erra = false;
                double alpha_i;
                double alpha_ix;
                if (dead == 0.0)
                {
                    alpha_i = 1.0;
                    alpha_ix = alpha_i;
                }
                else if (dead == 1.0)
                {
                    alpha_i = Math.Pow((1.0 - z[i].exb / risk_theta), (1.0 / z[i].exb));
                    alpha_ix = Math.Exp(-dead / risk_theta);
                }
                else
                {
                    alpha_ix = Math.Exp(-dead / risk_theta);
                    alpha_i = alpha_solve(dead, dead_theta, risk_theta);
                    erra = (alpha_i == Constant.MISSING);
                }
                alpha_product = alpha_product * alpha_i;
                //  use a non-iterative solution for the hazard - see Stata manual
                alpha_productx = alpha_productx * alpha_ix;
                if (erra == false)
                {
                    for (j = i; j <= i + iinc; j++)
                    {
                        if (z[j] == null)
                        {
                            z[j] = new coxp();
                        }
                        z[j].s = alpha_product;
                        z[j].h = -Math.Log(alpha_productx);
                    }
                }
                i = i + iinc - 1;
            }

            int ictr = 0;
            double[] xp = new double[iobs + 1 ];
            double[] yp = new double[iobs + 1 ];
            double[] XR = new double[iobs + 1 ];
            for (i = 1; i <= iobs; i++)
            {
                if (z[i].s != 0.0)
                {
                    rc = z[i].exb * z[i].h;
                    rm = z[i].cens - rc;
                    rd = Math.Sign(rm) * Math.Sqrt(-2.0 * (rm + z[i].cens * Math.Log(z[i].cens - rm)));
                    ictr = ictr + 1;
                    yp[ictr] = rd;
                }
                xp[i] = z[i].TM;
            }

            ExFortran.Rank(xp, XR, 1, ictr, 1, out xf);
            yp[0] = Constant.MISSING; //  Ensure charts don't have bogus (0,0) points
            ParameterBag outputParameters = new ParameterBag();
            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                ParameterBag chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ch.PlotXYAndReturnRtf(host, xp, yp, "Time to event", "Deviance residual", "Deviance residuals vs. times", false, 0, false));
            }

            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                ParameterBag chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ch.PlotXYAndReturnRtf(host, XR, yp, "Rank of time to event", "Deviance residual", "Deviance residuals vs. ranks of times", false, 0, false));
            }

            // save to worksheet if requested
            if (save)
            {
                // restore the original record order if calling plot function or output to worksheet
                Array.Sort(z, 1, iobs, new CoxpByIndex());

                DoubleVariable leverageVariable = new DoubleVariable(iobs, "Leverage");
                DoubleVariable proportionalityVariable = new DoubleVariable(iobs, "Proportionality");
                DoubleVariable coxOakesResidualVariable = new DoubleVariable(iobs, "Cox-Oakes residual");
                DoubleVariable coxSnellResidualVariable = new DoubleVariable(iobs, "Cox-Snell residual");
                DoubleVariable martingaleResidualVariable = new DoubleVariable(iobs, "Martingale residual");
                DoubleVariable devianceResidualVariable = new DoubleVariable(iobs, "Deviance residual");
                DataFrame resultsFrame = new DataFrame();
                resultsFrame.Variables.Add(leverageVariable);
                resultsFrame.Variables.Add(proportionalityVariable);
                resultsFrame.Variables.Add(coxOakesResidualVariable);
                resultsFrame.Variables.Add(coxSnellResidualVariable);
                resultsFrame.Variables.Add(martingaleResidualVariable);
                resultsFrame.Variables.Add(devianceResidualVariable);
                for (i = 1; i <= iobs; i++)
                {
                    leverageVariable.set_Data(i - 1, ARR2[i, 2]);
                    proportionalityVariable.set_Data(i - 1, ARR2[i, 5]);
                    coxOakesResidualVariable.set_Data(i - 1, ARR2[i, 3]);
                    rc = z[i].exb * z[i].h;
                    rm = z[i].cens - rc;
                    rd = Math.Sign(rm) * Math.Sqrt(-2.0 * (rm + z[i].cens * Math.Log(z[i].cens - rm)));
                    coxSnellResidualVariable.set_Data(i - 1, rc);
                    martingaleResidualVariable.set_Data(i - 1, rm);
                    devianceResidualVariable.set_Data(i - 1, rd);
                }
                outputParameters.AddOutput("results", resultsFrame);

            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult RptCoxHazardRatios(ITemplateHost host, ParameterBag parameters)
        {
            double[,] ARR2 = ((double[,])(parameters["ARR2"].Data));
            double[, ,] ARR3 = ((double[, ,])(parameters["ARR3"].Data));
            ColumnData[] CDAT1 = ((ColumnData[])(parameters["CDAT1"].Data));

            //  RTF_LoadTemplate("cox_det.rtf") Then
            double GAMMA = parameters["gamma"].AsDouble;
            double cit;
            double P0;
            MathDbl.civ(0, out cit, GAMMA, out P0);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("pc", Formatting.XRound(100 * GAMMA, 2));
            outputParameters.AddOutput("pc2", Formatting.XRound(100 * GAMMA, 2));

            IList<ParameterBag> hazardList = new List<ParameterBag>();
            outputParameters.AddOutput("*hazard", hazardList);
            for (int i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
            {
                ParameterBag hazardParameters = new ParameterBag();
                hazardList.Add(hazardParameters);
                hazardParameters.AddOutput("par", CDAT1[i].Title);
                hazardParameters.AddOutput("ec", host.RoundU(Formatting.SafeExp(ARR3[1, i, 1])));
                hazardParameters.AddOutput("ell", host.RoundU(Formatting.SafeExp(ARR3[1, i, 1] - cit * ARR3[1, i, 2])));
                hazardParameters.AddOutput("eul", host.RoundU(Formatting.SafeExp(ARR3[1, i, 1] + cit * ARR3[1, i, 2])));
            }
            IList<ParameterBag> parameterList = new List<ParameterBag>();
            outputParameters.AddOutput("*parameter", parameterList);
            for (int i = 1; i <= Convert.ToInt32(ARR2[1, 0]); i++)
            {
                ParameterBag parameterParameters = new ParameterBag();
                parameterList.Add(parameterParameters);
                parameterParameters.AddOutput("par", CDAT1[i].Title);
                parameterParameters.AddOutput("coef", host.RoundU(ARR3[1, i, 1]));
                parameterParameters.AddOutput("se", host.RoundU(ARR3[1, i, 2]));
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptCoxModelAnalysis(ITemplateHost host, ParameterBag parameters)
        {
            double[,] ARR2 = ((double[,])(parameters["ARR2"].Data));
            //  RTF_LoadTemplate("cox_dev.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("ll0", host.RoundU(ARR2[3, 0]));
            outputParameters.AddOutput("ll", host.RoundU(ARR2[2, 0]));
            double x2dev = -2.0 * (ARR2[3, 0] - ARR2[2, 0]);
            outputParameters.AddOutput("x2", host.RoundU(x2dev));
            outputParameters.AddOutput("df", ARR2[1, 0].ToString());
            outputParameters.AddOutput("p", host.pval(PDF.chivalp(Math.Abs(x2dev), ARR2[1, 0])));
            return new StepResult(StepSuccess.Success, outputParameters);
        }

    }


}

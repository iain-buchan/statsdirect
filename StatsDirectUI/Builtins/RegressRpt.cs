using System;
using System.Collections.Generic;

using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    [Serializable]
    public class MinMax
    {
        public double MinX;
        public double MaxX;
        public double MinY;
        public double MaxY;
    }

    [Serializable]
    public class GroupedCovarianceData
    {
        public double[] a;
        public double[] b;
        public string[] bnam;
        public ColumnData[] cx;
        public double GAMMA;
        public int k;
        public int maxr;
        public int maxreps;
        public MinMax minMax;
        public int[] nxi;
        public int[,] ny;
        public double[] rssx;
        public string xlab;
        public double[] xmean;
        public double[,] xt;
        public double[, ,] y;
        public double[] ymean;
    }

    public class RegressRpt
    {
        public static ParameterBag RptGroupedLinearity(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame predictorFrame = parameters["predictor"].AsDataFrame;
            int nx = predictorFrame.Variables[0].Length;
            double[] x = predictorFrame.Variables[0].AsDoubleVariable.Data; //  0-based

            DataFrame outcomesFrame = parameters["outcomes"].AsDataFrame;

            double totssy = 0; double totny = 0; double totsy = 0; double sx = 0; double ssx = 0; double sxy = 0;
            double tntot = 0;
            for (int j = 0; j < nx; j++)
            {
                DoubleVariable v = outcomesFrame.Variables[j].AsDoubleVariable;
                double[] data = v.Data;
                double ysum = 0;
                double ysum2 = 0;
                for (int j2 = 0; j2 <= v.Length - 1; j2++)
                {
                    ysum += data[j2];
                    ysum2 += (data[j2] * data[j2]);
                }
                totssy += ysum2;
                totny += v.Length;
                totsy += ysum;
                tntot += (ysum * ysum / Convert.ToDouble(v.Length));
                sx += v.Length * x[j];
                ssx += v.Length * x[j] * x[j];
                sxy += x[j] * ysum;
            }
            double totssq = totssy - (totsy * totsy / totny);
            double rsdssq = totssy - tntot;
            double regssq = (sxy - (sx * totsy / totny)) * (sxy - (sx * totsy / totny)) / (ssx - (sx * sx / totny));
            // ssx = ssx - sx * sx / totny; 
            // sxy = sxy - sx * totsy / totny; 
            double devssq = totssq - regssq - rsdssq;
            double vr = regssq / (rsdssq / (totny - nx));
            double P = PDF.fvalp(vr, 1.0, totny - nx);
            string Q2 = P > 0.05 ? "NOT " : string.Empty;
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("reg_ssq", host.RoundU(regssq));
            outputParameters.AddOutput("reg_df", "1");
            outputParameters.AddOutput("reg_msq", host.RoundU(regssq));
            outputParameters.AddOutput("reg_vr", host.RoundU(vr));
            outputParameters.AddOutput("reg_p", host.pval(P));
            vr = (devssq / Convert.ToDouble(nx - 2)) / (rsdssq / (totny - nx));
            P = PDF.fvalp(vr, nx - 2, totny - nx);
            string Q = P <= 0.05 ? "NOT " : string.Empty;
            outputParameters.AddOutput("dev_ssq", host.RoundU(devssq));
            outputParameters.AddOutput("dev_df", (nx - 2).ToString());
            outputParameters.AddOutput("dev_msq", host.RoundU(devssq / Convert.ToDouble(nx - 2)));
            outputParameters.AddOutput("dev_vr", host.RoundU(vr));
            outputParameters.AddOutput("dev_p", host.pval(P));
            outputParameters.AddOutput("res_ssq", host.RoundU(rsdssq));
            outputParameters.AddOutput("res_df", (totny - nx).ToString());
            outputParameters.AddOutput("res_msq", host.RoundU(rsdssq / (totny - nx)));
            outputParameters.AddOutput("tot_ssq", host.RoundU(totssq));
            outputParameters.AddOutput("tot_df", (totny - 1).ToString());
            outputParameters.AddOutput("reg", Q2);
            outputParameters.AddOutput("lin", Q);
            return outputParameters;
        }


        public static ParameterBag RptGroupedCovariance(ITemplateHost host, ParameterBag parameters)
        {
            double gtxx = 0; double gtxy = 0; double gtyy = 0; double grandn = 0; double grandx = 0; double grandsqx = 0; double grandsqy = 0;
            double tsy = 0; double tsx = 0; double cit; double p0;
            double grandcpr = 0;
            double grandbit = 0;
            double residssq = 0; double t;
            double syy = 0; double sxx = 0; double sxy = 0; double tn = 0; double tnx = 0;

            GroupedCovarianceData gcd = ((GroupedCovarianceData)(parameters["gcd"].Data));
            double[] a = gcd.a;
            double[] b = gcd.b;
            string[] bnam = gcd.bnam;
            ColumnData[] cx = gcd.cx;
            double gamma = gcd.GAMMA;
            int k = gcd.k;
            int maxreps = gcd.maxreps;
            int[] nxi = gcd.nxi;
            int[,] ny = gcd.ny;
            double[] rssx = gcd.rssx;
            string xlab = gcd.xlab;
            double[] xmean = gcd.xmean;
            double[,] xt = gcd.xt;
            double[, ,] y = gcd.y;
            double[] ymean = gcd.ymean;

            bool yrep = maxreps > 1;

            //  By now:
            //  yrep is true if y replicates are being used, false otherwise (in which case a single predictor is being used and maxreps = 1)
            //  - y(k, maxr, maxreps) contains the outcome data for each predictor - the p'th predictor is in y(, , p)
            // main calcs on each xy pair in turn
            for (int g = 1; g <= k; g++)
            {
                double sx = 0.0;
                double sy = 0.0;
                double txx = 0.0;
                double txy = 0.0;
                double tyy = 0.0;
                double tny = 0.0;
                for (int j = 1; j <= nxi[g]; j++)
                {
                    double qsy = 0.0;
                    for (int j2 = 1; j2 <= ny[g, j]; j2++)
                    {
                        qsy += y[g, j, j2];
                        tyy += (y[g, j, j2] * y[g, j, j2]);
                    }
                    tny += ny[g, j];
                    sy += qsy;
                    sx += ny[g, j] * xt[g, j];
                    txx += ny[g, j] * xt[g, j] * xt[g, j];
                    txy += xt[g, j] * qsy;
                }
                gtxx += txx;
                gtxy += txy;
                gtyy += tyy;
                xmean[g] = sx / tny;
                ymean[g] = sy / tny;
                double ssx = txx - (sx * sx / tny);
                rssx[g] = 1.0 / ssx;
                double ssy = tyy - (sy * sy / tny);
                double ssxy = txy - (sx * sy / tny);
                b[g] = ssxy / ssx;
                a[g] = (sy / tny) - b[g] * (sx / tny);
                bnam[g] = g.ToString() + " (" + cx[g].Title + ")";
                grandn += tny;
                grandx += sx;
                grandsqx += ssx;
                grandsqy += ssy;
                grandcpr += ssxy;
                grandbit += (ssxy * ssxy / ssx);
                residssq += ssy - (ssxy * ssxy / ssx);
                syy += (sy * sy / tny);
                sxx += (sx * sx / tny);
                sxy += (sx * sy / tny);
                tsx += sx;
                tsy += sy;
                tn += tny;
                tnx += nxi[g];
            }
            // mean xmean as basline mean x for later corrected y means
            double mx0 = grandx / grandn;
            bool cancelled;
            mx0 = host.GetDouble("Enter basline mean for predictors (default is the overall mean of predictor values)", "Covariance Analysis", mx0, out cancelled);
            if (cancelled)
                throw new TemplateOperationCancelledException();
            //  RTF_LoadTemplate("grpcov.rtf")
            ParameterBag outputParameters = new ParameterBag();
            double comssq = grandcpr * grandcpr / grandsqx;
            double btwnssq = grandbit - comssq;
            double residmsq = residssq / (grandn - 2 * k);
            double vr = comssq / (residssq / (grandn - 2 * k));
            double p = PDF.fvalp(vr, 1.0, grandn - 2 * k);
            string Q = p > 0.05 ? "NOT " : string.Empty;
            outputParameters.AddOutput("com_ssq", host.RoundU(comssq));
            outputParameters.AddOutput("com_df", "1");
            outputParameters.AddOutput("com_msq", host.RoundU(comssq));
            outputParameters.AddOutput("com_vr", host.RoundU(vr));
            outputParameters.AddOutput("com_p", host.pval(p));
            vr = (btwnssq / Convert.ToDouble(k - 1)) / (residssq / (grandn - 2 * k));
            p = PDF.fvalp(vr, k - 1, grandn - 2 * k);
            string q2 = p > 0.05 ? "NOT " : string.Empty;
            outputParameters.AddOutput("bet_ssq", host.RoundU(btwnssq));
            outputParameters.AddOutput("bet_df", (k - 1).ToString());
            outputParameters.AddOutput("bet_msq", host.RoundU(btwnssq / Convert.ToDouble(k - 1)));
            outputParameters.AddOutput("bet_vr", host.RoundU(vr));
            outputParameters.AddOutput("bet_p", host.pval(p));
            outputParameters.AddOutput("res_ssq", host.RoundU(residssq));
            outputParameters.AddOutput("res_df", (grandn - 2 * k).ToString());
            outputParameters.AddOutput("res_msq", host.RoundU(residmsq));
            outputParameters.AddOutput("grp_ssq", host.RoundU(grandsqy));
            outputParameters.AddOutput("grp_df", (grandn - k).ToString());
            outputParameters.AddOutput("com", Q);
            outputParameters.AddOutput("diff", q2);
            int degf = Convert.ToInt32(grandn - 2 * k);
            MathDbl.civ(degf, out cit, gamma, out p0);
            IList<ParameterBag> slopeList = new List<ParameterBag>();
            outputParameters.AddOutput("*slope", slopeList);
            for (int g = 1; g <= k - 1; g++)
            {
                for (int j = g + 1; j <= k; j++)
                {
                    ParameterBag slopeParameters = new ParameterBag();
                    slopeList.Add(slopeParameters);
                    slopeParameters.AddOutput("lab1", bnam[g]);
                    slopeParameters.AddOutput("lab2", bnam[j]);
                    slopeParameters.AddOutput("res1", host.RoundU(b[g]));
                    slopeParameters.AddOutput("res2", host.RoundU(b[j]));
                    double dif = Math.Abs(b[g] - b[j]);
                    t = Math.Sqrt(residmsq * (rssx[g] + rssx[j])) * cit;
                    slopeParameters.AddOutput("pc", Formatting.XRound((1 - p0) * 100, 2));
                    slopeParameters.AddOutput("dif", host.RoundU(dif));
                    slopeParameters.AddOutput("from", host.RoundU(dif - t));
                    slopeParameters.AddOutput("to", host.RoundU(dif + t));
                    t = (b[g] - b[j]) / Math.Sqrt(residmsq * (rssx[g] + rssx[j]));
                    slopeParameters.AddOutput("t", host.RoundU(t));
                    p = PDF.tvalp(Math.Abs(t), grandn - 2 * k);
                    if (p > 1.0 - p)
                    {
                        p = 1.0 - p;
                    }
                    slopeParameters.AddOutput("p", host.pval(p * 2.0));
                }
            }
            double syyb = syy - (tsy * tsy / tn);
            double sxxb = sxx - (tsx * tsx / tn);
            double sxyb = sxy - (tsx * tsy / tn);
            double syyt = gtyy - (tsy * tsy / tn);
            double sxyt = gtxy - (tsx * tsy / tn);
            double sxxt = gtxx - (tsx * tsx / tn);
            double sxyw = gtxy - sxy;
            double sxxw = gtxx - sxx;
            double syyw = gtyy - syy;
            double csst = syyt - (sxyt * sxyt / sxxt);
            double cssw = syyw - (sxyw * sxyw / sxxw);
            double cssb = csst - cssw;
            // Uncorrected
            outputParameters.AddOutput("uc_bet_yy", host.RoundU(syyb));
            outputParameters.AddOutput("uc_bet_xy", host.RoundU(sxyb));
            outputParameters.AddOutput("uc_bet_xx", host.RoundU(sxxb));
            outputParameters.AddOutput("uc_bet_df", (k - 1).ToString());
            outputParameters.AddOutput("uc_with_yy", host.RoundU(syyw));
            outputParameters.AddOutput("uc_with_xy", host.RoundU(sxyw));
            outputParameters.AddOutput("uc_with_xx", host.RoundU(sxxw));
            outputParameters.AddOutput("uc_with_df", (tnx - k).ToString());
            outputParameters.AddOutput("uc_tot_yy", host.RoundU(syyt));
            outputParameters.AddOutput("uc_tot_xy", host.RoundU(sxyt));
            outputParameters.AddOutput("uc_tot_xx", host.RoundU(sxxt));
            outputParameters.AddOutput("uc_tot_df", (tnx - 1).ToString());
            // Corrected
            double crWithDf = tnx - k - 1;
            vr = (cssb / Convert.ToDouble(k - 1)) / (cssw / crWithDf);
            p = PDF.fvalp(vr, k - 1, crWithDf);
            outputParameters.AddOutput("cr_bet_ssq", host.RoundU(cssb));
            outputParameters.AddOutput("cr_bet_df", (k - 1).ToString());
            outputParameters.AddOutput("cr_bet_msq", host.RoundU(cssb / Convert.ToDouble(k - 1)));
            outputParameters.AddOutput("cr_bet_vr", host.RoundU(vr));
            outputParameters.AddOutput("cr_with_ssq", host.RoundU(cssw));
            outputParameters.AddOutput("cr_with_df", crWithDf.ToString());
            outputParameters.AddOutput("cr_with_msq", host.RoundU(cssw / crWithDf));
            outputParameters.AddOutput("cr_tot_ssq", host.RoundU(csst));
            outputParameters.AddOutput("cr_tot_df", (tnx - 2).ToString());
            Q = p <= 0.05 ? "NOT " : string.Empty;
            outputParameters.AddOutput("p", host.pval(p));
            double bs = sxyw / sxxw;
            outputParameters.AddOutput("x_mean", host.RoundU(mx0));
            IList<ParameterBag> cmyList = new List<ParameterBag>();
            outputParameters.AddOutput("*cmy", cmyList);
            for (int g = 1; g <= k; g++)
            {
                ParameterBag cmyParameters = new ParameterBag();
                cmyList.Add(cmyParameters);
                double cmy = ymean[g] + (bs * (mx0 - xmean[g]));
                double secmy = Math.Sqrt((cssw / crWithDf) * ((1.0 / Convert.ToDouble(nxi[g]) + ((mx0 - xmean[g]) * (mx0 - xmean[g]) / sxxw))));
                cmyParameters.AddOutput("y", host.RoundU(cmy));
                cmyParameters.AddOutput("res", host.RoundU(secmy));
            }
            // Line separations
            outputParameters.AddOutput("slope", host.RoundU(bs));
            cit = PDF.tfromp(p0 / 2, crWithDf);
            if (q2.Length == 0)
            { // Lines not parallel

                IList<ParameterBag> notParallelList = new List<ParameterBag>();
                notParallelList.Add(new ParameterBag());
                outputParameters.AddOutput("*notParallel", notParallelList);
            }
            else
            {
                outputParameters.AddOutput("*notParallel", null);
            }
            IList<ParameterBag> sepList = new List<ParameterBag>();
            outputParameters.AddOutput("*sep", sepList);
            for (int g = 1; g <= k - 1; g++)
            {
                for (int j = g + 1; j <= k; j++)
                {
                    t = ymean[g] - ymean[j] - (bs * (xmean[g] - xmean[j]));
                    ParameterBag sepParameters = new ParameterBag();
                    sepList.Add(sepParameters);
                    sepParameters.AddOutput("lab1", bnam[g]);
                    sepParameters.AddOutput("lab2", bnam[j]);
                    sepParameters.AddOutput("sep", host.RoundU(t));
                    double zz = cit * Math.Sqrt((cssw / crWithDf) * ((1.0 / Convert.ToDouble(nxi[g])) + (1.0 / Convert.ToDouble(nxi[j])) + ((xmean[g] - xmean[j]) * (xmean[g] - xmean[j]) / sxxw)));
                    sepParameters.AddOutput("pc", Formatting.XRound((1 - p0) * 100, 2));
                    sepParameters.AddOutput("fromSep", host.RoundU(t - zz));
                    sepParameters.AddOutput("toSep", host.RoundU(t + zz));
                    t = t / Math.Sqrt((cssw / crWithDf) * ((1.0 / Convert.ToDouble(nxi[g])) + (1.0 / Convert.ToDouble(nxi[j])) + ((xmean[g] - xmean[j]) * (xmean[g] - xmean[j]) / sxxw)));
                    sepParameters.AddOutput("t", host.RoundU(t));
                    sepParameters.AddOutput("df", host.RoundU(tnx - k - 1));
                    p = PDF.tvalp(Math.Abs(t), crWithDf);
                    if (p > 1.0 - p)
                        p = 1.0 - p;
                    sepParameters.AddOutput("pSep", host.pval(p * 2.0));
                }
            }
            string ylab = yrep ? "Y Replicates" : "Y";
            using (ChartRenderer ch = new ChartRenderer(ChartDefinition.Empty()))
            {
                ch.DataMinX = gcd.minMax.MinX;
                ch.DataMaxX = gcd.minMax.MaxX;
                ch.DataMinY = gcd.minMax.MinY;
                ch.DataMaxY = gcd.minMax.MaxY;
                string rtf = ch.PlotXYRAndReturnRtf(host, xt, y, k, nxi, ny, b, a, xlab, ylab, "Grouped Linear Regression", bnam);
                outputParameters.AddOutput("chart", rtf);
            }

            return outputParameters;
        }


        public static ParameterBag RptConditionalLogisticRegression(ITemplateHost host, ParameterBag parameters)
        {
            const string capti = "Conditional logistic regression";

            double GAMMA = parameters["gamma"].AsDouble;
            if (GAMMA <= 0)
                throw new TemplateOperationCancelledException();
            double cit;
            double P0;
            MathDbl.civ(0, out cit, GAMMA, out P0);

            // bool OK = false; 
            DataFrame stratumFrame = parameters["stratum"].AsDataFrame;
            ClassifierVariable stratumVariable = stratumFrame.Variables[0].AsClassifierVariable;
            int rows = stratumVariable.Length;
            int[] isi = new int[rows + 1];
            int[] ic = new int[rows + 1];
            int strata = stratumVariable.GroupCount;
            string[] stratlab = new string[strata + 1];
            for (int i = 1; i <= rows; i++)
            {
                if (stratumVariable.Data[i - 1] == Constant.MISSING)
                {
                    isi[i] = 0;
                }
                else
                {
                    isi[i] = Convert.ToInt32(stratumVariable.Data[i - 1]) + 1; //  Groups are numbered 0 to n-1 in SD3, were 1 to n in SD2. The +1 causes the array offsets to line up.
                }
            }
            for (int i = 1; i <= strata; i++)
                stratlab[i] = stratumVariable.Groups[i - 1].Label;

            DataFrame caseControlFrame = parameters["case-control"].AsDataFrame;
            DoubleVariable caseControlVariable = caseControlFrame.Variables[0].AsDoubleVariable;
            for (int i = 1; i <= rows; i++)
            {
                //  Pre-validated to 0 or 1
                if (caseControlVariable.Data[i - 1] == Constant.MISSING)
                    isi[i] = 0;
                else
                    ic[i] = Convert.ToInt32(caseControlVariable.Data[i - 1]);
            }

            DataFrame predictorsFrame = parameters["predictors"].AsDataFrame;
            // Store the predictor Data
            int cols = predictorsFrame.VariableCount;
            double[,] x = new double[cols + 1, rows + 1];
            ColumnData[] cd = new ColumnData[cols + 1];
            for (int c = 1; c <= cols; c++)
            {
                DoubleVariable v = predictorsFrame.Variables[c - 1].AsDoubleVariable;
                cd[c] = new ColumnData { Title = v.Title };

                for (int r = 1; r <= rows; r++)
                {
                    x[c, r] = v.Data[r - 1];
                }
            }
            // check predictors for categorical data not yet dummied
            // transpose x into z
            double[,] z = new double[rows + 1, cols + 1];
            for (int C = 1; C <= cols; C++)
            {
                for (int r = 1; r <= rows; r++)
                {
                    z[r, C] = x[C, r];
                    if (z[r, C] == Constant.MISSING)
                    {
                        isi[r] = 0;
                    }
                }
            }

            double[] b = new double[cols + 1];
            double[] cov = new double[((int)(Math.Floor((double)cols * (cols + 1) / 2))) + 1];
            double[] sc = new double[cols + 1 ];
            double[] se = new double[cols + 1];
            int[] isz = new int[cols + 1];
            int[] NCA = new int[strata + 1 ];
            int[] nct = new int[strata + 1 ];

            bool show_counts = parameters["show-counts"].AsBoolean;
            double tol = Parsing.Cdbl_Txt(parameters["accuracy"].AsString);
            if (tol > 0.001)
            {
                tol = 0.001;
            }

            const int maxit = 15;

            // first with a single unity predictor to get LR chi-square baseline
            double[,] z_dum = new double[rows + 1, 1 + 1];
            int[] isz_dum = new int[1 + 1 ];
            isz_dum[1] = 1;
            for (int i = 1; i <= rows; i++)
                z_dum[i, 1] = 1.0;

            int iter;
            int ifault;
            double devx;
            clogit(rows, 1, strata, z_dum, rows, isz_dum, 1, ic, isi, out devx, b, se, sc, cov, NCA, nct, tol, maxit, out iter, out ifault);

            for (int i = 1; i <= cols; i++)
                isz[i] = i;

            double dev;
            clogit(rows, cols, strata, z, rows, isz, cols, ic, isi, out dev, b, se, sc, cov, NCA, nct, tol, maxit, out iter, out ifault);

            double lrx2 = Math.Abs(devx - dev);

            string warn = string.Empty;
            switch (ifault)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                    host.Error("Fault in calculation.", capti);
                    throw new TemplateOperationCancelledException();
                case 5:
                    warn = Formatting.ERRCOLON + "Matrix singularity.";
                    if (cols > 2)
                        warn = warn + " Try using fewer predictors.";
                    break;
                case 6:
                    warn = Formatting.WRNCOLON + "Regression failed to converge.  Try reducing accuracy.";
                    break;
            }

            // RTF_LoadTemplate("clogit.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            if (show_counts)
            {
                IList<ParameterBag> countsList = new List<ParameterBag>();
                outputParameters.AddOutput("*counts", countsList);
                ParameterBag countsParameters = new ParameterBag();
                countsList.Add(countsParameters);
                IList<ParameterBag> countList = new List<ParameterBag>();
                countsParameters.AddOutput("*count", countList);
                for (int i = 1; i <= strata; i++)
                {
                    ParameterBag countParameters = new ParameterBag();
                    countList.Add(countParameters);
                    countParameters.AddOutput("st", stratlab[i]);
                    countParameters.AddOutput("ca", NCA[i].ToString());
                    countParameters.AddOutput("ct", nct[i].ToString());
                }
            }
            else
            {
                outputParameters.AddOutput("*counts", null);
            }

            outputParameters.AddOutput("dv", host.RoundU(dev));
            outputParameters.AddOutput("warn", warn);
            outputParameters.AddOutput("x2", host.RoundU(lrx2));
            outputParameters.AddOutput("p_dev", host.pval(PDF.chivalp(lrx2, Convert.ToDouble(cols))));
            outputParameters.AddOutput("r2", host.RoundU(lrx2 / devx));

            IList<ParameterBag> estList = new List<ParameterBag>();
            outputParameters.AddOutput("*est", estList);
            for (int i = 1; i <= cols; i++)
            {
                ParameterBag estParameters = new ParameterBag();
                estList.Add(estParameters);
                estParameters.AddOutput("lab", cd[i].Title);
                estParameters.AddOutput("b", host.RoundU(-b[i]));
                estParameters.AddOutput("se", host.RoundU(se[i]));
                double zz;
                if (se[i] == 0.0)
                {
                    zz = Constant.MISSING;
                    estParameters.AddOutput("z", host.RoundU(zz));
                    estParameters.AddOutput("p", "* error: drop this variable *");
                }
                else
                {
                    zz = -b[i] / se[i];
                    double P = 1.0 - PDF.alnorm(zz);
                    if (P > 1.0 - P)
                    {
                        P = 1.0 - P;
                    }
                    P = 2.0 * P;
                    estParameters.AddOutput("z", host.RoundU(zz));
                    estParameters.AddOutput("p", host.pval(P));
                }
            }

            outputParameters.AddOutput("pc", Formatting.XRound(GAMMA * 100, 2));
            IList<ParameterBag> orList = new List<ParameterBag>();
            outputParameters.AddOutput("*or", orList);
            for (int i = 1; i <= cols; i++)
            {
                ParameterBag orParameters = new ParameterBag();
                orList.Add(orParameters);
                orParameters.AddOutput("lab", cd[i].Title);
                orParameters.AddOutput("or", host.RoundU(Formatting.SafeExp(-b[i])));
                double lci = Formatting.SafeExp(-b[i] - se[i] * cit);
                double uci = Formatting.SafeExp(-b[i] + se[i] * cit);
                if (lci > uci)
                {
                    double temp = lci;
                    lci = uci;
                    uci = temp;
                }
                orParameters.AddOutput("from", host.RoundU(lci));
                orParameters.AddOutput("to", host.RoundU(uci));
            }
            return outputParameters;
        }


        ///  <summary>
        ///  conditional logistic - from as 196
        ///  </summary>
        ///  <param name="n"></param>
        ///  <param name="m"></param>
        ///  <param name="ns"></param>
        ///  <param name="z"></param>
        ///  <param name="ldz"></param>
        ///  <param name="isz"></param>
        ///  <param name="ip"></param>
        ///  <param name="ic"></param>
        ///  <param name="isi"></param>
        ///  <param name="dev"></param>
        ///  <param name="b"></param>
        ///  <param name="se"></param>
        ///  <param name="sc"></param>
        ///  <param name="cov"></param>
        ///  <param name="nca"></param>
        ///  <param name="nct"></param>
        ///  <param name="tol"></param>
        ///  <param name="maxit"></param>
        ///  <param name="iter"></param>
        ///  <param name="ifault"></param>
        ///  <remarks></remarks>
        private static void clogit(int n, int m, int ns, double[,] z, int ldz, int[] isz, int ip, int[] ic, int[] isi, out double dev, double[] b, double[] se, double[] sc, double[] cov, int[] nca, int[] nct, double tol, int maxit, out int iter, out int ifault)
        {
            int k; // Used in many ways through this function; this should be optimised, but not trivial to do so
            // int nrec = 1; 
            // tola = 10# * DPMACH(3)
            // double tola = 10.0 * 0.000000000000000111022302462516; 

            ifault = 1;
            iter = 0;
            dev = Constant.MISSING;

            if (m < 1 || n < 2 || ns < 1 || ip < 1 || ldz < n)
            {
                return;
            }
            int j = 0;
            for (int i = 1; i <= m; i++)
            {
                if ((isz[i] < 0))
                    return;

                if (isz[i] > 0)
                    j++;
            }
            if (j != ip)
                return;

            ifault = 2;
            int nobs = 0;
            for (int i = 1; i <= ns; i++)
            {
                nca[i] = 0;
                nct[i] = 0;
            }
            for (int i = 1; i <= n; i++)
            {
                j = isi[i];
                if (j < 0 || j > ns)
                    return;

                if (j > 0)
                {
                    nobs++;
                    j = isi[i];
                    if (ic[i] == 0)
                        nca[j]++;
                    else if (ic[i] == 1)
                        nct[j]++;
                    else
                        return;
                }
            }

            if (nobs <= ip)
                return;

            ifault = 0;

            nobs = nca[1] + nct[1];
            int maxobs = nobs;
            nct[1] = nca[1];
            nca[1] = 0;
            for (int i = 2; i <= ns; i++)
            {
                k = nca[i];
                int l = nct[i];
                if (k + l > maxobs)
                    maxobs = k + l;

                nca[i] = nobs;
                nobs = nobs + k;
                nct[i] = nobs;
                nobs = nobs + l;
            }
            maxobs = maxobs + 1;
            k = ip * nobs + maxobs * (ip + 2) * (ip + 1) / 2 + maxobs - 1;
            double[] wk = new double[k + 1 ];

            // int l1 = ip * nobs + 1; 
            // int l2 = maxobs + l1; 
            // int l3 = maxobs * ip + l2; 
            // int l4 = maxobs * ip * ( ip + 1 ) / 2 + l3; 

            //  sort by strata then case-control
            for (int i = 1; i <= n; i++)
            {
                int l = isi[i];
                if (l > 0)
                {
                    if ((ic[i] == 0))
                    {
                        k = nca[l];
                        nca[l] = k + 1;
                    }
                    else if (ic[i] == 1)
                    {
                        k = nct[l];
                        nct[l] = k + 1;
                    }
                    k = ip * k;
                    for (j = 1; j <= m; j++)
                    {
                        if (isz[j] > 0)
                        {
                            k++;
                            wk[k] = z[i, j];
                        }
                    }
                }
            }

            //  center covariates
            int ncase = nca[1];
            int ncc = nct[1];
            k = 0;
            for (int l = 1; l <= ns; l++)
            {
                if (ncase > 0)
                {
                    for (j = 1; j <= ip; j++)
                    {
                        double sum = 0.0;
                        int jk = k + j;
                        for (int i = 1; i <= ncase; i++)
                        {
                            sum += wk[jk];
                            jk += ip;
                        }
                        sum = sum / Convert.ToDouble(ncase);
                        jk = k + j;
                        for (int i = 1; i <= ncc; i++)
                        {
                            wk[jk] -= sum;
                            jk += ip;
                        }
                    }
                }
                if (l < ns)
                {
                    k = nct[l] * ip;
                    ncase = nca[l + 1] - nct[l];
                    ncc = nct[l + 1] - nct[l];
                }
            }

            for (int i = ns; i >= 2; i--)
            {
                nct[i] = nct[i] - nca[i];
                nca[i] = nca[i] - nct[i - 1];
            }

            nct[1] = nct[1] - nca[1];

            double[,] wz = new double[ip + 1, nobs + 1];
            for (j = 1; j <= nobs; j++)
                for (int i = 1; i <= ip; i++)
                    wz[i, j] = wk[i + (j - 1) * ip];

            clmain2(nobs, maxobs, ns, wz, nca, nct, ip, out dev, b, sc, cov, maxit, tol, out iter, ref ifault);

            dev = -2.0 * dev; 
            k = 0;
            for (int i = 1; i <= ip; i++)
            {
                k += i;
                if (cov[k] > 0.0)
                    se[i] = Math.Sqrt(cov[k]);
                else
                    se[i] = 0.0;
            }

        }


        private static void clmain2(int nobs, int maxobs, int ns, double[,] z, int[] nca, int[] nct, int ip, out double dlik, double[] b, double[] sc, double[] cov, int maxit, double tol, out int iter, ref int ifault)
        {

            //      based on applied statistics algorithm as 196 (logcch)

            //      dimension b(ip), cov(ip*(ip+1)/2), sc(ip), u(*), wb(*), wd2b(ip*(ip+1)/2,*), wdb(ip,*), z(ldz,*), nca(ns), nct(ns)

            int info;
            double dlikx = 0;
            double[] wb = new double[maxobs + 1];
            double[,] wdb = new double[ip + 1, maxobs + 1];
            double[,] wd2b = new double[ip * (ip + 1) / 2 + 1, maxobs + 1];
            double[] u = new double[maxobs + 1 ];

            double toobig = -Math.Log(Constant.SPREAL);
            iter = 0;

            do
            {

                iter = iter + 1;
                dlik = 0.0;
                int k = 0;
                for (int j = 1; j <= ip; j++)
                {
                    sc[j] = 0.0;
                    for (int jj = 1; jj <= j; jj++)
                    {
                        k = k + 1;
                        cov[k] = 0.0;
                    }
                }

                int nid = 0;
                for (int i = 1; i <= ns; i++)
                {
                    int m = nca[i];
                    int n = m + nct[i];
                    if (nca[i] > 0 & nct[i] > 0)
                    {
                        double sum = 0.0;
                        for (int j = 1; j <= n; j++)
                        {
                            int j1 = j + nid;
                            double t = 0.0;
                            for (k = 1; k <= ip; k++)
                            {
                                t = t + b[k] * z[k, j1];
                            }
                            u[j] = t;
                            sum = sum + t;
                        }
                        double c1 = PDF.alogam(Convert.ToDouble(n)) - PDF.alogam(Convert.ToDouble(m)) - PDF.alogam(Convert.ToDouble(n - m));
                        c1 = c1 / Convert.ToDouble(m) + sum / Convert.ToDouble(n);
                        for (int j = 1; j <= n; j++)
                        {
                            double eu = u[j] - c1;
                            if (eu < toobig)
                            {
                                u[j] = Math.Exp(eu);
                            }
                            else
                            {
                                ifault = 4;
                                return;
                            }
                        }
                        Howard2(m, n, u, z, nid + 1, ip, wb, wdb, wd2b);
                        double bmn = wb[m + 1];
                        dlik = dlik - Math.Log(bmn) - c1 * Convert.ToDouble(m);
                        int l = 0;
                        int ir = 1;
                        int iis = 0;
                        //  cumulative score to stratum
                        for (k = 1; k <= ip; k++)
                        {
                            sc[k] = sc[k] - wdb[k, m + 1] / bmn;
                            for (int kk = 1; kk <= k; kk++)
                            {
                                l = l + 1;
                                iis = iis + 1;
                                if (iis > ir)
                                {
                                    ir = ir + 1;
                                    iis = 1;
                                }
                                cov[l] = cov[l] + wd2b[l, m + 1] / bmn - wdb[ir, m + 1] * wdb[iis, m + 1] / (bmn * bmn);
                            }
                        }
                    }
                    nid += n;
                }

                //  factorize u (info mat)

                dpptrf(ip, cov, out info);
                if (info > 0)
                {
                    ifault = 5;
                    return;
                }

                //  new parameter estimates

                if (maxit > 0)
                {
                    for (int i = 1; i <= ip; i++)
                    {
                        wdb[i, 1] = sc[i];
                    }
                    dpptrs(ip, cov, wdb, out info);
                    for (int i = 1; i <= ip; i++)
                    {
                        b[i] = b[i] + wdb[i, 1];
                    }
                }

                if (iter != 1)
                {
                    if (Math.Abs(dlikx - dlik) > (1.0 + Math.Abs(dlikx)) * tol)
                    {
                        if (iter >= maxit)
                        {
                            ifault = 6;
                            break;
                        }
                        dlikx = dlik;
                    }
                    else
                        break;
                }
                else if (maxit > 1)
                {
                    dlikx = dlik;
                }
                else if (maxit == 1)
                {
                    ifault = 6;
                    break;
                }
                else
                    break;
            }
            while (true);

            //  invert u (info mat)
            dpptri(ip, cov, out info);
            if (info > 0)
                ifault = 5;
        }

        private static void Howard2(int m, int n, double[] u, double[,] z, int idz, int ip, double[] wb, double[,] wdb, double[,] wd2b)
        {

            //      from as 196

            //       dimension u(n), wb(n+1), wd2b(ip*(ip+1)/2,n+1),wdb(ip,n+1), z(ldz,*)

            for (int j = 1; j <= m + 1; j++)
            {
                wb[j] = 0.0;
                for (int i = 1; i <= ip; i++)
                    wdb[i, j] = 0.0;
                for (int i = 1; i <= ip * (ip + 1) / 2; i++)
                    wd2b[i, j] = 0.0;
            }

            wb[1] = 1.0;
            for (int im = 1; im <= n - m + 1; im++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int j1 = j + 1;
                    int i = j + im - 1;
                    int iz = idz + i - 1;
                    int ir = 1;
                    int iis = 0;
                    int l;
                    for (l = 1; l <= ip * (ip + 1) / 2; l++)
                    {
                        iis = iis + 1;
                        if (iis > ir)
                        {
                            ir = ir + 1;
                            iis = 1;
                        }
                        wd2b[l, j1] = wd2b[l, j1] + u[i] * wd2b[l, j] + z[ir, iz] * z[iis, iz] * u[i] * wb[j] + z[ir, iz] * u[i] * wdb[iis, j] + z[iis, iz] * u[i] * wdb[ir, j];
                    }
                    for (l = 1; l <= ip; l++)
                    {
                        wdb[l, j1] = wdb[l, j1] + u[i] * wdb[l, j] + z[l, iz] * u[i] * wb[j];
                    }
                    wb[j1] = wb[j1] + u[i] * wb[j];
                }
            }
        }


        /// <summary>
        ///  Computes the inverse of a real symmetric positive definite
        ///  matrix a using the cholesky factorization a = u**t*u or a = l*l**t
        ///  computed by dpptrf.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="ap"></param>
        /// <param name="info"></param>
        private static void dpptri(int n, double[] ap, out int info)
        {
            info = 0;
            if (n < 0)
            {
                info = -2;
            }
            if (info != 0)
            {
                info = -info;
                return;
            }
            if (n == 0)
            {
                return;
            }
            dtptri(false, n, ap, out info);
            if (info > 0)
            {
                return;
            }
            int jj = 0;
            for (int j = 1; j <= n; j++)
            {
                int jc = jj + 1;
                jj = jj + j;
                if (j > 1)
                {
                    dspr(j - 1, 1.0, ap, jc, 1, ap, 1);
                }
                double ajj = ap[jj];
                int ict = jc;
                int k;
                for (k = 1; k <= j; k++)
                {
                    ap[ict] = ap[ict] * ajj;
                    ict = ict + 1;
                }
            }

        }

        /// <summary>
        /// Solves a system of linear equations a*x = b with an upper symmetric
        /// positive definite matrix a in packed storage using the cholesky
        /// factorization a = u**t*u or a = l*l**t computed by dpptrf.
        /// </summary>
        private static void dpptrs(int n, double[] ap, double[,] b, out int info)
        {
            double[] tb = new double[n + 1 ];

            info = 0;
            if (n < 0)
            {
                info = -2;
            }
            if (info != 0)
            {
                info = -info;
                return;
            }
            if (n == 0)
            {
                return;
            }

            for (int i = 1; i <= n; i++)
            {
                tb[i] = b[i, 1];
            }

            dtpsv(false, false, n, ap, 1, tb, 1, 1, out info);
            dtpsv(true, false, n, ap, 1, tb, 1, 1, out info);

            for (int i = 1; i <= n; i++)
            {
                b[i, 1] = tb[i];
            }
        }

        /// <summary>
        /// Computes the cholesky factorization of an upper real symmetric positive definite matrix a stored in packed format.
        /// </summary>
        private static void dpptrf(int n, double[] ap, out int info)
        {
            info = 0;
            if (n < 0)
            {
                info = -2;
            }
            if (info != 0)
            {
                info = -info;
                return;
            }
            if (n == 0)
            {
                return;
            }
            int jj = 0;
            for (int j = 1; j <= n; j++)
            {
                int jc = jj + 1;
                jj = jj + j;
                if (j > 1)
                {
                    dtpsv(false, false, j - 1, ap, 1, ap, jc, 1, out info);
                }
                double ddot = 0.0;
                for (int i = jc; i <= jc + j - 2; i++)
                {
                    ddot = ddot + ap[i] * ap[i];
                }
                double ajj = ap[jj] - ddot;
                if (ajj <= 0.0)
                {
                    ap[jj] = ajj;
                    info = j;
                    return;
                }
                ap[jj] = Math.Sqrt(ajj);
            }
        }

        /// <summary>
        /// Performs the symmetric rank 1 operation
        /// 
        ///      a := alpha*x*x' + a,
        /// 
        ///   where alpha is a real scalar, x is an n element vector and a is an
        ///   n by n upper symmetric matrix, supplied in packed form.
        /// </summary>
        private static void dspr(int n, double alpha, double[] x, int idx, int incx, double[] ap, int idap)
        {
            int info = 0;
            if (n < 0)
            {
                info = 2;
            }
            else if (incx == 0)
            {
                info = 5;
            }
            if (info != 0)
            {
                return;
            }
            if (n == 0 | alpha == 0.0)
            {
                return;
            }
            int kx = incx <= 0 ? idx - (n - 1) * incx : idx;
            int kk = idap;
            int jx = kx;
            for (int j = 1; j <= n; j++)
            {
                if (x[jx] != 0.0)
                {
                    double temp = alpha * x[jx];
                    int ix = kx;
                    for (int k = kk; k <= kk + j - 1; k++)
                    {
                        ap[k] = ap[k] + x[ix] * temp;
                        ix = ix + incx;
                    }
                }
                jx = jx + incx;
                kk = kk + j;
            }

        }

        /// <summary>
        /// Solves one of the systems of equations
        /// 
        ///      a*x = b,   or   a'*x = b,
        /// 
        ///   where b and x are n element vectors and a is an n by n unit, or
        ///   non-unit, upper triangular matrix, supplied in packed form.
        /// 
        ///   no test for singularity or near-singularity is included in this
        ///   routine. such tests must be performed before calling this routine.
        /// </summary>
        private static void dtpsv(bool ntrans, bool udiag, int n, double[] ap, int idap, double[] x, int idx, int incx, out int info)
        {
            int ix, k, j, kx, kk, jx;
            double temp;

            info = 0;
            if (n < 0)
            {
                info = 4;
            }
            else if (incx == 0)
            {
                info = 7;
            }
            if (info != 0)
            {
                return;
            }
            if (n == 0)
            {
                return;
            }

            bool nounit = (udiag == false);

            if (incx <= 0)
            {
                kx = idx - (n - 1) * incx;
            }
            else
            {
                kx = idx;
            }
            if (ntrans)
            {
                kk = idap + (n * (n + 1)) / 2 - 1;
                jx = kx + (n - 1) * incx;
                for (j = n; j >= 1; j--)
                {
                    if (x[jx] != 0.0)
                    {
                        if (nounit)
                        {
                            x[jx] = x[jx] / ap[kk];
                        }
                        temp = x[jx];
                        ix = jx;
                        for (k = kk - 1; k >= kk - j + 1; k--)
                        {
                            ix = ix - incx;
                            x[ix] = x[ix] - temp * ap[k];
                        }
                    }
                    jx = jx - incx;
                    kk = kk - j;
                }
            }
            else
            {
                kk = 1;
                jx = kx;
                for (j = 1; j <= n; j++)
                {
                    temp = x[jx];
                    ix = kx;
                    for (k = kk; k <= kk + j - 2; k++)
                    {
                        temp = temp - ap[k] * x[ix];
                        ix = ix + incx;
                    }
                    if (nounit)
                    {
                        temp = temp / ap[kk + j - 1];
                    }
                    x[jx] = temp;
                    jx = jx + incx;
                    kk = kk + j;
                }
            }

        }

        /// <summary>
        /// Performs one of the matrix-vector operations
        /// 
        ///      x := a*x,   or   x := a'*x,
        /// 
        ///   where x is an n element vector and  a is an n by n unit, or non-unit,
        ///   upper triangular matrix, supplied in packed form.
        /// </summary>
        private static void dtpmv(bool ntrans, bool udiag, int n, double[] ap, int idap, double[] x, int idx, int incx, out int info)
        {
            int ix, jx, j, kx, kk, k;
            double temp;

            info = 0;
            if (n < 0)
            {
                info = 4;
            }
            else if (incx == 0)
            {
                info = 7;
            }
            if (info != 0)
            {
                return;
            }
            if (n == 0)
            {
                return;
            }
            bool nounit = (udiag == false);
            if (incx <= 0)
            {
                kx = idx - (n - 1) * incx;
            }
            else
            {
                kx = idx;
            }

            if (ntrans)
            {
                kk = idap;
                jx = kx;
                for (j = 1; j <= n; j++)
                {
                    if (x[jx] != 0.0)
                    {
                        temp = x[jx];
                        ix = kx;
                        for (k = kk; k <= kk + j - 2; k++)
                        {
                            x[ix] = x[ix] + temp * ap[k];
                            ix = ix + incx;
                        }
                        if (nounit)
                        {
                            x[jx] = x[jx] * ap[kk + j - 1];
                        }
                    }
                    jx = jx + incx;
                    kk = kk + j;
                }
            }
            else
            {
                kk = idap + (n * (n + 1)) / 2 - 1;
                jx = kx + (n - 1) * incx;
                for (j = n; j >= 1; j--)
                {
                    temp = x[jx];
                    ix = jx;
                    if (nounit)
                    {
                        temp = temp * ap[kk];
                    }
                    for (k = kk - 1; k >= kk - j + 1; k--)
                    {
                        ix = ix - incx;
                        temp = temp + ap[k] * x[ix];
                    }
                    x[jx] = temp;
                    jx = jx - incx;
                    kk = kk - j;
                }
            }

        }

        /// <summary>
        /// Computes the inverse of a real upper triangular matrix a stored in packed format.
        /// </summary>
        private static void dtptri(bool udiag, int n, double[] ap, out int info)
        {
            info = 0;
            bool nounit = (udiag == false);
            if (n < 0)
            {
                info = -3;
            }
            if (info != 0)
            {
                info = -info;
                return;
            }
            if (nounit)
            {
                int jj = 0;
                for (info = 1; info <= n; info++)
                {
                    jj = jj + info;
                    if (ap[jj] == 0.0)
                    {
                        return;
                    }
                }
                info = 0;
            }
            int jc = 1;
            for (int j = 1; j <= n; j++)
            {
                double ajj;
                if (nounit)
                {
                    ap[jc + j - 1] = 1.0 / ap[jc + j - 1];
                    ajj = -ap[jc + j - 1];
                }
                else
                {
                    ajj = -1.0;
                }
                dtpmv(true, udiag, j - 1, ap, 1, ap, jc, 1, out info);
                int ict = jc;
                int k;
                for (k = 1; k <= j - 1; k++)
                {
                    ap[ict] = ap[ict] * ajj;
                    ict = ict + 1;
                }
                jc = jc + j;
            }
        }

    }
}

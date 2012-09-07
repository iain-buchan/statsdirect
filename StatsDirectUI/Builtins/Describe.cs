using System.Text;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using StatsDirect.Data;

using System;
using System.Collections.Generic;
namespace StatsDirect.Builtins
{
    public class SummaryStatisticsOptions : IFillable
    {
        public string Text;

        public string FillerToUse
        {
            get
            {
                return "SummaryStatistics";
            }
        } // interface properties implemented by FillerToUse
        string IFillable.FillerToUse
        {
            get
            {
                return FillerToUse;
            }
        }

    }


    public class Describe
    {
        // private static bool prevchk1;
        // private static bool prevchk2; 
        private static readonly bool[] checked1 = new bool[22];
        private static readonly bool[] checked2 = new bool[22];
        private static int PrevCentileType;
        private static int UserCentileA1;
        private static int UserCentileB1;
        private static int UserCentileA2;
        private static int UserCentileB2;

        private class GroupByTitle : IComparer<Group>
        {
            private int Compare(Group x, Group y)
            {
                if (x.Label.Equals(y.Label))
                {
                    return 0;
                }

                //  If both titles are numeric, compare numerically; else, compare as text
                bool lower;
                double numericX;
                double numericY;
                if (double.TryParse(x.Label, out numericX) && double.TryParse(y.Label, out numericY))
                {
                    lower = numericX <= numericY;
                }
                else
                {
                    lower = String.CompareOrdinal(x.Label, y.Label) < 0;
                }

                return lower ? -1 : 1;
            }
            // interface methods implemented by Compare
            int IComparer<Group>.Compare(Group x, Group y)
            {
                return Compare(x, y);
            }

        }


        public static StepResult RptPreferences(ITemplateHost host, ParameterBag parameters)
        {
            int i; int held;
            int k; int extra; int nrp; int tp; int tn; int OK = 0; int j;
            double cap = 0;

            const string pg = "Preference Groups";

            int Seed = parameters["seed"].AsInt32;
            DataFrame capacitiesFrame = parameters["capacities"].AsDataFrame;
            DoubleVariable capacitiesVariable = capacitiesFrame.Variables[0].AsDoubleVariable;
            int ng = capacitiesVariable.Length;
            double[] gc = new double[ng + 1 /* VB to C# conversion */ ];
            for (i = 1; i <= ng; i++)
            {
                gc[i] = capacitiesVariable.Data[i - 1];
                cap += gc[i];
            }
            DataFrame preferencesFrame = parameters["preferences"].AsDataFrame;
            int P = preferencesFrame.VariableCount;
            int s = preferencesFrame.Variables[0].Length;
            double[,] x = new double[P + 1 /* VB to C# conversion */, s + 1 /* VB to C# conversion */];
            for (i = 1; i <= P; i++)
            {
                DoubleVariable preferencesVariable = preferencesFrame.Variables[i - 1].AsDoubleVariable;
                for (j = 1; j <= s; j++)
                {
                    x[i, j] = preferencesVariable.Data[j - 1];
                    if (x[i, j] < 1 | x[i, j] > ng)
                    {
                        host.Error("invalid preference in group " + i.ToString() + "at row " + j.ToString(), pg);
                        throw new TemplateOperationCancelledException();
                    }
                }
            }
            if (ng < P)
            {
                host.Error("fewer groups than preferences", pg);
                throw new TemplateOperationCancelledException();
            }
            if (cap < Convert.ToDouble(s))
            {
                host.Error("more subects (" + s.ToString() + ") than total capacity of groups (" + cap.ToString() + ")", pg);
                throw new TemplateOperationCancelledException();
            }
            int[] done = new int[s + 1 /* VB to C# conversion */ ];
            int[] gp = new int[s + 1 /* VB to C# conversion */ ];
            int[] full = new int[ng + 1 /* VB to C# conversion */ ];
            int[] tmp = new int[s + 1 + 1 /* VB to C# conversion */ ];
            MersenneTwister mt = new MersenneTwister(Seed);
            for (i = 1; i <= P; i++)
            {
                for (j = 1; j <= ng; j++)
                {
                    held = 0;
                    for (k = 1; k <= s; k++)
                    {
                        if (x[i, k] == j & done[k] == 0 & full[j] < gc[j])
                        {
                            held++;
                            tmp[held] = k;
                        }
                    }
                    if (held > 0)
                    {
                        if (held > 1)
                        {
                            for (tn = 1; tn <= 3; tn++)
                            {
                                for (k = 1; k <= held; k++)
                                {
                                    nrp = Convert.ToInt32((held - 2) * mt.NextDouble()) + 1;
                                    tp = tmp[k];
                                    tmp[k] = tmp[nrp];
                                    tmp[nrp] = tp;
                                }
                            }
                        }
                        extra = held >= gc[j] ? (int)(Math.Floor(held - gc[j])) : 0;
                        for (k = 1; k <= held - extra; k++)
                        {
                            gp[tmp[k]] = j;
                            done[tmp[k]] = -1;
                            full[j] = full[j] + 1;
                            OK = OK + 1;
                        }
                    }
                }
            }
            if (OK < s)
            {
                held = 0;
                for (j = 1; j <= ng; j++)
                {
                    if (full[j] < gc[j])
                    {
                        for (k = 1; k <= ((int)(Math.Floor(gc[j] - full[j]))); k++)
                        {
                            held++;
                            tmp[held] = j;
                        }
                    }
                }
                for (tn = 1; tn <= 3; tn++)
                {
                    for (k = 1; k <= held; k++)
                    {
                        nrp = Convert.ToInt32((held - 2) * mt.NextDouble()) + 1;
                        tp = tmp[k];
                        tmp[k] = tmp[nrp];
                        tmp[nrp] = tp;
                    }
                }
                extra = 0;
                for (k = 1; k <= s; k++)
                {
                    if (done[k] == 0)
                    {
                        extra = extra + 1;
                        gp[k] = tmp[extra];
                    }
                }
            }
            //  RTF_LoadTemplate("prefer.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("groups", ng.ToString());
            outputParameters.AddOutput("capacity", cap.ToString());
            outputParameters.AddOutput("subjects", s.ToString());
            outputParameters.AddOutput("seed", Seed.ToString());
            IList<ParameterBag> groupsList = new List<ParameterBag>();
            outputParameters.AddOutput("*groups", groupsList);
            for (i = 1; i <= s; i++)
            {
                ParameterBag groupsParameters = new ParameterBag();
                groupsList.Add(groupsParameters);
                groupsParameters.AddOutput("sub", i.ToString());
                groupsParameters.AddOutput("grp", gp[i].ToString());
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptFrequency(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            for (int v = 0; v <= data.VariableCount - 1; v++)
            {
                if (!(data.Variables[v].IsClassifier))
                {
                    data.Variables[v] = TemplateProcessor.gidx_bins(data.Variables[v].AsDoubleVariable);
                }
            }

            //  RTF_LoadTemplate("freq.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> variableList = new List<ParameterBag>();
            outputParameters.AddOutput("*variable", variableList);
            foreach (Variable v in data.Variables)
            {
                ClassifierVariable vc = v.AsClassifierVariable;
                ParameterBag variableParameters = new ParameterBag();
                variableList.Add(variableParameters);
                variableParameters.AddOutput("ti", vc.Title);
                variableParameters.AddOutput("n", vc.Length.ToString());
                int cm = 0;
                int xtot = vc.Length;
                int bins = vc.GroupCount;
                Group[] bin = new Group[bins + 1 /* for VB to C# conversion */ ];
                int i;
                for (i = 1; i <= bins; i++)
                {
                    bin[i] = vc.get_Group(i - 1);
                    if (bin[i].Label == Formatting.MISSINGLABEL)
                    {
                        xtot -= bin[i].NBin;
                    }
                }

                Array.Sort(bin, 1, bins, new GroupByTitle());

                List<ParameterBag> binList = new List<ParameterBag>();
                variableParameters.AddOutput("*bin", binList);
                for (i = 1; i <= bins; i++)
                {
                    int xn = bin[i].NBin;
                    ParameterBag binParameters = new ParameterBag();
                    binList.Add(binParameters);
                    binParameters.AddOutput("x", bin[i].Label);
                    binParameters.AddOutput("fx", xn.ToString());
                    if (bin[i].Label != Formatting.MISSINGLABEL)
                    {
                        binParameters.AddOutput("%", host.RoundU(100.0 * Convert.ToDouble(xn) / Convert.ToDouble(xtot)));
                        cm = cm + xn;
                        binParameters.AddOutput("cm", cm.ToString());
                        binParameters.AddOutput("%2", host.RoundU(100.0 * Convert.ToDouble(cm) / Convert.ToDouble(xtot)));
                    }
                    else
                    {
                        binParameters.AddOutput("%", "na");
                        binParameters.AddOutput("cm", "na");
                        binParameters.AddOutput("%2", "na");
                    }
                }
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult QuickSummary(ITemplateHost host, ParameterBag parameters)
        {
            double GAMMA = parameters["gamma"].AsDouble;
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable v0 = data.Variables[0].AsDoubleVariable;
            double[] x = new double[v0.Length + 1 /* VB to C# conversion */ ];
            Array.Copy(v0.Data, 0, x, 1, v0.Length);
            Summary sx = new Summary();
            sx.FullSummaryFromX(x, v0.Length, v0.Title, GAMMA, 5, 95, 1);
            const int flt = 6;
            const int k = 24;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Title: " + v0.Title);
            sb.AppendLine("");
            sb.AppendLine(Formatting.PadTo("Valid data", k) + sx.ValidData.ToString());
            sb.AppendLine(Formatting.PadTo("Missing", k) + sx.MissingData.ToString());
            sb.AppendLine(Formatting.PadTo("Sum", k) + Formatting.RoundOut(sx.Sum, flt));
            sb.AppendLine(Formatting.PadTo("Mean", k) + Formatting.RoundOut(sx.Mean, flt));
            sb.AppendLine(Formatting.PadTo("Variance", k) + Formatting.RoundOut(sx.Variance, flt));
            sb.AppendLine(Formatting.PadTo("Standard deviation", k) + Formatting.RoundOut(sx.Sd, flt));
            sb.AppendLine(Formatting.PadTo("Variation coefficient", k) + Formatting.RoundOut(sx.VarianceCoefficient, flt));
            sb.AppendLine(Formatting.PadTo("Standard error of mean", k) + Formatting.RoundOut(sx.Sem, flt));
            sb.AppendLine(Formatting.PadTo(Formatting.XRound(100 * GAMMA, 1) + "% Upper CL of mean", k) + Formatting.RoundOut(sx.MeanUCL, flt));
            sb.AppendLine(Formatting.PadTo(Formatting.XRound(100 * GAMMA, 1) + "% Lower CL of mean", k) + Formatting.RoundOut(sx.MeanLCL, flt));
            sb.AppendLine(Formatting.PadTo("Geometric mean", k) + Formatting.RoundOut(sx.GeometricMean, flt));
            sb.AppendLine(Formatting.PadTo("Skewness", k) + Formatting.RoundOut(sx.Skewness, flt));
            sb.AppendLine(Formatting.PadTo("Kurtosis", k) + Formatting.RoundOut(sx.Kurtosis, flt));
            sb.AppendLine(Formatting.PadTo("Maximum", k) + Formatting.RoundOut(sx.Maximum, flt));
            sb.AppendLine(Formatting.PadTo("95th percentile", k) + Formatting.RoundOut(sx.UserCentileU, flt));
            sb.AppendLine(Formatting.PadTo("Upper quartile", k) + Formatting.RoundOut(sx.UpperQuartile, flt));
            sb.AppendLine(Formatting.PadTo("Median", k) + Formatting.RoundOut(sx.Median, flt));
            sb.AppendLine(Formatting.PadTo("Lower quartile", k) + Formatting.RoundOut(sx.LowerQuartile, flt));
            sb.AppendLine(Formatting.PadTo("5th percentile", k) + Formatting.RoundOut(sx.UserCentileL, flt));
            sb.AppendLine(Formatting.PadTo("Minimum", k) + Formatting.RoundOut(sx.Minimum, flt));
            sb.AppendLine(Formatting.PadTo("Range", k) + Formatting.RoundOut(sx.Range, flt));
            SummaryStatisticsOptions sso = new SummaryStatisticsOptions { Text = sb.ToString() };
            host.Amend(sso, parameters);
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }


        public static StepResult RptUnivariateSummary(ITemplateHost host, ParameterBag parameters)
        {
            return RptDescriptive(host, parameters, false);
        }


        public static StepResult RptWeightedUnivariateSummary(ITemplateHost host, ParameterBag parameters)
        {
            return RptDescriptive(host, parameters, true);
        }

        private static StepResult RptDescriptive(ITemplateHost host, ParameterBag parameters, bool isWeighted)
        {
            int i; int j;
            int maxrows; int cols;
            double nsumwt = 0;
            string wti = null;
            ColumnData[] cdx;
            double[,] x; double[,] w = null;

            // get data
            if (isWeighted)
            {
                nsumwt = Constant.MISSING;
                // store the data
                DataFrame data = parameters["data"].AsDataFrame;
                maxrows = data.MaxRows;
                cols = data.VariableCount - 1;
                x = new double[cols + 1, maxrows + 1];
                cdx = new ColumnData[cols + 1];
                for (i = 0; i <= cols; i++)
                {
                    DoubleVariable vi = data.Variables[i].AsDoubleVariable;
                    cdx[i] = new ColumnData { Title = vi.Title, Rows = vi.Length };
                }

                w = new double[cols + 1, maxrows + 1];
                DataFrame weightsFrame = parameters["weights"].AsDataFrame;
                DoubleVariable weightsVariable = weightsFrame.Variables[0].AsDoubleVariable;
                wti = weightsVariable.Title;

                // Load the data, skipping rows where weights are 0 or missing
                int targetRow = 1;
                int removed = 0;
                for (int row = 0; row < maxrows; row++)
                {
                    double weight = weightsVariable.Data[row];
                    if (weight == Constant.MISSING || weight == 0)
                    {
                        // Remove the row from any variables that are at least this long
                        for (int col = 0; col <= cols; col++)
                        {
                            if (row < cdx[col].Rows + removed)
                                cdx[col].Rows--;
                        }
                        removed++;
                        continue;
                    }

                    if (weight < 0.0)
                    {
                        host.Error("Weights must not be negative", "Descriptive Statistics");
                        throw new TemplateOperationCancelledException();
                    }

                    for (int col = 0; col <= cols; col++)
                    {
                        w[col, targetRow] = weight;
                        DoubleVariable vi = data.Variables[col].AsDoubleVariable;
                        double value = Constant.MISSING;
                        if (vi.Length > row)
                            value = vi.Data[row];
                        x[col, targetRow] = value;
                    }
                    targetRow++;
                }
                // Account for missing or zero weights
                maxrows = targetRow - 1;
            }
            else
            {
                //  Index = 1: Univariate summary
                // store the data
                DataFrame data = parameters["data"].AsDataFrame;
                maxrows = data.MaxRows;
                cols = data.VariableCount - 1;
                x = new double[cols + 1, maxrows + 1];
                cdx = new ColumnData[cols + 1];
                for (i = 0; i <= cols; i++)
                {
                    DoubleVariable vi = data.Variables[i].AsDoubleVariable;
                    cdx[i] = new ColumnData { Title = vi.Title, Rows = vi.Length };
                    for (j = 1; j <= vi.Length; j++)
                        x[i, j] = vi.Data[j - 1];
                }
            }

            // get options
            double GAMMA = parameters["gamma"].AsDouble;
            string qxcl = " " + Formatting.XRound(GAMMA * 100, 1) + "% CL of mean";
            string sumTitle = isWeighted ? "Sum of weights" : "Sum";
            string[] titles = { "Valid data", "Missing data", sumTitle, "Mean", "Variance", "Standard deviation", "Variance coefficient", "Standard error of mean", "Upper" + qxcl, "Lower" + qxcl, "Geometric mean", "Skewness", "Kurtosis", "Maximum", "Upper quartile", "Median", "Lower quartile", "Minimum", "Range", "User defined centiles", null };

            checked1[0] = parameters["report-valid-data"].AsBoolean;
            checked1[1] = parameters["report-missing-data"].AsBoolean;
            checked1[2] = parameters["report-sum"].AsBoolean;
            checked1[3] = parameters["report-mean"].AsBoolean;
            checked1[4] = parameters["report-variance"].AsBoolean;
            checked1[5] = parameters["report-sd"].AsBoolean;
            checked1[6] = parameters["report-variance-coeff"].AsBoolean;
            checked1[7] = parameters["report-sem"].AsBoolean;
            checked1[8] = parameters["report-u95cl"].AsBoolean;
            checked1[9] = parameters["report-l95cl"].AsBoolean;
            checked1[10] = parameters["report-geometric-mean"].AsBoolean;
            checked1[11] = parameters["report-skewness"].AsBoolean;
            checked1[12] = parameters["report-kurtosis"].AsBoolean;
            checked1[13] = parameters["report-maximum"].AsBoolean;
            checked1[14] = parameters["report-uq"].AsBoolean;
            checked1[15] = parameters["report-median"].AsBoolean;
            checked1[16] = parameters["report-lq"].AsBoolean;
            checked1[17] = parameters["report-minimum"].AsBoolean;
            checked1[18] = parameters["report-range"].AsBoolean;
            checked1[19] = parameters["report-udc"].AsBoolean;
            UserCentileA1 = Convert.ToInt32(parameters["report-udca"].AsDouble);
            UserCentileB1 = Convert.ToInt32(parameters["report-udcb"].AsDouble);
            PrevCentileType = int.Parse(parameters["report-centile-type"].AsString);
            double centxl = UserCentileA1;
            double centxu = UserCentileB1;
            // prevchk1 = true; 
            bool shouldSave = parameters["output-to-frame"].AsBoolean;

            // get a result object for each column of data
            Summary[] sx = new Summary[cols + 1 /* VB to C# conversion */ ];
            for (i = 0; i <= cols; i++)
            {
                sx[i] = new Summary();
                if (isWeighted)
                    sx[i].WeightedSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, w, wti, nsumwt);
                else
                    sx[i].FullSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, PrevCentileType);
            }

            // Fill the report
            //  RTF_LoadTemplate("describe.rtf") Then
            // Insert the result into the report
            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> titlesList = new List<ParameterBag>();
            outputParameters.AddOutput("*titles", titlesList);
            for (i = 0; i <= cols; i++)
            { // Title

                ParameterBag titlesParameters = new ParameterBag();
                titlesList.Add(titlesParameters);
                string val = sx[i].Title;
                if ((i + 1) % 3 == 0 & cols > 2)
                    val += Formatting.RTFCRLF;
                titlesParameters.AddOutput("title", val);
            }
            List<ParameterBag> fieldsList = new List<ParameterBag>();
            outputParameters.AddOutput("*fields", fieldsList);
            for (j = 0; j <= 20; j++)
            {
                fieldsList.Add(FillField(host, j, sx, cols, checked1[j], titles[j], checked1[19], isWeighted));
            }

            // Fill the worksheet if required
            if (shouldSave)
            {
                //  descriptor = DescriptorForUnivariateDescription("Which results to paste to worksheet", GAMMA, False, True, False, False)
                //  If Host.DisplayOptions(descriptor) Then
                // Find how many columns have been selected
                int lc = 1;
                for (i = 0; i <= 19; i++)
                {
                    //  checked2(i) = descriptor.CheckBoxes(i).Checked
                    checked2[i] = checked1[i];
                    if (checked2[i])
                    {
                        lc += 1;
                    }
                }
                UserCentileA2 = UserCentileA1;
                UserCentileB2 = UserCentileB1;
                centxl = UserCentileA2;
                centxu = UserCentileB2;

                // recalculate if centiles selected have changed
                if ((UserCentileA2 > 0 & UserCentileA2 != UserCentileA1) | (UserCentileB2 > 0 & UserCentileB2 != UserCentileB1))
                {
                    for (i = 0; i <= cols; i++)
                    {
                        if (isWeighted)
                            sx[i].WeightedSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, w, wti, nsumwt);
                        else
                            sx[i].FullSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, PrevCentileType);
                    }
                }

                if (UserCentileA2 > 0)
                {
                    lc += 1;
                }
                if (UserCentileB2 > 0)
                {
                    lc += 1;
                }
                // prevchk2 = true; 
                if (lc > 0)
                {
                    DataFrame outputFrame = new DataFrame();
                    outputParameters.AddOutput("output", outputFrame);
                    // If the worksheet is loaded then fill it
                    StringVariable totalsVariable = new StringVariable { Title = "Title" };
                    totalsVariable.EnsureLength(cols + 1);
                    for (i = 0; i <= cols; i++)
                    {
                        totalsVariable.set_Data(i, sx[i].Title);
                    }
                    outputFrame.Variables.Add(totalsVariable);
                    for (i = 0; i <= 18; i++)
                    {
                        if (checked2[i])
                        {
                            outputFrame.Variables.Add(FillCell(i, sx, cols, titles[i], checked2[19]));
                        }
                    }
                    if (checked2[19])
                    {
                        outputFrame.Variables.Add(FillCell(19, sx, cols, null, checked2[19]));
                        outputFrame.Variables.Add(FillCell(20, sx, cols, null, checked2[19]));
                    }
                }
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        ///  <summary>
        ///  Return the median of the elements of x from ia to iz inclusive.
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="ia"></param>
        ///  <param name="iz"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public static double Median(double[] x, int ia, int iz)
        {
            double[] ao = new double[iz - ia + 1 /* for VB to C# conversion */ ];
            int reali = 0;
            for (int i = ia; i <= iz; i++)
            {
                if (x[i] != Constant.MISSING)
                {
                    ao[reali] = x[i];
                    reali = reali + 1;
                }
            }
            //  At this point, reali counts the number of elements that have been copied to ao.
            if (reali > 1)
            {
                // create temp variable for copying values 
                double[] transTemp0 = new double[reali - 1 + 1 /* for VB to C# conversion */];
                Array.Copy(ao, transTemp0, Math.Min(ao.Length, transTemp0.Length));
                ao = transTemp0;
                Array.Sort(ao);
                //  The -1 is because ao is 0-based
                double imdn = (0.5 * (reali + 1)) - 1;
                if (imdn < 0)
                    imdn = 0;
                if (imdn > reali - 1)
                    imdn = reali - 1;
                double fimdn = Math.Floor(imdn);
                int iimdn = (int)fimdn;
                if (imdn - fimdn == 0)
                {
                    //  Exact element
                    return ao[iimdn];
                }
                //  Weighted mean of adjacent elements
                return ao[iimdn] + (ao[iimdn + 1] - ao[iimdn]) * (imdn - fimdn);
            }
            return Constant.MISSING;
        }


        private static DoubleVariable FillCell(int opt, Summary[] sx, int cols, string title, bool shouldOutputCentiles)
        {
            int i = 0;
            DoubleVariable v = new DoubleVariable();
            v.EnsureLength(cols + 1);
            if (opt > 18)
            {
                if (shouldOutputCentiles)
                {
                    switch (opt)
                    {
                        case 19:
                            v.Title = sx[i].UserCentileUCaption;
                            break;
                        case 20:
                            v.Title = sx[i].UserCentileLCaption;
                            break;
                    }

                    for (i = 0; i <= cols; i++)
                    {
                        switch (opt)
                        {
                            case 19:
                                v.set_Data(i, sx[i].UserCentileU);
                                break;
                            case 20:
                                v.set_Data(i, sx[i].UserCentileL);
                                break;
                        }

                    }
                }
            }
            else
            {
                v.Title = title;
                for (i = 0; i <= cols; i++)
                {
                    double res;
                    switch (opt)
                    {
                        case 0:
                            res = sx[i].ValidData;
                            break;
                        case 1:
                            res = sx[i].MissingData;
                            break;
                        case 2:
                            res = sx[i].Sum;
                            break;
                        case 3:
                            res = sx[i].Mean;
                            break;
                        case 4:
                            res = sx[i].Variance;
                            break;
                        case 5:
                            res = sx[i].Sd;
                            break;
                        case 6:
                            res = sx[i].VarianceCoefficient;
                            break;
                        case 7:
                            res = sx[i].Sem;
                            break;
                        case 8:
                            res = sx[i].MeanUCL;
                            break;
                        case 9:
                            res = sx[i].MeanLCL;
                            break;
                        case 10:
                            res = sx[i].GeometricMean;
                            break;
                        case 11:
                            res = sx[i].Skewness;
                            break;
                        case 12:
                            res = sx[i].Kurtosis;
                            break;
                        case 13:
                            res = sx[i].Maximum;
                            break;
                        case 14:
                            res = sx[i].UpperQuartile;
                            break;
                        case 15:
                            res = sx[i].Median;
                            break;
                        case 16:
                            res = sx[i].LowerQuartile;
                            break;
                        case 17:
                            res = sx[i].Minimum;
                            break;
                        case 18:
                            res = sx[i].Range;
                            break;
                        default:
                            throw new ArgumentException("Unknown option", "opt");
                    }

                    v.set_Data(i, res);
                }
            }
            return v;
        }


        private static ParameterBag FillField(ITemplateHost host, int opt, Summary[] sx, int cols, bool optChecked, string optTitle, bool checked19, bool isWeighted)
        {
            ParameterBag fieldParameters = new ParameterBag();
            List<ParameterBag> resultsList = new List<ParameterBag>();
            fieldParameters.AddOutput("*results", resultsList);
            if (opt > 18)
            {
                if (checked19)
                {
                    switch (opt)
                    {
                        case 19:
                            fieldParameters.AddOutput("title", sx[0].UserCentileUCaption);
                            break;
                        case 20:
                            fieldParameters.AddOutput("title", sx[0].UserCentileLCaption);
                            break;
                    }

                    for (int i = 0; i <= cols; i++)
                    {
                        ParameterBag resultsParameters = new ParameterBag();
                        resultsList.Add(resultsParameters);
                        string res;
                        switch (opt)
                        {
                            case 19:
                                res = host.RoundU(sx[i].UserCentileU);
                                break;
                            case 20:
                                res = host.RoundU(sx[i].UserCentileL);
                                break;
                            default:
                                throw new ArgumentException("Unknown opt", "opt");
                        }

                        if ((i + 1) % 3 == 0 & cols > 2)
                        {
                            res += Formatting.RTFCRLF;
                        }
                        resultsParameters.AddOutput("result", res);
                    }
                    if (cols > 2 & (cols + 1) % 3 != 0)
                    {
                        //  Hack a LF on the end of the last string
                        string s = resultsList[resultsList.Count - 1]["result"].AsString;
                        s += Formatting.RTFCRLF;
                        resultsList[resultsList.Count - 1]["result"] = new FilledParameter(false, s);
                    }

                }
            }
            else
            {
                if (optChecked)
                {
                    fieldParameters.AddOutput("title", optTitle);
                    for (int i = 0; i <= cols; i++)
                    {
                        ParameterBag resultsParameters = new ParameterBag();
                        resultsList.Add(resultsParameters);
                        string res;
                        switch (opt)
                        {
                            case 0:
                                res = sx[i].ValidData.ToString();
                                break;
                            case 1:
                                res = sx[i].MissingData.ToString();
                                break;
                            case 2:
                                res = host.RoundU(isWeighted ? sx[i].SumOfWeights : sx[i].Sum);
                                break;
                            case 3:
                                res = host.RoundU(sx[i].Mean);
                                break;
                            case 4:
                                res = host.RoundU(sx[i].Variance);
                                break;
                            case 5:
                                res = host.RoundU(sx[i].Sd);
                                break;
                            case 6:
                                res = host.RoundU(sx[i].VarianceCoefficient);
                                break;
                            case 7:
                                res = host.RoundU(sx[i].Sem);
                                break;
                            case 8:
                                res = host.RoundU(sx[i].MeanUCL);
                                break;
                            case 9:
                                res = host.RoundU(sx[i].MeanLCL);
                                break;
                            case 10:
                                res = host.RoundU(sx[i].GeometricMean);
                                break;
                            case 11:
                                res = host.RoundU(sx[i].Skewness);
                                break;
                            case 12:
                                res = host.RoundU(sx[i].Kurtosis);
                                break;
                            case 13:
                                res = host.RoundU(sx[i].Maximum);
                                break;
                            case 14:
                                res = host.RoundU(sx[i].UpperQuartile);
                                break;
                            case 15:
                                res = host.RoundU(sx[i].Median);
                                break;
                            case 16:
                                res = host.RoundU(sx[i].LowerQuartile);
                                break;
                            case 17:
                                res = host.RoundU(sx[i].Minimum);
                                break;
                            case 18:
                                res = host.RoundU(sx[i].Range);
                                break;
                            default:
                                throw new ArgumentException("Unknown opt", "opt");
                        }

                        if ((i + 1) % 3 == 0 & cols > 2)
                        {
                            res += Formatting.RTFCRLF;
                        }
                        resultsParameters.AddOutput("result", res);
                    }
                    string s = resultsList[resultsList.Count - 1]["result"].AsString;
                    resultsList[resultsList.Count - 1]["result"] = new FilledParameter(false, s);
                }
            }
            return fieldParameters;
        }


        // Private Shared Function DescriptorForUnivariateDescription(ByVal Title As String, ByVal GAMMA As Double, ByVal UseCheck1 As Boolean, ByVal UseCheck2 As Boolean, ByVal UseCentileTypes As Boolean, ByVal ShowOutputSelection As Boolean) As OptionDescriptor
        //     Dim qxcl As String = " " & Formatting.XRound(GAMMA * 100, 1) & "% CL of mean"
        //     Dim descriptor As OptionDescriptor = New OptionDescriptor()
        //     descriptor.SelectionBoxes.Add(New SelectionBoxDescriptor())
        //     descriptor.SelectionBoxes.Add(New SelectionBoxDescriptor())
        //     descriptor.SelectionBoxes(0).Title = "User Centile A"
        //     descriptor.SelectionBoxes(1).Title = "User Centile B"
        //     descriptor.SelectionBoxes(0).FillFactor(1, 20, 1, vbNullString, 4)
        //     descriptor.SelectionBoxes(1).FillFactor(99, 80, -1, vbNullString, 4)
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk0", "Valid data", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk1", "Missing data", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk2", "Sum", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk3", "Mean", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk4", "Variance", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk5", "Standard deviation", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk6", "Variance coefficient", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk7", "Standard error of mean", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk8", "Upper" & qxcl, False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk9", "Lower" & qxcl, False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk10", "Geometric mean", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk11", "Skewness", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk12", "Kurtosis", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk13", "Maximum", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk14", "Upper quartile", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk15", "Median", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk16", "Lower quartile", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk17", "Minimum", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk18", "Range", False, False))
        //     descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk19", "User defined centiles", False, False))
        //     If UseCentileTypes Then
        //         descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk20", "Centile type 1", PrevCentileType = 1, True))
        //         descriptor.CheckBoxes.Add(New CheckBoxDescriptor("chk21", "Centile type 2", PrevCentileType = 2, True))
        //     End If
        //     If UseCheck1 Then
        //         If prevchk1 Then
        //             For i As Integer = 0 To 19
        //                 descriptor.CheckBoxes(i).Checked = checked1(i)
        //             Next
        //             descriptor.SelectionBoxes(0).SelectedValue = Format(UserCentileA1)
        //             descriptor.SelectionBoxes(1).SelectedValue = Format(UserCentileB1)
        //             If descriptor.SelectionBoxes(0).Value = "0" Then descriptor.SelectionBoxes(0).SelectedValue = vbNullString
        //             If descriptor.SelectionBoxes(1).Value = "0" Then descriptor.SelectionBoxes(1).SelectedValue = vbNullString
        //         Else
        //             For i As Integer = 0 To 19
        //                 descriptor.CheckBoxes(i).Checked = True
        //             Next
        //             descriptor.SelectionBoxes(0).SelectedValue = "5"
        //             descriptor.SelectionBoxes(1).SelectedValue = "95"
        //         End If
        //     End If
        //     If UseCheck2 Then
        //         If prevchk2 Then
        //             For i As Integer = 0 To 19
        //                 descriptor.CheckBoxes(i).Checked = checked2(i)
        //             Next
        //             descriptor.SelectionBoxes(0).SelectedValue = Format(UserCentileA2)
        //             descriptor.SelectionBoxes(1).SelectedValue = Format(UserCentileB2)
        //             If descriptor.SelectionBoxes(0).Value = "0" Then descriptor.SelectionBoxes(0).SelectedValue = vbNullString
        //             If descriptor.SelectionBoxes(1).Value = "0" Then descriptor.SelectionBoxes(1).SelectedValue = vbNullString
        //         Else
        //             For i As Integer = 0 To 19
        //                 descriptor.CheckBoxes(i).Checked = checked1(i)
        //             Next
        //             descriptor.SelectionBoxes(0).SelectedValue = Format(UserCentileA1)
        //             descriptor.SelectionBoxes(1).SelectedValue = Format(UserCentileB1)
        //             If descriptor.SelectionBoxes(0).Value = "0" Then descriptor.SelectionBoxes(0).SelectedValue = vbNullString
        //             If descriptor.SelectionBoxes(1).Value = "0" Then descriptor.SelectionBoxes(1).SelectedValue = vbNullString
        //         End If
        //     End If
        //     descriptor.Title = Title
        //     Return descriptor
        // End Function

    }


}

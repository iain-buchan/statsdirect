using System.Diagnostics;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;

using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public class Sheet
    {
        private struct Catvar : IComparable<Catvar>
        {
            public string Title;
            public int Id;

            private int CompareTo(Catvar other)
            {
                double mti, oti;
                if (double.TryParse(Title, out mti) && double.TryParse(other.Title, out oti))
                {
                    if (mti < oti)
                    {
                        return -1;
                    }
                    return mti == oti ? 0 : 1;
                }
                return String.CompareOrdinal(Title, other.Title);
            }
            // interface methods implemented by CompareTo
            int IComparable<Catvar>.CompareTo(Catvar other)
            {
                return CompareTo(other);
            }

        }

        private class SortPair : IComparable<SortPair>
        {
            private readonly double value;
            public readonly int Row;

            private int CompareTo(SortPair other)
            {
                return value.CompareTo(other.value);
            }

            int IComparable<SortPair>.CompareTo(SortPair other)
            {
                return CompareTo(other);
            }

            public SortPair(double value, int row)
            {
                this.value = value;
                Row = row;
            }
        }

        public static ParameterBag ShtFillSeries(ITemplateHost host, ParameterBag parameters)
        {
            int rows = parameters["rows"].AsInt32;
            if (rows < 1)
                rows = 100;
            if (rows > 1000000)
                rows = 1000000;
            double startval = parameters["startval"].AsDouble;
            string formula = parameters["formula"].AsString.ToUpper();
            if (formula.Length < 3)
            {
                formula = "x+1";
            }
            string ti = parameters["title"].AsString;
            if (ti.Length < 1)
            {
                ti = "series=" + formula;
            }

            double currentval = startval;
            DoubleVariable v = new DoubleVariable(rows, ti);
            Calcit c = new Calcit(formula);
            DataFrame outputFrame = new DataFrame(v);
            double[] x = new double[1];
            for (int i = 0; i <= rows - 1; i++)
            {
                v.SetData(i, currentval);
                x[0] = currentval;
                currentval = c.Evaluate(x);
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }

        public static ParameterBag ShtConvertUnits(ITemplateHost host, ParameterBag parameters)
        {
            string conversion = parameters["conversion"].AsString;
            string[] splitConversion = conversion.Split('|');
            string formula = splitConversion[0];
            string outputUnits = splitConversion[1];
            Calcit c = new Calcit(formula);
            double[] x = new double[1];

            DataFrame dataFrame = parameters["data"].AsDataFrame;
            DataFrame outputFrame = new DataFrame();
            foreach (Variable inputVariable in dataFrame.Variables)
            {
                DoubleVariable dataVariable = inputVariable.AsDoubleVariable;
                DoubleVariable outputVariable = new DoubleVariable(dataVariable.Length, inputVariable.Title + " {" + outputUnits + "}");
                outputFrame.Variables.Add(outputVariable);
                double[] data = dataVariable.Data;
                double[] output = outputVariable.Data;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == Constant.MISSING)
                        output[i] = Constant.MISSING;
                    else
                    {
                        x[0] = data[i];
                        output[i] = c.Evaluate(x);
                    }
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }

        ///  <summary>
        ///  Returns a constant to add to all input values to ensure that all f(x) with the given input data are valid.
        ///  </summary>
        public static void XConstant(int rows, int lowerBound, double[] data, out double minimumC, out double suggestedC)
        {
            //  Suggest constant to make all fn(x) possible
            double aMin = double.MaxValue;
            double aMax = double.MinValue;
            for (int n = lowerBound; n <= rows + lowerBound - 1; n++)
            {
                if (data[n] != Constant.MISSING)
                {
                    if (data[n] < aMin)
                    {
                        aMin = data[n];
                    }
                    if (data[n] > aMax)
                    {
                        aMax = data[n];
                    }
                }
            }
            if (aMin < 0)
            {
                double zmin; double zstep;
                Charting.AxisScaler.Axis(ref aMin, ref aMax, 20, out zmin, out zstep);
                minimumC = Math.Abs(aMin);
                suggestedC = Math.Abs(zmin);
            }
            else
            {
                minimumC = Constant.MISSING;
                suggestedC = Constant.MISSING;
            }
        }


        public static ParameterBag ShtClearMissing(ITemplateHost host, ParameterBag parameters)
        {
            int r;
            int ctr;

            DataFrame data = parameters["data"].AsDataFrame;
            int totrows = data.MaxRows;
            int totcols = data.VariableCount;
            string[,] hold = new string[totrows + 1, totcols + 1];

            string clearRowString = parameters["row-or-cell"].AsString;
            bool clearRow = totcols > 1 && "row".Equals(clearRowString);
            double userNumber = parameters.ContainsKey("missing-double") ? parameters["missing-double"].AsDouble : Constant.MISSING;
            string userText = parameters.ContainsKey("missing-text") && parameters["missing-text"].AsString.Trim().Length > 0
                                  ? parameters["missing-text"].AsString
                                  : "";
            for (int c = 0; c <= totcols - 1; c++)
            {
                StringVariable v = data.Variables[c].AsStringVariable;
                int rx = 0;
                for (r = 0; r <= totrows - 1; r++)
                {
                    rx++;
                    hold[rx, c] = ((v.Length <= r) || IsMissing(v.Data[r], userNumber, userText)) ? "" : v.Data[r];
                }
                hold[0, c] = v.Title;
            }
            int maxctr = 0;
            // int lc = totcols; 

            //  Set up the output
            DataFrame outputFrame = new DataFrame();
            for (int c = 0; c <= totcols - 1; c++)
            {
                string outputName = data.Variables[c].AsStringVariable.Title;
                if (outputName.Length > 0)
                {
                    outputName += " [no gaps]";
                }
                StringVariable outputVariable = new StringVariable(totrows, outputName);
                outputFrame.Variables.Add(outputVariable);
            }
            // lc = lc + 1; 
            if (clearRow)
            {
                ctr = 0;
                for (r = 1; r <= totrows; r++)
                {
                    int ctrx = 0;
                    for (int c = 0; c <= totcols - 1; c++)
                    {
                        if (hold[r, c].Length > 0)
                        {
                            ctrx++;
                        }
                    }
                    if (ctrx == totcols)
                    {
                        ctr = ctr + 1;
                        for (int c = 0; c <= totcols - 1; c++)
                        {
                            if (ctr > maxctr)
                            {
                                maxctr = ctr;
                            }
                            outputFrame.Variables[c].AsStringVariable.SetData(ctr - 1, hold[r, c]);
                        }
                    }
                }
                for (int c = 0; c <= totcols - 1; c++)
                {
                    outputFrame.Variables[c].EnsureLength(ctr);
                }
            }
            else
            {
                for (int c = 0; c <= totcols - 1; c++)
                {
                    ctr = 0;
                    for (r = 1; r <= totrows; r++)
                    {
                        string transTemp2 = hold[r, c];
                        if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp2.Length > 0)
                        {
                            ctr = ctr + 1;
                            if (ctr > maxctr)
                            {
                                maxctr = ctr;
                            }
                            outputFrame.Variables[c].AsStringVariable.SetData(ctr - 1, hold[r, c]);
                        }
                    }
                    outputFrame.Variables[c].EnsureLength(ctr);
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }


        public static bool IsMissing(string value, double userNumber, string userText)
        {
            double x;
            if (double.TryParse(value, out x))
            {
                //  It's a double.  If it's MISSING, it's missing.
                if (x == Constant.MISSING)
                {
                    return true;
                }
                //  If there is a user number and x is that user number, it's missing.
                return userNumber != Constant.MISSING && x == userNumber;
            }
            if (value == null || Formatting.ASTERISK.Equals(value) || "MISSING".Equals(value.ToUpper()) || Formatting.FULLSTOP.Equals(value) || value.Trim().Length == 0)
            {
                return true;
            }
            return !string.IsNullOrEmpty(userText) && userText.Equals(value);
        }



        public static ParameterBag ShtDummyVariables(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            ClassifierVariable categoryVariable = data.Variables[0].AsClassifierVariable;
            DataFrame outputFrame = ToDummyVariables(host, categoryVariable, false);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }

        /// <returns>null if the user wishes to treat the data as continuous (in which case the caller should probably use the variable that has been passed in), otherwise a frame of dummies.</returns>
        public static DataFrame ToDummyVariables(ITemplateHost host, ClassifierVariable categoryVariable, bool allowContinuous)
        {
            int rows = categoryVariable.Length;
            int cats = categoryVariable.GroupCount;

            // find missing data category
            int mc = -1;
            for (int i = 0; i < cats; i++)
            {
                if (categoryVariable.Groups[i].Label == Formatting.MISSINGLABEL)
                {
                    mc = i;
                    break;
                }
            }

            // find the most prevalent category
            int maxcat = 0;
            int maxcatidx = 0;
            for (int i = 0; i < cats; i++)
            {
                if (i != mc && categoryVariable.Groups[i].NBin > maxcat)
                {
                    maxcat = categoryVariable.Groups[i].NBin;
                    maxcatidx = i;
                }
            }
            string maxcatti = categoryVariable.Groups[maxcatidx].Label;

            // get non-missing categories
            int ng = 1;
            double[] g = new double[cats];
            Catvar[] gcat = new Catvar[cats];
            for (int j = 0; j < rows; j++)
            {
                if (categoryVariable.Data[j] != mc && categoryVariable.Data[j] != Constant.MISSING)
                {
                    g[0] = categoryVariable.Data[j];
                    gcat[0].Title = categoryVariable.Groups[Convert.ToInt32(categoryVariable.Data[j])].Label;
                    gcat[0].Id = Convert.ToInt32(categoryVariable.Data[j]);
                    break;
                }
            }
            for (int j = 1; j < rows; j++)
            {
                bool newa = true;
                for (int i = 0; i < ng; i++)
                {
                    if (categoryVariable.Data[j] == g[i] || categoryVariable.Data[j] == mc || categoryVariable.Data[j] == Constant.MISSING)
                    {
                        newa = false;
                        break;
                    }
                }
                if (newa)
                {
                    g[ng] = categoryVariable.Data[j];
                    gcat[ng].Title = categoryVariable.Groups[Convert.ToInt32(categoryVariable.Data[j])].Label;
                    gcat[ng].Id = Convert.ToInt32(categoryVariable.Data[j]);
                    ng++;
                }
            }

            int dummies = ng - 1;
            if (dummies < 1)
            {
                host.Error("You must have more than one category in your data", "Dummy Variables");
                return null;
            }

            // sort categories by label to be consistent with Stata xi etc.
            Array.Sort(gcat, 0, ng);

            DummyOptions dm = new DummyOptions { LargestCategoryTitle = maxcatti, CategoryNames = new List<string>(), VariableName = categoryVariable.Title, AllowUserToTreatAsContinuous = allowContinuous };
            for (int j = 0; j < ng; j++)
                dm.CategoryNames.Add(gcat[j].Title);
            bool wasOk = null != host.Amend(dm, new ParameterBag());
            if (!(wasOk))
                throw new TemplateOperationCancelledException();

            DataFrame outputFrame = new DataFrame();
            if (dm.TreatAsContinuous)
                return null;
            // Split to multiple dummies
            for (int j = 0; j < ng; j++)
            {
                if (j != dm.JDrop)
                {
                    string title = categoryVariable.Title + "(" + gcat[j].Title + ")";
                    DoubleVariable outputVariable = new DoubleVariable(rows, title);
                    for (int r = 0; r < rows; r++)
                    {
                        // only enter if not missing category mc
                        if (categoryVariable.Data[r] != mc && categoryVariable.Data[r] != Constant.MISSING)
                            outputVariable.Data[r] = categoryVariable.Data[r] == gcat[j].Id ? 1 : 0;
                    }
                    outputFrame.Variables.Add(outputVariable);
                }
            }
            return outputFrame;
        }

        public static ParameterBag ShtLadderPowers(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            double cons = Constant.MISSING;
            if (parameters.ContainsKey("c"))
            {
                cons = parameters["c"].AsDouble;
            }
            bool skipMissing = cons == Constant.MISSING;
            if (skipMissing)
            {
                cons = 0;
            }
            string titleCore = inputVariable.Title;
            string logTitle = "ln(" + titleCore + ")";
            if (cons != 0)
            {
                titleCore = "(" + titleCore + " + " + cons.ToString() + ")";
                logTitle = "ln" + titleCore;
            }

            DataFrame outputFrame = new DataFrame();
            DoubleVariable minusTwoVariable = new DoubleVariable(inputVariable.Length, titleCore + "^-2");
            outputFrame.Variables.Add(minusTwoVariable);
            DoubleVariable minusOneVariable = new DoubleVariable(inputVariable.Length, titleCore + "^-1");
            outputFrame.Variables.Add(minusOneVariable);
            DoubleVariable minusHalfVariable = new DoubleVariable(inputVariable.Length, titleCore + "^-0.5");
            outputFrame.Variables.Add(minusHalfVariable);
            DoubleVariable logVariable = new DoubleVariable(inputVariable.Length, logTitle);
            outputFrame.Variables.Add(logVariable);
            DoubleVariable halfVariable = new DoubleVariable(inputVariable.Length, titleCore + "^0.5");
            outputFrame.Variables.Add(halfVariable);
            DoubleVariable squaredVariable = new DoubleVariable(inputVariable.Length, titleCore + "^2");
            outputFrame.Variables.Add(squaredVariable);

            for (int n = 0; n <= inputVariable.Length - 1; n++)
            {
                if (inputVariable.Data[n] == Constant.MISSING)
                {
                    minusTwoVariable.SetData(n, Constant.MISSING);
                    minusOneVariable.SetData(n, Constant.MISSING);
                    minusHalfVariable.SetData(n, Constant.MISSING);
                    logVariable.SetData(n, Constant.MISSING);
                    halfVariable.SetData(n, Constant.MISSING);
                    squaredVariable.SetData(n, Constant.MISSING);
                }
                else
                {
                    double z = inputVariable.Data[n] + cons;
                    // -2
                    if (z == 0)
                    {
                        minusTwoVariable.SetData(n, Constant.MISSING);
                    }
                    else
                    {
                        try
                        {
                            minusTwoVariable.SetData(n, Math.Pow(z, -2.0));
                        }
                        catch (Exception)
                        {
                            minusTwoVariable.Data[n] = Constant.MISSING;
                        }
                    }
                    // -1
                    minusOneVariable.SetData(n, z == 0 ? Constant.MISSING : Math.Pow(z, -1.0));
                    // -0.5
                    if (z <= 0)
                    {
                        minusHalfVariable.SetData(n, Constant.MISSING);
                    }
                    else
                    {
                        try
                        {
                            minusHalfVariable.SetData(n, Math.Pow(z, -0.5));
                        }
                        catch (Exception)
                        {
                            minusHalfVariable.Data[n] = Constant.MISSING;
                        }
                    }
                    // log
                    logVariable.SetData(n, z <= 0 ? Constant.MISSING : Math.Log(z));
                    // 0.5
                    if (z < 0)
                    {
                        halfVariable.SetData(n, Constant.MISSING);
                    }
                    else
                    {
                        try
                        {
                            halfVariable.SetData(n, Math.Pow(z, 0.5));
                        }
                        catch (Exception)
                        {
                            halfVariable.SetData(n, Constant.MISSING);
                        }
                    }
                    // 2
                    try
                    {
                        squaredVariable.SetData(n, Math.Pow(z, 2.0));
                    }
                    catch (Exception)
                    {
                        squaredVariable.SetData(n, Constant.MISSING);
                    }
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }


        public static ParameterBag ShtStandardize(ITemplateHost host, ParameterBag parameters)
        {
            string msd = parameters["mode"].AsString;
            int method;
            string lab;
            if ("msd".Equals(msd))
            {
                method = 1;
                lab = "(x-mean)/SD";
            }
            else if ("sd".Equals(msd))
            {
                method = 2;
                lab = "x/SD";
            }
            else if ("m".Equals(msd))
            {
                method = 3;
                lab = "x-mean";
            }
            else if ("ecdf".Equals(msd))
            {
                method = 4;
                lab = "ecdf";
            }
            else
            {
                throw new ArgumentException("Unknown method");
            }

            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            double[] inputData = inputVariable.Data;
            double sum = 0;
            int nx = 0;
            foreach (double value in inputData)
            {
                if (value != Constant.MISSING)
                {
                    nx += 1;
                    sum += value;
                }
            }
            double mean = sum / Convert.ToDouble(nx);
            double ssq = 0;
            foreach (double value in inputData)
            {
                ssq += (value - mean) * (value - mean);
            }
            double sd = Math.Sqrt(ssq / Convert.ToDouble(nx - 1));
            // int lc = 1; 
            string pre = "Std (" + lab + "): ";

            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(inputVariable.Length, pre + inputVariable.Title);
            outputFrame.Variables.Add(outputVariable);
            if (method == 4)
            {
                int ierr;
                MathDbl.ecdf(inputVariable.Data, outputVariable.Data, out ierr);
                if (ierr == 1)
                {
                    throw new ArgumentException("Must have at least 3 data values to calculate empirical CDF");
                }
            }
            else
            {
                for (int c = 0; c <= inputVariable.Length - 1; c++)
                {
                    double x = inputVariable.Data[c];
                    if (inputVariable.Data[c] != Constant.MISSING)
                    {
                        double tr;
                        switch (method)
                        {
                            case 1:
                                if (sd != 0.0)
                                {
                                    tr = (x - mean) / sd;
                                }
                                else
                                {
                                    tr = Constant.MISSING;
                                }
                                break;
                            case 2:
                                if (sd != 0.0)
                                {
                                    tr = x / sd;
                                }
                                else
                                {
                                    tr = Constant.MISSING;
                                }
                                break;
                            default:
                                tr = x - mean;
                                break;
                        }

                        outputVariable.SetData(c, tr);
                    }
                    else
                    {
                        outputVariable.SetData(c, Constant.MISSING);
                    }
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }


        public static double XSpr(double q)
        {
            double q1 = Math.Abs(q) > Constant.LMREAL ? Constant.MISSING : q;
            return double.IsNaN(q) ? Constant.MISSING : q1;
        }


        public static ParameterBag ShtCombine(ITemplateHost host, ParameterBag parameters)
        {
            int totrows = 0;
            int ep;
            string gpti = null; string datti = null; string lastgpti = null; string lastdatti = null;

            DataFrame data = parameters["data"].AsDataFrame;
            foreach (Variable v in data.Variables)
            {
                totrows += v.Length;
            }
            // reconstitute original labels if split using split function --->
            bool ok = true;
            for (int c = 0; c <= data.VariableCount - 1; c++)
            {
                DoubleVariable v = data.Variables[c].AsDoubleVariable;
                ep = v.Title.IndexOf("=", StringComparison.Ordinal);
                int tp = v.Title.IndexOf("~", StringComparison.Ordinal);
                if (ep < 0 || tp < 0 || tp > ep)
                {
                    ok = false;
                    break;
                }
                datti = v.Title.Substring(0, tp);
                if ((!string.IsNullOrEmpty(lastdatti)) && datti != lastdatti)
                {
                    ok = false;
                    break;
                }
                lastdatti = datti;
                gpti = v.Title.Substring(tp + 1, ep - tp - 1);
                if ((!string.IsNullOrEmpty(lastgpti)) && gpti != lastgpti)
                {
                    ok = false;
                    break;
                }
                lastgpti = gpti;
            }
            // <---
            if (!(ok))
            {
                datti = "Data";
                gpti = "Group ID";
            }
            DataFrame outputFrame = new DataFrame();
            StringVariable groupVariable = new StringVariable(totrows, gpti);
            outputFrame.Variables.Add(groupVariable);
            DoubleVariable dataVariable = new DoubleVariable(totrows, datti);
            outputFrame.Variables.Add(dataVariable);
            int row = 0;
            for (int c = 0; c <= data.VariableCount - 1; c++)
            {
                DoubleVariable v = data.Variables[c].AsDoubleVariable;
                ep = v.Title.IndexOf("=", StringComparison.Ordinal) + 1;
                string outputTitle = v.Title;
                if (ok)
                {
                    groupVariable.SetData(row, v.Title.Substring(v.Title.Length - v.Title.Length - ep));
                }
                foreach (double value in v.Data)
                {
                    groupVariable.SetData(row, outputTitle);
                    dataVariable.SetData(row, value);
                    row += 1;
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }


        public static ParameterBag ShtDates(ITemplateHost host, ParameterBag parameters)
        {

            DataFrame data = parameters["data"].AsDataFrame;
            DateVariable inputVariable = data.Variables[0].AsDateVariable;

            string interval = parameters["interval"].AsString;
            DateTime indate = parameters["indate"].AsDate;
            string q;
            switch (interval)
            {
                case "yyyy":
                    q = "Years";
                    break;
                case "m":
                    q = "Months";
                    break;
                case "w":
                    q = "Weeks";
                    break;
                case "d":
                    q = "Days";
                    break;
                case "h":
                    q = "Hours";
                    break;
                case "n":
                    q = "Minutes";
                    break;
                case "s":
                    q = "Seconds";
                    break;
                default:
                    throw new ArgumentException("parameters[interval]: Unexpected interval", "parameters");
            }


            string outputTitle = inputVariable.Title + "~" + q + " from " + indate;
            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(inputVariable.Length, outputTitle);
            outputFrame.Variables.Add(outputVariable);
            for (int i = 0; i <= inputVariable.Length - 1; i++)
            {
                if (inputVariable.Data[i] == DateTime.MinValue)
                {
                    outputVariable.SetData(i, Constant.MISSING);
                }
                else
                {
                    outputVariable.Data[i] = DateAndTime.DateDiff(interval, indate, inputVariable.Data[i]);
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }


        public static ParameterBag ShtGroupSplit(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame gidsFrame = parameters["gids"].AsDataFrame;
            ClassifierVariable gidsVariable = gidsFrame.Variables[0].AsClassifierVariable;
            int rows = gidsVariable.Length;
            int ng = gidsVariable.GroupCount;
            double[] gid = new double[rows + 1 ];
            string[] glabel = new string[ng + 1 ];
            double[] g = new double[ng + 1 ];
            for (int c = 1; c <= ng; c++)
            {
                if (gidsVariable.Title == "Group ID")
                    glabel[c] = gidsVariable.Groups[c - 1].Label;
                else
                    glabel[c] = gidsVariable.Title + "=" + gidsVariable.Groups[c - 1].Label;
                if (gidsVariable.Groups[c - 1].Label == Formatting.MISSINGLABEL)
                    g[c] = Constant.MISSING;
                else
                    g[c] = c - 1;
            }
            for (int c = 1; c <= rows; c++)
                gid[c] = gidsVariable.Data[c - 1];

            DataFrame data = parameters["data"].AsDataFrame;
            int cols = data.VariableCount;
            double[,] x = new double[cols + 1, rows + 1];
            // Transfer data to working arrays, padding with MISSING as necessary
            for (int i = 1; i <= rows; i++)
            {
                for (int j = 1; j <= cols; j++)
                {
                    if (i > data.Variables[j - 1].Length)
                    {
                        x[j, i] = Constant.MISSING;
                    }
                    else
                    {
                        x[j, i] = data.Variables[j - 1].AsDoubleVariable.Data[i - 1];
                    }
                }
            }

            DataFrame outputFrame = new DataFrame();
            int lc = 0;
            for (int j = 1; j <= ng; j++)
            {
                for (int c = 1; c <= cols; c++)
                {
                    string variableName;
                    if (data.Variables[c - 1].Title == "Data")
                    {
                        variableName = glabel[j];
                    }
                    else
                    {
                        variableName = data.Variables[c - 1].Title + "~" + glabel[j];
                    }
                    DoubleVariable v = new DoubleVariable(0, variableName); // TODO: Efficiency: This will reallocate the data array many times.  Can we be more sensible?
                    outputFrame.Variables.Add(v);
                }
                int rw = 0;
                for (int r = 1; r <= rows; r++)
                {
                    if (gid[r] == g[j])
                    {
                        for (int c = 1; c <= cols; c++)
                            outputFrame.Variables[lc + c - 1].AsDoubleVariable.SetData(rw, x[c, r]);
                        rw++;
                    }
                }
                lc += cols;
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("ng", ng);
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }


        public static ParameterBag ShtNormal(ITemplateHost host, ParameterBag parameters)
        {
            int n;
            int method; int nx = 0;
            int cx = 0; int c;
            double den;

            string lab = parameters["method"].AsString;
            if ("vdW".Equals(lab))
            {
                method = 1;
            }
            else if ("Blom".Equals(lab))
            {
                method = 2;
            }
            else
            {
                method = 3;
            }
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable dataVariable = data.Variables[0].AsDoubleVariable;
            int rows = dataVariable.Length;
            double[] prk = new double[rows + 1 ];
            for (n = 1; n <= rows; n++)
            {
                if (dataVariable.Data[n - 1] != Constant.MISSING)
                {
                    nx = nx + 1;
                    prk[nx] = dataVariable.Data[n - 1];
                }
            }
            double[] r = new double[nx + 1 ];
            double xf;
            ExFortran.Rank(prk, r, 1, nx, 0, out xf);
            if (method == 3)
            {
                if (rows > 2500)
                {
                    method = 1;
                    lab = "vdW";
                }
            }
            string pre = "Nml Score (" + lab + "): ";
            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(rows, pre + dataVariable.Title);
            outputFrame.Variables.Add(outputVariable);
            if (method == 1)
            {
                den = Convert.ToDouble(rows + 1L);
            }
            else
            {
                den = Convert.ToDouble(rows) + 1.0 / 4.0;
            }
            for (c = 1; c <= rows; c++)
            {
                if (dataVariable.Data[c - 1] != Constant.MISSING)
                {
                    cx = cx + 1;
                    double tr;
                    int ifault;
                    switch (method)
                    {
                        case 1:
                            // van der Waerden, Conover P396
                            tr = PDF.gauinv(r[cx] / den, out ifault);
                            if (ifault != 0)
                            {
                                tr = Constant.MISSING;
                            }
                            break;
                        case 2:
                            // Blom
                            tr = PDF.gauinv((r[cx] - 3.0 / 8.0) / den, out ifault);
                            if (ifault != 0)
                            {
                                tr = Constant.MISSING;
                            }
                            break;
                        default:
                            // expected normal order
                            tr = Expnos.expnos(Convert.ToInt32(r[cx]), rows);
                            break;
                    }

                    outputVariable.SetData(c - 1, tr == Constant.MISSING ? Constant.MISSING : tr);
                }
                else
                {
                    outputVariable.SetData(c - 1, Constant.MISSING);
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }


        public static ParameterBag ShtPairDifferences(ITemplateHost host, ParameterBag parameters)
        {
            return ShtPair(host, parameters, 1);
        }


        public static ParameterBag ShtPairMeans(ITemplateHost host, ParameterBag parameters)
        {
            return ShtPair(host, parameters, 2);
        }


        public static ParameterBag ShtPairSlopes(ITemplateHost host, ParameterBag parameters)
        {
            return ShtPair(host, parameters, 3);
        }


        ///  <param name="parameters"></param>
        /// <param name="index">1 = differences, 2 = means, 3 = slopes</param>
        /// <param name="host"></param>
        private static ParameterBag ShtPair(ITemplateHost host, ParameterBag parameters, int index)
        {
            int i;
            int ctr; int rows2 = 0; int limit = 0;
            int j;
            string qx = null; string xt = null;
            double[] xx = null; double[] x; double[] y = null;
            double mdn = 0;

            DataFrame yFrame = parameters["y"].AsDataFrame;
            DoubleVariable yVariable = yFrame.Variables[0].AsDoubleVariable;
            int rows = yVariable.Length;
            string yt = yVariable.Title;
            double[] yy = new double[rows + 1 ];
            for (i = 1; i <= rows; i++)
            {
                yy[i] = yVariable.Data[i - 1];
            }
            if (index != 2)
            {
                DataFrame xFrame = parameters["x"].AsDataFrame;
                DoubleVariable xVariable = xFrame.Variables[0].AsDoubleVariable;
                rows2 = xVariable.Length;
                if (rows2 != rows & index == 3)
                {
                    //  Should never happen due to the data acquisition, but just in case...
                    host.Error(Formatting.ERRCOLON + "unequal number of observations in X and Y.", "Pairwise");
                    return null;
                }
                xt = xVariable.Title;
                xx = new double[rows2 + 1 ];
                for (i = 1; i <= rows2; i++)
                {
                    xx[i] = xVariable.Data[i - 1];
                }
            }

            if (index == 2)
            {
                // means
                x = new double[rows + 1 ];
                ctr = 0;
                for (i = 1; i <= rows; i++)
                {
                    if (yy[i] != Constant.MISSING)
                    {
                        ctr = ctr + 1;
                        x[ctr] = yy[i];
                    }
                }
                rows = ctr;
            }
            else if (index == 3)
            {
                // slopes
                x = new double[rows + 1 ];
                y = new double[rows + 1 ];
                ctr = 0;
                for (i = 1; i <= rows; i++)
                {
                    Debug.Assert(xx != null, "xx != null");
                    if (xx[i] != Constant.MISSING & yy[i] != Constant.MISSING)
                    {
                        ctr = ctr + 1;
                        x[ctr] = xx[i];
                        y[ctr] = yy[i];
                    }
                }
                rows = ctr;
            }
            else
            {
                // differences
                x = new double[rows + 1 ];
                y = new double[rows2 + 1 ];
                ctr = 0;
                for (i = 1; i <= rows2; i++)
                {
                    Debug.Assert(xx != null, "xx != null");
                    if (xx[i] != Constant.MISSING)
                    {
                        ctr = ctr + 1;
                        x[ctr] = xx[i];
                    }
                }
                rows2 = ctr;
                ctr = 0;
                for (i = 1; i <= rows; i++)
                {
                    if (yy[i] != Constant.MISSING)
                    {
                        ctr = ctr + 1;
                        y[ctr] = yy[i];
                    }
                }
                rows = ctr;
            }

            // lc = 1; 
            switch (index)
            {
                case 1:
                    limit = rows * rows;
                    qx = "Differences (" + xt + " - " + yt + ")";
                    break;
                case 2:
                    limit = Convert.ToInt32(rows * (rows + 1) / 2);
                    qx = "Means within " + yt;
                    break;
                case 3:
                    limit = Convert.ToInt32((rows - 2) * (rows - 1) / 2);
                    qx = "Slopes(" + xt + ", " + yt + ")";
                    break;
            }

            if (limit > host.Preferences.MaxRows)
            {
                host.Error(Formatting.ERRCOLON + "too many data.", "Pairwise");
                return null;
            }
            // t = InputFrm("New column name", qx, "Pairwise")
            string t = qx;

            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(limit, t);
            outputFrame.Variables.Add(outputVariable);
            int cnt = 0;
            switch (index)
            {
                case 1:
                    for (i = 1; i <= rows; i++)
                    {
                        for (j = 1; j <= rows2; j++)
                        {
                            Debug.Assert(y != null, "y != null");
                            if (x[i] == Constant.MISSING || y[j] == Constant.MISSING)
                            {
                                outputVariable.SetData(cnt, Constant.MISSING);
                            }
                            else
                            {
                                outputVariable.SetData(cnt, x[i] - y[j]);
                            }
                            cnt = cnt + 1;
                        }
                    }
                    break;
                case 2:
                    for (i = 1; i <= rows; i++)
                    {
                        for (j = i; j <= rows; j++)
                        {
                            if (x[i] == Constant.MISSING)
                            {
                                outputVariable.SetData(cnt, Constant.MISSING);
                            }
                            else
                            {
                                outputVariable.SetData(cnt, (x[i] + x[j]) / 2.0);
                            }
                            cnt = cnt + 1;
                        }
                    }
                    break;
                case 3:
                    Debug.Assert(y != null, "y != null");
                    for (i = 1; i <= rows - 1; i++)
                    {
                        for (j = i + 1; j <= rows; j++)
                        {
                            if (x[i] != x[j])
                            {
                                if (x[i] == Constant.MISSING || y[j] == Constant.MISSING || x[i] - x[j] == 0.0)
                                {
                                    outputVariable.SetData(cnt, Constant.MISSING);
                                }
                                else
                                {
                                    outputVariable.SetData(cnt, (y[i] - y[j]) / (x[i] - x[j]));
                                }
                                cnt = cnt + 1;
                            }
                        }
                    }
                    if (parameters["calculate-slope"].AsBoolean)
                    {
                        double gamma = parameters["gamma"].AsDouble;
                        double p = (1.0 - gamma) / 2.0;
                        if (p < 0 | p > 1)
                        {
                            p = 0.025;
                        }
                        int nx = rows;
                        int ix;
                        int fault;
                        double pu;
                        MathDbl.taufromp(p, out pu, out ix, ref nx, out fault);
                        double[] pws = new double[cnt + 1 ];
                        if (fault == 0)
                        {
                            cnt = 0;
                            for (i = 1; i <= rows - 1; i++)
                            {
                                for (j = i + 1; j <= rows; j++)
                                {
                                    if (x[i] != x[j])
                                    {
                                        cnt = cnt + 1;
                                        if (x[i] != Constant.MISSING && y[j] != Constant.MISSING)
                                        {
                                            pws[cnt] = (y[i] - y[j]) / (x[i] - x[j]);
                                        }
                                    }
                                }
                            }
                            Array.Sort(pws, 1, cnt);
                            int ri = ((int)(Math.Floor(0.5 * Convert.ToDouble(cnt - ix))));
                            int si = Convert.ToInt32(0.5 * Convert.ToDouble(cnt + ix));
                            double imdn = 0.5 * Convert.ToDouble(cnt + 1);
                            if (imdn < 1.0)
                            {
                                imdn = 1.0;
                            }
                            if (imdn > cnt)
                            {
                                imdn = cnt;
                            }
                            if (imdn - Math.Floor(imdn) == 0.0)
                            {
                                mdn = pws[Convert.ToInt32(imdn)];
                            }
                            if (imdn - Math.Floor(imdn) != 0.0)
                            {
                                mdn = pws[((int)(Math.Floor(imdn)))] + (pws[Convert.ToInt32(Math.Floor(imdn) + 1.0)] - pws[((int)(Math.Floor(imdn)))]) * (imdn - Math.Floor(imdn));
                            }
                            double lci = pws[ri];
                            double uci = pws[si];
                            t = t + " [Median slope (" + Formatting.XRound(gamma * 100, 2) + "% CI)= " + host.RoundU(mdn) + " (" + host.RoundU(lci) + " to " + host.RoundU(uci) + ")]";
                            outputVariable.Title = t;
                        }
                        else
                        {
                            host.Error("TODO: Error description", "Pairwise");
                        }
                    }
                    break;
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }

        public static ParameterBag ShtRndBeta(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndBeta(host, rows, cols, a, b, seed));
        }

        public static ParameterBag ShtRndBinomial(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            int nn = parameters["nn"].AsInt32;
            double p = parameters["p"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndBino(host, rows, cols, nn, p, seed));
        }

        public static ParameterBag ShtRndCauchy(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double l = parameters["l"].AsDouble;
            double s = parameters["s"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndCauchy(host, rows, cols, l, s, seed));
        }


        public static ParameterBag ShtRndChiSquare(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double df = parameters["df"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndChi(host, rows, cols, df, seed));
        }


        public static ParameterBag ShtRndExponential(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double xm = parameters["xm"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndExpo(host, rows, cols, xm, seed));
        }


        public static ParameterBag ShtRndF(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double dfn = parameters["dfn"].AsDouble;
            double dfd = parameters["dfd"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndF(host, rows, cols, dfn, dfd, seed));
        }


        public static ParameterBag ShtRndGamma(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndGamma(host, rows, cols, a, b, seed));
        }


        public static ParameterBag ShtRndGeometric(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double p = parameters["p"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndGeom(host, rows, cols, p, seed));
        }


        public static ParameterBag ShtRndLogit(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double mu = parameters["mu"].AsDouble;
            double sigma = parameters["sigma"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndLogit(host, rows, cols, mu, sigma, seed));
        }


        public static ParameterBag ShtRndLogNormal(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double xm = parameters["xm"].AsDouble;
            double sd = parameters["sd"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndLogNorm(host, rows, cols, xm, sd, seed));
        }


        public static ParameterBag ShtRndNegativeBinomial(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double n = parameters["n"].AsDouble;
            double p = parameters["p"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndNegBin(host, rows, cols, n, p, seed));
        }


        public static ParameterBag ShtRndNormal(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double xm = parameters["xm"].AsDouble;
            double sd = parameters["sd"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndNorm(host, rows, cols, xm, sd, seed));
        }


        public static ParameterBag ShtRndPoisson(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double xm = parameters["xm"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndPoisson(host, rows, cols, xm, seed));
        }


        public static ParameterBag ShtRndT(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double df = parameters["df"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndT(host, rows, cols, df, seed));
        }


        public static ParameterBag ShtRndUniform01(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndUni(host, rows, cols, Constant.MISSING, Constant.MISSING, false, seed));
        }


        public static ParameterBag ShtRndUniformAB(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            bool isCount = "count".Equals(parameters["numberType"].AsString);
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndUni(host, rows, cols, a, b, isCount, seed));
        }


        public static ParameterBag ShtRndWeibull(ITemplateHost host, ParameterBag parameters)
        {
            int cols = parameters["cols"].AsInt32;
            int rows = parameters["rows"].AsInt32;
            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            int seed = parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndWeibull(host, rows, cols, a, b, seed));
        }


        private static ParameterBag WrapFrame(string name, DataFrame frame)
        {
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput(name, frame);
            return outputParameters;
        }


        public static ParameterBag ShtRank(ITemplateHost host, ParameterBag parameters)
        {
            int c; int cx = 0;
            int nx = 0;
            double tie;
            string temp;

            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            int q = Parsing.Cint_Txt(parameters["tie-correction"].AsString);
            int rows = inputVariable.Length;
            double[] prk = new double[rows + 1 ];
            foreach (double value in inputVariable.Data)
            {
                if (value != Constant.MISSING)
                {
                    nx = nx + 1;
                    prk[nx] = value;
                }
            }
            double[] r = new double[nx + 1 ];
            ExFortran.Rank(prk, r, 1, nx, q, out tie);
            const string pre = "Rank: ";
            if (q < 2)
            {
                temp = "";
            }
            else { temp = " [tie correction = " + tie.ToString() + "]"; }
            string title = pre + inputVariable.Title + temp;
            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(rows, title);
            outputFrame.Variables.Add(outputVariable);
            for (c = 1; c <= rows; c++)
            {
                if (inputVariable.Data[c - 1] != Constant.MISSING)
                {
                    cx = cx + 1;
                    outputVariable.SetData(c - 1, r[cx] == Constant.MISSING ? Constant.MISSING : r[cx]);
                }
                else
                {
                    outputVariable.SetData(c - 1, Constant.MISSING);
                }
            }
            return WrapFrame("output", outputFrame);
        }


        ///  <summary>
        ///  Assumes the input is a frame of string variables.  Returns a frame mirrored around x=y.
        ///  </summary>
        ///  <param name="host"></param>
        ///  <param name="parameters"></param>
        ///  <returns></returns>
        public static ParameterBag ShtRotate(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            int cols = data.VariableCount;
            int rows = data.MaxRows;
            DataFrame outputFrame = new DataFrame();
            for (int row = 0; row <= rows - 1; row++)
            {
                StringVariable v = new StringVariable(cols, null); //  Prevent title being emitted on output
                outputFrame.Variables.Add(v);
                for (int col = 0; col <= cols - 1; col++)
                {
                    StringVariable inv = data.Variables[col].AsStringVariable;
                    if (inv.Length > row)
                    {
                        v.SetData(col, inv.Data[row]);
                    }
                }
            }
            return WrapFrame("output", outputFrame);
        }

        private class DoubleAscending : IComparer<double>
        {
            private static int Compare(double x, double y)
            {
                if (x > y)
                {
                    return 1;
                }
                if (x == y)
                {
                    return 0;
                }
                return -1;
            }

            // interface methods implemented by Compare
            int IComparer<double>.Compare(double x, double y)
            {
                return Compare(x, y);
            }

        }


        private class DoubleDescending : IComparer<double>
        {
            private static int Compare(double x, double y)
            {
                if (x < y)
                    return 1;
                return x == y ? 0 : -1;
            }

            // interface methods implemented by Compare
            int IComparer<double>.Compare(double x, double y)
            {
                return Compare(x, y);
            }
        }

        public static ParameterBag ShtSort(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable dataVariable = data.Variables[0].AsDoubleVariable;
            string sort = parameters["sort"].AsString;
            IComparer<double> comp;
            if ("asc".Equals(sort))
            {
                comp = new DoubleAscending();
            }
            else { comp = new DoubleDescending(); }
            bool hasLink = parameters.ContainsKey("linkdata") && parameters["linkdata"] != null;

            int rows = dataVariable.Length;

            string t = "Sort: " + dataVariable.Title;

            double[] dataArray = new double[rows];
            int nx;
            if (hasLink)
            {
                DataFrame linkData = parameters["linkdata"].AsDataFrame;
                DoubleVariable linkVariable = linkData.Variables[0].AsDoubleVariable;
                t += " (by " + linkVariable.Title + ")";
                double[] linkArray = new double[rows];
                nx = 0;
                for (int i = 0; i <= rows - 1; i++)
                {
                    if (dataVariable.Data[i] != Constant.MISSING && linkVariable.Data[i] != Constant.MISSING)
                    {
                        dataArray[nx] = dataVariable.Data[i];
                        linkArray[nx] = linkVariable.Data[i];
                        nx += 1;
                    }
                }
                Array.Sort(linkArray, dataArray, 0, nx, comp);
            }
            else
            {
                nx = 0;
                foreach (double v in dataVariable.Data)
                {
                    if (v != Constant.MISSING)
                    {
                        dataArray[nx] = v;
                        nx += 1;
                    }
                }
                Array.Sort(dataArray, 0, nx, comp);
            }


            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(dataArray, t);
            outputVariable.TruncateDataToLength(nx);
            outputFrame.Variables.Add(outputVariable);
            return WrapFrame("output", outputFrame);
        }

        public static ParameterBag ShtSortByExpression(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            string expression = parameters["expression"].AsString;

            int rows = data.MaxRows;
            int cols = data.VariableCount;

            // Work through the rows
            Calcit clc = new Calcit(expression);

            double[] x = new double[cols];
            SortPair[] sortArray = new SortPair[rows];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                    x[col] = data.Variables[col].AsDoubleVariable.Data[row];
                sortArray[row] = new SortPair(XSpr(clc.Evaluate(x)), row);
            }
            Array.Sort(sortArray);

            DataFrame outputFrame = new DataFrame();
            foreach (Variable v in data.Variables)
                outputFrame.Variables.Add(new DoubleVariable(rows, "Sort(" + expression + "): " + v.Title));
            for (int col = 0; col < cols; col++)
            {
                double[] src = data.Variables[col].AsDoubleVariable.Data;
                double[] target = outputFrame.Variables[col].AsDoubleVariable.Data;
                for (int row = 0; row < rows; row++)
                    target[row] = src[sortArray[row].Row];
            }
            return WrapFrame("output", outputFrame);
        }

        public static ParameterBag ShtSortInPlace(ITemplateHost host, ParameterBag parameters)
        {
            //  A gross hack - this just hands off to the UI.
            host.Amend(new SortInPlaceOptions(), parameters);
            return new ParameterBag();
        }


        public static ParameterBag ShtTransformLog(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 0);
        }


        public static ParameterBag ShtTransformLog10(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 1);
        }


        public static ParameterBag ShtTransformLogit(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 2);
        }


        public static ParameterBag ShtTransformProbit(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 3);
        }


        public static ParameterBag ShtTransformAngular(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 4);
        }


        public static ParameterBag ShtTransformCumulate(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 5);
        }


        public static ParameterBag ShtTransformECDF(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 6);
        }


        public static ParameterBag ShtTransformZsd(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 7);
        }


        public static ParameterBag ShtTransformZecdf(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 8);
        }


        private static ParameterBag ShtTransforms(ParameterBag parameters, int index)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            double[] inputData = inputVariable.Data;
            int rows = inputData.Length;

            double[] a = new double[rows];
            if ((index == 0))
            {
                double cons = Constant.MISSING;
                if (parameters.ContainsKey("c"))
                {
                    cons = parameters["c"].AsDouble;
                }
                bool skipInvalid = cons == Constant.MISSING;
                if (skipInvalid)
                {
                    cons = 0;
                }
                for (int n = 0; n <= rows - 1; n++)
                {
                    if (inputData[n] == Constant.MISSING)
                    {
                        a[n] = Constant.MISSING;
                    }
                    else
                    {
                        double z = inputData[n] + cons;
                        if (z <= 0.0)
                        {
                            a[n] = Constant.MISSING;
                        }
                        else
                        {
                            a[n] = Math.Log(z);
                        }
                    }
                }
                string title;
                if (cons != 0.0)
                {
                    title = "Log(natural): " + cons.ToString() + " + " + inputVariable.Title;
                }
                else
                {
                    title = "Log(natural): " + inputVariable.Title;
                }
                return WrapDoubleVariable(a, title);
            }
            if ((index == 1))
            {
                double cons = Constant.MISSING;
                if (parameters.ContainsKey("c"))
                {
                    cons = parameters["c"].AsDouble;
                }
                bool skipInvalid = cons == Constant.MISSING;
                if (skipInvalid)
                {
                    cons = 0;
                }
                double log10 = Math.Log(10.0);
                for (int n = 0; n <= rows - 1; n++)
                {
                    if (inputData[n] == Constant.MISSING)
                    {
                        a[n] = Constant.MISSING;
                    }
                    else
                    {
                        double z = inputData[n] + cons;
                        if (z <= 0.0)
                        {
                            a[n] = Constant.MISSING;
                        }
                        else
                        {
                            a[n] = Math.Log(z) / log10;
                        }
                    }
                }
                string title;
                if (cons != 0.0)
                {
                    title = "Log(base 10): " + cons.ToString() + " + " + inputVariable.Title;
                }
                else
                {
                    title = "Log(base 10): " + inputVariable.Title;
                }
                return WrapDoubleVariable(a, title);
            }
            if ((index == 2))
            {
                double maxi = XAmanipdp(parameters["discrete"].AsString, inputData);
                for (int n = 0; n <= rows - 1; n++)
                {
                    if (inputData[n] == Constant.MISSING)
                    {
                        a[n] = Constant.MISSING;
                    }
                    else
                    {
                        double prop = Math.Abs(inputData[n] / maxi);
                        if (prop == 1 | prop == 0)
                        {
                            a[n] = Constant.MISSING;
                        }
                        else
                        {
                            if (prop / (1.0 - prop) < 0)
                            {
                                a[n] = Constant.MISSING;
                            }
                            else
                            {
                                a[n] = Math.Log(prop / (1.0 - prop));
                            }
                        }
                    }
                }
                return WrapDoubleVariable(a, "Logit: " + inputVariable.Title);
            }
            if ((index == 3))
            {
                double maxi = XAmanipdp(parameters["discrete"].AsString, inputData);
                for (int n = 0; n <= rows - 1; n++)
                {
                    if (inputData[n] == Constant.MISSING)
                    {
                        a[n] = Constant.MISSING;
                    }
                    else
                    {
                        double prop = Math.Abs(inputData[n] / maxi);
                        if (prop == 1.0 | prop == 0.0)
                        {
                            a[n] = Constant.MISSING;
                        }
                        else
                        {
                            int fault;
                            double zed = PDF.gauinv(prop, out fault);
                            if (fault == 0)
                            {
                                a[n] = 5 + zed;
                            }
                            else { a[n] = Constant.MISSING; }
                        }
                    }
                }
                return WrapDoubleVariable(a, "Probit: " + inputVariable.Title);
            }
            if ((index == 4))
            {
                double maxi = XAmanipdp(parameters["discrete"].AsString, inputData);
                for (int n = 0; n <= rows - 1; n++)
                {
                    if (inputData[n] == Constant.MISSING)
                    {
                        a[n] = Constant.MISSING;
                    }
                    else
                    {
                        double prop = Math.Abs(inputData[n] / maxi);
                        if ((prop == 0.0))
                        {
                            a[n] = 0;
                        }
                        else if ((prop == 1.0))
                        {
                            a[n] = 90;
                        }
                        else if ((prop < 0.0) || (prop > 1.0))
                        {
                            a[n] = Constant.MISSING;
                        }
                        else
                        {
                            double x = Math.Sqrt(prop);
                            a[n] = 57.2957795130824 * (Math.Atan(x / Math.Sqrt(1.0 - x * x)));
                        }
                    }
                }
                return WrapDoubleVariable(a, "Angle: " + inputVariable.Title);
            }
            if ((index == 5))
            {
                for (int n = 0; n <= rows - 1; n++)
                {
                    if (inputData[n] == Constant.MISSING)
                    {
                        a[n] = Constant.MISSING;
                    }
                    else if (n == 0)
                    {
                        a[n] = inputData[n];
                    }
                    else
                    {
                        a[n] = a[n - 1] + inputData[n];
                    }
                }
                return WrapDoubleVariable(a, "Cumulate: " + inputVariable.Title);
            }
            if ((index == 6))
            {
                double[] fn = new double[inputData.Length];
                int err;
                MathDbl.ecdf(inputData, fn, out err);
                if (err == 0)
                {
                    return WrapDoubleVariable(fn, "ECDF: " + inputVariable.Title);
                }
                throw new ArgumentException("Insufficient data");
            }
            if ((index == 7))
            {
                double[] fn = new double[inputData.Length];
                int err;
                MathDbl.zscore(inputData, ref fn, false, out err);
                if (err == 0)
                {
                    return WrapDoubleVariable(fn, "Z: " + inputVariable.Title);
                }
                throw new ArgumentException("Insufficient data");
            }
            if ((index == 8))
            {
                double[] fn = new double[inputData.Length];
                int err;
                MathDbl.zscore(inputData, ref fn, true, out err);
                if (err == 0)
                {
                    return WrapDoubleVariable(fn, "Z score (ECDF): " + inputVariable.Title);
                }
                throw new ArgumentException("Insufficient data");
            }
            throw new ArgumentException("Unknown index");
        }


        private static double XAmanipdp(string discrete, double[] data)
        {
            if ("discrete".Equals(discrete))
            {
                bool allMissing = true;
                double maxi = double.MinValue;
                foreach (double v in data)
                {
                    if (v != Constant.MISSING)
                    {
                        allMissing = false;
                        if (Math.Abs(v) > maxi)
                        {
                            maxi = Math.Abs(v);
                        }
                    }
                }
                return allMissing ? Constant.MISSING : maxi;
            }
            return 1.0;
        }


        private static ParameterBag WrapDoubleVariable(double[] a, string title)
        {
            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(a, title);
            outputFrame.Variables.Add(outputVariable);
            return WrapFrame("output", outputFrame);
        }


        public static ParameterBag ShtGroupCategorise(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            int rows = inputVariable.Length;
            CategoriseOptions options = new CategoriseOptions
                                            {
                                                Title = "Categorised: " + inputVariable.Title,
                                                PassX = new double[rows],
                                                Data = inputVariable
                                            };
            if (null == host.Amend(options, parameters))
            {
                throw new TemplateOperationCancelledException();
            }
            DataFrame outputFrame = new DataFrame();
            if ((options.PassX != null) && options.PassX.Length > 0)
            {
                string lab = options.Title;
                DoubleVariable boundariesVariable = new DoubleVariable(options.PassX, lab);
                outputFrame.Variables.Add(boundariesVariable);
            }
            if (options.Categories != null)
            {
                StringVariable categoryVariable = new StringVariable(options.Categories, "category");
                outputFrame.Variables.Add(categoryVariable);
                DoubleVariable countVariable = new DoubleVariable(options.Counts.Length, "count");
                outputFrame.Variables.Add(countVariable);
                int r;
                for (r = 0; r <= options.Counts.Length - 1; r++)
                {
                    countVariable.SetData(r, options.Counts[r]);
                }
            }
            return WrapFrame("output", outputFrame);
        }


        public static ParameterBag ShtGroupExtract(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            bool isFindAndReplace = data.VariableCount > 1;

            if (isFindAndReplace)
            {
                ExtractionOptions options = new ExtractionOptions { DataFrame = data };
                ParameterBag outputParameters = host.Amend(options, parameters);
                return outputParameters;
            }
            else
            {
                DoubleVariable dataVariable = data.Variables[0].AsDoubleVariable;
                string dtitle = dataVariable.Title;

                DataFrame identifiersFrame = parameters["identifiers"].AsDataFrame;

                int cols = identifiersFrame.VariableCount;
                string l = "";
                /* #537: Always use X1, X2 etc.
                if (cols == 1)
                {
                    l += "X: " + identifiersFrame.Variables[0].Title + "\r\n";
                }
                else
                 */
                {
                    int j;
                    for (j = 1; j <= cols; j++)
                    {
                        l += "X" + j + ": " + identifiersFrame.Variables[j - 1].Title + "\r\n";
                    }
                }

                ExtractionOptions options = new ExtractionOptions
                {
                    Title = dtitle,
                    IdentifierNames = l,
                    DataFrame = data,
                    IdentifiersFrame = identifiersFrame
                };
                ParameterBag outputParameters = host.Amend(options, parameters);
                return outputParameters;
            }
        }

        public static ParameterBag ConvertUnits(ITemplateHost host, ParameterBag parameters)
        {
            ConvertUnitsOptions convertUnitsOptions = new ConvertUnitsOptions();
            ParameterBag outputParameters = host.Amend(convertUnitsOptions, parameters);
            return outputParameters;
        }

        internal static ParameterBag ShtToggleFilters(ITemplateHost host, ParameterBag parameters)
        {
            //  A gross hack - this just hands off to the UI.
            host.Amend(new ToggleFiltersOptions(), parameters);
            return new ParameterBag();
        }

        internal static ParameterBag ShtContract(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame observationsFrame = parameters["observations"].AsDataFrame;
            ClassifierVariable observationsVariable = observationsFrame.Variables[0].AsClassifierVariable;
            IList<Group> groups = observationsVariable.Groups;
            DataFrame covariatesOrNull;

            string[] outputValues = new string[groups.Count];
            double[] outputFrequencies = new double[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                outputValues[i] = groups[i].Label;
                outputFrequencies[i] = groups[i].NBin;
            }
            string valuesTitle = observationsVariable.Title;
            if (valuesTitle.EndsWith("_Individual"))
                valuesTitle = valuesTitle.Replace("_Individual", "");
            else
                valuesTitle += "_Grouped";
            string frequenciesTitle = observationsVariable.Title;
            if (frequenciesTitle.EndsWith("_Individual"))
                frequenciesTitle = frequenciesTitle.Replace("_Individual", "");
            // frequenciesTitle += "_Counts"; Removed as part of #958
            StringVariable valuesVariable = new StringVariable(outputValues, valuesTitle);
            DoubleVariable frequenciesVariable = new DoubleVariable(outputFrequencies, frequenciesTitle);
            DataFrame outputFrame = new DataFrame();
            outputFrame.Variables.Add(valuesVariable);
            outputFrame.Variables.Add(frequenciesVariable);
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }

        internal static ParameterBag ShtExpand(ITemplateHost host, ParameterBag parameters)
        {
            int MAXROWS = 1000000; // Maximum number of output rows we're willing to tolerate.  TODO: Should really acquire this from the host.
            string mode = parameters["mode"].AsString;
            DataFrame covariatesOrNull;
            StringVariable labelsOrNull;
            int totalOutputRows;
            int[] yesPerGroup;
            int[] noPerGroup;
            bool hasResponses;
            switch (mode)
            {
                case "categories-counts":
                    {
                        DoubleVariable countsVariable = parameters["counts"].AsDataFrame.Variables[0].AsDoubleVariable;
                        yesPerGroup = ToIntArray(countsVariable.Data, out totalOutputRows);
                        noPerGroup = new int[yesPerGroup.Length]; // Initialised to 0
                        labelsOrNull = parameters["categories"].AsDataFrame.Variables[0].AsStringVariable;
                        covariatesOrNull = null;
                        hasResponses = false;
                    }
                    break;
                case "categories-counts-covariates":
                    {
                        DoubleVariable countsVariable = parameters["counts"].AsDataFrame.Variables[0].AsDoubleVariable;
                        yesPerGroup = ToIntArray(countsVariable.Data, out totalOutputRows);
                        noPerGroup = new int[yesPerGroup.Length]; // Initialised to 0
                        labelsOrNull = parameters["categories"].AsDataFrame.Variables[0].AsStringVariable;
                        covariatesOrNull = parameters["covariates"].AsDataFrame;
                        hasResponses = false;
                    }
                    break;
                case "responders-nonresps-covariates":
                    {
                        DoubleVariable respondersVariable = parameters["responders"].AsDataFrame.Variables[0].AsDoubleVariable;
                        int yesses;
                        yesPerGroup = ToIntArray(respondersVariable.Data, out yesses);
                        DoubleVariable nonrespsVariable = parameters["nonresps"].AsDataFrame.Variables[0].AsDoubleVariable;
                        int noes;
                        noPerGroup = ToIntArray(nonrespsVariable.Data, out noes);
                        totalOutputRows = yesses + noes;
                        labelsOrNull = null;
                        covariatesOrNull = parameters["covariates"].AsDataFrame;
                        hasResponses = true;
                    }
                    break;
                case "responders-totals-covariates":
                    {
                        DoubleVariable respondersVariable = parameters["responders"].AsDataFrame.Variables[0].AsDoubleVariable;
                        int scrap;
                        yesPerGroup = ToIntArray(respondersVariable.Data, out scrap);
                        DoubleVariable totalsVariable = parameters["totals"].AsDataFrame.Variables[0].AsDoubleVariable;
                        int[] totalsPerGroup = ToIntArray(totalsVariable.Data, out totalOutputRows);
                        noPerGroup = new int[yesPerGroup.Length];
                        for (int i = 0; i < yesPerGroup.Length; i++)
                            noPerGroup[i] = totalsPerGroup[i] - yesPerGroup[i];
                        labelsOrNull = null;
                        covariatesOrNull = parameters["covariates"].AsDataFrame;
                        hasResponses = true;
                    }
                    break;
                case "resprops-totals-covariates":
                    {
                        DoubleVariable respropsVariable = parameters["resprops"].AsDataFrame.Variables[0].AsDoubleVariable;
                        DoubleVariable totalsVariable = parameters["totals"].AsDataFrame.Variables[0].AsDoubleVariable;
                        int[] totalsPerGroup = ToIntArray(totalsVariable.Data, out totalOutputRows);
                        yesPerGroup = new int[totalsVariable.Length];
                        noPerGroup = new int[yesPerGroup.Length];
                        for (int i = 0; i < yesPerGroup.Length; i++)
                        {
                            yesPerGroup[i] = (int)Math.Round(totalsPerGroup[i] * respropsVariable.Data[i], MidpointRounding.AwayFromZero);
                            noPerGroup[i] = totalsPerGroup[i] - yesPerGroup[i];
                        }
                        labelsOrNull = null;
                        covariatesOrNull = parameters["covariates"].AsDataFrame;
                        hasResponses = true;
                    }
                    break;
                default:
                    throw new Exception("Unknown mode '" + mode + "' when trying to expand data");
            }
            if (totalOutputRows > MAXROWS)
            {
                host.Error("The output would require " + totalOutputRows + " rows, which will not fit into the spreadsheet", "Expand");
                throw new TemplateOperationCancelledException();
            }
            bool hasLabels = null != labelsOrNull;
            int covariatesCount = null == covariatesOrNull ? 0 : covariatesOrNull.VariableCount;

            DataFrame outputFrame = new DataFrame();
            // Add label variable if we have it
            StringVariable outputLabels;
            if (hasLabels)
            {
                string title = labelsOrNull.Title;
                if (title.EndsWith("_Grouped"))
                    title = title.Replace("_Grouped", "");
                else
                    title += "_Individual";
                outputLabels = new StringVariable(totalOutputRows, title);
                outputFrame.Variables.Add(outputLabels);
            }
            else
                outputLabels = null;

            // Add responses variable if we have it
            DoubleVariable outputResponses;
            if (hasResponses)
            {
                outputResponses = new DoubleVariable(totalOutputRows, "Response");
                outputFrame.Variables.Add(outputResponses);
            }
            else
                outputResponses = null;

            // Add covariates if we have them
            VariantVariable[] outputCovariates = new VariantVariable[covariatesCount];
            for (int i = 0; i < covariatesCount; i++)
            {
                VariantVariable v = new VariantVariable(totalOutputRows, covariatesOrNull.Variables[i].Title);
                outputFrame.Variables.Add(v);
                outputCovariates[i] = v;
            }
            int nextOutputOffset = 0;
            for (int srcRow = 0; srcRow < yesPerGroup.Length; srcRow++)
            {
                if (yesPerGroup[srcRow] > 0)
                {
                    nextOutputOffset = FillExtractOutputRow(covariatesOrNull, labelsOrNull, yesPerGroup[srcRow], 1, hasResponses, hasLabels, covariatesCount, outputLabels, outputResponses, outputCovariates, nextOutputOffset, srcRow);
                    nextOutputOffset = FillExtractOutputRow(covariatesOrNull, labelsOrNull, noPerGroup[srcRow], 0, hasResponses, hasLabels, covariatesCount, outputLabels, outputResponses, outputCovariates, nextOutputOffset, srcRow);
                }

            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return outputParameters;
        }

        private static int FillExtractOutputRow(DataFrame covariatesOrNull, StringVariable labelsOrNull, int copies, int response, bool hasResponses, bool hasLabels, int covariatesCount, StringVariable outputLabels, DoubleVariable outputResponses, VariantVariable[] outputCovariates, int nextOutputOffset, int srcRow)
        {
            for (int copy = 0; copy < copies; copy++)
            {
                if (hasLabels)
                    outputLabels.Data[nextOutputOffset] = labelsOrNull.Data[srcRow];
                if (hasResponses)
                    outputResponses.Data[nextOutputOffset] = response;
                for (int covariate = 0; covariate < covariatesCount; covariate++)
                    outputCovariates[covariate].Data[nextOutputOffset] = covariatesOrNull.Variables[covariate].AsVariantVariable.Data[srcRow];
                nextOutputOffset++;
            }
            return nextOutputOffset;
        }

        private static int[] ToIntArray(double[] p, out int sum)
        {
            int total = 0;
            int[] output = new int[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                int rounded = (int)Math.Round(p[i], MidpointRounding.AwayFromZero);
                total += rounded;
                output[i] = rounded;
            }
            sum = total;
            return output;
        }
    }
}

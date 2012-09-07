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
    public class DummyOptions : IFillable
    {
        public string MaxCatTi;
        public List<string> Names;

        ///  <summary>
        ///  The name that the user selected, or Nothing if no &lt;none> was selected.
        ///  </summary>
        public int JDrop;

        public string FillerToUse
        {
            get
            {
                return "Dummy";
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


    public class CategoriseOptions : IFillable
    {
        public string Title;
        public double[] PASSX;
        public string[] Categories;
        public int[] Counts;
        public DoubleVariable Data;

        public string FillerToUse
        {
            get
            {
                return "Categorise";
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


    public class ExtractionOptions : IFillable
    {
        public string Title;
        public double[] PASSX;
        public DoubleVariable Data;
        public DataFrame IdentifiersFrame;
        public string IdentifierNames;

        public string FillerToUse
        {
            get
            {
                return "Extraction";
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


    public class SortInPlaceOptions : IFillable
    {
        public string FillerToUse
        {
            get
            {
                return "SortInPlace";
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


    public class Sheet
    {
        private struct Catvar : IComparable<Catvar>
        {
            public string Ti;
            public int I;

            private int CompareTo(Catvar other)
            {
                double mti, oti;
                if (double.TryParse(Ti, out mti) && double.TryParse(other.Ti, out oti))
                {
                    if (mti < oti)
                    {
                        return -1;
                    }
                    return mti == oti ? 0 : 1;
                }
                return String.CompareOrdinal(Ti, other.Ti);
            }
            // interface methods implemented by CompareTo
            int IComparable<Catvar>.CompareTo(Catvar other)
            {
                return CompareTo(other);
            }

        }

        private class SortPair : IComparable<SortPair>
        {
            public readonly double Value;
            public readonly int Row;

            private int CompareTo(SortPair other)
            {
                return Value.CompareTo(other.Value);
            }

            int IComparable<SortPair>.CompareTo(SortPair other)
            {
                return CompareTo(other);
            }

            public SortPair(double value, int row)
            {
                Value = value;
                Row = row;
            }
        }

        public static StepResult ShtFillSeries(ITemplateHost host, ParameterBag parameters)
        {
            int rows = parameters["rows"].AsInt32;
            if (rows < 1)
            {
                rows = 100;
            }
            if (rows > 1000000)
            {
                rows = 1000000;
            }
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
                v.set_Data(i, currentval);
                x[0] = currentval;
                currentval = c.Evaluate(x);
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        ///  <summary>
        ///  Returns a constant to add to all input values to ensure that all f(x) with the given input data are valid.
        ///  </summary>
        public static void XConstant(int rows, int lowerBound, double[] Data, out double minimumC, out double suggestedC)
        {
            //  Suggest constant to make all fn(x) possible
            double a_min = double.MaxValue;
            double a_max = double.MinValue;
            for (int n = lowerBound; n <= rows + lowerBound - 1; n++)
            {
                if (Data[n] != Constant.MISSING)
                {
                    if (Data[n] < a_min)
                    {
                        a_min = Data[n];
                    }
                    if (Data[n] > a_max)
                    {
                        a_max = Data[n];
                    }
                }
            }
            if (a_min < 0)
            {
                double zmin = 0; double zstep = 0;
                Charting.AxisScaler.Axis(ref a_min, ref a_max, 20, ref zmin, ref zstep);
                minimumC = Math.Abs(a_min);
                suggestedC = Math.Abs(zmin);
            }
            else
            {
                minimumC = Constant.MISSING;
                suggestedC = Constant.MISSING;
            }
        }


        public static StepResult ShtClearMissing(ITemplateHost host, ParameterBag parameters)
        {
            int r;
            int ctr;
            string userText;

            DataFrame data = parameters["data"].AsDataFrame;
            int totrows = data.MaxRows;
            int totcols = data.VariableCount;
            string[,] hold = new string[totrows + 1 /* for VB to C# conversion */, totcols + 1 /* for VB to C# conversion */];

            string clearRowString = parameters["row-or-cell"].AsString;
            bool clearRow = totcols > 1 && "row".Equals(clearRowString);
            double userNumber = parameters.ContainsKey("missing-double") ? parameters["missing-double"].AsDouble : Constant.MISSING;
            userText = parameters.ContainsKey("missing-text") && parameters["missing-text"].AsString.Trim().Length > 0
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
            for (int C = 0; C <= totcols - 1; C++)
            {
                string outputName = data.Variables[C].AsStringVariable.Title;
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
                        for (int C = 0; C <= totcols - 1; C++)
                        {
                            if (ctr > maxctr)
                            {
                                maxctr = ctr;
                            }
                            outputFrame.Variables[C].AsStringVariable.set_Data(ctr - 1, hold[r, C]);
                        }
                    }
                }
                for (int C = 0; C <= totcols - 1; C++)
                {
                    outputFrame.Variables[C].EnsureLength(ctr);
                }
            }
            else
            {
                for (int C = 0; C <= totcols - 1; C++)
                {
                    ctr = 0;
                    for (r = 1; r <= totrows; r++)
                    {
                        string transTemp2 = hold[r, C];
                        if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp2.Length > 0)
                        {
                            ctr = ctr + 1;
                            if (ctr > maxctr)
                            {
                                maxctr = ctr;
                            }
                            outputFrame.Variables[C].AsStringVariable.set_Data(ctr - 1, hold[r, C]);
                        }
                    }
                    outputFrame.Variables[C].EnsureLength(ctr);
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
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



        public static StepResult ShtDummyVariables(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            ClassifierVariable categoryVariable = data.Variables[0].AsClassifierVariable;
            int rows = categoryVariable.Length;
            double[] gid = new double[rows + 1 /* for VB to C# conversion */ ];
            for (int c = 1; c <= rows; c++)
            {
                gid[c] = categoryVariable.Data[c - 1];
            }
            int cats = categoryVariable.GroupCount;

            // find missing data category
            int mc = -1;
            for (int i = 0; i <= cats - 1; i++)
            {
                if (categoryVariable.Groups[i].Label == Formatting.MISSINGLABEL)
                {
                    mc = i;
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }

            // find the most prevalent category
            int maxcat = 0;
            int maxcatidx = 0;
            for (int i = 0; i <= cats - 1; i++)
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
            double[] g = new double[cats - 1 + 1 /* for VB to C# conversion */ ];
            Catvar[] gcat = new Catvar[cats - 1 + 1 /* for VB to C# conversion */ ];
            for (int j = 1; j <= rows; j++)
            {
                if (gid[j] != mc & gid[j] != Constant.MISSING)
                {
                    g[0] = gid[j];
                    gcat[0].Ti = categoryVariable.Groups[Convert.ToInt32(gid[j])].Label;
                    gcat[0].I = Convert.ToInt32(gid[j]);
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }
            for (int j = 2; j <= rows; j++)
            {
                bool newa = true;
                for (int i = 0; i <= ng - 1; i++)
                {
                    if (gid[j] == g[i] | gid[j] == mc | gid[j] == Constant.MISSING)
                    {
                        newa = false;
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                if (newa)
                {
                    ng = ng + 1;
                    g[ng - 1] = gid[j];
                    gcat[ng - 1].Ti = categoryVariable.Groups[Convert.ToInt32(gid[j])].Label;
                    gcat[ng - 1].I = Convert.ToInt32(gid[j]);
                }
            }

            int dummies = ng - 1;
            if (dummies < 1)
            {
                host.Error("You must have more than one category in your data", "Dummy Variables");
                return null;
            }

            // sort categories bt label to be consistent with Stata xi etc.
            Array.Sort(gcat, 0, ng);

            DummyOptions dm = new DummyOptions { MaxCatTi = maxcatti, Names = new List<string>() };
            for (int j = 0; j <= ng - 1; j++)
                dm.Names.Add(gcat[j].Ti);
            bool wasOk = host.Amend(dm, parameters);
            if (!(wasOk))
            {
                throw new TemplateOperationCancelledException();
            }

            int ctr = 0;
            DataFrame outputFrame = new DataFrame();
            for (int j = 0; j <= ng - 1; j++)
            {
                if (j != dm.JDrop)
                {
                    ctr = ctr + 1;
                    string ti = categoryVariable.Title + "(" + gcat[j].Ti + ")";
                    DoubleVariable outputVariable = new DoubleVariable(rows, ti);
                    outputFrame.Variables.Add(outputVariable);
                }
            }
            for (int r = 1; r <= rows; r++)
            {
                ctr = 0;
                for (int j = 0; j <= ng - 1; j++)
                {
                    if (j != dm.JDrop)
                    {
                        ctr = ctr + 1;
                        int dv = gid[r] == gcat[j].I ? 1 : 0;
                        // only enter if not missing category mc
                        if (gid[r] != mc && gid[r] != Constant.MISSING)
                        {
                            outputFrame.Variables[ctr - 1].AsDoubleVariable.set_Data(r - 1, dv);
                        }
                    }
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult ShtLadderPowers(ITemplateHost host, ParameterBag parameters)
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

            for (int N = 0; N <= inputVariable.Length - 1; N++)
            {
                if (inputVariable.Data[N] == Constant.MISSING)
                {
                    minusTwoVariable.set_Data(N, Constant.MISSING);
                    minusOneVariable.set_Data(N, Constant.MISSING);
                    minusHalfVariable.set_Data(N, Constant.MISSING);
                    logVariable.set_Data(N, Constant.MISSING);
                    halfVariable.set_Data(N, Constant.MISSING);
                    squaredVariable.set_Data(N, Constant.MISSING);
                }
                else
                {
                    double z = inputVariable.Data[N] + cons;
                    // -2
                    if (z == 0)
                    {
                        minusTwoVariable.set_Data(N, Constant.MISSING);
                    }
                    else
                    {
                        try
                        {
                            minusTwoVariable.set_Data(N, Math.Pow(z, -2.0));
                        }
                        catch (Exception)
                        {
                            minusTwoVariable.Data[N] = Constant.MISSING;
                        }
                    }
                    // -1
                    minusOneVariable.set_Data(N, z == 0 ? Constant.MISSING : Math.Pow(z, -1.0));
                    // -0.5
                    if (z <= 0)
                    {
                        minusHalfVariable.set_Data(N, Constant.MISSING);
                    }
                    else
                    {
                        try
                        {
                            minusHalfVariable.set_Data(N, Math.Pow(z, -0.5));
                        }
                        catch (Exception)
                        {
                            minusHalfVariable.Data[N] = Constant.MISSING;
                        }
                    }
                    // log
                    logVariable.set_Data(N, z <= 0 ? Constant.MISSING : Math.Log(z));
                    // 0.5
                    if (z < 0)
                    {
                        halfVariable.set_Data(N, Constant.MISSING);
                    }
                    else
                    {
                        try
                        {
                            halfVariable.set_Data(N, Math.Pow(z, 0.5));
                        }
                        catch (Exception)
                        {
                            halfVariable.set_Data(N, Constant.MISSING);
                        }
                    }
                    // 2
                    try
                    {
                        squaredVariable.set_Data(N, Math.Pow(z, 2.0));
                    }
                    catch (Exception)
                    {
                        squaredVariable.set_Data(N, Constant.MISSING);
                    }
                }
            }
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult ShtStandardize(ITemplateHost host, ParameterBag parameters)
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

                        outputVariable.set_Data(c, tr);
                    }
                    else
                    {
                        outputVariable.set_Data(c, Constant.MISSING);
                    }
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static double XSpr(double q)
        {
            double q1 = Math.Abs(q) > Constant.LMREAL ? Constant.MISSING : q;
            return double.IsNaN(q) ? Constant.MISSING : q1;
        }


        public static StepResult ShtCombine(ITemplateHost host, ParameterBag parameters)
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
            for (int C = 0; C <= data.VariableCount - 1; C++)
            {
                DoubleVariable v = data.Variables[C].AsDoubleVariable;
                ep = v.Title.IndexOf("=", StringComparison.Ordinal) + 1;
                string outputTitle = v.Title;
                if (ok)
                {
                    groupVariable.set_Data(row, v.Title.Substring(v.Title.Length - v.Title.Length - ep));
                }
                foreach (double value in v.Data)
                {
                    groupVariable.set_Data(row, outputTitle);
                    dataVariable.set_Data(row, value);
                    row += 1;
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult ShtDates(ITemplateHost host, ParameterBag parameters)
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
                    outputVariable.set_Data(i, Constant.MISSING);
                }
                else
                {
                    outputVariable.Data[i] = DateAndTime.DateDiff(interval, indate, inputVariable.Data[i]);
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult shtGroupSplit(ITemplateHost Host, ParameterBag Parameters)
        {
            DataFrame gidsFrame = Parameters["gids"].AsDataFrame;
            ClassifierVariable gidsVariable = gidsFrame.Variables[0].AsClassifierVariable;
            int rows = gidsVariable.Length;
            int ng = gidsVariable.GroupCount;
            double[] gid = new double[rows + 1 /* for VB to C# conversion */ ];
            string[] glabel = new string[ng + 1 /* for VB to C# conversion */ ];
            double[] g = new double[ng + 1 /* for VB to C# conversion */ ];
            for (int C = 1; C <= ng; C++)
            {
                if (gidsVariable.Title == "Group ID")
                {
                    glabel[C] = gidsVariable.get_Group(C - 1).Label;
                }
                else
                {
                    glabel[C] = gidsVariable.Title + "=" + gidsVariable.get_Group(C - 1).Label;
                }
                if (gidsVariable.get_Group(C - 1).Label == Formatting.MISSINGLABEL)
                {
                    g[C] = Constant.MISSING;
                }
                else { g[C] = Convert.ToDouble(C) - 1; }
            }
            for (int C = 1; C <= rows; C++)
            {
                gid[C] = gidsVariable.Data[C - 1];
            }

            DataFrame data = Parameters["data"].AsDataFrame;
            int cols = data.VariableCount;
            double[,] x = new double[cols + 1 /* for VB to C# conversion */, rows + 1 /* for VB to C# conversion */];
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
                for (int C = 1; C <= cols; C++)
                {
                    string variableName;
                    if (data.Variables[C - 1].Title == "Data")
                    {
                        variableName = glabel[j];
                    }
                    else
                    {
                        variableName = data.Variables[C - 1].Title + "~" + glabel[j];
                    }
                    DoubleVariable v = new DoubleVariable(0, variableName); //  TODO: Efficiency: This will reallocate the data array many times.  Can we be more sensible?
                    outputFrame.Variables.Add(v);
                }
                int rw = 0;
                for (int r = 1; r <= rows; r++)
                {
                    if (gid[r] == g[j])
                    {
                        for (int C = 1; C <= cols; C++)
                        {
                            outputFrame.Variables[lc + C - 1].AsDoubleVariable.set_Data(rw, x[C, r]);
                        }
                        rw += 1;
                    }
                }
                lc += cols;
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("ng", ng);
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult ShtNormal(ITemplateHost host, ParameterBag parameters)
        {
            int n;
            int method; int nx = 0;
            int cx = 0; int c;
            double den;

            string Lab = parameters["method"].AsString;
            if ("vdW".Equals(Lab))
            {
                method = 1;
            }
            else if ("Blom".Equals(Lab))
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
            double[] prk = new double[rows + 1 /* for VB to C# conversion */ ];
            for (n = 1; n <= rows; n++)
            {
                if (dataVariable.Data[n - 1] != Constant.MISSING)
                {
                    nx = nx + 1;
                    prk[nx] = dataVariable.Data[n - 1];
                }
            }
            double[] r = new double[nx + 1 /* for VB to C# conversion */ ];
            double xf;
            ExFortran.Rank(prk, r, 1, nx, 0, out xf);
            if (method == 3)
            {
                if (rows > 2500)
                {
                    method = 1;
                    Lab = "vdW";
                }
            }
            string pre = "Nml Score (" + Lab + "): ";
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

                    outputVariable.set_Data(c - 1, tr == Constant.MISSING ? Constant.MISSING : tr);
                }
                else
                {
                    outputVariable.set_Data(c - 1, Constant.MISSING);
                }
            }

            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("output", outputFrame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult ShtPairDifferences(ITemplateHost host, ParameterBag parameters)
        {
            return ShtPair(host, parameters, 1);
        }


        public static StepResult ShtPairMeans(ITemplateHost host, ParameterBag parameters)
        {
            return ShtPair(host, parameters, 2);
        }


        public static StepResult ShtPairSlopes(ITemplateHost host, ParameterBag parameters)
        {
            return ShtPair(host, parameters, 3);
        }


        ///  <param name="parameters"></param>
        /// <param name="index">1 = differences, 2 = means, 3 = slopes</param>
        /// <param name="host"></param>
        private static StepResult ShtPair(ITemplateHost host, ParameterBag parameters, int index)
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
            double[] yy = new double[rows + 1 /* for VB to C# conversion */ ];
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
                xx = new double[rows2 + 1 /* for VB to C# conversion */ ];
                for (i = 1; i <= rows2; i++)
                {
                    xx[i] = xVariable.Data[i - 1];
                }
            }

            if (index == 2)
            {
                // means
                x = new double[rows + 1 /* for VB to C# conversion */ ];
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
                x = new double[rows + 1 /* for VB to C# conversion */ ];
                y = new double[rows + 1 /* for VB to C# conversion */ ];
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
                x = new double[rows + 1 /* for VB to C# conversion */ ];
                y = new double[rows2 + 1 /* for VB to C# conversion */ ];
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
                                outputVariable.set_Data(cnt, Constant.MISSING);
                            }
                            else
                            {
                                outputVariable.set_Data(cnt, x[i] - y[j]);
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
                                outputVariable.set_Data(cnt, Constant.MISSING);
                            }
                            else
                            {
                                outputVariable.set_Data(cnt, (x[i] + x[j]) / 2.0);
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
                                    outputVariable.set_Data(cnt, Constant.MISSING);
                                }
                                else
                                {
                                    outputVariable.set_Data(cnt, (y[i] - y[j]) / (x[i] - x[j]));
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
                        double[] pws = new double[cnt + 1 /* for VB to C# conversion */ ];
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
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult shtRndBeta(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double a = Parameters["a"].AsDouble;
            double b = Parameters["b"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndBeta(Host, rows, cols, a, b, Seed));
        }

        public static StepResult shtRndBinomial(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            int nn = Parameters["nn"].AsInt32;
            double P = Parameters["p"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndBino(Host, rows, cols, nn, P, Seed));
        }

        public static StepResult shtRndCauchy(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double l = Parameters["l"].AsDouble;
            double s = Parameters["s"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndCauchy(Host, rows, cols, l, s, Seed));
        }


        public static StepResult shtRndChiSquare(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double df = Parameters["df"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndChi(Host, rows, cols, df, Seed));
        }


        public static StepResult shtRndExponential(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double xm = Parameters["xm"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndExpo(Host, rows, cols, xm, Seed));
        }


        public static StepResult shtRndF(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double dfn = Parameters["dfn"].AsDouble;
            double dfd = Parameters["dfd"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndF(Host, rows, cols, dfn, dfd, Seed));
        }


        public static StepResult shtRndGamma(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double a = Parameters["a"].AsDouble;
            double b = Parameters["b"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndGamma(Host, rows, cols, a, b, Seed));
        }


        public static StepResult shtRndGeometric(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double p = Parameters["p"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndGeom(Host, rows, cols, p, Seed));
        }


        public static StepResult shtRndLogit(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double mu = Parameters["mu"].AsDouble;
            double sigma = Parameters["sigma"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndLogit(Host, rows, cols, mu, sigma, Seed));
        }


        public static StepResult shtRndLogNormal(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double xm = Parameters["xm"].AsDouble;
            double sd = Parameters["sd"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndLogNorm(Host, rows, cols, xm, sd, Seed));
        }


        public static StepResult shtRndNegativeBinomial(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double n = Parameters["n"].AsDouble;
            double p = Parameters["p"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndNegBin(Host, rows, cols, n, p, Seed));
        }


        public static StepResult shtRndNormal(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double xm = Parameters["xm"].AsDouble;
            double sd = Parameters["sd"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndNorm(Host, rows, cols, xm, sd, Seed));
        }


        public static StepResult shtRndPoisson(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double xm = Parameters["xm"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndPoisson(Host, rows, cols, xm, Seed));
        }


        public static StepResult shtRndT(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double df = Parameters["df"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndT(Host, rows, cols, df, Seed));
        }


        public static StepResult shtRndUniform01(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndUni(Host, rows, cols, Constant.MISSING, Constant.MISSING, false, Seed));
        }


        public static StepResult shtRndUniformAB(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double a = Parameters["a"].AsDouble;
            double b = Parameters["b"].AsDouble;
            bool isCount = "count".Equals(Parameters["numberType"].AsString);
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndUni(Host, rows, cols, a, b, isCount, Seed));
        }


        public static StepResult shtRndWeibull(ITemplateHost Host, ParameterBag Parameters)
        {
            int cols = Parameters["cols"].AsInt32;
            int rows = Parameters["rows"].AsInt32;
            double a = Parameters["a"].AsDouble;
            double b = Parameters["b"].AsDouble;
            int Seed = Parameters["seed"].AsInt32;
            return WrapFrame("output", Random.rndWeibull(Host, rows, cols, a, b, Seed));
        }


        private static StepResult WrapFrame(string Name, DataFrame Frame)
        {
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput(Name, Frame);
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult ShtRank(ITemplateHost host, ParameterBag parameters)
        {
            int c; int cx = 0;
            int nx = 0;
            double tie;
            string temp;

            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            int q = Int32.Parse(parameters["tie-correction"].AsString);
            int rows = inputVariable.Length;
            double[] prk = new double[rows + 1 /* for VB to C# conversion */ ];
            foreach (double value in inputVariable.Data)
            {
                if (value != Constant.MISSING)
                {
                    nx = nx + 1;
                    prk[nx] = value;
                }
            }
            double[] r = new double[nx + 1 /* for VB to C# conversion */ ];
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
                    outputVariable.set_Data(c - 1, r[cx] == Constant.MISSING ? Constant.MISSING : r[cx]);
                }
                else
                {
                    outputVariable.set_Data(c - 1, Constant.MISSING);
                }
            }
            return WrapFrame("output", outputFrame);
        }


        ///  <summary>
        ///  Assumes the input is a frame of string variables.  Returns a frame mirrored around x=y.
        ///  </summary>
        ///  <param name="Host"></param>
        ///  <param name="Parameters"></param>
        ///  <returns></returns>
        public static StepResult shtRotate(ITemplateHost Host, ParameterBag Parameters)
        {
            DataFrame data = Parameters["data"].AsDataFrame;
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
                        v.set_Data(col, inv.Data[row]);
                    }
                }
            }
            return WrapFrame("output", outputFrame);
        }


        private class DoubleAscending : IComparer<double>
        {
            private int Compare(double x, double y)
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
            private int Compare(double x, double y)
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

        public static StepResult ShtSort(ITemplateHost host, ParameterBag parameters)
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

            double[] dataArray = new double[rows - 1 + 1 /* for VB to C# conversion */ ];
            int nx;
            if (hasLink)
            {
                DataFrame linkData = parameters["linkdata"].AsDataFrame;
                DoubleVariable linkVariable = linkData.Variables[0].AsDoubleVariable;
                t += " (by " + linkVariable.Title + ")";
                double[] linkArray = new double[rows - 1 + 1 /* for VB to C# conversion */ ];
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

        public static StepResult ShtSortByExpression(ITemplateHost host, ParameterBag parameters)
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

        public static StepResult ShtSortInPlace(ITemplateHost host, ParameterBag parameters)
        {
            //  A gross hack - this just hands off to the UI.
            host.Amend(new SortInPlaceOptions(), parameters);
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }


        public static StepResult ShtTransformLog(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 0);
        }


        public static StepResult ShtTransformLog10(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 1);
        }


        public static StepResult ShtTransformLogit(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 2);
        }


        public static StepResult ShtTransformProbit(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 3);
        }


        public static StepResult ShtTransformAngular(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 4);
        }


        public static StepResult ShtTransformCumulate(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 5);
        }


        public static StepResult ShtTransformECDF(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 6);
        }


        public static StepResult ShtTransformZsd(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 7);
        }


        public static StepResult ShtTransformZecdf(ITemplateHost host, ParameterBag parameters)
        {
            return ShtTransforms(parameters, 8);
        }


        private static StepResult ShtTransforms(ParameterBag parameters, int index)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            double[] inputData = inputVariable.Data;
            int rows = inputData.Length;

            double[] a = new double[rows - 1 + 1 /* for VB to C# conversion */ ];
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
                for (int N = 0; N <= rows - 1; N++)
                {
                    if (inputData[N] == Constant.MISSING)
                    {
                        a[N] = Constant.MISSING;
                    }
                    else
                    {
                        double z = inputData[N] + cons;
                        if (z <= 0.0)
                        {
                            a[N] = Constant.MISSING;
                        }
                        else
                        {
                            a[N] = Math.Log(z);
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
                return x_amanipst(a, title);
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
                for (int N = 0; N <= rows - 1; N++)
                {
                    if (inputData[N] == Constant.MISSING)
                    {
                        a[N] = Constant.MISSING;
                    }
                    else
                    {
                        double z = inputData[N] + cons;
                        if (z <= 0.0)
                        {
                            a[N] = Constant.MISSING;
                        }
                        else
                        {
                            a[N] = Math.Log(z) / log10;
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
                return x_amanipst(a, title);
            }
            if ((index == 2))
            {
                double maxi = x_amanipdp(parameters["discrete"].AsString, inputData);
                for (int N = 0; N <= rows - 1; N++)
                {
                    if (inputData[N] == Constant.MISSING)
                    {
                        a[N] = Constant.MISSING;
                    }
                    else
                    {
                        double prop = Math.Abs(inputData[N] / maxi);
                        if (prop == 1 | prop == 0)
                        {
                            a[N] = Constant.MISSING;
                        }
                        else
                        {
                            if (prop / (1.0 - prop) < 0)
                            {
                                a[N] = Constant.MISSING;
                            }
                            else
                            {
                                a[N] = Math.Log(prop / (1.0 - prop));
                            }
                        }
                    }
                }
                return x_amanipst(a, "Logit: " + inputVariable.Title);
            }
            if ((index == 3))
            {
                double maxi = x_amanipdp(parameters["discrete"].AsString, inputData);
                for (int N = 0; N <= rows - 1; N++)
                {
                    if (inputData[N] == Constant.MISSING)
                    {
                        a[N] = Constant.MISSING;
                    }
                    else
                    {
                        double prop = Math.Abs(inputData[N] / maxi);
                        if (prop == 1.0 | prop == 0.0)
                        {
                            a[N] = Constant.MISSING;
                        }
                        else
                        {
                            int fault;
                            double zed = PDF.gauinv(prop, out fault);
                            if (fault == 0)
                            {
                                a[N] = 5 + zed;
                            }
                            else { a[N] = Constant.MISSING; }
                        }
                    }
                }
                return x_amanipst(a, "Probit: " + inputVariable.Title);
            }
            if ((index == 4))
            {
                double maxi = x_amanipdp(parameters["discrete"].AsString, inputData);
                for (int N = 0; N <= rows - 1; N++)
                {
                    if (inputData[N] == Constant.MISSING)
                    {
                        a[N] = Constant.MISSING;
                    }
                    else
                    {
                        double prop = Math.Abs(inputData[N] / maxi);
                        if ((prop == 0.0))
                        {
                            a[N] = 0;
                        }
                        else if ((prop == 1.0))
                        {
                            a[N] = 90;
                        }
                        else if ((prop < 0.0) || (prop > 1.0))
                        {
                            a[N] = Constant.MISSING;
                        }
                        else
                        {
                            double x = Math.Sqrt(prop);
                            a[N] = 57.2957795130824 * (Math.Atan(x / Math.Sqrt(1.0 - x * x)));
                        }
                    }
                }
                return x_amanipst(a, "Angle: " + inputVariable.Title);
            }
            if ((index == 5))
            {
                for (int N = 0; N <= rows - 1; N++)
                {
                    if (inputData[N] == Constant.MISSING)
                    {
                        a[N] = Constant.MISSING;
                    }
                    else if (N == 0)
                    {
                        a[N] = inputData[N];
                    }
                    else
                    {
                        a[N] = a[N - 1] + inputData[N];
                    }
                }
                return x_amanipst(a, "Cumulate: " + inputVariable.Title);
            }
            if ((index == 6))
            {
                double[] fn = new double[inputData.Length - 1 + 1 /* for VB to C# conversion */ ];
                int err;
                MathDbl.ecdf(inputData, fn, out err);
                if (err == 0)
                {
                    return x_amanipst(fn, "ECDF: " + inputVariable.Title);
                }
                throw new ArgumentException("Insufficient data");
            }
            if ((index == 7))
            {
                double[] fn = new double[inputData.Length - 1 + 1 /* for VB to C# conversion */ ];
                int err;
                MathDbl.zscore(inputData, ref fn, false, out err);
                if (err == 0)
                {
                    return x_amanipst(fn, "Z: " + inputVariable.Title);
                }
                throw new ArgumentException("Insufficient data");
            }
            if ((index == 8))
            {
                double[] fn = new double[inputData.Length - 1 + 1 /* for VB to C# conversion */ ];
                int err;
                MathDbl.zscore(inputData, ref fn, true, out err);
                if (err == 0)
                {
                    return x_amanipst(fn, "Z score (ECDF): " + inputVariable.Title);
                }
                throw new ArgumentException("Insufficient data");
            }
            throw new ArgumentException("Unknown index");
        }


        private static double x_amanipdp(string discrete, double[] data)
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
                if (allMissing)
                {
                    return Constant.MISSING;
                }
                return maxi;
            }
            return 1.0;
        }


        private static StepResult x_amanipst(double[] a, string title)
        {
            DataFrame outputFrame = new DataFrame();
            DoubleVariable outputVariable = new DoubleVariable(a, title);
            outputFrame.Variables.Add(outputVariable);
            return WrapFrame("output", outputFrame);
        }


        public static StepResult ShtGroupCategorise(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable inputVariable = data.Variables[0].AsDoubleVariable;
            int rows = inputVariable.Length;
            CategoriseOptions options = new CategoriseOptions
                                            {
                                                Title = "Categorised: " + inputVariable.Title,
                                                PASSX = new double[rows - 1 + 1 /* for VB to C# conversion */],
                                                Data = inputVariable
                                            };
            if (!(host.Amend(options, parameters)))
            {
                throw new TemplateOperationCancelledException();
            }
            DataFrame outputFrame = new DataFrame();
            if ((options.PASSX != null) && options.PASSX.Length > 0)
            {
                string Lab = options.Title;
                DoubleVariable boundariesVariable = new DoubleVariable(options.PASSX, Lab);
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
                    countVariable.set_Data(r, options.Counts[r]);
                }
            }
            return WrapFrame("output", outputFrame);
        }


        public static StepResult ShtGroupExtract(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable dataVariable = data.Variables[0].AsDoubleVariable;
            string dtitle = dataVariable.Title;

            DataFrame identifiersFrame = parameters["identifiers"].AsDataFrame;

            int cols = identifiersFrame.VariableCount;
            string l = "";
            if (cols == 1)
            {
                l += "X: " + identifiersFrame.Variables[0].Title + "\r\n";
            }
            else
            {
                int j;
                for (j = 1; j <= cols; j++)
                {
                    l += "X" + j.ToString() + ": " + identifiersFrame.Variables[j - 1].Title + "\r\n";
                }
            }

            ExtractionOptions options = new ExtractionOptions
                                            {
                                                Title = dtitle,
                                                IdentifierNames = l,
                                                Data = dataVariable,
                                                IdentifiersFrame = identifiersFrame
                                            };
            host.Amend(options, parameters);
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }

    }

}

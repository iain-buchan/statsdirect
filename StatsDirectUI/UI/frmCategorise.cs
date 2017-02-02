using System;
using System.Text;
using System.Windows.Forms;

using StatsDirect.Numerics;
using StatsDirect.Utilities;
using StatsDirect.Data;
using StatsDirect.Builtins;
using StatsDirect.Charting;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class frmCategorise : Form
    {
        private static int CAT_TYPE;
        private static double CAT_MIN;
        private static double CAT_INT;
        private static int CAT_STEPS;
        private static bool CAT_KEY;

        private readonly CategoriseOptions options;
        private bool userCancelled;
        private bool filling; // Used to prevent changes to textboxes forcing user-defined values

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        public frmCategorise(CategoriseOptions options)
        {
            this.options = options;
            InitializeComponent();
            LoadDefaults();
        }

        private void CmdOkClick(object sender, EventArgs e)
        {
            SaveDefaults();
            FillOptionsFromForm();
            Close();
        }

        private void CmdCancelClick(object sender, EventArgs e)
        {
            userCancelled = true;
            SaveDefaults();
            Close();
        }

        private void FrmCategoriseShown(object sender, EventArgs e)
        {
            FillFormFromOptions();
        }

        private void FillOptionsFromForm()
        {
            options.Title = txtTitle.Text;
            // PASSX is already filled
            if (chkSaveKeyAndCounts.Checked)
            {
                options.Categories = new string[gridCutoffs.Rows.Count - 1];
                options.Counts = new int[gridCutoffs.Rows.Count - 1];
                // Skip the last (blank) row
                for (int i = 0; i < gridCutoffs.Rows.Count - 1; i++)
                {
                    object val = gridCutoffs.Rows[i].Cells[0].Value;
                    if (null != val)
                        options.Categories[i] = (string)val;
                    val = gridCutoffs.Rows[i].Cells[1].Value;
                    if (null != val)
                        options.Counts[i] = (int)val;
                }
            }
        }

        private void FillFormFromOptions()
        {
            SummStats(options.Data);
            CalculateGroups();
            txtTitle.Text = options.Title;
        }

        private void LoadDefaults()
        {
            if (CAT_TYPE >= 0)
            {
                if (0 == CAT_TYPE)
                    rdoQuartiles.Checked = true;
                else if (1 == CAT_TYPE)
                    rdoQuintiles.Checked = true;
                else if (2 == CAT_TYPE)
                    rdoDeciles.Checked = true;
                else if (3 == CAT_TYPE)
                    rdoAge15By5.Checked = true;
                else if (4 == CAT_TYPE)
                    rdoAge15By10.Checked = true;
                else if (5 == CAT_TYPE)
                    rdoAge1By5.Checked = true;
                else if (6 == CAT_TYPE)
                    rdoAge6Groups.Checked = true;
                else if (7 == CAT_TYPE)
                    rdoUserDefined.Checked = true;
            }
            chkSaveKeyAndCounts.Checked = CAT_KEY;
        }

        private void SaveDefaults()
        {
            if (rdoQuartiles.Checked)
                CAT_TYPE = 0;
            else if (rdoQuintiles.Checked)
                CAT_TYPE = 1;
            else if (rdoDeciles.Checked)
                CAT_TYPE = 2;
            else if (rdoAge15By5.Checked)
                CAT_TYPE = 3;
            else if (rdoAge15By10.Checked)
                CAT_TYPE = 4;
            else if (rdoAge1By5.Checked)
                CAT_TYPE = 5;
            else if (rdoAge6Groups.Checked)
                CAT_TYPE = 6;
            else // user-defined
                CAT_TYPE = 7;
            CAT_MIN = Parsing.Cdbl_Txt(txtMinimum.Text);
            CAT_STEPS = Parsing.Cint_Txt(txtIntervals.Text);
            CAT_INT = Parsing.Cdbl_Txt(txtInterval.Text);
            CAT_KEY = chkSaveKeyAndCounts.Checked;
        }

        private int reali;
        private double[] ao;

        private void GetCounts(int k, double[] bin, DoubleVariable v)
        {
            gridCutoffs.ClearSelection();
            gridCutoffs.Rows.Clear();
            int start = 1;
            int i;
            gridCutoffs.Rows.Add(k + 1);
            for (i = 1; i <= k + 1; i++)
            {
                int count = 0;
                if (i <= k)
                {
                    if (start <= reali)
                    {
                        int j;
                        for (j = start; j <= reali; j++)
                        {
                            if (ao[j] <= bin[i])
                                count++;
                            else
                                break;
                        }
                        start = j;
                    }
                    if (1 == i)
                        gridCutoffs.Rows[i - 1].Cells[0].Value = "<= " + bin[i].ToString();
                    else
                        gridCutoffs.Rows[i - 1].Cells[0].Value = "> " + bin[i - 1].ToString() + "; <= " + bin[i].ToString();
                }
                else
                {
                    if (start <= reali)
                    {
                        int j;
                        for (j = start; j <= reali; j++)
                        {
                            if (ao[j] > bin[k])
                            {
                                count++;
                            }
                            else
                            {
                                start = j + 1;
                                break;
                            }
                        }
                    }
                    gridCutoffs.Rows[i - 1].Cells[0].Value = "> " + bin[k].ToString();
                }
                gridCutoffs.Rows[i - 1].Cells[1].Value = count;
            }
            for (i = 0; i < v.Length; i++)
            {
                int j;
                for (j = 1; j <= k + 1; j++)
                {
                    if (1 == j)
                    {
                        if (v.Data[i] <= bin[j])
                            break;
                    }
                    else if (j == k + 1)
                    {
                        if (v.Data[i] > bin[j - 1])
                            break;
                    }
                    else
                    {
                        if (v.Data[i] > bin[j - 1] && v.Data[i] <= bin[j])
                            break;
                    }
                }
                if (Constant.MISSING == v.Data[i])
                    options.PassX[i] = Constant.MISSING;
                else
                    options.PassX[i] = j;
            }
        }

        private enum CentileMethod
        {
            Method1, // Non-Stata
            Method2 // Stata
        }

        private void GetQuantiles(int n, int k, double[] q, CentileMethod method)
        {
            if (k < 2)
                return;
            if (null == ao)
                return;
            for (int i = 1; i <= k; i++)
            {
                double centile = i / ((double)k);

                switch (method)
                {
                    case CentileMethod.Method1:
                        {
                            // "Method 1" - non-Stata
                            double index = centile * n;
                            double cumsum = 0;
                            int j;
                            double lastcumsum = 0;
                            for (j = 1; j <= n; j++)
                            {
                                cumsum += 1;
                                if (cumsum > index)
                                    break;
                                lastcumsum = cumsum;
                            }
                            if (j > n)
                                j = n;
                            if (lastcumsum == index)
                                q[i] = (ao[j - 1] + ao[j]) / 2.0;
                            else
                                q[i] = ao[j];
                        }
                        break;
                    case CentileMethod.Method2:
                        {
                            double index = Math.Floor(centile * (n + 1));
                            double h = centile * (n + 1) - index;
                            int bottom = (index < 1) ? 1 : index > n ? n : Convert.ToInt32(index);
                            int top = index + 1 > n ? n : Convert.ToInt32(index) + 1;
                            q[i] = (1.0 - h) * ao[bottom] + h * ao[top];
                        }
                        break;
                }
            }
        }

        private void SummStats(DoubleVariable v)
        {
            if (v.Length < 1)
            {
                txtDescribe.Text = "Not enough numeric data in this column to analyse.";
                return;

            }
            ao = new double[v.Length + 1];
            reali = 0;
            for (int i = 1; i <= v.Length; i++)
            {
                ao[i] = v.Data[i - 1];
                if (ao[i] != Constant.MISSING)
                    reali += 1;
            }
            Summary sx = new Summary();
            const double gamma = 0.95;
            const double userCentL = 5;
            const double userCentU = 95;
            const int centileDef = 1;
            int rows = v.Length;
            string title = v.Title;
            sx.FullSummaryFromXSort(ao, out ao, rows, title, gamma, userCentL, userCentU, centileDef);
            const int flt = 6;
            const int k = 19;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Title: " + v.Title + "\r\n");
            sb.AppendLine(Formatting.PadTo("Valid data", k) + sx.ValidData.ToString());
            sb.AppendLine(Formatting.PadTo("Missing", k) + sx.MissingData.ToString());
            sb.AppendLine(Formatting.PadTo("Sum", k) + Formatting.RoundOut(sx.Sum, flt));
            sb.AppendLine(Formatting.PadTo("Mean", k) + Formatting.RoundOut(sx.Mean, flt));
            sb.AppendLine(Formatting.PadTo("Variance", k) + Formatting.RoundOut(sx.Variance, flt));
            sb.AppendLine(Formatting.PadTo("Std. dev.", k) + Formatting.RoundOut(sx.SD, flt));
            sb.AppendLine(Formatting.PadTo("Variation coef.", k) + Formatting.RoundOut(sx.VarianceCoefficient, flt));
            sb.AppendLine(Formatting.PadTo("Std. err.", k) + Formatting.RoundOut(sx.SEM, flt));
            sb.AppendLine(Formatting.PadTo(Formatting.XRound(100 * gamma, 1) + "% Upper CL", k) + Formatting.RoundOut(sx.MeanUCL, flt));
            sb.AppendLine(Formatting.PadTo(Formatting.XRound(100 * gamma, 1) + "% Lower CL", k) + Formatting.RoundOut(sx.MeanLCL, flt));
            sb.AppendLine(Formatting.PadTo("Geometric mean", k) + Formatting.RoundOut(sx.GeometricMean, flt));
            sb.AppendLine(Formatting.PadTo("Skewness", k) + Formatting.RoundOut(sx.Skewness, flt));
            sb.AppendLine(Formatting.PadTo("Kurtosis", k) + Formatting.RoundOut(sx.Kurtosis, flt));
            sb.AppendLine(Formatting.PadTo("Maximum", k) + Formatting.RoundOut(sx.Maximum, flt));
            sb.AppendLine(Formatting.PadTo("95th percentile", k) + Formatting.RoundOut(sx.UserCentileU, flt));
            sb.AppendLine(Formatting.PadTo("Upper quartile", k) + Formatting.RoundOut(sx.UpperQuartile, flt));
            sb.AppendLine(Formatting.PadTo("Median", k) + Formatting.RoundOut(sx.Median, flt));
            sb.AppendLine(Formatting.PadTo("Lower quartile", k) + Formatting.RoundOut(sx.LowerQuartile, flt));
            sb.AppendLine(Formatting.PadTo("Interquartile range", k) + Formatting.RoundOut(sx.InterquartileRange, flt));
            sb.AppendLine(Formatting.PadTo("5th percentile", k) + Formatting.RoundOut(sx.UserCentileL, flt));
            sb.AppendLine(Formatting.PadTo("Minimum", k) + Formatting.RoundOut(sx.Minimum, flt));
            sb.AppendLine(Formatting.PadTo("Range", k) + Formatting.RoundOut(sx.Range, flt));
            txtDescribe.Text = sb.ToString();
            filling = true;
            txtIntervals.Text = CAT_STEPS > 1 ? CAT_STEPS.ToString() : 10.ToString();
            int nsteps = Parsing.Cint_Txt(txtIntervals.Text);
            if (sx.Minimum >= CAT_MIN && sx.Maximum <= CAT_MIN + (CAT_STEPS + 1) * CAT_INT && sx.Maximum > CAT_MIN + CAT_INT)
            {
                txtMinimum.Text = Math.Round(CAT_MIN, 14).ToString();
                txtInterval.Text = Math.Round(CAT_INT, 14).ToString();
            }
            else
            {
                LinearAxisScale axisScale = LinearAxisScaler.v_axis(sx.Minimum, sx.Maximum, nsteps);
                txtIntervals.Text = nsteps.ToString();
                txtMinimum.Text = Math.Round(axisScale.MinimumScaleValue, 14).ToString();
                txtInterval.Text = Math.Round(axisScale.Interval, 14).ToString();
            }
            filling = false;
        }

        private void CalculateGroups()
        {
            CentileMethod method = rdoCentileMethod1.Checked ? CentileMethod.Method1 : CentileMethod.Method2;
            int c;
            if (rdoQuartiles.Checked)
                c = 0;
            else if (rdoQuintiles.Checked)
                c = 1;
            else if (rdoDeciles.Checked)
                c = 2;
            else if (rdoAge15By5.Checked)
                c = 3;
            else if (rdoAge15By10.Checked)
                c = 4;
            else if (rdoAge1By5.Checked)
                c = 5;
            else if (rdoAge6Groups.Checked)
                c = 6;
            else if (rdoTertiles.Checked)
                c = 8;
            else // user-defined
                c = 7;
            int nbins;
            double[] bin;
            switch (c)
            {
                case 0:
                    nbins = 4;
                    bin = new double[nbins + 1];
                    GetQuantiles(reali, 4, bin, method);
                    nbins--;
                    break;
                case 1:
                    nbins = 5;
                    bin = new double[nbins + 1];
                    GetQuantiles(reali, 5, bin, method);
                    nbins--;
                    break;
                case 2:
                    nbins = 10;
                    bin = new double[nbins + 1];
                    GetQuantiles(reali, 10, bin, method);
                    nbins--;
                    break;
                case 3:
                    nbins = 15;
                    bin = new double[nbins + 1];
                    bin[1] = 15;
                    bin[2] = 20;
                    bin[3] = 25;
                    bin[4] = 30;
                    bin[5] = 35;
                    bin[6] = 40;
                    bin[7] = 45;
                    bin[8] = 50;
                    bin[9] = 55;
                    bin[10] = 60;
                    bin[11] = 65;
                    bin[12] = 70;
                    bin[13] = 75;
                    bin[14] = 80;
                    bin[15] = 85;
                    break;
                case 4:
                    nbins = 8;
                    bin = new double[nbins + 1];
                    bin[1] = 15;
                    bin[2] = 25;
                    bin[3] = 35;
                    bin[4] = 45;
                    bin[5] = 55;
                    bin[6] = 65;
                    bin[7] = 75;
                    bin[8] = 85;
                    break;
                case 5:
                    nbins = 18;
                    bin = new double[nbins + 1];
                    bin[1] = 1;
                    bin[2] = 5;
                    bin[3] = 10;
                    bin[4] = 15;
                    bin[5] = 20;
                    bin[6] = 25;
                    bin[7] = 30;
                    bin[8] = 35;
                    bin[9] = 40;
                    bin[10] = 45;
                    bin[11] = 50;
                    bin[12] = 55;
                    bin[13] = 60;
                    bin[14] = 65;
                    bin[15] = 70;
                    bin[16] = 75;
                    bin[17] = 80;
                    bin[18] = 85;
                    break;
                case 6:
                    nbins = 8;
                    bin = new double[nbins + 1];
                    bin[1] = 1;
                    bin[2] = 5;
                    bin[3] = 15;
                    bin[4] = 35;
                    bin[5] = 65;
                    bin[6] = 75;
                    break;
                case 7:
                    double xmin = Parsing.Cdbl_Txt(txtMinimum.Text);
                    nbins = Parsing.Cint_Txt(txtIntervals.Text);
                    double xint = Parsing.Cdbl_Txt(txtInterval.Text);
                    bin = new double[nbins + 1];
                    double pivot = xmin;
                    for (int i = 1; i <= nbins; i++)
                    {
                        pivot += xint;
                        bin[i] = Math.Round(pivot, 14);
                    }
                    nbins--;
                    break;
                case 8: // Tertiles
                    nbins = 3;
                    bin = new double[nbins + 1];
                    GetQuantiles(reali, 3, bin, method);
                    nbins--;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            GetCounts(nbins, bin, options.Data);
        }

        private void CalculateCounts()
        {
            double[] bin = new double[gridCutoffs.Rows.Count];
            int k = 0;
            for (int i = 0; i < gridCutoffs.Rows.Count; i++)
            {
                string buf = (string)gridCutoffs.Rows[i].Cells[0].Value;
                if (null == buf)
                    continue;
                buf = buf.Replace("=", string.Empty);
                buf = buf.Replace("<", string.Empty);
                buf = buf.Replace(">", string.Empty);
                int j = buf.IndexOf(";", StringComparison.Ordinal);
                if (j >= 0)
                {
                    if (buf.Length > j + 1)
                        buf = buf.Substring(j + 1);
                }
                buf = buf.Trim();
                if (buf.Length > 0)
                {
                    k++;
                    bin[k] = Parsing.Cdbl_Txt(buf);
                    if (k > 1 && bin[k] == bin[k - 1])
                        --k;
                }
            }
            Array.Sort(bin, 1, k);
            GetCounts(k, bin, options.Data);
            gridCutoffs.Select();
            gridCutoffs.CurrentCell = gridCutoffs.Rows[0].Cells[0];
        }

        private void TxtIntervalTextChanged(object sender, EventArgs e)
        {
            if (!filling)
                rdoUserDefined.Checked = true;
        }

        private void TxtMinimumTextChanged(object sender, EventArgs e)
        {
            if (!filling)
                rdoUserDefined.Checked = true;
        }

        private void TxtIntervalsTextChanged(object sender, EventArgs e)
        {
            if (!filling)
                rdoUserDefined.Checked = true;
        }

        private void CmdReGroupClick(object sender, EventArgs e)
        {
            CalculateGroups();
        }

        private void CmdRecalculateClick(object sender, EventArgs e)
        {
            CalculateCounts();
        }

        private void CmdHelpClick(object sender, EventArgs e)
        {
            SdApplication.SoleInstance.ShowHelp(this, "1059");
        }
    }

}

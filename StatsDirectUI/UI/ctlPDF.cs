using System;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    /// <summary>
    /// The calculator of the Distributions menu: the tail probabilities of a value, and the value that has a tail probability,
    /// for the normal, Student's t, F, chi-square, studentized range, binomial, Poisson, Kendall's tau, Spearman's rho and
    /// non-central t distributions.
    /// </summary>
    /// <remarks>
    /// There is a box for the value (txtPdf), two for the parameters (txtDf and txtDf2) and three for probabilities: the
    /// lower tail (txtLp), the upper tail (txtUp) and the two sided probability (txt2p). SetVisibility shows the boxes that
    /// a distribution has, and names them. Calculate works out the probabilities of the value, and Invert the value of a
    /// probability; which of them applies is known from the box that was entered last. The binomial and Poisson
    /// distributions use the three boxes of probabilities for the probability of the number that was entered, of that number
    /// or more, and of that number or fewer. The line that goes to the report is that of the last calculation.
    /// A probability is shown to 15 decimal places, or to 15 figures if it is below 1e-15; the studentized range is shown
    /// to 7 decimal places. For the normal, Student's t and non-central t distributions a small tail is worked out as
    /// itself, never as 1 less the other tail.
    /// </remarks>
    public partial class ctlPDF : IFillParameterBag
    {
        private enum TouchedValue
        {
            NotSet,
            Pdf,
            Df,
            Df2,
            Lp,
            Up,
            P2
        }

        private const string MINIMAL = "< 1E-15";
        private readonly DistributionType selectedTest;
        private bool inverseAvailable;
        private string lastCalculationAsString;
        /// <summary>
        /// If true, the user has changed a text box but not hit Calculate or Invert, or anything else that would perform the calculation.
        /// </summary>
        private bool dirty;
        /// <summary>
        /// The box that was hit prior to lastTouchedValue - needed for some inversions.
        /// </summary>
        private TouchedValue priorTouchedValue = TouchedValue.NotSet;
        private TouchedValue lastTouchedValue = TouchedValue.NotSet;

        public ctlPDF(DistributionOptions options)
        {
            selectedTest = options.SelectedTest;
            InitializeComponent();
            SetVisibility();
            txtPdf.Tag = TouchedValue.Pdf;
            txtDf.Tag = TouchedValue.Df;
            txtDf2.Tag = TouchedValue.Df2;
            txtLp.Tag = TouchedValue.Lp;
            txtUp.Tag = TouchedValue.Up;
            txt2p.Tag = TouchedValue.P2;
        }

        private void Calc_Click(object sender, EventArgs e)
        {
            Calculate();
        }

        private void DoubleClickTextbox(object sender, EventArgs e)
        {
            Control ctl = (Control)sender;
            NoteHistory((TouchedValue)ctl.Tag);
            CalculateOrInvert();
        }

        private void EnterTextbox(object sender, EventArgs e)
        {
            Control ctl = (Control)sender;
            NoteHistory((TouchedValue)ctl.Tag);
        }

        // The box that was entered last, and the one before it: by these Calculate and Invert know what is asked of them
        private void NoteHistory(TouchedValue tv)
        {
            if (tv != lastTouchedValue)
            {
                if (priorTouchedValue != lastTouchedValue)
                    priorTouchedValue = lastTouchedValue;
                lastTouchedValue = tv;
            }
        }

        private void LeaveTextbox(object sender, EventArgs e)
        {
            // TODO: Put back textbox leaves as command triggers.  This causes a problem as a leave fires before a button click - is there a better way to handle this using a different event?
            /*
            Control ctl = (Control)sender;
            lastTouchedValue = (TouchedValue)ctl.Tag;
            CalculateOrInvert();
            ctl.Enabled = true;
             */
        }

        private void edpdf_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                if (pnlDf.Visible)
                    SelectNextControl(txtPdf, true, true, true, true);
                else
                    Calculate();
            }
            else
                dirty = true;
        }

        private void eddf_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                if (pnlDf2.Visible)
                    SelectNextControl(txtDf, true, true, true, true);
                else
                    Calculate();
            }
            else
                dirty = true;
        }

        private void eddf2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                Calculate();
            }
            else
            {
                if (selectedTest == DistributionType.Rho || selectedTest == DistributionType.Kendall)
                    txtPdf.Text = string.Empty;
                dirty = true;
            }
        }

        private void NoteDirty(object sender, KeyPressEventArgs e)
        {
            dirty = true;
        }

        // The value for a confidence level, which is kept within 0.01 to 99.99 per cent: the upper tail is half of what the
        // level leaves. (The panel that has the buttons of the confidence level is not shown.)
        private void BtnLclClick(object sender, EventArgs e)
        {
            try
            {
                if (CdblTxt(cboCl.Text) >= 100.0)
                    cboCl.Text = 99.99.ToString();
                if (CdblTxt(cboCl.Text) <= 0.0)
                    cboCl.Text = 0.01.ToString();
                double cl = CdblTxt(cboCl.Text) / 100.0;
                double p = (1.0 - cl) / 2.0;
                txtUp.Text = p.ToString();
                CalculateUp();
                dirty = false;
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        // As BtnLclClick, but the half of what the level leaves is put into the two sided box, of which the upper tail is
        // again the half: the value is that of an upper tail of a quarter of what the level leaves
        private void BtnUclClick(object sender, EventArgs e)
        {
            try
            {
                if (CdblTxt(cboCl.Text) >= 100.0)
                    cboCl.Text = 99.99.ToString();
                if (CdblTxt(cboCl.Text) <= 0.0)
                    cboCl.Text = 0.01.ToString();
                double cl = CdblTxt(cboCl.Text) / 100.0;
                double P = (1.0 - cl) / 2.0;
                txt2p.Text = P.ToString();
                Calculate2P();
                dirty = false;
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void CalculateOrInvert()
        {
            // Each detects whether it should run
            Calculate();
            Invert(false);
        }

        // The probabilities of what was entered last, if that was the value or one of the two parameters. Nothing is done if a
        // probability was entered last: Invert is for that.
        private void Calculate()
        {
            switch (lastTouchedValue)
            {
                case TouchedValue.Pdf:
                    lblError.Text = string.Empty;
                    CalculatePdf();
                    dirty = false;
                    break;
                case TouchedValue.Df:
                    lblError.Text = string.Empty;
                    CalculateDf();
                    dirty = false;
                    break;
                case TouchedValue.Df2:
                    lblError.Text = string.Empty;
                    CalculateDf2();
                    dirty = false;
                    break;
            }
        }

        // The value of the probability that was entered last. (The argument is not used: a parameter that was changed is
        // followed whatever it is.)
        private void Invert(bool invertIfDfChanged)
        {
            InvertOn(lastTouchedValue, true);
        }

        // The value of the probability of the box tv. If tv is the box of the first parameter, the probability is that of the
        // box that was entered before it, or the lower tail if that was the value.
        private void InvertOn(TouchedValue tv, bool invertIfDfChanged)
        {
            switch (tv)
            {
                case TouchedValue.Lp:
                    lblError.Text = string.Empty;
                    CalculateLp();
                    dirty = false;
                    break;
                case TouchedValue.Up:
                    lblError.Text = string.Empty;
                    CalculateUp();
                    dirty = false;
                    break;
                case TouchedValue.P2:
                    lblError.Text = string.Empty;
                    Calculate2P();
                    dirty = false;
                    break;
                case TouchedValue.Df:
                    if (!invertIfDfChanged)
                        break;
                    // There's a use case where the user enters PDF, DF, clicks Calculate, changes DF, clicks Invert.  In this case, any of LP, UP or 2P are suitable sources for the p-value; we choose LP.
                    InvertOn(priorTouchedValue == TouchedValue.Pdf ? TouchedValue.Lp : priorTouchedValue, false);
                    break;
            }
        }

        // From the two sided box. The probability is kept within 0 to 1; its half is the upper tail, of which the value is
        // found, and the lower tail is 1 less that. For the Poisson distribution the box has the probability of the number of
        // events or fewer, and the mean that gives it is found.
        private void Calculate2P()
        {
            try
            {
                if (inverseAvailable)
                {
                    double p = CdblTxt(txt2p.Text);
                    if (p < 0)
                        p = 0.0;
                    if (p > 1)
                        p = 1.0;
                    Pval15Into(txt2p, p, AllowsZeroP(selectedTest));
                    if (selectedTest != DistributionType.Poisson)
                    {
                        p /= 2.0;
                        if (p > 1.0 - p)
                            p = 1.0 - p;
                        Pval15Into(txtLp, 1.0 - p, AllowsZeroP(selectedTest));
                        Pval15Into(txtUp, p, AllowsZeroP(selectedTest));
                    }
                    XFromP(p, 3);
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        // From the box of the first parameter. For Kendall's tau and Spearman's rho with a tau or rho entered the probability
        // is worked out at once. For the rest a parameter below 0 is made 0, and the probabilities are worked out unless the
        // box of the second parameter is still to be filled.
        private void CalculateDf()
        {
            try
            {
                double df = CdblTxt(txtDf.Text);
                if ((selectedTest == DistributionType.Rho || selectedTest == DistributionType.Kendall) && txtPdf.Text.Length > 0)
                {
                    // the range checks, and T or S, are PFromRho's and PFromKendall's, the same whichever box is filled
                    PFromX();
                }
                else // Not rho, not Kendall
                {
                    if (df < 0)
                        df = 0;
                    Xval15Into(txtDf, df);
                    if (pnlDf2.Visible && txtDf2.Text.Length == 0)
                        txtDf2.Focus();
                    else
                        PFromX();
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        // From the box of the second parameter, which is a whole number but for the mean of the Poisson distribution and the
        // non-centrality of the non-central t
        private void CalculateDf2()
        {
            try
            {
                if (selectedTest != DistributionType.Poisson && selectedTest != DistributionType.NonCentralT)
                {
                    int df = Parsing.Cint_Txt(txtDf2.Text);
                    // degrees of freedom and a number of samples start at 1; a number of successes, the Hotelling-Pabst T or
                    // Kendall's S can be 0, and S negative
                    if (selectedTest != DistributionType.Binomial && selectedTest != DistributionType.Kendall && selectedTest != DistributionType.Rho)
                    {
                        if (df < 1)
                            df = 1;
                    }
                    txtDf2.Text = df.ToString();
                }
                else
                    Xval15Tidy(txtDf2);
                PFromX();
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        // From the lower tail box. The probability is kept within 0 to 1; the upper tail is 1 less it, and the two sided
        // probability twice the smaller of the two. XFromP is given the upper tail, and that the lower tail box is where the
        // probability is from, so that the normal, Student's t, chi-square, studentized range and non-central t
        // distributions take the lower tail as it was entered.
        private void CalculateLp()
        {
            try
            {
                if (inverseAvailable)
                {
                    double p = CdblTxt(txtLp.Text);
                    if (p < 0.0)
                        p = 0.0;
                    if (p > 1.0)
                        p = 1.0;
                    Pval15Into(txtLp, p, AllowsZeroP(selectedTest));
                    Pval15Into(txtUp, 1.0 - p, AllowsZeroP(selectedTest));
                    double p2;
                    if (p > 1.0 - p)
                        p2 = 1.0 - p;
                    else
                        p2 = p;
                    p2 = 2.0 * p2;
                    Pval15Into(txt2p, p2, AllowsZeroP(selectedTest));
                    XFromP(1.0 - p, 1);
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        // From the box of the value: the probabilities are worked out unless the box of the first parameter is still to be
        // filled
        private void CalculatePdf()
        {
            try
            {
                Xval15Tidy(txtPdf);
                if (pnlDf.Visible && txtDf.Text.Length == 0)
                    txtDf.Focus();
                else
                    PFromX();
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        // From the upper tail box. The probability is kept within 0 to 1; the lower tail is 1 less it, and the two sided
        // probability twice the smaller of the two. For the Poisson distribution the box has the probability of the number of
        // events or more, and the mean that gives it is found.
        private void CalculateUp()
        {
            try
            {
                if (!inverseAvailable)
                    return;
                double p = CdblTxt(txtUp.Text);
                if (p < 0.0)
                    p = 0.0;
                if (p > 1.0)
                    p = 1.0;
                Pval15Into(txtUp, p, AllowsZeroP(selectedTest));
                if (selectedTest != DistributionType.Poisson)
                {
                    Pval15Into(txtLp, 1.0 - p, AllowsZeroP(selectedTest));
                    double p2 = 2.0 * (p > 1.0 - p ? 1.0 - p : p);
                    Pval15Into(txt2p, p2, AllowsZeroP(selectedTest));
                }
                XFromP(p, 2);
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        // Whether a probability of 0 can be shown. For Kendall's tau and Spearman's rho it cannot: every S or T that there can
        // be has a chance, and a probability below 1e-15 is shown as "< 1E-15".
        private static bool AllowsZeroP(DistributionType dt)
        {
            return !(dt == DistributionType.Rho || dt == DistributionType.Kendall);
        }

        private void PFromX()
        {
            switch (selectedTest)
            {
                case DistributionType.Z:
                    PFromZ();
                    break;
                case DistributionType.T:
                    PFromT();
                    break;
                case DistributionType.F:
                    PFromF();
                    break;
                case DistributionType.ChiSq:
                    PFromChiSq();
                    break;
                case DistributionType.Q:
                    PFromQ();
                    break;
                case DistributionType.Binomial:
                    PFromBinomial();
                    break;
                case DistributionType.Poisson:
                    PFromPoisson();
                    break;
                case DistributionType.Kendall:
                    PFromKendall();
                    break;
                case DistributionType.Rho:
                    PFromRho();
                    break;
                case DistributionType.NonCentralT:
                    PFromNonCentralT();
                    break;
            }
        }

        // Non-central t: the probabilities that a value is t or less, and above t, with whole degrees of freedom and the
        // non-centrality delta
        private void PFromNonCentralT()
        {
            double t = CdblTxt(txtPdf.Text);
            int df = Parsing.Cint_Txt(txtDf.Text);
            double delta = CdblTxt(txtDf2.Text);
            double p = ExFortran.pnct(t, df, delta, out int flt);
            // The smaller tail is worked out as itself and the larger is 1 less the smaller. The upper tail is the lower tail of
            // -t with the non-centrality -delta: as 1 - p it kept nothing of a tail below 1e-16 (t of 60 with 9 degrees of
            // freedom and a non-centrality of -3 printed 0).
            double pu = 1.0 - p;
            if (flt == 0 && p > 0.5)
            {
                pu = ExFortran.pnct(0.0 - t, df, 0.0 - delta, out flt);
                p = 1.0 - pu;
            }
            if (flt != 0)
            {
                txtUp.Text = Formatting.ERRR;
                txtLp.Text = Formatting.ERRR;
            }
            else
            {
                Pval15Into(txtLp, p, true);
                Pval15Into(txtUp, pu, true);
            }
            lastCalculationAsString = "P(non-central t < " + txtPdf.Text.Trim() + ", df " + txtDf.Text.Trim() + ", delta " +
                                      txtDf2.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper, " + txtLp.Text.Trim() +
                                      " lower";
        }

        // Spearman's rho: the upper tail probability, that of a rank correlation of rho or more, which is a Hotelling-Pabst T
        // of the T entered or less. If rho is entered T is worked out from it, to the nearest whole number; if not, rho is
        // worked out from T. An odd T, which no sample without ties has, has the probability of the even number below it.
        private void PFromRho()
        {
            int ix = 0;
            double pu = 0;
            int fault = 0;
            int nx = Parsing.Cint_Txt(txtDf.Text);
            // The same range whichever box is filled: at least four pairs; T within 0 and the most that there can be,
            // n(n^2 - 1)/3, so rho within -1 to 1. The most is formed as a double (as an int it overflowed above 1290 pairs),
            // and is to be a number that the routine can take.
            double most = (double)nx * ((double)nx * nx - 1.0) / 3.0;
            bool inRange = nx >= 4 && most <= int.MaxValue - 4;
            if (txtPdf.Text.Length > 0)
            {
                double rh = CdblTxt(txtPdf.Text);
                inRange = inRange && rh >= -1.0 && rh <= 1.0;
                if (inRange)
                {
                    ix = Convert.ToInt32((1.0 - rh) * most / 2.0);
                    txtDf2.Text = ix.ToString();
                }
                else
                    txtDf2.Text = Formatting.ERRR;
            }
            else
            {
                ix = Parsing.Cint_Txt(txtDf2.Text);
                inRange = inRange && ix >= 0 && ix <= most;
                if (inRange)
                    Xval15Into(txtPdf, 1.0 - ix / (most / 2.0));
            }
            if (!inRange)
                fault = -1;
            else
                pu = MathDbl.prhoUpper(nx, ix, out fault);
            Pval15Into(txtUp, pu, false, fault != 0);
            lastCalculationAsString = "P(Hotelling T " + ix.ToString() + ", n " + nx.ToString() + ") = " + txtUp.Text.Trim() + " upper tail";
        }

        // Kendall's tau: the upper tail probability, that of an S of the S entered or more. If tau is entered S is worked out
        // from it, to the nearest whole number; if not, tau is worked out from S.
        private void PFromKendall()
        {
            int ix = 0;
            double pu = 0;
            int fault = 0;
            int nx = Parsing.Cint_Txt(txtDf.Text);
            // The same range whichever box is filled: at least two observations, so that there are pairs; S within the n(n - 1)/2
            // pairs, so tau within -1 to 1. The tau box used to test a variable that was never set, and an S below 1 became 1.
            // (formed as a double: as an int the product overflowed above 46341 observations)
            double pairs = (double)nx * (nx - 1) / 2.0;
            bool inRange = nx >= 2;
            if (txtPdf.Text.Length > 0)
            {
                double tau = CdblTxt(txtPdf.Text);
                inRange = inRange && tau >= -1.0 && tau <= 1.0;
                if (inRange)
                {
                    ix = Convert.ToInt32(tau * pairs);
                    txtDf2.Text = ix.ToString();
                }
                else
                    txtDf2.Text = Formatting.ERRR;
            }
            else
            {
                ix = Parsing.Cint_Txt(txtDf2.Text);
                inRange = inRange && ix >= -pairs && ix <= pairs;
                if (inRange)
                    Xval15Into(txtPdf, ix / pairs);
            }
            if (!inRange)
                fault = -1;
            else
                pu = MathDbl.kendp(ix, nx, ref fault);
            Pval15Into(txtUp, pu, false, fault != 0);
            lastCalculationAsString = "P(Kendall's T " + ix.ToString() + ", n " + nx.ToString() + ") = " + txtUp.Text.Trim() + " upper tail";
        }

        // Poisson: the probabilities of the number of events, of that number or more and of that number or fewer, with the
        // mean M. The number is to be whole and not below 0, and the mean not below 0.
        private void PFromPoisson()
        {
            double phi = 0;
            double plo = 0;
            double term = 0;
            int fault;
            int nl = Parsing.Cint_Txt(txtDf.Text);
            double M = CdblTxt(txtDf2.Text);
            if (nl < 0 || M < 0.0)
                fault = -1;
            else
            {
                string x1 = "Probability of " + nl.ToString();
                lblLp.Text = x1 + " events";
                lblUp.Text = x1 + " or more events";
                lbl2p.Text = x1 + " or fewer events";
                ExFortran.poisson(M, nl, out phi, out plo, out term, out fault);
            }
            if (fault != 0)
            {
                txtLp.Text = Formatting.ERRR;
                txtUp.Text = Formatting.ERRR;
                txt2p.Text = Formatting.ERRR;
            }
            else
            {
                Pval15Into(txtLp, term, true);
                Pval15Into(txtUp, phi, true);
                Pval15Into(txt2p, plo, true);
            }
            lastCalculationAsString = "P(Poisson n " + txtDf.Text.Trim() + ", µ " + txtDf2.Text.Trim() + ") = " + txtLp.Text.Trim() + " for n events,  " + txtUp.Text.Trim() + " for n or more events,  " + txt2p.Text.Trim() + " for n or fewer events";
        }

        // Binomial: the probabilities of r successes in n trials, of r or more and of r or fewer, with the probability pud of
        // a success in a trial. There is to be a trial at least, and r is to be a whole number from 0 to n.
        private void PFromBinomial()
        {
            double dterm = 0;
            double dplo = 0;
            double dphi = 0;
            int fault;
            double pud = CdblTxt(txtPdf.Text);
            int n = Parsing.Cint_Txt(txtDf.Text);
            int r = Parsing.Cint_Txt(txtDf2.Text);
            // (a number that is not whole is read as the least int, which is below 0)
            if (pud < 0 || pud > 1.0 || n < 1 || r < 0 || r > n)
                fault = -1;
            else
            {
                const string x1 = "Probability of observing ";
                string x2 = r.ToString() + " successes";
                lblLp.Text = x1 + "=" + x2;
                lblUp.Text = x1 + ">=" + x2;
                lbl2p.Text = x1 + "<=" + x2;
                ExFortran.bino(n, pud, r, out dterm, out dplo, out dphi, out fault);
            }
            if (fault != 0)
            {
                txtLp.Text = Formatting.ERRR;
                txtUp.Text = Formatting.ERRR;
                txt2p.Text = Formatting.ERRR;
            }
            else
            {
                Pval15Into(txtLp, dterm, true);
                Pval15Into(txtUp, dphi, true);
                Pval15Into(txt2p, dplo, true);
            }
            lastCalculationAsString = "P(binomial p " + txtPdf.Text.Trim() + ", " + txtDf.Text.Trim() + " trials) = " + txtLp.Text.Trim() + " [" + txtDf2.Text.Trim() + " successes], " + txtUp.Text.Trim() + " [>=" + txtDf2.Text.Trim() + " successes], " + txt2p.Text.Trim() + " [<=" + txtDf2.Text.Trim() + " successes]";
        }

        // Studentized range: the probabilities that the range of the means of a number of samples, divided by the standard
        // error of a mean with df degrees of freedom, is Q or less, and above Q. They are shown to 7 decimal places. The
        // routine is within 0.0000005 where it has a series (8 to 20000 degrees of freedom that are twice the number of
        // samples or more), or within 0.000002 with more than 30 samples; it is within what is shown where it integrates
        // or, for two samples, has Student's t.
        private void PFromQ()
        {
            double pu = PDF.probsr(CdblTxt(txtPdf.Text), CdblTxt(txtDf2.Text), CdblTxt(txtDf.Text));
            if (pu == Constant.MISSING)
            {
                txtUp.Text = Formatting.ERRR;
                txtLp.Text = Formatting.ERRR;
            }
            else
            {
                txtLp.Text = Formatting.XRound(pu, 7);
                txtUp.Text = Formatting.XRound(1.0 - pu, 7);
            }
            lastCalculationAsString = "P(Q " + txtPdf.Text.Trim() + ", df " + txtDf.Text.Trim() + ", samples " + txtDf2.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper,  " + txtLp.Text.Trim() + " lower";
        }

        // Chi-square: the upper tail probability. (The two sided box is not shown for this distribution.)
        private void PFromChiSq()
        {
            double xtmp = PDF.chivalp(CdblTxt(txtPdf.Text), CdblTxt(txtDf.Text));
            Pval15Into(txtUp, xtmp, true);
            Pval15Into(txt2p, 2.0 * CdblTxt(txtUp.Text), true);
            lastCalculationAsString = "P(chi-sq " + txtPdf.Text.Trim() + ", df " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper tail";
        }

        // F (variance ratio): the upper tail probability
        private void PFromF()
        {
            double pu = PDF.fvalp(CdblTxt(txtPdf.Text), CdblTxt(txtDf.Text), CdblTxt(txtDf2.Text));
            Pval15Into(txtUp, pu, true);
            lastCalculationAsString = "P(F " + txtPdf.Text.Trim() + ", dfn " + txtDf.Text.Trim() + ", dfd " + txtDf2.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper";
        }

        // Student's t: the upper and lower tails, and the two sided probability, which is twice the smaller tail
        private void PFromT()
        {
            double t = CdblTxt(txtPdf.Text);
            double df = CdblTxt(txtDf.Text);
            double pu = PDF.tvalp(t, df);
            Pval15Into(txtUp, pu, true);
            // the lower tail is the upper tail of -t: as 1 - pu it kept nothing of a tail below 1e-16 (t of -60 with 30 degrees
            // of freedom printed 0)
            double pl = double.IsNaN(pu) ? pu : PDF.tvalp(-t, df);
            Pval15Into(txtLp, pl, true);
            // twice the smaller tail itself, not twice the tail as displayed and read back
            double p2 = 2.0 * Math.Min(pu, pl);
            Pval15Into(txt2p, p2, true);
            lastCalculationAsString = "P(t " + txtPdf.Text.Trim() + ", df " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper, " + txtLp.Text.Trim() + " lower, " + txt2p.Text.Trim() + " two sided";
        }

        // Normal: the upper and lower tails of a standard normal deviate, and the two sided probability, which is twice the
        // smaller tail
        private void PFromZ()
        {
            double z = CdblTxt(txtPdf.Text);
            // what could not be read as a number is the missing value, the least double: it had the lower tail 0
            if (z == Constant.MISSING)
                z = double.NaN;
            double pl = PDF.alnorm(z);
            // the upper tail is the lower tail of -z: as 1 - pl it kept nothing of a tail below 1e-16 (z of 9 printed 0)
            double pu = PDF.alnorm(-z);
            Pval15Into(txtUp, pu, true);
            Pval15Into(txtLp, pl, true);
            // twice the smaller tail itself, not twice the tail as displayed and read back
            double p2 = 2.0 * Math.Min(pu, pl);
            Pval15Into(txt2p, p2, true);
            lastCalculationAsString = "P(z " + txtPdf.Text.Trim() + ") = " + Pval15(pu, true) + " upper,  " + Pval15(pl, true) + " lower,  " + Pval15(p2, true) + " two sided";
        }

        // The boxes that the distribution has, and their names, and whether the value of a probability can be found (for the
        // binomial distribution it cannot). The lower tail box of the Poisson distribution cannot be entered: it has the
        // probability of the number of events.
        private void SetVisibility()
        {
            switch (selectedTest)
            {
                case DistributionType.Z:
                    inverseAvailable = true;
                    pnlLp.Visible = true;
                    pnlDf.Visible = false;
                    lblPdf.Text = "Normal deviate (z)";
                    pnl2p.Visible = true;
                    break;
                case DistributionType.T:
                    inverseAvailable = true;
                    lblPdf.Text = "Student's t";
                    break;
                case DistributionType.F:
                    inverseAvailable = true;
                    lblDf.Text = "Numerator degrees of freedom";
                    lblDf2.Text = "Denominator degrees of freedom";
                    lblUp.Text = "Upper tail P";
                    pnlDf2.Visible = true;
                    pnlLp.Visible = false;
                    pnl2p.Visible = false;
                    lblPdf.Text = "Variance ratio F";
                    break;
                case DistributionType.ChiSq:
                    inverseAvailable = true;
                    pnlLp.Visible = false;
                    pnlDf.Visible = true;
                    lblPdf.Text = "Chi-square";
                    pnl2p.Visible = false;
                    break;
                case DistributionType.Q:
                    inverseAvailable = true;
                    lblPdf.Text = "Studentized range Q";
                    lblDf2.Text = "Number of samples";
                    pnlDf2.Visible = true;
                    pnl2p.Visible = false;
                    break;
                case DistributionType.Binomial:
                    inverseAvailable = false;
                    lblPdf.Text = "Probability of success per trial";
                    lblDf.Text = "Number of trials";
                    lblDf2.Text = "Number of successes";
                    lblLp.Text = string.Empty;
                    lblUp.Text = string.Empty;
                    lbl2p.Text = string.Empty;
                    pnlDf2.Visible = true;
                    txtPdf.Text = ".5";
                    break;
                case DistributionType.Poisson:
                    inverseAvailable = true;
                    pnlPdf.Visible = false;
                    lblDf.Text = "Number of random events";
                    lblDf2.Text = "Mean";
                    lblLp.Text = string.Empty;
                    lblUp.Text = string.Empty;
                    lbl2p.Text = string.Empty;
                    pnlDf2.Visible = true;
                    break;
                case DistributionType.Kendall:
                    inverseAvailable = true;
                    lblPdf.Text = "Kendall's tau";
                    lblDf2.Text = "S (Nc - Nd)";
                    lblDf.Text = "Sample size";
                    pnlDf2.Visible = true;
                    pnlLp.Visible = false;
                    pnl2p.Visible = false;
                    lblUp.Text = "Upper tail P";
                    break;
                case DistributionType.Rho:
                    inverseAvailable = true;
                    lblPdf.Text = "Spearman's rho";
                    lblDf2.Text = "Hotelling-Pabst T";
                    lblDf.Text = "Sample size";
                    pnlDf2.Visible = true;
                    pnlLp.Visible = false;
                    pnl2p.Visible = false;
                    lblUp.Text = "Upper tail P";
                    break;
                case DistributionType.NonCentralT:
                    inverseAvailable = true;
                    lblPdf.Text = "Non-central t";
                    lblDf.Text = "Degrees of freedom";
                    lblDf2.Text = "Non-centrality";
                    lblLp.Text = "P for t <= t";
                    lblUp.Text = "P for t > t";
                    pnlDf2.Visible = true;
                    pnl2p.Visible = false;
                    break;
            }
            cmdInvert.Visible = inverseAvailable;
            txtLp.ReadOnly = !(inverseAvailable && selectedTest != DistributionType.Poisson);
            txtUp.ReadOnly = !inverseAvailable;
            txt2p.ReadOnly = !inverseAvailable;
        }

        // The value of a probability. P is the upper tail probability (for the Poisson distribution, the probability of the
        // box), and idx the box that was entered: 1 the lower tail, 2 the upper tail, 3 the two sided box.
        private void XFromP(double P, int idx)
        {
            // degrees of freedom below 1 are taken as 1; the Poisson count of events can be 0
            if (pnlDf.Visible && selectedTest != DistributionType.Poisson && CdblTxt(txtDf.Text) < 1)
                txtDf.Text = "1";

            switch (selectedTest)
            {
                case DistributionType.Z:
                    ZFromP(P, idx);
                    break;
                case DistributionType.T:
                    TFromP(P, idx);
                    break;
                case DistributionType.F:
                    FFromP(P);
                    break;
                case DistributionType.ChiSq:
                    ChiSqFromP(P, idx);
                    break;
                case DistributionType.Q:
                    QFromP(P);
                    break;
                case DistributionType.Poisson:
                    InvPoisson(idx, P, Parsing.Cint_Txt(txtDf.Text));
                    lastCalculationAsString = "Poisson mean(P of " + txtDf.Text.Trim() + " events " + Pval15(CdblTxt(txtLp.Text), true) + ", more " + Pval15(CdblTxt(txtUp.Text), true) + ", fewer " + Pval15(CdblTxt(txt2p.Text), true) + ") = " + txtDf2.Text.Trim();
                    break;
                case DistributionType.Kendall:
                    KendallFromP(P);
                    break;
                case DistributionType.Rho:
                    RhoFromP(P);
                    break;
                case DistributionType.NonCentralT:
                    NonCentralTFromP(P, idx);
                    break;
            }
        }

        private void NonCentralTFromP(double P, int idx)
        {
            // tnct takes a lower tail area. An upper tail P is inverted by symmetry, as minus the value of its own area with
            // the non-centrality -delta: through the lower tail box, which holds 1 - P to 15 places, a small P lost figures,
            // and one below 1e-15 was refused.
            int df = Parsing.Cint_Txt(txtDf.Text);
            double delta = CdblTxt(txtDf2.Text);
            double x;
            int flt;
            if (idx == 1)
                x = ExFortran.tnct(CdblTxt(txtLp.Text), df, delta, out flt);
            else
            {
                x = ExFortran.tnct(P, df, 0.0 - delta, out flt);
                if (flt == 0)
                    x = 0.0 - x;
            }
            Xval15Into(txtPdf, x, flt != 0);
            lastCalculationAsString = "non-central t(P " + Pval15(P, true) + ", df " + txtDf.Text.Trim() + ", delta " + txtDf2.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        // Spearman's rho of an upper tail P: the greatest T of which the probability is not above P, and its rho and
        // probability
        private void RhoFromP(double P)
        {
            int nx = Parsing.Cint_Txt(txtDf.Text);
            double rh = MathDbl.rhofromp(P, out double pu, out int ix, nx, out int fault);
            if (fault == 0)
            {
                txtDf2.Text = ix.ToString();
                Xval15Into(txtPdf, rh);
                Pval15Into(txtUp, pu, false);
            }
            else
                txtUp.Text = Formatting.ERRR;
            lastCalculationAsString = "Hotelling T (upper tail P " + Pval15(P, false) + ", n " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim();
        }

        // Kendall's tau of an upper tail P: the greatest S of which the probability is not below P, and its tau and
        // probability
        private void KendallFromP(double P)
        {
            int nx = Parsing.Cint_Txt(txtDf.Text);
            double tau = MathDbl.taufromp(P, out double pu, out int ix, ref nx, out int fault);
            if (fault == 0)
            {
                txtDf2.Text = ix.ToString();
                Xval15Into(txtPdf, tau);
                Pval15Into(txtUp, pu, false);
            }
            else
                txtUp.Text = Formatting.ERRR;
            lastCalculationAsString = "Kendall's T (upper tail P " + Pval15(P, false) + ", n " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim();
        }

        // The studentized range of a probability. The routine takes the probability below the value, which is read from the
        // lower tail box: the box has 1 - P to 15 places if the upper tail was entered, which is more than the 7 places of
        // the value need.
        private void QFromP(double P)
        {
            double x = PDF.quantsr(CdblTxt(txtLp.Text), CdblTxt(txtDf2.Text), CdblTxt(txtDf.Text));
            txtPdf.Text = x == Constant.MISSING ? Formatting.ERRR : Formatting.XRound(x, 7);
            lastCalculationAsString = "Q(upper P " + Formatting.XRound(P, 7) + ", df " + txtDf.Text.Trim() + ", samples " + txtDf2.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        private void ChiSqFromP(double P, int idx)
        {
            // An upper tail P is solved for as itself. Through the lower tail box, which holds 1 - P formed in double precision, a
            // small P lost figures (5e-7 with 1 degree of freedom gave 25.2638207260669 for 25.2638207259082).
            double x;
            int fault;
            if (idx == 1)
                x = PDF.ppchi2(CdblTxt(txtLp.Text), CdblTxt(txtDf.Text), out fault);
            else
                x = PDF.ppchi2(P, CdblTxt(txtDf.Text), true, out fault);
            Xval15Into(txtPdf, x, fault != 0);
            lastCalculationAsString = "chi-sq(upper P " + Pval15(P, true) + ", df " + txtDf.Text.Trim() + ") = " +
                                      txtPdf.Text.Trim();
        }

        // F of an upper tail P. (The routine has the denominator degrees of freedom first.)
        private void FFromP(double P)
        {
            double x = PDF.ffromp(CdblTxt(txtDf2.Text), CdblTxt(txtDf.Text), P);
            Xval15Into(txtPdf, x);
            lastCalculationAsString = "F(upper P " + Pval15(P, true) + ", dfn " + txtDf.Text.Trim() + ", dfd " +
                                      txtDf2.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        private void TFromP(double P, int idx)
        {
            // tfromp takes an upper tail area. A lower tail P is inverted by symmetry, as minus the value of its own area:
            // through 1 - P formed in double precision a small P lost figures (1e-12 with 1 degree of freedom gave
            // -318316927901.781 for -318309886183.791), and one below 1e-16 gave minus infinity.
            double df = CdblTxt(txtDf.Text);
            double x = idx == 1 ? 0.0 - PDF.tfromp(CdblTxt(txtLp.Text), df) : PDF.tfromp(P, df);
            Xval15Into(txtPdf, x);
            lastCalculationAsString = "t(upper P " + Pval15(P, true) + ", df " + txtDf.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        private void ZFromP(double P, int idx)
        {
            // gauinv takes a lower tail area. An upper or two sided P is inverted by symmetry, as minus the deviate of its own
            // value: through the lower tail box, which holds 1 - P formed in double precision, a P of 1e-14 gave 7.65073 for
            // 7.65063. (0.0 - keeps the deviate of a P of 0.5 at +0.)
            double x;
            int fault;
            if (idx == 1)
                x = PDF.gauinv(CdblTxt(txtLp.Text), out fault);
            else
                x = 0.0 - PDF.gauinv(P, out fault);
            Xval15Into(txtPdf, x, fault != 0);
            lastCalculationAsString = "z(upper P " + Pval15(P, true) + ") = " + txtPdf.Text.Trim();
        }

        /* Utilities */

        // The mean of the Poisson distribution with which nl events or more (idx 2), or nl events or fewer (idx 3), have the
        // probability P; the boxes are given the three probabilities of nl events with that mean. Above 100,000 events the
        // user is asked first, as the search is long.
        private void InvPoisson(int idx, double P, int nl)
        {
            int ifault;
            double xmid = 0;
            double trm = 0;
            double plo = 0;
            double phi = 0;

            if (nl == 0)
            {
                // The routine grows its search bracket by nl, so with no events it never leaves the origin (it returned a mean of
                // 1e-16 for any P). The probability of no events is exp(-mean), so the mean at which 0 or fewer events has
                // probability P is -ln P; 0 or more events has probability 1 whatever the mean, so that inverse is refused.
                // (0.0 - keeps the mean of a P of 1 at +0.)
                if (idx == 3 && P > 0.0 && P <= 1.0)
                {
                    xmid = 0.0 - Math.Log(P);
                    trm = P;
                    plo = P;
                    phi = 1.0;
                    ifault = 0;
                }
                else
                    ifault = 1;
            }
            else if (nl > 100000)
            {
                if (SdApplication.SoleInstance.Query("This calculation can take a long time with large numbers.\r\n\r\nDo you wish to continue?", "StatsDirect Poisson Inverse"))
                    ExFortran.poissoni(idx, P, out xmid, out trm, out phi, out plo, nl, out ifault);
                else
                    ifault = 4;
            }
            else
                ExFortran.poissoni(idx, P, out xmid, out trm, out phi, out plo, nl, out ifault);

            if (ifault == 0)
            {
                Pval15Into(txtLp, trm, true);
                Pval15Into(txtUp, phi, true);
                Pval15Into(txt2p, plo, true);
                txtDf2.Text = Formatting.XRound(xmid, 15);
            }
            else
            {
                txtLp.Text = Formatting.ERRR;
                txtUp.Text = Formatting.ERRR;
                txt2p.Text = Formatting.ERRR;
                txtDf2.Text = Formatting.ERRR;
            }

        }

        private static void Xval15Into(TextBox txt, double x, bool isError)
        {
            if (isError)
                txt.Text = Formatting.ERRR;
            else
                Xval15Into(txt, x);
        }

        // A value is put into its box, unless the box shows it already to 1e-13: what was entered is then left as it was
        // entered
        private static void Xval15Into(TextBox txt, double x)
        {
            bool shouldReplace = true;
            try
            {
                double current = CdblTxt(txt.Text);
                if (Math.Abs(x - current) < 1e-13)
                    shouldReplace = false;
            }
            catch (Exception)
            {
                shouldReplace = true;
            }
            if (shouldReplace)
                txt.Text = Xval15(x);
        }

        // What was entered in a box, as the calculator shows a value
        private static void Xval15Tidy(TextBox txt)
        {
            txt.Text = Xval15(CdblTxt(txt.Text));
        }

        // A value as it is shown: in 15 figures, of which 15 at most are decimal places; in 7 figures and an exponent if it is
        // above 1e15 or below 1e-15
        private static string Xval15(double x)
        {
            return Formatting.XRound(x, 15);
        }

        private static void Pval15Into(TextBox txt, double p, bool allowZero, bool isError)
        {
            if (isError)
                txt.Text = Formatting.ERRR;
            else
                Pval15Into(txt, p, allowZero);
        }

        private static void Pval15Into(TextBox txt, double p, bool allowZero)
        {
            // The box is left alone only when it already shows this value, so that a P typed as 0.050 or 5e-2 is not rewritten.
            // That is judged on the text that would be written, read back without rounding. Read back to 14 places, as it used to
            // be, every P below 1e-14 looked the same (3.7e-19 and 1.5e-23 both read as 0), so a second small P was not written
            // over the first: possible now that a chi-square tail can be that small.
            string fresh = Pval15(p, allowZero);
            string shown = txt.Text;
            bool same = string.Equals(shown, fresh)
                || (double.TryParse(shown, out double a) && double.TryParse(fresh, out double b) && a == b);
            if (!same)
                txt.Text = fresh;
        }

        // A probability as it is shown: to 15 decimal places; to 15 figures if it is below 1e-15, or as "< 1E-15" if a
        // probability of 0 cannot be shown; as 1 from 0.999999999999999; as "error" if there is none
        private static string Pval15(double p, bool allowZero)
        {
            if (p == Constant.MISSING || double.IsNaN(p))
                return Formatting.ERRR;
            if (!allowZero && p < 1e-15)
                return MINIMAL;
            // a tail too small for 15 decimal places is shown to 15 significant figures (it used to print as the raw double)
            if (p < 1e-15)
                return p.ToString("G15");
            if (p >= 0.999999999999999)
                return "1";
            return Formatting.XRound(p, 15);
        }

        // The number of a box. "< 1E-15" is read as 2.2e-16, and what cannot be read as a number as the missing value.
        private static double CdblTxt(string value)
        {
            return MINIMAL.Equals(value) ? Constant.EPSILON : Parsing.Cdbl_Txt(value);
        }

        private void FriendlyError(Exception ex)
        {
            lblError.Text = "StatsDirect couldn't calculate that function: " + ex.Message;
            SdApplication.WriteToBlackbox("Somewhere in ctlPdf", ex);
        }

        /// <summary>
        /// Gives the report the line of the last calculation ("gr"). If a box was changed since, the calculation is made first.
        /// </summary>
        public Control Fill(ParameterBag outputParameters, bool doValidation)
        {
            if (dirty)
                CalculateOrInvert();
            outputParameters.AddOutput("gr", lastCalculationAsString);
            return null;
        }

        private void cmdInvert_Click(object sender, EventArgs e)
        {
            Invert(true);
        }
    }
}
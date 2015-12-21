using System;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
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
        private readonly ITemplateHost host;
        /// <summary>
        /// The box that was hit prior to lastTouchedValue - needed for some inversions.
        /// </summary>
        private TouchedValue priorTouchedValue = TouchedValue.NotSet;
        private TouchedValue lastTouchedValue = TouchedValue.NotSet;

        public ctlPDF(DistributionOptions options, ITemplateHost host)
        {
            selectedTest = options.SelectedTest;
            this.host = host;
            InitializeComponent();
            SetVisibility();
            txtPdf.Tag = TouchedValue.Pdf;
            txtDf.Tag = TouchedValue.Df;
            txtDf2.Tag = TouchedValue.Df2;
            txtLp.Tag = TouchedValue.Lp;
            txtUp.Tag = TouchedValue.Up;
            txt2p.Tag = TouchedValue.P2;
        }

        private void Calc_Click(Object sender, EventArgs e)
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
            }
        }

        private void BtnLclClick(Object sender, EventArgs e)
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
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void BtnUclClick(Object sender, EventArgs e)
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

        private void Calculate()
        {
            switch (lastTouchedValue)
            {
                case TouchedValue.Pdf:
                    lblError.Text = string.Empty;
                    CalculatePdf();
                    break;
                case TouchedValue.Df:
                    lblError.Text = string.Empty;
                    CalculateDf();
                    break;
                case TouchedValue.Df2:
                    lblError.Text = string.Empty;
                    CalculateDf2();
                    break;
            }
        }

        private void Invert(bool invertIfDfChanged)
        {
            InvertOn(lastTouchedValue, true);
        }

        private void InvertOn(TouchedValue tv, bool invertIfDfChanged)
        {
            switch (tv)
            {
                case TouchedValue.Lp:
                    lblError.Text = string.Empty;
                    CalculateLp();
                    break;
                case TouchedValue.Up:
                    lblError.Text = string.Empty;
                    CalculateUp();
                    break;
                case TouchedValue.P2:
                    lblError.Text = string.Empty;
                    Calculate2P();
                    break;
                case TouchedValue.Df:
                    if (!invertIfDfChanged)
                        break;
                    // There's a use case where the user enters PDF, DF, clicks Calculate, changes DF, clicks Invert.  In this case, any of LP, UP or 2P are suitable sources for the p-value; we choose LP.
                    InvertOn(priorTouchedValue == TouchedValue.Pdf ? TouchedValue.Lp : priorTouchedValue, false);
                    break;
            }
        }

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
                        p = p / 2.0;
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

        private void CalculateDf()
        {
            try
            {
                double df = CdblTxt(txtDf.Text);
                if ((selectedTest == DistributionType.Rho || selectedTest == DistributionType.Kendall) && txtPdf.Text.Length > 0)
                {
                    bool bewareOfDf = (Constant.MISSING == df || df < int.MinValue || df > int.MaxValue);

                    int n;
                    if (selectedTest == DistributionType.Rho)
                    {
                        n = ((int)(Math.Floor(df)));
                        double rh = CdblTxt(txtPdf.Text);
                        if (bewareOfDf || n < 4 || rh < 0.0 || rh > 1.0)
                            txtDf2.Text = Formatting.ERRR;
                        else
                            txtDf2.Text = Convert.ToInt32(((1.0 - rh) * (n * (Math.Pow(n, 2) - 1))) / 6).ToString();
                        PFromX();
                    }
                    else // Kendall
                    {
                        n = Convert.ToInt32(df);
                        double tau = CdblTxt(txtPdf.Text);
                        if (n < 4 || tau < 0.0 || tau > 1.0)
                            txtDf2.Text = Formatting.ERRR;
                        else
                            txtDf2.Text = Convert.ToInt32(tau * (n * (n - 1) / 2.0)).ToString();
                        PFromX();
                    }
                }
                else // Not rho, not Kendall
                {
                    if (df < 0)
                    {
                        df = 0;
                    }
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

        private void CalculateDf2()
        {
            try
            {
                if (selectedTest != DistributionType.Poisson && selectedTest != DistributionType.NonCentralT)
                {
                    int df = Parsing.Cint_Txt(txtDf2.Text);
                    if (selectedTest != DistributionType.Binomial)
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

        private void PFromNonCentralT()
        {
            int flt;
            double p = ExFortran.pnct(CdblTxt(txtPdf.Text), Parsing.Cint_Txt(txtDf.Text),
                                      CdblTxt(txtDf2.Text), out flt);
            if (flt != 0)
            {
                txtUp.Text = Formatting.ERRR;
                txtLp.Text = Formatting.ERRR;
            }
            else
            {
                Pval15Into(txtLp, p, true);
                Pval15Into(txtUp, 1.0 - p, true);
            }
            lastCalculationAsString = "P(non-central t < " + txtPdf.Text.Trim() + ", df " + txtDf.Text.Trim() + ", delta " +
                                      txtDf2.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper, " + txtLp.Text.Trim() +
                                      " lower";
        }

        private void PFromRho()
        {
            double rh;
            int fault;
            int nx = Parsing.Cint_Txt(txtDf.Text);
            int ix = 0;
            if (txtPdf.Text.Length > 0)
            {
                rh = CdblTxt(txtPdf.Text);
                if (nx >= 4 & rh <= 1)
                {
                    ix = Convert.ToInt32(((1.0 - rh) * (nx * (nx * nx - 1))) / 6);
                    txtDf2.Text = ix.ToString();
                }
                else
                    txtDf2.Text = Formatting.ERRR;
            }
            else
            {
                ix = ((int)(CdblTxt(txtDf2.Text)));
                rh = 1.0 - ix / ((nx * (nx * nx - 1)) / 6.0);
                Xval15Into(txtPdf, rh);
            }
            double pu = 0;
            if (txtDf2.Text == Formatting.ERRR || nx < 4)
                fault = -1;
            else
                pu = 1.0 - ExFortran.prho(nx, ix, out fault);
            Pval15Into(txtUp, pu, false, fault != 0);
            lastCalculationAsString = "P(Hotelling T " + ix.ToString() + ", n " + nx.ToString() + ") = " + txtUp.Text.Trim() + " upper tail";
        }

        private void PFromKendall()
        {
            double rh = 0;
            int ix = 0;
            double pu = 0;
            int fault = 0;
            int nx = Parsing.Cint_Txt(txtDf.Text);
            double tau;
            if (txtPdf.Text.Length > 0)
            {
                tau = CdblTxt(txtPdf.Text);
                if (nx > 0 & rh <= 1)
                {
                    ix = Convert.ToInt32(tau * (nx * (nx - 1) / 2.0));
                    txtDf2.Text = ix.ToString();
                }
                else
                    txtDf2.Text = Formatting.ERRR;
            }
            else
            {
                ix = Parsing.Cint_Txt(txtDf2.Text);
                tau = ix / (nx * (nx - 1) / 2.0);
                Xval15Into(txtPdf, tau);
            }
            if (txtDf2.Text == Formatting.ERRR || nx < 1)
                fault = -1;
            else
                pu = MathDbl.kendp(ix, nx, ref fault);
            Pval15Into(txtUp, pu, false, fault != 0);
            lastCalculationAsString = "P(Kendall's T " + ix.ToString() + ", n " + nx.ToString() + ") = " + txtUp.Text.Trim() + " upper tail";
        }

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

        private void PFromBinomial()
        {
            double dterm = 0;
            double dplo = 0;
            double dphi = 0;
            int fault;
            double pud = CdblTxt(txtPdf.Text);
            int n = Parsing.Cint_Txt(txtDf.Text);
            int r = Parsing.Cint_Txt(txtDf2.Text);
            if (pud < 0 || pud > 1.0 || n < 1 || r > n)
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

        private void PFromChiSq()
        {
            double xtmp = PDF.chivalp(CdblTxt(txtPdf.Text), CdblTxt(txtDf.Text));
            Pval15Into(txtUp, xtmp, true);
            Pval15Into(txt2p, 2.0 * CdblTxt(txtUp.Text), true);
            lastCalculationAsString = "P(chi-sq " + txtPdf.Text.Trim() + ", df " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper tail";
        }

        private void PFromF()
        {
            double pu = PDF.fvalp(CdblTxt(txtPdf.Text), CdblTxt(txtDf.Text), CdblTxt(txtDf2.Text));
            Pval15Into(txtUp, pu, true);
            lastCalculationAsString = "P(F " + txtPdf.Text.Trim() + ", dfn " + txtDf.Text.Trim() + ", dfd " + txtDf2.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper";
        }

        private void PFromT()
        {
            double pu = PDF.tvalp(CdblTxt(txtPdf.Text), CdblTxt(txtDf.Text));
            Pval15Into(txtUp, pu, true);
            double pl = 1.0 - pu;
            Pval15Into(txtLp, pl, true);
            if (pu > pl)
                pu = pl;
            pu = 2.0 * CdblTxt(pval15(pu, true));
            Pval15Into(txt2p, pu, true);
            lastCalculationAsString = "P(t " + txtPdf.Text.Trim() + ", df " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim() + " upper, " + txtLp.Text.Trim() + " lower, " + txt2p.Text.Trim() + " two sided";
        }

        private void PFromZ()
        {
            double pl = PDF.alnorm(CdblTxt(txtPdf.Text));
            double pu = 1.0 - pl;
            Pval15Into(txtUp, pu, true);
            Pval15Into(txtLp, pl, true);
            double p = CdblTxt(pu < pl ? pval15(pu, true) : pval15(pl, true));
            Pval15Into(txt2p, 2.0 * p, true);
            lastCalculationAsString = "P(z " + txtPdf.Text.Trim() + ") = " + pval15(pu, true) + " upper,  " + pval15(pl, true) + " lower,  " + pval15(2.0 * p, true) + " two sided";
        }

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

        private void XFromP(double P, int idx)
        {
            if (pnlDf.Visible && CdblTxt(txtDf.Text) < 1)
                txtDf.Text = "1";

            switch (selectedTest)
            {
                case DistributionType.Z:
                    ZFromP(P);
                    break;
                case DistributionType.T:
                    TFromP(P);
                    break;
                case DistributionType.F:
                    FFromP(P);
                    break;
                case DistributionType.ChiSq:
                    ChiSqFromP(P);
                    break;
                case DistributionType.Q:
                    QFromP(P);
                    break;
                case DistributionType.Poisson:
                    InvPoisson(idx, P, Parsing.Cint_Txt(txtDf.Text));
                    lastCalculationAsString = "Poisson mean(P of " + txtDf.Text.Trim() + " events " + pval15(CdblTxt(txtLp.Text), true) + ", fewer " + pval15(CdblTxt(txtUp.Text), true) + ", more " + pval15(CdblTxt(txt2p.Text), true) + ") = " + txtDf2.Text.Trim();
                    break;
                case DistributionType.Kendall:
                    KendallFromP(P);
                    break;
                case DistributionType.Rho:
                    RhoFromP(P);
                    break;
                case DistributionType.NonCentralT:
                    NonCentralTFromP(P);
                    break;
            }
        }

        private void NonCentralTFromP(double P)
        {
            int flt;
            double x = ExFortran.tnct(CdblTxt(txtLp.Text), Parsing.Cint_Txt(txtDf.Text), CdblTxt(txtDf2.Text), out flt);
            Xval15Into(txtPdf, x, flt != 0);
            lastCalculationAsString = "non-central t(P " + pval15(P, true) + ", df " + txtDf.Text.Trim() + ", delta " + txtDf2.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        private void RhoFromP(double P)
        {
            double pu;
            int ix;
            int fault;
            int nx = Parsing.Cint_Txt(txtDf.Text);
            double rh = MathDbl.rhofromp(P, out pu, out ix, nx, out fault);
            if (fault == 0)
            {
                txtDf2.Text = ix.ToString();
                Xval15Into(txtPdf, rh);
                Pval15Into(txtUp, pu, false);
            }
            else
                txtUp.Text = Formatting.ERRR;
            lastCalculationAsString = "Hotelling T (upper tail P " + pval15(P, false) + ", n " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim();
        }

        private void KendallFromP(double P)
        {
            double pu;
            int ix;
            int fault;
            int nx = Parsing.Cint_Txt(txtDf.Text);
            double tau = MathDbl.taufromp(P, out pu, out ix, ref nx, out fault);
            if (fault == 0)
            {
                txtDf2.Text = ix.ToString();
                Xval15Into(txtPdf, tau);
                Pval15Into(txtUp, pu, false);
            }
            else
                txtUp.Text = Formatting.ERRR;
            lastCalculationAsString = "Kendall's T (upper tail P " + pval15(P, false) + ", n " + txtDf.Text.Trim() + ") = " + txtUp.Text.Trim();
        }

        private void QFromP(double P)
        {
            double x = PDF.quantsr(CdblTxt(txtLp.Text), CdblTxt(txtDf2.Text), CdblTxt(txtDf.Text));
            txtPdf.Text = x == Constant.MISSING ? Formatting.ERRR : Formatting.XRound(x, 7);
            lastCalculationAsString = "Q(upper P " + Formatting.XRound(P, 7) + ", df " + txtDf.Text.Trim() + ", samples " + txtDf2.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        private void ChiSqFromP(double P)
        {
            int fault;
            double x = PDF.ppchi2(CdblTxt(txtLp.Text), CdblTxt(txtDf.Text), out fault);
            Xval15Into(txtPdf, x, fault != 0);
            lastCalculationAsString = "chi-sq(upper P " + pval15(P, true) + ", df " + txtDf.Text.Trim() + ") = " +
                                      txtPdf.Text.Trim();
        }

        private void FFromP(double P)
        {
            double x = PDF.ffromp(CdblTxt(txtDf2.Text), CdblTxt(txtDf.Text), P);
            Xval15Into(txtPdf, x);
            lastCalculationAsString = "F(upper P " + pval15(P, true) + ", dfn " + txtDf.Text.Trim() + ", dfd " +
                                      txtDf2.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        private void TFromP(double P)
        {
            double x = PDF.tfromp(P, CdblTxt(txtDf.Text));
            Xval15Into(txtPdf, x);
            lastCalculationAsString = "t(upper P " + pval15(P, true) + ", df " + txtDf.Text.Trim() + ") = " + txtPdf.Text.Trim();
        }

        private void ZFromP(double P)
        {
            int fault;
            double x = PDF.gauinv(CdblTxt(txtLp.Text), out fault);
            Xval15Into(txtPdf, x, fault != 0);
            lastCalculationAsString = "z(upper P " + pval15(P, true) + ") = " + txtPdf.Text.Trim();
        }

        /* Utilities */

        private void InvPoisson(int idx, double P, int nl)
        {
            int ifault;
            double xmid = 0;
            double trm = 0;
            double plo = 0;
            double phi = 0;

            if (nl > 100000)
            {
                if (host.Query("This calculation can take a long time with large numbers.\r\n\r\nDo you wish to continue?", "StatsDirect Poisson Inverse"))
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

        private static void Xval15Tidy(TextBox txt)
        {
            txt.Text = Xval15(CdblTxt(txt.Text));
        }

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
            bool shouldReplace = true;
            try
            {
                double current = CdblTxt(txt.Text);
                if (Math.Abs(p - current) < 1e-14)
                    shouldReplace = false;
            }
            catch (Exception)
            {
                shouldReplace = true;
            }
            if (shouldReplace)
                txt.Text = pval15(p, allowZero);
        }

        private static string pval15(double p, bool allowZero)
        {
            if (p == Constant.MISSING || double.IsNaN(p))
                return Formatting.ERRR;
            if (!allowZero && p < 1e-15)
                return MINIMAL;
            if (p < Constant.EPSNEG)
                return p.ToString();
            if (p >= 0.999999999999999)
                return "1";
            return Formatting.XRound(p, 15);
        }

        private static double CdblTxt(string value)
        {
            return MINIMAL.Equals(value) ? Constant.EPSILON : Parsing.Cdbl_Txt(value);
        }

        private void FriendlyError(Exception ex)
        {
            lblError.Text = "StatsDirect couldn't calculate that function: " + ex.Message;
        }

        public Control Fill(ParameterBag outputParameters, bool doValidation)
        {
            outputParameters.AddOutput("gr", lastCalculationAsString);
            return null;
        }

        private void cmdInvert_Click(object sender, EventArgs e)
        {
            Invert(true);
        }
    }
}
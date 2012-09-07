using System;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    public partial class ctlPDF
    {
        enum Ed
        {
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

        public ctlPDF(DistributionOptions options, ITemplateHost host)
        {
            selectedTest = options.SelectedTest;
            this.host = host;
            InitializeComponent();
            SetVisibility();
            edpdf.Tag = Ed.Pdf;
            eddf.Tag = Ed.Df;
            eddf2.Tag = Ed.Df2;
            edlp.Tag = Ed.Lp;
            edup.Tag = Ed.Up;
            ed2p.Tag = Ed.P2;
        }

        private void Calc_Click(Object sender, EventArgs e)
        {
            Calculate();
        }

        private void DoubleClickTextbox(object sender, EventArgs e)
        {
            Control ctl = (Control)sender;
            Calc.Tag = ctl.Tag;
            Calculate();
        }

        private void EnterTextbox(object sender, EventArgs e)
        {
            Calc.Tag = ((Control)sender).Tag;
        }

        private void LeaveTextbox(object sender, EventArgs e)
        {
            Control ctl = (Control)sender;
            Calc.Tag = ctl.Tag;
            Calculate();
            ctl.Enabled = true;
        }

        private void edpdf_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                if (eddf.Visible)
                    SelectNextControl(edpdf, true, true, true, true);
                else
                    Calculate();
            }
        }

        private void eddf_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                if (eddf2.Visible)
                    SelectNextControl(eddf, true, true, true, true);
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
                DistributionType dt = (DistributionType)lbpdf.Tag;
                if (dt == DistributionType.Rho || dt == DistributionType.Kendall)
                    edpdf.Text = "";
            }
        }

        private void btn_lcl_Click(Object sender, EventArgs e)
        {
            try
            {
                if (CdblTxt(combo_cl.Text) >= 100.0)
                    combo_cl.Text = 99.99.ToString();
                if (CdblTxt(combo_cl.Text) <= 0.0)
                    combo_cl.Text = 0.01.ToString();
                double cl = CdblTxt(combo_cl.Text) / 100.0;
                double P = (1.0 - cl) / 2.0;
                edup.Text = P.ToString();
                CalculateUp();
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void btn_ucl_Click(Object sender, EventArgs e)
        {
            try
            {
                if (CdblTxt(combo_cl.Text) >= 100.0)
                    combo_cl.Text = 99.99.ToString();
                if (CdblTxt(combo_cl.Text) <= 0.0)
                    combo_cl.Text = 0.01.ToString();
                double cl = CdblTxt(combo_cl.Text) / 100.0;
                double P = (1.0 - cl) / 2.0;
                ed2p.Text = P.ToString();
                Calculate2p();
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void Calculate()
        {
            switch ((Ed)Calc.Tag)
            {
                case Ed.Pdf:
                    CalculatePdf();
                    break;
                case Ed.Df:
                    CalculateDf();
                    break;
                case Ed.Df2:
                    CalculateDf2();
                    break;
                case Ed.Lp:
                    CalculateLp();
                    break;
                case Ed.Up:
                    CalculateUp();
                    break;
                case Ed.P2:
                    Calculate2p();
                    break;
            }
        }

        private void Calculate2p()
        {
            try
            {
                if (inverseAvailable)
                {
                    double P = CdblTxt(ed2p.Text);
                    if (P < 0)
                        P = 0.0;
                    if (P > 1)
                        P = 1.0;
                    DistributionType dt = (DistributionType)lbpdf.Tag;
                    pval15Into(ed2p, P, AllowsZeroP(dt));
                    if (dt != DistributionType.Poisson)
                    {
                        P = P / 2.0;
                        if (P > 1.0 - P)
                            P = 1.0 - P;
                        pval15Into(edlp, 1.0 - P, AllowsZeroP(dt));
                        pval15Into(edup, P, AllowsZeroP(dt));
                    }
                    xFromP(P, 3);
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
                double df = CdblTxt(eddf.Text);
                DistributionType dt = (DistributionType)lbpdf.Tag;
                if ((dt == DistributionType.Rho || dt == DistributionType.Kendall) && edpdf.Text.Length > 0)
                {
                    int N;
                    if (dt == DistributionType.Rho)
                    {
                        N = ((int)(Math.Floor(df)));
                        double rh = CdblTxt(edpdf.Text);
                        if (N < 4 || rh < 0.0 || rh > 1.0)
                            eddf2.Text = Formatting.ERRR;
                        else
                            eddf2.Text = Convert.ToInt32(((1.0 - rh) * (N * (Math.Pow(N, 2) - 1))) / 6).ToString();
                        pfromx();
                    }
                    else
                    {
                        N = Convert.ToInt32(df);
                        double tau = CdblTxt(edpdf.Text);
                        if (N < 4 || tau < 0.0 || tau > 1.0)
                            eddf2.Text = Formatting.ERRR;
                        else
                            eddf2.Text = Convert.ToInt32(tau * (N * (N - 1) / 2.0)).ToString();
                        pfromx();
                    }
                }
                else
                {
                    if (df < 0)
                    {
                        df = 0;
                    }
                    xval15Into(eddf, df);
                    if (eddf2.Visible && eddf2.Text.Length == 0)
                        eddf2.Focus();
                    else
                        pfromx();
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
                DistributionType dt = (DistributionType)lbpdf.Tag;
                if (dt != DistributionType.Poisson && dt != DistributionType.NonCentralT)
                {
                    int df = Parsing.Cint_Txt(eddf2.Text);
                    if (dt != DistributionType.Binomial)
                    {
                        if (df < 1)
                            df = 1;
                    }
                    eddf2.Text = df.ToString();
                }
                else
                    xval15Tidy(eddf2);
                pfromx();
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
                    double P = CdblTxt(edlp.Text);
                    if (P < 0.0)
                        P = 0.0;
                    if (P > 1.0)
                        P = 1.0;
                    DistributionType dt = (DistributionType)lbpdf.Tag;
                    pval15Into(edlp, P, AllowsZeroP(dt));
                    pval15Into(edup, 1.0 - P, AllowsZeroP(dt));
                    double p2;
                    if (P > 1.0 - P)
                        p2 = 1.0 - P;
                    else
                        p2 = P;
                    p2 = 2.0 * p2;
                    pval15Into(ed2p, p2, AllowsZeroP(dt));
                    xFromP(1.0 - P, 1);
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
                xval15Tidy(edpdf);
                if (eddf.Visible && eddf.Text.Length == 0)
                    eddf.Focus();
                else
                    pfromx();
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
                if (inverseAvailable)
                {
                    double P = CdblTxt(edup.Text);
                    if (P < 0.0)
                        P = 0.0;
                    if (P > 1.0)
                        P = 1.0;
                    DistributionType dt = (DistributionType)lbpdf.Tag;
                    pval15Into(edup, P, AllowsZeroP(dt));
                    if (dt != DistributionType.Poisson)
                    {
                        pval15Into(edlp, 1.0 - P, AllowsZeroP(dt));
                        double P2 = 2.0 * (P > 1.0 - P ? 1.0 - P : P);
                        pval15Into(ed2p, P2, AllowsZeroP(dt));
                    }
                    xFromP(P, 2);
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private bool AllowsZeroP(DistributionType dt)
        {
            return !(dt == DistributionType.Rho || dt == DistributionType.Kendall);
        }

        private void pfromx()
        {
            int fault;
            int nx;
            int ix = 0;
            double pl;
            double pu = 0;
            double plo = 0;
            double phi = 0;
            double dterm = 0;
            double term = 0;
            double rh = 0;
            double dplo = 0;
            double dphi = 0;
            string x1;

            switch (selectedTest)
            {
                case DistributionType.Z:
                    pl = PDF.alnorm(CdblTxt(edpdf.Text));
                    pu = 1.0 - pl;
                    pval15Into(edup, pu, true);
                    pval15Into(edlp, pl, true);
                    double p = double.Parse(pu < pl ? pval15(pu, true) : pval15(pl, true));
                    pval15Into(ed2p, 2.0 * p, true);
                    lastCalculationAsString = "P(z " + edpdf.Text.Trim() + ") = " + pval15(pu, true) + " upper,  " + pval15(pl, true) + " lower,  " + pval15(2.0 * p, true) + " two sided";
                    break;
                case DistributionType.T:
                    pu = PDF.tvalp(CdblTxt(edpdf.Text), CdblTxt(eddf.Text));
                    pval15Into(edup, pu, true);
                    pl = 1.0 - pu;
                    pval15Into(edlp, pl, true);
                    if (pu > pl)
                        pu = pl;
                    pu = 2.0 * double.Parse(pval15(pu, true));
                    pval15Into(ed2p, pu, true);
                    lastCalculationAsString = "P(t " + edpdf.Text.Trim() + ", df " + eddf.Text.Trim() + ") = " + edup.Text.Trim() + " upper, " + edlp.Text.Trim() + " lower, " + ed2p.Text.Trim() + " two sided";
                    break;
                case DistributionType.F:
                    pu = PDF.fvalp(CdblTxt(edpdf.Text), CdblTxt(eddf.Text), CdblTxt(eddf2.Text));
                    pval15Into(edup, pu, true);
                    lastCalculationAsString = "P(F " + edpdf.Text.Trim() + ", dfn " + eddf.Text.Trim() + ", dfd " + eddf2.Text.Trim() + ") = " + edup.Text.Trim() + " upper";
                    break;
                case DistributionType.ChiSq:
                    double xtmp = PDF.chivalp(CdblTxt(edpdf.Text), CdblTxt(eddf.Text));
                    pval15Into(edup, xtmp, true);
                    pval15Into(ed2p, 2.0 * CdblTxt(edup.Text), true);
                    lastCalculationAsString = "P(chi-sq " + edpdf.Text.Trim() + ", df " + eddf.Text.Trim() + ") = " + edup.Text.Trim() + " upper tail";
                    break;
                case DistributionType.Q:
                    pu = PDF.probsr(CdblTxt(edpdf.Text), CdblTxt(eddf2.Text), CdblTxt(eddf.Text));
                    if (pu == Constant.MISSING)
                    {
                        edup.Text = Formatting.ERRR;
                        edlp.Text = Formatting.ERRR;
                    }
                    else
                    {
                        edlp.Text = Formatting.XRound(pu, 7);
                        edup.Text = Formatting.XRound(1.0 - pu, 7);
                    }
                    lastCalculationAsString = "P(Q " + edpdf.Text.Trim() + ", df " + eddf.Text.Trim() + ", samples " + eddf2.Text.Trim() + ") = " + edup.Text.Trim() + " upper,  " + edlp.Text.Trim() + " lower";
                    break;
                case DistributionType.Binomial:
                    double pud = CdblTxt(edpdf.Text);
                    int n = Parsing.Cint_Txt(eddf.Text);
                    int r = Parsing.Cint_Txt(eddf2.Text);
                    if (pud < 0 || pud > 1.0 || n < 1 || r > n)
                        fault = -1;
                    else
                    {
                        x1 = "Probability of observing ";
                        string x2 = r.ToString() + " successes";
                        lblp.Text = x1 + "=" + x2;
                        lbup.Text = x1 + ">=" + x2;
                        lb2p.Text = x1 + "<=" + x2;
                        ExFortran.bino(n, pud, r, out dterm, out dplo, out dphi, out fault);
                    }
                    if (fault != 0)
                    {
                        edlp.Text = Formatting.ERRR;
                        edup.Text = Formatting.ERRR;
                        ed2p.Text = Formatting.ERRR;
                    }
                    else
                    {
                        pval15Into(edlp, dterm, true);
                        pval15Into(edup, dphi, true);
                        pval15Into(ed2p, dplo, true);
                    }
                    lastCalculationAsString = "P(binomial p " + edpdf.Text.Trim() + ", " + eddf.Text.Trim() + " trials) = " + edlp.Text.Trim() + " [" + eddf2.Text.Trim() + " successes], "
                        + edup.Text.Trim() + " [>=" + eddf2.Text.Trim() + " successes], " + ed2p.Text.Trim() + " [<=" + eddf2.Text.Trim() + " successes]";
                    break;
                case DistributionType.Poisson:
                    int nl = Parsing.Cint_Txt(eddf.Text);
                    double M = CdblTxt(eddf2.Text);
                    if (nl < 0 || M < 0.0)
                        fault = -1;
                    else
                    {
                        x1 = "Probability of " + nl.ToString();
                        lblp.Text = x1 + " events";
                        lbup.Text = x1 + " or more events";
                        lb2p.Text = x1 + " or fewer events";
                        ExFortran.poisson(M, nl, out phi, out plo, out term, out fault);
                    }
                    if (fault != 0)
                    {
                        edlp.Text = Formatting.ERRR;
                        edup.Text = Formatting.ERRR;
                        ed2p.Text = Formatting.ERRR;
                    }
                    else
                    {
                        pval15Into(edlp, term, true);
                        pval15Into(edup, phi, true);
                        pval15Into(ed2p, plo, true);
                    }
                    lastCalculationAsString = "P(Poisson n " +
                               eddf.Text.Trim() + ", µ " +
                               eddf2.Text.Trim() +
                               ") = " +
                               edlp.Text.Trim() + " for n events,  " +
                               edup.Text.Trim() +
                               " for n or more events,  " +
                               ed2p.Text.Trim()
                               + " for n or fewer events";

                    break;
                case DistributionType.Kendall:
                    fault = 0;
                    nx = Parsing.Cint_Txt(eddf.Text);
                    double tau;
                    if (edpdf.Text.Length > 0)
                    {
                        tau = CdblTxt(edpdf.Text);
                        if (nx > 0 & rh <= 1)
                        {
                            ix = Convert.ToInt32(tau * (nx * (nx - 1) / 2.0));
                            eddf2.Text = ix.ToString();
                        }
                        else
                            eddf2.Text = Formatting.ERRR;
                    }
                    else
                    {
                        ix = Parsing.Cint_Txt(eddf2.Text);
                        tau = ix / (nx * (nx - 1) / 2.0);
                        xval15Into(edpdf, tau);
                    }
                    if (eddf2.Text == Formatting.ERRR | nx < 1)
                        fault = -1;
                    else
                        pu = MathDbl.kendp(ix, nx, ref fault);
                    pval15Into(edup, pu, false, fault != 0);
                    lastCalculationAsString = "P(Kendall's T " + ix.ToString() + ", n " + nx.ToString() + ") = " +
                               edup.Text.Trim() +
                               " upper tail";
                    break;
                case DistributionType.Rho:
                    nx = Parsing.Cint_Txt(eddf.Text);
                    if (edpdf.Text.Length > 0)
                    {
                        rh = CdblTxt(edpdf.Text);
                        if (nx >= 4 & rh <= 1)
                        {
                            ix = Convert.ToInt32(((1.0 - rh) * (nx * (nx * nx - 1))) / 6);
                            eddf2.Text = ix.ToString();
                        }
                        else
                            eddf2.Text = Formatting.ERRR;
                    }
                    else
                    {
                        ix = ((int)(CdblTxt(eddf2.Text)));
                        rh = 1.0 - ix / ((nx * (nx * nx - 1)) / 6.0);
                        xval15Into(edpdf, rh);
                    }
                    if (eddf2.Text == Formatting.ERRR | nx < 4)
                        fault = -1;
                    else
                        pu = 1.0 - ExFortran.prho(nx, ix, out fault);
                    pval15Into(edup, pu, false, fault != 0);
                    lastCalculationAsString = "P(Hotelling T " + ix.ToString() + ", n " + nx.ToString() + ") = " +
                               edup.Text.Trim() +
                               " upper tail";

                    break;
                case DistributionType.NonCentralT:
                    int flt;
                    p = ExFortran.pnct(CdblTxt(edpdf.Text), Parsing.Cint_Txt(eddf.Text),
                                       CdblTxt(eddf2.Text), out flt);
                    if (flt != 0)
                    {
                        edup.Text = Formatting.ERRR;
                        edlp.Text = Formatting.ERRR;
                    }
                    else
                    {
                        pval15Into(edlp, p, true);
                        pval15Into(edup, 1.0 - p, true);
                    }
                    lastCalculationAsString = "P(non-central t < " +
                               edpdf.Text.Trim() +
                               ", df " +
                               eddf.Text.Trim() + ", delta " +
                               eddf2.Text.Trim() +
                               ") = " +
                               edup.Text.Trim() + " upper, " +
                                edlp.Text.Trim()
                               + " lower";

                    break;
            }
        }

        private void SaveClick(Object sender, EventArgs e)
        {
            try
            {
                string toOutput = @"{\rtf1\ansi " + lastCalculationAsString.Trim() + @"\par}";
                host.OutputReport(toOutput, null, null, null);
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void SetVisibility()
        {
            string ti;

            lbpdf.Tag = selectedTest;
            edpdf.Text = "";
            eddf.Text = "";
            eddf2.Text = "";
            edlp.Text = "";
            edup.Text = "";
            ed2p.Text = "";
            label_cl.Visible = false;
            combo_cl.Visible = false;
            btn_ucl.Visible = false;
            btn_lcl.Visible = false;

            lbdf.Text = "Degrees of freedom";
            lblp.Text = "Lower tail P";
            lbup.Text = "Upper tail P";
            lb2p.Text = "Two tailed P";
            eddf2.Visible = false;
            lbdf2.Visible = false;
            lbpdf.Visible = true;
            edpdf.Visible = true;
            const string d = " distribution";

            switch (selectedTest)
            {
                case DistributionType.Z:
                    inverseAvailable = true;
                    ti = "Standard normal (Gaussian)" + d;
                    Text = ti;
                    lblp.Visible = true;
                    edlp.Visible = true;
                    lbdf.Visible = false;
                    eddf.Visible = false;
                    lbpdf.Text = "Normal deviate (z)";
                    lb2p.Visible = true;
                    ed2p.Visible = true;

                    break;
                case DistributionType.T:
                    inverseAvailable = true;
                    ti = "Student's t" + d;
                    Text = ti;
                    lblp.Visible = true;
                    edlp.Visible = true;
                    lbdf.Visible = true;
                    eddf.Visible = true;
                    ed2p.Visible = true;
                    lb2p.Visible = true;
                    lbpdf.Text = "Student's t";

                    break;
                case DistributionType.F:
                    inverseAvailable = true;
                    ti = "F (variance ratio)" + d;
                    Text = ti;
                    lbdf.Text = "Numerator degrees of freedom";
                    lbdf2.Text = "Denominator degrees of freedom";
                    lbup.Text = "Upper tail P";
                    lbdf.Visible = true;
                    eddf.Visible = true;
                    lbdf2.Visible = true;
                    eddf2.Visible = true;
                    lblp.Visible = false;
                    edlp.Visible = false;
                    lb2p.Visible = false;
                    ed2p.Visible = false;
                    lbpdf.Text = "Variance ratio F";

                    break;
                case DistributionType.ChiSq:
                    inverseAvailable = true;
                    ti = "Chi-Square" + d;
                    Text = ti;
                    lblp.Visible = false;
                    edlp.Visible = false;
                    lbdf.Visible = true;
                    eddf.Visible = true;
                    lbpdf.Text = "Chi-square";
                    lb2p.Visible = false;
                    ed2p.Visible = false;

                    break;
                case DistributionType.Q:
                    inverseAvailable = true;
                    ti = "Studentized range" + d;
                    Text = ti;
                    lbpdf.Text = "Studentized range Q";
                    lbdf2.Text = "Number of samples";
                    lbdf.Visible = true;
                    lblp.Visible = true;
                    edlp.Visible = true;
                    eddf.Visible = true;
                    lbdf2.Visible = true;
                    eddf2.Visible = true;
                    lbdf.Visible = true;
                    eddf.Visible = true;
                    lb2p.Visible = false;
                    ed2p.Visible = false;

                    break;
                case DistributionType.Binomial:
                    inverseAvailable = false;
                    ti = "Binomial" + d;
                    Text = ti;
                    lbpdf.Text = "Probability of success per trial";
                    lbdf.Text = "Number of trials";
                    lbdf2.Text = "Number of successes";
                    lblp.Text = "";
                    lbup.Text = "";
                    lb2p.Text = "";
                    lbdf.Visible = true;
                    eddf.Visible = true;
                    lbdf2.Visible = true;
                    eddf2.Visible = true;
                    lblp.Visible = true;
                    edlp.Visible = true;
                    lb2p.Visible = true;
                    ed2p.Visible = true;
                    edpdf.Text = ".5";

                    break;
                case DistributionType.Poisson:
                    inverseAvailable = true;
                    ti = "Poisson" + d;
                    Text = ti;
                    lbpdf.Visible = false;
                    edpdf.Visible = false;
                    lbdf.Text = "Number of random events";
                    lbdf2.Text = "Mean";
                    lblp.Text = "";
                    lbup.Text = "";
                    lb2p.Text = "";
                    lbdf.Visible = true;
                    eddf.Visible = true;
                    lbdf2.Visible = true;
                    eddf2.Visible = true;
                    lblp.Visible = true;
                    edlp.Visible = true;
                    lb2p.Visible = true;
                    ed2p.Visible = true;
                    label_cl.Visible = true;
                    combo_cl.Visible = true;
                    btn_ucl.Visible = true;
                    btn_lcl.Visible = true;
                    break;
                case DistributionType.Kendall:
                    inverseAvailable = true;
                    ti = "Kendall's tau";
                    lbpdf.Text = ti;
                    ti = ti + d;
                    Text = ti;
                    lbdf2.Text = "S (Nc - Nd)";
                    lbdf.Text = "Sample size";
                    lbdf.Visible = true;
                    lbdf2.Visible = true;
                    eddf2.Visible = true;
                    eddf.Visible = true;
                    lblp.Visible = false;
                    lb2p.Visible = false;
                    edlp.Visible = false;
                    ed2p.Visible = false;
                    lbup.Text = "Upper tail P";

                    break;
                case DistributionType.Rho:
                    inverseAvailable = true;
                    ti = "Spearman's rho";
                    lbpdf.Text = ti;
                    ti = ti + d;
                    Text = ti;
                    lbdf2.Text = "Hotelling-Pabst T";
                    lbdf.Text = "Sample size";
                    lbdf.Visible = true;
                    lbdf2.Visible = true;
                    eddf2.Visible = true;
                    eddf.Visible = true;
                    lblp.Visible = false;
                    lb2p.Visible = false;
                    edlp.Visible = false;
                    ed2p.Visible = false;
                    lbup.Text = "Upper tail P";

                    break;
                case DistributionType.NonCentralT:
                    inverseAvailable = true;
                    ti = "Non-central t" + d;
                    Text = ti;
                    lbpdf.Text = "Non-central t";
                    lbdf.Text = "Degrees of freedom";
                    lbdf2.Text = "Non-centrality";
                    lblp.Text = "P for t <= t";
                    lbup.Text = "P for t > t";
                    lbdf.Visible = true;
                    lblp.Visible = true;
                    edlp.Visible = true;
                    eddf.Visible = true;
                    lbdf2.Visible = true;
                    eddf2.Visible = true;
                    lbdf.Visible = true;
                    eddf.Visible = true;
                    lb2p.Visible = false;
                    ed2p.Visible = false;
                    break;
            }

            edlp.Enabled = inverseAvailable && !(selectedTest == DistributionType.Poisson);
            edup.Enabled = inverseAvailable;
            ed2p.Enabled = inverseAvailable;
        }

        private void xFromP(double P, int idx)
        {
            int nx;
            int ix;
            double x;
            double pu;

            int fault;
            if (eddf.Visible && CdblTxt(eddf.Text) < 1)
                eddf.Text = "1";

            switch (selectedTest)
            {
                case DistributionType.Z:
                    x = PDF.gauinv(CdblTxt(edlp.Text), out fault);
                    xval15Into(edpdf, x, fault != 0);
                    lastCalculationAsString = "z(upper P " + pval15(P, true) + ") = " + edpdf.Text.Trim();
                    break;
                case DistributionType.T:
                    x = PDF.tfromp(P, CdblTxt(eddf.Text));
                    xval15Into(edpdf, x);
                    lastCalculationAsString = "t(upper P " + pval15(P, true) + ", df " + eddf.Text.Trim() + ") = " + edpdf.Text.Trim();
                    break;
                case DistributionType.F:
                    x = PDF.ffromp(CdblTxt(eddf2.Text), CdblTxt(eddf.Text), P);
                    xval15Into(edpdf, x);
                    lastCalculationAsString = "F(upper P " + pval15(P, true) + ", dfn " + eddf.Text.Trim() + ", dfd " + eddf2.Text.Trim() + ") = " + edpdf.Text.Trim();
                    break;
                case DistributionType.ChiSq:
                    x = PDF.ppchi2(CdblTxt(edlp.Text), CdblTxt(eddf.Text), out fault);
                    xval15Into(edpdf, x, fault != 0);
                    lastCalculationAsString = "chi-sq(upper P " + pval15(P, true) + ", df " + eddf.Text.Trim() + ") = " + edpdf.Text.Trim();
                    break;
                case DistributionType.Q:
                    x = PDF.quantsr(CdblTxt(edlp.Text), CdblTxt(eddf2.Text), CdblTxt(eddf.Text));
                    edpdf.Text = x == Constant.MISSING ? Formatting.ERRR : Formatting.XRound(x, 7);
                    lastCalculationAsString = "Q(upper P " + Formatting.XRound(P, 7) + ", df " + eddf.Text.Trim() + ", samples " + eddf2.Text.Trim() + ") = " + edpdf.Text.Trim();
                    break;
                case DistributionType.Poisson:
                    InvPoisson(idx, P, Parsing.Cint_Txt(eddf.Text));
                    lastCalculationAsString = "Poisson mean(P of " + eddf.Text.Trim() + " events " + pval15(CdblTxt(edlp.Text), true) + ", fewer " + pval15(CdblTxt(edup.Text), true) + ", more " + pval15(CdblTxt(ed2p.Text), true) + ") = " + eddf2.Text.Trim();
                    break;
                case DistributionType.Kendall:
                    nx = Parsing.Cint_Txt(eddf.Text);
                    double tau = MathDbl.taufromp(P, out pu, out ix, ref nx, out fault);
                    if (fault == 0)
                    {
                        eddf2.Text = ix.ToString();
                        xval15Into(edpdf, tau);
                        pval15Into(edup, pu, false);
                    }
                    else
                        edup.Text = Formatting.ERRR;
                    lastCalculationAsString = "Kendall's T (upper tail P " + pval15(P, false) + ", n " + eddf.Text.Trim() + ") = " + edup.Text.Trim();
                    break;
                case DistributionType.Rho:
                    nx = Parsing.Cint_Txt(eddf.Text);
                    double rh = MathDbl.rhofromp(P, out pu, out ix, nx, out fault);
                    if (fault == 0)
                    {
                        eddf2.Text = ix.ToString();
                        xval15Into(edpdf, rh);
                        pval15Into(edup, pu, false);
                    }
                    else
                        edup.Text = Formatting.ERRR;
                    lastCalculationAsString = "Hotelling T (upper tail P " + pval15(P, false) + ", n " + eddf.Text.Trim() + ") = " + edup.Text.Trim();
                    break;
                case DistributionType.NonCentralT:
                    int flt;
                    x = ExFortran.tnct(CdblTxt(edlp.Text), Parsing.Cint_Txt(eddf.Text),
                                       CdblTxt(eddf2.Text), out flt);
                    xval15Into(edpdf, x, flt != 0);
                    lastCalculationAsString = "non-central t(P " + pval15(P, true) + ", df " + eddf.Text.Trim() + ", delta " + eddf2.Text.Trim() + ") = " + edpdf.Text.Trim();
                    break;
            }
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
                pval15Into(edlp, trm, true);
                pval15Into(edup, phi, true);
                pval15Into(ed2p, plo, true);
                eddf2.Text = Formatting.XRound(xmid, 15);
            }
            else
            {
                edlp.Text = Formatting.ERRR;
                edup.Text = Formatting.ERRR;
                ed2p.Text = Formatting.ERRR;
                eddf2.Text = Formatting.ERRR;
            }

        }

        private void xval15Into(TextBox txt, double x, bool isError)
        {
            if (isError)
                txt.Text = Formatting.ERRR;
            else
                xval15Into(txt, x);
        }

        private void xval15Into(TextBox txt, double x)
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
                txt.Text = xval15(x);
        }

        private void xval15Tidy(TextBox txt)
        {
            txt.Text = xval15(CdblTxt(txt.Text));
        }

        private string xval15(double x)
        {
            return Formatting.XRound(x, 15);
        }

        private void pval15Into(TextBox txt, double P, bool allowZero, bool isError)
        {
            if (isError)
                txt.Text = Formatting.ERRR;
            else
                pval15Into(txt, P, allowZero);
        }

        private void pval15Into(TextBox txt, double P, bool allowZero)
        {
            bool shouldReplace = true;
            try
            {
                double current = CdblTxt(txt.Text);
                if (Math.Abs(P - current) < 1e-14)
                    shouldReplace = false;
            }
            catch (Exception)
            {
                shouldReplace = true;
            }
            if (shouldReplace)
                txt.Text = pval15(P, allowZero);
        }

        private string pval15(double P, bool allowZero)
        {
            if (P == Constant.MISSING || double.IsNaN(P))
                return Formatting.ERRR;
            if (!allowZero && P < 1e-15)
                return MINIMAL;
            if (P < Constant.EPSNEG)
                return P.ToString();
            if (P >= 0.999999999999999)
                return "1";
            return Formatting.XRound(P, 15);
        }

        private double CdblTxt(string value)
        {
            if (MINIMAL.Equals(value))
                return Constant.EPSILON;
            return Parsing.Cdbl_Txt(value);
        }

        private void FriendlyError(Exception ex)
        {
            SDApplication.SoleInstance.FriendlyError("StatsDirect couldn't calculate that function", ex, false);
        }
    }
}
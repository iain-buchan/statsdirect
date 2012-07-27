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
        private readonly int selectedTest;
        private bool inv;
        private string tempsave;
        private bool holdit;
        private readonly ITemplateHost host;

        public ctlPDF(DistributionOptions options, ITemplateHost host)
        {
            selectedTest = options.SelectedTest;
            this.host = host;
            InitializeComponent();
            btn_lcl.Click += btn_lcl_Click;
            btn_ucl.Click += btn_ucl_Click;
            Calc.Click += Calc_Click;
            ed2p.DoubleClick += ed2p_DblClick;
            ed2p.GotFocus += ed2p_GotFocus;
            ed2p.LostFocus += ed2p_LostFocus;
            eddf.DoubleClick += eddf_dblclick;
            eddf.GotFocus += eddf_GotFocus;
            eddf.KeyPress += eddf_KeyPress;
            eddf.LostFocus += eddf_LostFocus;
            eddf2.DoubleClick += eddf2_DblClick;
            eddf2.GotFocus += eddf2_GotFocus;
            eddf2.KeyPress += eddf2_KeyPress;
            eddf2.LostFocus += eddf2_LostFocus;
            edlp.DoubleClick += edlp_DblClick;
            edlp.GotFocus += edlp_GotFocus;
            edlp.LostFocus += edlp_LostFocus;
            edpdf.DoubleClick += edpdf_DblClick;
            edpdf.GotFocus += edpdf_GotFocus;
            edpdf.KeyPress += edpdf_KeyPress;
            edpdf.LostFocus += edpdf_LostFocus;
            edup.DoubleClick += edup_DblClick;
            edup.GotFocus += edup_GotFocus;
            edup.LostFocus += edup_LostFocus;
            Save.Click += SaveClick;
            SetVisibility();
        }

        private void InvPoisson(int idx, double P, int nl)
        {
            int ifault;
            double xmid = 0;
            double trm = 0;
            double plo = 0;
            double phi = 0;

            if (nl > 100000)
            {
                if (
                    host.Query(
                        "This calculation can take a long time with large numbers." + "\r\n" + "\r\n" +
                        "Do you wish to continue?", "StatsDirect Poisson Inverse"))
                    ExFortran.poissoni(idx, P, out xmid, out trm, out phi, out plo, nl, out ifault);
                else
                    ifault = 4;
            }
            else
                ExFortran.poissoni(idx, P, out xmid, out trm, out phi, out plo, nl, out ifault);

            if (ifault == 0)
            {
                edlp.Text = pval15(trm);
                edup.Text = pval15(phi);
                ed2p.Text = pval15(plo);
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


        public string xval15(double x)
        {
            return Formatting.XRound(x, 15);
        }


        private void btn_lcl_Click(Object sender, EventArgs e)
        {
            try
            {
                if (Parsing.Cdbl_Txt(combo_cl.Text) >= 100.0)
                {
                    combo_cl.Text = 99.99.ToString();
                }
                if (Parsing.Cdbl_Txt(combo_cl.Text) <= 0.0)
                {
                    combo_cl.Text = 0.01.ToString();
                }
                double cl = Parsing.Cdbl_Txt(combo_cl.Text) / 100.0;
                double P = (1.0 - cl) / 2.0;
                edup.Text = P.ToString();
                edup_DblClick(null, EventArgs.Empty);
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
                if (Parsing.Cdbl_Txt(combo_cl.Text) >= 100.0)
                {
                    combo_cl.Text = 99.99.ToString();
                }
                if (Parsing.Cdbl_Txt(combo_cl.Text) <= 0.0)
                {
                    combo_cl.Text = 0.01.ToString();
                }
                double cl = Parsing.Cdbl_Txt(combo_cl.Text) / 100.0;
                double P = (1.0 - cl) / 2.0;
                ed2p.Text = P.ToString();
                ed2p_DblClick(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }


        private void Calc_Click(Object sender, EventArgs e)
        {

            switch (Convert.ToString(Calc.Tag))
            {
                case "edpdf":
                    edpdf_DblClick(null, EventArgs.Empty);
                    break;
                case "eddf":
                    eddf_dblclick(null, EventArgs.Empty);
                    break;
                case "eddf2":
                    eddf2_DblClick(null, EventArgs.Empty);
                    break;
                case "edlp":
                    edlp_DblClick(null, EventArgs.Empty);
                    break;
                case "edup":
                    edup_DblClick(null, EventArgs.Empty);
                    break;
                case "ed2p":
                    ed2p_DblClick(null, EventArgs.Empty);
                    break;
            }

        }


        private void ed2p_DblClick(object sender, EventArgs e)
        {
            try
            {
                if (inv)
                {
                    double P = Parsing.Cdbl_Txt(ed2p.Text);
                    if (P < 0)
                    {
                        P = 0.0;
                    }
                    if (P > 1)
                    {
                        P = 1.0;
                    }
                    ed2p.Text = pval15(P);
                    if (Convert.ToString(lbpdf.Tag) != "p")
                    {
                        P = P / 2.0;
                        if (P > 1.0 - P)
                        {
                            P = 1.0 - P;
                        }
                        edlp.Text = pval15(1.0 - P);
                        edup.Text = pval15(P);
                    }
                    xFromP(P, 3);
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }


        private void ed2p_GotFocus(object sender, EventArgs e)
        {
            Calc.Tag = "ed2p";
        }


        private void ed2p_LostFocus(object sender, EventArgs e)
        {
            if (holdit)
            {
                return;
            }
            ed2p_DblClick(null, EventArgs.Empty);
            ed2p.Enabled = true;
        }


        private void eddf_dblclick(object sender, EventArgs e)
        {
            try
            {
                double df = Parsing.Cdbl_Txt(eddf.Text);
                if ((Convert.ToString(lbpdf.Tag) == "s" | Convert.ToString(lbpdf.Tag) == "k") & edpdf.Text.Length > 0)
                {
                    int N;
                    if (Convert.ToString(lbpdf.Tag) == "s")
                    {
                        N = ((int)(Math.Floor(df)));
                        double rh = Parsing.Cdbl_Txt(edpdf.Text);
                        if (N < 4 | rh < 0.0 | rh > 1.0)
                        {
                            eddf2.Text = Formatting.ERRR;
                        }
                        else
                        {
                            eddf2.Text = Convert.ToInt32(((1.0 - rh) * (N * (Math.Pow(N, 2) - 1))) / 6).ToString();
                        }
                        pfromx();
                    }
                    else
                    {
                        N = Convert.ToInt32(df);
                        double tau = Parsing.Cdbl_Txt(edpdf.Text);
                        if (N < 4 | tau < 0.0 | tau > 1.0)
                        {
                            eddf2.Text = Formatting.ERRR;
                        }
                        else
                        {
                            eddf2.Text = Convert.ToInt32(tau * (N * (N - 1) / 2.0)).ToString();
                        }
                        pfromx();
                    }
                }
                else
                {
                    if (df < 0)
                    {
                        df = 0;
                    }
                    eddf.Text = xval15(df);
                    if (eddf2.Visible && eddf2.Text.Length == 0)
                    {
                        eddf2.Focus();
                    }
                    else
                    {
                        pfromx();
                    }
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void eddf_GotFocus(object sender, EventArgs e)
        {
            Calc.Tag = "eddf";
        }

        private void eddf_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                if (eddf2.Visible)
                {
                    SelectNextControl(eddf, true, true, true, true);
                }
                else
                {
                    Calc_Click(null, EventArgs.Empty);
                }
            }
        }

        private void eddf_LostFocus(object sender, EventArgs e)
        {
            if (holdit)
            {
                return;
            }
            eddf_dblclick(null, EventArgs.Empty);
        }

        private void eddf2_DblClick(object sender, EventArgs e)
        {
            try
            {
                if (Convert.ToString(lbpdf.Tag) != "p" & Convert.ToString(lbpdf.Tag) != "nct")
                {
                    int df = Parsing.Cint_Txt(eddf2.Text);
                    if (Convert.ToString(lbpdf.Tag) != "b")
                    {
                        if (df < 1)
                        {
                            df = 1;
                        }
                    }
                    eddf2.Text = df.ToString();
                }
                else
                {
                    eddf2.Text = xval15(Parsing.Cdbl_Txt(eddf2.Text));
                }
                pfromx();
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void eddf2_GotFocus(object sender, EventArgs e)
        {
            Calc.Tag = "eddf2";
        }

        private void eddf2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                Calc_Click(null, EventArgs.Empty);
            }
            else
            {
                if (Convert.ToString(lbpdf.Tag) == "s" || Convert.ToString(lbpdf.Tag) == "k")
                {
                    edpdf.Text = "";
                }
            }
        }

        private void eddf2_LostFocus(object sender, EventArgs e)
        {

            if (holdit)
            {
                return;
            }
            eddf2_DblClick(null, EventArgs.Empty);
        }

        private void edlp_DblClick(object sender, EventArgs e)
        {
            try
            {
                if (inv)
                {
                    double P = Parsing.Cdbl_Txt(edlp.Text);
                    if (P < 0.0)
                    {
                        P = 0.0;
                    }
                    if (P > 1.0)
                    {
                        P = 1.0;
                    }
                    edlp.Text = pval15(P);
                    edup.Text = pval15(1.0 - P);
                    double p2;
                    if (P > 1.0 - P)
                    {
                        p2 = 1.0 - P;
                    }
                    else
                    {
                        p2 = P;
                    }
                    p2 = 2.0 * p2;
                    ed2p.Text = pval15(p2);
                    xFromP(1.0 - P, 1);
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void edlp_GotFocus(object sender, EventArgs e)
        {
            Calc.Tag = "edlp";
        }

        private void edlp_LostFocus(object sender, EventArgs e)
        {
            if (holdit)
            {
                return;
            }
            edlp_DblClick(null, EventArgs.Empty);
            edlp.Enabled = true;
        }


        private void edpdf_DblClick(object sender, EventArgs e)
        {
            try
            {
                edpdf.Text = xval15(Parsing.Cdbl_Txt(edpdf.Text));
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

        private void edpdf_GotFocus(object sender, EventArgs e)
        {
            Calc.Tag = "edpdf";
        }

        private void edpdf_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                if (eddf.Visible)
                    SelectNextControl(edpdf, true, true, true, true);
                else
                    Calc_Click(null, EventArgs.Empty);
            }
        }

        private void edpdf_LostFocus(object sender, EventArgs e)
        {
            if (holdit)
                return;
            edpdf_DblClick(null, EventArgs.Empty);
        }

        private void edup_DblClick(object sender, EventArgs e)
        {
            try
            {
                if (inv)
                {
                    double P = Parsing.Cdbl_Txt(edup.Text);
                    if (P < 0.0)
                        P = 0.0;
                    if (P > 1.0)
                        P = 1.0;
                    edup.Text = pval15(P);
                    if (Convert.ToString(lbpdf.Tag) != "p")
                    {
                        edlp.Text = pval15(1.0 - P);
                        double P2 = 2.0 * (P > 1.0 - P ? 1.0 - P : P);
                        ed2p.Text = pval15(P2);
                    }
                    xFromP(P, 2);
                }
            }
            catch (Exception ex)
            {
                FriendlyError(ex);
            }
        }

        private void edup_GotFocus(object sender, EventArgs e)
        {
            Calc.Tag = "edup";
        }

        private void edup_LostFocus(object sender, EventArgs e)
        {
            if (holdit)
                return;
            edup_DblClick(null, EventArgs.Empty);
            edup.Enabled = true;
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
                case 0:
                    pl = PDF.alnorm(Parsing.Cdbl_Txt(edpdf.Text));
                    pu = 1.0 - pl;
                    edup.Text = pval15(pu);
                    edlp.Text = pval15(pl);
                    double p = double.Parse(pu < pl ? pval15(pu) : pval15(pl));
                    ed2p.Text = pval15(2.0 * p);
                    tempsave = "P(z " + xval15(Parsing.Cdbl_Txt(edpdf.Text)) + ") = " + pval15(pu) + " upper,  " + pval15(pl) + " lower,  " + pval15(2.0 * p) + " two sided";
                    break;
                case 1:
                    pu = PDF.tvalp(Parsing.Cdbl_Txt(edpdf.Text), Parsing.Cdbl_Txt(eddf.Text));
                    edup.Text = pval15(pu);
                    pl = 1.0 - pu;
                    edlp.Text = pval15(pl);
                    if (pu > pl)
                        pu = pl;
                    pu = 2.0 * double.Parse(pval15(pu));
                    ed2p.Text = pval15(pu);
                    tempsave = "P(t " + edpdf.Text.Trim() + ", df " + eddf.Text.Trim() + ") = " + edup.Text.Trim() + " upper, " + edlp.Text.Trim() + " lower, " + ed2p.Text.Trim() + " two sided";
                    break;
                case 2:
                    pu = PDF.fvalp(Parsing.Cdbl_Txt(edpdf.Text), Parsing.Cdbl_Txt(eddf.Text), Parsing.Cdbl_Txt(eddf2.Text));
                    edup.Text = pval15(pu);
                    tempsave = "P(F " + edpdf.Text.Trim() + ", dfn " + eddf.Text.Trim() + ", dfd " + eddf2.Text.Trim() + ") = " + edup.Text.Trim() + " upper";
                    break;
                case 3:
                    double xtmp = PDF.chivalp(Parsing.Cdbl_Txt(edpdf.Text), Parsing.Cdbl_Txt(eddf.Text));
                    string qtmp = xtmp == Constant.MISSING ? Formatting.ERRR : pval15(xtmp);
                    edup.Text = qtmp;
                    ed2p.Text = pval15(2.0 * Parsing.Cdbl_Txt(edup.Text));
                    tempsave = "P(chi-sq " + edpdf.Text.Trim() + ", df " + eddf.Text.Trim() + ") = " + edup.Text.Trim() + " upper tail";
                    break;
                case 4:
                    pu = PDF.probsr(Parsing.Cdbl_Txt(edpdf.Text), Parsing.Cdbl_Txt(eddf2.Text), Parsing.Cdbl_Txt(eddf.Text));
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
                    tempsave = "P(Q " + edpdf.Text.Trim() + ", df " + eddf.Text.Trim() + ", samples " + eddf2.Text.Trim() + ") = " + edup.Text.Trim() + " upper,  " + edlp.Text.Trim() + " lower";
                    break;
                case 5:
                    double pud = Parsing.Cdbl_Txt(edpdf.Text);
                    int n = Parsing.Cint_Txt(eddf.Text);
                    int r = Parsing.Cint_Txt(eddf2.Text);
                    if (pud < 0 || pud > 1.0 || n < 1 || r > n)
                    {
                        fault = -1;
                    }
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
                        edlp.Text = pval15(dterm);
                        edup.Text = pval15(dphi);
                        ed2p.Text = pval15(dplo);
                    }
                    string transTemp20 = edpdf.Text;
                    string transTemp21 = eddf.Text;
                    string transTemp22 = edlp.Text;
                    string transTemp23 = eddf2.Text;
                    string transTemp24 = edup.Text;
                    string transTemp25 = eddf2.Text;
                    string transTemp26 = ed2p.Text;
                    string transTemp27 = eddf2.Text;
                    tempsave = "P(binomial p " + transTemp20.Trim() + ", " + transTemp21.Trim() + " trials) = " + transTemp22.Trim() + " [" + transTemp23.Trim() + " successes], " + transTemp24.Trim() + " [>=" + transTemp25.Trim() + " successes], " + transTemp26.Trim() + " [<=" + transTemp27.Trim() + " successes]";
                    break;
                case 6:
                    int nl = Parsing.Cint_Txt(eddf.Text);
                    double M = Parsing.Cdbl_Txt(eddf2.Text);
                    if (nl < 0 | M < 0.0)
                    {
                        fault = -1;
                    }
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
                        edlp.Text = pval15(term);
                        edup.Text = pval15(phi);
                        ed2p.Text = pval15(plo);
                    }
                    string transTemp28 = eddf.Text;
                    string transTemp29 = eddf2.Text;
                    string transTemp30 = edlp.Text;
                    string transTemp31 = edup.Text;
                    string transTemp32 = ed2p.Text;
                    tempsave = "P(Poisson n " +
                               transTemp28.Trim() + ", µ " +
                               transTemp29.Trim() +
                               ") = " +
                               transTemp30.Trim() + " for n events,  " +
                               transTemp31.Trim() +
                               " for n or more events,  " +
                               transTemp32.Trim()
                               + " for n or fewer events";

                    break;
                case 7:
                    fault = 0;
                    nx = Parsing.Cint_Txt(eddf.Text);
                    string transTemp33 = edpdf.Text;
                    double tau;
                    if (transTemp33.Length > 0)
                    {
                        tau = Parsing.Cdbl_Txt(edpdf.Text);
                        if (nx > 0 & rh <= 1)
                        {
                            ix = Convert.ToInt32(tau * (nx * (nx - 1) / 2.0));
                            eddf2.Text = ix.ToString();
                        }
                        else
                        {
                            eddf2.Text = Formatting.ERRR;
                        }
                    }
                    else
                    {
                        ix = Parsing.Cint_Txt(eddf2.Text);
                        tau = ix / (nx * (nx - 1) / 2.0);
                        edpdf.Text = xval15(tau);
                    }
                    if (eddf2.Text == Formatting.ERRR | nx < 1)
                    {
                        fault = -1;
                    }
                    else
                    {
                        pu = MathDbl.kendp(ix, nx, ref fault);
                    }
                    edup.Text = fault != 0 ? Formatting.ERRR : pval15(pu);
                    string transTemp34 = edup.Text;
                    tempsave = "P(Kendall's T " + ix.ToString() + ", n " + nx.ToString() + ") = " +
                               transTemp34.Trim() +
                               " upper tail";

                    break;
                case 8:
                    nx = Parsing.Cint_Txt(eddf.Text);
                    string transTemp35 = edpdf.Text;
                    if (transTemp35.Length > 0)
                    {
                        rh = Parsing.Cdbl_Txt(edpdf.Text);
                        if (nx >= 4 & rh <= 1)
                        {
                            ix = Convert.ToInt32(((1.0 - rh) * (nx * (nx * nx - 1))) / 6);
                            eddf2.Text = ix.ToString();
                        }
                        else
                        {
                            eddf2.Text = Formatting.ERRR;
                        }
                    }
                    else
                    {
                        ix = ((int)(Parsing.Cdbl_Txt(eddf2.Text)));
                        rh = 1.0 - ix / ((nx * (nx * nx - 1)) / 6.0);
                        edpdf.Text = xval15(rh);
                    }
                    if (eddf2.Text == Formatting.ERRR | nx < 4)
                    {
                        fault = -1;
                    }
                    else
                    {
                        pu = 1.0 - ExFortran.prho(nx, ix, out fault);
                    }
                    edup.Text = fault != 0 ? Formatting.ERRR : pval15(pu);
                    string transTemp36 = edup.Text;
                    tempsave = "P(Hotelling T " + ix.ToString() + ", n " + nx.ToString() + ") = " +
                               transTemp36.Trim() +
                               " upper tail";

                    break;
                case 9:
                    // fault = 0; 
                    int flt;
                    p = ExFortran.pnct(Parsing.Cdbl_Txt(edpdf.Text), Parsing.Cint_Txt(eddf.Text),
                                       Parsing.Cdbl_Txt(eddf2.Text), out flt);
                    if (flt != 0)
                    {
                        edup.Text = Formatting.ERRR;
                        edlp.Text = Formatting.ERRR;
                    }
                    else
                    {
                        edlp.Text = pval15(p);
                        edup.Text = pval15(1.0 - p);
                    }
                    string transTemp37 = edpdf.Text;
                    string transTemp38 = eddf.Text;
                    string transTemp39 = eddf2.Text;
                    string transTemp40 = edup.Text;
                    string transTemp41 = edlp.Text;
                    tempsave = "P(non-central t < " +
                               transTemp37.Trim() +
                               ", df " +
                               transTemp38.Trim() + ", delta " +
                               transTemp39.Trim() +
                               ") = " +
                               transTemp40.Trim() + " upper, " +
                                transTemp41.Trim()
                               + " lower";

                    break;
            }

            holdit = false;
        }


        private string pval15(double P)
        {
            if (P == Constant.MISSING)
                return Formatting.ERRR;
            if (P < Constant.EPSNEG)
                return P.ToString();
            if (P >= 0.999999999999999)
                return "1";
            return Formatting.XRound(P, 15);
        }


        private void SaveClick(Object sender, EventArgs e)
        {
            try
            {
                string toOutput = @"{\rtf1\ansi" + tempsave.Trim() + @"\par}";
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

            lbpdf.Tag = "";
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
                case 0:
                    inv = true;
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
                case 1:
                    inv = true;
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
                case 2:
                    inv = true;
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
                case 3:
                    inv = true;
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
                case 4:
                    inv = true;
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
                case 5:
                    inv = false;
                    lbpdf.Tag = "b";
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
                case 6:
                    inv = true;
                    lbpdf.Tag = "p";
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
                case 7:
                    inv = true;
                    lbpdf.Tag = "k";
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
                case 8:
                    inv = true;
                    lbpdf.Tag = "s";
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
                case 9:
                    inv = true;
                    lbpdf.Tag = "nct";
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


            edlp.Enabled = inv;
            if (Convert.ToString(lbpdf.Tag) == "p")
            {
                edlp.Enabled = false;
            }
            edup.Enabled = inv;
            ed2p.Enabled = inv;
        }


        private void xFromP(double P, int idx)
        {
            int nx;
            int ix;
            double x;
            double pu;

            int fault;
            if (eddf.Visible && Parsing.Cdbl_Txt(eddf.Text) < 1)
                eddf.Text = "1";

            switch (selectedTest)
            {
                case 0:
                    x = PDF.gauinv(Parsing.Cdbl_Txt(edlp.Text), out fault);
                    edpdf.Text = fault == 0 ? xval15(x) : Formatting.ERRR;
                    tempsave = "z(upper P " + pval15(P) + ") = " + edpdf.Text.Trim();

                    break;
                case 1:
                    x = PDF.tfromp(P, Parsing.Cdbl_Txt(eddf.Text));
                    edpdf.Text = xval15(x);
                    string transTemp43 = eddf.Text;
                    string transTemp44 = edpdf.Text;
                    tempsave = "t(upper P " + pval15(P) + ", df " +
                               transTemp43.Trim() +
                               ") = " +
                               transTemp44.Trim();

                    break;
                case 2:
                    x = PDF.ffromp(Parsing.Cdbl_Txt(eddf2.Text), Parsing.Cdbl_Txt(eddf.Text), P);
                    edpdf.Text = xval15(x);
                    tempsave = "F(upper P " + pval15(P) + ", dfn " + eddf.Text.Trim() + ", dfd " + eddf2.Text.Trim() + ") = " + edpdf.Text.Trim();

                    break;
                case 3:
                    x = PDF.ppchi2(Parsing.Cdbl_Txt(edlp.Text), Parsing.Cdbl_Txt(eddf.Text), out fault);
                    edpdf.Text = fault == 0 ? xval15(x) : Formatting.ERRR;
                    string transTemp48 = eddf.Text;
                    string transTemp49 = edpdf.Text;
                    tempsave = "chi-sq(upper P " + pval15(P) + ", df " +
                               transTemp48.Trim() +
                               ") = " +
                               transTemp49.Trim();

                    break;
                case 4:
                    x = PDF.quantsr(Parsing.Cdbl_Txt(edlp.Text), Parsing.Cdbl_Txt(eddf2.Text),
                                    Parsing.Cdbl_Txt(eddf.Text));
                    edpdf.Text = x == Constant.MISSING ? Formatting.ERRR : Formatting.XRound(x, 7);
                    string transTemp50 = eddf.Text;
                    string transTemp51 = eddf2.Text;
                    string transTemp52 = edpdf.Text;
                    tempsave = "Q(upper P " + Formatting.XRound(P, 7) + ", df " +
                               transTemp50.Trim() +
                               ", samples " +
                               transTemp51.Trim() + ") = " +
                               transTemp52.Trim();

                    break;
                case 6:
                    InvPoisson(idx, P, Parsing.Cint_Txt(eddf.Text));
                    tempsave = "Poisson mean(P of " + eddf.Text.Trim() + " events " + pval15(Parsing.Cdbl_Txt(edlp.Text)) + ", fewer " + pval15(Parsing.Cdbl_Txt(edup.Text)) + ", more " + pval15(Parsing.Cdbl_Txt(ed2p.Text)) + ") = " + eddf2.Text.Trim();
                    break;
                case 7:
                    nx = Parsing.Cint_Txt(eddf.Text);
                    double tau = MathDbl.taufromp(P, out pu, out ix, ref nx, out fault);
                    if (fault == 0)
                    {
                        eddf2.Text = ix.ToString();
                        edpdf.Text = xval15(tau);
                        edup.Text = pval15(pu);
                    }
                    else
                    {
                        edup.Text = Formatting.ERRR;
                    }
                    string transTemp55 = eddf.Text;
                    string transTemp56 = edup.Text;
                    tempsave = "Kendall's T (upper tail P " + pval15(P) + ", n " + transTemp55.Trim() + ") = " + transTemp56.Trim();

                    break;
                case 8:
                    nx = Parsing.Cint_Txt(eddf.Text);
                    double rh = MathDbl.rhofromp(P, out pu, out ix, nx, out fault);
                    if (fault == 0)
                    {
                        eddf2.Text = ix.ToString();
                        edpdf.Text = xval15(rh);
                        edup.Text = pval15(pu);
                    }
                    else
                    {
                        edup.Text = Formatting.ERRR;
                    }
                    string transTemp57 = eddf.Text;
                    string transTemp58 = edup.Text;
                    tempsave = "Hotelling T (upper tail P " + pval15(P) + ", n " +
                               transTemp57.Trim() +
                               ") = " +
                               transTemp58.Trim();

                    break;
                case 9:
                    int flt;
                    x = ExFortran.tnct(Parsing.Cdbl_Txt(edlp.Text), Parsing.Cint_Txt(eddf.Text),
                                       Parsing.Cdbl_Txt(eddf2.Text), out flt);
                    edpdf.Text = flt == 0 ? xval15(x) : Formatting.ERRR;
                    tempsave = "non-central t(P " + pval15(P) + ", df " + eddf.Text.Trim() + ", delta " +
                               eddf2.Text.Trim() + ") = " + edpdf.Text.Trim();

                    break;
            }

            holdit = false;
        }

        private void FriendlyError(Exception ex)
        {
            SDApplication.SoleInstance.FriendlyError("StatsDirect couldn't calculate that function", ex, false);
        }
    }
}
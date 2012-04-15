using System;
using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class frmCoxRegressionOptions : Form
    {
        private bool userCancelled;
        private ParameterBag parameterBag;

        public frmCoxRegressionOptions()
        {
            InitializeComponent();
            FillFormFromParameter();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            userCancelled = false;
            FillParameterFromForm();
            Close();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            userCancelled = true;
            Close();
        }

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        public ParameterBag ParameterBag
        {
            get { return parameterBag; }
        }

        private void FillFormFromParameter()
        {
            txtAccuracy.Text = 0.0000001.ToString();
        }

        private void FillParameterFromForm()
        {
            parameterBag = new ParameterBag();
            parameterBag.AddOutput("centre-continuous-covariates", chkCentre.Checked);
            parameterBag.AddOutput("accuracy", Utilities.Parsing.Cdbl_Txt(txtAccuracy.Text));
            parameterBag.AddOutput("splitting-ratio", cboStrata.Text);
        }
    }
}

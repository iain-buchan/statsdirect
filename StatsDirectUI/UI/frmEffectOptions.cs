using System;
using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class frmEffectOptions : Form
    {
        private bool userCancelled;
        private ParameterBag parameterBag;

        public frmEffectOptions()
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
            // Do nothing
        }

        private void FillParameterFromForm()
        {
            string type = "d";
            if (rdoTypeG.Checked)
                type = "g";
            else if (rdoTypeM.Checked)
                type = "m";
            parameterBag = new ParameterBag();
            parameterBag.AddOutput("type", type);
        }
    }
}

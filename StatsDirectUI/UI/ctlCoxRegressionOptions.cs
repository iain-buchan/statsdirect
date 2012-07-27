using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlCoxRegressionOptions : UserControl, IFillParameterBag
    {
        private MultipleOptionsParameter parameter;

        public ctlCoxRegressionOptions(MultipleOptionsParameter parameter)
        {
            InitializeComponent();
            this.parameter = parameter;
            FillFormFromParameter();
        }

        private void FillFormFromParameter()
        {
            txtAccuracy.Text = 0.0000001.ToString("N7");
        }

        private void FillParameterFromForm(ParameterBag parameterBag)
        {
            parameterBag.AddOutput("centre-continuous-covariates", chkCentre.Checked);
            parameterBag.AddOutput("accuracy", Utilities.Parsing.Cdbl_Txt(txtAccuracy.Text));
            parameterBag.AddOutput("splitting-ratio", cboStrata.Text);
        }

        Control IFillParameterBag.Fill(ParameterBag outputParameters, bool doValidation)
        {
            FillParameterFromForm(outputParameters);
            return null;
        }
    }
}

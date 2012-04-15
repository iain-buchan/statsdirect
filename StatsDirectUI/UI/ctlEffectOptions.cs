using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlEffectOptions : UserControl, IFillParameterBag
    {
        private MultipleOptionsParameter parameter;

        public ctlEffectOptions(MultipleOptionsParameter parameter)
        {
            InitializeComponent();
            this.parameter = parameter;
            FillFormFromParameter();
        }

        private void FillFormFromParameter()
        {
            // Do nothing
        }

        private void FillParameterFromForm(ParameterBag parameterBag)
        {
            string type = "d";
            if (rdoTypeG.Checked)
                type = "g";
            else if (rdoTypeM.Checked)
                type = "m";
            parameterBag.AddOutput("type", type);
        }


        Control IFillParameterBag.Fill(ParameterBag outputParameters)
        {
            FillParameterFromForm(outputParameters);
            return null;
        }
    }
}

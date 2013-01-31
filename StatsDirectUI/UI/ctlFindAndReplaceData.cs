using System;
using System.Windows.Forms;

using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlFindAndReplaceData : UserControl, IFillParameterBag
    {
        private static string FIND_EXPRESSION = "";
        private static string REPLACE_EXPRESSION = "";

        private readonly ExtractionOptions options;

        public ctlFindAndReplaceData(ExtractionOptions options)
        {
            this.options = options;
            InitializeComponent();
            FillControlFromOptions();
        }

        private Control Extract(ParameterBag outputParameters)
        {
            string searchExpression = txtExpression.Text.Trim().ToUpper();
            if (0 == searchExpression.Length)
                return txtExpression;
            Calcit finder = new Calcit(searchExpression);
            string replaceExpression = txtReplace.Text.Trim().ToUpper();
            if (0 == replaceExpression.Length)
                return txtReplace;
            Calcit replacer = new Calcit(replaceExpression);

            using (WaitCursor wc = new WaitCursor())
            {
                DataFrame outputFrame = new DataFrame();
                double[] x = new double[1];
                foreach (Variable source in options.DataFrame.Variables)
                {
                    int rows = source.Length;
                    StringVariable outputVariable = new StringVariable(rows, source.Title);
                    outputFrame.Variables.Add(outputVariable);
                    double[] sourceData = source.AsDoubleVariable.Data;
                    for (int n = 0; n < rows; n++)
                    {
                        if (sourceData[n] != Constant.MISSING)
                        {
                            x[0] = sourceData[n];
                            if (1 == finder.Evaluate(x))
                                outputVariable.Data[n] = replacer.Evaluate(x).ToString();
                        }
                    }
                }

                outputParameters.AddOutput("extracted", outputFrame);
                return null;
            }
        }

        private void FillControlFromOptions()
        {
            txtExpression.Text = FIND_EXPRESSION;
            txtReplace.Text = REPLACE_EXPRESSION;
        }

        private Control FillOptionsFromControl(ParameterBag outputParameters)
        {
            FIND_EXPRESSION = txtExpression.Text;
            REPLACE_EXPRESSION = txtReplace.Text;
            return Extract(outputParameters);
        }

        public Control Fill(ParameterBag outputParameters, bool doValidation)
        {
            return FillOptionsFromControl(outputParameters);
        }
    }
}

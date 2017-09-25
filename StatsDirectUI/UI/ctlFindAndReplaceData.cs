using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Expressions;
using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlFindAndReplaceData : UserControl, IFillParameterBag
    {
        private static string FIND_EXPRESSION = string.Empty;
        private static string REPLACE_EXPRESSION = string.Empty;

        private readonly ExtractionOptions options;

        public ctlFindAndReplaceData(ExtractionOptions options)
        {
            this.options = options;
            InitializeComponent();
            FillControlFromOptions();
        }

        private Control Extract(ParameterBag outputParameters)
        {
            string searchExpression = txtExpression.Text.Trim();
            if (0 == searchExpression.Length)
                return txtExpression;
            DataType[] oneDouble = { DataType.Double };
            Calcit finder = new Calcit(searchExpression, oneDouble, false);
            string replaceExpression = txtReplace.Text.Trim();
            if (0 == replaceExpression.Length)
                return txtReplace;
            Calcit replacer = new Calcit(replaceExpression, oneDouble, false);

            using (new WaitCursor())
            {
                DataFrame outputFrame = new DataFrame();
                double[] x = new double[1];
                foreach (IVariable source in options.DataFrame.Variables)
                {
                    int rows = source.Length;
                    IVariable outputVariable = new VariantVariable(rows, source.Title);
                    outputFrame.Variables.Add(outputVariable);
                    double[] sourceData = ((DoubleVariable) source).Data;
                    for (int n = 0; n < rows; n++)
                    {
                        if (sourceData[n] != Constant.MISSING)
                        {
                            x[0] = sourceData[n];
                            if (finder.Evaluate<bool>(x))
                                outputVariable.DataAsObject(n, replacer.Evaluate<object>(x));
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

using System;
using System.Windows.Forms;

using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Expressions;

namespace StatsDirect.UI
{
    public partial class ctlExtraction : UserControl, IFillParameterBag
    {
        private static bool EXT_KEEPROW;
        private static string EXT_EXPRESSION = "X1=";

        readonly ExtractionOptions options;

        public ctlExtraction(ExtractionOptions options)
        {
            this.options = options;
            InitializeComponent();
            FillControlFromOptions();
        }

        private Control Extract(bool count, ParameterBag outputParameters)
        {
            ITemplateHost host = SdApplication.SoleInstance;

            int cols = options.IdentifiersFrame.VariableCount;
            string dtitle = options.Title;

            string expression = txtExpression.Text.Trim();
            if (0 == expression.Length)
                return txtExpression;
  
            bool ok = false;
            for (int k = 0; k < cols; k++)
            {
                if (expression.Contains("X" + (k + 1).ToString()))
                {
                    ok = true;
                    break;
                }
            }
            if (!ok || "X1=".Equals(expression))
            {
                host.Error("Invalid expression, you must enter an expression such as X1>0 or X1=1 etc.", "Extract variable");
                return txtExpression;
            }

            int rows = options.DataFrame.Variables[0].Length;
  
            string expressionWithOriginalNames = expression;
            for (int k = 0; k < options.IdentifiersFrame.VariableCount; k++)
            {
                expressionWithOriginalNames = expressionWithOriginalNames.Replace("X" + (k + 1).ToString(), options.IdentifiersFrame.Variables[k].Title);
            }
    
            expressionWithOriginalNames = dtitle + " {" + expressionWithOriginalNames + "}";
            if (!count)
            {
                DataFrame outputFrame = new DataFrame();
                StringVariable outputVariable = new StringVariable(rows, expressionWithOriginalNames);
                outputFrame.Variables.Add(outputVariable);
                int cnt = 0;
                DataType[] dataTypes = new DataType[cols];
                for (int col = 0; col < cols; col++)
                    dataTypes[col] = DataType.Double;
                Calcit calcit = new Calcit(expression, dataTypes, false);
                double[] x = new double[cols];
                for (int n = 0; n < rows; n++)
                {
                    if ((options.DataFrame.Variables[0] as DoubleVariable).Data[n] != Constant.MISSING)
                    {
   
                        // Put row into working array
                        for (int j = 0; j < cols; j++)
                            x[j] = (options.IdentifiersFrame.Variables[j] as DoubleVariable).Data[n];
  
                        // See if expression is true
                        if (1 == calcit.Evaluate(x))
                        {
                            int rw = chkKeepRowPositions.Checked ? n : cnt;
                            outputVariable.Data[rw] = (options.DataFrame.Variables[0] as DoubleVariable).Data[n].ToString();
                            cnt++;
                        }
      
                    }
                }
                if (!chkKeepRowPositions.Checked)
                    outputVariable.TruncateDataToLength(cnt);

                outputParameters.AddOutput("extracted", outputFrame);
            }
            else
            {
                int cnt = 0;
                DataType[] dataTypes = new DataType[cols];
                for (int col = 0; col < cols; col++)
                    dataTypes[col] = DataType.Double;
                Calcit calcit = new Calcit(expression, dataTypes, false);
                double[] x = new double[cols];
                for (int N = 0; N < rows; N++)
                {
                    if ((options.DataFrame.Variables[0] as DoubleVariable).Data[N] != Constant.MISSING)
                    {
                        // Put row into working array
                        for (int j = 0; j < cols; j++)
                            x[j] = (options.IdentifiersFrame.Variables[j] as DoubleVariable).Data[N];
  
                        // See if expression is true
                        if (1 == calcit.Evaluate(x))
                            cnt++;
                    }
                }
                lblMessage.Text = cnt + " data points out of " + rows + " match your expression.";
            }
            return null;
        }

        private void FillControlFromOptions()
        {
            chkKeepRowPositions.Checked = EXT_KEEPROW;
            txtExpression.Text = EXT_EXPRESSION;
            lblIdentifiers.Text = options.IdentifierNames;
        }

        private Control FillOptionsFromControl(ParameterBag outputParameters)
        {
            EXT_KEEPROW = chkKeepRowPositions.Checked;
            EXT_EXPRESSION = txtExpression.Text;
            return Extract(false, outputParameters);
        }

        private void cmdCount_Click(object sender, EventArgs e)
        {
            Control errorControl = Extract(true, null);
            if (null != errorControl)
            {
                errorControl.Focus();
            }
        }

        public Control Fill(ParameterBag outputParameters, bool doValidation)
        {
            return FillOptionsFromControl(outputParameters);
        }
    }
}

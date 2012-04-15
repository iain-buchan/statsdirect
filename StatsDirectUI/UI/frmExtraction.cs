using System;
using System.Windows.Forms;

using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class frmExtraction : Form
    {
        private static bool EXT_KEEPROW;
        private static string EXT_EXPRESSION = "X1=";

        readonly ExtractionOptions options;

        public frmExtraction(ExtractionOptions options)
        {
            this.options = options;
            InitializeComponent();
            LoadDefaults();
        }

        private void Extract(bool count)
        {
            ITemplateHost host = SDApplication.SoleInstance;

            int cols = options.IdentifiersFrame.VariableCount;
            string dtitle = options.Title;
            txtMessage.Text = "";

            string Q = txtExpression.Text.Trim().ToUpper();
            if (0 == Q.Length)
            {
                host.Error("Please enter an expression first", "Extract variable");
                txtExpression.Text = "X1=";
                txtExpression.Select();
            }
  
            bool OK = false;
            for (int k = 0; k < cols; k++)
            {
                if (Q.Contains("X" + (k + 1).ToString()))
                {
                    OK = true;
                    break;
                }
            }
            if (!OK || "X1=".Equals(Q))
            {
                host.Error("Invalid expression, you must enter an expression such as X1>0 or X1=1 etc.", "Extract variable");
                txtExpression.Text = "X1=";
                txtExpression.Select();
                return;
            }

            int rows = options.Data.Length;
  
            string qx = Q;
            for (int k = 0; k < options.IdentifiersFrame.VariableCount; k++)
            {
                qx = qx.Replace("X" + (k + 1).ToString(), options.IdentifiersFrame.Variables[k].Title);
            }
    
            qx = dtitle + " [" + qx + "]";
            if (!count)
            {
                string t = host.GetString("Name for new variable", "Extract variables from " + dtitle, qx);
                if (null == t)
                    return;
  
                // this.Hide();

                DataFrame outputFrame = new DataFrame();
                StringVariable outputVariable = new StringVariable(rows, t);
                outputFrame.Variables.Add(outputVariable);
                int cnt = 0;
                Calcit calcit = new Calcit(Q);
                double[] x = new double[cols];
                for (int N = 0; N < rows; N++)
                {
                    if (options.Data.Data[N] != Constant.MISSING)
                    {
   
                        // Put row into working array
                        for (int j = 0; j < cols; j++)
                        {
                            x[j] = options.IdentifiersFrame.Variables[j].AsDoubleVariable.Data[N];
                        }
  
                        // See if expression is true
                        if (1 == calcit.Evaluate(x))
                        {
                            int rw = chkKeepRowPositions.Checked ? N : cnt;
                            outputVariable.Data[rw] = options.Data.Data[N].ToString();
                            cnt++;
                        }
      
                    }
                }
                if (!chkKeepRowPositions.Checked)
                    outputVariable.TruncateDataToLength(cnt);

                host.OutputFrame(outputFrame, false, false, windowPicker.SelectedPaneAndPosition());
    
                txtMessage.Text = cnt.ToString() + " data points were extracted into the new variable: " + t;
                // this.Show();
            }
 
            else
            {
                int cnt = 0;
                Calcit calcit = new Calcit(Q);
                double[] x = new double[cols];
                for (int N = 0; N < rows; N++)
                {
                    if (options.Data.Data[N] != Constant.MISSING)
                    {
                        // Put row into working array
                        for (int j = 0; j < cols; j++)
                        {
                            x[j] = options.IdentifiersFrame.Variables[j].AsDoubleVariable.Data[N];
                        }
  
                        // See if expression is true
                        if (1 == calcit.Evaluate(x))
                            cnt++;
                    }
                }
                txtMessage.Text = cnt.ToString() + " data points match your expression.";
            }
        }

        private void SetFormFromOptions()
        {
            Text = "Extract Data from " + options.Title;
            lblIdentifiers.Text = options.IdentifierNames;
            txtMessage.Text = "";
        }

        private void SaveDefaults()
        {
            EXT_KEEPROW = chkKeepRowPositions.Checked;
            EXT_EXPRESSION = txtExpression.Text;
        }

        private void LoadDefaults()
        {
            chkKeepRowPositions.Checked = EXT_KEEPROW;
            txtExpression.Text = EXT_EXPRESSION;
        }

        private void cmdCount_Click(object sender, EventArgs e)
        {
            Extract(true);
        }

        private void cmdExtract_Click(object sender, EventArgs e)
        {
            Extract(false);
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            SaveDefaults();
            Close();
        }

        private void frmExtraction_Shown(object sender, EventArgs e)
        {
            SetFormFromOptions();
        }

        private void cmdHelp_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ShowCurrentHelp();
        }
    }
}

using System;
using System.Text;
using System.Windows.Forms;
using System.Media;
using StatsDirect.Numerics;
using StatsDirect.UI;
using StatsDirect.Utilities;
using StatsDirect.Builtins;

namespace StatsDirect.Calculator
{
    public partial class frmStatsDirectCalculator : Form
    {
        public frmStatsDirectCalculator()
        {
            InitializeComponent();
        }

        private void cmdCalculate_Click(object sender, EventArgs e)
        {
            DoCalculate();
        }

        private void DoCalculate()
        {
            try
            {
                string equation = txtExpression.Text;
                if (equation.Length > 0)
                {
                    Calcit c = new Calcit(equation);
                    double res = c.Evaluate(null);
                    txtResult.Text = Constant.MISSING == res ? Formatting.ERRR : " " + res.ToString();
                }
                else
                {
                    txtResult.Text = " Please enter an expression first";
                    SystemSounds.Beep.Play();
                }
                txtExpression.Focus();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void cmdSave_Click(object sender, EventArgs e)
        {
            DoCalculate();
            DoSave();
        }

        private void DoSave()
        {
            try
            {
                if (txtExpression.Text.Trim().Length > 0)
                {
                    lstSavedExpressions.Items.Add(txtExpression.Text.Trim() + "\t" + txtResult.Text);
                    lstSavedExpressions.Enabled = true;
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
                txtExpression.Focus();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void SavePosition()
        {
            Calculator.Default.CalculatorTop = Top;
            Calculator.Default.CalculatorLeft = Left;
            Calculator.Default.CalculatorWidth = Width;
            Calculator.Default.CalculatorHeight = Height;
            Calculator.Default.CalculatorMaximized = WindowState == FormWindowState.Maximized;
            Calculator.Default.Save();
        }

        private void LoadPosition()
        {
            if (null != Calculator.Default && null != Calculator.Default.Properties)
            {
                Top = Calculator.Default.CalculatorTop;
                Left = Calculator.Default.CalculatorLeft;
                Width = Calculator.Default.CalculatorWidth;
                Height = Calculator.Default.CalculatorHeight;
                WindowState = Calculator.Default.CalculatorMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            }
        }

        private void cmdPaste_Click(object sender, EventArgs e)
        {
            DoPaste();
        }

        private void DoPaste()
        {
            try
            {
                if (lstSavedExpressions.SelectedIndex >= 0)
                {
                    string toPaste = ((string)lstSavedExpressions.SelectedItem);
                    toPaste = toPaste.Substring(0, toPaste.IndexOf('\t'));
                    txtExpression.SelectionLength = 0;
                    txtExpression.SelectedText = toPaste;
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void cmdDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (lstSavedExpressions.SelectedIndex >= 0)
                {
                    lstSavedExpressions.Items.RemoveAt(lstSavedExpressions.SelectedIndex);
                    if (0 == lstSavedExpressions.Items.Count)
                    {
                        lstSavedExpressions.Enabled = false;
                        cmdPaste.Enabled = false;
                        cmdDelete.Enabled = false;
                    }
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }

        }

        private void cmdHelp_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ShowHelp(this, "1020");
        }

        private void lstSavedExpressions_Enter(object sender, EventArgs e)
        {
            cmdPaste.Enabled = true;
            cmdDelete.Enabled = true;
        }

        private void HandleException(Exception ex)
        {
            SDApplication.SoleInstance.FriendlyError("Couldn't evaluate expression", ex, false);
        }

        private void frmStatsDirectCalculator_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                SavePosition();
                if (lstSavedExpressions.Items.Count > 0)
                {
                    if (DialogResult.Yes == MessageBox.Show(this, "Copy saved results to clipboard?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, 0))
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (object os in lstSavedExpressions.Items)
                        {
                            string s = (string)os;
                            string[] splitS = s.Split('\t');
                            string expr = splitS[0].Trim();
                            string result = splitS[1].Trim();
                            string formatted = expr + " = " + result;
                            sb.AppendLine(formatted);
                        }
                        Clipboard.SetText(sb.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }

        }

        private void frmStatsDirectCalculator_Load(object sender, EventArgs e)
        {
            LoadPosition();
        }

        private void frmStatsDirectCalculator_Shown(object sender, EventArgs e)
        {
            txtExpression.Focus();
        }
    }
}

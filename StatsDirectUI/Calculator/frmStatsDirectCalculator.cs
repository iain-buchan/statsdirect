using System;
using System.Text;
using System.Windows.Forms;
using System.Media;
using StatsDirect.Numerics;
using StatsDirect.UI;
using StatsDirect.Utilities;
using StatsDirect.Builtins;
using StatsDirect.Expressions;

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
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                string equation = txtExpression.Text;
                if (equation.Length > 0)
                {
                    Calcit c = new(equation, new DataType[0], false);
                    object res = c.EvaluateObject<object>(null);
                    txtResult.Text = res is double && Constant.MISSING == (double)res ? Formatting.ERRR : res.ToString();
                }
                else
                {
                    txtResult.Text = " Please enter an expression first";
                    SystemSounds.Beep.Play();
                }
                txtExpression.Focus();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
#endif
        }

        private void cmdSave_Click(object sender, EventArgs e)
        {
            DoCalculate();
            DoSave();
        }

        private void DoSave()
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
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
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
#endif
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void SavePosition()
        {
            Settings settings = Settings.Default;
            settings.CalculatorTop = Top;
            settings.CalculatorLeft = Left;
            settings.CalculatorWidth = Width;
            settings.CalculatorHeight = Height;
            settings.CalculatorMaximized = WindowState == FormWindowState.Maximized;
            settings.Save();
        }

        private void LoadPosition()
        {
            Settings settings = Settings.Default;
            if (settings is not null && settings.WasLoaded)
            {
                Top = settings.CalculatorTop;
                Left = settings.CalculatorLeft;
                Width = settings.CalculatorWidth;
                Height = settings.CalculatorHeight;
                WindowState = settings.CalculatorMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            }
        }

        private void cmdPaste_Click(object sender, EventArgs e)
        {
            DoPaste();
        }

        private void DoPaste()
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                if (lstSavedExpressions.SelectedIndex >= 0)
                {
                    string toPaste = (string)lstSavedExpressions.SelectedItem;
                    toPaste = toPaste.Substring(0, toPaste.IndexOf('\t'));
                    txtExpression.SelectionLength = 0;
                    txtExpression.SelectedText = toPaste;
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
#endif
        }

        private void cmdDelete_Click(object sender, EventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
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
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
#endif
        }

        private void cmdHelp_Click(object sender, EventArgs e)
        {
            SdApplication.SoleInstance.ShowHelp(this, "1020");
        }

        private void lstSavedExpressions_Enter(object sender, EventArgs e)
        {
            cmdPaste.Enabled = true;
            cmdDelete.Enabled = true;
        }

        private static void HandleException(Exception ex)
        {
            SdApplication.SoleInstance.FriendlyError("Couldn't evaluate expression", ex, false);
        }

        private void frmStatsDirectCalculator_FormClosing(object sender, FormClosingEventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                SavePosition();
                if (lstSavedExpressions.Items.Count > 0)
                {
                    if (DialogResult.Yes == MessageBox.Show(this, "Copy saved results to clipboard?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, 0))
                    {
                        StringBuilder sb = new();
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
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
#endif
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

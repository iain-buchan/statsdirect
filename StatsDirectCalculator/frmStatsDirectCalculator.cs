using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Media;
using StatsDirect.Numerics;
using StatsDirect.Utilities;
using StatsDirect.Builtins;

namespace StatsDirectCalculator
{
    public partial class frmStatsDirectCalculator : Form
    {
        private string thousands_separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
        private readonly string HELP_FILE_PATH = System.IO.Path.Combine(StatsDirect.Configuration.SDConfiguration.InstallationDirectory, Properties.Settings.Default.HelpFileName);

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
                    if (equation.Contains(thousands_separator))
                    {
                        txtExpression.Text = equation.Replace(thousands_separator, "");
                        txtResult.Text = " Digit grouping symbols removed, try again";
                        SystemSounds.Beep.Play();
                    }
                    else
                    {
                        Calcit c = new Calcit();
                        double res = c.f_calcit(ref equation);
                        if (Constant.MISSING == res)
                        {
                            txtResult.Text = Formatting.ERRR;
                        }
                        else
                        {
                            txtResult.Text = " " + res.ToString();
                        }
                    }
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
            SavePosition();
            this.Close();
        }

        private void SavePosition()
        {
            Properties.Settings.Default.MainTop = this.Top;
            Properties.Settings.Default.MainLeft = this.Left;
            Properties.Settings.Default.MainWidth = this.Width;
            Properties.Settings.Default.MainHeight = this.Height;
            Properties.Settings.Default.MainMaximized = this.WindowState == FormWindowState.Maximized;
            Properties.Settings.Default.Save();
        }

        private void LoadPosition()
        {
            if (null != Properties.Settings.Default && null != Properties.Settings.Default.Properties)
            {
                this.Top = Properties.Settings.Default.MainTop;
                this.Left = Properties.Settings.Default.MainLeft;
                this.Width = Properties.Settings.Default.MainWidth;
                this.Height = Properties.Settings.Default.MainHeight;
                if (Properties.Settings.Default.MainMaximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
                else
                {
                    this.WindowState = FormWindowState.Normal;
                }
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
            ShowHelp(999);
        }

        private void ShowHelp(int helpContextId)
        {
            System.Windows.Forms.Help.ShowHelp(this, HELP_FILE_PATH, System.Windows.Forms.HelpNavigator.TopicId, helpContextId.ToString());
        }

        private void lstSavedExpressions_Enter(object sender, EventArgs e)
        {
            cmdPaste.Enabled = true;
            cmdDelete.Enabled = true;
        }

        private void HandleException(Exception ex)
        {
            // TODO: Log or present exception.  For now, just eat it, to match the original "on error resume next".
        }

        private void frmStatsDirectCalculator_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                bool isStandalone = true;
                if (lstSavedExpressions.Items.Count > 0)
                {
                    string z = isStandalone ? "clipboard" : "report";
                    if (DialogResult.Yes == MessageBox.Show(this, "Copy saved results to " + z + "?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, 0, HELP_FILE_PATH, "999"))
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

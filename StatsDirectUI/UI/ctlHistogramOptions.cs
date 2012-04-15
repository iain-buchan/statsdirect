using System;
using System.Windows.Forms;
using StatsDirect.Charting;
using System.Media;

namespace StatsDirect.UI
{
    public partial class ctlHistogramOptions : UserControl
    {
        private ChartDefinition definition;
        private HistogramOptions options;
        private int currentSeriesIndex;
        private bool fillingForm;

        public event EventHandler ScaleChanged;

        public ctlHistogramOptions()
        {
            InitializeComponent();
        }

        public ChartDefinition ChartDefinition
        {
            set
            {
                definition = value;
                if (null != options)
                    options.ScaleChanged -= options_ScaleChanged;
                options = (HistogramOptions)definition.ChartOptions;
                options.ScaleChanged += options_ScaleChanged;
                if (options.PoolVariablesForBins)
                {
                    options.Reset(true, 0, options.PoolVariablesForBins, 0);
                }
                else
                {
                    for (int i = 0; i < options.Series.Count; i++)
                        options.Reset(true, 0, options.PoolVariablesForBins, i);
                }
                FillFormFromOptions();
            }
        }

        void options_ScaleChanged(object sender, EventArgs e)
        {
            if (null != ScaleChanged)
                ScaleChanged(this, e);
        }

        public bool FillOptionsFromForm()
        {
            bool optionsAreOk = true;

            options.PoolVariablesForBins = chkPoolVariables.Checked;

            HistogramSeriesOptions seriesOptions = options.HistoSeriesOptions[currentSeriesIndex];
            if (!int.TryParse(txtBins.Text, out seriesOptions.Bins))
            {
                FailAndHighlight(txtBins);
                optionsAreOk = false;
            }
            if (!double.TryParse(txtMidpointInterval.Text, out seriesOptions.MidPointInterval))
            {
                FailAndHighlight(txtMidpointInterval);
                optionsAreOk = false;
            }
            if (!double.TryParse(txtMinimumMidpoint.Text, out seriesOptions.MinimumBinMidPoint))
            {
                FailAndHighlight(txtMinimumMidpoint);
                optionsAreOk = false;
            }
            options.ShowRelativeFrequencies = chkShowRelativeFrequencies.Checked;
            if (!options.IsAscii)
            {
                options.OverlayNormalCurve = chkOverlayNormalCurve.Checked;
            }

            return optionsAreOk;
        }

        private void FailAndHighlight(TextBox toHighlight)
        {
            SystemSounds.Exclamation.Play();
            toHighlight.Focus();
        }

        public void FillFormFromOptions()
        {
            try
            {
                fillingForm = true;
                if (0 == cboVariable.Items.Count)
                {
                    foreach (Series s in options.Series)
                    {
                        cboVariable.Items.Add(s.Title);
                    }
                }
                chkPoolVariables.Enabled = options.Series.Count > 1;
                chkPoolVariables.Checked = options.PoolVariablesForBins;
                cmdNextVariable.Enabled = currentSeriesIndex < options.Series.Count - 1;
                cmdPreviousVariable.Enabled = currentSeriesIndex > 0;
                cboVariable.SelectedIndex = currentSeriesIndex;

                HistogramSeriesOptions seriesOptions = options.HistoSeriesOptions[currentSeriesIndex];
                lblDataMaximum.Text = seriesOptions.MaximumValue.ToString();
                lblDataMinimum.Text = seriesOptions.MinimumValue.ToString();
                txtBins.Text = seriesOptions.Bins.ToString();
                txtMidpointInterval.Text = seriesOptions.MidPointInterval.ToString();
                txtMinimumMidpoint.Text = seriesOptions.MinimumBinMidPoint.ToString();
                chkShowRelativeFrequencies.Checked = options.ShowRelativeFrequencies;

                if (options.IsAscii)
                {
                    chkOverlayNormalCurve.Enabled = false;
                }
                else
                {
                    chkOverlayNormalCurve.Checked = options.OverlayNormalCurve;
                }
                ListBins();
            }
            finally
            {
                fillingForm = false;
            }
        }

        private void ListBins()
        {
            int mp;
            if (!int.TryParse(txtBins.Text, out mp))
                FailAndHighlight(txtBins);
            double zint;
            if (!double.TryParse(txtMidpointInterval.Text, out zint))
                FailAndHighlight(txtMidpointInterval);
            double zmin;
            if (!double.TryParse(txtMinimumMidpoint.Text, out zmin))
                FailAndHighlight(txtMinimumMidpoint);
            lstBinValues.Items.Clear();
            for (int i = 0; i < mp; i++)
            {
                double ltpt = AxisScaler.Axis_Q0(zmin, zmin + (zint * i) - zint / 2.0);
                double mdpt = AxisScaler.Axis_Q0(zmin, zmin + (zint * i));
                double rtpt = AxisScaler.Axis_Q0(zmin, zmin + (zint * i) + zint / 2.0);
                ListViewItem item = new ListViewItem {Text = Utilities.Formatting.XRound(ltpt, 9)};
                item.SubItems.Add(Utilities.Formatting.XRound(mdpt, 9));
                item.SubItems.Add(Utilities.Formatting.XRound(rtpt, 9));
                lstBinValues.Items.Add(item);
            }
        }

        private void cmdAutoBins_Click(object sender, EventArgs e)
        {
            try
            {
                options.PoolVariablesForBins = chkPoolVariables.Checked;
                options.Reset(true, 0, chkPoolVariables.Checked, currentSeriesIndex);
                FillFormFromOptions();
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        private void cmdAutoMidpoints_Click(object sender, EventArgs e)
        {
            try
            {
                int bins = Int32.Parse(txtBins.Text);
                options.PoolVariablesForBins = chkPoolVariables.Checked;
                options.Reset(false, bins, chkPoolVariables.Checked, currentSeriesIndex);
                FillFormFromOptions();
            }
            catch (FormatException)
            {
                SDApplication.SoleInstance.msgbox_x("Please enter the number of bins", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Histogram", true);
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        private void cmdReset_Click(object sender, EventArgs e)
        {
            ListBins();
        }

        private void cmdPreviousVariable_Click(object sender, EventArgs e)
        {
            if (currentSeriesIndex > 0)
            {
                if (!FillOptionsFromForm())
                    return;
                --currentSeriesIndex;
                FillFormFromOptions();
            }
        }

        private void cmdNextVariable_Click(object sender, EventArgs e)
        {
            if (currentSeriesIndex < options.Series.Count - 1)
            {
                if (!FillOptionsFromForm())
                    return;
                currentSeriesIndex++;
                FillFormFromOptions();
            }
        }

        private void cboVariable_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!fillingForm)
            {
                // TODO: How to prevent the change if there's an issue?
                FillOptionsFromForm();
                currentSeriesIndex = cboVariable.SelectedIndex;
                FillFormFromOptions();
            }
        }

        private void chkShowRelativeFrequencies_CheckedChanged(object sender, EventArgs e)
        {
            if (!FillOptionsFromForm())
                return;
            foreach (HistogramSeriesOptions hso in options.HistoSeriesOptions)
            {
                if (chkShowRelativeFrequencies.Checked)
                {
                    if ("Counts".Equals(hso.YAxisTitle))
                        hso.YAxisTitle = "Proportion";
                }
                else
                {
                    if ("Proportion".Equals(hso.YAxisTitle))
                        hso.YAxisTitle = "Counts";
                }
            }
            if (null != ScaleChanged)
                ScaleChanged(this, EventArgs.Empty);
        }
    }
}
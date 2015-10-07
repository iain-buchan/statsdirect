using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StatsDirect.Charting;
using System.Media;
using StatsDirect.Utilities;

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
                List<Series> series = definition.XSeries.Count > 0 ? definition.XSeries : definition.YSeries;
                // On a new call, series options will exist, but Bins will be set to zero for all.  On a replay, Bins will be set to sane values.  Detect the replay and don't reset the bins on one.
                if (null != options.HistoSeriesOptions && options.HistoSeriesOptions.Count > 0 && options.HistoSeriesOptions[0].Bins == 0)
                {
                    if (options.PoolVariablesForBins)
                    {
                        options.Reset(true, 0, options.PoolVariablesForBins, 0, series);
                    }
                    else
                    {
                        for (int i = 0; i < series.Count; i++)
                            options.Reset(true, 0, options.PoolVariablesForBins, i, series);
                    }
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
                options.OverlayNormalCurve = chkOverlayNormalCurve.Checked;

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
                List<Series> series = definition.XSeries.Count > 0 ? definition.XSeries : definition.YSeries;
                fillingForm = true;
                if (0 == cboVariable.Items.Count)
                {
                    foreach (Series s in series)
                        cboVariable.Items.Add(s.Title);
                }
                chkPoolVariables.Enabled = series.Count > 1;
                chkPoolVariables.Checked = options.PoolVariablesForBins;
                cmdNextVariable.Enabled = currentSeriesIndex < series.Count - 1;
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
                    chkOverlayNormalCurve.Enabled = false;
                else
                    chkOverlayNormalCurve.Checked = options.OverlayNormalCurve;
                ListBins();
            }
            finally
            {
                fillingForm = false;
            }
        }

        /// <summary>
        /// Given the current options, populate the list with the bins that will be used: left, midpoint, right.
        /// </summary>
        private void ListBins()
        {
            int mp;
            if (!int.TryParse(txtBins.Text, out mp))
                FailAndHighlight(txtBins);
            double zInt;
            if (!double.TryParse(txtMidpointInterval.Text, out zInt))
                FailAndHighlight(txtMidpointInterval);
            double zMin;
            if (!double.TryParse(txtMinimumMidpoint.Text, out zMin))
                FailAndHighlight(txtMinimumMidpoint);
            lstBinValues.Items.Clear();
            for (int i = 0; i < mp; i++)
            {
                double leftPoint = AxisScaler.Axis_Q0(zMin, zMin + (zInt * i) - zInt / 2.0);
                double midPoint = AxisScaler.Axis_Q0(zMin, zMin + (zInt * i));
                double rightPoint = AxisScaler.Axis_Q0(zMin, zMin + (zInt * i) + zInt / 2.0);
                ListViewItem item = new ListViewItem {Text = Formatting.XRound(leftPoint, 9)};
                item.SubItems.Add(Formatting.XRound(midPoint, 9));
                item.SubItems.Add(Formatting.XRound(rightPoint, 9));
                lstBinValues.Items.Add(item);
            }
        }

        private void cmdAutoBins_Click(object sender, EventArgs e)
        {
            try
            {
                List<Series> series = definition.XSeries.Count > 0 ? definition.XSeries : definition.YSeries;
                options.PoolVariablesForBins = chkPoolVariables.Checked;
                options.Reset(true, 0, chkPoolVariables.Checked, currentSeriesIndex, series);
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
                int bins = Parsing.Cint_Txt(txtBins.Text);
                options.PoolVariablesForBins = chkPoolVariables.Checked;
                List<Series> series = definition.XSeries.Count > 0 ? definition.XSeries : definition.YSeries;
                options.Reset(false, bins, chkPoolVariables.Checked, currentSeriesIndex, series);
                FillFormFromOptions();
            }
            catch (FormatException)
            {
                SdApplication.SoleInstance.MsgboxX("Please enter the number of bins", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Histogram", true);
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        private void cmdReset_Click(object sender, EventArgs e)
        {
            try
            {
                ListBins();
            }
            catch (Exception)
            {
                // Do nothing
            }
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
            List<Series> series = definition.XSeries.Count > 0 ? definition.XSeries : definition.YSeries;
            if (currentSeriesIndex < series.Count - 1)
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
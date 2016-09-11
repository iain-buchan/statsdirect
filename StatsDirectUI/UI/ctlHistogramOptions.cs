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
                if (null != options.HistoSeriesOptions && options.HistoSeriesOptions.Count > 0 && options.HistoSeriesOptions[0].BinsDescriptor.Bins == 0)
                {
                    for (int i = 0; i < series.Count; i++)
                        options.Reset(true, 0, i, series[i]);
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
            int bins;
            if (!int.TryParse(txtBins.Text, out bins))
            {
                FailAndHighlight(txtBins);
                optionsAreOk = false;
            }
            double midpointInterval;
            if (!double.TryParse(txtMidpointInterval.Text, out midpointInterval))
            {
                FailAndHighlight(txtMidpointInterval);
                optionsAreOk = false;
            }
            double minimumBinMidpoint;
            if (!double.TryParse(txtMinimumMidpoint.Text, out minimumBinMidpoint))
            {
                FailAndHighlight(txtMinimumMidpoint);
                optionsAreOk = false;
            }
            double minimum = minimumBinMidpoint - (midpointInterval / 2.0);
            double maximum = minimum + bins * midpointInterval;
            double[] edges = HistogramBinChooser.Linspace(minimum, maximum, bins);
            int actualRows;
            double[] sortedData = HistogramSeriesOptions.ExtractNonMissingDataAndSort(definition.YSeries[currentSeriesIndex], out actualRows);
            Array.Sort(sortedData, 0, actualRows);
            int[] counts = HistogramBinChooser.SortedHist(sortedData, actualRows, edges);
            seriesOptions.BinsDescriptor = new BinsDescriptor { Edges = edges, Counts = counts };
            options.ShowRelativeFrequencies = chkShowRelativeFrequencies.Checked;
            if (!options.IsAscii)
                options.OverlayNormalCurve = chkOverlayNormalCurve.Checked;

            return optionsAreOk;
        }

        private static void FailAndHighlight(TextBox toHighlight)
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
                BinsDescriptor descriptor = seriesOptions.BinsDescriptor;
                double minimumBinMidpoint = (descriptor.Edges[0] + descriptor.Edges[1]) / 2.0;
                double maximumBinMidpoint = (descriptor.Edges[descriptor.Bins] + descriptor.Edges[descriptor.Bins - 1]) / 2.0;
                double binMidpointInterval = (maximumBinMidpoint - minimumBinMidpoint) / (descriptor.Bins - 1);

                lblDataMaximum.Text = descriptor.HighestEdge.ToString();
                lblDataMinimum.Text = descriptor.LowestEdge.ToString();
                txtBins.Text = descriptor.Bins.ToString();
                txtMidpointInterval.Text = binMidpointInterval.ToString();
                txtMinimumMidpoint.Text = minimumBinMidpoint.ToString();
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
                options.Reset(true, 0, currentSeriesIndex, series[currentSeriesIndex]);
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
                options.Reset(false, bins, currentSeriesIndex, series[currentSeriesIndex]);
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
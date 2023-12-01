using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StatsDirect.Charting;
using System.Media;
using StatsDirect.Utilities;
using StatsDirect.Numerics;
using StatsDirect.Charting.Options;

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
                if (options is not null)
                    options.ScaleChanged -= options_ScaleChanged;
                options = (HistogramOptions)definition.ChartOptions;
                options.ScaleChanged += options_ScaleChanged;
                IReadOnlyList<ISeries> series = definition.XSeries.Count > 0
                    ? definition.XSeries
                    : definition.YSeries;
                // On a new call, series options will exist, but Bins will be set to zero for all.  On a replay, Bins will be set to sane values.  Detect the replay and don't reset the bins on one.
                if (options.HistogramSeriesOptions is not null && options.HistogramSeriesOptions.Count > 0 && options.HistogramSeriesOptions[0].BinsDescriptor.Bins == 0)
                {
                    for (int i = 0; i < series.Count; i++)
                        options.Reset(true, 0, i, (DoubleSeries)series[i]);
                }
                FillFormFromOptions();
            }
        }

        void options_ScaleChanged(object? sender, EventArgs e)
        {
            ScaleChanged?.Invoke(this, e);
        }

        public bool FillOptionsFromForm()
        {
            bool optionsAreOk = true;

            options.PoolVariablesForBins = chkPoolVariables.Checked;
            options.BinChoiceMethod = ToBinChoiceMethod((string)cboBinChoiceMethod.SelectedItem);

            HistogramSeriesOptions seriesOptions = options.HistogramSeriesOptions[currentSeriesIndex];
            if (!int.TryParse(txtBins.Text, out int bins))
            {
                FailAndHighlight(txtBins);
                optionsAreOk = false;
            }
            if (!double.TryParse(txtMidpointInterval.Text, out double midpointInterval))
            {
                FailAndHighlight(txtMidpointInterval);
                optionsAreOk = false;
            }
            if (!double.TryParse(txtMinimumMidpoint.Text, out double minimumBinMidpoint))
            {
                FailAndHighlight(txtMinimumMidpoint);
                optionsAreOk = false;
            }
            double minimum = minimumBinMidpoint - midpointInterval / 2.0;
            double maximum = minimum + bins * midpointInterval;
            double[] edges = HistogramBinChooser.Linspace(minimum, maximum, bins);
            double[] sortedData = HistogramSeriesOptions.ExtractNonMissingDataAndSort((DoubleSeries)definition.YSeries[currentSeriesIndex], out int actualRows);
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
                cboBinChoiceMethod.SelectedItem = ToDisplayString(options.BinChoiceMethod);
                IReadOnlyList<ISeries> series = definition.XSeries.Count > 0
                    ? definition.XSeries
                    : definition.YSeries;
                fillingForm = true;
                if (0 == cboVariable.Items.Count)
                {
                    foreach (ISeries s in series)
                        cboVariable.Items.Add(s.Title);
                }
                chkPoolVariables.Enabled = series.Count > 1;
                chkPoolVariables.Checked = options.PoolVariablesForBins;
                cmdNextVariable.Enabled = currentSeriesIndex < series.Count - 1;
                cmdPreviousVariable.Enabled = currentSeriesIndex > 0;
                cboVariable.SelectedIndex = currentSeriesIndex;

                HistogramSeriesOptions seriesOptions = options.HistogramSeriesOptions[currentSeriesIndex];
                BinsDescriptor descriptor = seriesOptions.BinsDescriptor;
                double minimumBinMidpoint = (descriptor.Edges[0] + descriptor.Edges[1]) / 2.0;
                double maximumBinMidpoint = (descriptor.Edges[descriptor.Bins] + descriptor.Edges[descriptor.Bins - 1]) / 2.0;
                double binMidpointInterval = (maximumBinMidpoint - minimumBinMidpoint) / (descriptor.Bins - 1);

                lblDataMaximum.Text = descriptor.HighestEdge.ToString("G12");
                lblDataMinimum.Text = descriptor.LowestEdge.ToString("G12");
                txtBins.Text = descriptor.Bins.ToString();
                txtMidpointInterval.Text = binMidpointInterval.ToString("G12");
                txtMinimumMidpoint.Text = minimumBinMidpoint.ToString("G12");
                chkShowRelativeFrequencies.Checked = options.ShowRelativeFrequencies;

                if (options.IsAscii)
                    chkOverlayNormalCurve.Enabled = false;
                else
                    chkOverlayNormalCurve.Checked = options.OverlayNormalCurve;
                ListBins();
                cboBinChoiceMethod.AutoSizeToList();
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
            if (!int.TryParse(txtBins.Text, out int mp))
                FailAndHighlight(txtBins);
            if (!double.TryParse(txtMidpointInterval.Text, out double zInt))
                FailAndHighlight(txtMidpointInterval);
            if (!double.TryParse(txtMinimumMidpoint.Text, out double zMin))
                FailAndHighlight(txtMinimumMidpoint);
            lstBinValues.Items.Clear();
            for (int i = 0; i < mp; i++)
            {
                double leftPoint = Q0(zMin, zMin + zInt * (i - 0.5));
                double midPoint = Q0(zMin, zMin + zInt * i);
                double rightPoint = Q0(zMin, zMin + zInt * (i + 0.5));
                ListViewItem item = new() { Text = Formatting.XRound(leftPoint, 9)};
                item.SubItems.Add(Formatting.XRound(midPoint, 9));
                item.SubItems.Add(Formatting.XRound(rightPoint, 9));
                lstBinValues.Items.Add(item);
            }
        }

        /// <summary>
        /// Snap q to 0 if it is small and zmin is not - useful when binning to provide reasonable displays.
        /// </summary>
        private static double Q0(double zmin, double q)
        {
            if (Math.Abs(zmin) > Constant.EPSILON && Math.Abs(q) < Constant.EPSILON)
                return 0.0;
            return q;
        }

        private void cmdAutoBins_Click(object? sender, EventArgs e)
        {
            try
            {
                DoAutoBins();
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        private void DoAutoBins()
        {
            IReadOnlyList<ISeries> series = definition.XSeries.Count > 0
                ? definition.XSeries
                : definition.YSeries;
            options.BinChoiceMethod = ToBinChoiceMethod((string)cboBinChoiceMethod.SelectedItem);
            options.PoolVariablesForBins = chkPoolVariables.Checked;
            options.Reset(true, 0, currentSeriesIndex, (DoubleSeries)series[currentSeriesIndex]);
            FillFormFromOptions();
        }

        private void cmdAutoMidpoints_Click(object? sender, EventArgs e)
        {
            try
            {
                int bins = Parsing.Cint_Txt(txtBins.Text);
                options.PoolVariablesForBins = chkPoolVariables.Checked;
                IReadOnlyList<ISeries> series = definition.XSeries.Count > 0
                    ? definition.XSeries
                    : definition.YSeries;
                options.Reset(false, bins, currentSeriesIndex, (DoubleSeries)series[currentSeriesIndex]);
                FillFormFromOptions();
            }
            catch (FormatException)
            {
                SdApplication.MsgboxX("Please enter the number of bins", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Histogram", true);
            }
            catch (Exception)
            {
                // Do nothing
            }
        }

        private void cmdReset_Click(object? sender, EventArgs e)
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

        private void cmdPreviousVariable_Click(object? sender, EventArgs e)
        {
            if (currentSeriesIndex > 0)
            {
                if (!FillOptionsFromForm())
                    return;
                --currentSeriesIndex;
                FillFormFromOptions();
            }
        }

        private void cmdNextVariable_Click(object? sender, EventArgs e)
        {
            IReadOnlyList<ISeries> series = definition.XSeries.Count > 0
                ? definition.XSeries
                : definition.YSeries;
            if (currentSeriesIndex < series.Count - 1)
            {
                if (!FillOptionsFromForm())
                    return;
                currentSeriesIndex++;
                FillFormFromOptions();
            }
        }

        private void cboVariable_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (!fillingForm)
            {
                // TODO: How to prevent the change if there's an issue?
                FillOptionsFromForm();
                currentSeriesIndex = cboVariable.SelectedIndex;
                FillFormFromOptions();
            }
        }

        private void cboBinChoiceMethod_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (!fillingForm)
            {
                try
                {
                    DoAutoBins();
                }
                catch (Exception)
                {
                    // Do nothing
                }
            }
        }

        private void chkShowRelativeFrequencies_CheckedChanged(object? sender, EventArgs e)
        {
            if (!FillOptionsFromForm())
                return;
            foreach (HistogramSeriesOptions hso in options.HistogramSeriesOptions)
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
            ScaleChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string ToDisplayString(BinChoiceMethod binChoiceMethod) =>
            binChoiceMethod switch
            {
                BinChoiceMethod.FreedmanDaconis => "Freedman-Daconis",
                BinChoiceMethod.Shimazaki => "Shimazaki-Shinomoto",
                BinChoiceMethod.OldStatsDirect => "StatsDirect Mid-point",
                _ => binChoiceMethod.ToString(),
            };

        private static BinChoiceMethod ToBinChoiceMethod(string displayString)
        {
            if (Enum.TryParse(displayString, out BinChoiceMethod binChoiceMethod))
                return binChoiceMethod;
            if ("Shimazaki-Shinomoto".Equals(displayString))
                return BinChoiceMethod.Shimazaki;
            if ("Freedman-Daconis".Equals(displayString))
                return BinChoiceMethod.FreedmanDaconis;
            if ("StatsDirect Mid-point".Equals(displayString))
                return BinChoiceMethod.OldStatsDirect;
            return BinChoiceMethod.NotSet;
        }
    }
}
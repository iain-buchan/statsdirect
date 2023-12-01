using System;
using System.Windows.Forms;
using StatsDirect.Charting;
using StatsDirect.Charting.Options;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    /// <summary>
    /// A user interface for modifying box+whisker options.
    /// </summary>
    public partial class ctlBoxWhiskerOptions : UserControl, IBoxWhiskerOptions
    {
        private IBoxWhiskerOptions options;
        private bool settingValues;

        ISdPreferences SdPreferences { get; }

        public event EventHandler? XAxisTitleChanged;

        public ctlBoxWhiskerOptions(ISdPreferences sdPreferences)
        {
            SdPreferences = sdPreferences;
            InitializeComponent();
        }

        public ChartDefinition ChartDefinition
        {
            set
            {
                options = (IBoxWhiskerOptions)value.ChartOptions;
                FillFormFromOptions();
            }
        }

        private BoxWhiskerMethod GetMethodFromForm()
        {
            if (rdoMethodMCIR.Checked)
                return BoxWhiskerMethod.MeanConfidenceIntervalRange;
            if (rdoMethodMSDR.Checked)
                return BoxWhiskerMethod.MeanStandardDeviationRange;
            if (rdoMethodMSER.Checked)
                return BoxWhiskerMethod.MeanStandardErrorRange;
            if (rdoMethodMQR.Checked)
                return BoxWhiskerMethod.MedianQuartilesRange;
            if (rdoMethodSevenNumberSummary.Checked)
                return BoxWhiskerMethod.SevenNumberSummary;
            if (rdoMethodBowley.Checked)
                return BoxWhiskerMethod.BowleySummary;
            // Should never happen!  Return a default.
            return BoxWhiskerMethod.MeanConfidenceIntervalRange;
        }

        public void FillFormFromOptions()
        {
            chkMarkMeanAndMedian.Checked = options.MarkMeanAndMedian;
            FillFencesFromOptions();
            settingValues = true;
            cboCco.Text = (SdPreferences.DefaultConfidenceInterval * 100.0).ToString("N0");
            FillMethodFromOptions();
            SetFenceAvailability();
            XAxisTitleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void FillFencesFromOptions()
        {
            settingValues = true;
            chkUseInnerFence.Checked = options.UseInnerFence;
            chkUseOuterFence.Checked = options.UseOuterFence;
            settingValues = false;
        }

        private void FillMethodFromOptions()
        {
            settingValues = true;
            switch (options.Method)
            {
                case BoxWhiskerMethod.MeanConfidenceIntervalRange:
                    rdoMethodMCIR.Checked = true;
                    break;
                case BoxWhiskerMethod.MeanStandardDeviationRange:
                    rdoMethodMSDR.Checked = true;
                    break;
                case BoxWhiskerMethod.MeanStandardErrorRange:
                    rdoMethodMSER.Checked = true;
                    break;
                case BoxWhiskerMethod.MedianQuartilesRange:
                    rdoMethodMQR.Checked = true;
                    break;
                case BoxWhiskerMethod.SevenNumberSummary:
                    rdoMethodSevenNumberSummary.Checked = true;
                    break;
                case BoxWhiskerMethod.BowleySummary:
                    rdoMethodBowley.Checked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("options", options.Method, "Unknown options.Method");
            }
            cboCco.Enabled = rdoMethodMCIR.Checked;
            settingValues = false;
        }

        private void NoteMethodChanged()
        {
            FillMethodFromOptions();
            SetFenceAvailability();
            XAxisTitleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetFenceAvailability()
        {
            grpFences.Enabled = FencesAreAvailable;
        }

        private bool FencesAreAvailable =>
            BoxWhiskerMethod.MedianQuartilesRange == options.Method
                || BoxWhiskerMethod.MeanStandardDeviationRange == options.Method
                || BoxWhiskerMethod.MeanStandardErrorRange == options.Method
                || BoxWhiskerMethod.MeanConfidenceIntervalRange == options.Method;

        double IBoxWhiskerOptions.Cco =>
            double.TryParse(cboCco.Text, out double cco)
                ? cco / 100.0
                : options.Cco;
        bool IBoxWhiskerOptions.MarkMeanAndMedian => chkMarkMeanAndMedian.Checked;
        BoxWhiskerMethod IBoxWhiskerOptions.Method => GetMethodFromForm();
        bool IBoxWhiskerOptions.UseInnerFence => chkUseInnerFence.Checked;
        bool IBoxWhiskerOptions.UseOuterFence => chkUseOuterFence.Checked;

        private void NoteFencesChanged()
        {
            if (!settingValues)
                XAxisTitleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NoteMarkMeanAndMedianChanged()
        {
            if (!settingValues)
                XAxisTitleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NoteCcoChanged()
        {
            if (!settingValues)
                XAxisTitleChanged?.Invoke(this, EventArgs.Empty);
        }

        private void cboCco_SelectedIndexChanged(object? sender, EventArgs e) => NoteCcoChanged();
        private void cboCco_TextUpdate(object? sender, EventArgs e) => NoteCcoChanged();
        private void chkMarkMeanAndMedian_CheckedChanged(object? sender, EventArgs e) => NoteMarkMeanAndMedianChanged();
        private void chkUseInnerFence_CheckedChanged(object? sender, EventArgs e) => NoteFencesChanged();
        private void chkUseOuterFence_CheckedChanged(object? sender, EventArgs e) => NoteFencesChanged();
        private void rdoMethod_CheckedChanged(object? sender, EventArgs e) => NoteMethodChanged();
    }
}
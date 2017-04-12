using System;
using System.Windows.Forms;
using StatsDirect.Charting;

namespace StatsDirect.UI
{
    /// <summary>
    /// A user interface for modifying box+whisker options.
    /// </summary>
    public partial class ctlBoxWhiskerOptions : UserControl, IOkable
    {
        private ChartDefinition definition;
        private BoxWhiskerOptions options;
        private bool settingValues;

        public event EventHandler XAxisTitleChanged;

        public ctlBoxWhiskerOptions()
        {
            InitializeComponent();
        }

        public ChartDefinition ChartDefinition
        {
            set
            {
                definition = value;
                options = (BoxWhiskerOptions)definition.ChartOptions;
                FillFormFromOptions();
            }
        }

        void IOkable.OkClicked()
        {
            FillOptionsFromForm();
        }

        public void FillOptionsFromForm()
        {
            FillOptionsFromMarkMeanAndMedian();
            FillOptionsFromFences();
            FillOptionsFromType();
            FillOptionsFromCco();
        }

        public void FillOptionsFromMarkMeanAndMedian()
        {
            options.MarkMeanAndMedian = chkMarkMeanAndMedian.Checked;
        }

        public void FillOptionsFromCco()
        {
            if (double.TryParse(cboCco.Text, out options.Cco))
                options.Cco /= 100.0;
        }

        private void FillOptionsFromFences()
        {
            options.UseInnerFence = chkUseInnerFence.Checked;
            options.UseOuterFence = chkUseOuterFence.Checked;
        }

        private void FillOptionsFromType()
        {
            if (rdoMethodMCIR.Checked)
                options.Method = BoxWhiskerOptions.BoxWhiskerMethod.MeanConfidenceIntervalRange;
            else if (rdoMethodMSDR.Checked)
                options.Method = BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardDeviationRange;
            else if (rdoMethodMSER.Checked)
                options.Method = BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardErrorRange;
            else if (rdoMethodMQR.Checked)
                options.Method = BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange;
            else if (rdoMethodSevenNumberSummary.Checked)
                options.Method = BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary;
            else if (rdoMethodBowley.Checked)
                options.Method = BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary;
        }

        public void FillFormFromOptions()
        {
            options.SetDefaultXAxisTitle();
            chkMarkMeanAndMedian.Checked = options.MarkMeanAndMedian;
            FillFencesFromOptions();
            settingValues = true;
            cboCco.Text = (SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval * 100.0).ToString("N0");
            FillTypeFromOptions();
            SetFenceAvailability();
            if (null != XAxisTitleChanged)
                XAxisTitleChanged(this, EventArgs.Empty);
        }

        private void FillFencesFromOptions()
        {
            settingValues = true;
            chkUseInnerFence.Checked = options.UseInnerFence;
            chkUseOuterFence.Checked = options.UseOuterFence;
            settingValues = false;
        }

        private void FillTypeFromOptions()
        {
            settingValues = true;
            switch (options.Method)
            {
                case BoxWhiskerOptions.BoxWhiskerMethod.MeanConfidenceIntervalRange:
                    rdoMethodMCIR.Checked = true;
                    break;
                case BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardDeviationRange:
                    rdoMethodMSDR.Checked = true;
                    break;
                case BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardErrorRange:
                    rdoMethodMSER.Checked = true;
                    break;
                case BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange:
                    rdoMethodMQR.Checked = true;
                    break;
                case BoxWhiskerOptions.BoxWhiskerMethod.SevenNumberSummary:
                    rdoMethodSevenNumberSummary.Checked = true;
                    break;
                case BoxWhiskerOptions.BoxWhiskerMethod.BowleySummary:
                    rdoMethodBowley.Checked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("options", options.Method, "Unknown options.Method");
            }
            cboCco.Enabled = rdoMethodMCIR.Checked;
            settingValues = false;
        }

        private void rdoMethod_CheckedChanged(object sender, EventArgs e)
        {
            NoteTypeChanged();
        }

        private void NoteTypeChanged()
        {
            FillOptionsFromType();
            if (double.TryParse(cboCco.Text, out options.Cco))
                options.Cco /= 100.0;
            options.SetDefaultXAxisTitle();
            FillTypeFromOptions();
            SetFenceAvailability();
            if (null != XAxisTitleChanged)
                XAxisTitleChanged(this, EventArgs.Empty);
        }

        private void SetFenceAvailability()
        {
            bool fencesAreAvailable = FencesAreAvailable;
            grpFences.Enabled = fencesAreAvailable;
        }

        private bool FencesAreAvailable => BoxWhiskerOptions.BoxWhiskerMethod.MedianQuartilesRange == options.Method
                                           || BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardDeviationRange == options.Method
                                           || BoxWhiskerOptions.BoxWhiskerMethod.MeanStandardErrorRange == options.Method
                                           || BoxWhiskerOptions.BoxWhiskerMethod.MeanConfidenceIntervalRange == options.Method;

        private void chkUseInnerFence_CheckedChanged(object sender, EventArgs e)
        {
            NoteFencesChanged();
        }

        private void NoteFencesChanged()
        {
            if (!settingValues)
            {
                FillOptionsFromFences();
                options.SetDefaultXAxisTitle();
                if (null != XAxisTitleChanged)
                    XAxisTitleChanged(this, EventArgs.Empty);
            }
        }

        private void NoteMarkMeanAndMedianChanged()
        {
            if (!settingValues)
            {
                FillOptionsFromMarkMeanAndMedian();
                options.SetDefaultXAxisTitle();
                if (null != XAxisTitleChanged)
                    XAxisTitleChanged(this, EventArgs.Empty);
            }
        }

        private void chkUseOuterFence_CheckedChanged(object sender, EventArgs e)
        {
            NoteFencesChanged();
        }

        private void cboCco_TextUpdate(object sender, EventArgs e)
        {
            NoteCcoChanged();
        }

        private void NoteCcoChanged()
        {
            if (!settingValues)
            {
                FillOptionsFromCco();
                options.SetDefaultXAxisTitle();
                if (null != XAxisTitleChanged)
                    XAxisTitleChanged(this, EventArgs.Empty);
            }
        }

        private void cboCco_SelectedIndexChanged(object sender, EventArgs e)
        {
            NoteCcoChanged();
        }

        private void chkMarkMeanAndMedian_CheckedChanged(object sender, EventArgs e)
        {
            NoteMarkMeanAndMedianChanged();
        }
    }
}
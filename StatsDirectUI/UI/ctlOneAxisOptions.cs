using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StatsDirect.Templates;
using System.Linq;

namespace StatsDirect.UI
{
    public partial class ctlOneAxisOptions : UserControl
    {
        private ICollection<ScaleType> allowedScaleTypes;
        private double dataMinimum;
        private double dataMinGreaterThanZero;
        private double dataMaximum;
        private int intervals;
        private double scaleMinimum;
        private double scaleMaximum;
        private int intervalsPerMajorTic;
        private string mask;
        private bool settingValues;
        private List<ScaleType> scaleTypesInCboScale;

        // Must match the indices order of ScaleType, as must the combo box entries.
        private static readonly string[] printableScaleTypes = { "Linear", "Natural log", "Log 10", "Date", "Category" };

        public ctlOneAxisOptions()
        {
            InitializeComponent();
            cboGridLines.SelectedIndex = 0;
            cboScaleTextDirection.SelectedIndex = 0;
            scaleTypesInCboScale = new List<ScaleType>();
        }

        public bool IsYAxis { get; set; }

        public ICollection<ScaleType> AllowedScaleTypes
        {
            get { return allowedScaleTypes; }
            set
            {
                allowedScaleTypes = value;
                SetFormFromAllowedScaleTypes();
            }
        }

        public double MinimumDataValue
        {
            get { return dataMinimum; }
            set
            {
                dataMinimum = value;
                SetCandidateScaleValues();
                SetDataRangeLabel();
            }
        }

        public double DataMinGreaterThanZero
        {
            get { return dataMinGreaterThanZero; }
            set
            {
                dataMinGreaterThanZero = value;
                SetCandidateScaleValues();
                SetDataRangeLabel();
            }
        }

        public double MaximumDataValue
        {
            get { return dataMaximum; }
            set
            {
                dataMaximum = value;
                SetCandidateScaleValues();
                SetDataRangeLabel();
            }
        }

        public int Intervals => intervals;

        public LabelDirection LabelDirection
        {
            get { return (LabelDirection)cboScaleTextDirection.SelectedIndex; }
            set { cboScaleTextDirection.SelectedIndex = (int)value; }
        }

        public string Mask => mask;

        public int IntervalsPerMajorTic => intervalsPerMajorTic;

        public double MinimumScaleValue => scaleMinimum;

        public double MaximumScaleValue => scaleMaximum;

        public ScaleType ScaleType
        {
            // TODO: Why does the combo on Y sometimes drift to having nothing selected?
            get { return scaleTypesInCboScale[cboScale.SelectedIndex < 0 ? 0 : cboScale.SelectedIndex]; }
            set { cboScale.SelectedIndex = scaleTypesInCboScale.IndexOf(value); }
        }

        public string Title
        {
            get { return txtTitle.Text; }
            set { txtTitle.Text = value; }
        }

        public System.Drawing.Drawing2D.DashStyle GridLineDashStyle
        {
            get
            {
                switch (cboGridLines.SelectedIndex)
                {
                    case 0:
                        return System.Drawing.Drawing2D.DashStyle.Solid;
                    case 1:
                        return System.Drawing.Drawing2D.DashStyle.Solid;
                    case 2:
                        return System.Drawing.Drawing2D.DashStyle.Dash;
                    default:
                        return System.Drawing.Drawing2D.DashStyle.Solid;
                }
            }
        }

        public bool HasGridLines => cboGridLines.SelectedIndex > 0;

        public bool HasMarkerLine
        {
            get
            {
                double scratch;
                return double.TryParse(cboMarkerLineAt.Text, out scratch);
            }
        }

        public bool HasTitle
        {
            get { return pnlTitle.Visible; }
            set { pnlTitle.Visible = value; }
        }

        public double MarkerLineValue => Utilities.Parsing.Cdbl_Txt(cboMarkerLineAt.Text);

        private void SetFormFromAllowedScaleTypes()
        {
            cboScale.Items.Clear();
            scaleTypesInCboScale.Clear();
            foreach (ScaleType scaleType in Enum.GetValues(typeof(ScaleType)))
            {
                if (allowedScaleTypes.Contains(scaleType))
                {
                    cboScale.Items.Add(printableScaleTypes[(int)scaleType]);
                    scaleTypesInCboScale.Add(scaleType);
                }
            }
            if (cboScale.Items.Count > 0)
                cboScale.SelectedIndex = 0;
        }

        private void SetScaleVisibility()
        {
            pnlScale.Visible = ShouldShowScaleChooser;
            pnlScaleTextMask.Visible = ShouldShowScaleTextMask;
            pnlScaleTextDirection.Visible = ShouldShowScaleTextDirection;
            pnlRange.Visible = ShouldShowRange;
            pnlMarkerLine.Visible = ShouldShowMarkerLine;
        }

        private bool ShouldShowMarkerLine
        {
            get
            {
                ScaleType selectedScaleType = ScaleType;
                return selectedScaleType == ScaleType.Linear;
            }
        }

        private bool ShouldShowScaleChooser => null != allowedScaleTypes && allowedScaleTypes.Count > 1;

        private bool ShouldShowRange
        {
            get
            {
                ScaleType selectedScaleType = ScaleType;
                return selectedScaleType == ScaleType.Linear
                    || selectedScaleType == ScaleType.Log10
                    || selectedScaleType == ScaleType.LogNatural;
            }
        }

        private bool ShouldShowScaleTextMask
        {
            get
            {
                ScaleType selectedScaleType = ScaleType;
                return selectedScaleType == ScaleType.Linear
                    || selectedScaleType == ScaleType.Log10
                    || selectedScaleType == ScaleType.LogNatural
                    || selectedScaleType == ScaleType.Date;
            }
        }

        private static bool ShouldShowScaleTextDirection => true;

        private void cboScale_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                SetScaleVisibility();
                SetCandidateScaleValues();
            }
            catch (Exception)
            {
                // TODO: Warn of the exception
            }
        }

        private void SetDataRangeLabel()
        {
            lblDataRange.Text = string.Format("Data range: {0} to {1}", SdApplication.SoleInstance.RoundU(MinimumDataValue), SdApplication.SoleInstance.RoundU(MaximumDataValue));
        }

        private void SetCandidateScaleValues()
        {
            // Ensure we have at least some range before trying to set the scale - this can be called, for example, when min has been set but max hasn't yet.
            if (MaximumDataValue <= MinimumDataValue)
                return;

            double qMin = MinimumDataValue;
            double qMax = MaximumDataValue;
            if (ShouldShowMarkerLine && HasMarkerLine)
            {
                double v = MarkerLineValue;
                if (v < qMin)
                    qMin = v;
                if (v > qMax)
                    qMax = v;
            }
            ScaleType selectedScaleType = ScaleType;
            IAxisScale axisScale = Charting.AxisScalerFactory.AxisScalerFor(selectedScaleType).Q_Axis(qMin, DataMinGreaterThanZero, qMax, IsYAxis);
            IList<Tic> tics = axisScale.Tics();
            intervals = tics.Count - 1;
            scaleMinimum = axisScale.MinimumScaleValue;
            scaleMaximum = axisScale.MaximumScaleValue;
            int majorTics = tics.Where(tic => tic.TicType == TicType.Major).Count();
            if (majorTics > 1)
                intervalsPerMajorTic = intervals / (majorTics - 1); // There's one extra tic - should be a major - at the end. TODO: Check this if there's ever phase added to this.
            else
                intervalsPerMajorTic = 1;
            mask = Charting.AxisMasker.AxisMask(axisScale);
            settingValues = true;
            txtScaleTextMask.Text = mask;
            txtMinimum.Text = scaleMinimum.ToString(mask);
            txtTics.Text = intervals.ToString("N0");
            txtMaximum.Text = scaleMaximum.ToString(mask);
            settingValues = false;
        }

        private void txtMinimum_TextChanged(object sender, EventArgs e)
        {
            try
            {
                RecalculateScale();
            }
            catch (Exception)
            {
                // TODO: Warn of the exception
            }
        }

        private void RecalculateScale()
        {
            if (settingValues)
                return;
            scaleMinimum = Utilities.Parsing.Cdbl_Txt(txtMinimum.Text);
            intervals = Utilities.Parsing.Cint_Txt(txtTics.Text);
            scaleMaximum = Utilities.Parsing.Cdbl_Txt(txtMaximum.Text);
        }

        private void txtTics_TextChanged(object sender, EventArgs e)
        {
            try
            {
                RecalculateScale();
            }
            catch (Exception)
            {
                // TODO: Warn of the exception
            }
        }

        private void txtMaximum_TextChanged(object sender, EventArgs e)
        {
            try
            {
                RecalculateScale();
            }
            catch (Exception)
            {
                // TODO: Warn of the exception
            }
        }

        private void txtScaleTextMask_TextChanged(object sender, EventArgs e)
        {
            if (settingValues)
                return;
            mask = txtScaleTextMask.Text;
        }

        private void cboMarkerLineAt_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                SetCandidateScaleValues();
            }
            catch (Exception)
            {
                // TODO: Warn of the exception
            }
        }
    }
}

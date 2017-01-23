using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlOneAxisOptions : UserControl
    {
        private bool hasScale;
        private ICollection<ScaleType> allowedScaleTypes;
        private double dataMin;
        private double dataMinGreaterThanZero;
        private double dataMax;
        private int div;
        private double zMin;
        private double zInt;
        private int minorTicsPerMajorTic;
        private string mask;
        private bool settingValues;
        private List<ScaleType> scaleTypesInCboScale;

        // Must match the indices order of ScaleType, as must the combo box entries.
        private static readonly string[] printableScaleTypes = { "Linear", "Natural log", "Log 10", "Date", "Category" };

        public ctlOneAxisOptions()
        {
            InitializeComponent();
            hasScale = true; // By default
            cboGridLines.SelectedIndex = 0;
            cboScaleTextDirection.SelectedIndex = 0;
            scaleTypesInCboScale = new List<ScaleType>();
        }

        public ICollection<ScaleType> AllowedScaleTypes
        {
            get { return allowedScaleTypes; }
            set
            {
                allowedScaleTypes = value;
                SetFormFromAllowedScaleTypes();
            }
        }

        public double DataMin
        {
            get { return dataMin; }
            set
            {
                dataMin = value;
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

        public double DataMax
        {
            get { return dataMax; }
            set
            {
                dataMax = value;
                SetCandidateScaleValues();
                SetDataRangeLabel();
            }
        }

        public int Div
        {
            get { return div; }
        }

        public LabelDirection LabelDirection
        {
            get { return (LabelDirection)cboScaleTextDirection.SelectedIndex; }
            set { cboScaleTextDirection.SelectedIndex = (int)value; }
        }

        public string Mask
        {
            get { return mask; }
        }

        public int MinorTicsPerMajorTic
        {
            get { return minorTicsPerMajorTic; }
        }

        public double ScaleMin
        {
            get { return zMin; }
        }

        public double ScaleMax
        {
            get { return zMin + div * zInt; }
        }

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

        public double ZInt
        {
            get { return zInt; }
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

        public bool HasGridLines
        {
            get { return cboGridLines.SelectedIndex > 0; }
        }

        public bool HasMarkerLine
        {
            get
            {
                double scratch;
                return double.TryParse(cboMarkerLineAt.Text, out scratch);
            }
        }

        public bool HasScale
        {
            get { return hasScale; }
            set
            {
                hasScale = value;
                SetScaleVisibility();
            }
        }

        public bool HasTitle
        {
            get { return pnlTitle.Visible; }
            set { pnlTitle.Visible = value; }
        }

        public double MarkerLineValue
        {
            get
            {
                return Utilities.Parsing.Cdbl_Txt(cboMarkerLineAt.Text);
            }
        }

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
            pnlScale.Visible = hasScale;
            pnlScaleTextMask.Visible = hasScale && ShouldShowScaleTextMask;
            pnlScaleTextDirection.Visible = hasScale && ShouldShowScaleTextDirection;
            pnlRange.Visible = hasScale && ShouldShowRange;
            pnlMarkerLine.Visible = hasScale && ShouldShowMarkerLine;
        }

        private bool ShouldShowMarkerLine
        {
            get
            {
                ScaleType selectedScaleType = ScaleType;
                return selectedScaleType == ScaleType.Linear;
            }
        }

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

        private static bool ShouldShowScaleTextDirection
        {
            get { return true; }
        }

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
            lblDataRange.Text = string.Format("Data range: {0} to {1}", SdApplication.SoleInstance.RoundU(DataMin), SdApplication.SoleInstance.RoundU(DataMax));
        }

        private void SetCandidateScaleValues()
        {
            // Ensure we have at least some range before trying to set the scale - this can be called, for example, when min has been set but max hasn't yet.
            if (DataMax <= DataMin)
                return;

            double qMin = DataMin;
            double qMax = DataMax;
            if (ShouldShowMarkerLine && HasMarkerLine)
            {
                double v = MarkerLineValue;
                if (v < qMin)
                    qMin = v;
                if (v > qMax)
                    qMax = v;
            }
            ScaleType selectedScaleType = ScaleType;
            Charting.AxisScaler.Q_Axis(ref qMin, DataMinGreaterThanZero, ref qMax, out div, out zMin, out zInt, out minorTicsPerMajorTic, selectedScaleType);
            mask = Charting.AxisScaler.AxisMask(zInt, zMin, div, minorTicsPerMajorTic, selectedScaleType);
            settingValues = true;
            txtScaleTextMask.Text = mask;
            txtMinimum.Text = zMin.ToString(mask);
            txtTics.Text = div.ToString("N0");
            txtInterval.Text = zInt.ToString(/*mask*/); // #1090: Don't use mask as it's calculated for the major tics; minor tics may well require more decimal places.
            settingValues = false;
            lblMaximumValue.Text = (zMin + div * zInt).ToString(mask);
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
            zMin = Utilities.Parsing.Cdbl_Txt(txtMinimum.Text);
            div = Utilities.Parsing.Cint_Txt(txtTics.Text);
            zInt = Utilities.Parsing.Cdbl_Txt(txtInterval.Text);
            lblMaximumValue.Text = ScaleMax.ToString(mask);
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

        private void txtInterval_TextChanged(object sender, EventArgs e)
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
            try
            {
                lblMaximumValue.Text = ScaleMax.ToString(mask);
            }
            catch (FormatException)
            {
                // TODO: Warn of an invalid format
            }
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

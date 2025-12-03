using StatsDirect.Charting;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlOneAxisOptions : UserControl
    {
        private ICollection<ScaleType> allowedScaleTypes;
        private double dataMinimum;
        private double dataMinGreaterThanZero;
        private double dataMaximum;
        private bool settingValues;
        private readonly List<ScaleType> scaleTypesInCboScale;

        // Must match the indices order of ScaleType, as must the combo box entries.
        private static readonly string[] PRINTABLE_SCALE_TYPES = { "Linear", "Natural log", "Log 10", "Date", "Category" };

        public ctlOneAxisOptions()
        {
            InitializeComponent();
            cboGridLines.SelectedIndex = 0;
            cboScaleTextDirection.SelectedIndex = 0;
            scaleTypesInCboScale = new List<ScaleType>();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool IsYAxis { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ICollection<ScaleType> AllowedScaleTypes
        {
            get => allowedScaleTypes;
            set
            {
                allowedScaleTypes = value;
                SetFormFromAllowedScaleTypes();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double MinimumDataValue
        {
            get => dataMinimum;
            set
            {
                dataMinimum = value;
                SetCandidateScaleValues();
                SetDataRangeLabel();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double DataMinGreaterThanZero
        {
            get => dataMinGreaterThanZero;
            set
            {
                dataMinGreaterThanZero = value;
                SetCandidateScaleValues();
                SetDataRangeLabel();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double MaximumDataValue
        {
            get => dataMaximum;
            set
            {
                dataMaximum = value;
                SetCandidateScaleValues();
                SetDataRangeLabel();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public LabelDirection LabelDirection
        {
            get => (LabelDirection)cboScaleTextDirection.SelectedIndex;
            set => cboScaleTextDirection.SelectedIndex = (int)value;
        }

        public double MinimumScaleValue { get; private set; }

        public double MaximumScaleValue { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ScaleType ScaleType
        {
            // TODO: Why does the combo on Y sometimes drift to having nothing selected?
            get => scaleTypesInCboScale[cboScale.SelectedIndex < 0 ? 0 : cboScale.SelectedIndex];
            set => cboScale.SelectedIndex = scaleTypesInCboScale.IndexOf(value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string Title
        {
            get => txtTitle.Text;
            set => txtTitle.Text = value;
        }

        public DashStyleDescriptor GridLineDashStyle
        {
            get
            {
                switch (cboGridLines.SelectedIndex)
                {
                    case 0:
                        return DashStyleDescriptor.Solid;
                    case 1:
                        return DashStyleDescriptor.Solid;
                    case 2:
                        return DashStyleDescriptor.Dash;
                    default:
                        return DashStyleDescriptor.Solid;
                }
            }
        }

        public bool HasGridLines => cboGridLines.SelectedIndex > 0;

        public bool HasMarkerLine => double.TryParse(cboMarkerLineAt.Text, out double _);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasTitle
        {
            get => pnlTitle.Visible;
            set => pnlTitle.Visible = value;
        }

        public double MarkerLineValue => Utilities.Parsing.Cdbl_Txt(cboMarkerLineAt.Text);

        private void SetFormFromAllowedScaleTypes()
        {
            cboScale.Items.Clear();
            scaleTypesInCboScale.Clear();
            if (null != allowedScaleTypes)
            {
                foreach (ScaleType scaleType in Enum.GetValues(typeof(ScaleType)))
                {
                    if (allowedScaleTypes.Contains(scaleType))
                    {
                        cboScale.Items.Add(PRINTABLE_SCALE_TYPES[(int)scaleType]);
                        scaleTypesInCboScale.Add(scaleType);
                    }
                }
            }
            if (cboScale.Items.Count > 0)
                cboScale.SelectedIndex = 0;
        }

        private void SetScaleVisibility()
        {
            pnlScale.Visible = ShouldShowScaleChooser;
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
            lblDataRange.Text = $"Data range: {SdApplication.SoleInstance.RoundU(MinimumDataValue)} to {SdApplication.SoleInstance.RoundU(MaximumDataValue)}";
        }

        private void SetCandidateScaleValues()
        {
            // Ensure we have at least some range before trying to set the scale - this can be called, for example, when min has been set but max hasn't yet.
            if (MaximumDataValue <= MinimumDataValue)
                return;

            double minimumValue = MinimumDataValue;
            double maximumValue = MaximumDataValue;
            if (ShouldShowMarkerLine && HasMarkerLine)
            {
                double v = MarkerLineValue;
                if (v < minimumValue)
                    minimumValue = v;
                if (v > maximumValue)
                    maximumValue = v;
            }
            ScaleType selectedScaleType = ScaleType;
            IAxisScale axisScale = Charting.AxisScalerFactory.AxisScalerFor(selectedScaleType).QAxis(minimumValue, DataMinGreaterThanZero, maximumValue, IsYAxis, false);
            MinimumScaleValue = axisScale.MinimumScaleValue;
            MaximumScaleValue = axisScale.MaximumScaleValue;
            settingValues = true;
            txtMinimum.Text = MinimumScaleValue.ToString();
            txtMaximum.Text = MaximumScaleValue.ToString();
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
            MinimumScaleValue = Utilities.Parsing.Cdbl_Txt(txtMinimum.Text);
            MaximumScaleValue = Utilities.Parsing.Cdbl_Txt(txtMaximum.Text);
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

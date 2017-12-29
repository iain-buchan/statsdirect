using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlOneSeriesOptions : UserControl
    {
        Charting.MarkerType markerType;
        private bool useColour;
        private bool showMarkerStyle;
        private bool showMarkerSize;
        private bool showMarkerColour;
        private bool showLineColour;
        private bool showLineThickness;
        private bool showDashStyle;
        private bool showFillStyle;

        public ctlOneSeriesOptions()
        {
            InitializeComponent();
        }

        public bool ShowMarkerStyle
        {
            get => showMarkerStyle;
            set
            {
                showMarkerStyle = value;
                pnlMarkerStyle.Visible = value;
            }
        }

        public bool ShowMarkerSize
        {
            get => showMarkerSize;
            set
            {
                showMarkerSize = value;
                pnlMarkerSize.Visible = value;
            }
        }

        public bool ShowMarkerColour
        {
            get => showMarkerColour;
            set
            {
                showMarkerColour = value;
                grpMarkerColour.Visible = value && useColour;
            }
        }

        public bool ShowLineColour
        {
            get => showLineColour;
            set
            {
                showLineColour = value;
                grpLineColour.Visible = value && useColour;
            }
        }

        public bool ShowLineThickness
        {
            get => showLineThickness;
            set 
            {
                showLineThickness = value;
                pnlLineThickness.Visible = value;
            }
        }

        public bool ShowDashStyle
        {
            get => showDashStyle;
            set
            {
                showDashStyle = value;
                pnlDashStyle.Visible = value && !useColour;
            }
        }

        public bool ShowFillStyle
        {
            get => showFillStyle;
            set
            {
                showFillStyle = value;
                pnlFillStyle.Visible = showFillStyle && !useColour;
                chkFillMarker.Visible = !(showFillStyle && !useColour);
            }
        }

        public Charting.MarkerType MarkerType
        {
            get => markerType;
            set
            {
                markerType = value;
                SetFormFromMarkerType();
            }
        }

        /// <summary>
        /// Save the edited marker details into the marker
        /// </summary>
        public void Save()
        {
            SetMarkerTypeFromForm();
        }

        private void SetFormFromMarkerType()
        {
            if (null != markerType)
            {
                lineThickness.LineThickness = (int)markerType.Width;
                dashStyler.DashStyle = markerType.LineDashStyle;
                markerShaper.MarkerShape = markerType.MarkerShape;
                markerColorPanel.Color = markerType.MarkerColor;
                lineColorPanel.Color = markerType.LineColor;
                chkFillMarker.Checked = markerType.IsMarkerFilled;
                string markerString = markerType.MarkerSize.ToString();
                cboMarkerSize.Text = markerString;
                if (cboMarkerSize.Items.Contains(markerString))
                    cboMarkerSize.SelectedValue = markerString;
                fillStyler.FillStyle = markerType.MarkerFillStyle;
            }
        }

        private void SetMarkerTypeFromForm()
        {
            if (null != markerType)
            {
                markerType.MarkerColor = markerColorPanel.Color;
                markerType.LineColor = lineColorPanel.Color;
                markerType.IsMarkerFilled = chkFillMarker.Checked;
                double.TryParse(cboMarkerSize.Text, out double markerSize);
                markerType.MarkerSize = markerSize;
                markerType.MarkerShape = markerShaper.MarkerShape;
                markerType.LineDashStyle = dashStyler.DashStyle;
                markerType.Width = lineThickness.LineThickness;
                markerType.MarkerFillStyle = fillStyler.FillStyle;
            }
        }


        public void SetColour(bool useColour)
        {
            this.useColour = useColour;
            grpMarkerColour.Visible = showMarkerColour && useColour;
            grpLineColour.Visible = showLineColour && useColour;
            pnlDashStyle.Visible = showDashStyle && !useColour;
            pnlFillStyle.Visible = showFillStyle && !useColour;
            chkFillMarker.Visible = !(showFillStyle && !useColour);
        }

        internal void SetShowLineOptions(bool showLineOptions)
        {
            ShowLineColour = showLineOptions;
            ShowLineThickness = showLineOptions;
            ShowDashStyle = showLineOptions;
        }

        internal void SetShowMarkerOptions(bool showMarkerOptions)
        {
            ShowMarkerColour = showMarkerOptions;
            ShowMarkerSize = showMarkerOptions;
            ShowMarkerStyle = showMarkerOptions;
        }
    }
}

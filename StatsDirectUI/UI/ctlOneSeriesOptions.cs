using StatsDirect.Charting;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal partial class ctlOneSeriesOptions : UserControl
    {
        private const int DEFAULT_MARKER_SIZE = 6; // TODO: Fetch from some configuration source

        MarkerType markerType;
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

        public MarkerType MarkerType
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
                markerColorPanel.Color = ToColor(markerType.MarkerColor);
                lineColorPanel.Color = ToColor(markerType.LineColor);
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
            if (!double.TryParse(cboMarkerSize.Text, out double markerSize))
                markerSize = (null != markerType) ? markerType.MarkerSize : DEFAULT_MARKER_SIZE;
            markerType = new(
                chkFillMarker.Checked,
                ToColorDescriptor(lineColorPanel.Color),
                dashStyler.DashStyle,
                ToColorDescriptor(markerColorPanel.Color),
                fillStyler.FillStyle,
                markerShaper.MarkerShape,
                markerSize,
                lineThickness.LineThickness
                );
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

        private static Color ToColor(ColorDescriptor color) => Color.FromArgb(color.R, color.G, color.B);

        private static ColorDescriptor ToColorDescriptor(Color color) => new ColorDescriptor(color.R, color.G, color.B);
    }
}

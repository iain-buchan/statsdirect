using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlOneSeriesOptions : UserControl
    {
        Charting.MarkerType markerType;
        bool useColour;
        bool showMarkerStyle;
        bool showMarkerSize;
        bool showMarkerColour;
        bool showLineThickness;
        bool showDashStyle;
        bool showFillStyle;

        public ctlOneSeriesOptions()
        {
            InitializeComponent();
        }

        public bool ShowMarkerStyle
        {
            get { return showMarkerStyle; }
            set
            {
                showMarkerStyle = value;
                pnlMarkerStyle.Visible = value;
            }
        }

        public bool ShowMarkerSize
        {
            get { return showMarkerSize; }
            set
            {
                showMarkerSize = value;
                pnlMarkerSize.Visible = value;
            }
        }

        public bool ShowMarkerColour
        {
            get { return showMarkerColour; }
            set
            {
                showMarkerColour = value;
                grpColour.Visible = value && useColour;
            }
        }

        public bool ShowLineThickness
        {
            get { return showLineThickness; }
            set 
            {
                showLineThickness = value;
                pnlLineThickness.Visible = value;
            }
        }

        public bool ShowDashStyle
        {
            get { return showDashStyle; }
            set
            {
                showDashStyle = value;
                pnlDashStyle.Visible = value && !useColour;
            }
        }

        public bool ShowFillStyle
        {
            get { return showFillStyle; }
            set
            {
                showFillStyle = value;
                pnlFillStyle.Visible = showFillStyle && !useColour;
                chkFillMarker.Visible = !(showFillStyle && !useColour);
            }
        }

        public Charting.MarkerType MarkerType
        {
            get { return markerType; }
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
                ctlDashStyle1.DashStyle = markerType.Style;
                ctlMarkerShape1.MarkerShape = markerType.Shape;
                colorPanel.Color = markerType.Color;
                chkFillMarker.Checked = markerType.IsFilled;
                string markerString = markerType.MarkerSize.ToString();
                cboMarkerSize.Text = markerString;
                if (cboMarkerSize.Items.Contains(markerString))
                    cboMarkerSize.SelectedValue = markerString;
                ctlFillStyle1.FillStyle = markerType.FillStyle;
            }
        }

        private void SetMarkerTypeFromForm()
        {
            if (null != markerType)
            {
                markerType.Color = colorPanel.Color;
                markerType.IsFilled = chkFillMarker.Checked;
                double.TryParse(cboMarkerSize.Text, out markerType.MarkerSize);
                markerType.Shape = ctlMarkerShape1.MarkerShape;
                markerType.Style = ctlDashStyle1.DashStyle;
                markerType.Width = lineThickness.LineThickness;
                markerType.FillStyle = ctlFillStyle1.FillStyle;
            }
        }


        public void SetColour(bool useColour)
        {
            this.useColour = useColour;
            grpColour.Visible = showMarkerColour && useColour;
            pnlDashStyle.Visible = showDashStyle && !useColour;
            pnlFillStyle.Visible = showFillStyle && !useColour;
            chkFillMarker.Visible = !(showFillStyle && !useColour);
        }
    }
}

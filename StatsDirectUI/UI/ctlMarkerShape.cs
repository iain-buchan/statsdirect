using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlMarkerShape : UserControl
    {
        private Charting.MarkerShape markerShape;

        [Browsable(true)]
        public EventHandler MarkerShapeChanged;

        public ctlMarkerShape()
        {
            InitializeComponent();
            SetupArrays();
            MarkerShape = Charting.MarkerShape.Circle;
        }

        private void SetupArrays()
        {
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 0));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 1));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 2));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 3));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 4));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 5));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 6));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 7));
            cboMarkerShape.Items.Add(new ComboBoxExItem("", 8));
        }

        public Charting.MarkerShape MarkerShape
        {
            get
            {
                return markerShape;
            }
            set
            {
                if (markerShape != value && value > 0 && ((int)value) <= cboMarkerShape.Items.Count)
                {
                    markerShape = value;
                    cboMarkerShape.SelectedIndex = ((int)value) - 1;
                    OnMarkerShapeChanged(EventArgs.Empty);
                }
            }
        }

        protected virtual void OnMarkerShapeChanged(EventArgs e)
        {
            if (null != MarkerShapeChanged)
                MarkerShapeChanged(this, e);
        }

        private void cboMarkerShape_SelectedIndexChanged(object sender, EventArgs e)
        {
            MarkerShape = (Charting.MarkerShape)(cboMarkerShape.SelectedIndex + 1);
        }
    }
}

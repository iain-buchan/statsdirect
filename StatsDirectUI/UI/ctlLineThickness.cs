using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlLineThickness : UserControl
    {
        private int lineThickness;

        [Browsable(true)]
        public EventHandler LineThicknessChanged;

        public ctlLineThickness()
        {
            InitializeComponent();
            SetupArrays();
        }

        private void SetupArrays()
        {
            cboLineThickness.Items.Add(new ComboBoxExItem(string.Empty, 0));
            cboLineThickness.Items.Add(new ComboBoxExItem(string.Empty, 1));
            cboLineThickness.Items.Add(new ComboBoxExItem(string.Empty, 2));
            cboLineThickness.Items.Add(new ComboBoxExItem(string.Empty, 3));
        }

        [Browsable(true)]
        public int LineThickness
        {
            get
            {
                return lineThickness;
            }
            set
            {
                if (lineThickness != value && value > 0 && value <= cboLineThickness.Items.Count)
                {
                    lineThickness = value;
                    cboLineThickness.SelectedIndex = value - 1;
                    OnLineThicknessChanged(EventArgs.Empty);
                }
            }
        }

        protected virtual void OnLineThicknessChanged(EventArgs e)
        {
            if (null != LineThicknessChanged)
                LineThicknessChanged(this, e);
        }

        private void cboLineThickness_SelectedIndexChanged(object sender, EventArgs e)
        {
            LineThickness = cboLineThickness.SelectedIndex + 1;
        }
    }
}

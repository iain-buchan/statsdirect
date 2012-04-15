using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlFillStyle : UserControl
    {
        private Charting.FillStyle fillStyle;

        [Browsable(true)]
        public EventHandler FillStyleChanged;

        public ctlFillStyle()
        {
            InitializeComponent();
            SetupArrays();
            FillStyle = Charting.FillStyle.None;
        }

        private void SetupArrays()
        {
            cboFillStyle.Items.Add(new ComboBoxExItem("", 0));
            cboFillStyle.Items.Add(new ComboBoxExItem("", 1));
            cboFillStyle.Items.Add(new ComboBoxExItem("", 2));
            cboFillStyle.Items.Add(new ComboBoxExItem("", 3));
            cboFillStyle.Items.Add(new ComboBoxExItem("", 4));
        }

        public Charting.FillStyle FillStyle
        {
            get
            {
                return fillStyle;
            }
            set
            {
                if (fillStyle != value && value >= 0 && ((int)value) < cboFillStyle.Items.Count)
                {
                    fillStyle = value;
                    cboFillStyle.SelectedIndex = (int)value;
                    OnFillStyleChanged(EventArgs.Empty);
                }
            }
        }

        protected virtual void OnFillStyleChanged(EventArgs e)
        {
            if (null != FillStyleChanged)
                FillStyleChanged(this, e);
        }

        private void cboFillStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            fillStyle = (Charting.FillStyle)cboFillStyle.SelectedIndex;
        }
    }
}

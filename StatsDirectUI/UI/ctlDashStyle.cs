using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlDashStyle : UserControl
    {
        private System.Drawing.Drawing2D.DashStyle dashStyle = System.Drawing.Drawing2D.DashStyle.Custom;

        [Browsable(true)]
        public EventHandler DashStyleChanged;

        public ctlDashStyle()
        {
            InitializeComponent();
            SetupArrays();
        }

        private void SetupArrays()
        {
            cboDashStyle.Items.Add(new ComboBoxExItem(string.Empty, 0));
            cboDashStyle.Items.Add(new ComboBoxExItem(string.Empty, 1));
            cboDashStyle.Items.Add(new ComboBoxExItem(string.Empty, 2));
            cboDashStyle.Items.Add(new ComboBoxExItem(string.Empty, 3));
            cboDashStyle.Items.Add(new ComboBoxExItem(string.Empty, 4));
        }

        public System.Drawing.Drawing2D.DashStyle DashStyle
        {
            get
            {
                return dashStyle;
            }
            set
            {
                if (dashStyle != value && value >= 0 && ((int)value) < cboDashStyle.Items.Count)
                {
                    dashStyle = value;
                    cboDashStyle.SelectedIndex = (int)value;
                    OnDashStyleChanged(EventArgs.Empty);
                }
            }
        }

        protected virtual void OnDashStyleChanged(EventArgs e)
        {
            if (null != DashStyleChanged)
                DashStyleChanged(this, e);
        }

        private void cboDashStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            DashStyle = (System.Drawing.Drawing2D.DashStyle)cboDashStyle.SelectedIndex;
        }
    }
}

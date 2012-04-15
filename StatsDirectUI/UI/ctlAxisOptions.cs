using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlAxisOptions : UserControl
    {
        private bool axisLabelsAreSwapped;

        public ctlAxisOptions()
        {
            InitializeComponent();
        }

        public ctlOneAxisOptions X
        {
            get { return ctlOneAxisOptionsX; }
        }

        public ctlOneAxisOptions Y
        {
            get { return ctlOneAxisOptionsY; }
        }

        public bool AxisLabelsAreSwapped
        {
            get
            {
                return axisLabelsAreSwapped;
            }
            set
            {
                if (axisLabelsAreSwapped != value)
                {
                    tabAxis.TabPages[0].Text = value ? "Y" : "X";
                    tabAxis.TabPages[1].Text = value ? "X" : "Y";
                    axisLabelsAreSwapped = value;
                }
            }
        }
    }
}

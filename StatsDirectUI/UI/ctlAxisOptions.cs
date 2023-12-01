using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal partial class ctlAxisOptions : UserControl
    {
        private bool axisLabelsAreSwapped;
        private bool xIsVisible = true;
        private bool yIsVisible = true;
        private readonly TabPage tab0;
        private readonly TabPage tab1;

        public ctlAxisOptions()
        {
            InitializeComponent();
            tab0 = tabAxis.TabPages[0];
            tab1 = tabAxis.TabPages[1];
        }

        public ctlOneAxisOptions X => ctlOneAxisOptionsX;

        public ctlOneAxisOptions Y => ctlOneAxisOptionsY;

        public bool ShowX
        {
            get => xIsVisible;
            set
            {
                xIsVisible = value;
                SetTabLabelsAndVisibility();
            }
        }

        public bool ShowY
        {
            get => yIsVisible;
            set
            {
                yIsVisible = value;
                SetTabLabelsAndVisibility();
            }
        }

        public bool AxisLabelsAreSwapped
        {
            get => axisLabelsAreSwapped;
            set
            {
                axisLabelsAreSwapped = value;
                SetTabLabelsAndVisibility();
            }
        }

        private void SetTabLabelsAndVisibility()
        {
            SuspendLayout();
            TabPage tabX = axisLabelsAreSwapped ? tab1 : tab0;
            TabPage tabY = axisLabelsAreSwapped ? tab0 : tab1;
            tabX.Text = "X";
            tabY.Text = "Y";
            // Ensure X and Y are visible iff they are shown, and are in the right order
            if (tabAxis.TabPages.Contains(tabX))
                tabAxis.TabPages.Remove(tabX);
            if (tabAxis.TabPages.Contains(tabY))
                tabAxis.TabPages.Remove(tabY);
            if (ShowX)
                tabAxis.TabPages.Add(tabX);
            if (ShowY)
                tabAxis.TabPages.Add(tabY);
            ResumeLayout();
        }
    }
}

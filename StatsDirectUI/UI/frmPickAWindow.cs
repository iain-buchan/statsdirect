using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmPickAWindow : Form
    {
        private bool wasCancelled;

        public frmPickAWindow()
        {
            InitializeComponent();
        }

        public bool WasCancelled
        {
            get { return wasCancelled; }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            wasCancelled = true;
            Close();
        }

        internal void SetWindows(IList<Pane> info, string NewName, Pane defaultSelection)
        {
            int indexToSelect = -1;
            foreach (Pane pane in info)
            {
                lstWindows.Items.Add(pane);
                if (pane.Equals(defaultSelection))
                {
                    indexToSelect = lstWindows.Items.Count - 1;
                }
            }
            if (null != NewName)
                lstWindows.Items.Add(new Pane(NewName, null, null));
            if (indexToSelect >= 0)
                lstWindows.SelectedIndex = indexToSelect;
        }
        internal Pane SelectedWindow()
        {
            if (wasCancelled)
                return null;
            if (null == lstWindows.SelectedItem)
                return null;
            return (Pane)lstWindows.SelectedItem;
        }

        private void cmdSelect_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void lstWindows_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (null == lstWindows.SelectedItem)
                wasCancelled = true;
            Close();
        }

        private void frmPickAWindow_Shown(object sender, EventArgs e)
        {
            if (lstWindows.Items.Count > 0 && lstWindows.SelectedIndex < 0)
                lstWindows.SelectedIndex = 0;
        }
    }
}
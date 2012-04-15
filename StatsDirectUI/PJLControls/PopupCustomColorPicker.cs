using System;
using System.Drawing;
using System.Windows.Forms;

namespace PJLControls
{
    public partial class PopupCustomColorPicker : Form
    {
        private bool userCancelled;

        public PopupCustomColorPicker()
        {
            InitializeComponent();
        }

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        public Color CustomColor
        {
            get { return picker.Color; }
            set { picker.Color = value; }
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            userCancelled = false;
            Close();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            userCancelled = true;
            Close();
        }
    }
}

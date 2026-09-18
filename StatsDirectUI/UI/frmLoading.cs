using System;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmLoading : Form
    {
        public frmLoading()
        {
            InitializeComponent();

            //  The designer's 177 x 90 pixels are the logo's own size, and the form does not scale.  On a scaled display that left the logo small and, worse,
            //  distorted: Windows will not make a window narrower than a minimum that grows with the scaling (204 pixels at 150%, 324 at 250%), so the form
            //  came out wider than the logo's shape and the logo was stretched to fit.  Size the form from the logo and the display's scaling, and never
            //  stretch the logo out of proportion whatever size the form ends up.
            Image logo = BackgroundImage;
            if (null != logo)
            {
                double scale = DeviceDpi / 96.0;
                ClientSize = new Size((int)Math.Round(logo.Width * scale), (int)Math.Round(logo.Height * scale));
            }
            BackgroundImageLayout = ImageLayout.Zoom;
        }
    }
}

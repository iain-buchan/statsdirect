using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlFont : UserControl
    {
        private Font userFont;

        public ctlFont()
        {
            InitializeComponent();
            userFont = Font;
        }

        public Font UserFont
        {
            get => userFont;
            set
            {
                userFont = value;
                lblDescription.Text = null == userFont ? "(no font set)" : Describe(userFont);
            }
        }

        [Browsable(true)]
        [Description("The name that will be used to describe why the font is being requested")]
        public string Purpose
        {
            get => lblPurpose.Text;
            set => lblPurpose.Text = value;
        }

        private void cmdChange_Click(object sender, EventArgs e)
        {
            UserFont = GetChangedOrOriginalFont(UserFont);
        }

        private Font GetChangedOrOriginalFont(Font original)
        {
            using (FontDialog dlg = new FontDialog())
            {
                dlg.Font = original;
                dlg.ShowColor = false;
                dlg.ShowApply = false;
                dlg.ShowEffects = false;
                dlg.ShowHelp = false;
                DialogResult result = dlg.ShowDialog(this);
                return DialogResult.OK == result ? dlg.Font : original;
            }
        }

        private static string Describe(Font font)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(font.Name);
            sb.Append(' ');
            sb.Append(font.SizeInPoints.ToString("N0"));
            sb.Append("pt");
            if (font.Bold)
                sb.Append(" bold");
            if (font.Italic)
                sb.Append(" italic");
            if (font.Strikeout)
                sb.Append(" strikeout");
            if (font.Underline)
                sb.Append(" underline");
            return sb.ToString();
        }
    }
}

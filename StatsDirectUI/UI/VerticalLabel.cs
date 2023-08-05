// From http://www.codeproject.com/KB/miscctrl/Vertical_Label_Control.aspx
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// A custom windows control to display text vertically
    /// </summary>
    [ToolboxBitmap(typeof(VerticalLabel), "VerticalLabel.ico")]
    public class VerticalLabel : Control
    {
        private string labelText;

        private readonly Container components = new();

        /// <summary>
        /// VerticalLabel constructor
        /// </summary>
        public VerticalLabel()
        {
            CreateControl();
            InitializeComponent();
            SetStyle(ControlStyles.Opaque, true);
        }

        /// <summary>
        /// Dispose override method
        /// </summary>
        /// <param name="disposing">boolean parameter</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        [System.Diagnostics.DebuggerStepThrough]
        private void InitializeComponent()
        {
            Size = new Size(24, 100);
        }

        /// <summary>
        /// OnPaint override. This is where the text is rendered vertically.
        /// </summary>
        /// <param name="e">PaintEventArgs</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            Color controlBackColor = BackColor;
            using Pen labelBorderPen = new(TransparentBackground ? Color.Empty : controlBackColor, 0);
            using SolidBrush labelBackColorBrush = new(TransparentBackground ? Color.Empty : controlBackColor);
            using SolidBrush labelForeColorBrush = new(ForeColor);
            base.OnPaint(e);
            float vlblControlWidth = Size.Width;
            float vlblControlHeight = Size.Height;
            e.Graphics.DrawRectangle(labelBorderPen, 0, 0, vlblControlWidth, vlblControlHeight);
            e.Graphics.FillRectangle(labelBackColorBrush, 0, 0, vlblControlWidth, vlblControlHeight);
            e.Graphics.TextRenderingHint = RenderingMode;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            if (TextDrawMode == VerticalLabelDrawMode.BottomUp)
            {
                const float vlblTransformX = 0;
                float vlblTransformY = vlblControlHeight;
                e.Graphics.TranslateTransform(vlblTransformX, vlblTransformY);
                e.Graphics.RotateTransform(270);
                e.Graphics.DrawString(labelText, Font, labelForeColorBrush, 0, 0);
            }
            else
            {
                e.Graphics.TranslateTransform(vlblControlWidth, 0);
                e.Graphics.RotateTransform(90);
                e.Graphics.DrawString(labelText, Font, labelForeColorBrush, 0, 0, StringFormat.GenericTypographic);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        protected override CreateParams CreateParams//v1.10 
        {
            // [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20;  // Turn on WS_EX_TRANSPARENT
                return cp;
            }
        }

        private void VerticalTextBox_Resize(object sender, System.EventArgs e)
        {
            Invalidate();
        }

        /// <summary>
        /// Graphics rendering mode. Supprot for antialiasing.
        /// </summary>
        [Category("Properties"), Description("Rendering mode.")]
        public System.Drawing.Text.TextRenderingHint RenderingMode { get; set; } = System.Drawing.Text.TextRenderingHint.SystemDefault;

        /// <summary>
        /// The text to be displayed in the control
        /// </summary>
        [Category("VerticalLabel"), Description("Text is displayed vertically in container.")]
        public override string Text
        {
            get => labelText;
            set
            {
                labelText = value;
                Invalidate();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        [Category("Properties"), Description("Whether the text will be drawn from Bottom or from Top.")]
        public VerticalLabelDrawMode TextDrawMode { get; set; } = VerticalLabelDrawMode.BottomUp;

        [Category("Properties"), Description("Whether the text will be drawn with transparent background or not.")]
        public bool TransparentBackground { get; set; }
    }
    /// <summary>
    /// Text Drawing Mode
    /// </summary>
    public enum VerticalLabelDrawMode
    {
        /// <summary>
        /// Text is drawn from bottom - up
        /// </summary>
        BottomUp = 1,
        /// <summary>
        /// Text is drawn from top to bottom
        /// </summary>
        TopDown
    }
}

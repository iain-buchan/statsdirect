// ColorPicker.cs : implementation file
//
// Part of ColorPicker controls.
//
// Author      : Philip Lee (pl@pjl.nildram.co.uk)
// Date        : 14 October 2001
//
// Copyright © PJL Consultants Ltd. 2001, All Rights Reserved                      
//
// This code may be used in compiled form in any way you desire. This
// file may be redistributed unmodified by any means PROVIDING it is 
// not sold for profit without the authors written consent, and 
// providing that this notice and the authors name is included. If 
// the source code in this file is used in any commercial application 
// then a simple email would be nice.
//
// This file is provided "as is" with no expressed or implied warranty.
// The author accepts no liability for any damage, in any form, caused
// by this code. Use it at your own risk and as with all code expect bugs!
// It's been tested but I'm not perfect.
// 
// Please use and enjoy. Please let me know of any bugs/mods/improvements 
// that you have found/implemented and I will fix/incorporate them into this
// file.

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace StatsDirect.PJLControls
{
    /// <summary>
    /// A control with a combo-box like UI that allows the user to select a color
    /// from a fixed color palette.
    /// </summary>
    /// <remarks>
    /// This control simulates a combo-box style UI but drops down a <see cref="ColorPanel">ColorPanel</see>
    /// to allow the user to select a color.
    /// </remarks>
    public class ColorPicker : UserControl
    {
        const bool defaultAutoSize = true;
        const bool defaultDisplayColor = true;
        const bool defaultDisplayColorName = false;
        const BorderStyle defaultBorderStyle = BorderStyle.FixedSingle;
        static readonly Size defaultBorderSize = SystemInformation.BorderSize;

        private BorderStyle borderStyle = defaultBorderStyle;
        private Size borderSize = defaultBorderSize;
        private Size padding = new Size(1, 2);
        private ButtonState buttonState = ButtonState.Normal;
        private Rectangle comboButtonRectangle;
        private bool bDisplayColor = defaultDisplayColor;
        private bool bDisplayColorName = defaultDisplayColorName;
        private bool autoSize = defaultAutoSize;


        /// <summary>
        /// Initializes a new instance of the ColorPicker class.
        /// </summary>
        /// <remarks>
        /// The default constructor initializes all fields to their default values.
        /// </remarks>
        public ColorPicker()
        {
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            Trace.WriteLine(string.Format("Selectable={0}", GetStyle(ControlStyles.Selectable)));
            Trace.WriteLine(string.Format("UserPaint={0}", GetStyle(ControlStyles.UserPaint)));
            Trace.WriteLine(string.Format("AllPaintingInWmPaint={0}", GetStyle(ControlStyles.AllPaintingInWmPaint)));
            Trace.WriteLine(string.Format("CacheText={0}", GetStyle(ControlStyles.CacheText)));
            Trace.WriteLine(string.Format("EnableNotifyMessage={0}", GetStyle(ControlStyles.EnableNotifyMessage)));
            Trace.WriteLine(string.Format("CanFocus={0}", CanFocus));
            Trace.WriteLine(string.Format("CanSelect={0}", CanSelect));
            Trace.WriteLine(string.Format("IsHandleCreated={0}", IsHandleCreated));

            SetStyle(ControlStyles.CacheText, true);
        }

        #region Component Designer generated code
        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            // 
            // ColorPicker
            // 
            this.Name = "ColorPicker";
            this.Size = new System.Drawing.Size(184, 32);
        }
        #endregion


        /// <summary>
        /// Overrides OnGotFocus so the control can be redrawn with the focus.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnGotFocus(System.EventArgs e)
        {
            base.OnGotFocus(e);

            Refresh();
        }


        /// <summary>
        /// Overrides OnLostFocus so the control can be redrawn without the focus.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLostFocus(System.EventArgs e)
        {
            base.OnLostFocus(e);

            Refresh();
        }


        /// <summary>
        /// Overrides OnPaint so the control can be drawn.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e)
        {
            switch (borderStyle)
            {
                case BorderStyle.Fixed3D:
                    ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Sunken, Border3DSide.All);
                    break;
                case BorderStyle.FixedSingle:
                    ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Flat, Border3DSide.All);
                    break;
            }

            // fill rectangle with window color or control color if disabled
            KnownColor color_background = Enabled ? KnownColor.Window : KnownColor.Control;
            using (SolidBrush br = new SolidBrush(Color.FromKnownColor(color_background)))
            {

                Rectangle r = ClientRectangle;
                r.Inflate(-borderSize.Width, -borderSize.Height);
                e.Graphics.FillRectangle(br, r);

                if (Focused && Enabled)
                {
                    // add focus rectangle
                    Rectangle rh = new Rectangle(r.Location, new Size(r.Width - GetComboButtonRectangle().Width, r.Height));
                    rh.Inflate(-1, -1);
                    br.Color = Color.FromKnownColor(KnownColor.Highlight);
                    e.Graphics.FillRectangle(br, rh);
                }

                // draw rectangle of pick color
                Point text_p;

                if (bDisplayColor)
                {
                    // draw a rectangle of the chosen color
                    br.Color = panel_color;
                    Rectangle r1 = r;
                    r1.Width = 30;
                    r1.Inflate(-2, -2);

                    ControlPaint.DrawBorder3D(e.Graphics, r1, Border3DStyle.Flat, Border3DSide.All);
                    r1.Inflate(-1, -1);
                    e.Graphics.FillRectangle(br, r1);

                    text_p = new Point(r1.Right + padding.Width, (ClientRectangle.Height - Font.Height) / 2);
                }
                else
                {
                    text_p = new Point(r.Left + padding.Width, (ClientRectangle.Height - Font.Height) / 2);
                }

                // draw text in fore color or control dark if disabled
                br.Color =
                    Enabled ? (Focused ? Color.FromKnownColor(KnownColor.HighlightText) : ForeColor) : Color.FromKnownColor(KnownColor.ControlDark);

                string text = bDisplayColorName ? panel_color.Name : base.Text;

                e.Graphics.DrawString(text, Font, br, text_p);

                // draw combo button
                ControlPaint.DrawComboButton(e.Graphics, GetComboButtonRectangle(), buttonState);
            }

            base.OnPaint(e);
        }

        /// <summary>
        /// Overrides OnPaintBackground.  Since the whole of the control is drawn
        /// in OnPaint, OnPaintBackground is overridden to do nothing.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // no need to paint background
        }

        /// <summary>
        /// Overrides IsInputKey.<br></br><br></br>
        /// This allows the control to tell the base class that the keys
        /// Keys.Down or Keys.Down + Keys.Alt should cause the OnKeyDown event.
        /// </summary>
        /// <param name="keyData">One of the <c>System.Windows.Forms.Keys</c> values</param>
        /// <returns><B>true</B> if keyData is one of 
        /// Keys.Down or Keys.Down+Keys.Alt.  Otherwise <B>false</B>.</returns>
        protected override bool IsInputKey(Keys keyData)
        {
            bool bIsInputKey = true;

            if (keyData == Keys.Down || keyData == (Keys.Down | Keys.Alt))
            {
                ShowDropdown();
            }
            else
            {
                bIsInputKey = base.IsInputKey(keyData);
            }

            return bIsInputKey;
        }

        private Rectangle GetComboButtonRectangle()
        {
            if (comboButtonRectangle.IsEmpty)
            {
                int comboButtonHeight = ClientRectangle.Height - borderSize.Height * 2;
                int comboButtonWidth = SystemInformation.VerticalScrollBarWidth;

                comboButtonRectangle = new Rectangle(
                    ClientRectangle.Right - comboButtonWidth - borderSize.Width,
                    ClientRectangle.Top + borderSize.Height,
                    comboButtonWidth,
                    comboButtonHeight);
            }

            return comboButtonRectangle;
        }

        private bool MouseOnComboButton(MouseEventArgs e)
        {
            return MouseButtons.Left == e.Button && GetComboButtonRectangle().Contains(e.X, e.Y);
        }

        /// <summary>
        /// Overrides OnMouseDown.
        /// </summary>
        /// <remarks>
        /// Checks if the MouseDown event occured on the combo button.  If
        /// so the control is redrawn in the pushed state.</remarks>
        /// <param name="e"></param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (MouseOnComboButton(e) && Enabled)
            {
                buttonState = ButtonState.Pushed;

                Invalidate(GetComboButtonRectangle(), false);
                Update();
            }
        }

        // The creation of the drop down panel is deferred until the user
        // click the button.  So we store properties locally and then apply them
        // to the panel after it's created.
        private ColorSortOrder panel_colorSortOrder = ColorPanel.defaultColorSortOrder;
        private Size panel_colorWellSize = ColorPanel.defaultColorWellSize;
        private ColorSet panel_colorSet = ColorPanel.defaultColorSet;
        private Color panel_color = ColorPanel.defaultColor;
        private BorderStyle panel_PanelBorderStyle = ColorPanel.defaultBorderStyle;
        private int panel_columns = ColorPanel.defaultPreferredColumns;
        private Color[] panel_customColors = ColorPanel.DefaultCustomColors();

        /// <summary>
        /// Display the ColorPicker's drop down palette.
        /// </summary>
        public void ShowDropdown()
        {
            Point p = new Point(Left, Bottom);
            Point q = Parent.PointToScreen(p);

            using (ColorPanelForm popup = new ColorPanelForm())
            {
                popup.Top = q.Y;
                popup.Left = q.X;
                popup.ColorSet = panel_colorSet;
                popup.ColorSortOrder = panel_colorSortOrder;
                popup.ColorWellSize = panel_colorWellSize;
                popup.PanelBorderStyle = panel_PanelBorderStyle;
                popup.Columns = panel_columns;
                popup.CustomColors = panel_customColors;
                popup.Color = panel_color;

                // set color after colorSet since changing the colorSet
                // resets the color to black

                if (panel_columns <= 0)
                {
                    Trace.WriteLine(string.Format("Setting '{0}' width = {1}", popup.Name, Width));
                    popup.ParentWidth = Width;
                }

                if (DialogResult.OK == popup.ShowDialog(this))
                {
                    panel_color = popup.Color;
                    OnColorChanged(new ColorChangedEventArgs(panel_color));
                }
            }
        }

        /// <summary>
        /// Overrides OnMouseUp.
        /// </summary>
        /// <remarks>
        /// When the user releases the mouse over the combo button after having pressed it, the 
        /// contained <see cref="ColorPanel">ColorPanel</see> is displayed.
        /// </remarks>
        /// <param name="e"></param>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (buttonState == ButtonState.Pushed)
            {
                buttonState = ButtonState.Normal;

                Invalidate(GetComboButtonRectangle(), false);
                Update();

                if (MouseOnComboButton(e))
                {
                    ShowDropdown();
                }
            }
        }

        private void SetControlSize()
        {
            if (autoSize)
            {
                int h = FontHeight + 2 * padding.Height + 2 * borderSize.Height;
                int w = Width;

                if (w < SystemInformation.VerticalScrollBarWidth + borderSize.Width * 2)
                {
                    w = SystemInformation.VerticalScrollBarWidth + borderSize.Width * 2;
                }

                ClientSize = new Size(w, h);
            }

            comboButtonRectangle = new Rectangle();

            Refresh();
        }

        /// <summary>
        /// Override OnResize.
        /// </summary>
        /// <remarks>
        /// OnResize is overridden in order to autosize the control.<br></br><br></br>
        /// 
        /// The control's width may be set freely but if the AutoSize property is set to <b>true</b>
        /// the control's height is fixed to Font.Height + Border.Height + 4.
        /// </remarks>
        /// <param name="e"></param>
        protected override void OnResize(System.EventArgs e)
        {
            base.OnResize(e);

            SetControlSize();
        }

        /// <summary>
        /// Sets/gets the control's BorderStyle.
        /// </summary>
        [Browsable(true), Category("Appearance")]
        [Description("Indicates the border style of the picker control.")]
        [DefaultValue(defaultBorderStyle)]
        public new BorderStyle BorderStyle
        {
            get
            {
                return borderStyle;
            }
            set
            {
                Size bs = new Size();

                switch (value)
                {
                    case BorderStyle.Fixed3D:
                        bs = SystemInformation.Border3DSize;
                        break;
                    case BorderStyle.FixedSingle:
                        bs = SystemInformation.BorderSize;
                        break;
                    case BorderStyle.None:
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(BorderStyle));
                }

                borderStyle = value;
                borderSize = bs;

                comboButtonRectangle = new Rectangle();

                SetControlSize();
            }
        }

        /// <summary>
        /// The ColorChangedEvent event handler.
        /// </summary>
        [Browsable(true), Category("ColorPicker")]
        public event ColorChangedEventHandler ColorChanged;

        /// <summary>
        /// Raises the ColorChanged event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnColorChanged(ColorChangedEventArgs e)
        {
            ColorChanged?.Invoke(this, e);

            Refresh();
        }

        /// <summary>
        /// Set/get the drop down panel's border style.
        /// </summary>
        [Browsable(true), Category("ColorPicker"), DefaultValue(ColorPanel.defaultBorderStyle)]
        [Description("Set/get the drop-down panel's border style.")]
        public BorderStyle PanelBorderStyle
        {
            get
            {
                return panel_PanelBorderStyle;
            }
            set
            {
                panel_PanelBorderStyle = value;
            }
        }

        /// <summary>
        /// Set/get the pick color.
        /// </summary>
        [Browsable(true), Category("ColorPicker"), Description("Get/set the pick color.")]
        public Color Color
        {
            get
            {
                return panel_color;
            }
            set
            {
                panel_color = value;
                Refresh();
            }
        }

        /// <summary>
        /// Design time support to reset the Color property to its default value.
        /// </summary>
        public void ResetColor()
        {
            Color = ColorPanel.defaultColor;
        }

        /// <summary>
        /// Design time support to indicate whether the Color property should be serialized.
        /// </summary>
        /// <returns></returns>
        public bool ShouldSerializeColor()
        {
            return Color != ColorPanel.defaultColor;
        }

        /// <summary>
        /// Set/get the set of colors displayed by the contained 
        /// <see cref="ColorPanel">ColorPanel</see> control.<br></br><br></br>
        /// See <see cref="PJLControls.ColorSet">ColorSet</see>.
        /// </summary>
        [Browsable(true), Category("ColorPicker"), DefaultValue(ColorPanel.defaultColorSet)]
        [Description("Get/set the palette of colors to be displayed by the drop-down panel.")]
        public ColorSet ColorSet
        {
            get
            {
                return panel_colorSet;
            }
            set
            {
                panel_colorSet = value;
            }
        }

        /// <summary>
        /// Set/get the size of the color wells in the drop down palette.
        /// </summary>
        [Browsable(true), Category("ColorPicker")]
        [Description("Set/get the size of the color wells displayed in the drop-down color panel.")]
        public Size ColorWellSize
        {
            get
            {
                return panel_colorWellSize;
            }
            set
            {
                panel_colorWellSize = value;
            }
        }

        /// <summary>
        /// Design time support to reset the ColorWellSize property to its default value.
        /// </summary>
        public void ResetColorWellSize()
        {
            ColorWellSize = ColorPanel.defaultColorWellSize;
        }

        /// <summary>
        /// Design time support to indicate whether the ColorWellSize property should be serialized.
        /// </summary>
        /// <returns></returns>
        public bool ShouldSerializeColorWellSize()
        {
            return ColorWellSize != ColorPanel.defaultColorWellSize;
        }

        /// <summary>
        /// Set/get the order in which colors in the palette or the contained 
        /// <see cref="ColorPanel">ColorPanel</see> should be sorted.<br></br><br></br>
        /// See <see cref="PJLControls.ColorSortOrder">ColorSortOrder</see>.
        /// </summary>
        [Browsable(true), Category("ColorPicker"), DefaultValue(ColorPanel.defaultColorSortOrder)]
        [Description("Get/set the order that the colors in the color palette are displayed.")]
        public ColorSortOrder ColorSortOrder
        {
            get
            {
                return panel_colorSortOrder;
            }
            set
            {
                panel_colorSortOrder = value;
            }
        }

        /// <summary>
        /// Show/hide the selected color in the combo-box part of the control.
        /// </summary>
        [Browsable(true), Category("ColorPicker"), DefaultValue(defaultDisplayColor)]
        [Description("Show/hide the selected color in the combo-box part of the control.")]
        public bool DisplayColor
        {
            get
            {
                return bDisplayColor;
            }
            set
            {
                if (value != bDisplayColor)
                {
                    bDisplayColor = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Show/hide the name of the selected color in the text part of the combo-box.<br></br>
        /// Otherwise show the value of the Text property.
        /// </summary>
        [Browsable(true), Category("ColorPicker"), DefaultValue(defaultDisplayColorName)]
        [Description("Show/hide the name of the selected color in the text part of the combo-box.")]
        public bool DisplayColorName
        {
            get
            {
                return bDisplayColorName;
            }
            set
            {
                if (value != bDisplayColorName)
                {
                    bDisplayColorName = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Overrides OnEnabledChanged so the control can redraw itself 
        /// enabled/disabled.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnEnabledChanged(System.EventArgs e)
        {
            base.OnEnabledChanged(e);

            buttonState = Enabled ? ButtonState.Normal : ButtonState.Inactive;

            Refresh();
        }

        /// <summary>
        /// Overrides OnFontChanged so the control can resize/redraw itself appropriately.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFontChanged(System.EventArgs e)
        {
            SetControlSize();
            Refresh();
        }

        /// <summary>
        /// Set/get the autosize property.
        /// </summary>
        /// <remarks>
        /// Setting AutoSize to <B>true</B>, fixes the controls height to 
        /// Font.Height + Border.Height + 4.
        /// </remarks>
        [Browsable(true), Category("ColorPicker"), DefaultValue(defaultAutoSize)]
        [Description("If true, the height of the control is fixed and depends on the font.")]
        public new bool AutoSize
        {
            get
            {
                return autoSize;
            }
            set
            {
                autoSize = value;
                // no need to refresh
            }
        }

        /// <summary>
        /// Design time support to reset the _Text property to it's default value.
        /// </summary>
        public void Reset_Text()
        {
            base.Text = string.Empty;
        }

        /// <summary>
        /// Design time support to indicate whether the _Text property should be serialized?
        /// </summary>
        /// <returns></returns>
        public bool ShouldSerialize_Text()
        {
            return !string.IsNullOrEmpty(base.Text);
        }

        /// <summary>
        /// Set/get the number of preferred columns in the drop-down color panel.<br></br><br></br>
        /// If you set this value less than or equal to 0 the panel is set to the same width as the combo-box.<br></br>
        /// </summary>
        [Browsable(true), Category("ColorPicker"), DefaultValue(ColorPanel.defaultPreferredColumns)]
        [Description("Set/get the number of preferred columns in the drop-down panel.  If set to 0 then the panel will have the same width as the picker.")]
        public int Columns
        {
            get
            {
                return panel_columns;
            }
            set
            {
                panel_columns = value <= 0 ? 0 : value;
            }
        }

        /// <summary>
        /// Set/get the custom color palette to be displayed.
        /// </summary>
        [Browsable(true), Category("ColorPicker")]
        [Description("Set/get the custom color palette.")]
        public Color[] CustomColors
        {
            get
            {
                return panel_customColors;
            }
            set
            {
                if (value == null || value.Length < 1)
                {
                    panel_customColors = new[] { Color.White };
                }
                else
                {
                    panel_customColors = value;
                }
            }
        }

        /// <summary>
        /// Design time support to reset the CustomColors property to its default value.
        /// </summary>
        public void ResetCustomColors()
        {
            CustomColors = ColorPanel.DefaultCustomColors();
        }

        /// <summary>
        /// Design time support to indicate whether the CustomColors property should be serialized.
        /// </summary>
        /// <returns></returns>
        public bool ShouldSerializeCustomColors()
        {
            return ColorPanel.ShouldSerializeCustomColors(CustomColors);
        }
    }
}

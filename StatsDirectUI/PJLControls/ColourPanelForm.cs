// ColourPanelForm.cs : implementation file
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

using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.PJLControls
{
	/// <summary>
	/// ColorPanelForm is used to host an instance of ColorPanel.
	/// <br></br>
	/// This enable the ColorPanel control to be dropped down by the
	/// ColorPicker.
	/// <br></br><br></br>
	/// </summary>
	internal class ColorPanelForm : Form
	{
		private ColorPanelWithCapture colorPanel;

		public ColorPanelForm()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.colorPanel = new ColorPanelWithCapture();
			this.SuspendLayout();
			// 
			// colorPanel
			// 
			this.colorPanel.BackColor = System.Drawing.SystemColors.Control;
			this.colorPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.colorPanel.Color = System.Drawing.Color.Red;
			this.colorPanel.ColorSet = ColorSet.Web;
			this.colorPanel.ColorSortOrder = ColorSortOrder.Brightness;
			this.colorPanel.ColorWellSize = new System.Drawing.Size(16, 16);
			this.colorPanel.Columns = 0;
			this.colorPanel.CustomColors = new System.Drawing.Color[] {
																		  System.Drawing.Color.White};
			this.colorPanel.Name = "colorPanel";
			this.colorPanel.Size = new System.Drawing.Size(292, 132);
			this.colorPanel.TabIndex = 0;
			this.colorPanel.ColorChanged += new ColorChangedEventHandler(this.colorPanel_ColorChanged);
			this.colorPanel.PanelClosing += new ColorPanelClosingEventHandler(this.colorPanel_PanelClosing);
			this.colorPanel.Resize += new System.EventHandler(this.colorPanel_Resize);
			// 
			// ColorPanelForm
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(360, 176);
			this.ControlBox = false;
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.colorPanel});
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			this.Name = "ColorPanelForm";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			this.Text = "ColorPanelForm";
			this.ResumeLayout(false);

		}
		#endregion

		private void colorPanel_ColorChanged(object sender, ColorChangedEventArgs e)
		{
			DialogResult = DialogResult.OK;
		}

		private void colorPanel_PanelClosing(object sender, System.EventArgs e)
		{
			if( DialogResult != DialogResult.OK )
			{
				DialogResult = DialogResult.Cancel;
			}
		}

		private void colorPanel_Resize(object sender, System.EventArgs e)
		{
			// make the form the same size as the panel
			colorPanel.Top  = 0;
			colorPanel.Left = 0;

			Width = colorPanel.Width;
			Height = colorPanel.Height;
		}

		protected override void OnDeactivate(System.EventArgs e)
		{
			base.OnDeactivate(e);

			if( DialogResult != DialogResult.OK )
			{
				DialogResult = DialogResult.Cancel;
			}
		}

		// Forward properties to contained panel
		public BorderStyle PanelBorderStyle
		{
			get
			{
				return colorPanel.BorderStyle;
			}
			set
			{
				colorPanel.BorderStyle = value;
			}
		}

		public Color Color
		{
			get
			{
				return colorPanel.Color;
			}
			set
			{
				colorPanel.Color = value;
			}
		}

		public ColorSet ColorSet
		{
			get
			{
				return colorPanel.ColorSet;
			}
			set
			{
				colorPanel.ColorSet = value;
			}
		}

		public Size ColorWellSize
		{
			get
			{
				return colorPanel.ColorWellSize;
			}
			set
			{
				colorPanel.ColorWellSize = value;
			}
		}

		public ColorSortOrder ColorSortOrder
		{
			get
			{
				return colorPanel.ColorSortOrder;
			}
			set
			{
				colorPanel.ColorSortOrder = value;
			}
		}

		public int Columns
		{
			get
			{
				return colorPanel.Columns;
			}
			set
			{
				colorPanel.Columns = value;
			}
		}

		public Color[] CustomColors
		{
			get
			{
				return colorPanel.CustomColors;
			}
			set
			{
				colorPanel.CustomColors = value;
			}
		}

		/// <summary>
		/// This is so ColorPicker can inform the ColorPanelForm/ColorPanel of the desired
		/// width of the control.
		/// </summary>
		internal int ParentWidth
		{
			set
			{
				colorPanel.ParentWidth = value;
			}
		}
	}
}

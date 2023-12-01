namespace StatsDirect.UI
{
    partial class ctlMarkerShape
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components is not null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ctlMarkerShape));
            this.ilMarkerStyles = new System.Windows.Forms.ImageList(this.components);
            this.cboMarkerShape = new StatsDirect.UI.ComboBoxEx();
            this.SuspendLayout();
            // 
            // ilMarkerStyles
            // 
            this.ilMarkerStyles.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ilMarkerStyles.ImageStream")));
            this.ilMarkerStyles.TransparentColor = System.Drawing.Color.Transparent;
            this.ilMarkerStyles.Images.SetKeyName(0, "Marker1.gif");
            this.ilMarkerStyles.Images.SetKeyName(1, "Marker2.gif");
            this.ilMarkerStyles.Images.SetKeyName(2, "Marker3.gif");
            this.ilMarkerStyles.Images.SetKeyName(3, "Marker4.gif");
            this.ilMarkerStyles.Images.SetKeyName(4, "Marker5.gif");
            this.ilMarkerStyles.Images.SetKeyName(5, "Marker6.gif");
            this.ilMarkerStyles.Images.SetKeyName(6, "Marker7.gif");
            this.ilMarkerStyles.Images.SetKeyName(7, "Marker8.gif");
            this.ilMarkerStyles.Images.SetKeyName(8, "Marker9.gif");
            // 
            // cboMarkerShape
            // 
            this.cboMarkerShape.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cboMarkerShape.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMarkerShape.FormattingEnabled = true;
            this.cboMarkerShape.ImageList = this.ilMarkerStyles;
            this.cboMarkerShape.ItemHeight = 19;
            this.cboMarkerShape.Location = new System.Drawing.Point(0, 0);
            this.cboMarkerShape.Margin = new System.Windows.Forms.Padding(0);
            this.cboMarkerShape.MaxDropDownItems = 9;
            this.cboMarkerShape.Name = "cboMarkerShape";
            this.cboMarkerShape.Size = new System.Drawing.Size(40, 25);
            this.cboMarkerShape.TabIndex = 1;
            this.cboMarkerShape.SelectedIndexChanged += new System.EventHandler(this.cboMarkerShape_SelectedIndexChanged);
            // 
            // ctlMarkerShape
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.cboMarkerShape);
            this.MinimumSize = new System.Drawing.Size(40, 25);
            this.Name = "ctlMarkerShape";
            this.Size = new System.Drawing.Size(40, 25);
            this.ResumeLayout(false);

        }

        #endregion

        private ComboBoxEx cboMarkerShape;
        private System.Windows.Forms.ImageList ilMarkerStyles;
    }
}
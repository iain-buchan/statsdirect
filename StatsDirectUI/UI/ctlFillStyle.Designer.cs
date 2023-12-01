namespace StatsDirect.UI
{
    partial class ctlFillStyle
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ctlFillStyle));
            this.cboFillStyle = new StatsDirect.UI.ComboBoxEx();
            this.ilFillStyle = new System.Windows.Forms.ImageList(this.components);
            this.SuspendLayout();
            // 
            // cboFillStyle
            // 
            this.cboFillStyle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboFillStyle.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cboFillStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFillStyle.FormattingEnabled = true;
            this.cboFillStyle.ImageList = this.ilFillStyle;
            this.cboFillStyle.ItemHeight = 19;
            this.cboFillStyle.Location = new System.Drawing.Point(0, 0);
            this.cboFillStyle.Margin = new System.Windows.Forms.Padding(0);
            this.cboFillStyle.Name = "cboFillStyle";
            this.cboFillStyle.Size = new System.Drawing.Size(40, 25);
            this.cboFillStyle.TabIndex = 0;
            this.cboFillStyle.SelectedIndexChanged += new System.EventHandler(this.cboFillStyle_SelectedIndexChanged);
            // 
            // ilFillStyle
            // 
            this.ilFillStyle.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ilFillStyle.ImageStream")));
            this.ilFillStyle.TransparentColor = System.Drawing.Color.Transparent;
            this.ilFillStyle.Images.SetKeyName(0, "Fill0.gif");
            this.ilFillStyle.Images.SetKeyName(1, "Fill1.gif");
            this.ilFillStyle.Images.SetKeyName(2, "Fill2.gif");
            this.ilFillStyle.Images.SetKeyName(3, "Fill3.gif");
            this.ilFillStyle.Images.SetKeyName(4, "Fill4.gif");
            // 
            // ctlFillStyle
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Controls.Add(this.cboFillStyle);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MinimumSize = new System.Drawing.Size(40, 21);
            this.Name = "ctlFillStyle";
            this.Size = new System.Drawing.Size(40, 21);
            this.ResumeLayout(false);

        }

        #endregion

        private ComboBoxEx cboFillStyle;
        private System.Windows.Forms.ImageList ilFillStyle;
    }
}
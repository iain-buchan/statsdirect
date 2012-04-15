namespace StatsDirect.UI
{
    partial class ctlDashStyle
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
            if (disposing && (components != null))
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ctlDashStyle));
            this.cboDashStyle = new StatsDirect.UI.ComboBoxEx();
            this.ilDashStyles = new System.Windows.Forms.ImageList(this.components);
            this.SuspendLayout();
            // 
            // cboDashStyle
            // 
            this.cboDashStyle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.cboDashStyle.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cboDashStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDashStyle.FormattingEnabled = true;
            this.cboDashStyle.ImageList = this.ilDashStyles;
            this.cboDashStyle.Location = new System.Drawing.Point(0, 0);
            this.cboDashStyle.Margin = new System.Windows.Forms.Padding(0);
            this.cboDashStyle.Name = "cboDashStyle";
            this.cboDashStyle.Size = new System.Drawing.Size(100, 21);
            this.cboDashStyle.TabIndex = 0;
            this.cboDashStyle.SelectedIndexChanged += new System.EventHandler(this.cboDashStyle_SelectedIndexChanged);
            // 
            // ilDashStyles
            // 
            this.ilDashStyles.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ilDashStyles.ImageStream")));
            this.ilDashStyles.TransparentColor = System.Drawing.Color.Transparent;
            this.ilDashStyles.Images.SetKeyName(0, "Style1.gif");
            this.ilDashStyles.Images.SetKeyName(1, "Style2.gif");
            this.ilDashStyles.Images.SetKeyName(2, "Style3.gif");
            this.ilDashStyles.Images.SetKeyName(3, "Style4.gif");
            this.ilDashStyles.Images.SetKeyName(4, "Style5.gif");
            // 
            // ctlDashStyle
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.cboDashStyle);
            this.MaximumSize = new System.Drawing.Size(10000, 21);
            this.MinimumSize = new System.Drawing.Size(100, 21);
            this.Name = "ctlDashStyle";
            this.Size = new System.Drawing.Size(100, 21);
            this.ResumeLayout(false);

        }

        #endregion

        private ComboBoxEx cboDashStyle;
        private System.Windows.Forms.ImageList ilDashStyles;

    }
}
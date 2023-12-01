namespace StatsDirect.UI
{
    partial class ctlLineThickness
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ctlLineThickness));
            this.ilThicknesses = new System.Windows.Forms.ImageList(this.components);
            this.cboLineThickness = new StatsDirect.UI.ComboBoxEx();
            this.SuspendLayout();
            // 
            // ilThicknesses
            // 
            this.ilThicknesses.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("ilThicknesses.ImageStream")));
            this.ilThicknesses.TransparentColor = System.Drawing.Color.Transparent;
            this.ilThicknesses.Images.SetKeyName(0, "Width1.gif");
            this.ilThicknesses.Images.SetKeyName(1, "Width2.gif");
            this.ilThicknesses.Images.SetKeyName(2, "Width3.gif");
            this.ilThicknesses.Images.SetKeyName(3, "Width4.gif");
            // 
            // cboLineThickness
            // 
            this.cboLineThickness.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboLineThickness.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cboLineThickness.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboLineThickness.FormattingEnabled = true;
            this.cboLineThickness.ImageList = this.ilThicknesses;
            this.cboLineThickness.Location = new System.Drawing.Point(0, 0);
            this.cboLineThickness.Margin = new System.Windows.Forms.Padding(0);
            this.cboLineThickness.Name = "cboLineThickness";
            this.cboLineThickness.Size = new System.Drawing.Size(193, 21);
            this.cboLineThickness.TabIndex = 11;
            this.cboLineThickness.SelectedIndexChanged += new System.EventHandler(this.cboLineThickness_SelectedIndexChanged);
            // 
            // ctlLineThickness
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Controls.Add(this.cboLineThickness);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MaximumSize = new System.Drawing.Size(10000, 21);
            this.MinimumSize = new System.Drawing.Size(100, 21);
            this.Name = "ctlLineThickness";
            this.Size = new System.Drawing.Size(193, 21);
            this.ResumeLayout(false);

        }

        #endregion

        private ComboBoxEx cboLineThickness;
        private System.Windows.Forms.ImageList ilThicknesses;
    }
}
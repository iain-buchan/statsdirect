namespace StatsDirect.UI
{
    partial class frmCoxRegressionOptions
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmCoxRegressionOptions));
            this.cmdCancel = new System.Windows.Forms.Button();
            this.cmdHelp = new System.Windows.Forms.Button();
            this.cmdOK = new System.Windows.Forms.Button();
            this.txtAccuracy = new System.Windows.Forms.TextBox();
            this.lblAccuracy = new System.Windows.Forms.Label();
            this.chkCentre = new System.Windows.Forms.CheckBox();
            this.cboStrata = new System.Windows.Forms.ComboBox();
            this.lblStrata = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(229, 12);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(75, 23);
            this.cmdCancel.TabIndex = 8;
            this.cmdCancel.Text = "&Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // cmdHelp
            // 
            this.cmdHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdHelp.Location = new System.Drawing.Point(148, 12);
            this.cmdHelp.Name = "cmdHelp";
            this.cmdHelp.Size = new System.Drawing.Size(75, 23);
            this.cmdHelp.TabIndex = 7;
            this.cmdHelp.Text = "&Help";
            this.cmdHelp.UseVisualStyleBackColor = true;
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.Location = new System.Drawing.Point(67, 12);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(75, 23);
            this.cmdOK.TabIndex = 6;
            this.cmdOK.Text = "&OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // txtAccuracy
            // 
            this.txtAccuracy.Location = new System.Drawing.Point(12, 57);
            this.txtAccuracy.Name = "txtAccuracy";
            this.txtAccuracy.Size = new System.Drawing.Size(125, 20);
            this.txtAccuracy.TabIndex = 9;
            this.txtAccuracy.Text = "0.0000001";
            // 
            // lblAccuracy
            // 
            this.lblAccuracy.AutoSize = true;
            this.lblAccuracy.Location = new System.Drawing.Point(143, 60);
            this.lblAccuracy.Name = "lblAccuracy";
            this.lblAccuracy.Size = new System.Drawing.Size(52, 13);
            this.lblAccuracy.TabIndex = 10;
            this.lblAccuracy.Text = "Accuracy";
            // 
            // chkCentre
            // 
            this.chkCentre.AutoSize = true;
            this.chkCentre.Checked = true;
            this.chkCentre.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCentre.Location = new System.Drawing.Point(12, 211);
            this.chkCentre.Name = "chkCentre";
            this.chkCentre.Size = new System.Drawing.Size(164, 17);
            this.chkCentre.TabIndex = 11;
            this.chkCentre.Text = "Centre continuous covariates";
            this.chkCentre.UseVisualStyleBackColor = true;
            // 
            // cboStrata
            // 
            this.cboStrata.FormattingEnabled = true;
            this.cboStrata.Items.AddRange(new object[] {
            "1000",
            "no split"});
            this.cboStrata.Location = new System.Drawing.Point(11, 155);
            this.cboStrata.Name = "cboStrata";
            this.cboStrata.Size = new System.Drawing.Size(121, 21);
            this.cboStrata.TabIndex = 12;
            this.cboStrata.Text = "1000";
            // 
            // lblStrata
            // 
            this.lblStrata.AutoSize = true;
            this.lblStrata.Location = new System.Drawing.Point(9, 100);
            this.lblStrata.Name = "lblStrata";
            this.lblStrata.Size = new System.Drawing.Size(289, 52);
            this.lblStrata.TabIndex = 13;
            this.lblStrata.Text = resources.GetString("lblStrata.Text");
            // 
            // frmCoxRegressionOptions
            // 
            this.AcceptButton = this.cmdOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdCancel;
            this.ClientSize = new System.Drawing.Size(316, 249);
            this.ControlBox = false;
            this.Controls.Add(this.lblStrata);
            this.Controls.Add(this.cboStrata);
            this.Controls.Add(this.chkCentre);
            this.Controls.Add(this.lblAccuracy);
            this.Controls.Add(this.txtAccuracy);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.cmdHelp);
            this.Controls.Add(this.cmdOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "frmCoxRegressionOptions";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Cox Regression Options";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.Button cmdHelp;
        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.TextBox txtAccuracy;
        private System.Windows.Forms.Label lblAccuracy;
        private System.Windows.Forms.CheckBox chkCentre;
        private System.Windows.Forms.ComboBox cboStrata;
        private System.Windows.Forms.Label lblStrata;
    }
}
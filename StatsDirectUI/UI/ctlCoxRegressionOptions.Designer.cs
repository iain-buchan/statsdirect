namespace StatsDirect.UI
{
    partial class ctlCoxRegressionOptions
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ctlCoxRegressionOptions));
            this.txtAccuracy = new System.Windows.Forms.TextBox();
            this.lblAccuracy = new System.Windows.Forms.Label();
            this.chkCentre = new System.Windows.Forms.CheckBox();
            this.cboStrata = new System.Windows.Forms.ComboBox();
            this.lblStrata = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // txtAccuracy
            // 
            this.txtAccuracy.Location = new System.Drawing.Point(3, 3);
            this.txtAccuracy.Name = "txtAccuracy";
            this.txtAccuracy.Size = new System.Drawing.Size(125, 20);
            this.txtAccuracy.TabIndex = 9;
            this.txtAccuracy.Text = "0.0000001";
            // 
            // lblAccuracy
            // 
            this.lblAccuracy.AutoSize = true;
            this.lblAccuracy.Location = new System.Drawing.Point(134, 6);
            this.lblAccuracy.Name = "lblAccuracy";
            this.lblAccuracy.Size = new System.Drawing.Size(109, 13);
            this.lblAccuracy.TabIndex = 10;
            this.lblAccuracy.Text = "Precision of estimates";
            // 
            // chkCentre
            // 
            this.chkCentre.AutoSize = true;
            this.chkCentre.Checked = true;
            this.chkCentre.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCentre.Location = new System.Drawing.Point(3, 157);
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
            this.cboStrata.Location = new System.Drawing.Point(2, 101);
            this.cboStrata.Name = "cboStrata";
            this.cboStrata.Size = new System.Drawing.Size(121, 21);
            this.cboStrata.TabIndex = 12;
            this.cboStrata.Text = "1000";
            // 
            // lblStrata
            // 
            this.lblStrata.AutoSize = true;
            this.lblStrata.Location = new System.Drawing.Point(0, 46);
            this.lblStrata.Name = "lblStrata";
            this.lblStrata.Size = new System.Drawing.Size(289, 52);
            this.lblStrata.TabIndex = 13;
            this.lblStrata.Text = resources.GetString("lblStrata.Text");
            // 
            // ctlCoxRegressionOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lblStrata);
            this.Controls.Add(this.cboStrata);
            this.Controls.Add(this.chkCentre);
            this.Controls.Add(this.lblAccuracy);
            this.Controls.Add(this.txtAccuracy);
            this.Name = "ctlCoxRegressionOptions";
            this.Size = new System.Drawing.Size(295, 176);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtAccuracy;
        private System.Windows.Forms.Label lblAccuracy;
        private System.Windows.Forms.CheckBox chkCentre;
        private System.Windows.Forms.ComboBox cboStrata;
        private System.Windows.Forms.Label lblStrata;
    }
}
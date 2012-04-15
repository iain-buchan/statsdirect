namespace StatsDirect.UI
{
    partial class frmEffectOptions
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
            this.cmdCancel = new System.Windows.Forms.Button();
            this.cmdHelp = new System.Windows.Forms.Button();
            this.cmdOK = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.rdoTypeD = new System.Windows.Forms.RadioButton();
            this.rdoTypeG = new System.Windows.Forms.RadioButton();
            this.rdoTypeM = new System.Windows.Forms.RadioButton();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(199, 12);
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
            this.cmdHelp.Location = new System.Drawing.Point(118, 12);
            this.cmdHelp.Name = "cmdHelp";
            this.cmdHelp.Size = new System.Drawing.Size(75, 23);
            this.cmdHelp.TabIndex = 7;
            this.cmdHelp.Text = "&Help";
            this.cmdHelp.UseVisualStyleBackColor = true;
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.Location = new System.Drawing.Point(37, 12);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(75, 23);
            this.cmdOK.TabIndex = 6;
            this.cmdOK.Text = "&OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.rdoTypeM);
            this.groupBox1.Controls.Add(this.rdoTypeG);
            this.groupBox1.Controls.Add(this.rdoTypeD);
            this.groupBox1.Location = new System.Drawing.Point(12, 41);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(263, 96);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Type of analysis";
            // 
            // rdoTypeD
            // 
            this.rdoTypeD.AutoSize = true;
            this.rdoTypeD.Checked = true;
            this.rdoTypeD.Location = new System.Drawing.Point(6, 19);
            this.rdoTypeD.Name = "rdoTypeD";
            this.rdoTypeD.Size = new System.Drawing.Size(252, 17);
            this.rdoTypeD.TabIndex = 0;
            this.rdoTypeD.TabStop = true;
            this.rdoTypeD.Text = "Standardized effect size (d) from mean, n and sd";
            this.rdoTypeD.UseVisualStyleBackColor = true;
            // 
            // rdoTypeG
            // 
            this.rdoTypeG.AutoSize = true;
            this.rdoTypeG.Location = new System.Drawing.Point(6, 44);
            this.rdoTypeG.Name = "rdoTypeG";
            this.rdoTypeG.Size = new System.Drawing.Size(215, 17);
            this.rdoTypeG.TabIndex = 1;
            this.rdoTypeG.Text = "Standardized effect size (d) from g and n";
            this.rdoTypeG.UseVisualStyleBackColor = true;
            // 
            // rdoTypeM
            // 
            this.rdoTypeM.AutoSize = true;
            this.rdoTypeM.Location = new System.Drawing.Point(6, 67);
            this.rdoTypeM.Name = "rdoTypeM";
            this.rdoTypeM.Size = new System.Drawing.Size(249, 17);
            this.rdoTypeM.TabIndex = 2;
            this.rdoTypeM.Text = "Weighted mean difference from mean, n and sd";
            this.rdoTypeM.UseVisualStyleBackColor = true;
            // 
            // frmCoxRegressionOptions
            // 
            this.AcceptButton = this.cmdOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdCancel;
            this.ClientSize = new System.Drawing.Size(286, 150);
            this.ControlBox = false;
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.cmdHelp);
            this.Controls.Add(this.cmdOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "frmCoxRegressionOptions";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Which type of effect size meta-analysis?";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.Button cmdHelp;
        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton rdoTypeM;
        private System.Windows.Forms.RadioButton rdoTypeG;
        private System.Windows.Forms.RadioButton rdoTypeD;
    }
}
namespace StatsDirect.UI
{
    partial class frmAbout
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmAbout));
            this.txtLicense = new System.Windows.Forms.TextBox();
            this.lblCopyright = new System.Windows.Forms.Label();
            this.cmdOK = new System.Windows.Forms.Button();
            this.lblSysInfo = new System.Windows.Forms.Label();
            this.lblBanner = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.lblVisit = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // txtLicense
            // 
            this.txtLicense.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLicense.BackColor = System.Drawing.Color.White;
            this.txtLicense.Location = new System.Drawing.Point(15, 186);
            this.txtLicense.Multiline = true;
            this.txtLicense.Name = "txtLicense";
            this.txtLicense.ReadOnly = true;
            this.txtLicense.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLicense.Size = new System.Drawing.Size(398, 201);
            this.txtLicense.TabIndex = 0;
            this.txtLicense.TabStop = false;
            this.txtLicense.Text = resources.GetString("txtLicense.Text");
            // 
            // lblCopyright
            // 
            this.lblCopyright.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblCopyright.AutoSize = true;
            this.lblCopyright.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCopyright.Location = new System.Drawing.Point(12, 398);
            this.lblCopyright.Name = "lblCopyright";
            this.lblCopyright.Size = new System.Drawing.Size(282, 13);
            this.lblCopyright.TabIndex = 1;
            this.lblCopyright.Text = "Copyright ©1990-2024 StatsDirect Ltd.  All rights reserved.";
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdOK.Location = new System.Drawing.Point(336, 393);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(75, 23);
            this.cmdOK.TabIndex = 2;
            this.cmdOK.Text = "&OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // lblSysInfo
            // 
            this.lblSysInfo.AutoSize = true;
            this.lblSysInfo.Location = new System.Drawing.Point(12, 156);
            this.lblSysInfo.Name = "lblSysInfo";
            this.lblSysInfo.Size = new System.Drawing.Size(65, 13);
            this.lblSysInfo.TabIndex = 6;
            this.lblSysInfo.Text = "(system info)";
            // 
            // lblBanner
            // 
            this.lblBanner.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblBanner.Location = new System.Drawing.Point(193, 13);
            this.lblBanner.Margin = new System.Windows.Forms.Padding(0);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Size = new System.Drawing.Size(218, 58);
            this.lblBanner.TabIndex = 7;
            this.lblBanner.Text = "StatsDirect Statistical Software";
            // 
            // lblVersion
            // 
            this.lblVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVersion.Location = new System.Drawing.Point(198, 71);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(213, 41);
            this.lblVersion.TabIndex = 8;
            this.lblVersion.Text = "Version: ";
            // 
            // lblVisit
            // 
            this.lblVisit.AutoSize = true;
            this.lblVisit.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblVisit.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVisit.ForeColor = System.Drawing.Color.Purple;
            this.lblVisit.Location = new System.Drawing.Point(28, 125);
            this.lblVisit.Name = "lblVisit";
            this.lblVisit.Size = new System.Drawing.Size(353, 13);
            this.lblVisit.TabIndex = 9;
            this.lblVisit.Text = "Click here to visit www.statsdirect.com for news and updates";
            this.lblVisit.Click += new System.EventHandler(this.lblVisit_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.ErrorImage = global::StatsDirect.UI.Properties.Resources.sdbutton2;
            this.pictureBox1.Image = global::StatsDirect.UI.Properties.Resources.sdbutton2;
            this.pictureBox1.InitialImage = global::StatsDirect.UI.Properties.Resources.sdbutton2;
            this.pictureBox1.Location = new System.Drawing.Point(13, 13);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(177, 90);
            this.pictureBox1.TabIndex = 10;
            this.pictureBox1.TabStop = false;
            // 
            // frmAbout
            // 
            this.AcceptButton = this.cmdOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this.cmdOK;
            this.ClientSize = new System.Drawing.Size(423, 426);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.lblVisit);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.lblBanner);
            this.Controls.Add(this.lblSysInfo);
            this.Controls.Add(this.cmdOK);
            this.Controls.Add(this.lblCopyright);
            this.Controls.Add(this.txtLicense);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmAbout";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About StatsDirect";
            this.Shown += new System.EventHandler(this.frmAbout_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtLicense;
        private System.Windows.Forms.Label lblCopyright;
        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.Label lblSysInfo;
        private System.Windows.Forms.Label lblBanner;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblVisit;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}
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
            txtLicense = new System.Windows.Forms.TextBox();
            lblCopyright = new System.Windows.Forms.Label();
            cmdOK = new System.Windows.Forms.Button();
            lblSysInfo = new System.Windows.Forms.Label();
            lblBanner = new System.Windows.Forms.Label();
            lblVersion = new System.Windows.Forms.Label();
            lblVisit = new System.Windows.Forms.Label();
            pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // txtLicense
            // 
            txtLicense.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtLicense.BackColor = System.Drawing.Color.White;
            txtLicense.Location = new System.Drawing.Point(15, 186);
            txtLicense.Multiline = true;
            txtLicense.Name = "txtLicense";
            txtLicense.ReadOnly = true;
            txtLicense.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtLicense.Size = new System.Drawing.Size(398, 201);
            txtLicense.TabIndex = 0;
            txtLicense.TabStop = false;
            txtLicense.Text = resources.GetString("txtLicense.Text");
            // 
            // lblCopyright
            // 
            lblCopyright.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            lblCopyright.AutoSize = true;
            lblCopyright.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point);
            lblCopyright.Location = new System.Drawing.Point(12, 398);
            lblCopyright.Name = "lblCopyright";
            lblCopyright.Size = new System.Drawing.Size(282, 13);
            lblCopyright.TabIndex = 1;
            lblCopyright.Text = "Copyright ©1990-2026 Iain E. Buchan, University of Liverpool";
            // 
            // cmdOK
            // 
            cmdOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            cmdOK.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            cmdOK.Location = new System.Drawing.Point(336, 393);
            cmdOK.Name = "cmdOK";
            cmdOK.Size = new System.Drawing.Size(75, 23);
            cmdOK.TabIndex = 2;
            cmdOK.Text = "&OK";
            cmdOK.UseVisualStyleBackColor = true;
            cmdOK.Click += cmdOK_Click;
            // 
            // lblSysInfo
            // 
            lblSysInfo.AutoSize = true;
            lblSysInfo.Location = new System.Drawing.Point(12, 156);
            lblSysInfo.Name = "lblSysInfo";
            lblSysInfo.Size = new System.Drawing.Size(65, 13);
            lblSysInfo.TabIndex = 6;
            lblSysInfo.Text = "(system info)";
            // 
            // lblBanner
            // 
            lblBanner.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblBanner.Location = new System.Drawing.Point(193, 13);
            lblBanner.Margin = new System.Windows.Forms.Padding(0);
            lblBanner.Name = "lblBanner";
            lblBanner.Size = new System.Drawing.Size(218, 58);
            lblBanner.TabIndex = 7;
            lblBanner.Text = "StatsDirect Statistical Software";
            // 
            // lblVersion
            // 
            lblVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblVersion.Location = new System.Drawing.Point(198, 71);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new System.Drawing.Size(213, 41);
            lblVersion.TabIndex = 8;
            lblVersion.Text = "Version: ";
            // 
            // lblVisit
            // 
            lblVisit.AutoSize = true;
            lblVisit.Cursor = System.Windows.Forms.Cursors.Hand;
            lblVisit.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point);
            lblVisit.ForeColor = System.Drawing.Color.Purple;
            lblVisit.Location = new System.Drawing.Point(28, 125);
            lblVisit.Name = "lblVisit";
            lblVisit.Size = new System.Drawing.Size(353, 13);
            lblVisit.TabIndex = 9;
            lblVisit.Text = "Click here to visit www.statsdirect.com for news and updates";
            lblVisit.Click += lblVisit_Click;
            // 
            // pictureBox1
            // 
            pictureBox1.ErrorImage = Properties.Resources.sdbutton2;
            pictureBox1.Image = Properties.Resources.sdbutton2;
            pictureBox1.InitialImage = Properties.Resources.sdbutton2;
            pictureBox1.Location = new System.Drawing.Point(13, 13);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(177, 90);
            pictureBox1.TabIndex = 10;
            pictureBox1.TabStop = false;
            // 
            // frmAbout
            // 
            AcceptButton = cmdOK;
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.White;
            CancelButton = cmdOK;
            ClientSize = new System.Drawing.Size(423, 426);
            Controls.Add(pictureBox1);
            Controls.Add(lblVisit);
            Controls.Add(lblVersion);
            Controls.Add(lblBanner);
            Controls.Add(lblSysInfo);
            Controls.Add(cmdOK);
            Controls.Add(lblCopyright);
            Controls.Add(txtLicense);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "frmAbout";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "About StatsDirect";
            Shown += frmAbout_Shown;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
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
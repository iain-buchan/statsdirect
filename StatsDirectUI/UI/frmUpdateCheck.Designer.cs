namespace StatsDirect.UI
{
    partial class frmUpdateCheck
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmUpdateCheck));
            this.cmdUpdateStatsDirect = new System.Windows.Forms.Button();
            this.cmdClose = new System.Windows.Forms.Button();
            this.lblStatsDirectStatus = new System.Windows.Forms.Label();
            this.lblWhatsNew = new System.Windows.Forms.Label();
            this.lblRStatus = new System.Windows.Forms.Label();
            this.grpStatsDirect = new System.Windows.Forms.GroupBox();
            this.grpR = new System.Windows.Forms.GroupBox();
            this.cmdDownloadR = new System.Windows.Forms.Button();
            this.grpStatsDirect.SuspendLayout();
            this.grpR.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdUpdateStatsDirect
            // 
            this.cmdUpdateStatsDirect.Location = new System.Drawing.Point(9, 94);
            this.cmdUpdateStatsDirect.Name = "cmdUpdateStatsDirect";
            this.cmdUpdateStatsDirect.Size = new System.Drawing.Size(246, 23);
            this.cmdUpdateStatsDirect.TabIndex = 0;
            this.cmdUpdateStatsDirect.Text = "&Close StatsDirect and update";
            this.cmdUpdateStatsDirect.UseVisualStyleBackColor = true;
            this.cmdUpdateStatsDirect.Visible = false;
            this.cmdUpdateStatsDirect.Click += new System.EventHandler(this.button1_Click);
            // 
            // cmdClose
            // 
            this.cmdClose.Location = new System.Drawing.Point(197, 274);
            this.cmdClose.Name = "cmdClose";
            this.cmdClose.Size = new System.Drawing.Size(75, 23);
            this.cmdClose.TabIndex = 1;
            this.cmdClose.Text = "&Close";
            this.cmdClose.UseVisualStyleBackColor = true;
            this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
            // 
            // lblStatsDirectStatus
            // 
            this.lblStatsDirectStatus.Location = new System.Drawing.Point(10, 16);
            this.lblStatsDirectStatus.Name = "lblStatsDirectStatus";
            this.lblStatsDirectStatus.Size = new System.Drawing.Size(238, 51);
            this.lblStatsDirectStatus.TabIndex = 2;
            this.lblStatsDirectStatus.Text = "Checking for updates to StatsDirect...";
            // 
            // lblWhatsNew
            // 
            this.lblWhatsNew.AutoSize = true;
            this.lblWhatsNew.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblWhatsNew.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblWhatsNew.ForeColor = System.Drawing.SystemColors.Highlight;
            this.lblWhatsNew.Location = new System.Drawing.Point(10, 67);
            this.lblWhatsNew.Name = "lblWhatsNew";
            this.lblWhatsNew.Size = new System.Drawing.Size(69, 13);
            this.lblWhatsNew.TabIndex = 3;
            this.lblWhatsNew.Text = "What\'s new?";
            this.lblWhatsNew.Visible = false;
            this.lblWhatsNew.Click += new System.EventHandler(this.lblWhatsNew_Click);
            // 
            // lblRStatus
            // 
            this.lblRStatus.Location = new System.Drawing.Point(9, 16);
            this.lblRStatus.Name = "lblRStatus";
            this.lblRStatus.Size = new System.Drawing.Size(246, 75);
            this.lblRStatus.TabIndex = 4;
            this.lblRStatus.Text = "Checking for updates to R...";
            // 
            // grpStatsDirect
            // 
            this.grpStatsDirect.Controls.Add(this.cmdUpdateStatsDirect);
            this.grpStatsDirect.Controls.Add(this.lblWhatsNew);
            this.grpStatsDirect.Controls.Add(this.lblStatsDirectStatus);
            this.grpStatsDirect.Location = new System.Drawing.Point(12, 12);
            this.grpStatsDirect.Name = "grpStatsDirect";
            this.grpStatsDirect.Size = new System.Drawing.Size(261, 125);
            this.grpStatsDirect.TabIndex = 5;
            this.grpStatsDirect.TabStop = false;
            this.grpStatsDirect.Text = "StatsDirect";
            // 
            // grpR
            // 
            this.grpR.Controls.Add(this.cmdDownloadR);
            this.grpR.Controls.Add(this.lblRStatus);
            this.grpR.Location = new System.Drawing.Point(12, 143);
            this.grpR.Name = "grpR";
            this.grpR.Size = new System.Drawing.Size(261, 125);
            this.grpR.TabIndex = 6;
            this.grpR.TabStop = false;
            this.grpR.Text = "R";
            // 
            // cmdDownloadR
            // 
            this.cmdDownloadR.Location = new System.Drawing.Point(9, 94);
            this.cmdDownloadR.Name = "cmdDownloadR";
            this.cmdDownloadR.Size = new System.Drawing.Size(246, 23);
            this.cmdDownloadR.TabIndex = 0;
            this.cmdDownloadR.Text = "&Download R";
            this.cmdDownloadR.UseVisualStyleBackColor = true;
            this.cmdDownloadR.Visible = false;
            this.cmdDownloadR.Click += new System.EventHandler(this.cmdDownloadR_Click);
            // 
            // frmUpdateCheck
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 306);
            this.Controls.Add(this.cmdClose);
            this.Controls.Add(this.grpR);
            this.Controls.Add(this.grpStatsDirect);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmUpdateCheck";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Check for updates";
            this.Shown += new System.EventHandler(this.frmUpdateCheck_Shown);
            this.grpStatsDirect.ResumeLayout(false);
            this.grpStatsDirect.PerformLayout();
            this.grpR.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cmdUpdateStatsDirect;
        private System.Windows.Forms.Button cmdClose;
        private System.Windows.Forms.Label lblStatsDirectStatus;
        private System.Windows.Forms.Label lblWhatsNew;
        private System.Windows.Forms.Label lblRStatus;
        private System.Windows.Forms.GroupBox grpStatsDirect;
        private System.Windows.Forms.GroupBox grpR;
        private System.Windows.Forms.Button cmdDownloadR;
    }
}
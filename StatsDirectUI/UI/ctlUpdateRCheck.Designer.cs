namespace StatsDirect.UI
{
    partial class ctlUpdateRCheck
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
            if (disposing)
                checker.Dispose();
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
            this.lblStatus = new System.Windows.Forms.Label();
            this.grpR = new System.Windows.Forms.GroupBox();
            this.cmdDownloadR = new System.Windows.Forms.Button();
            this.grpR.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblStatus
            // 
            this.lblStatus.Location = new System.Drawing.Point(9, 16);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(246, 75);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "Checking for updates to R...";
            // 
            // grpR
            // 
            this.grpR.Controls.Add(this.cmdDownloadR);
            this.grpR.Controls.Add(this.lblStatus);
            this.grpR.Location = new System.Drawing.Point(0, 0);
            this.grpR.Margin = new System.Windows.Forms.Padding(0);
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
            // ctlUpdateRCheck
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.grpR);
            this.Name = "ctlUpdateRCheck";
            this.Size = new System.Drawing.Size(261, 125);
            this.grpR.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.GroupBox grpR;
        private System.Windows.Forms.Button cmdDownloadR;
    }
}
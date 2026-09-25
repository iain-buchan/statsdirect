namespace StatsDirect.UI
{
    partial class ctlUpdateStatsDirectCheck
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
                statsDirectChecker.Dispose();
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
            this.cmdUpdateStatsDirect = new System.Windows.Forms.Button();
            this.lblStatsDirectStatus = new System.Windows.Forms.Label();
            this.lblWhatsNew = new System.Windows.Forms.Label();
            this.grpStatsDirect = new System.Windows.Forms.GroupBox();
            this.grpStatsDirect.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdUpdateStatsDirect
            // 
            this.cmdUpdateStatsDirect.Location = new System.Drawing.Point(9, 94);
            this.cmdUpdateStatsDirect.Name = "cmdUpdateStatsDirect";
            this.cmdUpdateStatsDirect.Size = new System.Drawing.Size(246, 23);
            this.cmdUpdateStatsDirect.TabIndex = 0;
            this.cmdUpdateStatsDirect.Text = "&Download and update";
            this.cmdUpdateStatsDirect.UseVisualStyleBackColor = true;
            this.cmdUpdateStatsDirect.Visible = false;
            this.cmdUpdateStatsDirect.Click += new System.EventHandler(this.button1_Click);
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
            // grpStatsDirect
            // 
            this.grpStatsDirect.Controls.Add(this.cmdUpdateStatsDirect);
            this.grpStatsDirect.Controls.Add(this.lblWhatsNew);
            this.grpStatsDirect.Controls.Add(this.lblStatsDirectStatus);
            this.grpStatsDirect.Location = new System.Drawing.Point(0, 0);
            this.grpStatsDirect.Margin = new System.Windows.Forms.Padding(0);
            this.grpStatsDirect.Name = "grpStatsDirect";
            this.grpStatsDirect.Size = new System.Drawing.Size(261, 125);
            this.grpStatsDirect.TabIndex = 5;
            this.grpStatsDirect.TabStop = false;
            this.grpStatsDirect.Text = "StatsDirect";
            // 
            // ctlUpdateStatsDirectCheck
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.grpStatsDirect);
            this.Name = "ctlUpdateStatsDirectCheck";
            this.Size = new System.Drawing.Size(261, 125);
            this.grpStatsDirect.ResumeLayout(false);
            this.grpStatsDirect.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cmdUpdateStatsDirect;
        private System.Windows.Forms.Label lblStatsDirectStatus;
        private System.Windows.Forms.Label lblWhatsNew;
        private System.Windows.Forms.GroupBox grpStatsDirect;
    }
}
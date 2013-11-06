namespace StatsDirect.UI
{
    partial class frmInstallStatsDirect2
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmInstallStatsDirect2));
            this.lblRubric = new System.Windows.Forms.Label();
            this.lnkInstallR = new System.Windows.Forms.LinkLabel();
            this.cmdInstalled = new System.Windows.Forms.Button();
            this.cmdDoNotInstall = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblRubric
            // 
            this.lblRubric.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblRubric.Location = new System.Drawing.Point(12, 9);
            this.lblRubric.Name = "lblRubric";
            this.lblRubric.Size = new System.Drawing.Size(464, 103);
            this.lblRubric.TabIndex = 0;
            this.lblRubric.Text = resources.GetString("lblRubric.Text");
            // 
            // lnkInstallR
            // 
            this.lnkInstallR.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lnkInstallR.AutoSize = true;
            this.lnkInstallR.Location = new System.Drawing.Point(12, 120);
            this.lnkInstallR.Name = "lnkInstallR";
            this.lnkInstallR.Size = new System.Drawing.Size(119, 13);
            this.lnkInstallR.TabIndex = 1;
            this.lnkInstallR.TabStop = true;
            this.lnkInstallR.Text = "Download StatsDirect 2";
            this.lnkInstallR.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkInstallR_LinkClicked);
            // 
            // cmdInstalled
            // 
            this.cmdInstalled.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdInstalled.Location = new System.Drawing.Point(258, 115);
            this.cmdInstalled.Name = "cmdInstalled";
            this.cmdInstalled.Size = new System.Drawing.Size(131, 23);
            this.cmdInstalled.TabIndex = 3;
            this.cmdInstalled.Text = "StatsDirect 2 is installed";
            this.cmdInstalled.UseVisualStyleBackColor = true;
            this.cmdInstalled.Click += new System.EventHandler(this.cmdInstalled_Click);
            // 
            // cmdDoNotInstall
            // 
            this.cmdDoNotInstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdDoNotInstall.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdDoNotInstall.Location = new System.Drawing.Point(395, 115);
            this.cmdDoNotInstall.Name = "cmdDoNotInstall";
            this.cmdDoNotInstall.Size = new System.Drawing.Size(81, 23);
            this.cmdDoNotInstall.TabIndex = 4;
            this.cmdDoNotInstall.Text = "Cancel";
            this.cmdDoNotInstall.UseVisualStyleBackColor = true;
            this.cmdDoNotInstall.Click += new System.EventHandler(this.cmdDoNotInstall_Click);
            // 
            // frmInstallStatsDirect2
            // 
            this.AcceptButton = this.lnkInstallR;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdDoNotInstall;
            this.ClientSize = new System.Drawing.Size(488, 150);
            this.Controls.Add(this.cmdDoNotInstall);
            this.Controls.Add(this.cmdInstalled);
            this.Controls.Add(this.lnkInstallR);
            this.Controls.Add(this.lblRubric);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmInstallStatsDirect2";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "{0} StatsDirect 2?";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblRubric;
        private System.Windows.Forms.LinkLabel lnkInstallR;
        private System.Windows.Forms.Button cmdInstalled;
        private System.Windows.Forms.Button cmdDoNotInstall;
    }
}
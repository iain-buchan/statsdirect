namespace StatsDirect.UI
{
    partial class frmInstallR
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmInstallR));
            this.lblRubric = new System.Windows.Forms.Label();
            this.lnkInstallR = new System.Windows.Forms.LinkLabel();
            this.lnkMoreInformation = new System.Windows.Forms.LinkLabel();
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
            this.lblRubric.Size = new System.Drawing.Size(464, 83);
            this.lblRubric.TabIndex = 0;
            this.lblRubric.Text = resources.GetString("lblRubric.Text");
            // 
            // lnkInstallR
            // 
            this.lnkInstallR.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lnkInstallR.AutoSize = true;
            this.lnkInstallR.Location = new System.Drawing.Point(12, 100);
            this.lnkInstallR.Name = "lnkInstallR";
            this.lnkInstallR.Size = new System.Drawing.Size(45, 13);
            this.lnkInstallR.TabIndex = 1;
            this.lnkInstallR.TabStop = true;
            this.lnkInstallR.Text = "Install R";
            this.lnkInstallR.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkInstallR_LinkClicked);
            // 
            // lnkMoreInformation
            // 
            this.lnkMoreInformation.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lnkMoreInformation.AutoSize = true;
            this.lnkMoreInformation.Location = new System.Drawing.Point(83, 100);
            this.lnkMoreInformation.Name = "lnkMoreInformation";
            this.lnkMoreInformation.Size = new System.Drawing.Size(126, 13);
            this.lnkMoreInformation.TabIndex = 2;
            this.lnkMoreInformation.TabStop = true;
            this.lnkMoreInformation.Text = "More information about R";
            this.lnkMoreInformation.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkMoreInformation_LinkClicked);
            // 
            // cmdInstalled
            // 
            this.cmdInstalled.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdInstalled.Location = new System.Drawing.Point(314, 95);
            this.cmdInstalled.Name = "cmdInstalled";
            this.cmdInstalled.Size = new System.Drawing.Size(75, 23);
            this.cmdInstalled.TabIndex = 3;
            this.cmdInstalled.Text = "R is installed";
            this.cmdInstalled.UseVisualStyleBackColor = true;
            this.cmdInstalled.Click += new System.EventHandler(this.cmdInstalled_Click);
            // 
            // cmdDoNotInstall
            // 
            this.cmdDoNotInstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdDoNotInstall.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdDoNotInstall.Location = new System.Drawing.Point(395, 95);
            this.cmdDoNotInstall.Name = "cmdDoNotInstall";
            this.cmdDoNotInstall.Size = new System.Drawing.Size(81, 23);
            this.cmdDoNotInstall.TabIndex = 4;
            this.cmdDoNotInstall.Text = "Cancel";
            this.cmdDoNotInstall.UseVisualStyleBackColor = true;
            this.cmdDoNotInstall.Click += new System.EventHandler(this.cmdDoNotInstall_Click);
            // 
            // frmInstallR
            // 
            this.AcceptButton = this.lnkInstallR;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdDoNotInstall;
            this.ClientSize = new System.Drawing.Size(488, 130);
            this.Controls.Add(this.cmdDoNotInstall);
            this.Controls.Add(this.cmdInstalled);
            this.Controls.Add(this.lnkMoreInformation);
            this.Controls.Add(this.lnkInstallR);
            this.Controls.Add(this.lblRubric);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmInstallR";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Install R?";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblRubric;
        private System.Windows.Forms.LinkLabel lnkInstallR;
        private System.Windows.Forms.LinkLabel lnkMoreInformation;
        private System.Windows.Forms.Button cmdInstalled;
        private System.Windows.Forms.Button cmdDoNotInstall;
    }
}
namespace StatsDirect.UI
{
    partial class frmOpening
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmOpening));
            this.cmdOk = new System.Windows.Forms.Button();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.grpCreate = new System.Windows.Forms.GroupBox();
            this.cmdNewReport = new System.Windows.Forms.Button();
            this.cmdNewWorkbook = new System.Windows.Forms.Button();
            this.rdoNewReport = new System.Windows.Forms.RadioButton();
            this.rdoNewWorkbook = new System.Windows.Forms.RadioButton();
            this.grpOpen = new System.Windows.Forms.GroupBox();
            this.cmdBrowseImage = new System.Windows.Forms.Button();
            this.cmdBrowse = new System.Windows.Forms.Button();
            this.lstRecent = new System.Windows.Forms.ListBox();
            this.grpCreate.SuspendLayout();
            this.grpOpen.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdOk
            // 
            this.cmdOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOk.Location = new System.Drawing.Point(440, 13);
            this.cmdOk.Name = "cmdOk";
            this.cmdOk.Size = new System.Drawing.Size(75, 23);
            this.cmdOk.TabIndex = 2;
            this.cmdOk.Text = "&OK";
            this.cmdOk.UseVisualStyleBackColor = true;
            this.cmdOk.Click += new System.EventHandler(this.cmdOk_Click);
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.Location = new System.Drawing.Point(440, 43);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(75, 23);
            this.cmdCancel.TabIndex = 3;
            this.cmdCancel.Text = "&Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // grpCreate
            // 
            this.grpCreate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpCreate.Controls.Add(this.cmdNewReport);
            this.grpCreate.Controls.Add(this.cmdNewWorkbook);
            this.grpCreate.Controls.Add(this.rdoNewReport);
            this.grpCreate.Controls.Add(this.rdoNewWorkbook);
            this.grpCreate.Location = new System.Drawing.Point(12, 13);
            this.grpCreate.Name = "grpCreate";
            this.grpCreate.Size = new System.Drawing.Size(422, 104);
            this.grpCreate.TabIndex = 0;
            this.grpCreate.TabStop = false;
            this.grpCreate.Text = "Create";
            // 
            // cmdNewReport
            // 
            this.cmdNewReport.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.cmdNewReport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cmdNewReport.ForeColor = System.Drawing.SystemColors.Control;
            this.cmdNewReport.Image = global::StatsDirect.UI.Properties.Resources.sd3_report_32;
            this.cmdNewReport.Location = new System.Drawing.Point(6, 59);
            this.cmdNewReport.Name = "cmdNewReport";
            this.cmdNewReport.Size = new System.Drawing.Size(35, 35);
            this.cmdNewReport.TabIndex = 3;
            this.cmdNewReport.TabStop = false;
            this.cmdNewReport.UseVisualStyleBackColor = true;
            this.cmdNewReport.Click += new System.EventHandler(this.cmdNewReport_Click);
            // 
            // cmdNewWorkbook
            // 
            this.cmdNewWorkbook.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.cmdNewWorkbook.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cmdNewWorkbook.ForeColor = System.Drawing.SystemColors.Control;
            this.cmdNewWorkbook.Image = global::StatsDirect.UI.Properties.Resources.sd3_grid_32;
            this.cmdNewWorkbook.Location = new System.Drawing.Point(6, 19);
            this.cmdNewWorkbook.Name = "cmdNewWorkbook";
            this.cmdNewWorkbook.Size = new System.Drawing.Size(35, 34);
            this.cmdNewWorkbook.TabIndex = 2;
            this.cmdNewWorkbook.TabStop = false;
            this.cmdNewWorkbook.UseVisualStyleBackColor = true;
            this.cmdNewWorkbook.Click += new System.EventHandler(this.cmdNewWorkbook_Click);
            // 
            // rdoNewReport
            // 
            this.rdoNewReport.AutoSize = true;
            this.rdoNewReport.Location = new System.Drawing.Point(47, 68);
            this.rdoNewReport.Name = "rdoNewReport";
            this.rdoNewReport.Size = new System.Drawing.Size(77, 17);
            this.rdoNewReport.TabIndex = 1;
            this.rdoNewReport.TabStop = true;
            this.rdoNewReport.Text = "New report";
            this.rdoNewReport.UseVisualStyleBackColor = true;
            this.rdoNewReport.Click += new System.EventHandler(this.rdoNewReport_Click);
            // 
            // rdoNewWorkbook
            // 
            this.rdoNewWorkbook.AutoSize = true;
            this.rdoNewWorkbook.Location = new System.Drawing.Point(47, 28);
            this.rdoNewWorkbook.Name = "rdoNewWorkbook";
            this.rdoNewWorkbook.Size = new System.Drawing.Size(121, 17);
            this.rdoNewWorkbook.TabIndex = 0;
            this.rdoNewWorkbook.TabStop = true;
            this.rdoNewWorkbook.Text = "New data workbook";
            this.rdoNewWorkbook.UseVisualStyleBackColor = true;
            this.rdoNewWorkbook.Click += new System.EventHandler(this.rdoNewWorkbook_Click);
            // 
            // grpOpen
            // 
            this.grpOpen.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpOpen.Controls.Add(this.cmdBrowseImage);
            this.grpOpen.Controls.Add(this.cmdBrowse);
            this.grpOpen.Controls.Add(this.lstRecent);
            this.grpOpen.Location = new System.Drawing.Point(12, 123);
            this.grpOpen.Name = "grpOpen";
            this.grpOpen.Size = new System.Drawing.Size(503, 140);
            this.grpOpen.TabIndex = 1;
            this.grpOpen.TabStop = false;
            this.grpOpen.Text = "Open";
            // 
            // cmdBrowseImage
            // 
            this.cmdBrowseImage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cmdBrowseImage.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.cmdBrowseImage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cmdBrowseImage.ForeColor = System.Drawing.SystemColors.Control;
            this.cmdBrowseImage.Image = global::StatsDirect.UI.Properties.Resources.Open;
            this.cmdBrowseImage.Location = new System.Drawing.Point(6, 97);
            this.cmdBrowseImage.Name = "cmdBrowseImage";
            this.cmdBrowseImage.Size = new System.Drawing.Size(34, 33);
            this.cmdBrowseImage.TabIndex = 2;
            this.cmdBrowseImage.TabStop = false;
            this.cmdBrowseImage.UseVisualStyleBackColor = true;
            this.cmdBrowseImage.Click += new System.EventHandler(this.cmdBrowseImage_Click);
            // 
            // cmdBrowse
            // 
            this.cmdBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cmdBrowse.Location = new System.Drawing.Point(47, 102);
            this.cmdBrowse.Name = "cmdBrowse";
            this.cmdBrowse.Size = new System.Drawing.Size(91, 23);
            this.cmdBrowse.TabIndex = 1;
            this.cmdBrowse.Text = "&Browse for file";
            this.cmdBrowse.UseVisualStyleBackColor = true;
            this.cmdBrowse.Click += new System.EventHandler(this.cmdBrowse_Click);
            // 
            // lstRecent
            // 
            this.lstRecent.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lstRecent.FormattingEnabled = true;
            this.lstRecent.Location = new System.Drawing.Point(6, 19);
            this.lstRecent.Name = "lstRecent";
            this.lstRecent.Size = new System.Drawing.Size(491, 69);
            this.lstRecent.TabIndex = 0;
            this.lstRecent.DoubleClick += new System.EventHandler(this.lstRecent_DoubleClick);
            this.lstRecent.Enter += new System.EventHandler(this.lstRecent_Enter);
            this.lstRecent.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.lstRecent_KeyPress);
            // 
            // frmOpening
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(527, 279);
            this.Controls.Add(this.grpOpen);
            this.Controls.Add(this.grpCreate);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.cmdOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmOpening";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Open or create file";
            this.Shown += new System.EventHandler(this.frmOpening_Shown);
            this.grpCreate.ResumeLayout(false);
            this.grpCreate.PerformLayout();
            this.grpOpen.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cmdOk;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.GroupBox grpCreate;
        private System.Windows.Forms.GroupBox grpOpen;
        private System.Windows.Forms.Button cmdNewReport;
        private System.Windows.Forms.Button cmdNewWorkbook;
        private System.Windows.Forms.RadioButton rdoNewReport;
        private System.Windows.Forms.RadioButton rdoNewWorkbook;
        private System.Windows.Forms.Button cmdBrowse;
        private System.Windows.Forms.ListBox lstRecent;
        private System.Windows.Forms.Button cmdBrowseImage;
    }
}
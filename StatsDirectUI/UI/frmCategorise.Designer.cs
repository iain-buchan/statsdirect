namespace StatsDirect.UI
{
    partial class frmCategorise
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmCategorise));
            this.cmdOK = new System.Windows.Forms.Button();
            this.cmdHelp = new System.Windows.Forms.Button();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.lblCutoff = new System.Windows.Forms.Label();
            this.gridCutoffs = new System.Windows.Forms.DataGridView();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chkSaveKeyAndCounts = new System.Windows.Forms.CheckBox();
            this.cmdRecalculate = new System.Windows.Forms.Button();
            this.txtTitle = new System.Windows.Forms.TextBox();
            this.txtDescribe = new System.Windows.Forms.TextBox();
            this.grpAutomatic = new System.Windows.Forms.GroupBox();
            this.grpCentileMethod = new System.Windows.Forms.GroupBox();
            this.rdoCentileMethod2 = new System.Windows.Forms.RadioButton();
            this.rdoCentileMethod1 = new System.Windows.Forms.RadioButton();
            this.rdoTertiles = new System.Windows.Forms.RadioButton();
            this.txtIntervals = new System.Windows.Forms.TextBox();
            this.txtInterval = new System.Windows.Forms.TextBox();
            this.txtMinimum = new System.Windows.Forms.TextBox();
            this.lblIntervals = new System.Windows.Forms.Label();
            this.lblInterval = new System.Windows.Forms.Label();
            this.lblMinimum = new System.Windows.Forms.Label();
            this.cmdReGroup = new System.Windows.Forms.Button();
            this.rdoUserDefined = new System.Windows.Forms.RadioButton();
            this.rdoAge6Groups = new System.Windows.Forms.RadioButton();
            this.rdoAge1By5 = new System.Windows.Forms.RadioButton();
            this.rdoAge15By10 = new System.Windows.Forms.RadioButton();
            this.rdoAge15By5 = new System.Windows.Forms.RadioButton();
            this.rdoDeciles = new System.Windows.Forms.RadioButton();
            this.rdoQuintiles = new System.Windows.Forms.RadioButton();
            this.rdoQuartiles = new System.Windows.Forms.RadioButton();
            this.lblTitle = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.gridCutoffs)).BeginInit();
            this.grpAutomatic.SuspendLayout();
            this.grpCentileMethod.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.Location = new System.Drawing.Point(314, 12);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(75, 23);
            this.cmdOK.TabIndex = 15;
            this.cmdOK.Text = "&OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // cmdHelp
            // 
            this.cmdHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdHelp.Location = new System.Drawing.Point(395, 12);
            this.cmdHelp.Name = "cmdHelp";
            this.cmdHelp.Size = new System.Drawing.Size(75, 23);
            this.cmdHelp.TabIndex = 16;
            this.cmdHelp.Text = "&Help";
            this.cmdHelp.UseVisualStyleBackColor = true;
            this.cmdHelp.Click += new System.EventHandler(this.cmdHelp_Click);
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(476, 12);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(75, 23);
            this.cmdCancel.TabIndex = 17;
            this.cmdCancel.Text = "&Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // lblCutoff
            // 
            this.lblCutoff.Location = new System.Drawing.Point(12, 17);
            this.lblCutoff.Name = "lblCutoff";
            this.lblCutoff.Size = new System.Drawing.Size(280, 48);
            this.lblCutoff.TabIndex = 6;
            this.lblCutoff.Text = "A cut-off of x means that values up to and including x, but greater than any cut-" +
                "off below, will be a category for grouping your data:";
            // 
            // gridCutoffs
            // 
            this.gridCutoffs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridCutoffs.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column1,
            this.Column2});
            this.gridCutoffs.Location = new System.Drawing.Point(12, 68);
            this.gridCutoffs.Name = "gridCutoffs";
            this.gridCutoffs.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridCutoffs.Size = new System.Drawing.Size(280, 480);
            this.gridCutoffs.TabIndex = 0;
            // 
            // Column1
            // 
            this.Column1.HeaderText = "cut-off";
            this.Column1.Name = "Column1";
            this.Column1.Width = 130;
            // 
            // Column2
            // 
            this.Column2.HeaderText = "count";
            this.Column2.Name = "Column2";
            this.Column2.ReadOnly = true;
            this.Column2.Width = 80;
            // 
            // chkSaveKeyAndCounts
            // 
            this.chkSaveKeyAndCounts.AutoSize = true;
            this.chkSaveKeyAndCounts.Location = new System.Drawing.Point(12, 558);
            this.chkSaveKeyAndCounts.Name = "chkSaveKeyAndCounts";
            this.chkSaveKeyAndCounts.Size = new System.Drawing.Size(127, 17);
            this.chkSaveKeyAndCounts.TabIndex = 1;
            this.chkSaveKeyAndCounts.Text = "Save key and counts";
            this.chkSaveKeyAndCounts.UseVisualStyleBackColor = true;
            // 
            // cmdRecalculate
            // 
            this.cmdRecalculate.Location = new System.Drawing.Point(209, 554);
            this.cmdRecalculate.Name = "cmdRecalculate";
            this.cmdRecalculate.Size = new System.Drawing.Size(83, 23);
            this.cmdRecalculate.TabIndex = 2;
            this.cmdRecalculate.Text = "Re-Calculate";
            this.cmdRecalculate.UseVisualStyleBackColor = true;
            this.cmdRecalculate.Click += new System.EventHandler(this.cmdRecalculate_Click);
            // 
            // txtTitle
            // 
            this.txtTitle.Location = new System.Drawing.Point(209, 583);
            this.txtTitle.Name = "txtTitle";
            this.txtTitle.Size = new System.Drawing.Size(345, 20);
            this.txtTitle.TabIndex = 14;
            // 
            // txtDescribe
            // 
            this.txtDescribe.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDescribe.Location = new System.Drawing.Point(298, 68);
            this.txtDescribe.Multiline = true;
            this.txtDescribe.Name = "txtDescribe";
            this.txtDescribe.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDescribe.Size = new System.Drawing.Size(256, 137);
            this.txtDescribe.TabIndex = 11;
            this.txtDescribe.TabStop = false;
            // 
            // grpAutomatic
            // 
            this.grpAutomatic.Controls.Add(this.grpCentileMethod);
            this.grpAutomatic.Controls.Add(this.rdoTertiles);
            this.grpAutomatic.Controls.Add(this.txtIntervals);
            this.grpAutomatic.Controls.Add(this.txtInterval);
            this.grpAutomatic.Controls.Add(this.txtMinimum);
            this.grpAutomatic.Controls.Add(this.lblIntervals);
            this.grpAutomatic.Controls.Add(this.lblInterval);
            this.grpAutomatic.Controls.Add(this.lblMinimum);
            this.grpAutomatic.Controls.Add(this.cmdReGroup);
            this.grpAutomatic.Controls.Add(this.rdoUserDefined);
            this.grpAutomatic.Controls.Add(this.rdoAge6Groups);
            this.grpAutomatic.Controls.Add(this.rdoAge1By5);
            this.grpAutomatic.Controls.Add(this.rdoAge15By10);
            this.grpAutomatic.Controls.Add(this.rdoAge15By5);
            this.grpAutomatic.Controls.Add(this.rdoDeciles);
            this.grpAutomatic.Controls.Add(this.rdoQuintiles);
            this.grpAutomatic.Controls.Add(this.rdoQuartiles);
            this.grpAutomatic.Location = new System.Drawing.Point(298, 211);
            this.grpAutomatic.Name = "grpAutomatic";
            this.grpAutomatic.Size = new System.Drawing.Size(256, 366);
            this.grpAutomatic.TabIndex = 12;
            this.grpAutomatic.TabStop = false;
            this.grpAutomatic.Text = "Automatic Grouping";
            // 
            // grpCentileMethod
            // 
            this.grpCentileMethod.Controls.Add(this.rdoCentileMethod2);
            this.grpCentileMethod.Controls.Add(this.rdoCentileMethod1);
            this.grpCentileMethod.Location = new System.Drawing.Point(140, 19);
            this.grpCentileMethod.Name = "grpCentileMethod";
            this.grpCentileMethod.Size = new System.Drawing.Size(110, 73);
            this.grpCentileMethod.TabIndex = 15;
            this.grpCentileMethod.TabStop = false;
            this.grpCentileMethod.Text = "Calculate cuts";
            // 
            // rdoCentileMethod2
            // 
            this.rdoCentileMethod2.AutoSize = true;
            this.rdoCentileMethod2.Location = new System.Drawing.Point(7, 44);
            this.rdoCentileMethod2.Name = "rdoCentileMethod2";
            this.rdoCentileMethod2.Size = new System.Drawing.Size(70, 17);
            this.rdoCentileMethod2.TabIndex = 1;
            this.rdoCentileMethod2.TabStop = true;
            this.rdoCentileMethod2.Text = "Method 2";
            this.rdoCentileMethod2.UseVisualStyleBackColor = true;
            // 
            // rdoCentileMethod1
            // 
            this.rdoCentileMethod1.AutoSize = true;
            this.rdoCentileMethod1.Checked = true;
            this.rdoCentileMethod1.Location = new System.Drawing.Point(7, 20);
            this.rdoCentileMethod1.Name = "rdoCentileMethod1";
            this.rdoCentileMethod1.Size = new System.Drawing.Size(70, 17);
            this.rdoCentileMethod1.TabIndex = 0;
            this.rdoCentileMethod1.TabStop = true;
            this.rdoCentileMethod1.Text = "Method 1";
            this.rdoCentileMethod1.UseVisualStyleBackColor = true;
            // 
            // rdoTertiles
            // 
            this.rdoTertiles.AutoSize = true;
            this.rdoTertiles.Location = new System.Drawing.Point(6, 19);
            this.rdoTertiles.Name = "rdoTertiles";
            this.rdoTertiles.Size = new System.Drawing.Size(109, 17);
            this.rdoTertiles.TabIndex = 14;
            this.rdoTertiles.Text = "Tertiles (3 groups)";
            this.rdoTertiles.UseVisualStyleBackColor = true;
            // 
            // txtIntervals
            // 
            this.txtIntervals.Location = new System.Drawing.Point(140, 302);
            this.txtIntervals.Name = "txtIntervals";
            this.txtIntervals.Size = new System.Drawing.Size(100, 20);
            this.txtIntervals.TabIndex = 13;
            this.txtIntervals.Text = "10";
            this.txtIntervals.TextChanged += new System.EventHandler(this.txtIntervals_TextChanged);
            // 
            // txtInterval
            // 
            this.txtInterval.Location = new System.Drawing.Point(140, 276);
            this.txtInterval.Name = "txtInterval";
            this.txtInterval.Size = new System.Drawing.Size(100, 20);
            this.txtInterval.TabIndex = 12;
            this.txtInterval.Text = "0";
            this.txtInterval.TextChanged += new System.EventHandler(this.txtInterval_TextChanged);
            // 
            // txtMinimum
            // 
            this.txtMinimum.Location = new System.Drawing.Point(140, 250);
            this.txtMinimum.Name = "txtMinimum";
            this.txtMinimum.Size = new System.Drawing.Size(100, 20);
            this.txtMinimum.TabIndex = 11;
            this.txtMinimum.Text = "0";
            // 
            // lblIntervals
            // 
            this.lblIntervals.AutoSize = true;
            this.lblIntervals.Location = new System.Drawing.Point(33, 305);
            this.lblIntervals.Name = "lblIntervals";
            this.lblIntervals.Size = new System.Drawing.Size(101, 13);
            this.lblIntervals.TabIndex = 11;
            this.lblIntervals.Text = "Number of intervals:";
            // 
            // lblInterval
            // 
            this.lblInterval.AutoSize = true;
            this.lblInterval.Location = new System.Drawing.Point(38, 279);
            this.lblInterval.Name = "lblInterval";
            this.lblInterval.Size = new System.Drawing.Size(96, 13);
            this.lblInterval.TabIndex = 10;
            this.lblInterval.Text = "Step size (interval):";
            // 
            // lblMinimum
            // 
            this.lblMinimum.AutoSize = true;
            this.lblMinimum.Location = new System.Drawing.Point(80, 253);
            this.lblMinimum.Name = "lblMinimum";
            this.lblMinimum.Size = new System.Drawing.Size(51, 13);
            this.lblMinimum.TabIndex = 9;
            this.lblMinimum.Text = "Minimum:";
            // 
            // cmdReGroup
            // 
            this.cmdReGroup.Location = new System.Drawing.Point(6, 333);
            this.cmdReGroup.Name = "cmdReGroup";
            this.cmdReGroup.Size = new System.Drawing.Size(75, 23);
            this.cmdReGroup.TabIndex = 8;
            this.cmdReGroup.Text = "Re-Group";
            this.cmdReGroup.UseVisualStyleBackColor = true;
            this.cmdReGroup.Click += new System.EventHandler(this.cmdReGroup_Click);
            // 
            // rdoUserDefined
            // 
            this.rdoUserDefined.AutoSize = true;
            this.rdoUserDefined.Location = new System.Drawing.Point(6, 226);
            this.rdoUserDefined.Name = "rdoUserDefined";
            this.rdoUserDefined.Size = new System.Drawing.Size(125, 17);
            this.rdoUserDefined.TabIndex = 10;
            this.rdoUserDefined.Text = "User-defined interval:";
            this.rdoUserDefined.UseVisualStyleBackColor = true;
            // 
            // rdoAge6Groups
            // 
            this.rdoAge6Groups.AutoSize = true;
            this.rdoAge6Groups.Location = new System.Drawing.Point(6, 194);
            this.rdoAge6Groups.Name = "rdoAge6Groups";
            this.rdoAge6Groups.Size = new System.Drawing.Size(235, 17);
            this.rdoAge6Groups.TabIndex = 9;
            this.rdoAge6Groups.Text = "Ages <1, 1-4, 5-14, 15-34, 35-64, 65-74, 75+";
            this.rdoAge6Groups.UseVisualStyleBackColor = true;
            // 
            // rdoAge1By5
            // 
            this.rdoAge1By5.AutoSize = true;
            this.rdoAge1By5.Location = new System.Drawing.Point(6, 170);
            this.rdoAge1By5.Name = "rdoAge1By5";
            this.rdoAge1By5.Size = new System.Drawing.Size(145, 17);
            this.rdoAge1By5.TabIndex = 8;
            this.rdoAge1By5.Text = "Ages <1... 5 yearly to 85+";
            this.rdoAge1By5.UseVisualStyleBackColor = true;
            // 
            // rdoAge15By10
            // 
            this.rdoAge15By10.AutoSize = true;
            this.rdoAge15By10.Location = new System.Drawing.Point(6, 147);
            this.rdoAge15By10.Name = "rdoAge15By10";
            this.rdoAge15By10.Size = new System.Drawing.Size(157, 17);
            this.rdoAge15By10.TabIndex = 7;
            this.rdoAge15By10.Text = "Ages <15... 10 yearly to 85+";
            this.rdoAge15By10.UseVisualStyleBackColor = true;
            // 
            // rdoAge15By5
            // 
            this.rdoAge15By5.AutoSize = true;
            this.rdoAge15By5.Location = new System.Drawing.Point(6, 123);
            this.rdoAge15By5.Name = "rdoAge15By5";
            this.rdoAge15By5.Size = new System.Drawing.Size(151, 17);
            this.rdoAge15By5.TabIndex = 6;
            this.rdoAge15By5.Text = "Ages <15... 5 yearly to 85+";
            this.rdoAge15By5.UseVisualStyleBackColor = true;
            // 
            // rdoDeciles
            // 
            this.rdoDeciles.AutoSize = true;
            this.rdoDeciles.Location = new System.Drawing.Point(6, 90);
            this.rdoDeciles.Name = "rdoDeciles";
            this.rdoDeciles.Size = new System.Drawing.Size(116, 17);
            this.rdoDeciles.TabIndex = 5;
            this.rdoDeciles.Text = "Deciles (10 groups)";
            this.rdoDeciles.UseVisualStyleBackColor = true;
            // 
            // rdoQuintiles
            // 
            this.rdoQuintiles.AutoSize = true;
            this.rdoQuintiles.Location = new System.Drawing.Point(6, 66);
            this.rdoQuintiles.Name = "rdoQuintiles";
            this.rdoQuintiles.Size = new System.Drawing.Size(115, 17);
            this.rdoQuintiles.TabIndex = 4;
            this.rdoQuintiles.Text = "Quintiles (5 groups)";
            this.rdoQuintiles.UseVisualStyleBackColor = true;
            // 
            // rdoQuartiles
            // 
            this.rdoQuartiles.AutoSize = true;
            this.rdoQuartiles.Checked = true;
            this.rdoQuartiles.Location = new System.Drawing.Point(6, 42);
            this.rdoQuartiles.Name = "rdoQuartiles";
            this.rdoQuartiles.Size = new System.Drawing.Size(116, 17);
            this.rdoQuartiles.TabIndex = 3;
            this.rdoQuartiles.TabStop = true;
            this.rdoQuartiles.Text = "Quartiles (4 groups)";
            this.rdoQuartiles.UseVisualStyleBackColor = true;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(173, 586);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(30, 13);
            this.lblTitle.TabIndex = 13;
            this.lblTitle.Text = "Title:";
            // 
            // frmCategorise
            // 
            this.AcceptButton = this.cmdOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdCancel;
            this.ClientSize = new System.Drawing.Size(563, 610);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.grpAutomatic);
            this.Controls.Add(this.txtDescribe);
            this.Controls.Add(this.txtTitle);
            this.Controls.Add(this.cmdRecalculate);
            this.Controls.Add(this.chkSaveKeyAndCounts);
            this.Controls.Add(this.gridCutoffs);
            this.Controls.Add(this.lblCutoff);
            this.Controls.Add(this.cmdOK);
            this.Controls.Add(this.cmdHelp);
            this.Controls.Add(this.cmdCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmCategorise";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Categorise data into groups";
            this.Shown += new System.EventHandler(this.frmCategorise_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.gridCutoffs)).EndInit();
            this.grpAutomatic.ResumeLayout(false);
            this.grpAutomatic.PerformLayout();
            this.grpCentileMethod.ResumeLayout(false);
            this.grpCentileMethod.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.Button cmdHelp;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.Label lblCutoff;
        private System.Windows.Forms.DataGridView gridCutoffs;
        private System.Windows.Forms.CheckBox chkSaveKeyAndCounts;
        private System.Windows.Forms.Button cmdRecalculate;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.TextBox txtDescribe;
        private System.Windows.Forms.GroupBox grpAutomatic;
        private System.Windows.Forms.RadioButton rdoAge1By5;
        private System.Windows.Forms.RadioButton rdoAge15By10;
        private System.Windows.Forms.RadioButton rdoAge15By5;
        private System.Windows.Forms.RadioButton rdoDeciles;
        private System.Windows.Forms.RadioButton rdoQuintiles;
        private System.Windows.Forms.RadioButton rdoQuartiles;
        private System.Windows.Forms.RadioButton rdoAge6Groups;
        private System.Windows.Forms.TextBox txtIntervals;
        private System.Windows.Forms.TextBox txtInterval;
        private System.Windows.Forms.TextBox txtMinimum;
        private System.Windows.Forms.Label lblIntervals;
        private System.Windows.Forms.Label lblInterval;
        private System.Windows.Forms.Label lblMinimum;
        private System.Windows.Forms.Button cmdReGroup;
        private System.Windows.Forms.RadioButton rdoUserDefined;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.RadioButton rdoTertiles;
        private System.Windows.Forms.GroupBox grpCentileMethod;
        private System.Windows.Forms.RadioButton rdoCentileMethod2;
        private System.Windows.Forms.RadioButton rdoCentileMethod1;
    }
}
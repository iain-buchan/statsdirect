using System;

namespace StatsDirect.UI
{
    partial class ctlHistogramOptions
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
            if (disposing)
            {
                foreach (Delegate d in ScaleChanged.GetInvocationList())
                {
                    ScaleChanged -= (EventHandler)d;
                }
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
            this.label1 = new System.Windows.Forms.Label();
            this.txtBins = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.lblDataMinimum = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.lblDataMaximum = new System.Windows.Forms.Label();
            this.txtMidpointInterval = new System.Windows.Forms.TextBox();
            this.txtMinimumMidpoint = new System.Windows.Forms.TextBox();
            this.cmdAutoBins = new System.Windows.Forms.Button();
            this.cmdAutoMidpoints = new System.Windows.Forms.Button();
            this.cmdReset = new System.Windows.Forms.Button();
            this.lstBinValues = new System.Windows.Forms.ListView();
            this.colLow = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colMidpoint = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colHigh = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.chkOverlayNormalCurve = new System.Windows.Forms.CheckBox();
            this.chkPoolVariables = new System.Windows.Forms.CheckBox();
            this.cboVariable = new System.Windows.Forms.ComboBox();
            this.lblVariable = new System.Windows.Forms.Label();
            this.cmdPreviousVariable = new System.Windows.Forms.Button();
            this.cmdNextVariable = new System.Windows.Forms.Button();
            this.chkShowRelativeFrequencies = new System.Windows.Forms.CheckBox();
            this.lblBinChoiceMethod = new System.Windows.Forms.Label();
            this.cboBinChoiceMethod = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(56, 56);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Data maximum: ";
            // 
            // txtBins
            // 
            this.txtBins.Location = new System.Drawing.Point(138, 120);
            this.txtBins.Name = "txtBins";
            this.txtBins.Size = new System.Drawing.Size(100, 20);
            this.txtBins.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(0, 250);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Bin values:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(54, 123);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(81, 13);
            this.label7.TabIndex = 7;
            this.label7.Text = "Number of bins:";
            this.label7.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblDataMinimum
            // 
            this.lblDataMinimum.AutoSize = true;
            this.lblDataMinimum.Location = new System.Drawing.Point(136, 73);
            this.lblDataMinimum.Name = "lblDataMinimum";
            this.lblDataMinimum.Size = new System.Drawing.Size(29, 13);
            this.lblDataMinimum.TabIndex = 8;
            this.lblDataMinimum.Text = "(min)";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(59, 73);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(76, 13);
            this.label9.TabIndex = 9;
            this.label9.Text = "Data minimum:";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(22, 149);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(113, 13);
            this.label13.TabIndex = 13;
            this.label13.Text = "Minimum bin mid-point:";
            this.label13.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(28, 175);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(107, 13);
            this.label14.TabIndex = 14;
            this.label14.Text = "Bin mid-point interval:";
            // 
            // lblDataMaximum
            // 
            this.lblDataMaximum.AutoSize = true;
            this.lblDataMaximum.Location = new System.Drawing.Point(136, 56);
            this.lblDataMaximum.Name = "lblDataMaximum";
            this.lblDataMaximum.Size = new System.Drawing.Size(32, 13);
            this.lblDataMaximum.TabIndex = 15;
            this.lblDataMaximum.Text = "(max)";
            // 
            // txtMidpointInterval
            // 
            this.txtMidpointInterval.Location = new System.Drawing.Point(138, 172);
            this.txtMidpointInterval.Name = "txtMidpointInterval";
            this.txtMidpointInterval.Size = new System.Drawing.Size(100, 20);
            this.txtMidpointInterval.TabIndex = 9;
            // 
            // txtMinimumMidpoint
            // 
            this.txtMinimumMidpoint.Location = new System.Drawing.Point(138, 146);
            this.txtMinimumMidpoint.Name = "txtMinimumMidpoint";
            this.txtMinimumMidpoint.Size = new System.Drawing.Size(100, 20);
            this.txtMinimumMidpoint.TabIndex = 7;
            // 
            // cmdAutoBins
            // 
            this.cmdAutoBins.Location = new System.Drawing.Point(244, 118);
            this.cmdAutoBins.Name = "cmdAutoBins";
            this.cmdAutoBins.Size = new System.Drawing.Size(75, 23);
            this.cmdAutoBins.TabIndex = 6;
            this.cmdAutoBins.Text = "&Auto Bins";
            this.cmdAutoBins.UseVisualStyleBackColor = true;
            this.cmdAutoBins.Click += new System.EventHandler(this.cmdAutoBins_Click);
            // 
            // cmdAutoMidpoints
            // 
            this.cmdAutoMidpoints.Location = new System.Drawing.Point(244, 144);
            this.cmdAutoMidpoints.Name = "cmdAutoMidpoints";
            this.cmdAutoMidpoints.Size = new System.Drawing.Size(75, 23);
            this.cmdAutoMidpoints.TabIndex = 8;
            this.cmdAutoMidpoints.Text = "A&uto mid-pts";
            this.cmdAutoMidpoints.UseVisualStyleBackColor = true;
            this.cmdAutoMidpoints.Click += new System.EventHandler(this.cmdAutoMidpoints_Click);
            // 
            // cmdReset
            // 
            this.cmdReset.Location = new System.Drawing.Point(244, 170);
            this.cmdReset.Name = "cmdReset";
            this.cmdReset.Size = new System.Drawing.Size(75, 23);
            this.cmdReset.TabIndex = 10;
            this.cmdReset.Text = "&Manual reset";
            this.cmdReset.UseVisualStyleBackColor = true;
            this.cmdReset.Click += new System.EventHandler(this.cmdReset_Click);
            // 
            // lstBinValues
            // 
            this.lstBinValues.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colLow,
            this.colMidpoint,
            this.colHigh});
            this.lstBinValues.FullRowSelect = true;
            this.lstBinValues.Location = new System.Drawing.Point(3, 266);
            this.lstBinValues.Name = "lstBinValues";
            this.lstBinValues.Size = new System.Drawing.Size(316, 149);
            this.lstBinValues.TabIndex = 13;
            this.lstBinValues.UseCompatibleStateImageBehavior = false;
            this.lstBinValues.View = System.Windows.Forms.View.Details;
            // 
            // colLow
            // 
            this.colLow.Text = "Low";
            this.colLow.Width = 95;
            // 
            // colMidpoint
            // 
            this.colMidpoint.Text = "Mid-point";
            this.colMidpoint.Width = 95;
            // 
            // colHigh
            // 
            this.colHigh.Text = "High";
            this.colHigh.Width = 95;
            // 
            // chkOverlayNormalCurve
            // 
            this.chkOverlayNormalCurve.AutoSize = true;
            this.chkOverlayNormalCurve.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkOverlayNormalCurve.Location = new System.Drawing.Point(23, 221);
            this.chkOverlayNormalCurve.Name = "chkOverlayNormalCurve";
            this.chkOverlayNormalCurve.Size = new System.Drawing.Size(129, 17);
            this.chkOverlayNormalCurve.TabIndex = 12;
            this.chkOverlayNormalCurve.Text = "Overlay normal curve:";
            this.chkOverlayNormalCurve.UseVisualStyleBackColor = true;
            // 
            // chkPoolVariables
            // 
            this.chkPoolVariables.AutoSize = true;
            this.chkPoolVariables.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkPoolVariables.Location = new System.Drawing.Point(62, 3);
            this.chkPoolVariables.Name = "chkPoolVariables";
            this.chkPoolVariables.Size = new System.Drawing.Size(90, 17);
            this.chkPoolVariables.TabIndex = 0;
            this.chkPoolVariables.Text = "Pooled scale:";
            this.chkPoolVariables.UseVisualStyleBackColor = true;
            // 
            // cboVariable
            // 
            this.cboVariable.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboVariable.FormattingEnabled = true;
            this.cboVariable.Location = new System.Drawing.Point(138, 26);
            this.cboVariable.Name = "cboVariable";
            this.cboVariable.Size = new System.Drawing.Size(136, 21);
            this.cboVariable.TabIndex = 1;
            this.cboVariable.SelectedIndexChanged += new System.EventHandler(this.cboVariable_SelectedIndexChanged);
            // 
            // lblVariable
            // 
            this.lblVariable.AutoSize = true;
            this.lblVariable.Location = new System.Drawing.Point(87, 30);
            this.lblVariable.Name = "lblVariable";
            this.lblVariable.Size = new System.Drawing.Size(48, 13);
            this.lblVariable.TabIndex = 30;
            this.lblVariable.Text = "Variable:";
            // 
            // cmdPreviousVariable
            // 
            this.cmdPreviousVariable.Location = new System.Drawing.Point(277, 26);
            this.cmdPreviousVariable.Margin = new System.Windows.Forms.Padding(0);
            this.cmdPreviousVariable.Name = "cmdPreviousVariable";
            this.cmdPreviousVariable.Size = new System.Drawing.Size(21, 21);
            this.cmdPreviousVariable.TabIndex = 2;
            this.cmdPreviousVariable.Text = "<";
            this.cmdPreviousVariable.UseVisualStyleBackColor = true;
            this.cmdPreviousVariable.Click += new System.EventHandler(this.cmdPreviousVariable_Click);
            // 
            // cmdNextVariable
            // 
            this.cmdNextVariable.Location = new System.Drawing.Point(298, 26);
            this.cmdNextVariable.Margin = new System.Windows.Forms.Padding(0);
            this.cmdNextVariable.Name = "cmdNextVariable";
            this.cmdNextVariable.Size = new System.Drawing.Size(21, 21);
            this.cmdNextVariable.TabIndex = 3;
            this.cmdNextVariable.Text = ">";
            this.cmdNextVariable.UseVisualStyleBackColor = true;
            this.cmdNextVariable.Click += new System.EventHandler(this.cmdNextVariable_Click);
            // 
            // chkShowRelativeFrequencies
            // 
            this.chkShowRelativeFrequencies.AutoSize = true;
            this.chkShowRelativeFrequencies.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkShowRelativeFrequencies.Location = new System.Drawing.Point(1, 198);
            this.chkShowRelativeFrequencies.Name = "chkShowRelativeFrequencies";
            this.chkShowRelativeFrequencies.Size = new System.Drawing.Size(151, 17);
            this.chkShowRelativeFrequencies.TabIndex = 11;
            this.chkShowRelativeFrequencies.Text = "Show relative frequencies:";
            this.chkShowRelativeFrequencies.UseVisualStyleBackColor = true;
            this.chkShowRelativeFrequencies.CheckedChanged += new System.EventHandler(this.chkShowRelativeFrequencies_CheckedChanged);
            // 
            // lblBinChoiceMethod
            // 
            this.lblBinChoiceMethod.AutoSize = true;
            this.lblBinChoiceMethod.Location = new System.Drawing.Point(37, 96);
            this.lblBinChoiceMethod.Name = "lblBinChoiceMethod";
            this.lblBinChoiceMethod.Size = new System.Drawing.Size(98, 13);
            this.lblBinChoiceMethod.TabIndex = 33;
            this.lblBinChoiceMethod.Text = "Bin choice method:";
            // 
            // cboBinChoiceMethod
            // 
            this.cboBinChoiceMethod.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboBinChoiceMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboBinChoiceMethod.FormattingEnabled = true;
            this.cboBinChoiceMethod.Items.AddRange(new object[] {
            "Doane",
            "Freedman-Daconis",
            "Shimazaki-Shinomoto",
            "Stata",
            "Sturges"});
            this.cboBinChoiceMethod.Location = new System.Drawing.Point(138, 93);
            this.cboBinChoiceMethod.Name = "cboBinChoiceMethod";
            this.cboBinChoiceMethod.Size = new System.Drawing.Size(181, 21);
            this.cboBinChoiceMethod.TabIndex = 4;
            // 
            // ctlHistogramOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.lblBinChoiceMethod);
            this.Controls.Add(this.cboBinChoiceMethod);
            this.Controls.Add(this.lblDataMinimum);
            this.Controls.Add(this.chkShowRelativeFrequencies);
            this.Controls.Add(this.cmdNextVariable);
            this.Controls.Add(this.cmdPreviousVariable);
            this.Controls.Add(this.lblVariable);
            this.Controls.Add(this.cboVariable);
            this.Controls.Add(this.chkPoolVariables);
            this.Controls.Add(this.chkOverlayNormalCurve);
            this.Controls.Add(this.lstBinValues);
            this.Controls.Add(this.cmdReset);
            this.Controls.Add(this.cmdAutoMidpoints);
            this.Controls.Add(this.cmdAutoBins);
            this.Controls.Add(this.txtMinimumMidpoint);
            this.Controls.Add(this.txtMidpointInterval);
            this.Controls.Add(this.lblDataMaximum);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtBins);
            this.Controls.Add(this.label1);
            this.Name = "ctlHistogramOptions";
            this.Size = new System.Drawing.Size(322, 418);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtBins;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label lblDataMinimum;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label lblDataMaximum;
        private System.Windows.Forms.TextBox txtMidpointInterval;
        private System.Windows.Forms.TextBox txtMinimumMidpoint;
        private System.Windows.Forms.Button cmdAutoBins;
        private System.Windows.Forms.Button cmdAutoMidpoints;
        private System.Windows.Forms.Button cmdReset;
        private System.Windows.Forms.ListView lstBinValues;
        private System.Windows.Forms.ColumnHeader colLow;
        private System.Windows.Forms.ColumnHeader colMidpoint;
        private System.Windows.Forms.ColumnHeader colHigh;
        private System.Windows.Forms.CheckBox chkOverlayNormalCurve;
        private System.Windows.Forms.CheckBox chkPoolVariables;
        private System.Windows.Forms.ComboBox cboVariable;
        private System.Windows.Forms.Label lblVariable;
        private System.Windows.Forms.Button cmdPreviousVariable;
        private System.Windows.Forms.Button cmdNextVariable;
        private System.Windows.Forms.CheckBox chkShowRelativeFrequencies;
        private System.Windows.Forms.Label lblBinChoiceMethod;
        private System.Windows.Forms.ComboBox cboBinChoiceMethod;
    }
}
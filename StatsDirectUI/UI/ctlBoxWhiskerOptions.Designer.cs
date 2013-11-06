namespace StatsDirect.UI
{
    partial class ctlBoxWhiskerOptions
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
            this.rdoMethodMQR = new System.Windows.Forms.RadioButton();
            this.rdoMethodMSDR = new System.Windows.Forms.RadioButton();
            this.rdoMethodMCIR = new System.Windows.Forms.RadioButton();
            this.grpMethod = new System.Windows.Forms.GroupBox();
            this.rdoMethodBowley = new System.Windows.Forms.RadioButton();
            this.rdoMethodSevenNumberSummary = new System.Windows.Forms.RadioButton();
            this.cboCco = new System.Windows.Forms.ComboBox();
            this.lblCIRange = new System.Windows.Forms.Label();
            this.chkMarkMeanAndMedian = new System.Windows.Forms.CheckBox();
            this.grpFences = new System.Windows.Forms.GroupBox();
            this.chkUseOuterFence = new System.Windows.Forms.CheckBox();
            this.chkUseInnerFence = new System.Windows.Forms.CheckBox();
            this.rdoMethodMSER = new System.Windows.Forms.RadioButton();
            this.grpMethod.SuspendLayout();
            this.grpFences.SuspendLayout();
            this.SuspendLayout();
            // 
            // rdoMethodMQR
            // 
            this.rdoMethodMQR.AutoSize = true;
            this.rdoMethodMQR.Location = new System.Drawing.Point(6, 19);
            this.rdoMethodMQR.Name = "rdoMethodMQR";
            this.rdoMethodMQR.Size = new System.Drawing.Size(260, 17);
            this.rdoMethodMQR.TabIndex = 0;
            this.rdoMethodMQR.TabStop = true;
            this.rdoMethodMQR.Text = "Median, quartiles and range (min, 25, 50, 75, max)";
            this.rdoMethodMQR.UseVisualStyleBackColor = true;
            this.rdoMethodMQR.CheckedChanged += new System.EventHandler(this.rdoMethod_CheckedChanged);
            // 
            // rdoMethodMSDR
            // 
            this.rdoMethodMSDR.AutoSize = true;
            this.rdoMethodMSDR.Location = new System.Drawing.Point(6, 39);
            this.rdoMethodMSDR.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.rdoMethodMSDR.Name = "rdoMethodMSDR";
            this.rdoMethodMSDR.Size = new System.Drawing.Size(196, 17);
            this.rdoMethodMSDR.TabIndex = 1;
            this.rdoMethodMSDR.TabStop = true;
            this.rdoMethodMSDR.Text = "Mean, standard deviation and range";
            this.rdoMethodMSDR.UseVisualStyleBackColor = true;
            this.rdoMethodMSDR.CheckedChanged += new System.EventHandler(this.rdoMethod_CheckedChanged);
            // 
            // rdoMethodMCIR
            // 
            this.rdoMethodMCIR.AutoSize = true;
            this.rdoMethodMCIR.Location = new System.Drawing.Point(6, 79);
            this.rdoMethodMCIR.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.rdoMethodMCIR.Name = "rdoMethodMCIR";
            this.rdoMethodMCIR.Size = new System.Drawing.Size(55, 17);
            this.rdoMethodMCIR.TabIndex = 3;
            this.rdoMethodMCIR.TabStop = true;
            this.rdoMethodMCIR.Text = "Mean,";
            this.rdoMethodMCIR.UseVisualStyleBackColor = true;
            this.rdoMethodMCIR.CheckedChanged += new System.EventHandler(this.rdoMethod_CheckedChanged);
            // 
            // grpMethod
            // 
            this.grpMethod.Controls.Add(this.rdoMethodMSER);
            this.grpMethod.Controls.Add(this.rdoMethodBowley);
            this.grpMethod.Controls.Add(this.rdoMethodSevenNumberSummary);
            this.grpMethod.Controls.Add(this.cboCco);
            this.grpMethod.Controls.Add(this.lblCIRange);
            this.grpMethod.Controls.Add(this.rdoMethodMQR);
            this.grpMethod.Controls.Add(this.rdoMethodMSDR);
            this.grpMethod.Controls.Add(this.rdoMethodMCIR);
            this.grpMethod.Location = new System.Drawing.Point(3, 3);
            this.grpMethod.Name = "grpMethod";
            this.grpMethod.Size = new System.Drawing.Size(276, 143);
            this.grpMethod.TabIndex = 19;
            this.grpMethod.TabStop = false;
            this.grpMethod.Text = "Type of plot";
            // 
            // rdoMethodBowley
            // 
            this.rdoMethodBowley.AutoSize = true;
            this.rdoMethodBowley.Location = new System.Drawing.Point(6, 119);
            this.rdoMethodBowley.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.rdoMethodBowley.Name = "rdoMethodBowley";
            this.rdoMethodBowley.Size = new System.Drawing.Size(243, 17);
            this.rdoMethodBowley.TabIndex = 6;
            this.rdoMethodBowley.TabStop = true;
            this.rdoMethodBowley.Text = "Bowley summary (min, 10, 25, 50, 75, 90, max)";
            this.rdoMethodBowley.UseVisualStyleBackColor = true;
            this.rdoMethodBowley.CheckedChanged += new System.EventHandler(this.rdoMethod_CheckedChanged);
            // 
            // rdoMethodSevenNumberSummary
            // 
            this.rdoMethodSevenNumberSummary.AutoSize = true;
            this.rdoMethodSevenNumberSummary.Location = new System.Drawing.Point(6, 99);
            this.rdoMethodSevenNumberSummary.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.rdoMethodSevenNumberSummary.Name = "rdoMethodSevenNumberSummary";
            this.rdoMethodSevenNumberSummary.Size = new System.Drawing.Size(255, 17);
            this.rdoMethodSevenNumberSummary.TabIndex = 5;
            this.rdoMethodSevenNumberSummary.TabStop = true;
            this.rdoMethodSevenNumberSummary.Text = "Seven number summary (2, 9, 25, 50, 75, 91, 98)";
            this.rdoMethodSevenNumberSummary.UseVisualStyleBackColor = true;
            this.rdoMethodSevenNumberSummary.CheckedChanged += new System.EventHandler(this.rdoMethod_CheckedChanged);
            // 
            // cboCco
            // 
            this.cboCco.Enabled = false;
            this.cboCco.FormattingEnabled = true;
            this.cboCco.Items.AddRange(new object[] {
            "99",
            "98",
            "97",
            "96",
            "95",
            "94",
            "93",
            "92",
            "91",
            "90"});
            this.cboCco.Location = new System.Drawing.Point(58, 78);
            this.cboCco.Name = "cboCco";
            this.cboCco.Size = new System.Drawing.Size(45, 21);
            this.cboCco.TabIndex = 4;
            this.cboCco.SelectedIndexChanged += new System.EventHandler(this.cboCco_SelectedIndexChanged);
            this.cboCco.TextUpdate += new System.EventHandler(this.cboCco_TextUpdate);
            // 
            // lblCIRange
            // 
            this.lblCIRange.AutoSize = true;
            this.lblCIRange.Location = new System.Drawing.Point(105, 81);
            this.lblCIRange.Name = "lblCIRange";
            this.lblCIRange.Size = new System.Drawing.Size(159, 13);
            this.lblCIRange.TabIndex = 11;
            this.lblCIRange.Text = "% confidence interval and range";
            // 
            // chkMarkMeanAndMedian
            // 
            this.chkMarkMeanAndMedian.AutoSize = true;
            this.chkMarkMeanAndMedian.Location = new System.Drawing.Point(9, 226);
            this.chkMarkMeanAndMedian.Name = "chkMarkMeanAndMedian";
            this.chkMarkMeanAndMedian.Size = new System.Drawing.Size(161, 17);
            this.chkMarkMeanAndMedian.TabIndex = 9;
            this.chkMarkMeanAndMedian.Text = "Mark both mean and median";
            this.chkMarkMeanAndMedian.UseVisualStyleBackColor = true;
            this.chkMarkMeanAndMedian.CheckedChanged += new System.EventHandler(this.chkMarkMeanAndMedian_CheckedChanged);
            // 
            // grpFences
            // 
            this.grpFences.Controls.Add(this.chkUseOuterFence);
            this.grpFences.Controls.Add(this.chkUseInnerFence);
            this.grpFences.Location = new System.Drawing.Point(3, 152);
            this.grpFences.Name = "grpFences";
            this.grpFences.Size = new System.Drawing.Size(276, 68);
            this.grpFences.TabIndex = 21;
            this.grpFences.TabStop = false;
            this.grpFences.Text = "Fences";
            // 
            // chkUseOuterFence
            // 
            this.chkUseOuterFence.AutoSize = true;
            this.chkUseOuterFence.Location = new System.Drawing.Point(7, 44);
            this.chkUseOuterFence.Name = "chkUseOuterFence";
            this.chkUseOuterFence.Size = new System.Drawing.Size(153, 17);
            this.chkUseOuterFence.TabIndex = 8;
            this.chkUseOuterFence.Text = "Use outer fence for outliers";
            this.chkUseOuterFence.UseVisualStyleBackColor = true;
            this.chkUseOuterFence.CheckedChanged += new System.EventHandler(this.chkUseOuterFence_CheckedChanged);
            // 
            // chkUseInnerFence
            // 
            this.chkUseInnerFence.AutoSize = true;
            this.chkUseInnerFence.Location = new System.Drawing.Point(7, 20);
            this.chkUseInnerFence.Name = "chkUseInnerFence";
            this.chkUseInnerFence.Size = new System.Drawing.Size(152, 17);
            this.chkUseInnerFence.TabIndex = 7;
            this.chkUseInnerFence.Text = "Use inner fence for outliers";
            this.chkUseInnerFence.UseVisualStyleBackColor = true;
            this.chkUseInnerFence.CheckedChanged += new System.EventHandler(this.chkUseInnerFence_CheckedChanged);
            // 
            // rdoMethodMSER
            // 
            this.rdoMethodMSER.AutoSize = true;
            this.rdoMethodMSER.Location = new System.Drawing.Point(6, 59);
            this.rdoMethodMSER.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.rdoMethodMSER.Name = "rdoMethodMSER";
            this.rdoMethodMSER.Size = new System.Drawing.Size(174, 17);
            this.rdoMethodMSER.TabIndex = 2;
            this.rdoMethodMSER.TabStop = true;
            this.rdoMethodMSER.Text = "Mean, standard error and range";
            this.rdoMethodMSER.UseVisualStyleBackColor = true;
            // 
            // ctlBoxWhiskerOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.chkMarkMeanAndMedian);
            this.Controls.Add(this.grpMethod);
            this.Controls.Add(this.grpFences);
            this.Name = "ctlBoxWhiskerOptions";
            this.Size = new System.Drawing.Size(282, 246);
            this.grpMethod.ResumeLayout(false);
            this.grpMethod.PerformLayout();
            this.grpFences.ResumeLayout(false);
            this.grpFences.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton rdoMethodMQR;
        private System.Windows.Forms.RadioButton rdoMethodMSDR;
        private System.Windows.Forms.RadioButton rdoMethodMCIR;
        private System.Windows.Forms.GroupBox grpMethod;
        private System.Windows.Forms.ComboBox cboCco;
        private System.Windows.Forms.Label lblCIRange;
        private System.Windows.Forms.RadioButton rdoMethodBowley;
        private System.Windows.Forms.RadioButton rdoMethodSevenNumberSummary;
        private System.Windows.Forms.CheckBox chkMarkMeanAndMedian;
        private System.Windows.Forms.GroupBox grpFences;
        private System.Windows.Forms.CheckBox chkUseOuterFence;
        private System.Windows.Forms.CheckBox chkUseInnerFence;
        private System.Windows.Forms.RadioButton rdoMethodMSER;
    }
}
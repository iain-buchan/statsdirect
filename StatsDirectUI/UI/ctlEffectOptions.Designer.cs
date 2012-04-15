namespace StatsDirect.UI
{
    partial class ctlEffectOptions
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.rdoTypeM = new System.Windows.Forms.RadioButton();
            this.rdoTypeG = new System.Windows.Forms.RadioButton();
            this.rdoTypeD = new System.Windows.Forms.RadioButton();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.rdoTypeM);
            this.groupBox1.Controls.Add(this.rdoTypeG);
            this.groupBox1.Controls.Add(this.rdoTypeD);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(263, 96);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Type of analysis";
            // 
            // rdoTypeM
            // 
            this.rdoTypeM.AutoSize = true;
            this.rdoTypeM.Location = new System.Drawing.Point(6, 67);
            this.rdoTypeM.Name = "rdoTypeM";
            this.rdoTypeM.Size = new System.Drawing.Size(249, 17);
            this.rdoTypeM.TabIndex = 2;
            this.rdoTypeM.Text = "Weighted mean difference from mean, n and sd";
            this.rdoTypeM.UseVisualStyleBackColor = true;
            // 
            // rdoTypeG
            // 
            this.rdoTypeG.AutoSize = true;
            this.rdoTypeG.Location = new System.Drawing.Point(6, 44);
            this.rdoTypeG.Name = "rdoTypeG";
            this.rdoTypeG.Size = new System.Drawing.Size(215, 17);
            this.rdoTypeG.TabIndex = 1;
            this.rdoTypeG.Text = "Standardized effect size (d) from g and n";
            this.rdoTypeG.UseVisualStyleBackColor = true;
            // 
            // rdoTypeD
            // 
            this.rdoTypeD.AutoSize = true;
            this.rdoTypeD.Checked = true;
            this.rdoTypeD.Location = new System.Drawing.Point(6, 19);
            this.rdoTypeD.Name = "rdoTypeD";
            this.rdoTypeD.Size = new System.Drawing.Size(252, 17);
            this.rdoTypeD.TabIndex = 0;
            this.rdoTypeD.TabStop = true;
            this.rdoTypeD.Text = "Standardized effect size (d) from mean, n and sd";
            this.rdoTypeD.UseVisualStyleBackColor = true;
            // 
            // ctlEffectOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox1);
            this.Name = "ctlEffectOptions";
            this.Size = new System.Drawing.Size(272, 104);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton rdoTypeM;
        private System.Windows.Forms.RadioButton rdoTypeG;
        private System.Windows.Forms.RadioButton rdoTypeD;
    }
}
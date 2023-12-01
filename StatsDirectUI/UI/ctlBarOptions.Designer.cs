namespace StatsDirect.UI
{
    partial class ctlBarOptions
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
            this.rdoTypeClustered = new System.Windows.Forms.RadioButton();
            this.rdoTypeStacked = new System.Windows.Forms.RadioButton();
            this.rdoTypeStacked100 = new System.Windows.Forms.RadioButton();
            this.grpMethod = new System.Windows.Forms.GroupBox();
            this.grpMethod.SuspendLayout();
            this.SuspendLayout();
            // 
            // rdoTypeClustered
            // 
            this.rdoTypeClustered.AutoSize = true;
            this.rdoTypeClustered.Location = new System.Drawing.Point(6, 19);
            this.rdoTypeClustered.Name = "rdoTypeClustered";
            this.rdoTypeClustered.Size = new System.Drawing.Size(69, 17);
            this.rdoTypeClustered.TabIndex = 8;
            this.rdoTypeClustered.TabStop = true;
            this.rdoTypeClustered.Text = "Clustered";
            this.rdoTypeClustered.UseVisualStyleBackColor = true;
            this.rdoTypeClustered.CheckedChanged += new System.EventHandler(this.rdoType_CheckedChanged);
            // 
            // rdoTypeStacked
            // 
            this.rdoTypeStacked.AutoSize = true;
            this.rdoTypeStacked.Location = new System.Drawing.Point(6, 39);
            this.rdoTypeStacked.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.rdoTypeStacked.Name = "rdoTypeStacked";
            this.rdoTypeStacked.Size = new System.Drawing.Size(65, 17);
            this.rdoTypeStacked.TabIndex = 9;
            this.rdoTypeStacked.TabStop = true;
            this.rdoTypeStacked.Text = "Stacked";
            this.rdoTypeStacked.UseVisualStyleBackColor = true;
            this.rdoTypeStacked.CheckedChanged += new System.EventHandler(this.rdoType_CheckedChanged);
            // 
            // rdoTypeStacked100
            // 
            this.rdoTypeStacked100.AutoSize = true;
            this.rdoTypeStacked100.Location = new System.Drawing.Point(6, 59);
            this.rdoTypeStacked100.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.rdoTypeStacked100.Name = "rdoTypeStacked100";
            this.rdoTypeStacked100.Size = new System.Drawing.Size(92, 17);
            this.rdoTypeStacked100.TabIndex = 10;
            this.rdoTypeStacked100.TabStop = true;
            this.rdoTypeStacked100.Text = "100% stacked";
            this.rdoTypeStacked100.UseVisualStyleBackColor = true;
            this.rdoTypeStacked100.CheckedChanged += new System.EventHandler(this.rdoType_CheckedChanged);
            // 
            // grpMethod
            // 
            this.grpMethod.Controls.Add(this.rdoTypeClustered);
            this.grpMethod.Controls.Add(this.rdoTypeStacked);
            this.grpMethod.Controls.Add(this.rdoTypeStacked100);
            this.grpMethod.Location = new System.Drawing.Point(3, 3);
            this.grpMethod.Name = "grpMethod";
            this.grpMethod.Size = new System.Drawing.Size(254, 84);
            this.grpMethod.TabIndex = 19;
            this.grpMethod.TabStop = false;
            this.grpMethod.Text = "Type of plot";
            // 
            // ctlBarOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.grpMethod);
            this.Name = "ctlBarOptions";
            this.Size = new System.Drawing.Size(260, 90);
            this.grpMethod.ResumeLayout(false);
            this.grpMethod.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RadioButton rdoTypeClustered;
        private System.Windows.Forms.RadioButton rdoTypeStacked;
        private System.Windows.Forms.RadioButton rdoTypeStacked100;
        private System.Windows.Forms.GroupBox grpMethod;
    }
}
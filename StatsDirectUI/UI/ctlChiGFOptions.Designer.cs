namespace StatsDirect.UI
{
    partial class ctlChiGFOptions
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
            this.txtDegreesOfFreedom = new System.Windows.Forms.TextBox();
            this.lblDegreesOfFreedom = new System.Windows.Forms.Label();
            this.lblCategories = new System.Windows.Forms.Label();
            this.lblN = new System.Windows.Forms.Label();
            this.lblFrequencies = new System.Windows.Forms.Label();
            this.colValue = new System.Windows.Forms.ColumnHeader();
            this.colObserved = new System.Windows.Forms.ColumnHeader();
            this.colExpected = new System.Windows.Forms.ColumnHeader();
            this.lstFrequencies = new System.Windows.Forms.ListView();
            this.txtValue = new System.Windows.Forms.TextBox();
            this.lblInstructions = new System.Windows.Forms.Label();
            this.rdoExpected = new System.Windows.Forms.RadioButton();
            this.rdoN = new System.Windows.Forms.RadioButton();
            this.lblEnteredValuesAre = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtDegreesOfFreedom
            // 
            this.txtDegreesOfFreedom.Location = new System.Drawing.Point(106, 4);
            this.txtDegreesOfFreedom.Name = "txtDegreesOfFreedom";
            this.txtDegreesOfFreedom.Size = new System.Drawing.Size(51, 20);
            this.txtDegreesOfFreedom.TabIndex = 2;
            // 
            // lblDegreesOfFreedom
            // 
            this.lblDegreesOfFreedom.AutoSize = true;
            this.lblDegreesOfFreedom.Location = new System.Drawing.Point(0, 7);
            this.lblDegreesOfFreedom.Name = "lblDegreesOfFreedom";
            this.lblDegreesOfFreedom.Size = new System.Drawing.Size(100, 13);
            this.lblDegreesOfFreedom.TabIndex = 3;
            this.lblDegreesOfFreedom.Text = "Degrees of freedom";
            // 
            // lblCategories
            // 
            this.lblCategories.AutoSize = true;
            this.lblCategories.Location = new System.Drawing.Point(216, 10);
            this.lblCategories.Name = "lblCategories";
            this.lblCategories.Size = new System.Drawing.Size(66, 13);
            this.lblCategories.TabIndex = 4;
            this.lblCategories.Text = "Categories =";
            // 
            // lblN
            // 
            this.lblN.AutoSize = true;
            this.lblN.Location = new System.Drawing.Point(219, 27);
            this.lblN.Name = "lblN";
            this.lblN.Size = new System.Drawing.Size(22, 13);
            this.lblN.TabIndex = 5;
            this.lblN.Text = "n =";
            // 
            // lblFrequencies
            // 
            this.lblFrequencies.AutoSize = true;
            this.lblFrequencies.Location = new System.Drawing.Point(4, 36);
            this.lblFrequencies.Name = "lblFrequencies";
            this.lblFrequencies.Size = new System.Drawing.Size(65, 13);
            this.lblFrequencies.TabIndex = 8;
            this.lblFrequencies.Text = "Frequencies";
            // 
            // colValue
            // 
            this.colValue.Text = "Value";
            this.colValue.Width = 120;
            // 
            // colObserved
            // 
            this.colObserved.Text = "Observed";
            this.colObserved.Width = 120;
            // 
            // colExpected
            // 
            this.colExpected.Text = "Expected";
            this.colExpected.Width = 120;
            // 
            // lstFrequencies
            // 
            this.lstFrequencies.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lstFrequencies.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colValue,
            this.colObserved,
            this.colExpected});
            this.lstFrequencies.FullRowSelect = true;
            this.lstFrequencies.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lstFrequencies.HideSelection = false;
            this.lstFrequencies.Location = new System.Drawing.Point(3, 55);
            this.lstFrequencies.MultiSelect = false;
            this.lstFrequencies.Name = "lstFrequencies";
            this.lstFrequencies.Size = new System.Drawing.Size(386, 184);
            this.lstFrequencies.TabIndex = 6;
            this.lstFrequencies.UseCompatibleStateImageBehavior = false;
            this.lstFrequencies.View = System.Windows.Forms.View.Details;
            this.lstFrequencies.MouseUp += new System.Windows.Forms.MouseEventHandler(this.lstFrequencies_MouseUp);
            this.lstFrequencies.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.lstFrequencies_KeyPress);
            // 
            // txtValue
            // 
            this.txtValue.Location = new System.Drawing.Point(243, 4);
            this.txtValue.Name = "txtValue";
            this.txtValue.Size = new System.Drawing.Size(129, 20);
            this.txtValue.TabIndex = 9;
            this.txtValue.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtValue_KeyPress);
            this.txtValue.Enter += new System.EventHandler(this.txtValue_Enter);
            // 
            // lblInstructions
            // 
            this.lblInstructions.AutoSize = true;
            this.lblInstructions.Location = new System.Drawing.Point(12, 4);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(225, 26);
            this.lblInstructions.TabIndex = 10;
            this.lblInstructions.Text = "To change an expected value: select the row,\r\nenter the new value here, then pres" +
                "s Enter";
            this.lblInstructions.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // rdoExpected
            // 
            this.rdoExpected.AutoSize = true;
            this.rdoExpected.Checked = true;
            this.rdoExpected.Location = new System.Drawing.Point(117, 41);
            this.rdoExpected.Name = "rdoExpected";
            this.rdoExpected.Size = new System.Drawing.Size(120, 17);
            this.rdoExpected.TabIndex = 0;
            this.rdoExpected.TabStop = true;
            this.rdoExpected.Text = "Expected frequency";
            this.rdoExpected.UseVisualStyleBackColor = true;
            this.rdoExpected.Click += new System.EventHandler(this.rdoExpected_Click);
            // 
            // rdoN
            // 
            this.rdoN.AutoSize = true;
            this.rdoN.Location = new System.Drawing.Point(243, 41);
            this.rdoN.Name = "rdoN";
            this.rdoN.Size = new System.Drawing.Size(94, 17);
            this.rdoN.TabIndex = 1;
            this.rdoN.Text = "Proportion of n";
            this.rdoN.UseVisualStyleBackColor = true;
            this.rdoN.Click += new System.EventHandler(this.rdoN_Click);
            // 
            // lblEnteredValuesAre
            // 
            this.lblEnteredValuesAre.AutoSize = true;
            this.lblEnteredValuesAre.Location = new System.Drawing.Point(12, 43);
            this.lblEnteredValuesAre.Name = "lblEnteredValuesAre";
            this.lblEnteredValuesAre.Size = new System.Drawing.Size(99, 13);
            this.lblEnteredValuesAre.TabIndex = 11;
            this.lblEnteredValuesAre.Text = "Entered values are:";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblEnteredValuesAre);
            this.panel1.Controls.Add(this.rdoN);
            this.panel1.Controls.Add(this.lblInstructions);
            this.panel1.Controls.Add(this.rdoExpected);
            this.panel1.Controls.Add(this.txtValue);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 245);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(392, 73);
            this.panel1.TabIndex = 12;
            // 
            // ctlChiGFOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.lblFrequencies);
            this.Controls.Add(this.lstFrequencies);
            this.Controls.Add(this.lblN);
            this.Controls.Add(this.lblCategories);
            this.Controls.Add(this.lblDegreesOfFreedom);
            this.Controls.Add(this.txtDegreesOfFreedom);
            this.MinimumSize = new System.Drawing.Size(392, 280);
            this.Name = "ctlChiGFOptions";
            this.Size = new System.Drawing.Size(392, 318);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtDegreesOfFreedom;
        private System.Windows.Forms.Label lblDegreesOfFreedom;
        private System.Windows.Forms.Label lblCategories;
        private System.Windows.Forms.Label lblN;
        private System.Windows.Forms.Label lblFrequencies;
        private System.Windows.Forms.ColumnHeader colValue;
        private System.Windows.Forms.ColumnHeader colObserved;
        private System.Windows.Forms.ColumnHeader colExpected;
        private System.Windows.Forms.ListView lstFrequencies;
        private System.Windows.Forms.TextBox txtValue;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.RadioButton rdoExpected;
        private System.Windows.Forms.RadioButton rdoN;
        private System.Windows.Forms.Label lblEnteredValuesAre;
        private System.Windows.Forms.Panel panel1;
    }
}
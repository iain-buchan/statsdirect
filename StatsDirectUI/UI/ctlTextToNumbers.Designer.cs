namespace StatsDirect.UI
{
    partial class ctlTextToNumbers
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
            this.gridNumbers = new System.Windows.Forms.DataGridView();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chkIgnoreTitle = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.gridNumbers)).BeginInit();
            this.SuspendLayout();
            // 
            // gridNumbers
            // 
            this.gridNumbers.AllowUserToAddRows = false;
            this.gridNumbers.AllowUserToDeleteRows = false;
            this.gridNumbers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridNumbers.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column1,
            this.Column2});
            this.gridNumbers.Location = new System.Drawing.Point(0, 23);
            this.gridNumbers.Margin = new System.Windows.Forms.Padding(0);
            this.gridNumbers.Name = "gridNumbers";
            this.gridNumbers.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridNumbers.Size = new System.Drawing.Size(413, 144);
            this.gridNumbers.TabIndex = 0;
            // 
            // Column1
            // 
            this.Column1.HeaderText = "Text";
            this.Column1.Name = "Column1";
            this.Column1.ReadOnly = true;
            this.Column1.Width = 250;
            // 
            // Column2
            // 
            this.Column2.HeaderText = "Number Code";
            this.Column2.Name = "Column2";
            this.Column2.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // chkIgnoreTitle
            // 
            this.chkIgnoreTitle.AutoSize = true;
            this.chkIgnoreTitle.Checked = true;
            this.chkIgnoreTitle.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkIgnoreTitle.Location = new System.Drawing.Point(0, 4);
            this.chkIgnoreTitle.Name = "chkIgnoreTitle";
            this.chkIgnoreTitle.Size = new System.Drawing.Size(175, 17);
            this.chkIgnoreTitle.TabIndex = 1;
            this.chkIgnoreTitle.Text = "Use top row as column heading";
            this.chkIgnoreTitle.UseVisualStyleBackColor = true;
            this.chkIgnoreTitle.CheckedChanged += new System.EventHandler(this.chkIgnoreTitle_CheckedChanged);
            // 
            // ctlTextToNumbers
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.chkIgnoreTitle);
            this.Controls.Add(this.gridNumbers);
            this.Name = "ctlTextToNumbers";
            this.Size = new System.Drawing.Size(413, 167);
            ((System.ComponentModel.ISupportInitialize)(this.gridNumbers)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView gridNumbers;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.CheckBox chkIgnoreTitle;
    }
}
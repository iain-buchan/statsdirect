namespace StatsDirect.UI
{
    partial class ctlScores
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
            this.gridScores = new System.Windows.Forms.DataGridView();
            this.ColumnScores = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RowScores = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.gridScores)).BeginInit();
            this.SuspendLayout();
            // 
            // gridScores
            // 
            this.gridScores.AllowUserToAddRows = false;
            this.gridScores.AllowUserToDeleteRows = false;
            this.gridScores.AllowUserToResizeRows = false;
            this.gridScores.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridScores.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnScores,
            this.RowScores});
            this.gridScores.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridScores.Location = new System.Drawing.Point(0, 0);
            this.gridScores.Name = "gridScores";
            this.gridScores.Size = new System.Drawing.Size(371, 181);
            this.gridScores.TabIndex = 4;
            // 
            // ColumnScores
            // 
            this.ColumnScores.HeaderText = "Column Scores";
            this.ColumnScores.Name = "ColumnScores";
            this.ColumnScores.Width = 150;
            // 
            // RowScores
            // 
            this.RowScores.HeaderText = "Row Scores";
            this.RowScores.Name = "RowScores";
            this.RowScores.Width = 150;
            // 
            // ctlScores
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.gridScores);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "ctlScores";
            this.Size = new System.Drawing.Size(371, 181);
            ((System.ComponentModel.ISupportInitialize)(this.gridScores)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView gridScores;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnScores;
        private System.Windows.Forms.DataGridViewTextBoxColumn RowScores;
    }
}
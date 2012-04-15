namespace StatsDirect.UI
{
    partial class ctlOptions
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.pnlCombo = new System.Windows.Forms.Panel();
            this.cbo1 = new System.Windows.Forms.ComboBox();
            this.lbl1 = new System.Windows.Forms.Label();
            this.cbo0 = new System.Windows.Forms.ComboBox();
            this.lbl0 = new System.Windows.Forms.Label();
            this.pnlCheck = new System.Windows.Forms.Panel();
            this.tableLayoutPanel1.SuspendLayout();
            this.pnlCombo.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.pnlCombo, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.pnlCheck, 0, 1);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(333, 57);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // pnlCombo
            // 
            this.pnlCombo.AutoSize = true;
            this.pnlCombo.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlCombo.Controls.Add(this.cbo1);
            this.pnlCombo.Controls.Add(this.lbl1);
            this.pnlCombo.Controls.Add(this.cbo0);
            this.pnlCombo.Controls.Add(this.lbl0);
            this.pnlCombo.Location = new System.Drawing.Point(3, 9);
            this.pnlCombo.Name = "pnlCombo";
            this.pnlCombo.Size = new System.Drawing.Size(327, 45);
            this.pnlCombo.TabIndex = 2;
            // 
            // cbo1
            // 
            this.cbo1.FormattingEnabled = true;
            this.cbo1.Location = new System.Drawing.Point(170, 21);
            this.cbo1.Name = "cbo1";
            this.cbo1.Size = new System.Drawing.Size(154, 21);
            this.cbo1.TabIndex = 3;
            // 
            // lbl1
            // 
            this.lbl1.AutoSize = true;
            this.lbl1.Location = new System.Drawing.Point(170, 4);
            this.lbl1.Name = "lbl1";
            this.lbl1.Size = new System.Drawing.Size(0, 13);
            this.lbl1.TabIndex = 2;
            // 
            // cbo0
            // 
            this.cbo0.FormattingEnabled = true;
            this.cbo0.Location = new System.Drawing.Point(10, 21);
            this.cbo0.Name = "cbo0";
            this.cbo0.Size = new System.Drawing.Size(154, 21);
            this.cbo0.TabIndex = 1;
            // 
            // lbl0
            // 
            this.lbl0.AutoSize = true;
            this.lbl0.Location = new System.Drawing.Point(10, 4);
            this.lbl0.Name = "lbl0";
            this.lbl0.Size = new System.Drawing.Size(0, 13);
            this.lbl0.TabIndex = 0;
            // 
            // pnlCheck
            // 
            this.pnlCheck.AutoSize = true;
            this.pnlCheck.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlCheck.Location = new System.Drawing.Point(3, 3);
            this.pnlCheck.Name = "pnlCheck";
            this.pnlCheck.Size = new System.Drawing.Size(0, 0);
            this.pnlCheck.TabIndex = 3;
            // 
            // ctlOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ctlOptions";
            this.Size = new System.Drawing.Size(333, 57);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.pnlCombo.ResumeLayout(false);
            this.pnlCombo.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel pnlCombo;
        private System.Windows.Forms.ComboBox cbo1;
        private System.Windows.Forms.Label lbl1;
        private System.Windows.Forms.ComboBox cbo0;
        private System.Windows.Forms.Label lbl0;
        private System.Windows.Forms.Panel pnlCheck;
    }
}
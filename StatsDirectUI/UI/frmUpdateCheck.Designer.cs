namespace StatsDirect.UI
{
    partial class frmUpdateCheck
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmUpdateCheck));
            this.cmdClose = new System.Windows.Forms.Button();
            this.ctlUpdateRCheck1 = new StatsDirect.UI.ctlUpdateRCheck();
            this.ctlUpdateStatsDirectCheck1 = new StatsDirect.UI.ctlUpdateStatsDirectCheck();
            this.tlpOuter = new System.Windows.Forms.TableLayoutPanel();
            this.pnlClose = new System.Windows.Forms.Panel();
            this.tlpOuter.SuspendLayout();
            this.pnlClose.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdClose
            // 
            this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdClose.Location = new System.Drawing.Point(183, 3);
            this.cmdClose.Name = "cmdClose";
            this.cmdClose.Size = new System.Drawing.Size(75, 23);
            this.cmdClose.TabIndex = 1;
            this.cmdClose.Text = "&Close";
            this.cmdClose.UseVisualStyleBackColor = true;
            this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
            // 
            // ctlUpdateRCheck1
            // 
            this.ctlUpdateRCheck1.AutoSize = true;
            this.ctlUpdateRCheck1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlUpdateRCheck1.Location = new System.Drawing.Point(3, 134);
            this.ctlUpdateRCheck1.Name = "ctlUpdateRCheck1";
            this.ctlUpdateRCheck1.Size = new System.Drawing.Size(261, 125);
            this.ctlUpdateRCheck1.TabIndex = 6;
            // 
            // ctlUpdateStatsDirectCheck1
            // 
            this.ctlUpdateStatsDirectCheck1.AutoSize = true;
            this.ctlUpdateStatsDirectCheck1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlUpdateStatsDirectCheck1.Location = new System.Drawing.Point(3, 3);
            this.ctlUpdateStatsDirectCheck1.Name = "ctlUpdateStatsDirectCheck1";
            this.ctlUpdateStatsDirectCheck1.Size = new System.Drawing.Size(261, 125);
            this.ctlUpdateStatsDirectCheck1.TabIndex = 7;
            // 
            // tlpOuter
            // 
            this.tlpOuter.AutoSize = true;
            this.tlpOuter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpOuter.ColumnCount = 1;
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.Controls.Add(this.pnlClose, 0, 2);
            this.tlpOuter.Controls.Add(this.ctlUpdateStatsDirectCheck1, 0, 0);
            this.tlpOuter.Controls.Add(this.ctlUpdateRCheck1, 0, 1);
            this.tlpOuter.Location = new System.Drawing.Point(12, 12);
            this.tlpOuter.Margin = new System.Windows.Forms.Padding(12);
            this.tlpOuter.Name = "tlpOuter";
            this.tlpOuter.RowCount = 3;
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOuter.Size = new System.Drawing.Size(267, 297);
            this.tlpOuter.TabIndex = 8;
            // 
            // pnlClose
            // 
            this.pnlClose.AutoSize = true;
            this.pnlClose.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlClose.Controls.Add(this.cmdClose);
            this.pnlClose.Location = new System.Drawing.Point(3, 265);
            this.pnlClose.Name = "pnlClose";
            this.pnlClose.Size = new System.Drawing.Size(261, 29);
            this.pnlClose.TabIndex = 0;
            // 
            // frmUpdateCheck
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.cmdClose;
            this.ClientSize = new System.Drawing.Size(292, 320);
            this.Controls.Add(this.tlpOuter);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmUpdateCheck";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Check for updates";
            this.UseWaitCursor = true;
            this.tlpOuter.ResumeLayout(false);
            this.tlpOuter.PerformLayout();
            this.pnlClose.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdClose;
        private ctlUpdateRCheck ctlUpdateRCheck1;
        private ctlUpdateStatsDirectCheck ctlUpdateStatsDirectCheck1;
        private System.Windows.Forms.TableLayoutPanel tlpOuter;
        private System.Windows.Forms.Panel pnlClose;
    }
}
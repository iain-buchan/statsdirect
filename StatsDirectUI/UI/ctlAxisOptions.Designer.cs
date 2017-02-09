namespace StatsDirect.UI
{
    partial class ctlAxisOptions
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabAxis = new System.Windows.Forms.TabControl();
            this.tabX = new System.Windows.Forms.TabPage();
            this.tabY = new System.Windows.Forms.TabPage();
            this.ctlOneAxisOptionsX = new StatsDirect.UI.ctlOneAxisOptions();
            this.ctlOneAxisOptionsY = new StatsDirect.UI.ctlOneAxisOptions();
            this.tabAxis.SuspendLayout();
            this.tabX.SuspendLayout();
            this.tabY.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabAxis
            // 
            this.tabAxis.Controls.Add(this.tabX);
            this.tabAxis.Controls.Add(this.tabY);
            this.tabAxis.Location = new System.Drawing.Point(0, 0);
            this.tabAxis.Margin = new System.Windows.Forms.Padding(0);
            this.tabAxis.Name = "tabAxis";
            this.tabAxis.SelectedIndex = 0;
            this.tabAxis.Size = new System.Drawing.Size(240, 276);
            this.tabAxis.TabIndex = 0;
            // 
            // tabX
            // 
            this.tabX.BackColor = System.Drawing.SystemColors.Control;
            this.tabX.Controls.Add(this.ctlOneAxisOptionsX);
            this.tabX.Location = new System.Drawing.Point(4, 22);
            this.tabX.Margin = new System.Windows.Forms.Padding(0);
            this.tabX.Name = "tabX";
            this.tabX.Size = new System.Drawing.Size(232, 250);
            this.tabX.TabIndex = 0;
            this.tabX.Text = "X";
            // 
            // tabY
            // 
            this.tabY.BackColor = System.Drawing.SystemColors.Control;
            this.tabY.Controls.Add(this.ctlOneAxisOptionsY);
            this.tabY.Location = new System.Drawing.Point(4, 22);
            this.tabY.Margin = new System.Windows.Forms.Padding(0);
            this.tabY.Name = "tabY";
            this.tabY.Size = new System.Drawing.Size(232, 272);
            this.tabY.TabIndex = 1;
            this.tabY.Text = "Y";
            // 
            // ctlOneAxisOptionsX
            // 
            this.ctlOneAxisOptionsX.AutoSize = true;
            this.ctlOneAxisOptionsX.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlOneAxisOptionsX.IsYAxis = false;
            this.ctlOneAxisOptionsX.Location = new System.Drawing.Point(0, 0);
            this.ctlOneAxisOptionsX.Margin = new System.Windows.Forms.Padding(0);
            this.ctlOneAxisOptionsX.Name = "ctlOneAxisOptionsX";
            this.ctlOneAxisOptionsX.Size = new System.Drawing.Size(234, 249);
            this.ctlOneAxisOptionsX.TabIndex = 0;
            this.ctlOneAxisOptionsX.Title = string.Empty;
            // 
            // ctlOneAxisOptionsY
            // 
            this.ctlOneAxisOptionsY.AutoSize = true;
            this.ctlOneAxisOptionsY.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlOneAxisOptionsX.IsYAxis = true;
            this.ctlOneAxisOptionsY.Location = new System.Drawing.Point(0, 0);
            this.ctlOneAxisOptionsY.Margin = new System.Windows.Forms.Padding(0);
            this.ctlOneAxisOptionsY.Name = "ctlOneAxisOptionsY";
            this.ctlOneAxisOptionsY.Size = new System.Drawing.Size(234, 249);
            this.ctlOneAxisOptionsY.TabIndex = 0;
            this.ctlOneAxisOptionsY.Title = string.Empty;
            // 
            // ctlAxisOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tabAxis);
            this.Name = "ctlAxisOptions";
            this.Size = new System.Drawing.Size(240, 276);
            this.tabAxis.ResumeLayout(false);
            this.tabX.ResumeLayout(false);
            this.tabX.PerformLayout();
            this.tabY.ResumeLayout(false);
            this.tabY.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabAxis;
        private System.Windows.Forms.TabPage tabX;
        private System.Windows.Forms.TabPage tabY;
        private ctlOneAxisOptions ctlOneAxisOptionsX;
        private ctlOneAxisOptions ctlOneAxisOptionsY;
    }
}

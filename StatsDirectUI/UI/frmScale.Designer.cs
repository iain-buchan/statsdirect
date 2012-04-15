namespace StatsDirect.UI
{
    partial class frmScale
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmScale));
            this.cmdOk = new System.Windows.Forms.Button();
            this.chkRequestLimits = new System.Windows.Forms.CheckBox();
            this.grpYAxis = new System.Windows.Forms.GroupBox();
            this.lblYScale = new System.Windows.Forms.Label();
            this.lstYScale = new System.Windows.Forms.ListView();
            this.lblYLowerLimit = new System.Windows.Forms.Label();
            this.lblYUpperLimit = new System.Windows.Forms.Label();
            this.txtYLowerLimit = new System.Windows.Forms.TextBox();
            this.txtYUpperLimit = new System.Windows.Forms.TextBox();
            this.grpXAxis = new System.Windows.Forms.GroupBox();
            this.lblXScale = new System.Windows.Forms.Label();
            this.lstXScale = new System.Windows.Forms.ListView();
            this.lblXLowerLimit = new System.Windows.Forms.Label();
            this.lblXUpperLimit = new System.Windows.Forms.Label();
            this.txtXLowerLimit = new System.Windows.Forms.TextBox();
            this.txtXUpperLimit = new System.Windows.Forms.TextBox();
            this.colX = new System.Windows.Forms.ColumnHeader();
            this.colY = new System.Windows.Forms.ColumnHeader();
            this.grpYAxis.SuspendLayout();
            this.grpXAxis.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdOk
            // 
            this.cmdOk.Location = new System.Drawing.Point(306, 13);
            this.cmdOk.Name = "cmdOk";
            this.cmdOk.Size = new System.Drawing.Size(75, 23);
            this.cmdOk.TabIndex = 3;
            this.cmdOk.Text = "OK";
            this.cmdOk.UseVisualStyleBackColor = true;
            this.cmdOk.Click += new System.EventHandler(this.cmdOk_Click);
            // 
            // chkRequestLimits
            // 
            this.chkRequestLimits.AutoSize = true;
            this.chkRequestLimits.Location = new System.Drawing.Point(12, 17);
            this.chkRequestLimits.Name = "chkRequestLimits";
            this.chkRequestLimits.Size = new System.Drawing.Size(163, 17);
            this.chkRequestLimits.TabIndex = 0;
            this.chkRequestLimits.Text = "Request limits for each graph";
            this.chkRequestLimits.UseVisualStyleBackColor = true;
            // 
            // grpYAxis
            // 
            this.grpYAxis.Controls.Add(this.lblYScale);
            this.grpYAxis.Controls.Add(this.lstYScale);
            this.grpYAxis.Controls.Add(this.lblYLowerLimit);
            this.grpYAxis.Controls.Add(this.lblYUpperLimit);
            this.grpYAxis.Controls.Add(this.txtYLowerLimit);
            this.grpYAxis.Controls.Add(this.txtYUpperLimit);
            this.grpYAxis.Location = new System.Drawing.Point(12, 40);
            this.grpYAxis.Name = "grpYAxis";
            this.grpYAxis.Size = new System.Drawing.Size(369, 117);
            this.grpYAxis.TabIndex = 1;
            this.grpYAxis.TabStop = false;
            this.grpYAxis.Text = "Y (vertical) axis";
            // 
            // lblYScale
            // 
            this.lblYScale.AutoSize = true;
            this.lblYScale.Location = new System.Drawing.Point(175, 16);
            this.lblYScale.Name = "lblYScale";
            this.lblYScale.Size = new System.Drawing.Size(82, 13);
            this.lblYScale.TabIndex = 5;
            this.lblYScale.Text = "Resulting scale:";
            // 
            // lstYScale
            // 
            this.lstYScale.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colY});
            this.lstYScale.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lstYScale.Location = new System.Drawing.Point(178, 32);
            this.lstYScale.Name = "lstYScale";
            this.lstYScale.Size = new System.Drawing.Size(185, 75);
            this.lstYScale.TabIndex = 4;
            this.lstYScale.TabStop = false;
            this.lstYScale.UseCompatibleStateImageBehavior = false;
            this.lstYScale.View = System.Windows.Forms.View.Details;
            // 
            // lblYLowerLimit
            // 
            this.lblYLowerLimit.AutoSize = true;
            this.lblYLowerLimit.Location = new System.Drawing.Point(10, 62);
            this.lblYLowerLimit.Name = "lblYLowerLimit";
            this.lblYLowerLimit.Size = new System.Drawing.Size(56, 13);
            this.lblYLowerLimit.TabIndex = 3;
            this.lblYLowerLimit.Text = "Lower limit";
            // 
            // lblYUpperLimit
            // 
            this.lblYUpperLimit.AutoSize = true;
            this.lblYUpperLimit.Location = new System.Drawing.Point(10, 35);
            this.lblYUpperLimit.Name = "lblYUpperLimit";
            this.lblYUpperLimit.Size = new System.Drawing.Size(56, 13);
            this.lblYUpperLimit.TabIndex = 2;
            this.lblYUpperLimit.Text = "Upper limit";
            // 
            // txtYLowerLimit
            // 
            this.txtYLowerLimit.Location = new System.Drawing.Point(72, 59);
            this.txtYLowerLimit.Name = "txtYLowerLimit";
            this.txtYLowerLimit.Size = new System.Drawing.Size(100, 20);
            this.txtYLowerLimit.TabIndex = 1;
            this.txtYLowerLimit.TextChanged += new System.EventHandler(this.txtYLowerLimit_TextChanged);
            // 
            // txtYUpperLimit
            // 
            this.txtYUpperLimit.Location = new System.Drawing.Point(72, 32);
            this.txtYUpperLimit.Name = "txtYUpperLimit";
            this.txtYUpperLimit.Size = new System.Drawing.Size(100, 20);
            this.txtYUpperLimit.TabIndex = 0;
            this.txtYUpperLimit.TextChanged += new System.EventHandler(this.txtYUpperLimit_TextChanged);
            // 
            // grpXAxis
            // 
            this.grpXAxis.Controls.Add(this.lblXScale);
            this.grpXAxis.Controls.Add(this.lstXScale);
            this.grpXAxis.Controls.Add(this.lblXLowerLimit);
            this.grpXAxis.Controls.Add(this.lblXUpperLimit);
            this.grpXAxis.Controls.Add(this.txtXLowerLimit);
            this.grpXAxis.Controls.Add(this.txtXUpperLimit);
            this.grpXAxis.Location = new System.Drawing.Point(12, 163);
            this.grpXAxis.Name = "grpXAxis";
            this.grpXAxis.Size = new System.Drawing.Size(369, 117);
            this.grpXAxis.TabIndex = 2;
            this.grpXAxis.TabStop = false;
            this.grpXAxis.Text = "X (horizontal) axis";
            // 
            // lblXScale
            // 
            this.lblXScale.AutoSize = true;
            this.lblXScale.Location = new System.Drawing.Point(175, 16);
            this.lblXScale.Name = "lblXScale";
            this.lblXScale.Size = new System.Drawing.Size(82, 13);
            this.lblXScale.TabIndex = 5;
            this.lblXScale.Text = "Resulting scale:";
            // 
            // lstXScale
            // 
            this.lstXScale.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colX});
            this.lstXScale.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lstXScale.Location = new System.Drawing.Point(178, 32);
            this.lstXScale.Name = "lstXScale";
            this.lstXScale.Size = new System.Drawing.Size(185, 75);
            this.lstXScale.TabIndex = 4;
            this.lstXScale.TabStop = false;
            this.lstXScale.UseCompatibleStateImageBehavior = false;
            this.lstXScale.View = System.Windows.Forms.View.Details;
            // 
            // lblXLowerLimit
            // 
            this.lblXLowerLimit.AutoSize = true;
            this.lblXLowerLimit.Location = new System.Drawing.Point(10, 62);
            this.lblXLowerLimit.Name = "lblXLowerLimit";
            this.lblXLowerLimit.Size = new System.Drawing.Size(56, 13);
            this.lblXLowerLimit.TabIndex = 3;
            this.lblXLowerLimit.Text = "Lower limit";
            // 
            // lblXUpperLimit
            // 
            this.lblXUpperLimit.AutoSize = true;
            this.lblXUpperLimit.Location = new System.Drawing.Point(10, 35);
            this.lblXUpperLimit.Name = "lblXUpperLimit";
            this.lblXUpperLimit.Size = new System.Drawing.Size(56, 13);
            this.lblXUpperLimit.TabIndex = 2;
            this.lblXUpperLimit.Text = "Upper limit";
            // 
            // txtXLowerLimit
            // 
            this.txtXLowerLimit.Location = new System.Drawing.Point(72, 59);
            this.txtXLowerLimit.Name = "txtXLowerLimit";
            this.txtXLowerLimit.Size = new System.Drawing.Size(100, 20);
            this.txtXLowerLimit.TabIndex = 1;
            this.txtXLowerLimit.TextChanged += new System.EventHandler(this.txtXLowerLimit_TextChanged);
            // 
            // txtXUpperLimit
            // 
            this.txtXUpperLimit.Location = new System.Drawing.Point(72, 32);
            this.txtXUpperLimit.Name = "txtXUpperLimit";
            this.txtXUpperLimit.Size = new System.Drawing.Size(100, 20);
            this.txtXUpperLimit.TabIndex = 0;
            this.txtXUpperLimit.TextChanged += new System.EventHandler(this.txtXUpperLimit_TextChanged);
            // 
            // colX
            // 
            this.colX.Width = 180;
            // 
            // colY
            // 
            this.colY.Width = 180;
            // 
            // frmScale
            // 
            this.AcceptButton = this.cmdOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(390, 293);
            this.Controls.Add(this.grpXAxis);
            this.Controls.Add(this.grpYAxis);
            this.Controls.Add(this.chkRequestLimits);
            this.Controls.Add(this.cmdOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmScale";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Scale limits";
            this.grpYAxis.ResumeLayout(false);
            this.grpYAxis.PerformLayout();
            this.grpXAxis.ResumeLayout(false);
            this.grpXAxis.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdOk;
        private System.Windows.Forms.CheckBox chkRequestLimits;
        private System.Windows.Forms.GroupBox grpYAxis;
        private System.Windows.Forms.Label lblYScale;
        private System.Windows.Forms.ListView lstYScale;
        private System.Windows.Forms.Label lblYLowerLimit;
        private System.Windows.Forms.Label lblYUpperLimit;
        private System.Windows.Forms.TextBox txtYLowerLimit;
        private System.Windows.Forms.TextBox txtYUpperLimit;
        private System.Windows.Forms.GroupBox grpXAxis;
        private System.Windows.Forms.Label lblXScale;
        private System.Windows.Forms.ListView lstXScale;
        private System.Windows.Forms.Label lblXLowerLimit;
        private System.Windows.Forms.Label lblXUpperLimit;
        private System.Windows.Forms.TextBox txtXLowerLimit;
        private System.Windows.Forms.TextBox txtXUpperLimit;
        private System.Windows.Forms.ColumnHeader colX;
        private System.Windows.Forms.ColumnHeader colY;
    }
}
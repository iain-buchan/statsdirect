namespace StatsDirect.UI
{
    partial class ctlOneAxisOptions
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
            this.cboScale = new System.Windows.Forms.ComboBox();
            this.lblScale = new System.Windows.Forms.Label();
            this.cboScaleTextDirection = new System.Windows.Forms.ComboBox();
            this.lblScaleTextDirection = new System.Windows.Forms.Label();
            this.txtScaleTextMask = new System.Windows.Forms.TextBox();
            this.lblScaleTextMask = new System.Windows.Forms.Label();
            this.lblDataRange = new System.Windows.Forms.Label();
            this.txtMinimum = new System.Windows.Forms.TextBox();
            this.txtTics = new System.Windows.Forms.TextBox();
            this.txtInterval = new System.Windows.Forms.TextBox();
            this.lblMinimum = new System.Windows.Forms.Label();
            this.lblTics = new System.Windows.Forms.Label();
            this.lblInterval = new System.Windows.Forms.Label();
            this.lblMaximum = new System.Windows.Forms.Label();
            this.lblMaximumValue = new System.Windows.Forms.Label();
            this.cboMarkerLineAt = new System.Windows.Forms.ComboBox();
            this.lblMarkerLineAt = new System.Windows.Forms.Label();
            this.lblGridLines = new System.Windows.Forms.Label();
            this.cboGridLines = new System.Windows.Forms.ComboBox();
            this.txtTitle = new System.Windows.Forms.TextBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.tlpOnOff = new System.Windows.Forms.TableLayoutPanel();
            this.pnlTitle = new System.Windows.Forms.Panel();
            this.pnlScale = new System.Windows.Forms.Panel();
            this.pnlScaleTextDirection = new System.Windows.Forms.Panel();
            this.pnlScaleTextMask = new System.Windows.Forms.Panel();
            this.pnlRange = new System.Windows.Forms.Panel();
            this.pnlGridLines = new System.Windows.Forms.Panel();
            this.pnlMarkerLine = new System.Windows.Forms.Panel();
            this.tlpOnOff.SuspendLayout();
            this.pnlTitle.SuspendLayout();
            this.pnlScale.SuspendLayout();
            this.pnlScaleTextDirection.SuspendLayout();
            this.pnlScaleTextMask.SuspendLayout();
            this.pnlRange.SuspendLayout();
            this.pnlGridLines.SuspendLayout();
            this.pnlMarkerLine.SuspendLayout();
            this.SuspendLayout();
            // 
            // cboScale
            // 
            this.cboScale.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboScale.FormattingEnabled = true;
            this.cboScale.Items.AddRange(new object[] {
            "Linear",
            "Natural log",
            "Log 10",
            "Date",
            "Category"});
            this.cboScale.Location = new System.Drawing.Point(106, 3);
            this.cboScale.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.cboScale.Name = "cboScale";
            this.cboScale.Size = new System.Drawing.Size(125, 21);
            this.cboScale.TabIndex = 0;
            this.cboScale.SelectedIndexChanged += new System.EventHandler(this.cboScale_SelectedIndexChanged);
            // 
            // lblScale
            // 
            this.lblScale.AutoSize = true;
            this.lblScale.Location = new System.Drawing.Point(3, 7);
            this.lblScale.Name = "lblScale";
            this.lblScale.Size = new System.Drawing.Size(34, 13);
            this.lblScale.TabIndex = 1;
            this.lblScale.Text = "Scale";
            // 
            // cboScaleTextDirection
            // 
            this.cboScaleTextDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboScaleTextDirection.FormattingEnabled = true;
            this.cboScaleTextDirection.Items.AddRange(new object[] {
            "Across",
            "Up",
            "Down",
            "Slope up",
            "Slope down"});
            this.cboScaleTextDirection.Location = new System.Drawing.Point(106, 3);
            this.cboScaleTextDirection.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.cboScaleTextDirection.Name = "cboScaleTextDirection";
            this.cboScaleTextDirection.Size = new System.Drawing.Size(125, 21);
            this.cboScaleTextDirection.TabIndex = 2;
            // 
            // lblScaleTextDirection
            // 
            this.lblScaleTextDirection.AutoSize = true;
            this.lblScaleTextDirection.Location = new System.Drawing.Point(3, 6);
            this.lblScaleTextDirection.Name = "lblScaleTextDirection";
            this.lblScaleTextDirection.Size = new System.Drawing.Size(97, 13);
            this.lblScaleTextDirection.TabIndex = 3;
            this.lblScaleTextDirection.Text = "Scale text direction";
            // 
            // txtScaleTextMask
            // 
            this.txtScaleTextMask.Location = new System.Drawing.Point(106, 3);
            this.txtScaleTextMask.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.txtScaleTextMask.Name = "txtScaleTextMask";
            this.txtScaleTextMask.Size = new System.Drawing.Size(125, 20);
            this.txtScaleTextMask.TabIndex = 4;
            this.txtScaleTextMask.Text = "#.###";
            this.txtScaleTextMask.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtScaleTextMask.TextChanged += new System.EventHandler(this.txtScaleTextMask_TextChanged);
            // 
            // lblScaleTextMask
            // 
            this.lblScaleTextMask.AutoSize = true;
            this.lblScaleTextMask.Location = new System.Drawing.Point(3, 6);
            this.lblScaleTextMask.Name = "lblScaleTextMask";
            this.lblScaleTextMask.Size = new System.Drawing.Size(82, 13);
            this.lblScaleTextMask.TabIndex = 5;
            this.lblScaleTextMask.Text = "Scale text mask";
            // 
            // lblDataRange
            // 
            this.lblDataRange.AutoSize = true;
            this.lblDataRange.Location = new System.Drawing.Point(3, 3);
            this.lblDataRange.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.lblDataRange.Name = "lblDataRange";
            this.lblDataRange.Size = new System.Drawing.Size(117, 13);
            this.lblDataRange.TabIndex = 6;
            this.lblDataRange.Text = "Data range: <a> to <b>";
            // 
            // txtMinimum
            // 
            this.txtMinimum.Location = new System.Drawing.Point(106, 19);
            this.txtMinimum.Name = "txtMinimum";
            this.txtMinimum.Size = new System.Drawing.Size(125, 20);
            this.txtMinimum.TabIndex = 7;
            this.txtMinimum.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtMinimum.TextChanged += new System.EventHandler(this.txtMinimum_TextChanged);
            // 
            // txtTics
            // 
            this.txtTics.Location = new System.Drawing.Point(106, 45);
            this.txtTics.Name = "txtTics";
            this.txtTics.Size = new System.Drawing.Size(125, 20);
            this.txtTics.TabIndex = 8;
            this.txtTics.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtTics.TextChanged += new System.EventHandler(this.txtTics_TextChanged);
            // 
            // txtInterval
            // 
            this.txtInterval.Location = new System.Drawing.Point(106, 71);
            this.txtInterval.Name = "txtInterval";
            this.txtInterval.Size = new System.Drawing.Size(125, 20);
            this.txtInterval.TabIndex = 9;
            this.txtInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtInterval.TextChanged += new System.EventHandler(this.txtInterval_TextChanged);
            // 
            // lblMinimum
            // 
            this.lblMinimum.AutoSize = true;
            this.lblMinimum.Location = new System.Drawing.Point(3, 22);
            this.lblMinimum.Name = "lblMinimum";
            this.lblMinimum.Size = new System.Drawing.Size(48, 13);
            this.lblMinimum.TabIndex = 10;
            this.lblMinimum.Text = "Minimum";
            // 
            // lblTics
            // 
            this.lblTics.AutoSize = true;
            this.lblTics.Location = new System.Drawing.Point(3, 48);
            this.lblTics.Name = "lblTics";
            this.lblTics.Size = new System.Drawing.Size(27, 13);
            this.lblTics.TabIndex = 11;
            this.lblTics.Text = "Tics";
            // 
            // lblInterval
            // 
            this.lblInterval.AutoSize = true;
            this.lblInterval.Location = new System.Drawing.Point(3, 74);
            this.lblInterval.Name = "lblInterval";
            this.lblInterval.Size = new System.Drawing.Size(42, 13);
            this.lblInterval.TabIndex = 12;
            this.lblInterval.Text = "Interval";
            // 
            // lblMaximum
            // 
            this.lblMaximum.AutoSize = true;
            this.lblMaximum.Location = new System.Drawing.Point(3, 94);
            this.lblMaximum.Name = "lblMaximum";
            this.lblMaximum.Size = new System.Drawing.Size(51, 13);
            this.lblMaximum.TabIndex = 13;
            this.lblMaximum.Text = "Maximum";
            // 
            // lblMaximumValue
            // 
            this.lblMaximumValue.Location = new System.Drawing.Point(106, 94);
            this.lblMaximumValue.Name = "lblMaximumValue";
            this.lblMaximumValue.Size = new System.Drawing.Size(125, 13);
            this.lblMaximumValue.TabIndex = 14;
            this.lblMaximumValue.Text = "<value>";
            this.lblMaximumValue.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // cboMarkerLineAt
            // 
            this.cboMarkerLineAt.FormattingEnabled = true;
            this.cboMarkerLineAt.Items.AddRange(new object[] {
            "None",
            "0",
            "1"});
            this.cboMarkerLineAt.Location = new System.Drawing.Point(106, 3);
            this.cboMarkerLineAt.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.cboMarkerLineAt.Name = "cboMarkerLineAt";
            this.cboMarkerLineAt.Size = new System.Drawing.Size(125, 21);
            this.cboMarkerLineAt.TabIndex = 21;
            this.cboMarkerLineAt.Text = "None";
            this.cboMarkerLineAt.SelectedIndexChanged += new System.EventHandler(this.cboMarkerLineAt_SelectedIndexChanged);
            // 
            // lblMarkerLineAt
            // 
            this.lblMarkerLineAt.AutoSize = true;
            this.lblMarkerLineAt.Location = new System.Drawing.Point(3, 6);
            this.lblMarkerLineAt.Name = "lblMarkerLineAt";
            this.lblMarkerLineAt.Size = new System.Drawing.Size(71, 13);
            this.lblMarkerLineAt.TabIndex = 17;
            this.lblMarkerLineAt.Text = "Marker line at";
            // 
            // lblGridLines
            // 
            this.lblGridLines.AutoSize = true;
            this.lblGridLines.Location = new System.Drawing.Point(3, 6);
            this.lblGridLines.Name = "lblGridLines";
            this.lblGridLines.Size = new System.Drawing.Size(50, 13);
            this.lblGridLines.TabIndex = 18;
            this.lblGridLines.Text = "Grid lines";
            // 
            // cboGridLines
            // 
            this.cboGridLines.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboGridLines.FormattingEnabled = true;
            this.cboGridLines.Items.AddRange(new object[] {
            "None",
            "Solid",
            "Dashed"});
            this.cboGridLines.Location = new System.Drawing.Point(106, 3);
            this.cboGridLines.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.cboGridLines.Name = "cboGridLines";
            this.cboGridLines.Size = new System.Drawing.Size(125, 21);
            this.cboGridLines.TabIndex = 19;
            // 
            // txtTitle
            // 
            this.txtTitle.Location = new System.Drawing.Point(106, 3);
            this.txtTitle.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.txtTitle.Name = "txtTitle";
            this.txtTitle.Size = new System.Drawing.Size(125, 20);
            this.txtTitle.TabIndex = 22;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(3, 6);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(27, 13);
            this.lblTitle.TabIndex = 23;
            this.lblTitle.Text = "Title";
            // 
            // tlpOnOff
            // 
            this.tlpOnOff.AutoSize = true;
            this.tlpOnOff.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpOnOff.ColumnCount = 1;
            this.tlpOnOff.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpOnOff.Controls.Add(this.pnlTitle, 0, 0);
            this.tlpOnOff.Controls.Add(this.pnlScale, 0, 1);
            this.tlpOnOff.Controls.Add(this.pnlScaleTextDirection, 0, 2);
            this.tlpOnOff.Controls.Add(this.pnlScaleTextMask, 0, 3);
            this.tlpOnOff.Controls.Add(this.pnlRange, 0, 4);
            this.tlpOnOff.Controls.Add(this.pnlGridLines, 0, 5);
            this.tlpOnOff.Controls.Add(this.pnlMarkerLine, 0, 6);
            this.tlpOnOff.Location = new System.Drawing.Point(0, 0);
            this.tlpOnOff.Margin = new System.Windows.Forms.Padding(0);
            this.tlpOnOff.Name = "tlpOnOff";
            this.tlpOnOff.RowCount = 7;
            this.tlpOnOff.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOnOff.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOnOff.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOnOff.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOnOff.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOnOff.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOnOff.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOnOff.Size = new System.Drawing.Size(234, 249);
            this.tlpOnOff.TabIndex = 24;
            // 
            // pnlTitle
            // 
            this.pnlTitle.AutoSize = true;
            this.pnlTitle.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlTitle.Controls.Add(this.txtTitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Location = new System.Drawing.Point(0, 0);
            this.pnlTitle.Margin = new System.Windows.Forms.Padding(0);
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(234, 23);
            this.pnlTitle.TabIndex = 0;
            // 
            // pnlScale
            // 
            this.pnlScale.AutoSize = true;
            this.pnlScale.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlScale.Controls.Add(this.cboScale);
            this.pnlScale.Controls.Add(this.lblScale);
            this.pnlScale.Location = new System.Drawing.Point(0, 23);
            this.pnlScale.Margin = new System.Windows.Forms.Padding(0);
            this.pnlScale.Name = "pnlScale";
            this.pnlScale.Size = new System.Drawing.Size(234, 24);
            this.pnlScale.TabIndex = 1;
            // 
            // pnlScaleTextDirection
            // 
            this.pnlScaleTextDirection.AutoSize = true;
            this.pnlScaleTextDirection.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlScaleTextDirection.Controls.Add(this.cboScaleTextDirection);
            this.pnlScaleTextDirection.Controls.Add(this.lblScaleTextDirection);
            this.pnlScaleTextDirection.Location = new System.Drawing.Point(0, 47);
            this.pnlScaleTextDirection.Margin = new System.Windows.Forms.Padding(0);
            this.pnlScaleTextDirection.Name = "pnlScaleTextDirection";
            this.pnlScaleTextDirection.Size = new System.Drawing.Size(234, 24);
            this.pnlScaleTextDirection.TabIndex = 2;
            // 
            // pnlScaleTextMask
            // 
            this.pnlScaleTextMask.AutoSize = true;
            this.pnlScaleTextMask.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlScaleTextMask.Controls.Add(this.txtScaleTextMask);
            this.pnlScaleTextMask.Controls.Add(this.lblScaleTextMask);
            this.pnlScaleTextMask.Location = new System.Drawing.Point(0, 71);
            this.pnlScaleTextMask.Margin = new System.Windows.Forms.Padding(0);
            this.pnlScaleTextMask.Name = "pnlScaleTextMask";
            this.pnlScaleTextMask.Size = new System.Drawing.Size(234, 23);
            this.pnlScaleTextMask.TabIndex = 3;
            // 
            // pnlRange
            // 
            this.pnlRange.AutoSize = true;
            this.pnlRange.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlRange.Controls.Add(this.lblDataRange);
            this.pnlRange.Controls.Add(this.txtMinimum);
            this.pnlRange.Controls.Add(this.txtTics);
            this.pnlRange.Controls.Add(this.txtInterval);
            this.pnlRange.Controls.Add(this.lblMinimum);
            this.pnlRange.Controls.Add(this.lblMaximumValue);
            this.pnlRange.Controls.Add(this.lblTics);
            this.pnlRange.Controls.Add(this.lblMaximum);
            this.pnlRange.Controls.Add(this.lblInterval);
            this.pnlRange.Location = new System.Drawing.Point(0, 94);
            this.pnlRange.Margin = new System.Windows.Forms.Padding(0);
            this.pnlRange.Name = "pnlRange";
            this.pnlRange.Size = new System.Drawing.Size(234, 107);
            this.pnlRange.TabIndex = 4;
            // 
            // pnlGridLines
            // 
            this.pnlGridLines.AutoSize = true;
            this.pnlGridLines.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlGridLines.Controls.Add(this.cboGridLines);
            this.pnlGridLines.Controls.Add(this.lblGridLines);
            this.pnlGridLines.Location = new System.Drawing.Point(0, 201);
            this.pnlGridLines.Margin = new System.Windows.Forms.Padding(0);
            this.pnlGridLines.Name = "pnlGridLines";
            this.pnlGridLines.Size = new System.Drawing.Size(234, 24);
            this.pnlGridLines.TabIndex = 6;
            // 
            // pnlMarkerLine
            // 
            this.pnlMarkerLine.AutoSize = true;
            this.pnlMarkerLine.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlMarkerLine.Controls.Add(this.cboMarkerLineAt);
            this.pnlMarkerLine.Controls.Add(this.lblMarkerLineAt);
            this.pnlMarkerLine.Location = new System.Drawing.Point(0, 225);
            this.pnlMarkerLine.Margin = new System.Windows.Forms.Padding(0);
            this.pnlMarkerLine.Name = "pnlMarkerLine";
            this.pnlMarkerLine.Size = new System.Drawing.Size(234, 24);
            this.pnlMarkerLine.TabIndex = 7;
            // 
            // ctlOneAxisOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tlpOnOff);
            this.Name = "ctlOneAxisOptions";
            this.Size = new System.Drawing.Size(234, 249);
            this.tlpOnOff.ResumeLayout(false);
            this.tlpOnOff.PerformLayout();
            this.pnlTitle.ResumeLayout(false);
            this.pnlTitle.PerformLayout();
            this.pnlScale.ResumeLayout(false);
            this.pnlScale.PerformLayout();
            this.pnlScaleTextDirection.ResumeLayout(false);
            this.pnlScaleTextDirection.PerformLayout();
            this.pnlScaleTextMask.ResumeLayout(false);
            this.pnlScaleTextMask.PerformLayout();
            this.pnlRange.ResumeLayout(false);
            this.pnlRange.PerformLayout();
            this.pnlGridLines.ResumeLayout(false);
            this.pnlGridLines.PerformLayout();
            this.pnlMarkerLine.ResumeLayout(false);
            this.pnlMarkerLine.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox cboScale;
        private System.Windows.Forms.Label lblScale;
        private System.Windows.Forms.ComboBox cboScaleTextDirection;
        private System.Windows.Forms.Label lblScaleTextDirection;
        private System.Windows.Forms.TextBox txtScaleTextMask;
        private System.Windows.Forms.Label lblScaleTextMask;
        private System.Windows.Forms.Label lblDataRange;
        private System.Windows.Forms.TextBox txtMinimum;
        private System.Windows.Forms.TextBox txtTics;
        private System.Windows.Forms.TextBox txtInterval;
        private System.Windows.Forms.Label lblMinimum;
        private System.Windows.Forms.Label lblTics;
        private System.Windows.Forms.Label lblInterval;
        private System.Windows.Forms.Label lblMaximum;
        private System.Windows.Forms.Label lblMaximumValue;
        private System.Windows.Forms.ComboBox cboMarkerLineAt;
        private System.Windows.Forms.Label lblMarkerLineAt;
        private System.Windows.Forms.Label lblGridLines;
        private System.Windows.Forms.ComboBox cboGridLines;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TableLayoutPanel tlpOnOff;
        private System.Windows.Forms.Panel pnlTitle;
        private System.Windows.Forms.Panel pnlScale;
        private System.Windows.Forms.Panel pnlScaleTextDirection;
        private System.Windows.Forms.Panel pnlScaleTextMask;
        private System.Windows.Forms.Panel pnlRange;
        private System.Windows.Forms.Panel pnlGridLines;
        private System.Windows.Forms.Panel pnlMarkerLine;
    }
}

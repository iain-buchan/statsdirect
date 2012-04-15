namespace StatsDirect.UI
{
    partial class ctlOneSeriesOptions
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
            this.chkFillMarker = new System.Windows.Forms.CheckBox();
            this.lblMarkerSize = new System.Windows.Forms.Label();
            this.cboMarkerSize = new System.Windows.Forms.ComboBox();
            this.grpColour = new System.Windows.Forms.GroupBox();
            this.tlpLayout = new System.Windows.Forms.TableLayoutPanel();
            this.pnlMarkerStyle = new System.Windows.Forms.Panel();
            this.lblMarkerStyle = new System.Windows.Forms.Label();
            this.pnlMarkerSize = new System.Windows.Forms.Panel();
            this.pnlFillStyle = new System.Windows.Forms.Panel();
            this.lblFillStyle = new System.Windows.Forms.Label();
            this.pnlLineThickness = new System.Windows.Forms.Panel();
            this.lblLineWidth = new System.Windows.Forms.Label();
            this.pnlDashStyle = new System.Windows.Forms.Panel();
            this.lblDashStyle = new System.Windows.Forms.Label();
            this.colorPanel = new PJLControls.ColorPanel();
            this.ctlMarkerShape1 = new StatsDirect.UI.ctlMarkerShape();
            this.ctlFillStyle1 = new StatsDirect.UI.ctlFillStyle();
            this.lineThickness = new StatsDirect.UI.ctlLineThickness();
            this.ctlDashStyle1 = new StatsDirect.UI.ctlDashStyle();
            this.grpColour.SuspendLayout();
            this.tlpLayout.SuspendLayout();
            this.pnlMarkerStyle.SuspendLayout();
            this.pnlMarkerSize.SuspendLayout();
            this.pnlFillStyle.SuspendLayout();
            this.pnlLineThickness.SuspendLayout();
            this.pnlDashStyle.SuspendLayout();
            this.SuspendLayout();
            // 
            // chkFillMarker
            // 
            this.chkFillMarker.AutoSize = true;
            this.chkFillMarker.Location = new System.Drawing.Point(123, 7);
            this.chkFillMarker.Name = "chkFillMarker";
            this.chkFillMarker.Size = new System.Drawing.Size(44, 17);
            this.chkFillMarker.TabIndex = 1;
            this.chkFillMarker.Text = "Fill?";
            this.chkFillMarker.UseVisualStyleBackColor = true;
            // 
            // lblMarkerSize
            // 
            this.lblMarkerSize.AutoSize = true;
            this.lblMarkerSize.Location = new System.Drawing.Point(6, 6);
            this.lblMarkerSize.Name = "lblMarkerSize";
            this.lblMarkerSize.Size = new System.Drawing.Size(61, 13);
            this.lblMarkerSize.TabIndex = 20;
            this.lblMarkerSize.Text = "Marker size";
            // 
            // cboMarkerSize
            // 
            this.cboMarkerSize.FormattingEnabled = true;
            this.cboMarkerSize.Items.AddRange(new object[] {
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "10",
            "12",
            "14",
            "16",
            "18",
            "20",
            "24",
            "28",
            "36"});
            this.cboMarkerSize.Location = new System.Drawing.Point(76, 3);
            this.cboMarkerSize.Margin = new System.Windows.Forms.Padding(0);
            this.cboMarkerSize.Name = "cboMarkerSize";
            this.cboMarkerSize.Size = new System.Drawing.Size(48, 21);
            this.cboMarkerSize.TabIndex = 4;
            // 
            // grpColour
            // 
            this.grpColour.Controls.Add(this.colorPanel);
            this.grpColour.Location = new System.Drawing.Point(3, 58);
            this.grpColour.Name = "grpColour";
            this.grpColour.Size = new System.Drawing.Size(182, 72);
            this.grpColour.TabIndex = 3;
            this.grpColour.TabStop = false;
            this.grpColour.Text = "Marker colour";
            // 
            // tlpLayout
            // 
            this.tlpLayout.AutoSize = true;
            this.tlpLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpLayout.ColumnCount = 1;
            this.tlpLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpLayout.Controls.Add(this.grpColour, 0, 2);
            this.tlpLayout.Controls.Add(this.pnlMarkerStyle, 0, 0);
            this.tlpLayout.Controls.Add(this.pnlMarkerSize, 0, 3);
            this.tlpLayout.Controls.Add(this.pnlFillStyle, 0, 1);
            this.tlpLayout.Controls.Add(this.pnlLineThickness, 0, 5);
            this.tlpLayout.Controls.Add(this.pnlDashStyle, 0, 6);
            this.tlpLayout.Location = new System.Drawing.Point(0, 0);
            this.tlpLayout.Margin = new System.Windows.Forms.Padding(0);
            this.tlpLayout.Name = "tlpLayout";
            this.tlpLayout.RowCount = 7;
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.Size = new System.Drawing.Size(188, 208);
            this.tlpLayout.TabIndex = 25;
            // 
            // pnlMarkerStyle
            // 
            this.pnlMarkerStyle.AutoSize = true;
            this.pnlMarkerStyle.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlMarkerStyle.Controls.Add(this.lblMarkerStyle);
            this.pnlMarkerStyle.Controls.Add(this.ctlMarkerShape1);
            this.pnlMarkerStyle.Controls.Add(this.chkFillMarker);
            this.pnlMarkerStyle.Location = new System.Drawing.Point(0, 0);
            this.pnlMarkerStyle.Margin = new System.Windows.Forms.Padding(0);
            this.pnlMarkerStyle.Name = "pnlMarkerStyle";
            this.pnlMarkerStyle.Size = new System.Drawing.Size(170, 29);
            this.pnlMarkerStyle.TabIndex = 1;
            // 
            // lblMarkerStyle
            // 
            this.lblMarkerStyle.AutoSize = true;
            this.lblMarkerStyle.Location = new System.Drawing.Point(6, 8);
            this.lblMarkerStyle.Name = "lblMarkerStyle";
            this.lblMarkerStyle.Size = new System.Drawing.Size(64, 13);
            this.lblMarkerStyle.TabIndex = 25;
            this.lblMarkerStyle.Text = "Marker style";
            // 
            // pnlMarkerSize
            // 
            this.pnlMarkerSize.AutoSize = true;
            this.pnlMarkerSize.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlMarkerSize.Controls.Add(this.lblMarkerSize);
            this.pnlMarkerSize.Controls.Add(this.cboMarkerSize);
            this.pnlMarkerSize.Location = new System.Drawing.Point(0, 133);
            this.pnlMarkerSize.Margin = new System.Windows.Forms.Padding(0);
            this.pnlMarkerSize.Name = "pnlMarkerSize";
            this.pnlMarkerSize.Size = new System.Drawing.Size(124, 24);
            this.pnlMarkerSize.TabIndex = 4;
            // 
            // pnlFillStyle
            // 
            this.pnlFillStyle.AutoSize = true;
            this.pnlFillStyle.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlFillStyle.Controls.Add(this.lblFillStyle);
            this.pnlFillStyle.Controls.Add(this.ctlFillStyle1);
            this.pnlFillStyle.Location = new System.Drawing.Point(0, 29);
            this.pnlFillStyle.Margin = new System.Windows.Forms.Padding(0);
            this.pnlFillStyle.Name = "pnlFillStyle";
            this.pnlFillStyle.Size = new System.Drawing.Size(116, 26);
            this.pnlFillStyle.TabIndex = 2;
            // 
            // lblFillStyle
            // 
            this.lblFillStyle.AutoSize = true;
            this.lblFillStyle.Location = new System.Drawing.Point(6, 8);
            this.lblFillStyle.Name = "lblFillStyle";
            this.lblFillStyle.Size = new System.Drawing.Size(19, 13);
            this.lblFillStyle.TabIndex = 1;
            this.lblFillStyle.Text = "Fill";
            // 
            // pnlLineThickness
            // 
            this.pnlLineThickness.AutoSize = true;
            this.pnlLineThickness.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlLineThickness.Controls.Add(this.lblLineWidth);
            this.pnlLineThickness.Controls.Add(this.lineThickness);
            this.pnlLineThickness.Location = new System.Drawing.Point(0, 157);
            this.pnlLineThickness.Margin = new System.Windows.Forms.Padding(0);
            this.pnlLineThickness.Name = "pnlLineThickness";
            this.pnlLineThickness.Size = new System.Drawing.Size(179, 24);
            this.pnlLineThickness.TabIndex = 5;
            // 
            // lblLineWidth
            // 
            this.lblLineWidth.AutoSize = true;
            this.lblLineWidth.Location = new System.Drawing.Point(6, 6);
            this.lblLineWidth.Name = "lblLineWidth";
            this.lblLineWidth.Size = new System.Drawing.Size(55, 13);
            this.lblLineWidth.TabIndex = 23;
            this.lblLineWidth.Text = "Line width";
            // 
            // pnlDashStyle
            // 
            this.pnlDashStyle.AutoSize = true;
            this.pnlDashStyle.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlDashStyle.Controls.Add(this.lblDashStyle);
            this.pnlDashStyle.Controls.Add(this.ctlDashStyle1);
            this.pnlDashStyle.Location = new System.Drawing.Point(0, 181);
            this.pnlDashStyle.Margin = new System.Windows.Forms.Padding(0);
            this.pnlDashStyle.Name = "pnlDashStyle";
            this.pnlDashStyle.Size = new System.Drawing.Size(182, 27);
            this.pnlDashStyle.TabIndex = 6;
            // 
            // lblDashStyle
            // 
            this.lblDashStyle.AutoSize = true;
            this.lblDashStyle.Location = new System.Drawing.Point(6, 6);
            this.lblDashStyle.Name = "lblDashStyle";
            this.lblDashStyle.Size = new System.Drawing.Size(51, 13);
            this.lblDashStyle.TabIndex = 24;
            this.lblDashStyle.Text = "Line style";
            // 
            // colorPanel
            // 
            this.colorPanel.Color = System.Drawing.Color.White;
            this.colorPanel.ColorSet = PJLControls.ColorSet.Custom;
            this.colorPanel.ColorSortOrder = PJLControls.ColorSortOrder.Unsorted;
            this.colorPanel.ColorWellSize = new System.Drawing.Size(18, 18);
            this.colorPanel.CustomColor = System.Drawing.Color.Empty;
            this.colorPanel.CustomColors = new System.Drawing.Color[] {
        System.Drawing.SystemColors.ButtonFace,
        System.Drawing.Color.Red,
        System.Drawing.Color.Lime,
        System.Drawing.Color.Blue,
        System.Drawing.Color.Yellow,
        System.Drawing.Color.Magenta,
        System.Drawing.Color.Cyan,
        System.Drawing.Color.Black,
        System.Drawing.Color.White,
        System.Drawing.Color.Gray,
        System.Drawing.Color.Maroon,
        System.Drawing.Color.Green,
        System.Drawing.Color.Navy,
        System.Drawing.Color.Olive,
        System.Drawing.Color.Purple,
        System.Drawing.Color.Teal,
        System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))))};
            this.colorPanel.LastWellIsCustom = true;
            this.colorPanel.Location = new System.Drawing.Point(9, 19);
            this.colorPanel.Name = "colorPanel";
            this.colorPanel.Size = new System.Drawing.Size(166, 40);
            this.colorPanel.TabIndex = 3;
            // 
            // ctlMarkerShape1
            // 
            this.ctlMarkerShape1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlMarkerShape1.Location = new System.Drawing.Point(76, 3);
            this.ctlMarkerShape1.MarkerShape = StatsDirect.Charting.MarkerShape.Circle;
            this.ctlMarkerShape1.MinimumSize = new System.Drawing.Size(35, 21);
            this.ctlMarkerShape1.Name = "ctlMarkerShape1";
            this.ctlMarkerShape1.Size = new System.Drawing.Size(41, 23);
            this.ctlMarkerShape1.TabIndex = 0;
            // 
            // ctlFillStyle1
            // 
            this.ctlFillStyle1.FillStyle = StatsDirect.Charting.FillStyle.None;
            this.ctlFillStyle1.Location = new System.Drawing.Point(76, 3);
            this.ctlFillStyle1.Margin = new System.Windows.Forms.Padding(0);
            this.ctlFillStyle1.MinimumSize = new System.Drawing.Size(40, 21);
            this.ctlFillStyle1.Name = "ctlFillStyle1";
            this.ctlFillStyle1.Size = new System.Drawing.Size(40, 23);
            this.ctlFillStyle1.TabIndex = 2;
            // 
            // lineThickness
            // 
            this.lineThickness.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.lineThickness.LineThickness = 1;
            this.lineThickness.Location = new System.Drawing.Point(76, 3);
            this.lineThickness.Margin = new System.Windows.Forms.Padding(0);
            this.lineThickness.MaximumSize = new System.Drawing.Size(10000, 21);
            this.lineThickness.MinimumSize = new System.Drawing.Size(100, 21);
            this.lineThickness.Name = "lineThickness";
            this.lineThickness.Size = new System.Drawing.Size(103, 21);
            this.lineThickness.TabIndex = 5;
            // 
            // ctlDashStyle1
            // 
            this.ctlDashStyle1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlDashStyle1.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            this.ctlDashStyle1.Location = new System.Drawing.Point(76, 3);
            this.ctlDashStyle1.MaximumSize = new System.Drawing.Size(10000, 21);
            this.ctlDashStyle1.MinimumSize = new System.Drawing.Size(100, 21);
            this.ctlDashStyle1.Name = "ctlDashStyle1";
            this.ctlDashStyle1.Size = new System.Drawing.Size(103, 21);
            this.ctlDashStyle1.TabIndex = 6;
            // 
            // ctlOneSeriesOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tlpLayout);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "ctlOneSeriesOptions";
            this.Size = new System.Drawing.Size(188, 208);
            this.grpColour.ResumeLayout(false);
            this.tlpLayout.ResumeLayout(false);
            this.tlpLayout.PerformLayout();
            this.pnlMarkerStyle.ResumeLayout(false);
            this.pnlMarkerStyle.PerformLayout();
            this.pnlMarkerSize.ResumeLayout(false);
            this.pnlMarkerSize.PerformLayout();
            this.pnlFillStyle.ResumeLayout(false);
            this.pnlFillStyle.PerformLayout();
            this.pnlLineThickness.ResumeLayout(false);
            this.pnlLineThickness.PerformLayout();
            this.pnlDashStyle.ResumeLayout(false);
            this.pnlDashStyle.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpColour;
        private PJLControls.ColorPanel colorPanel;
        private System.Windows.Forms.Label lblMarkerSize;
        private System.Windows.Forms.ComboBox cboMarkerSize;
        private System.Windows.Forms.CheckBox chkFillMarker;
        private ctlLineThickness lineThickness;
        private StatsDirect.UI.ctlDashStyle ctlDashStyle1;
        private ctlMarkerShape ctlMarkerShape1;
        private System.Windows.Forms.TableLayoutPanel tlpLayout;
        private System.Windows.Forms.Panel pnlMarkerStyle;
        private System.Windows.Forms.Panel pnlMarkerSize;
        private System.Windows.Forms.Panel pnlFillStyle;
        private ctlFillStyle ctlFillStyle1;
        private System.Windows.Forms.Label lblMarkerStyle;
        private System.Windows.Forms.Panel pnlLineThickness;
        private System.Windows.Forms.Label lblFillStyle;
        private System.Windows.Forms.Panel pnlDashStyle;
        private System.Windows.Forms.Label lblDashStyle;
        private System.Windows.Forms.Label lblLineWidth;
    }
}
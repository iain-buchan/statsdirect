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
            this.chkFillMarker = new System.Windows.Forms.CheckBox();
            this.lblMarkerSize = new System.Windows.Forms.Label();
            this.cboMarkerSize = new System.Windows.Forms.ComboBox();
            this.grpMarkerColour = new System.Windows.Forms.GroupBox();
            this.markerColorPanel = new StatsDirect.PJLControls.ColorPanel();
            this.tlpLayout = new System.Windows.Forms.TableLayoutPanel();
            this.pnlMarkerStyle = new System.Windows.Forms.Panel();
            this.lblMarkerStyle = new System.Windows.Forms.Label();
            this.markerShaper = new StatsDirect.UI.ctlMarkerShape();
            this.pnlMarkerSize = new System.Windows.Forms.Panel();
            this.pnlFillStyle = new System.Windows.Forms.Panel();
            this.lblFillStyle = new System.Windows.Forms.Label();
            this.fillStyler = new StatsDirect.UI.ctlFillStyle();
            this.pnlLineThickness = new System.Windows.Forms.Panel();
            this.lblLineWidth = new System.Windows.Forms.Label();
            this.lineThickness = new StatsDirect.UI.ctlLineThickness();
            this.pnlDashStyle = new System.Windows.Forms.Panel();
            this.lblDashStyle = new System.Windows.Forms.Label();
            this.dashStyler = new StatsDirect.UI.ctlDashStyle();
            this.grpLineColour = new System.Windows.Forms.GroupBox();
            this.lineColorPanel = new StatsDirect.PJLControls.ColorPanel();
            this.grpMarkerColour.SuspendLayout();
            this.tlpLayout.SuspendLayout();
            this.pnlMarkerStyle.SuspendLayout();
            this.pnlMarkerSize.SuspendLayout();
            this.pnlFillStyle.SuspendLayout();
            this.pnlLineThickness.SuspendLayout();
            this.pnlDashStyle.SuspendLayout();
            this.grpLineColour.SuspendLayout();
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
            // grpMarkerColour
            // 
            this.grpMarkerColour.Controls.Add(this.markerColorPanel);
            this.grpMarkerColour.Location = new System.Drawing.Point(3, 58);
            this.grpMarkerColour.Name = "grpMarkerColour";
            this.grpMarkerColour.Size = new System.Drawing.Size(182, 72);
            this.grpMarkerColour.TabIndex = 3;
            this.grpMarkerColour.TabStop = false;
            this.grpMarkerColour.Text = "Marker colour";
            // 
            // markerColorPanel
            // 
            this.markerColorPanel.Color = System.Drawing.Color.White;
            this.markerColorPanel.ColorSet = StatsDirect.PJLControls.ColorSet.Custom;
            this.markerColorPanel.ColorSortOrder = StatsDirect.PJLControls.ColorSortOrder.Unsorted;
            this.markerColorPanel.ColorWellSize = new System.Drawing.Size(18, 18);
            this.markerColorPanel.CustomColor = System.Drawing.Color.Empty;
            this.markerColorPanel.CustomColors = new System.Drawing.Color[] {
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
            this.markerColorPanel.LastWellIsCustom = true;
            this.markerColorPanel.Location = new System.Drawing.Point(9, 19);
            this.markerColorPanel.Name = "markerColorPanel";
            this.markerColorPanel.Size = new System.Drawing.Size(166, 40);
            this.markerColorPanel.TabIndex = 3;
            // 
            // tlpLayout
            // 
            this.tlpLayout.AutoSize = true;
            this.tlpLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpLayout.ColumnCount = 1;
            this.tlpLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpLayout.Controls.Add(this.grpLineColour, 0, 7);
            this.tlpLayout.Controls.Add(this.grpMarkerColour, 0, 2);
            this.tlpLayout.Controls.Add(this.pnlMarkerStyle, 0, 0);
            this.tlpLayout.Controls.Add(this.pnlMarkerSize, 0, 3);
            this.tlpLayout.Controls.Add(this.pnlFillStyle, 0, 1);
            this.tlpLayout.Controls.Add(this.pnlLineThickness, 0, 5);
            this.tlpLayout.Controls.Add(this.pnlDashStyle, 0, 6);
            this.tlpLayout.Location = new System.Drawing.Point(0, 0);
            this.tlpLayout.Margin = new System.Windows.Forms.Padding(0);
            this.tlpLayout.Name = "tlpLayout";
            this.tlpLayout.RowCount = 8;
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLayout.Size = new System.Drawing.Size(188, 286);
            this.tlpLayout.TabIndex = 25;
            // 
            // pnlMarkerStyle
            // 
            this.pnlMarkerStyle.AutoSize = true;
            this.pnlMarkerStyle.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlMarkerStyle.Controls.Add(this.lblMarkerStyle);
            this.pnlMarkerStyle.Controls.Add(this.markerShaper);
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
            // markerShaper
            // 
            this.markerShaper.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.markerShaper.Location = new System.Drawing.Point(76, 3);
            this.markerShaper.MarkerShape = StatsDirect.Charting.MarkerShape.Circle;
            this.markerShaper.MinimumSize = new System.Drawing.Size(35, 21);
            this.markerShaper.Name = "markerShaper";
            this.markerShaper.Size = new System.Drawing.Size(41, 23);
            this.markerShaper.TabIndex = 0;
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
            this.pnlFillStyle.Controls.Add(this.fillStyler);
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
            this.lblFillStyle.Size = new System.Drawing.Size(52, 13);
            this.lblFillStyle.TabIndex = 1;
            this.lblFillStyle.Text = "Marker fill";
            // 
            // fillStyler
            // 
            this.fillStyler.FillStyle = StatsDirect.Charting.FillStyle.None;
            this.fillStyler.Location = new System.Drawing.Point(76, 3);
            this.fillStyler.Margin = new System.Windows.Forms.Padding(0);
            this.fillStyler.MinimumSize = new System.Drawing.Size(40, 21);
            this.fillStyler.Name = "fillStyler";
            this.fillStyler.Size = new System.Drawing.Size(40, 23);
            this.fillStyler.TabIndex = 2;
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
            // pnlDashStyle
            // 
            this.pnlDashStyle.AutoSize = true;
            this.pnlDashStyle.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlDashStyle.Controls.Add(this.lblDashStyle);
            this.pnlDashStyle.Controls.Add(this.dashStyler);
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
            // dashStyler
            // 
            this.dashStyler.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.dashStyler.DashStyle = Charting.DashStyleDescriptor.Solid;
            this.dashStyler.Location = new System.Drawing.Point(76, 3);
            this.dashStyler.MaximumSize = new System.Drawing.Size(10000, 21);
            this.dashStyler.MinimumSize = new System.Drawing.Size(100, 21);
            this.dashStyler.Name = "dashStyler";
            this.dashStyler.Size = new System.Drawing.Size(103, 21);
            this.dashStyler.TabIndex = 6;
            // 
            // grpLineColour
            // 
            this.grpLineColour.Controls.Add(this.lineColorPanel);
            this.grpLineColour.Location = new System.Drawing.Point(3, 211);
            this.grpLineColour.Name = "grpLineColour";
            this.grpLineColour.Size = new System.Drawing.Size(182, 72);
            this.grpLineColour.TabIndex = 7;
            this.grpLineColour.TabStop = false;
            this.grpLineColour.Text = "Line colour";
            // 
            // lineColorPanel
            // 
            this.lineColorPanel.Color = System.Drawing.Color.White;
            this.lineColorPanel.ColorSet = StatsDirect.PJLControls.ColorSet.Custom;
            this.lineColorPanel.ColorSortOrder = StatsDirect.PJLControls.ColorSortOrder.Unsorted;
            this.lineColorPanel.ColorWellSize = new System.Drawing.Size(18, 18);
            this.lineColorPanel.CustomColor = System.Drawing.Color.Empty;
            this.lineColorPanel.CustomColors = new System.Drawing.Color[] {
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
            this.lineColorPanel.LastWellIsCustom = true;
            this.lineColorPanel.Location = new System.Drawing.Point(9, 19);
            this.lineColorPanel.Name = "lineColorPanel";
            this.lineColorPanel.Size = new System.Drawing.Size(166, 40);
            this.lineColorPanel.TabIndex = 3;
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
            this.Size = new System.Drawing.Size(188, 286);
            this.grpMarkerColour.ResumeLayout(false);
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
            this.grpLineColour.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpMarkerColour;
        private StatsDirect.PJLControls.ColorPanel markerColorPanel;
        private System.Windows.Forms.Label lblMarkerSize;
        private System.Windows.Forms.ComboBox cboMarkerSize;
        private System.Windows.Forms.CheckBox chkFillMarker;
        private ctlLineThickness lineThickness;
        private StatsDirect.UI.ctlDashStyle dashStyler;
        private ctlMarkerShape markerShaper;
        private System.Windows.Forms.TableLayoutPanel tlpLayout;
        private System.Windows.Forms.Panel pnlMarkerStyle;
        private System.Windows.Forms.Panel pnlMarkerSize;
        private System.Windows.Forms.Panel pnlFillStyle;
        private ctlFillStyle fillStyler;
        private System.Windows.Forms.Label lblMarkerStyle;
        private System.Windows.Forms.Panel pnlLineThickness;
        private System.Windows.Forms.Label lblFillStyle;
        private System.Windows.Forms.Panel pnlDashStyle;
        private System.Windows.Forms.Label lblDashStyle;
        private System.Windows.Forms.Label lblLineWidth;
        private System.Windows.Forms.GroupBox grpLineColour;
        private PJLControls.ColorPanel lineColorPanel;
    }
}
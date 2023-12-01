namespace StatsDirect.UI
{
    partial class ctlChartOptions
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
            this.tlpDisplay = new System.Windows.Forms.TableLayoutPanel();
            this.pnlLegendFont = new System.Windows.Forms.Panel();
            this.ctlLegendFont = new StatsDirect.UI.ctlFont();
            this.pnlSeriesLabelFont = new System.Windows.Forms.Panel();
            this.ctlSeriesLabelFont = new StatsDirect.UI.ctlFont();
            this.pnlSeriesOptions = new System.Windows.Forms.Panel();
            this.seriesOptions = new StatsDirect.UI.ctlSeriesOptions();
            this.pnlColour = new System.Windows.Forms.Panel();
            this.grpColour = new System.Windows.Forms.GroupBox();
            this.rdoMonochrome = new System.Windows.Forms.RadioButton();
            this.rdoColour = new System.Windows.Forms.RadioButton();
            this.pnlShowLegend = new System.Windows.Forms.Panel();
            this.chkShowLegend = new System.Windows.Forms.CheckBox();
            this.pnlTitleFont = new System.Windows.Forms.Panel();
            this.ctlTitleFont = new StatsDirect.UI.ctlFont();
            this.pnlAxisTitleFont = new System.Windows.Forms.Panel();
            this.ctlAxisTitleFont = new StatsDirect.UI.ctlFont();
            this.pnlAxisLabelFont = new System.Windows.Forms.Panel();
            this.ctlAxisLabelFont = new StatsDirect.UI.ctlFont();
            this.pnlAxisLineThickness = new System.Windows.Forms.Panel();
            this.lblAxisLineThickness = new System.Windows.Forms.Label();
            this.ctlAxisLineThickness = new StatsDirect.UI.ctlLineThickness();
            this.pnlBoxAxes = new System.Windows.Forms.Panel();
            this.chkBoxAxes = new System.Windows.Forms.CheckBox();
            this.pnlChartTitle = new System.Windows.Forms.Panel();
            this.txtChartTitle = new System.Windows.Forms.TextBox();
            this.lblChartTitle = new System.Windows.Forms.Label();
            this.pnlSeriesLabels = new System.Windows.Forms.Panel();
            this.gridSeriesLabels = new System.Windows.Forms.DataGridView();
            this.Title = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblSeriesLabels = new System.Windows.Forms.Label();
            this.pnlControlOptions = new System.Windows.Forms.Panel();
            this.grpControlOptions = new System.Windows.Forms.GroupBox();
            this.lblControlDecimalPlaces = new System.Windows.Forms.Label();
            this.lblControlLabelLinesTo = new System.Windows.Forms.Label();
            this.cboControlDecimalPlaces = new System.Windows.Forms.ComboBox();
            this.lblUpperControl = new System.Windows.Forms.Label();
            this.txtUpperControl = new System.Windows.Forms.TextBox();
            this.lblLowerControl = new System.Windows.Forms.Label();
            this.txtLowerControl = new System.Windows.Forms.TextBox();
            this.lblUpperWarning = new System.Windows.Forms.Label();
            this.txtUpperWarning = new System.Windows.Forms.TextBox();
            this.lblLowerWarning = new System.Windows.Forms.Label();
            this.txtLowerWarning = new System.Windows.Forms.TextBox();
            this.chkHasUserSpecifiedLimits = new System.Windows.Forms.CheckBox();
            this.lblSD = new System.Windows.Forms.Label();
            this.txtStandardDeviation = new System.Windows.Forms.TextBox();
            this.lblMean = new System.Windows.Forms.Label();
            this.txtMean = new System.Windows.Forms.TextBox();
            this.chkHasUserSpecifiedMeanAndSD = new System.Windows.Forms.CheckBox();
            this.lblObservations = new System.Windows.Forms.Label();
            this.lblUseFirst = new System.Windows.Forms.Label();
            this.cboKObs = new System.Windows.Forms.ComboBox();
            this.chk3SD = new System.Windows.Forms.CheckBox();
            this.chk2SD = new System.Windows.Forms.CheckBox();
            this.chk1SD = new System.Windows.Forms.CheckBox();
            this.chkMean = new System.Windows.Forms.CheckBox();
            this.tlpOuter = new System.Windows.Forms.TableLayoutPanel();
            this.tlpFunction = new System.Windows.Forms.TableLayoutPanel();
            this.ctlAxisOptions = new StatsDirect.UI.ctlAxisOptions();
            this.tlpCustom = new System.Windows.Forms.TableLayoutPanel();
            this.pnlPreview = new System.Windows.Forms.Panel();
            this.cmdPreview = new System.Windows.Forms.Button();
            this.pnlOrientation = new System.Windows.Forms.Panel();
            this.grpOrientation = new System.Windows.Forms.GroupBox();
            this.rdoOrientationVertical = new System.Windows.Forms.RadioButton();
            this.rdoOrientationHorizontal = new System.Windows.Forms.RadioButton();
            this.pnlBarOptions = new System.Windows.Forms.Panel();
            this.tlpBarOptions = new System.Windows.Forms.TableLayoutPanel();
            this.pnlBarOptionsNonStacked = new System.Windows.Forms.Panel();
            this.lblBarBarWidthPercent2 = new System.Windows.Forms.Label();
            this.lblBarBarWidthPercent1 = new System.Windows.Forms.Label();
            this.txtBarBarWidthPercent = new System.Windows.Forms.TextBox();
            this.ctlBarOptions = new StatsDirect.UI.ctlBarOptions();
            this.pnlForestOptions = new System.Windows.Forms.Panel();
            this.grpForestOptions = new System.Windows.Forms.GroupBox();
            this.chkForestMarkCentres = new System.Windows.Forms.CheckBox();
            this.lblForestDecimalPlaces = new System.Windows.Forms.Label();
            this.lblForestLabelEffectSizesTo = new System.Windows.Forms.Label();
            this.cboForestDecimalPlaces = new System.Windows.Forms.ComboBox();
            this.pnlNormalOptions = new System.Windows.Forms.Panel();
            this.grpNormalScaling = new System.Windows.Forms.GroupBox();
            this.rdoNormalScaled = new System.Windows.Forms.RadioButton();
            this.rdoNormalRaw = new System.Windows.Forms.RadioButton();
            this.grpNormalOptions = new System.Windows.Forms.GroupBox();
            this.rdoNormalExpectedNormalOrder = new System.Windows.Forms.RadioButton();
            this.rdoNormalBlom = new System.Windows.Forms.RadioButton();
            this.rdoNormalVanDerWaerden = new System.Windows.Forms.RadioButton();
            this.pnlPyramidOptions = new System.Windows.Forms.Panel();
            this.txtPyramidScaleMaximum = new System.Windows.Forms.TextBox();
            this.lblPyramidScaleMaximum = new System.Windows.Forms.Label();
            this.pnlRocOptions = new System.Windows.Forms.Panel();
            this.grpRocOptions = new System.Windows.Forms.GroupBox();
            this.cboRocWeight = new System.Windows.Forms.ComboBox();
            this.lblRocSensSpec = new System.Windows.Forms.Label();
            this.lblRocPercent = new System.Windows.Forms.Label();
            this.lblRocConfidenceInterval = new System.Windows.Forms.Label();
            this.rdoRocCutOffGt = new System.Windows.Forms.RadioButton();
            this.rdoRocCutOffGe = new System.Windows.Forms.RadioButton();
            this.rdoRocCutOffLe = new System.Windows.Forms.RadioButton();
            this.rdoRocCutOffLt = new System.Windows.Forms.RadioButton();
            this.cboRocCi = new System.Windows.Forms.ComboBox();
            this.chkRocShowCutOffCalculator = new System.Windows.Forms.CheckBox();
            this.chkRocShowCutoff = new System.Windows.Forms.CheckBox();
            this.pnlSurvivalOptions = new System.Windows.Forms.Panel();
            this.grpSurvivalOptions = new System.Windows.Forms.GroupBox();
            this.chkSurvivalUseSeriesColourForConfidenceIntervals = new System.Windows.Forms.CheckBox();
            this.chkSurvivalShowEventMarkers = new System.Windows.Forms.CheckBox();
            this.chkSurvivalShowCensorshipTics = new System.Windows.Forms.CheckBox();
            this.ctlBoxWhiskerOptions1 = new StatsDirect.UI.ctlBoxWhiskerOptions(SdPreferences);
            this.ctlHistogramOptions1 = new StatsDirect.UI.ctlHistogramOptions();
            this.tlpScatterXYOptions = new System.Windows.Forms.TableLayoutPanel();
            this.chkScatterXYPlotMarkers = new System.Windows.Forms.CheckBox();
            this.chkScatterXYPlotLines = new System.Windows.Forms.CheckBox();
            this.chkShouldCheckForOffsets = new System.Windows.Forms.CheckBox();
            this.cmdRocEditCutoffs = new System.Windows.Forms.Button();
            this.tlpDisplay.SuspendLayout();
            this.pnlLegendFont.SuspendLayout();
            this.pnlSeriesLabelFont.SuspendLayout();
            this.pnlSeriesOptions.SuspendLayout();
            this.pnlColour.SuspendLayout();
            this.grpColour.SuspendLayout();
            this.pnlShowLegend.SuspendLayout();
            this.pnlTitleFont.SuspendLayout();
            this.pnlAxisTitleFont.SuspendLayout();
            this.pnlAxisLabelFont.SuspendLayout();
            this.pnlAxisLineThickness.SuspendLayout();
            this.pnlBoxAxes.SuspendLayout();
            this.pnlChartTitle.SuspendLayout();
            this.pnlSeriesLabels.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridSeriesLabels)).BeginInit();
            this.pnlControlOptions.SuspendLayout();
            this.grpControlOptions.SuspendLayout();
            this.tlpOuter.SuspendLayout();
            this.tlpFunction.SuspendLayout();
            this.tlpCustom.SuspendLayout();
            this.pnlPreview.SuspendLayout();
            this.pnlOrientation.SuspendLayout();
            this.grpOrientation.SuspendLayout();
            this.pnlBarOptions.SuspendLayout();
            this.tlpBarOptions.SuspendLayout();
            this.pnlBarOptionsNonStacked.SuspendLayout();
            this.pnlForestOptions.SuspendLayout();
            this.grpForestOptions.SuspendLayout();
            this.pnlNormalOptions.SuspendLayout();
            this.grpNormalScaling.SuspendLayout();
            this.grpNormalOptions.SuspendLayout();
            this.pnlPyramidOptions.SuspendLayout();
            this.pnlRocOptions.SuspendLayout();
            this.grpRocOptions.SuspendLayout();
            this.pnlSurvivalOptions.SuspendLayout();
            this.grpSurvivalOptions.SuspendLayout();
            this.tlpScatterXYOptions.SuspendLayout();
            this.SuspendLayout();
            // 
            // tlpDisplay
            // 
            this.tlpDisplay.AutoSize = true;
            this.tlpDisplay.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpDisplay.ColumnCount = 1;
            this.tlpDisplay.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpDisplay.Controls.Add(this.pnlLegendFont, 0, 2);
            this.tlpDisplay.Controls.Add(this.pnlSeriesLabelFont, 0, 3);
            this.tlpDisplay.Controls.Add(this.pnlSeriesOptions, 0, 4);
            this.tlpDisplay.Controls.Add(this.pnlColour, 0, 0);
            this.tlpDisplay.Controls.Add(this.pnlShowLegend, 0, 1);
            this.tlpDisplay.Location = new System.Drawing.Point(248, 0);
            this.tlpDisplay.Margin = new System.Windows.Forms.Padding(0);
            this.tlpDisplay.Name = "tlpDisplay";
            this.tlpDisplay.RowCount = 6;
            this.tlpDisplay.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDisplay.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDisplay.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDisplay.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDisplay.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDisplay.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDisplay.Size = new System.Drawing.Size(206, 431);
            this.tlpDisplay.TabIndex = 1;
            // 
            // pnlLegendFont
            // 
            this.pnlLegendFont.AutoSize = true;
            this.pnlLegendFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlLegendFont.Controls.Add(this.ctlLegendFont);
            this.pnlLegendFont.Location = new System.Drawing.Point(0, 76);
            this.pnlLegendFont.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.pnlLegendFont.Name = "pnlLegendFont";
            this.pnlLegendFont.Size = new System.Drawing.Size(206, 34);
            this.pnlLegendFont.TabIndex = 9;
            // 
            // ctlLegendFont
            // 
            this.ctlLegendFont.AutoSize = true;
            this.ctlLegendFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlLegendFont.Location = new System.Drawing.Point(3, 3);
            this.ctlLegendFont.Margin = new System.Windows.Forms.Padding(0);
            this.ctlLegendFont.Name = "ctlLegendFont";
            this.ctlLegendFont.Purpose = "Legend font";
            this.ctlLegendFont.Size = new System.Drawing.Size(203, 31);
            this.ctlLegendFont.TabIndex = 50;
            this.ctlLegendFont.UserFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            // 
            // pnlSeriesLabelFont
            // 
            this.pnlSeriesLabelFont.AutoSize = true;
            this.pnlSeriesLabelFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlSeriesLabelFont.Controls.Add(this.ctlSeriesLabelFont);
            this.pnlSeriesLabelFont.Location = new System.Drawing.Point(0, 116);
            this.pnlSeriesLabelFont.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.pnlSeriesLabelFont.Name = "pnlSeriesLabelFont";
            this.pnlSeriesLabelFont.Size = new System.Drawing.Size(206, 34);
            this.pnlSeriesLabelFont.TabIndex = 10;
            // 
            // ctlSeriesLabelFont
            // 
            this.ctlSeriesLabelFont.AutoSize = true;
            this.ctlSeriesLabelFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlSeriesLabelFont.Location = new System.Drawing.Point(3, 3);
            this.ctlSeriesLabelFont.Margin = new System.Windows.Forms.Padding(0);
            this.ctlSeriesLabelFont.Name = "ctlSeriesLabelFont";
            this.ctlSeriesLabelFont.Purpose = "Variable label font";
            this.ctlSeriesLabelFont.Size = new System.Drawing.Size(203, 31);
            this.ctlSeriesLabelFont.TabIndex = 50;
            this.ctlSeriesLabelFont.UserFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            // 
            // pnlSeriesOptions
            // 
            this.pnlSeriesOptions.AutoSize = true;
            this.pnlSeriesOptions.Controls.Add(this.seriesOptions);
            this.pnlSeriesOptions.Location = new System.Drawing.Point(0, 153);
            this.pnlSeriesOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlSeriesOptions.Name = "pnlSeriesOptions";
            this.pnlSeriesOptions.Size = new System.Drawing.Size(203, 278);
            this.pnlSeriesOptions.TabIndex = 11;
            // 
            // seriesOptions
            // 
            this.seriesOptions.AutoSize = true;
            this.seriesOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.seriesOptions.ForcedFillStyle = StatsDirect.Charting.FillStyle.None;
            this.seriesOptions.ForcedIsFilled = default;
            this.seriesOptions.Location = new System.Drawing.Point(0, 0);
            this.seriesOptions.Margin = new System.Windows.Forms.Padding(0);
            this.seriesOptions.MarkerTypes = null;
            this.seriesOptions.Name = "seriesOptions";
            this.seriesOptions.SeriesOptionsDescriptors = null;
            this.seriesOptions.Size = new System.Drawing.Size(203, 278);
            this.seriesOptions.TabIndex = 0;
            // 
            // pnlColour
            // 
            this.pnlColour.AutoSize = true;
            this.pnlColour.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlColour.Controls.Add(this.grpColour);
            this.pnlColour.Location = new System.Drawing.Point(0, 0);
            this.pnlColour.Margin = new System.Windows.Forms.Padding(0);
            this.pnlColour.Name = "pnlColour";
            this.pnlColour.Size = new System.Drawing.Size(206, 50);
            this.pnlColour.TabIndex = 8;
            // 
            // grpColour
            // 
            this.grpColour.Controls.Add(this.rdoMonochrome);
            this.grpColour.Controls.Add(this.rdoColour);
            this.grpColour.Location = new System.Drawing.Point(3, 3);
            this.grpColour.Name = "grpColour";
            this.grpColour.Size = new System.Drawing.Size(200, 44);
            this.grpColour.TabIndex = 0;
            this.grpColour.TabStop = false;
            this.grpColour.Text = "Colour";
            // 
            // rdoMonochrome
            // 
            this.rdoMonochrome.AutoSize = true;
            this.rdoMonochrome.Location = new System.Drawing.Point(68, 19);
            this.rdoMonochrome.Name = "rdoMonochrome";
            this.rdoMonochrome.Size = new System.Drawing.Size(87, 17);
            this.rdoMonochrome.TabIndex = 1;
            this.rdoMonochrome.Text = "Monochrome";
            this.rdoMonochrome.UseVisualStyleBackColor = true;
            this.rdoMonochrome.CheckedChanged += new System.EventHandler(this.rdoMonochrome_CheckedChanged);
            // 
            // rdoColour
            // 
            this.rdoColour.AutoSize = true;
            this.rdoColour.Checked = true;
            this.rdoColour.Location = new System.Drawing.Point(6, 19);
            this.rdoColour.Name = "rdoColour";
            this.rdoColour.Size = new System.Drawing.Size(55, 17);
            this.rdoColour.TabIndex = 0;
            this.rdoColour.TabStop = true;
            this.rdoColour.Text = "Colour";
            this.rdoColour.UseVisualStyleBackColor = true;
            this.rdoColour.CheckedChanged += new System.EventHandler(this.rdoColour_CheckedChanged);
            // 
            // pnlShowLegend
            // 
            this.pnlShowLegend.AutoSize = true;
            this.pnlShowLegend.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlShowLegend.Controls.Add(this.chkShowLegend);
            this.pnlShowLegend.Location = new System.Drawing.Point(0, 50);
            this.pnlShowLegend.Margin = new System.Windows.Forms.Padding(0);
            this.pnlShowLegend.Name = "pnlShowLegend";
            this.pnlShowLegend.Size = new System.Drawing.Size(95, 23);
            this.pnlShowLegend.TabIndex = 9;
            // 
            // chkShowLegend
            // 
            this.chkShowLegend.AutoSize = true;
            this.chkShowLegend.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkShowLegend.Location = new System.Drawing.Point(4, 3);
            this.chkShowLegend.Name = "chkShowLegend";
            this.chkShowLegend.Size = new System.Drawing.Size(88, 17);
            this.chkShowLegend.TabIndex = 0;
            this.chkShowLegend.Text = "Show legend";
            this.chkShowLegend.UseVisualStyleBackColor = true;
            // 
            // pnlTitleFont
            // 
            this.pnlTitleFont.AutoSize = true;
            this.pnlTitleFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlTitleFont.Controls.Add(this.ctlTitleFont);
            this.pnlTitleFont.Location = new System.Drawing.Point(0, 31);
            this.pnlTitleFont.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.pnlTitleFont.Name = "pnlTitleFont";
            this.pnlTitleFont.Size = new System.Drawing.Size(150, 34);
            this.pnlTitleFont.TabIndex = 1;
            // 
            // ctlTitleFont
            // 
            this.ctlTitleFont.AutoSize = true;
            this.ctlTitleFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlTitleFont.Location = new System.Drawing.Point(3, 3);
            this.ctlTitleFont.Margin = new System.Windows.Forms.Padding(0);
            this.ctlTitleFont.Name = "ctlTitleFont";
            this.ctlTitleFont.Purpose = "Title font";
            this.ctlTitleFont.Size = new System.Drawing.Size(147, 31);
            this.ctlTitleFont.TabIndex = 1;
            this.ctlTitleFont.UserFont = null;
            // 
            // pnlAxisTitleFont
            // 
            this.pnlAxisTitleFont.AutoSize = true;
            this.pnlAxisTitleFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlAxisTitleFont.Controls.Add(this.ctlAxisTitleFont);
            this.pnlAxisTitleFont.Location = new System.Drawing.Point(0, 121);
            this.pnlAxisTitleFont.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.pnlAxisTitleFont.Name = "pnlAxisTitleFont";
            this.pnlAxisTitleFont.Size = new System.Drawing.Size(206, 34);
            this.pnlAxisTitleFont.TabIndex = 4;
            // 
            // ctlAxisTitleFont
            // 
            this.ctlAxisTitleFont.AutoSize = true;
            this.ctlAxisTitleFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlAxisTitleFont.Location = new System.Drawing.Point(3, 3);
            this.ctlAxisTitleFont.Margin = new System.Windows.Forms.Padding(0);
            this.ctlAxisTitleFont.Name = "ctlAxisTitleFont";
            this.ctlAxisTitleFont.Purpose = "Axis title font";
            this.ctlAxisTitleFont.Size = new System.Drawing.Size(203, 31);
            this.ctlAxisTitleFont.TabIndex = 50;
            this.ctlAxisTitleFont.UserFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            // 
            // pnlAxisLabelFont
            // 
            this.pnlAxisLabelFont.AutoSize = true;
            this.pnlAxisLabelFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlAxisLabelFont.Controls.Add(this.ctlAxisLabelFont);
            this.pnlAxisLabelFont.Location = new System.Drawing.Point(0, 161);
            this.pnlAxisLabelFont.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.pnlAxisLabelFont.Name = "pnlAxisLabelFont";
            this.pnlAxisLabelFont.Size = new System.Drawing.Size(206, 34);
            this.pnlAxisLabelFont.TabIndex = 5;
            // 
            // ctlAxisLabelFont
            // 
            this.ctlAxisLabelFont.AutoSize = true;
            this.ctlAxisLabelFont.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlAxisLabelFont.Location = new System.Drawing.Point(3, 3);
            this.ctlAxisLabelFont.Margin = new System.Windows.Forms.Padding(0);
            this.ctlAxisLabelFont.Name = "ctlAxisLabelFont";
            this.ctlAxisLabelFont.Purpose = "Axis label font";
            this.ctlAxisLabelFont.Size = new System.Drawing.Size(203, 31);
            this.ctlAxisLabelFont.TabIndex = 50;
            this.ctlAxisLabelFont.UserFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            // 
            // pnlAxisLineThickness
            // 
            this.pnlAxisLineThickness.AutoSize = true;
            this.pnlAxisLineThickness.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlAxisLineThickness.Controls.Add(this.lblAxisLineThickness);
            this.pnlAxisLineThickness.Controls.Add(this.ctlAxisLineThickness);
            this.pnlAxisLineThickness.Location = new System.Drawing.Point(0, 92);
            this.pnlAxisLineThickness.Margin = new System.Windows.Forms.Padding(0);
            this.pnlAxisLineThickness.Name = "pnlAxisLineThickness";
            this.pnlAxisLineThickness.Size = new System.Drawing.Size(214, 26);
            this.pnlAxisLineThickness.TabIndex = 3;
            // 
            // lblAxisLineThickness
            // 
            this.lblAxisLineThickness.AutoSize = true;
            this.lblAxisLineThickness.Location = new System.Drawing.Point(6, 8);
            this.lblAxisLineThickness.Name = "lblAxisLineThickness";
            this.lblAxisLineThickness.Size = new System.Drawing.Size(93, 13);
            this.lblAxisLineThickness.TabIndex = 1;
            this.lblAxisLineThickness.Text = "Axis line thickness";
            // 
            // ctlAxisLineThickness
            // 
            this.ctlAxisLineThickness.LineThickness = 1;
            this.ctlAxisLineThickness.Location = new System.Drawing.Point(102, 5);
            this.ctlAxisLineThickness.Margin = new System.Windows.Forms.Padding(0);
            this.ctlAxisLineThickness.MaximumSize = new System.Drawing.Size(10000, 21);
            this.ctlAxisLineThickness.MinimumSize = new System.Drawing.Size(100, 21);
            this.ctlAxisLineThickness.Name = "ctlAxisLineThickness";
            this.ctlAxisLineThickness.Size = new System.Drawing.Size(112, 21);
            this.ctlAxisLineThickness.TabIndex = 0;
            // 
            // pnlBoxAxes
            // 
            this.pnlBoxAxes.AutoSize = true;
            this.pnlBoxAxes.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlBoxAxes.Controls.Add(this.chkBoxAxes);
            this.pnlBoxAxes.Location = new System.Drawing.Point(0, 68);
            this.pnlBoxAxes.Margin = new System.Windows.Forms.Padding(0);
            this.pnlBoxAxes.Name = "pnlBoxAxes";
            this.pnlBoxAxes.Size = new System.Drawing.Size(181, 24);
            this.pnlBoxAxes.TabIndex = 2;
            // 
            // chkBoxAxes
            // 
            this.chkBoxAxes.AutoSize = true;
            this.chkBoxAxes.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkBoxAxes.Location = new System.Drawing.Point(6, 4);
            this.chkBoxAxes.Name = "chkBoxAxes";
            this.chkBoxAxes.Size = new System.Drawing.Size(172, 17);
            this.chkBoxAxes.TabIndex = 2;
            this.chkBoxAxes.Text = "Box in axes (lines top and right)";
            this.chkBoxAxes.UseVisualStyleBackColor = true;
            // 
            // pnlChartTitle
            // 
            this.pnlChartTitle.Controls.Add(this.txtChartTitle);
            this.pnlChartTitle.Controls.Add(this.lblChartTitle);
            this.pnlChartTitle.Location = new System.Drawing.Point(0, 0);
            this.pnlChartTitle.Margin = new System.Windows.Forms.Padding(0);
            this.pnlChartTitle.Name = "pnlChartTitle";
            this.pnlChartTitle.Size = new System.Drawing.Size(243, 28);
            this.pnlChartTitle.TabIndex = 0;
            // 
            // txtChartTitle
            // 
            this.txtChartTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtChartTitle.Location = new System.Drawing.Point(72, 4);
            this.txtChartTitle.Name = "txtChartTitle";
            this.txtChartTitle.Size = new System.Drawing.Size(168, 20);
            this.txtChartTitle.TabIndex = 0;
            // 
            // lblChartTitle
            // 
            this.lblChartTitle.AutoSize = true;
            this.lblChartTitle.Location = new System.Drawing.Point(3, 6);
            this.lblChartTitle.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
            this.lblChartTitle.Name = "lblChartTitle";
            this.lblChartTitle.Size = new System.Drawing.Size(30, 13);
            this.lblChartTitle.TabIndex = 0;
            this.lblChartTitle.Text = "Title:";
            // 
            // pnlSeriesLabels
            // 
            this.pnlSeriesLabels.AutoSize = true;
            this.pnlSeriesLabels.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlSeriesLabels.Controls.Add(this.gridSeriesLabels);
            this.pnlSeriesLabels.Controls.Add(this.lblSeriesLabels);
            this.pnlSeriesLabels.Location = new System.Drawing.Point(0, 198);
            this.pnlSeriesLabels.Margin = new System.Windows.Forms.Padding(0);
            this.pnlSeriesLabels.Name = "pnlSeriesLabels";
            this.pnlSeriesLabels.Size = new System.Drawing.Size(246, 171);
            this.pnlSeriesLabels.TabIndex = 6;
            // 
            // gridSeriesLabels
            // 
            this.gridSeriesLabels.AllowUserToAddRows = false;
            this.gridSeriesLabels.AllowUserToDeleteRows = false;
            this.gridSeriesLabels.AllowUserToResizeColumns = false;
            this.gridSeriesLabels.AllowUserToResizeRows = false;
            this.gridSeriesLabels.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridSeriesLabels.ColumnHeadersVisible = false;
            this.gridSeriesLabels.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Title});
            this.gridSeriesLabels.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridSeriesLabels.Location = new System.Drawing.Point(3, 20);
            this.gridSeriesLabels.MultiSelect = false;
            this.gridSeriesLabels.Name = "gridSeriesLabels";
            this.gridSeriesLabels.RowHeadersVisible = false;
            this.gridSeriesLabels.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.gridSeriesLabels.Size = new System.Drawing.Size(240, 148);
            this.gridSeriesLabels.TabIndex = 5;
            // 
            // Title
            // 
            this.Title.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Title.HeaderText = "Title";
            this.Title.Name = "Title";
            this.Title.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // lblSeriesLabels
            // 
            this.lblSeriesLabels.AutoSize = true;
            this.lblSeriesLabels.Location = new System.Drawing.Point(3, 4);
            this.lblSeriesLabels.Name = "lblSeriesLabels";
            this.lblSeriesLabels.Size = new System.Drawing.Size(78, 13);
            this.lblSeriesLabels.TabIndex = 0;
            this.lblSeriesLabels.Text = "Variable labels:";
            // 
            // pnlControlOptions
            // 
            this.pnlControlOptions.AutoSize = true;
            this.pnlControlOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlControlOptions.Controls.Add(this.grpControlOptions);
            this.pnlControlOptions.Location = new System.Drawing.Point(0, 166);
            this.pnlControlOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlControlOptions.Name = "pnlControlOptions";
            this.pnlControlOptions.Size = new System.Drawing.Size(405, 262);
            this.pnlControlOptions.TabIndex = 15;
            // 
            // grpControlOptions
            // 
            this.grpControlOptions.Controls.Add(this.lblControlDecimalPlaces);
            this.grpControlOptions.Controls.Add(this.lblControlLabelLinesTo);
            this.grpControlOptions.Controls.Add(this.cboControlDecimalPlaces);
            this.grpControlOptions.Controls.Add(this.lblUpperControl);
            this.grpControlOptions.Controls.Add(this.txtUpperControl);
            this.grpControlOptions.Controls.Add(this.lblLowerControl);
            this.grpControlOptions.Controls.Add(this.txtLowerControl);
            this.grpControlOptions.Controls.Add(this.lblUpperWarning);
            this.grpControlOptions.Controls.Add(this.txtUpperWarning);
            this.grpControlOptions.Controls.Add(this.lblLowerWarning);
            this.grpControlOptions.Controls.Add(this.txtLowerWarning);
            this.grpControlOptions.Controls.Add(this.chkHasUserSpecifiedLimits);
            this.grpControlOptions.Controls.Add(this.lblSD);
            this.grpControlOptions.Controls.Add(this.txtStandardDeviation);
            this.grpControlOptions.Controls.Add(this.lblMean);
            this.grpControlOptions.Controls.Add(this.txtMean);
            this.grpControlOptions.Controls.Add(this.chkHasUserSpecifiedMeanAndSD);
            this.grpControlOptions.Controls.Add(this.lblObservations);
            this.grpControlOptions.Controls.Add(this.lblUseFirst);
            this.grpControlOptions.Controls.Add(this.cboKObs);
            this.grpControlOptions.Controls.Add(this.chk3SD);
            this.grpControlOptions.Controls.Add(this.chk2SD);
            this.grpControlOptions.Controls.Add(this.chk1SD);
            this.grpControlOptions.Controls.Add(this.chkMean);
            this.grpControlOptions.Location = new System.Drawing.Point(3, 3);
            this.grpControlOptions.Name = "grpControlOptions";
            this.grpControlOptions.Size = new System.Drawing.Size(399, 256);
            this.grpControlOptions.TabIndex = 0;
            this.grpControlOptions.TabStop = false;
            this.grpControlOptions.Text = "Control chart lines";
            // 
            // lblControlDecimalPlaces
            // 
            this.lblControlDecimalPlaces.AutoSize = true;
            this.lblControlDecimalPlaces.Location = new System.Drawing.Point(130, 228);
            this.lblControlDecimalPlaces.Name = "lblControlDecimalPlaces";
            this.lblControlDecimalPlaces.Size = new System.Drawing.Size(77, 13);
            this.lblControlDecimalPlaces.TabIndex = 23;
            this.lblControlDecimalPlaces.Text = "decimal places";
            // 
            // lblControlLabelLinesTo
            // 
            this.lblControlLabelLinesTo.AutoSize = true;
            this.lblControlLabelLinesTo.Location = new System.Drawing.Point(6, 228);
            this.lblControlLabelLinesTo.Name = "lblControlLabelLinesTo";
            this.lblControlLabelLinesTo.Size = new System.Drawing.Size(69, 13);
            this.lblControlLabelLinesTo.TabIndex = 22;
            this.lblControlLabelLinesTo.Text = "Label lines to";
            // 
            // cboControlDecimalPlaces
            // 
            this.cboControlDecimalPlaces.FormattingEnabled = true;
            this.cboControlDecimalPlaces.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14"});
            this.cboControlDecimalPlaces.Location = new System.Drawing.Point(75, 225);
            this.cboControlDecimalPlaces.Name = "cboControlDecimalPlaces";
            this.cboControlDecimalPlaces.Size = new System.Drawing.Size(49, 21);
            this.cboControlDecimalPlaces.TabIndex = 21;
            // 
            // lblUpperControl
            // 
            this.lblUpperControl.AutoSize = true;
            this.lblUpperControl.Location = new System.Drawing.Point(203, 201);
            this.lblUpperControl.Name = "lblUpperControl";
            this.lblUpperControl.Size = new System.Drawing.Size(74, 13);
            this.lblUpperControl.TabIndex = 20;
            this.lblUpperControl.Text = "Upper control:";
            // 
            // txtUpperControl
            // 
            this.txtUpperControl.Location = new System.Drawing.Point(288, 198);
            this.txtUpperControl.Name = "txtUpperControl";
            this.txtUpperControl.Size = new System.Drawing.Size(100, 20);
            this.txtUpperControl.TabIndex = 19;
            // 
            // lblLowerControl
            // 
            this.lblLowerControl.AutoSize = true;
            this.lblLowerControl.Location = new System.Drawing.Point(3, 201);
            this.lblLowerControl.Name = "lblLowerControl";
            this.lblLowerControl.Size = new System.Drawing.Size(74, 13);
            this.lblLowerControl.TabIndex = 18;
            this.lblLowerControl.Text = "Lower control:";
            // 
            // txtLowerControl
            // 
            this.txtLowerControl.Location = new System.Drawing.Point(88, 198);
            this.txtLowerControl.Name = "txtLowerControl";
            this.txtLowerControl.Size = new System.Drawing.Size(100, 20);
            this.txtLowerControl.TabIndex = 17;
            // 
            // lblUpperWarning
            // 
            this.lblUpperWarning.AutoSize = true;
            this.lblUpperWarning.Location = new System.Drawing.Point(203, 175);
            this.lblUpperWarning.Name = "lblUpperWarning";
            this.lblUpperWarning.Size = new System.Drawing.Size(79, 13);
            this.lblUpperWarning.TabIndex = 16;
            this.lblUpperWarning.Text = "Upper warning:";
            // 
            // txtUpperWarning
            // 
            this.txtUpperWarning.Location = new System.Drawing.Point(288, 172);
            this.txtUpperWarning.Name = "txtUpperWarning";
            this.txtUpperWarning.Size = new System.Drawing.Size(100, 20);
            this.txtUpperWarning.TabIndex = 15;
            // 
            // lblLowerWarning
            // 
            this.lblLowerWarning.AutoSize = true;
            this.lblLowerWarning.Location = new System.Drawing.Point(3, 175);
            this.lblLowerWarning.Name = "lblLowerWarning";
            this.lblLowerWarning.Size = new System.Drawing.Size(79, 13);
            this.lblLowerWarning.TabIndex = 14;
            this.lblLowerWarning.Text = "Lower warning:";
            // 
            // txtLowerWarning
            // 
            this.txtLowerWarning.Location = new System.Drawing.Point(88, 172);
            this.txtLowerWarning.Name = "txtLowerWarning";
            this.txtLowerWarning.Size = new System.Drawing.Size(100, 20);
            this.txtLowerWarning.TabIndex = 13;
            // 
            // chkHasUserSpecifiedLimits
            // 
            this.chkHasUserSpecifiedLimits.AutoSize = true;
            this.chkHasUserSpecifiedLimits.Location = new System.Drawing.Point(6, 149);
            this.chkHasUserSpecifiedLimits.Name = "chkHasUserSpecifiedLimits";
            this.chkHasUserSpecifiedLimits.Size = new System.Drawing.Size(182, 17);
            this.chkHasUserSpecifiedLimits.TabIndex = 12;
            this.chkHasUserSpecifiedLimits.Text = "Specify warning and control limits";
            this.chkHasUserSpecifiedLimits.UseVisualStyleBackColor = true;
            this.chkHasUserSpecifiedLimits.CheckedChanged += new System.EventHandler(this.chkHasUserSpecifiedLimits_CheckedChanged);
            // 
            // lblSD
            // 
            this.lblSD.AutoSize = true;
            this.lblSD.Location = new System.Drawing.Point(165, 126);
            this.lblSD.Name = "lblSD";
            this.lblSD.Size = new System.Drawing.Size(99, 13);
            this.lblSD.TabIndex = 11;
            this.lblSD.Text = "Standard deviation:";
            // 
            // txtStandardDeviation
            // 
            this.txtStandardDeviation.Location = new System.Drawing.Point(266, 123);
            this.txtStandardDeviation.Name = "txtStandardDeviation";
            this.txtStandardDeviation.Size = new System.Drawing.Size(100, 20);
            this.txtStandardDeviation.TabIndex = 10;
            // 
            // lblMean
            // 
            this.lblMean.AutoSize = true;
            this.lblMean.Location = new System.Drawing.Point(6, 126);
            this.lblMean.Name = "lblMean";
            this.lblMean.Size = new System.Drawing.Size(37, 13);
            this.lblMean.TabIndex = 9;
            this.lblMean.Text = "Mean:";
            // 
            // txtMean
            // 
            this.txtMean.Location = new System.Drawing.Point(49, 123);
            this.txtMean.Name = "txtMean";
            this.txtMean.Size = new System.Drawing.Size(100, 20);
            this.txtMean.TabIndex = 8;
            // 
            // chkHasUserSpecifiedMeanAndSD
            // 
            this.chkHasUserSpecifiedMeanAndSD.AutoSize = true;
            this.chkHasUserSpecifiedMeanAndSD.Location = new System.Drawing.Point(7, 100);
            this.chkHasUserSpecifiedMeanAndSD.Name = "chkHasUserSpecifiedMeanAndSD";
            this.chkHasUserSpecifiedMeanAndSD.Size = new System.Drawing.Size(201, 17);
            this.chkHasUserSpecifiedMeanAndSD.TabIndex = 7;
            this.chkHasUserSpecifiedMeanAndSD.Text = "Specify mean and standard deviation";
            this.chkHasUserSpecifiedMeanAndSD.UseVisualStyleBackColor = true;
            this.chkHasUserSpecifiedMeanAndSD.CheckedChanged += new System.EventHandler(this.chkHasUserSpecifiedMeanAndSD_CheckedChanged);
            // 
            // lblObservations
            // 
            this.lblObservations.AutoSize = true;
            this.lblObservations.Location = new System.Drawing.Point(147, 74);
            this.lblObservations.Name = "lblObservations";
            this.lblObservations.Size = new System.Drawing.Size(219, 13);
            this.lblObservations.TabIndex = 6;
            this.lblObservations.Text = "observations to calculate the statistics above";
            // 
            // lblUseFirst
            // 
            this.lblUseFirst.AutoSize = true;
            this.lblUseFirst.Location = new System.Drawing.Point(6, 74);
            this.lblUseFirst.Name = "lblUseFirst";
            this.lblUseFirst.Size = new System.Drawing.Size(63, 13);
            this.lblUseFirst.TabIndex = 5;
            this.lblUseFirst.Text = "Use the first";
            // 
            // cboKObs
            // 
            this.cboKObs.FormattingEnabled = true;
            this.cboKObs.Location = new System.Drawing.Point(75, 71);
            this.cboKObs.Name = "cboKObs";
            this.cboKObs.Size = new System.Drawing.Size(66, 21);
            this.cboKObs.TabIndex = 4;
            // 
            // chk3SD
            // 
            this.chk3SD.AutoSize = true;
            this.chk3SD.Location = new System.Drawing.Point(148, 43);
            this.chk3SD.Name = "chk3SD";
            this.chk3SD.Size = new System.Drawing.Size(140, 17);
            this.chk3SD.TabIndex = 3;
            this.chk3SD.Text = "± 3 Standard Deviations";
            this.chk3SD.UseVisualStyleBackColor = true;
            // 
            // chk2SD
            // 
            this.chk2SD.AutoSize = true;
            this.chk2SD.Location = new System.Drawing.Point(148, 20);
            this.chk2SD.Name = "chk2SD";
            this.chk2SD.Size = new System.Drawing.Size(140, 17);
            this.chk2SD.TabIndex = 2;
            this.chk2SD.Text = "± 2 Standard Deviations";
            this.chk2SD.UseVisualStyleBackColor = true;
            // 
            // chk1SD
            // 
            this.chk1SD.AutoSize = true;
            this.chk1SD.Location = new System.Drawing.Point(7, 43);
            this.chk1SD.Name = "chk1SD";
            this.chk1SD.Size = new System.Drawing.Size(135, 17);
            this.chk1SD.TabIndex = 1;
            this.chk1SD.Text = "± 1 Standard Deviation";
            this.chk1SD.UseVisualStyleBackColor = true;
            // 
            // chkMean
            // 
            this.chkMean.AutoSize = true;
            this.chkMean.Location = new System.Drawing.Point(7, 20);
            this.chkMean.Name = "chkMean";
            this.chkMean.Size = new System.Drawing.Size(53, 17);
            this.chkMean.TabIndex = 0;
            this.chkMean.Text = "Mean";
            this.chkMean.UseVisualStyleBackColor = true;
            // 
            // tlpOuter
            // 
            this.tlpOuter.AutoSize = true;
            this.tlpOuter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpOuter.ColumnCount = 3;
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.Controls.Add(this.tlpDisplay, 1, 0);
            this.tlpOuter.Controls.Add(this.tlpFunction, 0, 0);
            this.tlpOuter.Controls.Add(this.tlpCustom, 2, 0);
            this.tlpOuter.Location = new System.Drawing.Point(0, 0);
            this.tlpOuter.Margin = new System.Windows.Forms.Padding(0);
            this.tlpOuter.Name = "tlpOuter";
            this.tlpOuter.RowCount = 1;
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpOuter.Size = new System.Drawing.Size(859, 1585);
            this.tlpOuter.TabIndex = 1;
            // 
            // tlpFunction
            // 
            this.tlpFunction.AutoSize = true;
            this.tlpFunction.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpFunction.ColumnCount = 1;
            this.tlpFunction.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpFunction.Controls.Add(this.pnlChartTitle, 0, 0);
            this.tlpFunction.Controls.Add(this.pnlTitleFont, 0, 1);
            this.tlpFunction.Controls.Add(this.pnlBoxAxes, 0, 2);
            this.tlpFunction.Controls.Add(this.pnlAxisLineThickness, 0, 3);
            this.tlpFunction.Controls.Add(this.pnlAxisTitleFont, 0, 4);
            this.tlpFunction.Controls.Add(this.pnlAxisLabelFont, 0, 5);
            this.tlpFunction.Controls.Add(this.pnlSeriesLabels, 0, 6);
            this.tlpFunction.Controls.Add(this.ctlAxisOptions, 0, 17);
            this.tlpFunction.Location = new System.Drawing.Point(0, 0);
            this.tlpFunction.Margin = new System.Windows.Forms.Padding(0);
            this.tlpFunction.Name = "tlpFunction";
            this.tlpFunction.RowCount = 18;
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpFunction.Size = new System.Drawing.Size(248, 620);
            this.tlpFunction.TabIndex = 0;
            // 
            // ctlAxisOptions
            // 
            this.ctlAxisOptions.AutoSize = true;
            this.ctlAxisOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlAxisOptions.AxisLabelsAreSwapped = false;
            this.ctlAxisOptions.Location = new System.Drawing.Point(4, 374);
            this.ctlAxisOptions.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ctlAxisOptions.Name = "ctlAxisOptions";
            this.ctlAxisOptions.ShowX = true;
            this.ctlAxisOptions.ShowY = true;
            this.ctlAxisOptions.Size = new System.Drawing.Size(240, 241);
            this.ctlAxisOptions.TabIndex = 7;
            // 
            // tlpCustom
            // 
            this.tlpCustom.AutoSize = true;
            this.tlpCustom.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpCustom.ColumnCount = 1;
            this.tlpCustom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpCustom.Controls.Add(this.pnlPreview, 0, 11);
            this.tlpCustom.Controls.Add(this.pnlOrientation, 0, 0);
            this.tlpCustom.Controls.Add(this.pnlBarOptions, 0, 1);
            this.tlpCustom.Controls.Add(this.pnlControlOptions, 0, 2);
            this.tlpCustom.Controls.Add(this.pnlForestOptions, 0, 3);
            this.tlpCustom.Controls.Add(this.pnlNormalOptions, 0, 4);
            this.tlpCustom.Controls.Add(this.pnlPyramidOptions, 0, 5);
            this.tlpCustom.Controls.Add(this.pnlRocOptions, 0, 7);
            this.tlpCustom.Controls.Add(this.pnlSurvivalOptions, 0, 8);
            this.tlpCustom.Controls.Add(this.ctlBoxWhiskerOptions1, 0, 9);
            this.tlpCustom.Controls.Add(this.ctlHistogramOptions1, 0, 10);
            this.tlpCustom.Controls.Add(this.tlpScatterXYOptions, 0, 6);
            this.tlpCustom.Location = new System.Drawing.Point(454, 0);
            this.tlpCustom.Margin = new System.Windows.Forms.Padding(0);
            this.tlpCustom.Name = "tlpCustom";
            this.tlpCustom.RowCount = 12;
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpCustom.Size = new System.Drawing.Size(405, 1585);
            this.tlpCustom.TabIndex = 2;
            // 
            // pnlPreview
            // 
            this.pnlPreview.AutoSize = true;
            this.pnlPreview.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlPreview.Controls.Add(this.cmdPreview);
            this.pnlPreview.Location = new System.Drawing.Point(0, 1555);
            this.pnlPreview.Margin = new System.Windows.Forms.Padding(0);
            this.pnlPreview.Name = "pnlPreview";
            this.pnlPreview.Size = new System.Drawing.Size(82, 30);
            this.pnlPreview.TabIndex = 24;
            // 
            // cmdPreview
            // 
            this.cmdPreview.Location = new System.Drawing.Point(4, 4);
            this.cmdPreview.Name = "cmdPreview";
            this.cmdPreview.Size = new System.Drawing.Size(75, 23);
            this.cmdPreview.TabIndex = 0;
            this.cmdPreview.Text = "Preview";
            this.cmdPreview.UseVisualStyleBackColor = true;
            this.cmdPreview.Click += new System.EventHandler(this.cmdPreview_Click);
            // 
            // pnlOrientation
            // 
            this.pnlOrientation.AutoSize = true;
            this.pnlOrientation.Controls.Add(this.grpOrientation);
            this.pnlOrientation.Location = new System.Drawing.Point(0, 0);
            this.pnlOrientation.Margin = new System.Windows.Forms.Padding(0);
            this.pnlOrientation.Name = "pnlOrientation";
            this.pnlOrientation.Size = new System.Drawing.Size(258, 50);
            this.pnlOrientation.TabIndex = 13;
            // 
            // grpOrientation
            // 
            this.grpOrientation.Controls.Add(this.rdoOrientationVertical);
            this.grpOrientation.Controls.Add(this.rdoOrientationHorizontal);
            this.grpOrientation.Location = new System.Drawing.Point(3, 3);
            this.grpOrientation.Name = "grpOrientation";
            this.grpOrientation.Size = new System.Drawing.Size(252, 44);
            this.grpOrientation.TabIndex = 0;
            this.grpOrientation.TabStop = false;
            this.grpOrientation.Text = "Orientation";
            // 
            // rdoOrientationVertical
            // 
            this.rdoOrientationVertical.AutoSize = true;
            this.rdoOrientationVertical.Location = new System.Drawing.Point(84, 19);
            this.rdoOrientationVertical.Name = "rdoOrientationVertical";
            this.rdoOrientationVertical.Size = new System.Drawing.Size(60, 17);
            this.rdoOrientationVertical.TabIndex = 1;
            this.rdoOrientationVertical.TabStop = true;
            this.rdoOrientationVertical.Text = "Vertical";
            this.rdoOrientationVertical.UseVisualStyleBackColor = true;
            this.rdoOrientationVertical.CheckedChanged += new System.EventHandler(this.rdoOrientationVertical_CheckedChanged);
            // 
            // rdoOrientationHorizontal
            // 
            this.rdoOrientationHorizontal.AutoSize = true;
            this.rdoOrientationHorizontal.Location = new System.Drawing.Point(6, 19);
            this.rdoOrientationHorizontal.Name = "rdoOrientationHorizontal";
            this.rdoOrientationHorizontal.Size = new System.Drawing.Size(72, 17);
            this.rdoOrientationHorizontal.TabIndex = 0;
            this.rdoOrientationHorizontal.TabStop = true;
            this.rdoOrientationHorizontal.Text = "Horizontal";
            this.rdoOrientationHorizontal.UseVisualStyleBackColor = true;
            this.rdoOrientationHorizontal.CheckedChanged += new System.EventHandler(this.rdoOrientationHorizontal_CheckedChanged);
            // 
            // pnlBarOptions
            // 
            this.pnlBarOptions.AutoSize = true;
            this.pnlBarOptions.Controls.Add(this.tlpBarOptions);
            this.pnlBarOptions.Location = new System.Drawing.Point(0, 50);
            this.pnlBarOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlBarOptions.Name = "pnlBarOptions";
            this.pnlBarOptions.Size = new System.Drawing.Size(260, 116);
            this.pnlBarOptions.TabIndex = 14;
            // 
            // tlpBarOptions
            // 
            this.tlpBarOptions.AutoSize = true;
            this.tlpBarOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpBarOptions.ColumnCount = 1;
            this.tlpBarOptions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpBarOptions.Controls.Add(this.pnlBarOptionsNonStacked, 0, 1);
            this.tlpBarOptions.Controls.Add(this.ctlBarOptions, 0, 0);
            this.tlpBarOptions.Location = new System.Drawing.Point(0, 0);
            this.tlpBarOptions.Margin = new System.Windows.Forms.Padding(0);
            this.tlpBarOptions.Name = "tlpBarOptions";
            this.tlpBarOptions.RowCount = 2;
            this.tlpBarOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpBarOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpBarOptions.Size = new System.Drawing.Size(260, 116);
            this.tlpBarOptions.TabIndex = 4;
            // 
            // pnlBarOptionsNonStacked
            // 
            this.pnlBarOptionsNonStacked.AutoSize = true;
            this.pnlBarOptionsNonStacked.Controls.Add(this.lblBarBarWidthPercent2);
            this.pnlBarOptionsNonStacked.Controls.Add(this.lblBarBarWidthPercent1);
            this.pnlBarOptionsNonStacked.Controls.Add(this.txtBarBarWidthPercent);
            this.pnlBarOptionsNonStacked.Location = new System.Drawing.Point(0, 90);
            this.pnlBarOptionsNonStacked.Margin = new System.Windows.Forms.Padding(0);
            this.pnlBarOptionsNonStacked.Name = "pnlBarOptionsNonStacked";
            this.pnlBarOptionsNonStacked.Size = new System.Drawing.Size(255, 26);
            this.pnlBarOptionsNonStacked.TabIndex = 3;
            // 
            // lblBarBarWidthPercent2
            // 
            this.lblBarBarWidthPercent2.AutoSize = true;
            this.lblBarBarWidthPercent2.Location = new System.Drawing.Point(175, 6);
            this.lblBarBarWidthPercent2.Name = "lblBarBarWidthPercent2";
            this.lblBarBarWidthPercent2.Size = new System.Drawing.Size(77, 13);
            this.lblBarBarWidthPercent2.TabIndex = 2;
            this.lblBarBarWidthPercent2.Text = "% of the space";
            // 
            // lblBarBarWidthPercent1
            // 
            this.lblBarBarWidthPercent1.AutoSize = true;
            this.lblBarBarWidthPercent1.Location = new System.Drawing.Point(4, 6);
            this.lblBarBarWidthPercent1.Name = "lblBarBarWidthPercent1";
            this.lblBarBarWidthPercent1.Size = new System.Drawing.Size(131, 13);
            this.lblBarBarWidthPercent1.TabIndex = 0;
            this.lblBarBarWidthPercent1.Text = "Bars cannot be wider than";
            // 
            // txtBarBarWidthPercent
            // 
            this.txtBarBarWidthPercent.Location = new System.Drawing.Point(135, 3);
            this.txtBarBarWidthPercent.Name = "txtBarBarWidthPercent";
            this.txtBarBarWidthPercent.Size = new System.Drawing.Size(34, 20);
            this.txtBarBarWidthPercent.TabIndex = 1;
            // 
            // ctlBarOptions
            // 
            this.ctlBarOptions.AutoSize = true;
            this.ctlBarOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlBarOptions.Location = new System.Drawing.Point(0, 0);
            this.ctlBarOptions.Margin = new System.Windows.Forms.Padding(0);
            this.ctlBarOptions.MinimumSize = new System.Drawing.Size(100, 21);
            this.ctlBarOptions.Name = "ctlBarOptions";
            this.ctlBarOptions.Size = new System.Drawing.Size(260, 90);
            this.ctlBarOptions.TabIndex = 4;
            this.ctlBarOptions.BarTypeChanged += new System.EventHandler(this.ctlBarOptions_BarTypeChanged);
            // 
            // pnlForestOptions
            // 
            this.pnlForestOptions.AutoSize = true;
            this.pnlForestOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlForestOptions.Controls.Add(this.grpForestOptions);
            this.pnlForestOptions.Location = new System.Drawing.Point(0, 428);
            this.pnlForestOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlForestOptions.Name = "pnlForestOptions";
            this.pnlForestOptions.Size = new System.Drawing.Size(305, 59);
            this.pnlForestOptions.TabIndex = 16;
            // 
            // grpForestOptions
            // 
            this.grpForestOptions.Controls.Add(this.chkForestMarkCentres);
            this.grpForestOptions.Controls.Add(this.lblForestDecimalPlaces);
            this.grpForestOptions.Controls.Add(this.lblForestLabelEffectSizesTo);
            this.grpForestOptions.Controls.Add(this.cboForestDecimalPlaces);
            this.grpForestOptions.Location = new System.Drawing.Point(0, 0);
            this.grpForestOptions.Margin = new System.Windows.Forms.Padding(0);
            this.grpForestOptions.Name = "grpForestOptions";
            this.grpForestOptions.Size = new System.Drawing.Size(305, 59);
            this.grpForestOptions.TabIndex = 0;
            this.grpForestOptions.TabStop = false;
            this.grpForestOptions.Text = "Forest plot options";
            // 
            // chkForestMarkCentres
            // 
            this.chkForestMarkCentres.AutoSize = true;
            this.chkForestMarkCentres.Location = new System.Drawing.Point(9, 37);
            this.chkForestMarkCentres.Name = "chkForestMarkCentres";
            this.chkForestMarkCentres.Size = new System.Drawing.Size(88, 17);
            this.chkForestMarkCentres.TabIndex = 27;
            this.chkForestMarkCentres.Text = "Mark centres";
            this.chkForestMarkCentres.UseVisualStyleBackColor = true;
            // 
            // lblForestDecimalPlaces
            // 
            this.lblForestDecimalPlaces.AutoSize = true;
            this.lblForestDecimalPlaces.Location = new System.Drawing.Point(225, 16);
            this.lblForestDecimalPlaces.Name = "lblForestDecimalPlaces";
            this.lblForestDecimalPlaces.Size = new System.Drawing.Size(77, 13);
            this.lblForestDecimalPlaces.TabIndex = 26;
            this.lblForestDecimalPlaces.Text = "decimal places";
            // 
            // lblForestLabelEffectSizesTo
            // 
            this.lblForestLabelEffectSizesTo.AutoSize = true;
            this.lblForestLabelEffectSizesTo.Location = new System.Drawing.Point(6, 16);
            this.lblForestLabelEffectSizesTo.Name = "lblForestLabelEffectSizesTo";
            this.lblForestLabelEffectSizesTo.Size = new System.Drawing.Size(164, 13);
            this.lblForestLabelEffectSizesTo.TabIndex = 25;
            this.lblForestLabelEffectSizesTo.Text = "Label effect sizes and intervals to";
            // 
            // cboForestDecimalPlaces
            // 
            this.cboForestDecimalPlaces.FormattingEnabled = true;
            this.cboForestDecimalPlaces.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "11",
            "12",
            "13",
            "14"});
            this.cboForestDecimalPlaces.Location = new System.Drawing.Point(170, 13);
            this.cboForestDecimalPlaces.Name = "cboForestDecimalPlaces";
            this.cboForestDecimalPlaces.Size = new System.Drawing.Size(49, 21);
            this.cboForestDecimalPlaces.TabIndex = 24;
            // 
            // pnlNormalOptions
            // 
            this.pnlNormalOptions.AutoSize = true;
            this.pnlNormalOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlNormalOptions.Controls.Add(this.grpNormalScaling);
            this.pnlNormalOptions.Controls.Add(this.grpNormalOptions);
            this.pnlNormalOptions.Location = new System.Drawing.Point(0, 487);
            this.pnlNormalOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlNormalOptions.Name = "pnlNormalOptions";
            this.pnlNormalOptions.Size = new System.Drawing.Size(314, 100);
            this.pnlNormalOptions.TabIndex = 17;
            // 
            // grpNormalScaling
            // 
            this.grpNormalScaling.Controls.Add(this.rdoNormalScaled);
            this.grpNormalScaling.Controls.Add(this.rdoNormalRaw);
            this.grpNormalScaling.Location = new System.Drawing.Point(0, 50);
            this.grpNormalScaling.Margin = new System.Windows.Forms.Padding(0);
            this.grpNormalScaling.Name = "grpNormalScaling";
            this.grpNormalScaling.Size = new System.Drawing.Size(314, 50);
            this.grpNormalScaling.TabIndex = 1;
            this.grpNormalScaling.TabStop = false;
            this.grpNormalScaling.Text = "Select scaling method";
            // 
            // rdoNormalScaled
            // 
            this.rdoNormalScaled.AutoSize = true;
            this.rdoNormalScaled.Location = new System.Drawing.Point(6, 19);
            this.rdoNormalScaled.Name = "rdoNormalScaled";
            this.rdoNormalScaled.Size = new System.Drawing.Size(145, 17);
            this.rdoNormalScaled.TabIndex = 0;
            this.rdoNormalScaled.TabStop = true;
            this.rdoNormalScaled.Text = "Plot scaled normal scores";
            this.rdoNormalScaled.UseVisualStyleBackColor = true;
            this.rdoNormalScaled.CheckedChanged += new System.EventHandler(this.rdoNormalScaled_CheckedChanged);
            // 
            // rdoNormalRaw
            // 
            this.rdoNormalRaw.AutoSize = true;
            this.rdoNormalRaw.Location = new System.Drawing.Point(156, 19);
            this.rdoNormalRaw.Name = "rdoNormalRaw";
            this.rdoNormalRaw.Size = new System.Drawing.Size(131, 17);
            this.rdoNormalRaw.TabIndex = 0;
            this.rdoNormalRaw.TabStop = true;
            this.rdoNormalRaw.Text = "Plot raw normal scores";
            this.rdoNormalRaw.UseVisualStyleBackColor = true;
            this.rdoNormalRaw.CheckedChanged += new System.EventHandler(this.rdoNormalRaw_CheckedChanged);
            // 
            // grpNormalOptions
            // 
            this.grpNormalOptions.Controls.Add(this.rdoNormalExpectedNormalOrder);
            this.grpNormalOptions.Controls.Add(this.rdoNormalBlom);
            this.grpNormalOptions.Controls.Add(this.rdoNormalVanDerWaerden);
            this.grpNormalOptions.Location = new System.Drawing.Point(0, 0);
            this.grpNormalOptions.Margin = new System.Windows.Forms.Padding(0);
            this.grpNormalOptions.Name = "grpNormalOptions";
            this.grpNormalOptions.Size = new System.Drawing.Size(314, 50);
            this.grpNormalOptions.TabIndex = 0;
            this.grpNormalOptions.TabStop = false;
            this.grpNormalOptions.Text = "Select score method";
            // 
            // rdoNormalExpectedNormalOrder
            // 
            this.rdoNormalExpectedNormalOrder.AutoSize = true;
            this.rdoNormalExpectedNormalOrder.Location = new System.Drawing.Point(174, 20);
            this.rdoNormalExpectedNormalOrder.Name = "rdoNormalExpectedNormalOrder";
            this.rdoNormalExpectedNormalOrder.Size = new System.Drawing.Size(131, 17);
            this.rdoNormalExpectedNormalOrder.TabIndex = 2;
            this.rdoNormalExpectedNormalOrder.TabStop = true;
            this.rdoNormalExpectedNormalOrder.Text = "Expected normal order";
            this.rdoNormalExpectedNormalOrder.UseVisualStyleBackColor = true;
            this.rdoNormalExpectedNormalOrder.CheckedChanged += new System.EventHandler(this.rdoNormalExpectedNormalOrder_CheckedChanged);
            // 
            // rdoNormalBlom
            // 
            this.rdoNormalBlom.AutoSize = true;
            this.rdoNormalBlom.Location = new System.Drawing.Point(120, 20);
            this.rdoNormalBlom.Name = "rdoNormalBlom";
            this.rdoNormalBlom.Size = new System.Drawing.Size(48, 17);
            this.rdoNormalBlom.TabIndex = 1;
            this.rdoNormalBlom.TabStop = true;
            this.rdoNormalBlom.Text = "Blom";
            this.rdoNormalBlom.UseVisualStyleBackColor = true;
            this.rdoNormalBlom.CheckedChanged += new System.EventHandler(this.rdoNormalBlom_CheckedChanged);
            // 
            // rdoNormalVanDerWaerden
            // 
            this.rdoNormalVanDerWaerden.AutoSize = true;
            this.rdoNormalVanDerWaerden.Location = new System.Drawing.Point(6, 19);
            this.rdoNormalVanDerWaerden.Name = "rdoNormalVanDerWaerden";
            this.rdoNormalVanDerWaerden.Size = new System.Drawing.Size(108, 17);
            this.rdoNormalVanDerWaerden.TabIndex = 0;
            this.rdoNormalVanDerWaerden.TabStop = true;
            this.rdoNormalVanDerWaerden.Text = "van der Waerden";
            this.rdoNormalVanDerWaerden.UseVisualStyleBackColor = true;
            this.rdoNormalVanDerWaerden.CheckedChanged += new System.EventHandler(this.rdoNormalVanDerWaerden_CheckedChanged);
            // 
            // pnlPyramidOptions
            // 
            this.pnlPyramidOptions.AutoSize = true;
            this.pnlPyramidOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlPyramidOptions.Controls.Add(this.txtPyramidScaleMaximum);
            this.pnlPyramidOptions.Controls.Add(this.lblPyramidScaleMaximum);
            this.pnlPyramidOptions.Location = new System.Drawing.Point(0, 587);
            this.pnlPyramidOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlPyramidOptions.Name = "pnlPyramidOptions";
            this.pnlPyramidOptions.Size = new System.Drawing.Size(158, 26);
            this.pnlPyramidOptions.TabIndex = 18;
            // 
            // txtPyramidScaleMaximum
            // 
            this.txtPyramidScaleMaximum.Location = new System.Drawing.Point(90, 3);
            this.txtPyramidScaleMaximum.Name = "txtPyramidScaleMaximum";
            this.txtPyramidScaleMaximum.Size = new System.Drawing.Size(65, 20);
            this.txtPyramidScaleMaximum.TabIndex = 1;
            // 
            // lblPyramidScaleMaximum
            // 
            this.lblPyramidScaleMaximum.AutoSize = true;
            this.lblPyramidScaleMaximum.Location = new System.Drawing.Point(4, 6);
            this.lblPyramidScaleMaximum.Name = "lblPyramidScaleMaximum";
            this.lblPyramidScaleMaximum.Size = new System.Drawing.Size(80, 13);
            this.lblPyramidScaleMaximum.TabIndex = 0;
            this.lblPyramidScaleMaximum.Text = "Scale maximum";
            // 
            // pnlRocOptions
            // 
            this.pnlRocOptions.AutoSize = true;
            this.pnlRocOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlRocOptions.Controls.Add(this.grpRocOptions);
            this.pnlRocOptions.Location = new System.Drawing.Point(0, 682);
            this.pnlRocOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlRocOptions.Name = "pnlRocOptions";
            this.pnlRocOptions.Size = new System.Drawing.Size(338, 124);
            this.pnlRocOptions.TabIndex = 20;
            // 
            // grpRocOptions
            // 
            this.grpRocOptions.Controls.Add(this.cmdRocEditCutoffs);
            this.grpRocOptions.Controls.Add(this.cboRocWeight);
            this.grpRocOptions.Controls.Add(this.lblRocSensSpec);
            this.grpRocOptions.Controls.Add(this.lblRocPercent);
            this.grpRocOptions.Controls.Add(this.lblRocConfidenceInterval);
            this.grpRocOptions.Controls.Add(this.rdoRocCutOffGt);
            this.grpRocOptions.Controls.Add(this.rdoRocCutOffGe);
            this.grpRocOptions.Controls.Add(this.rdoRocCutOffLe);
            this.grpRocOptions.Controls.Add(this.rdoRocCutOffLt);
            this.grpRocOptions.Controls.Add(this.cboRocCi);
            this.grpRocOptions.Controls.Add(this.chkRocShowCutOffCalculator);
            this.grpRocOptions.Controls.Add(this.chkRocShowCutoff);
            this.grpRocOptions.Location = new System.Drawing.Point(0, 0);
            this.grpRocOptions.Margin = new System.Windows.Forms.Padding(0);
            this.grpRocOptions.Name = "grpRocOptions";
            this.grpRocOptions.Size = new System.Drawing.Size(338, 124);
            this.grpRocOptions.TabIndex = 0;
            this.grpRocOptions.TabStop = false;
            this.grpRocOptions.Text = "ROC plot options";
            // 
            // cboRocWeight
            // 
            this.cboRocWeight.FormattingEnabled = true;
            this.cboRocWeight.Items.AddRange(new object[] {
            "0.1",
            "0.2",
            "0.3",
            "0.4",
            "0.5",
            "0.6",
            "0.7",
            "0.8",
            "0.9",
            "1.0",
            "1.5",
            "2.0",
            "2.5",
            "3.0",
            "3.5",
            "4.0",
            "4.5",
            "5.0",
            "5.5",
            "6.0",
            "6.5",
            "7.0",
            "7.5",
            "8.0",
            "8.5",
            "9.0",
            "9.5",
            "10.0"});
            this.cboRocWeight.Location = new System.Drawing.Point(161, 19);
            this.cboRocWeight.Name = "cboRocWeight";
            this.cboRocWeight.Size = new System.Drawing.Size(49, 21);
            this.cboRocWeight.TabIndex = 10;
            // 
            // lblRocSensSpec
            // 
            this.lblRocSensSpec.AutoSize = true;
            this.lblRocSensSpec.Location = new System.Drawing.Point(6, 22);
            this.lblRocSensSpec.Name = "lblRocSensSpec";
            this.lblRocSensSpec.Size = new System.Drawing.Size(149, 13);
            this.lblRocSensSpec.TabIndex = 9;
            this.lblRocSensSpec.Text = "sensitivity:specificity weighting";
            // 
            // lblRocPercent
            // 
            this.lblRocPercent.AutoSize = true;
            this.lblRocPercent.Location = new System.Drawing.Point(165, 97);
            this.lblRocPercent.Name = "lblRocPercent";
            this.lblRocPercent.Size = new System.Drawing.Size(15, 13);
            this.lblRocPercent.TabIndex = 8;
            this.lblRocPercent.Text = "%";
            // 
            // lblRocConfidenceInterval
            // 
            this.lblRocConfidenceInterval.AutoSize = true;
            this.lblRocConfidenceInterval.Location = new System.Drawing.Point(7, 97);
            this.lblRocConfidenceInterval.Name = "lblRocConfidenceInterval";
            this.lblRocConfidenceInterval.Size = new System.Drawing.Size(98, 13);
            this.lblRocConfidenceInterval.TabIndex = 7;
            this.lblRocConfidenceInterval.Text = "Confidence interval";
            // 
            // rdoRocCutOffGt
            // 
            this.rdoRocCutOffGt.AutoSize = true;
            this.rdoRocCutOffGt.Location = new System.Drawing.Point(288, 46);
            this.rdoRocCutOffGt.Name = "rdoRocCutOffGt";
            this.rdoRocCutOffGt.Size = new System.Drawing.Size(39, 17);
            this.rdoRocCutOffGt.TabIndex = 6;
            this.rdoRocCutOffGt.TabStop = true;
            this.rdoRocCutOffGt.Text = "> x";
            this.rdoRocCutOffGt.UseVisualStyleBackColor = true;
            // 
            // rdoRocCutOffGe
            // 
            this.rdoRocCutOffGe.AutoSize = true;
            this.rdoRocCutOffGe.Location = new System.Drawing.Point(237, 45);
            this.rdoRocCutOffGe.Name = "rdoRocCutOffGe";
            this.rdoRocCutOffGe.Size = new System.Drawing.Size(45, 17);
            this.rdoRocCutOffGe.TabIndex = 5;
            this.rdoRocCutOffGe.TabStop = true;
            this.rdoRocCutOffGe.Text = ">= x";
            this.rdoRocCutOffGe.UseVisualStyleBackColor = true;
            // 
            // rdoRocCutOffLe
            // 
            this.rdoRocCutOffLe.AutoSize = true;
            this.rdoRocCutOffLe.Location = new System.Drawing.Point(186, 46);
            this.rdoRocCutOffLe.Name = "rdoRocCutOffLe";
            this.rdoRocCutOffLe.Size = new System.Drawing.Size(45, 17);
            this.rdoRocCutOffLe.TabIndex = 4;
            this.rdoRocCutOffLe.TabStop = true;
            this.rdoRocCutOffLe.Text = "<= x";
            this.rdoRocCutOffLe.UseVisualStyleBackColor = true;
            // 
            // rdoRocCutOffLt
            // 
            this.rdoRocCutOffLt.AutoSize = true;
            this.rdoRocCutOffLt.Location = new System.Drawing.Point(141, 46);
            this.rdoRocCutOffLt.Name = "rdoRocCutOffLt";
            this.rdoRocCutOffLt.Size = new System.Drawing.Size(39, 17);
            this.rdoRocCutOffLt.TabIndex = 3;
            this.rdoRocCutOffLt.TabStop = true;
            this.rdoRocCutOffLt.Text = "< x";
            this.rdoRocCutOffLt.UseVisualStyleBackColor = true;
            // 
            // cboRocCi
            // 
            this.cboRocCi.FormattingEnabled = true;
            this.cboRocCi.Items.AddRange(new object[] {
            "90",
            "91",
            "92",
            "93",
            "94",
            "95",
            "96",
            "97",
            "98",
            "99"});
            this.cboRocCi.Location = new System.Drawing.Point(111, 94);
            this.cboRocCi.Name = "cboRocCi";
            this.cboRocCi.Size = new System.Drawing.Size(49, 21);
            this.cboRocCi.TabIndex = 2;
            // 
            // chkRocShowCutOffCalculator
            // 
            this.chkRocShowCutOffCalculator.AutoSize = true;
            this.chkRocShowCutOffCalculator.Location = new System.Drawing.Point(7, 71);
            this.chkRocShowCutOffCalculator.Name = "chkRocShowCutOffCalculator";
            this.chkRocShowCutOffCalculator.Size = new System.Drawing.Size(135, 17);
            this.chkRocShowCutOffCalculator.TabIndex = 1;
            this.chkRocShowCutOffCalculator.Text = "Show cut-off calculator";
            this.chkRocShowCutOffCalculator.UseVisualStyleBackColor = true;
            // 
            // chkRocShowCutoff
            // 
            this.chkRocShowCutoff.AutoSize = true;
            this.chkRocShowCutoff.Location = new System.Drawing.Point(7, 46);
            this.chkRocShowCutoff.Name = "chkRocShowCutoff";
            this.chkRocShowCutoff.Size = new System.Drawing.Size(128, 17);
            this.chkRocShowCutoff.TabIndex = 0;
            this.chkRocShowCutoff.Text = "Show optimum cut-off";
            this.chkRocShowCutoff.UseVisualStyleBackColor = true;
            // 
            // pnlSurvivalOptions
            // 
            this.pnlSurvivalOptions.AutoSize = true;
            this.pnlSurvivalOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlSurvivalOptions.Controls.Add(this.grpSurvivalOptions);
            this.pnlSurvivalOptions.Location = new System.Drawing.Point(0, 806);
            this.pnlSurvivalOptions.Margin = new System.Windows.Forms.Padding(0);
            this.pnlSurvivalOptions.Name = "pnlSurvivalOptions";
            this.pnlSurvivalOptions.Size = new System.Drawing.Size(267, 65);
            this.pnlSurvivalOptions.TabIndex = 21;
            // 
            // grpSurvivalOptions
            // 
            this.grpSurvivalOptions.Controls.Add(this.chkSurvivalUseSeriesColourForConfidenceIntervals);
            this.grpSurvivalOptions.Controls.Add(this.chkSurvivalShowEventMarkers);
            this.grpSurvivalOptions.Controls.Add(this.chkSurvivalShowCensorshipTics);
            this.grpSurvivalOptions.Location = new System.Drawing.Point(0, 0);
            this.grpSurvivalOptions.Margin = new System.Windows.Forms.Padding(0);
            this.grpSurvivalOptions.Name = "grpSurvivalOptions";
            this.grpSurvivalOptions.Size = new System.Drawing.Size(267, 65);
            this.grpSurvivalOptions.TabIndex = 0;
            this.grpSurvivalOptions.TabStop = false;
            this.grpSurvivalOptions.Text = "Survival plot options";
            // 
            // chkSurvivalUseSeriesColourForConfidenceIntervals
            // 
            this.chkSurvivalUseSeriesColourForConfidenceIntervals.AutoSize = true;
            this.chkSurvivalUseSeriesColourForConfidenceIntervals.Location = new System.Drawing.Point(6, 42);
            this.chkSurvivalUseSeriesColourForConfidenceIntervals.Name = "chkSurvivalUseSeriesColourForConfidenceIntervals";
            this.chkSurvivalUseSeriesColourForConfidenceIntervals.Size = new System.Drawing.Size(225, 17);
            this.chkSurvivalUseSeriesColourForConfidenceIntervals.TabIndex = 2;
            this.chkSurvivalUseSeriesColourForConfidenceIntervals.Text = "Use series colours for confidence intervals";
            this.chkSurvivalUseSeriesColourForConfidenceIntervals.UseVisualStyleBackColor = true;
            // 
            // chkSurvivalShowEventMarkers
            // 
            this.chkSurvivalShowEventMarkers.AutoSize = true;
            this.chkSurvivalShowEventMarkers.Location = new System.Drawing.Point(138, 19);
            this.chkSurvivalShowEventMarkers.Name = "chkSurvivalShowEventMarkers";
            this.chkSurvivalShowEventMarkers.Size = new System.Drawing.Size(123, 17);
            this.chkSurvivalShowEventMarkers.TabIndex = 1;
            this.chkSurvivalShowEventMarkers.Text = "Show event markers";
            this.chkSurvivalShowEventMarkers.UseVisualStyleBackColor = true;
            // 
            // chkSurvivalShowCensorshipTics
            // 
            this.chkSurvivalShowCensorshipTics.AutoSize = true;
            this.chkSurvivalShowCensorshipTics.Location = new System.Drawing.Point(6, 19);
            this.chkSurvivalShowCensorshipTics.Name = "chkSurvivalShowCensorshipTics";
            this.chkSurvivalShowCensorshipTics.Size = new System.Drawing.Size(126, 17);
            this.chkSurvivalShowCensorshipTics.TabIndex = 0;
            this.chkSurvivalShowCensorshipTics.Text = "Show censorship tics";
            this.chkSurvivalShowCensorshipTics.UseVisualStyleBackColor = true;
            // 
            // ctlBoxWhiskerOptions1
            // 
            this.ctlBoxWhiskerOptions1.AutoSize = true;
            this.ctlBoxWhiskerOptions1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlBoxWhiskerOptions1.Location = new System.Drawing.Point(4, 876);
            this.ctlBoxWhiskerOptions1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ctlBoxWhiskerOptions1.Name = "ctlBoxWhiskerOptions1";
            this.ctlBoxWhiskerOptions1.Size = new System.Drawing.Size(282, 246);
            this.ctlBoxWhiskerOptions1.TabIndex = 22;
            this.ctlBoxWhiskerOptions1.XAxisTitleChanged += new System.EventHandler(this.ctlBoxWhiskerOptions1_XAxisTitleChanged);
            // 
            // ctlHistogramOptions1
            // 
            this.ctlHistogramOptions1.AutoSize = true;
            this.ctlHistogramOptions1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlHistogramOptions1.Location = new System.Drawing.Point(4, 1132);
            this.ctlHistogramOptions1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.ctlHistogramOptions1.Name = "ctlHistogramOptions1";
            this.ctlHistogramOptions1.Size = new System.Drawing.Size(322, 418);
            this.ctlHistogramOptions1.TabIndex = 23;
            this.ctlHistogramOptions1.ScaleChanged += new System.EventHandler(this.ctlHistogramOptions1_ScaleChanged);
            // 
            // tlpScatterXYOptions
            // 
            this.tlpScatterXYOptions.AutoSize = true;
            this.tlpScatterXYOptions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpScatterXYOptions.ColumnCount = 1;
            this.tlpScatterXYOptions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpScatterXYOptions.Controls.Add(this.chkScatterXYPlotMarkers, 0, 0);
            this.tlpScatterXYOptions.Controls.Add(this.chkScatterXYPlotLines, 0, 1);
            this.tlpScatterXYOptions.Controls.Add(this.chkShouldCheckForOffsets, 0, 2);
            this.tlpScatterXYOptions.Location = new System.Drawing.Point(0, 613);
            this.tlpScatterXYOptions.Margin = new System.Windows.Forms.Padding(0);
            this.tlpScatterXYOptions.Name = "tlpScatterXYOptions";
            this.tlpScatterXYOptions.RowCount = 3;
            this.tlpScatterXYOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpScatterXYOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpScatterXYOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpScatterXYOptions.Size = new System.Drawing.Size(240, 69);
            this.tlpScatterXYOptions.TabIndex = 25;
            // 
            // chkScatterXYPlotMarkers
            // 
            this.chkScatterXYPlotMarkers.AutoSize = true;
            this.chkScatterXYPlotMarkers.Location = new System.Drawing.Point(3, 3);
            this.chkScatterXYPlotMarkers.Name = "chkScatterXYPlotMarkers";
            this.chkScatterXYPlotMarkers.Size = new System.Drawing.Size(123, 17);
            this.chkScatterXYPlotMarkers.TabIndex = 0;
            this.chkScatterXYPlotMarkers.Text = "Show series markers";
            this.chkScatterXYPlotMarkers.UseVisualStyleBackColor = true;
            this.chkScatterXYPlotMarkers.CheckedChanged += new System.EventHandler(this.chkScatterXYPlotMarkers_CheckedChanged);
            // 
            // chkScatterXYPlotLines
            // 
            this.chkScatterXYPlotLines.AutoSize = true;
            this.chkScatterXYPlotLines.Location = new System.Drawing.Point(3, 26);
            this.chkScatterXYPlotLines.Name = "chkScatterXYPlotLines";
            this.chkScatterXYPlotLines.Size = new System.Drawing.Size(68, 17);
            this.chkScatterXYPlotLines.TabIndex = 1;
            this.chkScatterXYPlotLines.Text = "Plot lines";
            this.chkScatterXYPlotLines.UseVisualStyleBackColor = true;
            this.chkScatterXYPlotLines.CheckedChanged += new System.EventHandler(this.chkScatterXYPlotLines_CheckedChanged);
            // 
            // chkShouldCheckForOffsets
            // 
            this.chkShouldCheckForOffsets.AutoSize = true;
            this.chkShouldCheckForOffsets.Location = new System.Drawing.Point(3, 49);
            this.chkShouldCheckForOffsets.Name = "chkShouldCheckForOffsets";
            this.chkShouldCheckForOffsets.Size = new System.Drawing.Size(234, 17);
            this.chkShouldCheckForOffsets.TabIndex = 2;
            this.chkShouldCheckForOffsets.Text = "Move error bars slightly if they would overlap";
            this.chkShouldCheckForOffsets.UseVisualStyleBackColor = true;
            this.chkShouldCheckForOffsets.Visible = false;
            // 
            // cmdRocEditCutoffs
            // 
            this.cmdRocEditCutoffs.Location = new System.Drawing.Point(164, 67);
            this.cmdRocEditCutoffs.Name = "cmdRocEditCutoffs";
            this.cmdRocEditCutoffs.Size = new System.Drawing.Size(91, 23);
            this.cmdRocEditCutoffs.TabIndex = 11;
            this.cmdRocEditCutoffs.Text = "Edit cut-offs...";
            this.cmdRocEditCutoffs.UseVisualStyleBackColor = true;
            // 
            // ctlChartOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tlpOuter);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.MinimumSize = new System.Drawing.Size(405, 40);
            this.Name = "ctlChartOptions";
            this.Size = new System.Drawing.Size(859, 1585);
            this.tlpDisplay.ResumeLayout(false);
            this.tlpDisplay.PerformLayout();
            this.pnlLegendFont.ResumeLayout(false);
            this.pnlLegendFont.PerformLayout();
            this.pnlSeriesLabelFont.ResumeLayout(false);
            this.pnlSeriesLabelFont.PerformLayout();
            this.pnlSeriesOptions.ResumeLayout(false);
            this.pnlSeriesOptions.PerformLayout();
            this.pnlColour.ResumeLayout(false);
            this.grpColour.ResumeLayout(false);
            this.grpColour.PerformLayout();
            this.pnlShowLegend.ResumeLayout(false);
            this.pnlShowLegend.PerformLayout();
            this.pnlTitleFont.ResumeLayout(false);
            this.pnlTitleFont.PerformLayout();
            this.pnlAxisTitleFont.ResumeLayout(false);
            this.pnlAxisTitleFont.PerformLayout();
            this.pnlAxisLabelFont.ResumeLayout(false);
            this.pnlAxisLabelFont.PerformLayout();
            this.pnlAxisLineThickness.ResumeLayout(false);
            this.pnlAxisLineThickness.PerformLayout();
            this.pnlBoxAxes.ResumeLayout(false);
            this.pnlBoxAxes.PerformLayout();
            this.pnlChartTitle.ResumeLayout(false);
            this.pnlChartTitle.PerformLayout();
            this.pnlSeriesLabels.ResumeLayout(false);
            this.pnlSeriesLabels.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridSeriesLabels)).EndInit();
            this.pnlControlOptions.ResumeLayout(false);
            this.grpControlOptions.ResumeLayout(false);
            this.grpControlOptions.PerformLayout();
            this.tlpOuter.ResumeLayout(false);
            this.tlpOuter.PerformLayout();
            this.tlpFunction.ResumeLayout(false);
            this.tlpFunction.PerformLayout();
            this.tlpCustom.ResumeLayout(false);
            this.tlpCustom.PerformLayout();
            this.pnlPreview.ResumeLayout(false);
            this.pnlOrientation.ResumeLayout(false);
            this.grpOrientation.ResumeLayout(false);
            this.grpOrientation.PerformLayout();
            this.pnlBarOptions.ResumeLayout(false);
            this.pnlBarOptions.PerformLayout();
            this.tlpBarOptions.ResumeLayout(false);
            this.tlpBarOptions.PerformLayout();
            this.pnlBarOptionsNonStacked.ResumeLayout(false);
            this.pnlBarOptionsNonStacked.PerformLayout();
            this.pnlForestOptions.ResumeLayout(false);
            this.grpForestOptions.ResumeLayout(false);
            this.grpForestOptions.PerformLayout();
            this.pnlNormalOptions.ResumeLayout(false);
            this.grpNormalScaling.ResumeLayout(false);
            this.grpNormalScaling.PerformLayout();
            this.grpNormalOptions.ResumeLayout(false);
            this.grpNormalOptions.PerformLayout();
            this.pnlPyramidOptions.ResumeLayout(false);
            this.pnlPyramidOptions.PerformLayout();
            this.pnlRocOptions.ResumeLayout(false);
            this.grpRocOptions.ResumeLayout(false);
            this.grpRocOptions.PerformLayout();
            this.pnlSurvivalOptions.ResumeLayout(false);
            this.grpSurvivalOptions.ResumeLayout(false);
            this.grpSurvivalOptions.PerformLayout();
            this.tlpScatterXYOptions.ResumeLayout(false);
            this.tlpScatterXYOptions.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpDisplay;
        private System.Windows.Forms.Panel pnlChartTitle;
        private System.Windows.Forms.TextBox txtChartTitle;
        private System.Windows.Forms.Label lblChartTitle;
        private System.Windows.Forms.Panel pnlBoxAxes;
        private System.Windows.Forms.CheckBox chkBoxAxes;
        private System.Windows.Forms.Panel pnlSeriesLabels;
        private System.Windows.Forms.Label lblSeriesLabels;
        private System.Windows.Forms.DataGridView gridSeriesLabels;
        private System.Windows.Forms.DataGridViewTextBoxColumn Title;
        private System.Windows.Forms.Panel pnlControlOptions;
        private System.Windows.Forms.GroupBox grpControlOptions;
        private System.Windows.Forms.CheckBox chk3SD;
        private System.Windows.Forms.CheckBox chk2SD;
        private System.Windows.Forms.CheckBox chk1SD;
        private System.Windows.Forms.CheckBox chkMean;
        private System.Windows.Forms.Label lblUseFirst;
        private System.Windows.Forms.ComboBox cboKObs;
        private System.Windows.Forms.Label lblObservations;
        private System.Windows.Forms.CheckBox chkHasUserSpecifiedMeanAndSD;
        private System.Windows.Forms.Label lblSD;
        private System.Windows.Forms.TextBox txtStandardDeviation;
        private System.Windows.Forms.Label lblMean;
        private System.Windows.Forms.TextBox txtMean;
        private System.Windows.Forms.CheckBox chkHasUserSpecifiedLimits;
        private System.Windows.Forms.Label lblUpperWarning;
        private System.Windows.Forms.TextBox txtUpperWarning;
        private System.Windows.Forms.Label lblLowerWarning;
        private System.Windows.Forms.TextBox txtLowerWarning;
        private System.Windows.Forms.Label lblUpperControl;
        private System.Windows.Forms.TextBox txtUpperControl;
        private System.Windows.Forms.Label lblLowerControl;
        private System.Windows.Forms.TextBox txtLowerControl;
        private System.Windows.Forms.TableLayoutPanel tlpOuter;
        private System.Windows.Forms.TableLayoutPanel tlpFunction;
        private System.Windows.Forms.ComboBox cboControlDecimalPlaces;
        private System.Windows.Forms.Label lblControlDecimalPlaces;
        private System.Windows.Forms.Label lblControlLabelLinesTo;
        private System.Windows.Forms.Panel pnlTitleFont;
        private System.Windows.Forms.Panel pnlAxisLabelFont;
        private System.Windows.Forms.Panel pnlAxisTitleFont;
        private System.Windows.Forms.Panel pnlLegendFont;
        private System.Windows.Forms.Panel pnlSeriesOptions;
        private ctlSeriesOptions seriesOptions;
        private System.Windows.Forms.Panel pnlForestOptions;
        private System.Windows.Forms.GroupBox grpForestOptions;
        private System.Windows.Forms.Label lblForestDecimalPlaces;
        private System.Windows.Forms.Label lblForestLabelEffectSizesTo;
        private System.Windows.Forms.ComboBox cboForestDecimalPlaces;
        private System.Windows.Forms.Panel pnlAxisLineThickness;
        private ctlLineThickness ctlAxisLineThickness;
        private System.Windows.Forms.Panel pnlSeriesLabelFont;
        private System.Windows.Forms.Panel pnlNormalOptions;
        private System.Windows.Forms.GroupBox grpNormalScaling;
        private System.Windows.Forms.GroupBox grpNormalOptions;
        private System.Windows.Forms.RadioButton rdoNormalExpectedNormalOrder;
        private System.Windows.Forms.RadioButton rdoNormalBlom;
        private System.Windows.Forms.RadioButton rdoNormalVanDerWaerden;
        private System.Windows.Forms.RadioButton rdoNormalScaled;
        private System.Windows.Forms.RadioButton rdoNormalRaw;
        private System.Windows.Forms.CheckBox chkScatterXYPlotMarkers;
        private System.Windows.Forms.Panel pnlOrientation;
        private System.Windows.Forms.GroupBox grpOrientation;
        private System.Windows.Forms.RadioButton rdoOrientationVertical;
        private System.Windows.Forms.RadioButton rdoOrientationHorizontal;
        private System.Windows.Forms.Panel pnlRocOptions;
        private System.Windows.Forms.GroupBox grpRocOptions;
        private System.Windows.Forms.ComboBox cboRocCi;
        private System.Windows.Forms.CheckBox chkRocShowCutoff;
        private System.Windows.Forms.RadioButton rdoRocCutOffGt;
        private System.Windows.Forms.RadioButton rdoRocCutOffGe;
        private System.Windows.Forms.RadioButton rdoRocCutOffLe;
        private System.Windows.Forms.RadioButton rdoRocCutOffLt;
        private System.Windows.Forms.Label lblRocPercent;
        private System.Windows.Forms.Label lblRocConfidenceInterval;
        private System.Windows.Forms.ComboBox cboRocWeight;
        private System.Windows.Forms.Label lblRocSensSpec;
        private System.Windows.Forms.Panel pnlPyramidOptions;
        private System.Windows.Forms.TextBox txtPyramidScaleMaximum;
        private System.Windows.Forms.Label lblPyramidScaleMaximum;
        private System.Windows.Forms.Panel pnlSurvivalOptions;
        private System.Windows.Forms.GroupBox grpSurvivalOptions;
        private System.Windows.Forms.CheckBox chkSurvivalShowEventMarkers;
        private System.Windows.Forms.CheckBox chkSurvivalShowCensorshipTics;
        private System.Windows.Forms.CheckBox chkSurvivalUseSeriesColourForConfidenceIntervals;
        private System.Windows.Forms.Panel pnlBarOptions;
        private System.Windows.Forms.Label lblBarBarWidthPercent1;
        private System.Windows.Forms.Label lblBarBarWidthPercent2;
        private System.Windows.Forms.TextBox txtBarBarWidthPercent;
        private System.Windows.Forms.TableLayoutPanel tlpBarOptions;
        private System.Windows.Forms.Panel pnlBarOptionsNonStacked;
        private System.Windows.Forms.TableLayoutPanel tlpCustom;
        private ctlAxisOptions ctlAxisOptions;
        private System.Windows.Forms.Label lblAxisLineThickness;
        private ctlFont ctlTitleFont;
        private ctlFont ctlLegendFont;
        private ctlFont ctlSeriesLabelFont;
        private ctlFont ctlAxisTitleFont;
        private ctlFont ctlAxisLabelFont;
        private System.Windows.Forms.Panel pnlColour;
        private System.Windows.Forms.GroupBox grpColour;
        private System.Windows.Forms.RadioButton rdoMonochrome;
        private System.Windows.Forms.RadioButton rdoColour;
        private System.Windows.Forms.Panel pnlShowLegend;
        private System.Windows.Forms.CheckBox chkShowLegend;
        private ctlBoxWhiskerOptions ctlBoxWhiskerOptions1;
        private ctlHistogramOptions ctlHistogramOptions1;
        private System.Windows.Forms.Panel pnlPreview;
        private System.Windows.Forms.Button cmdPreview;
        private ctlBarOptions ctlBarOptions;
        private System.Windows.Forms.CheckBox chkRocShowCutOffCalculator;
        private System.Windows.Forms.TableLayoutPanel tlpScatterXYOptions;
        private System.Windows.Forms.CheckBox chkScatterXYPlotLines;
        private System.Windows.Forms.CheckBox chkForestMarkCentres;
        private System.Windows.Forms.CheckBox chkShouldCheckForOffsets;
        private System.Windows.Forms.Button cmdRocEditCutoffs;
    }
}
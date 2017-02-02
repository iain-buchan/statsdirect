using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StatsDirect.Numerics;
using StatsDirect.Charting;
using StatsDirect.Templates;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    public partial class ctlChartOptions : UserControl, IOkable
    {
        private readonly ChartDefinition definition;
        private readonly GenericOptions options;

        public ctlChartOptions(ChartDefinition definition)
        {
            this.definition = definition;
            options = (GenericOptions)definition.ChartOptions;
            InitializeComponent();
            FillFormFromOptions();
        }

        private void FillOptionsFromForm()
        {
            options.UseColour = rdoColour.Checked;
            if (options.UsesShowLegend && options.ShowLegendIsRelevant)
            {
                options.ShowLegend = chkShowLegend.Checked;
            }
            if (options.UsesBoxAxes)
            {
                options.ShouldBoxAxes = chkBoxAxes.Checked;
            }
            if (options.UsesChartTitle)
            {
                options.Title = txtChartTitle.Text;
            }
            if (options.UsesSeriesLabels)
            {
                for (int i = 0; i < options.SeriesTitles.Length; i++)
                    options.SeriesTitles[i] = (string)gridSeriesLabels.Rows[i].Cells[0].Value;
            }
            if (options.UsesXAxisTitle)
            {
                options.XAxisTitle = ctlAxisOptions.X.Title;
            }
            if (options.UsesYAxisTitle)
            {
                options.YAxisTitle = ctlAxisOptions.Y.Title;
            }
            ScaleParameters scaleParameters = definition.ScaleParameters;
            FillAxisScaleParametersFromForm(scaleParameters.X, ctlAxisOptions.X);
            FillAxisScaleParametersFromForm(scaleParameters.Y, ctlAxisOptions.Y);
            if (options.UsesAxisLabelFontDescriptor)
            {
                options.AxisLabelFontDescriptor = ChartRenderer.SaveStringFromFont(ctlAxisLabelFont.UserFont);
            }
            if (options.UsesAxisTitleFontDescriptor)
            {
                options.AxisTitleFontDescriptor = ChartRenderer.SaveStringFromFont(ctlAxisTitleFont.UserFont);
            }
            if (options.UsesTitleFontDescriptor)
            {
                options.TitleFontDescriptor = ChartRenderer.SaveStringFromFont(ctlTitleFont.UserFont);
            }
            if (options.UsesLegendFontDescriptor)
            {
                options.LegendFontDescriptor = ChartRenderer.SaveStringFromFont(ctlLegendFont.UserFont);
            }
            if (options.ShowBarOptions)
            {
                FillBarOptionsFromForm();
            }
            if (options.ShowBoxWhiskerOptions)
            {
                FillBoxWhiskerOptionsFromForm();
            }
            if (options.ShowControlOptions)
            {
                FillControlOptionsFromForm();
            }
            if (options.ShowErrorBarOptions)
            {
                FillErrorBarOptionsFromForm();
            }
            if (options.ShowForestOptions)
            {
                FillForestOptionsFromForm();
            }
            if (options.ShowHistogramOptions)
            {
                FillHistogramOptionsFromForm();
            }
            if (options.ShowNormalOptions)
            {
                FillNormalOptionsFromForm();
            }
            if (options.ShowPyramidOptions)
            {
                FillPyramidOptionsFromForm();
            }
            if (options.ShowRocOptions)
            {
                FillRocOptionsFromForm();
            }
            if (options.ShowScatterXYOptions)
            {
                FillScatterXYOptionsFromForm();
            }
            if (options.ShowSurvivalOptions)
            {
                FillSurvivalOptionsFromForm();
            }
            if (options.UsesAxisLineThickness)
            {
                options.AxisLineThickness = ctlAxisLineThickness.LineThickness;
            }
            if (null != options.SeriesOptions)
            {
                seriesOptions.Save();
                options.MarkerTypes = seriesOptions.MarkerTypes;
            }
            FillOrientationFromForm();
        }

        private static void FillAxisScaleParametersFromForm(AxisScaleParameters asp, ctlOneAxisOptions ao)
        {
            asp.ScaleType = ao.ScaleType;
            asp.LabelDirection = ao.LabelDirection;

            switch (asp.ScaleType)
            {
                case ScaleType.Category:
                    // Do nothing
                    break;
                case ScaleType.Date:
                case ScaleType.Linear:
                    asp.AxisScale = new LinearAxisScale(ao.MinimumScaleValue, ao.MaximumScaleValue, ao.MinimumScaleValue, ao.MaximumScaleValue, ao.Intervals, ao.IntervalsPerMajorTic);
                    break;
                case ScaleType.Log10:
                    asp.AxisScale = new Log10AxisScale(ao.MinimumScaleValue, ao.MaximumScaleValue, (int)Math.Round(Math.Log10(ao.MinimumScaleValue)), (int)Math.Round(Math.Log10(ao.MaximumScaleValue)), new List<double>());
                    break;
                case ScaleType.LogNatural:
                    asp.AxisScale = new Log2AxisScale(ao.MinimumScaleValue, ao.MaximumScaleValue, (int)Math.Round(Log2(ao.MinimumScaleValue)), (int)Math.Round(Log2(ao.MaximumScaleValue)));
                    break;
                case ScaleType.NotSet:
                default:
                    throw new NotImplementedException("Unknown scale type in FillAxisScaleParametersFromForm");
            }
            asp.Mask = ao.Mask;

            asp.HasGridLines = ao.HasGridLines;
            asp.GridLineDashStyle = ao.GridLineDashStyle;

            if (ao.HasMarkerLine)
                asp.MarkerLineValue = ao.MarkerLineValue;
            else
                asp.MarkerLineValue = default(double?);
        }

        private static double Log2(double value)
        {
            return Math.Log(value) / Math.Log(2.0);
        }

        private void FillOrientationFromForm()
        {
            if (options.UsesOrientation)
            {
                if (rdoOrientationHorizontal.Checked)
                    options.Orientation = ChartOrientation.Horizontal;
                else if (rdoOrientationVertical.Checked)
                    options.Orientation = ChartOrientation.Vertical;
            }
        }

        private void FillBarOptionsFromForm()
        {
            BarOptions barOptions = (BarOptions)options;
            ctlBarOptions.FillOptionsFromForm();
            double maxBarWidth;
            if (double.TryParse(txtBarBarWidthPercent.Text, out maxBarWidth))
            {
                barOptions.MaxBarWidth = maxBarWidth / 100.0;
            }
        }

        private void FillBoxWhiskerOptionsFromForm()
        {
            ctlBoxWhiskerOptions1.FillOptionsFromForm();
        }

        private void FillHistogramOptionsFromForm()
        {
            ctlHistogramOptions1.FillOptionsFromForm();
        }

        private void FillSurvivalOptionsFromForm()
        {
            SurvivalOptions survivalOptions = (SurvivalOptions)options;
            survivalOptions.ShowCensorshipTics = chkSurvivalShowCensorshipTics.Checked;
            survivalOptions.ShowEventMarkers = chkSurvivalShowEventMarkers.Checked;
            survivalOptions.UseSeriesColourForConfidenceIntervals = chkSurvivalUseSeriesColourForConfidenceIntervals.Checked;
        }

        private void FillPyramidOptionsFromForm()
        {
            PyramidOptions pyramidOptions = (PyramidOptions)options;
            double.TryParse(txtPyramidScaleMaximum.Text, out pyramidOptions.ScaleMaximum);
        }

        private void FillRocOptionsFromForm()
        {
            ROCOptions rocOptions = (ROCOptions)options;
            rocOptions.ShowCutOffCalculator = chkRocShowCutOffCalculator.Checked;
            rocOptions.ShowOptimumCutOff = chkRocShowCutoff.Checked;
            double.TryParse(cboRocWeight.Text, out rocOptions.Weight);
            if (double.TryParse(cboRocCi.Text, out rocOptions.GAMMA))
                rocOptions.GAMMA /= 100.0;
            if (rdoRocCutOffGe.Checked)
                rocOptions.Showopts = ComparisonValue.GE;
            else if (rdoRocCutOffGt.Checked)
                rocOptions.Showopts = ComparisonValue.GT;
            else if (rdoRocCutOffLe.Checked)
                rocOptions.Showopts = ComparisonValue.LE;
            else if (rdoRocCutOffLt.Checked)
                rocOptions.Showopts = ComparisonValue.LT;
        }

        private void FillScatterXYOptionsFromForm()
        {
            ScatterXYOptions scatterXYOptions = (ScatterXYOptions)options;
            scatterXYOptions.PlotMarkers = chkScatterXYPlotMarkers.Checked;
            scatterXYOptions.JoinMarkersWithLines = chkScatterXYPlotLines.Checked;
        }

        private void FillErrorBarOptionsFromForm()
        {
            ErrorBarOptions errorBarOptions = (ErrorBarOptions)options;
            errorBarOptions.PlotMarkers = chkScatterXYPlotMarkers.Checked;
            errorBarOptions.JoinMarkersWithLines = chkScatterXYPlotLines.Checked;
            errorBarOptions.ShouldCheckForOffsets = chkShouldCheckForOffsets.Checked;
        }

        private void FillForestOptionsFromForm()
        {
            ForestOptions forestOptions = (ForestOptions)options;
            int effectSizeAndIntervalDecimalPlaces;
            int.TryParse(cboForestDecimalPlaces.Text, out effectSizeAndIntervalDecimalPlaces);
            forestOptions.EffectSizeAndIntervalDecimalPlaces = effectSizeAndIntervalDecimalPlaces;
            forestOptions.MarkCentres = chkForestMarkCentres.Checked;
        }

        private void FillNormalOptionsFromForm()
        {
            NormalOptions normalOptions = (NormalOptions)options;
            if (rdoNormalBlom.Checked)
                normalOptions.Method = NormalOptions.ScoreMethod.Blom;
            else if (rdoNormalExpectedNormalOrder.Checked)
                normalOptions.Method = NormalOptions.ScoreMethod.ExpectedNormalOrder;
            else if (rdoNormalVanDerWaerden.Checked)
                normalOptions.Method = NormalOptions.ScoreMethod.VanDerWaerden;
            // Else do nothing.  Should never happen!
            normalOptions.ShouldScaleZ = rdoNormalScaled.Checked;
        }

        private void FillControlOptionsFromForm()
        {
            ControlOptions controlOptions = (ControlOptions) options;
            controlOptions.UseMean = chkMean.Checked;
            controlOptions.Use1SD = chk1SD.Checked;
            controlOptions.Use2SD = chk2SD.Checked;
            controlOptions.Use3SD = chk3SD.Checked;
            if (chkHasUserSpecifiedLimits.Checked)
            {
                controlOptions.HasUserSpecifiedLimits = true;
                controlOptions.HasUserSpecifiedMeanAndSD = false;
                controlOptions.LowerWarningLimit = Utilities.Parsing.Cdbl_Txt(txtLowerWarning.Text);
                controlOptions.UpperWarningLimit = Utilities.Parsing.Cdbl_Txt(txtUpperWarning.Text);
                controlOptions.LowerControlLimit = Utilities.Parsing.Cdbl_Txt(txtLowerControl.Text);
                controlOptions.UpperControlLimit = Utilities.Parsing.Cdbl_Txt(txtUpperControl.Text);
            }
            else
            {
                if (chkHasUserSpecifiedMeanAndSD.Checked)
                {
                    controlOptions.HasUserSpecifiedMeanAndSD = true;
                    controlOptions.HasUserSpecifiedLimits = false;
                    controlOptions.UserSpecifiedMean = Utilities.Parsing.Cdbl_Txt(txtMean.Text);
                    controlOptions.UserSpecifiedSD = Utilities.Parsing.Cdbl_Txt(txtStandardDeviation.Text);
                }
                else
                {
                    controlOptions.HasUserSpecifiedMeanAndSD = false;
                    controlOptions.HasUserSpecifiedLimits = false;
                    int i = Utilities.Parsing.Cint_Txt((string)cboKObs.SelectedItem);
                    if (i > 2 && i != controlOptions.ObservationsToUse)
                        controlOptions.ObservationsToUse = i;
                }
                controlOptions.LowerWarningLimit = Constant.MISSING;
                controlOptions.UpperWarningLimit = Constant.MISSING;
                controlOptions.LowerControlLimit = Constant.MISSING;
                controlOptions.UpperControlLimit = Constant.MISSING;
            }
            int.TryParse(cboControlDecimalPlaces.Text, out controlOptions.RightHandDecimalPlaces);
        }

        private void FillFormFromOptions()
        {
            rdoColour.Checked = options.UseColour;
            rdoMonochrome.Checked = !rdoColour.Checked;
            SetColour(options.UseColour);
            pnlBoxAxes.Visible = options.UsesBoxAxes;
            pnlChartTitle.Visible = options.UsesChartTitle;
            pnlSeriesLabels.Visible = options.UsesSeriesLabels;

            FillFormFromScaleParameters();

            pnlBarOptions.Visible = options.ShowBarOptions;
            ctlBoxWhiskerOptions1.Visible = options.ShowBoxWhiskerOptions;
            pnlControlOptions.Visible = options.ShowControlOptions;
            pnlForestOptions.Visible = options.ShowForestOptions;
            ctlHistogramOptions1.Visible = options.ShowHistogramOptions;
            pnlNormalOptions.Visible = options.ShowNormalOptions;
            pnlPyramidOptions.Visible = options.ShowPyramidOptions;
            pnlRocOptions.Visible = options.ShowRocOptions;
            // Pure scatter charts always show their markers, which is the only option; line charts may or may not, and error bars may or may not.
            tlpScatterXYOptions.Visible = (options.ShowScatterXYOptions) || options.ShowErrorBarOptions;
            chkScatterXYPlotMarkers.Visible = definition.ChartType != ChartType.ScatterXY;
            chkScatterXYPlotLines.Visible = definition.ChartType != ChartType.LineXY;
            pnlSurvivalOptions.Visible = options.ShowSurvivalOptions;
            pnlLegendFont.Visible = options.ShowControlOptions;
            pnlTitleFont.Visible = options.UsesTitleFontDescriptor;
            pnlAxisLabelFont.Visible = options.UsesAxisLabelFontDescriptor;
            pnlAxisTitleFont.Visible = options.UsesAxisTitleFontDescriptor;
            pnlSeriesOptions.Visible = (null != options.SeriesOptions && options.SeriesOptions.Count > 0);
            pnlAxisLineThickness.Visible = options.UsesAxisLineThickness;
            pnlLegendFont.Visible = options.UsesLegendFontDescriptor;
            pnlOrientation.Visible = options.UsesOrientation;
            pnlShowLegend.Visible = options.UsesShowLegend && options.ShowLegendIsRelevant;
            pnlColour.Visible = options.UsesColour;
            ctlAxisOptions.ShowX = options.UsesXAxisOptions;
            ctlAxisOptions.ShowY = options.UsesYAxisOptions;
            if (pnlOrientation.Visible)
                grpOrientation.Text = options.OrientationLabel;
            if (options.UsesShowLegend && options.ShowLegendIsRelevant)
                chkShowLegend.Checked = options.ShowLegend;
            if (options.UsesBoxAxes)
                chkBoxAxes.Checked = options.ShouldBoxAxes;
            if (options.UsesChartTitle)
                txtChartTitle.Text = options.Title;
            if (options.UsesSeriesLabels)
                foreach (string seriesTitle in options.SeriesTitles)
                    gridSeriesLabels.Rows.Add(seriesTitle);
            if (options.UsesXAxisTitle)
                ctlAxisOptions.X.Title = options.XAxisTitle;
            if (options.UsesYAxisTitle)
                ctlAxisOptions.Y.Title = options.YAxisTitle;
            if (options.ShowBarOptions)
                FillFormFromBarOptions();
            if (options.ShowBoxWhiskerOptions)
                FillFormFromBoxWhiskerOptions();
            if (options.ShowControlOptions)
                FillFormFromControlOptions();
            if (options.ShowErrorBarOptions)
                FillFormFromErrorBarOptions();
            if (options.ShowForestOptions)
                FillFormFromForestOptions();
            if (options.ShowHistogramOptions)
                FillFormFromHistogramOptions();
            if (options.ShowNormalOptions)
                FillFormFromNormalOptions();
            if (options.ShowPyramidOptions)
                FillFormFromPyramidOptions();
            if (options.ShowRocOptions)
                FillFormFromRocOptions();
            if (options.ShowScatterXYOptions)
                FillFormFromScatterXYOptions();
            if (options.ShowSurvivalOptions)
                FillFormFromSurvivalOptions();
            if (options.UsesAxisLabelFontDescriptor)
            {
                ctlAxisLabelFont.Purpose = options.AxisLabelFontLabel + " Font";
                if (!string.IsNullOrEmpty(options.AxisLabelFontDescriptor))
                    ctlAxisLabelFont.UserFont = ChartRenderer.FontFromSaveString(options.AxisLabelFontDescriptor);
            }
            if (options.UsesAxisTitleFontDescriptor)
            {
                if (!string.IsNullOrEmpty(options.AxisTitleFontDescriptor))
                    ctlAxisTitleFont.UserFont = ChartRenderer.FontFromSaveString(options.AxisTitleFontDescriptor);
            }
            if (options.UsesLegendFontDescriptor)
            {
                ctlLegendFont.Purpose = options.LegendFontLabel + " Font";
                if (!string.IsNullOrEmpty(options.LegendFontDescriptor))
                    ctlLegendFont.UserFont = ChartRenderer.FontFromSaveString(options.LegendFontDescriptor);
            }
            if (options.UsesTitleFontDescriptor)
            {
                if (!string.IsNullOrEmpty(options.TitleFontDescriptor))
                    ctlTitleFont.UserFont = ChartRenderer.FontFromSaveString(options.TitleFontDescriptor);
            }
            if (options.UsesAxisLineThickness)
            {
                ctlAxisLineThickness.LineThickness = (int)options.AxisLineThickness;
            }
            if (pnlSeriesOptions.Visible)
            {
                seriesOptions.ShouldForceIsFilled = options.ShouldForceIsFilled;
                seriesOptions.ForcedIsFilled = options.ForcedIsFilled;
                seriesOptions.ShouldForceFillStyle = options.ShouldForceFillStyle;
                seriesOptions.ForcedFillStyle = options.ForcedFillStyle;
                seriesOptions.MarkerTypes = options.MarkerTypes;

                // Set this last as it creates the marker types if they're not already created - so it must have all possible information.
                seriesOptions.SeriesOptionsDescriptors = options.SeriesOptions;
            }
            if (options.UsesOrientation)
            {
                if (ChartOrientation.Horizontal == options.Orientation)
                    rdoOrientationHorizontal.Checked = true;
                else if (ChartOrientation.Vertical == options.Orientation)
                    rdoOrientationVertical.Checked = true;
                ctlAxisOptions.AxisLabelsAreSwapped = !options.IsNaturalOrientation;
            }
        }

        private void FillFormFromScaleParameters()
        {
            ScaleParameters scaleParameters = definition.ScaleParameters;
            ctlAxisOptions.X.HasTitle = options.UsesXAxisTitle;
            ctlAxisOptions.Y.HasTitle = options.UsesYAxisTitle;
            FillFormFromAxisScaleParameters(scaleParameters.X, ctlAxisOptions.X);
            FillFormFromAxisScaleParameters(scaleParameters.Y, ctlAxisOptions.Y);
        }

        private static void FillFormFromAxisScaleParameters(AxisScaleParameters asp, ctlOneAxisOptions ao)
        {
            ao.AllowedScaleTypes = asp.AllowedScaleTypes;
            ao.ScaleType = asp.ScaleType;
            ao.MinimumDataValue = asp.Min;
            ao.DataMinGreaterThanZero = asp.MinGreaterThanZero;
            ao.MaximumDataValue = asp.Max;
            ao.LabelDirection = asp.LabelDirection;
        }

        private void UncacheScaleParameters()
        {
            definition.ScaleParameters = null;
        }

        private void FillFormFromBarOptions()
        {
            ctlBarOptions.ChartDefinition = definition;
            BarOptions barOptions = (BarOptions)options;
            ctlBarOptions.FillFormFromOptions();
            txtBarBarWidthPercent.Text = Math.Round(barOptions.MaxBarWidth * 100.0, 2).ToString();
        }

        private void FillFormFromBoxWhiskerOptions()
        {
            ctlBoxWhiskerOptions1.ChartDefinition = definition;
            ctlBoxWhiskerOptions1.FillFormFromOptions();
        }

        private void FillFormFromHistogramOptions()
        {
            ctlHistogramOptions1.ChartDefinition = definition;
            ctlHistogramOptions1.FillFormFromOptions();
        }

        private void FillFormFromSurvivalOptions()
        {
            SurvivalOptions survivalOptions = (SurvivalOptions)options;
            chkSurvivalShowCensorshipTics.Checked = survivalOptions.ShowCensorshipTics;
            chkSurvivalShowEventMarkers.Checked = survivalOptions.ShowEventMarkers;
            chkSurvivalUseSeriesColourForConfidenceIntervals.Checked = survivalOptions.UseSeriesColourForConfidenceIntervals;
        }

        private void FillFormFromPyramidOptions()
        {
            PyramidOptions pyramidOptions = (PyramidOptions)options;
            txtPyramidScaleMaximum.Text = pyramidOptions.ScaleMaximum.ToString();
        }

        private void FillFormFromRocOptions()
        {
            ROCOptions rocOptions = (ROCOptions)options;
            chkRocShowCutoff.Checked = rocOptions.ShowOptimumCutOff;
            chkRocShowCutOffCalculator.Checked = rocOptions.ShowCutOffCalculator;
            cboRocCi.Text = (rocOptions.GAMMA * 100.0).ToString();
            cboRocWeight.Text = rocOptions.Weight.ToString("N1");
            switch (rocOptions.Showopts)
            {
                case ComparisonValue.GE:
                    rdoRocCutOffGe.Checked = true;
                    break;
                case ComparisonValue.GT:
                    rdoRocCutOffGt.Checked = true;
                    break;
                case ComparisonValue.LE:
                    rdoRocCutOffLe.Checked = true;
                    break;
                case ComparisonValue.LT:
                    rdoRocCutOffLt.Checked = true;
                    break;
                default:
                    // Guess!
                    rdoRocCutOffGe.Checked = true;
                    break;
            }
        }

        private void FillFormFromScatterXYOptions()
        {
            ScatterXYOptions scatterXYOptions = (ScatterXYOptions)options;
            chkScatterXYPlotMarkers.Checked = scatterXYOptions.PlotMarkers;
            chkScatterXYPlotLines.Checked = scatterXYOptions.JoinMarkersWithLines;
        }

        private void FillFormFromErrorBarOptions()
        {
            ErrorBarOptions errorBarOptions = (ErrorBarOptions)options;
            chkScatterXYPlotMarkers.Checked = errorBarOptions.PlotMarkers;
            chkScatterXYPlotLines.Checked = errorBarOptions.JoinMarkersWithLines;
            chkShouldCheckForOffsets.Visible = true;
            chkShouldCheckForOffsets.Checked = errorBarOptions.ShouldCheckForOffsets;
        }

        private void FillFormFromForestOptions()
        {
            ForestOptions forestOptions = (ForestOptions)options;
            cboForestDecimalPlaces.Text = forestOptions.EffectSizeAndIntervalDecimalPlaces.ToString();
            chkForestMarkCentres.Checked = forestOptions.MarkCentres;
        }

        private void FillFormFromNormalOptions()
        {
            NormalOptions normalOptions = (NormalOptions)options;
            switch (normalOptions.Method)
            {
                case NormalOptions.ScoreMethod.Blom:
                    rdoNormalBlom.Checked = true;
                    break;
                case NormalOptions.ScoreMethod.ExpectedNormalOrder:
                    rdoNormalExpectedNormalOrder.Checked = true;
                    break;
                case NormalOptions.ScoreMethod.VanDerWaerden:
                    rdoNormalVanDerWaerden.Checked = true;
                    break;
                default:
                    // Guess!
                    rdoNormalVanDerWaerden.Checked = true;
                    break;
            }
            if (normalOptions.ShouldScaleZ)
                rdoNormalScaled.Checked = true;
            else
                rdoNormalRaw.Checked = true;
        }

        private void FillFormFromControlOptions()
        {
            ControlOptions controlOptions = (ControlOptions) options;

            cboKObs.Items.Clear();
            for (int i = controlOptions.ObservationsToUse; i >= 3; --i)
                cboKObs.Items.Add(i.ToString());
            cboKObs.SelectedItem = controlOptions.ObservationsToUse.ToString();

            chkMean.Checked = controlOptions.UseMean;
            chk1SD.Checked = controlOptions.Use1SD;
            chk2SD.Checked = controlOptions.Use2SD;
            chk3SD.Checked = controlOptions.Use3SD;
            chkHasUserSpecifiedLimits.Checked = controlOptions.HasUserSpecifiedLimits;
            chkHasUserSpecifiedMeanAndSD.Checked = controlOptions.HasUserSpecifiedMeanAndSD;
            cboControlDecimalPlaces.Text = controlOptions.RightHandDecimalPlaces.ToString();
        }

        private void chkHasUserSpecifiedLimits_CheckedChanged(object sender, EventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                bool enableMeanAndSD = !chkHasUserSpecifiedLimits.Checked;
                chkHasUserSpecifiedMeanAndSD.Enabled = enableMeanAndSD;
                chkMean.Enabled = enableMeanAndSD;
                chk1SD.Enabled = enableMeanAndSD;
                chk2SD.Enabled = enableMeanAndSD;
                chk3SD.Enabled = enableMeanAndSD;
                cboKObs.Enabled = enableMeanAndSD;
                lblObservations.Enabled = enableMeanAndSD;
                lblUseFirst.Enabled = enableMeanAndSD;
                lblMean.Enabled = enableMeanAndSD;
                txtMean.Enabled = enableMeanAndSD;
                lblSD.Enabled = enableMeanAndSD;
                txtStandardDeviation.Enabled = enableMeanAndSD;
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                if (SdApplication.SoleInstance.MainWindow.InOperation)
                    SdApplication.SoleInstance.MainWindow.PuntThroughEventLoop(ex);
                else
                    throw;
            }
#endif
        }

        private void chkHasUserSpecifiedMeanAndSD_CheckedChanged(object sender, EventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                bool enableLimits = !chkHasUserSpecifiedMeanAndSD.Checked;
                chkHasUserSpecifiedLimits.Enabled = enableLimits;
                lblLowerWarning.Enabled = enableLimits;
                txtLowerWarning.Enabled = enableLimits;
                lblUpperWarning.Enabled = enableLimits;
                txtUpperWarning.Enabled = enableLimits;
                lblLowerControl.Enabled = enableLimits;
                txtLowerControl.Enabled = enableLimits;
                lblUpperControl.Enabled = enableLimits;
                txtUpperControl.Enabled = enableLimits;
                cboKObs.Enabled = enableLimits;
                lblObservations.Enabled = enableLimits;
                lblUseFirst.Enabled = enableLimits;
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                if (SdApplication.SoleInstance.MainWindow.InOperation)
                    SdApplication.SoleInstance.MainWindow.PuntThroughEventLoop(ex);
                else
                    throw;
            }
#endif
        }

        void IOkable.OkClicked()
        {
            FillOptionsFromForm();
        }

        private void cmdPreview_Click(object sender, EventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                FillOptionsFromForm();
                PreviewChart();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                if (SdApplication.SoleInstance.MainWindow.InOperation)
                    SdApplication.SoleInstance.MainWindow.PuntThroughEventLoop(ex);
                else
                    throw;
            }
#endif
        }

        private bool PreviewAsAscii
        {
            get
            {
                if (definition.ChartType == ChartType.BoxWhisker)
                    return ((BoxWhiskerOptions)definition.ChartOptions).IsAscii;
                if (definition.ChartType == ChartType.Histogram)
                    return ((HistogramOptions)definition.ChartOptions).IsAscii;
                if (definition.ChartType == ChartType.ScatterXY)
                    return ((ScatterXYOptions)definition.ChartOptions).IsAscii;
                return false;
            }
        }

        private void PreviewChart()
        {
            ChartOptionProcessor.PostProcessFilledChartOptions(definition);
            using (IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(definition))
            {
                if (PreviewAsAscii)
                {
                    renderer.IsAscii = true;
                    ParameterBag outputParameters = renderer.Plot(SdApplication.SoleInstance);
                    if (null == outputParameters)
                    {
                        // Plot failed
                        return;
                    }
                    using (frmTextPreview textPreview = new frmTextPreview())
                    {
                        string rtf = "{\\rtf1\\ansi " + renderer.GetAsciiRTF() + "}";
                        textPreview.Rtf = rtf;
                        textPreview.ShowDialog(SdApplication.SoleInstance.MainWindow);
                    }
                }
                else
                {
                    ParameterBag outputParameters = renderer.Plot(SdApplication.SoleInstance);
                    if (null == outputParameters)
                    {
                        // Plot failed
                        return;
                    }
                    Stream imageStream = renderer.GetImageStream();
                    Image metaImage = Image.FromStream(imageStream);
                    using (frmImagePreview imagePreview = new frmImagePreview())
                    {
                        imagePreview.Image = metaImage;
                        imagePreview.ShowDialog(SdApplication.SoleInstance.MainWindow);
                    }
                }
            }
        }

        private void rdoMonochrome_CheckedChanged(object sender, EventArgs e)
        {
            SetColour(false);
        }

        private void rdoColour_CheckedChanged(object sender, EventArgs e)
        {
            SetColour(true);
        }

        private void SetColour(bool useColour)
        {
            seriesOptions.SetColour(useColour);
        }

        private void ctlHistogramOptions1_ScaleChanged(object sender, EventArgs e)
        {
            Rescale();
        }

        private void rdoNormalExpectedNormalOrder_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoNormalExpectedNormalOrder.Checked)
                Rescale();
        }

        private void Rescale()
        {
            UncacheScaleParameters();
            FillFormFromScaleParameters();
        }

        private void rdoNormalBlom_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoNormalBlom.Checked)
                Rescale();
        }

        private void rdoNormalVanDerWaerden_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoNormalVanDerWaerden.Checked)
                Rescale();
        }

        private void rdoNormalScaled_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoNormalScaled.Checked)
                Rescale();
        }

        private void rdoNormalRaw_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoNormalRaw.Checked)
                Rescale();
        }

        private void ctlBoxWhiskerOptions1_XAxisTitleChanged(object sender, EventArgs e)
        {
            ctlAxisOptions.X.Title = definition.ChartOptions.XAxisTitle;
        }

        private void ctlBarOptions_BarTypeChanged(object sender, EventArgs e)
        {
            CheckBarScales();
        }

        private void CheckBarScales()
        {
            BarOptions bOptions = (BarOptions)definition.ChartOptions;
            ctlOneAxisOptions yo = ctlAxisOptions.Y;
            if (bOptions.Stacked)
            {
                if (bOptions.Stacked100Percent)
                {
                    yo.MinimumDataValue = 0;
                    yo.DataMinGreaterThanZero = 0;
                    yo.MaximumDataValue = 100;
                }
                else
                {
                    double minValue = 0; // Was double.MaxValue, but we want most axes to start from 0 (unless there are negative values being plotted)
                    double maxValue = double.MinValue;

                    if (bOptions.RotateWhenStacked)
                    {
                        // Rotated - add up the values down the series
                            foreach (Series s in definition.YSeries)
                            {
                                double sum = s.AsDoubleSeries.Sum;
                                if (sum < minValue)
                                    minValue = sum;
                                if (sum > maxValue)
                                    maxValue = sum;
                            }
                    }
                    else
                    {
                        // Straight - add up the values across the series
                        for (int i = 0; i < definition.YSeries[0].AsDoubleSeries.Points; i++)
                        {
                            double totalValue = 0;
                            foreach (Series s in definition.YSeries)
                            {
                                double value = s.AsDoubleSeries.Data[i];
                                if (value != Constant.MISSING)
                                    totalValue += value;
                            }
                            if (totalValue < minValue)
                                minValue = totalValue;
                            if (totalValue > maxValue)
                                maxValue = totalValue;
                        }
                    }
                    yo.MinimumDataValue = minValue;
                    yo.DataMinGreaterThanZero = minValue;
                    yo.MaximumDataValue = maxValue;
                }
            }
            else
            {
                yo.MinimumDataValue = definition.ScaleParameters.Y.Min;
                yo.DataMinGreaterThanZero = definition.ScaleParameters.Y.MinGreaterThanZero;
                yo.MaximumDataValue = definition.ScaleParameters.Y.Max;
            }
        }

        private void rdoOrientationHorizontal_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoOrientationHorizontal.Checked)
                OrientationChanged();
        }

        private void OrientationChanged()
        {
            FillOrientationFromForm();
            ctlAxisOptions.AxisLabelsAreSwapped = !options.IsNaturalOrientation;
        }

        private void rdoOrientationVertical_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoOrientationVertical.Checked)
                OrientationChanged();
        }

        private void chkScatterXYPlotLines_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                SetShowLineOptions(chkScatterXYPlotLines.Checked);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Error while changing line options", ex, false);
            }
        }

        private void SetShowLineOptions(bool showLineOptions)
        {
            seriesOptions.SetShowLineOptions(showLineOptions);
        }

        private void chkScatterXYPlotMarkers_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                SetShowMarkerOptions(chkScatterXYPlotMarkers.Checked);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Error while changing line options", ex, false);
            }
        }

        private void SetShowMarkerOptions(bool showMarkerOptions)
        {
            seriesOptions.SetShowMarkerOptions(showMarkerOptions);
        }
    }
}
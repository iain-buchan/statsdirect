using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StatsDirect.Numerics;
using StatsDirect.Charting;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using StatsDirect.Charting.Options;
using System.Linq;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    internal partial class ctlChartOptions : UserControl, IOkable
    {
        private const float DEFAULT_AXIS_LINE_THICKNESS = 1.0f; // TODO: This should come from a global defaults somewhere!

        private readonly ChartDefinition definition;
        private AbstractGenericOptions options;

        private IChartPreferences ChartPreferences { get; }
        private IChartRendererFactory ChartRendererFactory { get; }
        private ISdApplication SdApplication { get; }
        private ISdPreferences SdPreferences { get; }

        public ctlChartOptions(ChartDefinition definition, IChartPreferences chartPreferences, IChartRendererFactory chartRendererFactory, ISdApplication sdApplication, ISdPreferences sdPreferences)
        {
            ChartPreferences = chartPreferences;
            ChartRendererFactory = chartRendererFactory;
            SdApplication = sdApplication;
            SdPreferences = sdPreferences;
            this.definition = definition;
            options = (AbstractGenericOptions)definition.ChartOptions;
            InitializeComponent();
            FillFormFromOptions();
        }

        private void FillOptionsFromForm()
        {
            definition.ScaleParameters = new(
                AxisScaleParametersFromForm(definition.ScaleParameters.X, ctlAxisOptions.X, false),
                AxisScaleParametersFromForm(definition.ScaleParameters.Y, ctlAxisOptions.Y, true)
            );
            if (options is BarOptions)
                options = BarOptionsFromForm();
            if (options is BoxWhiskerOptions)
                options = BoxWhiskerOptionsFromForm();
            if (options is ControlOptions)
                options = ControlOptionsFromForm();
            if (options is ErrorBarOptions)
                FillErrorBarOptionsFromForm();
            if (options is ForestOptions)
                FillForestOptionsFromForm();
            if (options is HistogramOptions)
                FillHistogramOptionsFromForm();
            if (options is NormalOptions)
                FillNormalOptionsFromForm();
            if (options is PyramidOptions)
                FillPyramidOptionsFromForm();
            if (options is ROCOptions)
                FillRocOptionsFromForm();
            if (options is ScatterXYOptions)
                FillScatterXYOptionsFromForm();
            if (options is SurvivalOptions)
                FillSurvivalOptionsFromForm();
            if (options.SeriesOptions is not null)
            {
                seriesOptions.Save();
                options.MarkerTypes = seriesOptions.MarkerTypes;
            }
        }

        private AbstractGenericOptions GenericOptionsFromForm()
        {
            string[]? seriesTitles = default;
            if (options is ISeriesTitlesOptions)
            {
                seriesTitles = new string[options.SeriesTitles.Count];
                for (int i = 0; i < options.SeriesTitles.Count; i++)
                    seriesTitles[i] = (string)gridSeriesLabels.Rows[i].Cells[0].Value;
            }
            return new GenericOptions(options.ChartPreferences,
                options is IAxisLabelFontOptions ? FontCache.DescriptorFromFont(ctlAxisLabelFont.UserFont) : default,
                options.UsesAxisLineThickness ? ctlAxisLineThickness.LineThickness : default,
                options is IAxisTitleFontOptions ? FontCache.DescriptorFromFont(ctlAxisTitleFont.UserFont) : default,
                options is ILegendFontOptions ? FontCache.DescriptorFromFont(ctlLegendFont.UserFont) : default,
                options.UsesOrientation ? ChartOrientationFromForm() : default,
                seriesTitles,
                options.ShouldAutoscale,
                options is IBoxAxesOptions ? chkBoxAxes.Checked : default,
                (options.UsesShowLegend && options.ShowLegendIsRelevant) ? chkShowLegend.Checked : default,
                options is IChartTitleOptions ? txtChartTitle.Text : default,
                options.UsesTitleFontDescriptor ? FontCache.DescriptorFromFont(ctlTitleFont.UserFont) : default,
                rdoColour.Checked,
                options is IXAxisTitleOptions ? ctlAxisOptions.X.Title : default,
                options is IYAxisTitleOptions ? ctlAxisOptions.Y.Title : default
            );
        }

        private static AxisScaleParameters AxisScaleParametersFromForm(AxisScaleParameters template, ctlOneAxisOptions ao, bool isYAxis)
        {
            IAxisScaler? scaler = AxisScalerFactory.AxisScalerFor(template.ScaleType);
            return new(template)
            {
                AxisScale = scaler is not null
                    ? scaler.QAxis(ao.MinimumScaleValue, ao.MinimumScaleValue, ao.MaximumScaleValue, isYAxis, true)
                    : template.AxisScale,
                GridLineDashStyle = ao.GridLineDashStyle,
                HasGridLines = ao.HasGridLines,
                LabelDirection = ao.LabelDirection,
                MarkerLineValue = ao.HasMarkerLine ? ao.MarkerLineValue : default(double?),
                ScaleType = ao.ScaleType,

            };
        }

        private ChartOrientation? ChartOrientationFromForm()
        {
            if (options.UsesOrientation)
            {
                if (rdoOrientationHorizontal.Checked)
                    return ChartOrientation.Horizontal;
                if (rdoOrientationVertical.Checked)
                    return ChartOrientation.Vertical;
            }
            return default;
        }

        private BarOptions BarOptionsFromForm()
        {
            BarOptions oldOptions = (BarOptions)options;
            IBarOptions barOptions = ctlBarOptions;
            double maxBarWidth = oldOptions.MaxBarWidth;
            if (double.TryParse(txtBarBarWidthPercent.Text, out double parsedMaxBarWidth))
                maxBarWidth = parsedMaxBarWidth / 100.0;
            return new(GenericOptionsFromForm(),
                (IReadOnlyList<MarkerType>?)seriesOptions.MarkerTypes,
                oldOptions.SeriesOptions,
                maxBarWidth,
                oldOptions.RotateWhenStacked,
                barOptions.Stacked,
                barOptions.Stacked100Percent
            );
        }

        private BoxWhiskerOptions BoxWhiskerOptionsFromForm()
        {
            IBoxWhiskerOptions boxWhiskerOptions = ctlBoxWhiskerOptions1;
            return new(GenericOptionsFromForm(),
                cco: boxWhiskerOptions.Cco,
                isAscii: something,
                markMeanAndMedian: boxWhiskerOptions.MarkMeanAndMedian,
                method: boxWhiskerOptions.Method,
                useInnerFence: boxWhiskerOptions.UseInnerFence,
                useOuterFence: boxWhiskerOptions.UseOuterFence
            );
        }

        private ControlOptions ControlOptionsFromForm()
        {
            ControlAndWarningLimits? controlAndWarningLimits = default;
            MeanAndStandardDeviation? meanAndStandardDeviation = default;
            int? observationsToUse = default;
            if (chkHasUserSpecifiedLimits.Checked)
                controlAndWarningLimits = new ControlAndWarningLimits(
                    lowerWarningLimit: double.TryParse(txtLowerWarning.Text, out double lowerWarningLimit) ? lowerWarningLimit : default,
                    upperWarningLimit: double.TryParse(txtUpperWarning.Text, out double upperWarningLimit) ? upperWarningLimit : default,
                    lowerControlLimit: double.TryParse(txtLowerControl.Text, out double lowerControlLimit) ? lowerControlLimit : default,
                    upperControlLimit: double.TryParse(txtUpperControl.Text, out double upperControlLimit) ? upperControlLimit : default
                );
            else if (chkHasUserSpecifiedMeanAndSD.Checked)
                meanAndStandardDeviation = new(
                    Parsing.Cdbl_Txt(txtMean.Text),
                    Parsing.Cdbl_Txt(txtStandardDeviation.Text)
                );
            else
                observationsToUse = int.TryParse((string)cboKObs.SelectedItem, out int i) && i > 2 ? i : default;
            return new(GenericOptionsFromForm(),
                controlAndWarningLimits,
                observationsToUse,
                int.TryParse(cboControlDecimalPlaces.Text, out int rightHandDecimalPlaces) ? rightHandDecimalPlaces : default,
                chk1SD.Checked,
                chk2SD.Checked,
                chk3SD.Checked,
                chkMean.Checked,
                meanAndStandardDeviation
            );
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
            if (double.TryParse(cboRocCi.Text, out rocOptions.Gamma))
                rocOptions.Gamma /= 100.0;
            rocOptions.Comparison = RocOptionComparisonFromForm();
        }

        private Comparison RocOptionComparisonFromForm()
        {
            if (rdoRocCutOffGe.Checked)
                return Comparison.GreaterEqual;
            if (rdoRocCutOffGt.Checked)
                return Comparison.GreaterThan;
            if (rdoRocCutOffLe.Checked)
                return Comparison.LessEqual;
            if (rdoRocCutOffLt.Checked)
                return Comparison.LessThan;
            // None checked; choose a default
            return Comparison.GreaterEqual;
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
            int.TryParse(cboForestDecimalPlaces.Text, out int effectSizeAndIntervalDecimalPlaces);
            forestOptions.EffectSizeAndIntervalDecimalPlaces = effectSizeAndIntervalDecimalPlaces;
            forestOptions.MarkCentres = chkForestMarkCentres.Checked;
        }

        private void FillNormalOptionsFromForm()
        {
            NormalOptions normalOptions = (NormalOptions)options;
            normalOptions.Method = ScoreMethodFromForm();
            normalOptions.ShouldScaleZ = rdoNormalScaled.Checked;
        }

        private NormalOptions.ScoreMethod ScoreMethodFromForm()
        {
            if (rdoNormalBlom.Checked)
                return NormalOptions.ScoreMethod.Blom;
            if (rdoNormalExpectedNormalOrder.Checked)
                return NormalOptions.ScoreMethod.ExpectedNormalOrder;
            if (rdoNormalVanDerWaerden.Checked)
                return NormalOptions.ScoreMethod.VanDerWaerden;
            // One of those should have been checked!  Return a default
            return NormalOptions.ScoreMethod.Blom;
        }

        private void FillFormFromOptions()
        {
            rdoColour.Checked = options.UseColour;
            rdoMonochrome.Checked = !rdoColour.Checked;
            SetColour(options.UseColour);
            pnlBoxAxes.Visible = options is IBoxAxesOptions;
            pnlChartTitle.Visible = options is IChartTitleOptions;
            pnlSeriesLabels.Visible = options is ISeriesTitlesOptions;

            FillFormFromScaleParameters();

            pnlBarOptions.Visible = options is IBarOptions;
            ctlBoxWhiskerOptions1.Visible = options is BoxWhiskerOptions;
            pnlControlOptions.Visible = options is ControlOptions;
            pnlForestOptions.Visible = options is ForestOptions;
            ctlHistogramOptions1.Visible = options is HistogramOptions;
            pnlNormalOptions.Visible = options is NormalOptions;
            pnlPyramidOptions.Visible = options is PyramidOptions;
            pnlRocOptions.Visible = options is ROCOptions;
            // Pure scatter charts always show their markers, which is the only option; line charts may or may not, and error bars may or may not.
            tlpScatterXYOptions.Visible = options is ScatterXYOptions || options is ErrorBarOptions;
            chkScatterXYPlotMarkers.Visible = definition.ChartType != ChartType.ScatterXY;
            chkScatterXYPlotLines.Visible = definition.ChartType != ChartType.LineXY;
            pnlSurvivalOptions.Visible = options is SurvivalOptions;
            pnlLegendFont.Visible = options is ControlOptions;
            pnlTitleFont.Visible = options.UsesTitleFontDescriptor;
            pnlAxisLabelFont.Visible = options is IAxisLabelFontOptions;
            pnlAxisTitleFont.Visible = options is IAxisTitleFontOptions;
            pnlSeriesOptions.Visible = options is IMarkerTypes mt && mt.SeriesOptions is not null && mt.SeriesOptions.Count > 0;
            pnlAxisLineThickness.Visible = options.UsesAxisLineThickness;
            pnlLegendFont.Visible = options is ILegendFontOptions;
            pnlOrientation.Visible = options.UsesOrientation;
            pnlShowLegend.Visible = options.UsesShowLegend && options.ShowLegendIsRelevant;
            pnlColour.Visible = options.UsesColour;
            ctlAxisOptions.ShowX = options.UsesXAxisOptions;
            ctlAxisOptions.ShowY = options.UsesYAxisOptions;
            if (pnlOrientation.Visible)
                grpOrientation.Text = options.OrientationLabel;
            if (options.UsesShowLegend && options.ShowLegendIsRelevant)
                chkShowLegend.Checked = options.ShowLegend;
            if (options is IBoxAxesOptions)
                chkBoxAxes.Checked = options.ShouldBoxAxes;
            if (options is IChartTitleOptions)
                txtChartTitle.Text = options.Title;
            if (options is ISeriesTitlesOptions)
                foreach (string? seriesTitle in options.SeriesTitles)
                    gridSeriesLabels.Rows.Add(seriesTitle);
            if (options is IXAxisTitleOptions)
                ctlAxisOptions.X.Title = options.XAxisTitle;
            if (options is IYAxisTitleOptions)
                ctlAxisOptions.Y.Title = options.YAxisTitle;
            if (options is BarOptions)
                FillFormFromBarOptions();
            if (options is BoxWhiskerOptions)
                FillFormFromBoxWhiskerOptions();
            if (options is ControlOptions)
                FillFormFromControlOptions();
            if (options is ErrorBarOptions)
                FillFormFromErrorBarOptions();
            if (options is ForestOptions)
                FillFormFromForestOptions();
            if (options is HistogramOptions)
                FillFormFromHistogramOptions();
            if (options is NormalOptions)
                FillFormFromNormalOptions();
            if (options is PyramidOptions)
                FillFormFromPyramidOptions();
            if (options is ROCOptions)
                FillFormFromRocOptions();
            if (options is ScatterXYOptions)
                FillFormFromScatterXYOptions();
            if (options is SurvivalOptions)
                FillFormFromSurvivalOptions();
            if (options is IAxisLabelFontOptions)
            {
                ctlAxisLabelFont.Purpose = "Axis Label Font";
                if (options.AxisLabelFontDescriptor is not null)
                    ctlAxisLabelFont.UserFont = FontCache.FontFromDescriptor(options.AxisLabelFontDescriptor);
            }
            if (options is IAxisTitleFontOptions)
            {
                if (options.AxisTitleFontDescriptor is not null)
                    ctlAxisTitleFont.UserFont = FontCache.FontFromDescriptor(options.AxisTitleFontDescriptor);
            }
            if (options is ILegendFontOptions)
            {
                ctlLegendFont.Purpose = options.LegendFontLabel + " Font";
                if (options.LegendFontDescriptor is not null)
                    ctlLegendFont.UserFont = FontCache.FontFromDescriptor(options.LegendFontDescriptor);
            }
            if (options.UsesTitleFontDescriptor)
            {
                if (options.TitleFontDescriptor is not null)
                    ctlTitleFont.UserFont = FontCache.FontFromDescriptor(options.TitleFontDescriptor);
            }
            if (options.UsesAxisLineThickness)
            {
                ctlAxisLineThickness.LineThickness = (int)(options.AxisLineThickness ?? DEFAULT_AXIS_LINE_THICKNESS);
            }
            if (pnlSeriesOptions.Visible)
            {
                seriesOptions.ForcedIsFilled = options.ForcedIsFilled;
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
            ctlAxisOptions.X.HasTitle = options is IXAxisTitleOptions;
            ctlAxisOptions.Y.HasTitle = options is IYAxisTitleOptions;
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
            cboRocCi.Text = (rocOptions.Gamma * 100.0).ToString();
            cboRocWeight.Text = rocOptions.Weight.ToString("N1");
            switch (rocOptions.Comparison)
            {
                case Comparison.GreaterEqual:
                    rdoRocCutOffGe.Checked = true;
                    break;
                case Comparison.GreaterThan:
                    rdoRocCutOffGt.Checked = true;
                    break;
                case Comparison.LessEqual:
                    rdoRocCutOffLe.Checked = true;
                    break;
                case Comparison.LessThan:
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
            chkHasUserSpecifiedMeanAndSD.Checked = controlOptions.UserSpecifiedMeanAndStandardDeviation is not null;
            cboControlDecimalPlaces.Text = controlOptions.RightHandDecimalPlaces.ToString();
        }

        private void chkHasUserSpecifiedLimits_CheckedChanged(object? sender, EventArgs e)
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
                if (SdApplication.InOperation)
                    SdApplication.PuntThroughEventLoop(ex);
                else
                    throw;
            }
#endif
        }

        private void chkHasUserSpecifiedMeanAndSD_CheckedChanged(object? sender, EventArgs e)
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
                if (SdApplication.InOperation)
                    SdApplication.PuntThroughEventLoop(ex);
                else
                    throw;
            }
#endif
        }

        void IOkable.OkClicked()
        {
            FillOptionsFromForm();
        }

        private void cmdPreview_Click(object? sender, EventArgs e)
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
                if (SdApplication.InOperation)
                    SdApplication.PuntThroughEventLoop(ex);
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
            new ChartDefinitionProcessor(ChartPreferences, SdPreferences).PostProcessFilledChartOptions(definition);
            definition.IsAscii = PreviewAsAscii;
            // We're in Windows Forms land, so we know we can use EMF.
            using IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(definition, new EmfCanvasFactory());
            if (PreviewAsAscii)
            {
                ParameterBag outputParameters = renderer.Plot(false);
                if (outputParameters is null)
                {
                    // Plot failed
                    return;
                }
                using frmTextPreview textPreview = new();
                string rtf = "{\\rtf1\\ansi " + renderer.GetAscii().Replace(Environment.NewLine, Formatting.RTFCRLF) + "}";
                textPreview.Rtf = rtf;
                textPreview.ShowDialog(SdApplication.DialogOwner);
            }
            else
            {
                ParameterBag outputParameters = renderer.Plot(false);
                if (outputParameters is null)
                {
                    // Plot failed
                    return;
                }
                Stream imageStream = renderer.DetachAndReturnImageStream();
                Image metaImage = Image.FromStream(imageStream);
                using frmImagePreview imagePreview = new();
                imagePreview.Image = metaImage;
                imagePreview.ShowDialog(SdApplication.DialogOwner);
            }
        }

        private void rdoMonochrome_CheckedChanged(object? sender, EventArgs e)
        {
            SetColour(false);
        }

        private void rdoColour_CheckedChanged(object? sender, EventArgs e)
        {
            SetColour(true);
        }

        private void SetColour(bool useColour)
        {
            seriesOptions.SetColour(useColour);
        }

        private void ctlHistogramOptions1_ScaleChanged(object? sender, EventArgs e)
        {
            Rescale();
        }

        private void rdoNormalExpectedNormalOrder_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoNormalExpectedNormalOrder.Checked)
                Rescale();
        }

        private void Rescale()
        {
            UncacheScaleParameters();
            FillFormFromScaleParameters();
        }

        private void rdoNormalBlom_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoNormalBlom.Checked)
                Rescale();
        }

        private void rdoNormalVanDerWaerden_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoNormalVanDerWaerden.Checked)
                Rescale();
        }

        private void rdoNormalScaled_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoNormalScaled.Checked)
                Rescale();
        }

        private void rdoNormalRaw_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoNormalRaw.Checked)
                Rescale();
        }

        private void ctlBoxWhiskerOptions1_XAxisTitleChanged(object? sender, EventArgs e)
        {
            ctlAxisOptions.X.Title = definition.ChartOptions.XAxisTitle;
        }

        private void ctlBarOptions_BarTypeChanged(object? sender, EventArgs e)
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
                            foreach (DoubleSeries s in definition.YSeries.Cast<DoubleSeries>())
                            {
                                double sum = s.Sum;
                                if (sum < minValue)
                                    minValue = sum;
                                if (sum > maxValue)
                                    maxValue = sum;
                            }
                    }
                    else
                    {
                        // Straight - add up the values across the series
                        for (int i = 0; i < ((DoubleSeries)definition.YSeries[0]).Points; i++)
                        {
                            double totalValue = 0;
                            foreach (DoubleSeries s in definition.YSeries)
                            {
                                double value = s.Data[i];
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

        private void rdoOrientationHorizontal_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoOrientationHorizontal.Checked)
                OrientationChanged();
        }

        private void OrientationChanged()
        {
            FillOrientationFromForm();
            ctlAxisOptions.AxisLabelsAreSwapped = !options.IsNaturalOrientation;
        }

        private void rdoOrientationVertical_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoOrientationVertical.Checked)
                OrientationChanged();
        }

        private void chkScatterXYPlotLines_CheckedChanged(object? sender, EventArgs e)
        {
            try
            {
                SetShowLineOptions(chkScatterXYPlotLines.Checked);
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Error while changing line options", ex, false);
            }
        }

        private void SetShowLineOptions(bool showLineOptions)
        {
            seriesOptions.SetShowLineOptions(showLineOptions);
        }

        private void chkScatterXYPlotMarkers_CheckedChanged(object? sender, EventArgs e)
        {
            try
            {
                SetShowMarkerOptions(chkScatterXYPlotMarkers.Checked);
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Error while changing line options", ex, false);
            }
        }

        private void SetShowMarkerOptions(bool showMarkerOptions)
        {
            seriesOptions.SetShowMarkerOptions(showMarkerOptions);
        }
    }
}
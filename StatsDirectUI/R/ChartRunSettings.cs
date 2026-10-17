using System;
using System.Collections.Generic;
using System.Linq;
using StatsDirect.Charting;
using StatsDirect.Templates;

namespace StatsDirect.R
{
    /// <summary>Final chart choices as portable values, independent of host controls and mutable renderer objects.</summary>
    internal static class ChartRunSettings
    {
        public static ParameterBag Capture(ChartDefinition definition)
        {
            ChartOptions options = definition.ChartOptions;
            ParameterBag result = new();
            void Add(string key, object value) => result.AddInput(key, value);
            Add("type", definition.ChartType.ToString());
            Add("title", options.Title ?? "");
            Add("dataTitles", definition.XSeries.Concat(definition.YSeries).Select(series => series?.Title ?? "").ToArray());
            Add("xTitle", options.XAxisTitle ?? "");
            Add("yTitle", options.YAxisTitle ?? "");
            Add("lineWidth", (double)options.AxisLineThickness);
            Add("showLegend", options.ShowLegend);
            Add("useColour", options.UseColour);
            Add("x", Axis(definition.ScaleParameters.X));
            Add("y", Axis(definition.ScaleParameters.Y));
            if (options is GenericOptions generic)
            {
                Add("seriesTitles", generic.SeriesTitles ?? Array.Empty<string>());
                Add("horizontal", generic.Orientation == ChartOrientation.Horizontal);
                Add("boxAxes", options is ScatterXYOptions or ErrorBarOptions ? ChartPreferences.DefaultBoxAxes : generic.ShouldBoxAxes);
                Add("autoScale", generic.ShouldAutoscale);
                Font(result, "titleFont", generic.TitleFontDescriptor ?? ChartPreferences.DefaultTitleFont);
                Font(result, "axisTitleFont", generic.AxisTitleFontDescriptor ?? ChartPreferences.DefaultAxisTitleFont);
                Font(result, "axisLabelFont", generic.AxisLabelFontDescriptor ?? ChartPreferences.DefaultAxisLabelFont);
                Font(result, "legendFont", generic.LegendFontDescriptor ?? ChartPreferences.DefaultLegendFont);
            }
            int count = Math.Max(1, Math.Max(definition.XSeries.Count, definition.YSeries.Count));
            IList<MarkerType> markers = options.MarkerTypes;
            if (options is BarOptions { Stacked: true, RotateWhenStacked: true } rotated)
            {
                count = Math.Max(1, rotated.SeriesTitles?.Length ?? count);
                markers = null; // The native renderer creates new segment series and assigns the default markers after transposition.
            }
            Add("markers", Enumerable.Range(0, markers?.Count > 0 ? markers.Count : count)
                .Select(i => Marker(markers?.Count > 0 ? markers[i] : ChartPreferences.MarkerTypes[ChartOptions.SeriesNumberToMarkerNumber(i)], options.UseColour)).ToList());
            if (options is GenericOptions filled && filled.ShouldForceIsFilled && options is not ErrorBarOptions)
                foreach (ParameterBag marker in (IList<ParameterBag>)result["markers"].AsObject)
                    marker["filled"] = FilledParameterFactory.Input(filled.ForcedIsFilled);
            if (options is GenericOptions fillStyle && fillStyle.ShouldForceFillStyle)
                foreach (ParameterBag marker in (IList<ParameterBag>)result["markers"].AsObject)
                    marker["fillStyle"] = FilledParameterFactory.Input((int)fillStyle.ForcedFillStyle);
            switch (options)
            {
                case BarOptions bar:
                    if (bar.Orientation == ChartOrientation.Horizontal)
                        foreach (ParameterBag marker in (IList<ParameterBag>)result["markers"].AsObject)
                            marker["dashStyle"] = FilledParameterFactory.Input(0); // Native horizontal outlines use solid pens.
                    Add("stacked", bar.Stacked); Add("rotateWhenStacked", bar.RotateWhenStacked);
                    Add("stacked100Percent", bar.Stacked100Percent); Add("maxBarWidth", bar.MaxBarWidth);
                    break;
                case NormalOptions normal:
                    Add("method", (int)normal.Method); Add("scaleZ", normal.ShouldScaleZ);
                    break;
                case HistogramOptions histogram:
                    foreach (ParameterBag marker in (IList<ParameterBag>)result["markers"].AsObject)
                        marker["width"] = FilledParameterFactory.Input((double)histogram.LineWidth);
                    Add("overlayNormalCurve", histogram.OverlayNormalCurve); Add("poolVariablesForBins", histogram.PoolVariablesForBins);
                    Add("relativeFrequencies", histogram.ShowRelativeFrequencies); Add("binChoiceMethod", (int)histogram.BinChoiceMethod);
                    Add("histograms", (histogram.HistoSeriesOptions ?? new()).Select(series =>
                    {
                        ParameterBag bag = new();
                        bag.AddInput("title", series.ChartTitle ?? ""); bag.AddInput("xTitle", series.XAxisTitle ?? ""); bag.AddInput("yTitle", series.YAxisTitle ?? "");
                        if (series.BinsDescriptor != null)
                        {
                            bag.AddInput("edges", series.BinsDescriptor.Edges.ToList());
                            bag.AddInput("counts", series.BinsDescriptor.Counts.Select(c => (double)c).ToList());
                        }
                        return bag;
                    }).ToList());
                    break;
                case ScatterXYOptions scatter:
                    Add("plotMarkers", scatter.PlotMarkers); Add("joinMarkersWithLines", scatter.JoinMarkersWithLines);
                    break;
                case ErrorBarOptions error:
                    Add("plotMarkers", error.PlotMarkers); Add("joinMarkersWithLines", error.JoinMarkersWithLines);
                    Add("checkForOffsets", error.ShouldCheckForOffsets);
                    break;
                case PyramidOptions pyramid:
                    Add("scaleMaximum", pyramid.ScaleMaximum);
                    Add("pyramidTitles", new[] { pyramid.MaleFrame?.Variables.FirstOrDefault()?.Title ?? "", pyramid.FemaleFrame?.Variables.FirstOrDefault()?.Title ?? "" });
                    break;
                case ControlOptions control:
                    Add("useMean", control.UseMean); Add("use1SD", control.Use1SD); Add("use2SD", control.Use2SD); Add("use3SD", control.Use3SD);
                    Add("hasUserSpecifiedMeanAndSD", control.HasUserSpecifiedMeanAndSD);
                    Add("userSpecifiedMean", control.UserSpecifiedMean); Add("userSpecifiedSD", control.UserSpecifiedSD);
                    Add("hasUserSpecifiedLimits", control.HasUserSpecifiedLimits);
                    Add("lowerWarningLimit", control.LowerWarningLimit); Add("upperWarningLimit", control.UpperWarningLimit);
                    Add("lowerControlLimit", control.LowerControlLimit); Add("upperControlLimit", control.UpperControlLimit);
                    Add("observationsToUse", control.ObservationsToUse); Add("rightHandDecimalPlaces", control.RightHandDecimalPlaces);
                    break;
                case SurvivalOptions survival:
                    Add("showCensorshipTics", survival.ShowCensorshipTics); Add("showEventMarkers", survival.ShowEventMarkers);
                    Add("useSeriesColourForConfidenceIntervals", survival.UseSeriesColourForConfidenceIntervals);
                    Add("curveShapes", survival.Series.Select(series =>
                    {
                        ParameterBag shape = new();
                        shape.AddInput("length", series.XDat?.Length ?? 0);
                        shape.AddInput("hasLower", series.YDatL != null); shape.AddInput("hasUpper", series.YDatU != null);
                        shape.AddInput("hasEvent", series.CDat != null);
                        return shape;
                    }).ToList());
                    if (survival.Series.Sum(series => (long)(series.XDat?.Length ?? 0) + (series.YDat?.Length ?? 0)
                        + (series.YDatL?.Length ?? 0) + (series.YDatU?.Length ?? 0) + (series.CDat?.Length ?? 0)) <= RunRecord.EmbeddingThreshold)
                    Add("curves", survival.Series.Select(series =>
                    {
                        ParameterBag curve = new();
                        curve.AddInput("x", (series.XDat ?? Array.Empty<double>()).ToList());
                        curve.AddInput("y", (series.YDat ?? Array.Empty<double>()).ToList());
                        if (series.YDatL != null) curve.AddInput("lower", series.YDatL.ToList());
                        if (series.YDatU != null) curve.AddInput("upper", series.YDatU.ToList());
                        if (series.CDat != null) curve.AddInput("event", series.CDat.Select(value => (double)value).ToList());
                        return curve;
                    }).ToList());
                    break;
                case ForestOptions forest:
                    Add("forestTitles", (forest.Titles ?? Array.Empty<string>()).ToArray());
                    Add("effectDecimals", forest.EffectSizeAndIntervalDecimalPlaces); Add("confidence", forest.cco);
                    Add("markCentres", forest.MarkCentres);
                    break;
                case ROCOptions roc:
                    Add("comparison", (int)roc.Comparison); Add("confidence", roc.GAMMA); Add("weight", roc.Weight);
                    Add("showOptimumCutOff", roc.ShowOptimumCutOff);
                    break;
            }
            return result;
        }

        private static ParameterBag Axis(AxisScaleParameters axis)
        {
            ParameterBag bag = new();
            bag.AddInput("scale", (int)axis.ScaleType); bag.AddInput("grid", axis.HasGridLines);
            bag.AddInput("gridStyle", (int)axis.GridLineDashStyle); bag.AddInput("labelDirection", (int)axis.LabelDirection);
            if (axis.MarkerLineValue is double marker && double.IsFinite(marker)) bag.AddInput("marker", marker);
            if (axis.AxisScale is IAxisScale scale && double.IsFinite(scale.MinimumScaleValue) && double.IsFinite(scale.MaximumScaleValue)
                && scale.MinimumScaleValue < scale.MaximumScaleValue)
            {
                bag.AddInput("minimum", scale.MinimumScaleValue); bag.AddInput("maximum", scale.MaximumScaleValue);
                if (scale is ILinearAxisScale linear && double.IsFinite(linear.Interval) && linear.Interval > 0)
                {
                    bag.AddInput("interval", linear.Interval); bag.AddInput("firstTick", linear.FirstMajorTicValue);
                }
            }
            return bag;
        }

        private static void Font(ParameterBag bag, string prefix, FontDescriptor font)
        {
            if (font == null) return;
            bag.AddInput(prefix + "Family", font.FontFamily ?? "Arial");
            bag.AddInput(prefix + "Size", (double)font.SizeInPoints); bag.AddInput(prefix + "Style", font.Style);
        }

        private static ParameterBag Marker(MarkerType marker, bool colour)
        {
            static string Hex(ColorDescriptor color) => color == null ? "#000000" : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            ParameterBag bag = new();
            bag.AddInput("shape", (int)marker.MarkerShape); bag.AddInput("filled", marker.IsMarkerFilled);
            bag.AddInput("color", colour ? Hex(marker.MarkerColor) : "#000000");
            bag.AddInput("lineColor", colour ? Hex(marker.LineColor) : "#000000");
            bag.AddInput("width", (double)marker.Width); bag.AddInput("size", marker.MarkerSize);
            bag.AddInput("dashStyle", (int)marker.LineDashStyle);
            bag.AddInput("fillStyle", (int)marker.MarkerFillStyle);
            return bag;
        }
    }
}

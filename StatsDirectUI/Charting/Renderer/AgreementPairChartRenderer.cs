using StatsDirect.Charting.Options;
using StatsDirect.Charting.Scales;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;

namespace StatsDirect.Charting.Renderer
{
    class AgreementPairChartRenderer : AbstractXYChartRenderer, IChartRenderer
    {
        public AgreementPairChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory, ISdPreferences sdPreferences)
            : base(definition, canvasFactory, sdPreferences)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            AgreementOptions aOptions = (AgreementOptions)Definition.ChartOptions;
            Layout.Range mxdRange = GetMinMaxArray(aOptions.Mxd, ScaleType.Linear);
            double mxdMin = Math.Min(mxdRange.Min, aOptions.Lla);
            double mxdMax = Math.Max(mxdRange.Max, aOptions.Ula);
            Layout.Range avRange = GetMinMaxArray(aOptions.Av, ScaleType.Linear);
            double avMin = avRange.Min;
            double avMax = avRange.Max;

            return new ScaleParameters(
                new(new[] { ScaleType.Linear })
                {
                    ScaleType = ScaleType.Linear,
                    Max = avMax,
                    Min = avMin
                },
                new(new[] { ScaleType.Linear })
                {
                    ScaleType = ScaleType.Linear,
                    Max = mxdMax,
                    Min = mxdMin
                }
            );
        }

        ParameterBag IChartRenderer.Plot(bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            AgreementOptions aOptions = (AgreementOptions)Definition.ChartOptions;
            StartVectorPlot(aOptions);
            Layout.Range mxdRange = GetMinMaxArray(aOptions.Mxd, Definition.ScaleParameters.Y.ScaleType);
            double mxdMin = Math.Min(mxdRange.Min, aOptions.Lla);
            double mxdMax = Math.Max(mxdRange.Max, aOptions.Ula);
            AxisScales axisScales;
            PenDescriptor p = GetMarkerPen(ChartPreferences.MarkerTypes[0]);
            string xtxt = string.IsNullOrEmpty(aOptions.XAxisTitle)
                ? "mean"
                : aOptions.XAxisTitle;
            string ytxt = string.IsNullOrEmpty(aOptions.YAxisTitle)
                ? aOptions.HasLimits ? "difference" : "maximum difference"
                : aOptions.YAxisTitle;
            string title = aOptions.HasLimits
                ? $"Agreement Plot ({Formatting.XRound(100 * (1 - aOptions.P0), 2)}% limits of agreement)"
                : "Agreement Plot";
            axisScales = PlotXYInternal(aOptions.Av, aOptions.Mxd, xtxt, ytxt, title, false, DataMinMax.XCalc_YPreset, ChartPreferences.MarkerTypes[0].MarkerSize, ChartPreferences.MarkerTypes[0].MarkerShape, ChartPreferences.MarkerTypes[0].IsMarkerFilled, p, false, ChartAreaShape.Default, 0, 0, mxdMin, mxdMax);

            // Plot mean
            PenDescriptor greenPen = new(GrGreen, 2);
            DrawLineInChartCoordinates(greenPen, axisScales.X.MinimumScaleValue, aOptions.Mean, axisScales.X.MaximumScaleValue, aOptions.Mean);
            if (aOptions.HasLimits)
            {
                // Plot upper limit
                DrawLineInChartCoordinates(greenPen, axisScales.X.MinimumScaleValue, aOptions.Ula, axisScales.X.MaximumScaleValue, aOptions.Ula);
                // Plot lower limit
                DrawLineInChartCoordinates(greenPen, axisScales.X.MinimumScaleValue, aOptions.Lla, axisScales.X.MaximumScaleValue, aOptions.Lla);
            }
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}

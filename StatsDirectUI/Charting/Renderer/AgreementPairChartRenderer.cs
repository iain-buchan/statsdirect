using StatsDirect.Templates;
using StatsDirect.Utilities;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    class AgreementPairChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public AgreementPairChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            double avMin = 0;
            double avMax = 0;
            double mxdMin = 0;
            double mxdMax = 0;
            if (!(definition == null || definition.ChartOptions == null))
            {
                AgreementOptions aOptions = (AgreementOptions)definition.ChartOptions;
                Layout.Range mxdRange = GetMinMaxArray(aOptions.mxd, ScaleType.Linear);
                mxdMin = mxdRange.Min;
                mxdMax = mxdRange.Max;
                if (aOptions.HasLimits)
                {
                    if (aOptions.lla < mxdMin)
                        mxdMin = aOptions.lla;
                    if (aOptions.ula > mxdMax)
                        mxdMax = aOptions.ula;
                }
                Layout.Range avRange = GetMinMaxArray(aOptions.av, ScaleType.Linear);
                avMin = avRange.Min;
                avMax = avRange.Max;
            }

            return new ScaleParameters
            {
                X =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = avMax,
                    Min = avMin
                },
                Y =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = mxdMax,
                    Min = mxdMin
                }
            };
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            AgreementOptions aOptions = (AgreementOptions)definition.ChartOptions;
            StartVectorPlot();
            double mxdMin;
            double mxdMax;
            Layout.Range mxdRange = GetMinMaxArray(aOptions.mxd, definition.ScaleParameters.Y.ScaleType);
            mxdMin = mxdRange.Min;
            mxdMax = mxdRange.Max;
            using (Pen p = GetMarkerPen(ChartPreferences.MarkerTypes[0]))
            {
                if (aOptions.HasLimits)
                {
                    if (aOptions.lla < mxdMin)
                        mxdMin = aOptions.lla;
                    if (aOptions.ula > mxdMax)
                        mxdMax = aOptions.ula;
                    string xtxt = definition.ChartOptions.XAxisTitle;
                    if (string.IsNullOrEmpty(xtxt))
                        xtxt = "mean";
                    string ytxt = definition.ChartOptions.YAxisTitle;
                    if (string.IsNullOrEmpty(ytxt))
                        ytxt = "difference";
                    PlotXYInternal(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot (" + Formatting.XRound(100 * (1 - aOptions.P0), 2) + "% limits of agreement)", false, DataMinMax.XCalc_YPreset, ChartPreferences.MarkerTypes[0].MarkerSize, ChartPreferences.MarkerTypes[0].MarkerShape, ChartPreferences.MarkerTypes[0].IsMarkerFilled, p, false, 0, 0, mxdMin, mxdMax);
                }
                else
                {
                    string xtxt = definition.ChartOptions.XAxisTitle;
                    if (string.IsNullOrEmpty(xtxt))
                        xtxt = "mean";
                    string ytxt = definition.ChartOptions.YAxisTitle;
                    if (string.IsNullOrEmpty(ytxt))
                        ytxt = "maximum difference";
                    PlotXYInternal(aOptions.av, aOptions.mxd, xtxt, ytxt, "Agreement Plot", false, DataMinMax.XCalc_YPreset, ChartPreferences.MarkerTypes[0].MarkerSize, ChartPreferences.MarkerTypes[0].MarkerShape, ChartPreferences.MarkerTypes[0].IsMarkerFilled, p, false, 0, 0, mxdMin, mxdMax);
                }
            }

            // Plot mean
            using (Pen greenPen = new Pen(grGreen, 2))
            {
                double y1 = ToCanvasY(aOptions.mean);
                DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                if (aOptions.HasLimits)
                {
                    // Plot upper limit
                    y1 = ToCanvasY(aOptions.ula);
                    DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                    // Plot lower limit
                    y1 = ToCanvasY(aOptions.lla);
                    DrawLineInCanvasCoordinates(greenPen, xAxisCanvas, y1, xAxisCanvas + xExtCanvas, y1);
                }
            }
            EndVectorPlot();
            return new ParameterBag();
        }
    }
}

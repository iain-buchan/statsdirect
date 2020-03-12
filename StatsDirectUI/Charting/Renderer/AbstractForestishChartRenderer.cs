using StatsDirect.Charting.Scales;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsDirect.Charting.Renderer
{
    internal abstract class AbstractForestishChartRenderer : AbstractChartRenderer
    {
        protected const double featureHeight = 0.667;
        protected const double arrowWidth = 0.125;
        protected readonly MarkerType studyMarkerType = new MarkerType
        {
            MarkerColor = ColorDescriptor.Gray,
            LineColor = ColorDescriptor.Black,
            IsMarkerFilled = true,
            MarkerShape = MarkerShape.Square,
            LineDashStyle = DashStyleDescriptor.Solid,
            Width = 1
        };
        protected readonly MarkerType pooledMarkerType = new MarkerType
        {
            MarkerColor = ColorDescriptor.Gray,
            LineColor = ColorDescriptor.Black,
            IsMarkerFilled = true,
            MarkerShape = MarkerShape.Diamond,
            LineDashStyle = DashStyleDescriptor.Solid,
            Width = 1
        };

        protected AbstractForestishChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        protected static string RangeLabel(double odr, double odrl, double odru, double absMin) => Formatting.RoundMeta(odr, absMin) + " (" + Formatting.RoundMeta(odrl, absMin) + ", " + Formatting.RoundMeta(odru, absMin) + ")";

        protected void DrawRangeLabelInChartCoordinates(double odr, double odrl, double odru, double absMin, double yc) => DrawStringLabel(RangeLabel(odr, odrl, odru, absMin), XAxisCanvas + XExtCanvas + 10, ToCanvasY(yc), StringAlignment.Near, StringAlignment.Center);

        protected void PlotPooledMarker(double odr, double odrl, double odru, double absMin, double saveYc, string label)
        {
            PenDescriptor pooledCiPen = GetLinePen(pooledMarkerType, true);
            PenDescriptor pooledEffectPen = GetLinePen(ChartPreferences.MarkerTypes[10], false);

            double yc = 0.5;
            // Pooled effect marker line - draw first so that it's behind the marker and its lower end is therefore hidden.
            DrawLineInChartCoordinates(pooledEffectPen, odr, saveYc, odr, yc);

            DrawMarkerInChartCoordinates(odr, yc, ToCanvasHeight(featureHeight / 2), pooledMarkerType);
            // pooled marker
            DrawLineInChartCoordinates(pooledCiPen, odru, yc, odrl, yc);
            // pool label
            DrawStringLabel(ComboTi(label), XAxisCanvas - 15, ToCanvasY(yc), StringAlignment.Far, StringAlignment.Center);
            DrawRangeLabelInChartCoordinates(odr, odrl, odru, absMin, yc);
        }

        protected void PlotNoEffectMarker(AxisScales axisScales)
        {
            switch (Definition.ScaleParameters.X.ScaleType)
            {
                case ScaleType.Linear:
                    if (axisScales.X.MinimumScaleValue <= 0 && axisScales.X.MaximumScaleValue >= 0)
                        DrawLineInChartCoordinates(GrBlack, 0, axisScales.Y.MinimumScaleValue, 0, axisScales.Y.MaximumScaleValue);
                    break;
                case ScaleType.Log10:
                case ScaleType.LogNatural:
                    if (axisScales.X.MinimumScaleValue <= 1 && axisScales.X.MaximumScaleValue >= 1)
                        DrawLineInChartCoordinates(GrBlack, 1, axisScales.Y.MinimumScaleValue, 1, axisScales.Y.MaximumScaleValue);
                    break;
            }
        }
        protected abstract bool IncludeTable(MHOptions options, int i);
    }
}

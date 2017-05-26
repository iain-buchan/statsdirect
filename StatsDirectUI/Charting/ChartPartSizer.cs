using System;
using System.Drawing;

using StatsDirect.Charting.Renderer;

namespace StatsDirect.Charting
{
    class ChartPartSizer : IChartSizableVisitor
    {
        private SizeF cachedSize;
        private readonly AbstractChartRenderer chartRenderer;

        public static SizeF Size(AbstractChartRenderer ch, IChartSizable sizable)
        {
            return new ChartPartSizer(ch).SizeInternal(sizable);
        }

        private ChartPartSizer(AbstractChartRenderer chartRenderer)
        {
            this.chartRenderer = chartRenderer;
        }

        private SizeF SizeInternal(IChartSizable sizable)
        {
            sizable.Accept(this);
            return cachedSize;
        }

        void IChartSizableVisitor.Visit(Legend legend)
        {
            const int INTER_ROW_GAP = 6;
            const int LEGEND_MARKER_SIZE = 6;
            const int MARKER_TO_LEGEND_GAP = 12;
            const int BORDER_WIDTH = 0;

            int rows = legend.LegendEntries.Count;
            float legendFontHeight = chartRenderer.LegendHeightInCanvasCoordinates("M");
            float rowHeight = Math.Max(LEGEND_MARKER_SIZE, legendFontHeight);
            float totalHeight = rows * rowHeight + (rows - 1) * INTER_ROW_GAP + BORDER_WIDTH * 2;

            float widestLegend = 0;
            foreach (LegendEntry entry in legend.LegendEntries)
            {
                float legendWidth = chartRenderer.LegendWidthInCanvasCoordinates(entry.Label);
                if (legendWidth > widestLegend)
                    widestLegend = legendWidth;
            }
            float totalWidth = LEGEND_MARKER_SIZE + MARKER_TO_LEGEND_GAP + widestLegend + 2 * BORDER_WIDTH;

            cachedSize = new SizeF(totalWidth, totalHeight);
        }
    }
}

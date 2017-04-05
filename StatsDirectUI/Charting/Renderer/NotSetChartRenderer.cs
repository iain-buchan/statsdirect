using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class NotSetChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        public NotSetChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            throw new NotImplementedException();
        }

        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            throw new NotImplementedException();
        }
    }
}

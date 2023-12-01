using System;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    public sealed class NotSetChartRenderer : IChartRenderer
    {
        public NotSetChartRenderer()
        {
        }

        Stream? IChartRenderer.DetachAndReturnImageStream() => throw new NotImplementedException();

        string IChartRenderer.GetAscii()
        {
            throw new NotImplementedException();
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            throw new NotImplementedException();
        }

        ParameterBag IChartRenderer.Plot(bool isForReturnedParametersOnly)
        {
            throw new NotImplementedException();
        }

        void IDisposable.Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}

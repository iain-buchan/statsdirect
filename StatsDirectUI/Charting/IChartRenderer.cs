using System;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public interface IChartRenderer : IDisposable
    {
        ParameterBag Plot(bool isForReturnedParametersOnly);
        Stream? DetachAndReturnImageStream();
        string GetAscii();

        ScaleParameters GetScaleParameters();
    }
}

using System;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public interface IChartRenderer : IDisposable
    {
        ParameterBag Plot(ITemplateHost host);

        ScaleParameters GetScaleParameters();

        string GetAsciiRtf();

        Stream GetImageStream();

        double ImageWidth { get; }
        double ImageHeight { get; }
    }
}

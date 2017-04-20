using System;
using System.IO;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public interface IChartRenderer : IDisposable
    {
        ParameterBag Plot(ITemplateHost host);

        ScaleParameters GetScaleParameters();

        string GetAsciiRTF();

        Stream GetImageStream();

        int ImageWidth { get; }
        int ImageHeight { get; }
    }
}

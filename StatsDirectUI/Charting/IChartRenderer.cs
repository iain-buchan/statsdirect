using StatsDirect.Templates;
using System;
using System.IO;

namespace StatsDirect.Charting
{
    public interface IChartRenderer : IDisposable
    {
        ParameterBag Plot(ITemplateHost host);

        ScaleParameters GetScaleParameters();

        bool IsAscii { get; set; }

        string GetAsciiRTF();

        Stream GetImageStream();

        int ImageWidth { get; }
        int ImageHeight { get; }
    }
}

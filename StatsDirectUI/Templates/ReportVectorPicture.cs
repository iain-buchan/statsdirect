using System;

namespace StatsDirect.Templates;

/// <summary>A vector picture returned by an external R script, independent of report markup: the markup of an svg element, and its size in pixels.</summary>
public sealed class ReportVectorPicture : IRenderable
{
    public string Svg { get; }
    public int Width { get; }
    public int Height { get; }

    public ReportVectorPicture(string svg, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(svg);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Svg = svg;
        Width = width;
        Height = height;
    }

    public void Accept(IRenderableVisitor visitor) => visitor.Visit(this);
}

using System;

namespace StatsDirect.Templates;

/// <summary>A PNG returned by an external R script, independent of report markup.</summary>
public sealed class ReportPicture : IRenderable
{
    public byte[] Png { get; }
    public int Width { get; }
    public int Height { get; }

    public ReportPicture(byte[] png, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Png = png;
        Width = width;
        Height = height;
    }

    public void Accept(IRenderableVisitor visitor) => visitor.Visit(this);
}

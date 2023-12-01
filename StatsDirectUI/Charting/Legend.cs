using System.Collections.Generic;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A sequence of items to be drawn at some point on a chart as a legend.
    /// </summary>
    /// <remarks>Immutable</remarks>
    public class Legend : IChartSizable
    {
        public LegendPosition Position { get; }

        public IReadOnlyList<LegendEntry> LegendEntries { get; }

        public Legend(LegendPosition position, IReadOnlyList<LegendEntry> legendEntries)
        {
            Position = position;
            LegendEntries = legendEntries;
        }

        void IChartSizable.Accept(IChartSizableVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

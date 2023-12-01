using System.Collections.Generic;

namespace StatsDirect.Charting
{
    internal interface IMarkerTypes
    {
        /// <summary>
        /// If null, do not force.
        /// </summary>
        FillStyle? ForcedFillStyle { get; }
        /// <summary>
        /// If null, do not force
        /// </summary>
        bool? ForcedIsFilled { get; }
        ///  <summary>
        ///  Marker details.
        ///  </summary>
        IReadOnlyList<MarkerType> MarkerTypes { get; }
        IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }
    }
}

using System;

namespace StatsDirect.Charting
{
    ///  <summary>
    ///  A way of specifying the options that can be set for a series when it is passed through to some UI.
    ///  </summary>
    ///  <remarks>Immutable</remarks>
    [Serializable]
    public class SeriesOptionsDescriptor  
    {
        public string? SeriesName { get; init; }
        public bool AllowChangeToMarkerType { get; init; } = true;
        public bool AllowChangeToMarkerSize { get; init; } = true;
        public bool AllowChangeToMarkerColour { get; init; } = true;
        public bool AllowChangeToLineColour { get; init; } = true;
        public bool AllowChangeToLineThickness { get; init; } = true;
        public bool AllowChangeToDashStyle { get; init; } = true;
        public bool AllowChangeToFill { get; init; } = false;
        ///  <summary>
        ///  The index of the marker that this descriptor will affect.  This is designed to allow multiple descriptors to affect the same marker.
        ///  </summary>
        public int MarkerIndex { get; init; }
        
        public SeriesOptionsDescriptor() 
        { 
        } 
    } 
} 

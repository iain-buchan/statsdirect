using System;

namespace StatsDirect.Charting
{
    ///  <summary>
    ///  A way of specifying the options that can be set for a series when it is passed through to some UI.
    ///  </summary>
    ///  <remarks></remarks>
    [Serializable]
    public class SeriesOptionsDescriptor  
    { 
        public string SeriesName; 
        public bool AllowChangeToMarkerType; 
        public bool AllowChangeToMarkerSize; 
        public bool AllowChangeToMarkerColour; 
        public bool AllowChangeToLineThickness; 
        public bool AllowChangeToDashStyle; 
        public bool AllowChangeToFill; 
        ///  <summary>
        ///  The index of the marker that this descriptor will affect.  This is designed to allow multiple descriptors to affect the same marker.
        ///  </summary>
        public int MarkerIndex; 
        
        public SeriesOptionsDescriptor() 
        { 
            AllowChangeToDashStyle = true; 
            AllowChangeToLineThickness = true; 
            AllowChangeToMarkerColour = true; 
            AllowChangeToMarkerSize = true; 
            AllowChangeToMarkerType = true; 
            AllowChangeToFill = false; 
        } 
    } 
    
    
} 

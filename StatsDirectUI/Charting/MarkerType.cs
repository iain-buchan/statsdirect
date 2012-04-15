using System;
using System.Drawing;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class MarkerType  
    { 
        public MarkerShape Shape; 
        public Color Color; 
        public float Width; 
        public System.Drawing.Drawing2D.DashStyle Style; 
        ///  <summary>
        ///  If true, the marker shape is filled; if false, it is hollow.
        ///  </summary>
        public bool IsFilled; 
        ///  <summary>
        ///  Sizes are defined in co-ordinate sizes.  If the canvas gets larger, marker sizes get relatively smaller.
        ///  </summary>
        public double MarkerSize; 
        public FillStyle FillStyle; 
        
        // TRANSMISSINGCOMMENT: Method Clone
        public MarkerType Clone() 
        {
            MarkerType m = new MarkerType
                               {
                                   Color = Color,
                                   Shape = Shape,
                                   Style = Style,
                                   Width = Width,
                                   IsFilled = IsFilled,
                                   MarkerSize = MarkerSize,
                                   FillStyle = FillStyle
                               };
            return m; 
        } 
    } 
} 

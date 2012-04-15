using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public class Axis  
    {
        public string Title { get; set; }

        public AxisMode Mode { get; set; }

        ///  <summary>
        ///  Space by which the axis should be shifted in
        ///  </summary>
        public double ExtraSpace { get; set; }

        ///  <summary>
        ///  Extra space by which the axis title should be moved out
        ///  </summary>
        public double AxisTitleOffset { get; set; }

        public ScaleType ScaleType { get; set; }

        public Axis( string title, AxisMode mode, double extraSpace, ScaleType scaleType ) 
        { 
            Title = title; 
            Mode = mode; 
            ExtraSpace = extraSpace; 
            AxisTitleOffset = 0; 
            ScaleType = scaleType; 
        } 
    } 
}
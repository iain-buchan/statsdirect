using System;

using StatsDirect.Utilities; 

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class BoxWhiskerOptions
    [ Serializable ]
    public class BoxWhiskerOptions : GenericOptions 
    { 
        
        public enum BoxWhiskerMethod 
        { 
            MedianQuartilesRange = 1,
            MeanStandardDeviationRange = 2,
            MeanConfidenceIntervalRange = 3,
            SevenNumberSummary = 4,
            BowleySummary = 5,
        } 
        
        
        public BoxWhiskerMethod Method = BoxWhiskerMethod.MedianQuartilesRange; 
        public bool MarkMeanAndMedian; 
        public bool UseInnerFence; 
        public bool UseOuterFence; 
        //  Public MinX As Double
        //  Public MaxX As Double
        
        public bool IsAscii; 
        
        //  Display options
        public string AxisFontDescriptor; 
        public double Cco; 
        
        public BoxWhiskerOptions( bool UseColour ) : base( UseColour ) 
        { 
            
        } 
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.BoxWhisker; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Method SetDefaultXAxisTitle
        public void SetDefaultXAxisTitle() 
        { 
            //  This used to try to be cleverer, but it turns out that formatting for each combination is almost essential to allow variation.
            switch ( Method ) 
            {
                case BoxWhiskerMethod.MedianQuartilesRange:
                    if ( UseInnerFence ) 
                    { 
                        XAxisTitle = UseOuterFence ? "min < LQ < median%MEAN% > UQ > max, fences (1.5 & 3.0 IQR)" : "min < LQ < median%MEAN% > UQ > max, fence (1.5 IQR)"; 
                    } 
                    else 
                    { 
                        XAxisTitle = UseOuterFence ? "min < LQ < median%MEAN% > UQ > max, fence (3.0 IQR)" : "min < LQ < median%MEAN% > UQ > max"; 
                    } 
                    break;
                case BoxWhiskerMethod.MeanStandardDeviationRange:
                    if ( UseInnerFence ) 
                    { 
                        XAxisTitle = UseOuterFence ? "min < 1 SD < mean%MEDIAN% > 1 SD > max, fences (1.96 SD, 2.58 SD)" : "min < 1 SD < mean%MEDIAN% > 1 SD > max, fence (1.96 SD)"; 
                    } 
                    else 
                    { 
                        XAxisTitle = UseOuterFence ? "min < 1 SD < mean%MEDIAN% > 1 SD > max, fence (2.58 SD)" : "min < 1 SD < mean%MEDIAN% > 1 SD > max"; 
                    } 
                    break;
                case BoxWhiskerMethod.MeanConfidenceIntervalRange:
                    string ci = Formatting.XRound( Cco * 100.0, 1 ) + "% confidence interval"; 
                    if ( UseInnerFence ) 
                    { 
                        if ( UseOuterFence ) 
                        { 
                            XAxisTitle = "min < mean%MEDIAN% ? " + ci + " > max, fences (1.96 SD, 2.58 SD)"; 
                        } 
                        else 
                        { 
                            XAxisTitle = "min < mean%MEDIAN% ? " + ci + " > max, fence (1.96 SD)"; 
                        } 
                    } 
                    else 
                    { 
                        if ( UseOuterFence ) 
                        { 
                            XAxisTitle = "min < mean%MEDIAN% ? " + ci + " > max, fence (2.58 SD)"; 
                        } 
                        else 
                        { 
                            XAxisTitle = "min < mean%MEDIAN% ? " + ci + " > max"; 
                        } 
                    } 
                    break;
                case BoxWhiskerMethod.SevenNumberSummary:
                    XAxisTitle = "min < [ 2nd < 9th < [ LQ < median%MEAN% > UQ | > 91st > 98th ] > max"; 
                    break;
                case BoxWhiskerMethod.BowleySummary:
                    XAxisTitle = "[ min < 10th < | LQ < median%MEAN% > UQ | > 90th > max ]"; 
                    break;
                default:
                    throw new ArgumentOutOfRangeException( "options.Method", Method.ToString() ); 
            }
            
            if ( MarkMeanAndMedian ) 
            { 
                XAxisTitle = XAxisTitle.Replace( "%MEAN%", " & mean(x)" ); 
                XAxisTitle = XAxisTitle.Replace( "%MEDIAN%", " & median(x)" ); 
            } 
            else 
            { 
                XAxisTitle = XAxisTitle.Replace( "%MEAN%", "" ); 
                XAxisTitle = XAxisTitle.Replace( "%MEDIAN%", "" ); 
            } 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property ShowBoxWhiskerOptions
        public override bool ShowBoxWhiskerOptions 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesAxisLabelFontDescriptor
        public override bool UsesAxisLabelFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesAxisTitleFontDescriptor
        public override bool UsesAxisTitleFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesBoxAxes
        public override bool UsesBoxAxes 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesChartTitle
        public override bool UsesChartTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesColour
        public override bool UsesColour 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesOrientation
        public override bool UsesOrientation 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesSeriesLabels
        public override bool UsesSeriesLabels 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesShowLegend
        public override bool UsesShowLegend 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesTitleFontDescriptor
        public override bool UsesTitleFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesXAxisTitle
        public override bool UsesXAxisTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property ShowLegendIsRelevant
        public override bool ShowLegendIsRelevant 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property IsNaturalOrientation
        public override bool IsNaturalOrientation 
        { 
            get 
            { 
                return Orientation == ChartOrientation.Horizontal; 
            } 
        } 
    } 
    
    
} 

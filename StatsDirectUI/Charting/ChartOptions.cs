using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public abstract class ChartOptions  
    { 
        
        public enum OptionTypes 
        { 
            Agreement,
            ///  <summary>
            ///  Bar, stacked bar and 100% stacked bar
            ///  </summary>
            Bar,
            BoxWhisker,
            Control,
            ///  <summary>
            ///  Markers with error bars
            ///  </summary>
            ///  <remarks>Should really be Error, but that's a reserved word in VB.Net</remarks>
            ErrorBars,
            Forest,
            Gini,
            Histogram,
            Ladder,
            LinearRegression,
            Normal,
            Pyramid,
            ROC,
            ScatterXY,
            Spread,
            Survival,
        } 
        
        
        public string Title; 
        public string XAxisTitle; 
        public string YAxisTitle; 
        public float AxisLineThickness; 
        public bool ShowLegend; 
        private bool useColour; 
        private IList<MarkerType> markerTypes; 
        
        ///  <summary>
        ///  Given a series index (13 for the 14th series, for example) return the marker that should be used for that series.
        ///  </summary>
        ///  <param name="SeriesNumber"></param>
        ///  <returns></returns>
        ///  <remarks>This used to be considerably more complex; Peter has simplified.</remarks>
        public static int SeriesNumberToMarkerNumber( int SeriesNumber ) 
        { 
            return SeriesNumber % 10; 
            
            // If SeriesNumber = 0 Then
            //     Return 10
            // ElseIf SeriesNumber > 10 Then
            //     Return SeriesNumber - 10 * (SeriesNumber \ 10) ' TODO: PJC: Why does this work?
            // Else
            //     Return SeriesNumber
            // End If
        } 
        
        
        ///  <summary>
        ///  Marker details.
        ///  </summary>
        ///  <remarks>Explicitly allowed: one marker type may be referenced multiple times in the list.  This is used, for example, by the Ladder plot, which uses marker type 1 for slots 1 and 2 so that it can show a UI for the markers in slot 1, and the lines in slot 2.</remarks>
        public IList <MarkerType>MarkerTypes 
        { 
            get 
            { 
                return markerTypes; 
            } 
            set 
            { 
                markerTypes = value; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UseColour
        public bool UseColour 
        { 
            get 
            { 
                return useColour; 
            } 
            set 
            { 
                useColour = value; 
            } 
        }

        protected ChartOptions( bool useColour ) 
        { 
            this.useColour = useColour; 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesAxisLineThickness
        public virtual bool UsesAxisLineThickness 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesColour
        public virtual bool UsesColour 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesShowLegend
        public virtual bool UsesShowLegend 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesXAxisTitle
        public virtual bool UsesXAxisTitle 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual bool UsesYAxisTitle 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public abstract OptionTypes OptionType { get; }
        
        public abstract bool ShowLegendIsRelevant { get; }

        public virtual ChartOptions Clone()
        {
            ChartOptions theClone = (ChartOptions)MemberwiseClone();
            if (null != markerTypes)
                theClone.MarkerTypes = new List<MarkerType>(MarkerTypes);
            return theClone;
        }
    } 
} 

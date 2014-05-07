using System;
using System.Collections.Generic;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public abstract class GenericOptions : ChartOptions 
    { 
        public bool ShouldAutoscale; 
        public bool ShouldBoxAxes; 
        public string[] SeriesTitles; 
        public string TitleFontDescriptor; 
        public string AxisTitleFontDescriptor; 
        public string AxisLabelFontDescriptor; 
        public string LegendFontDescriptor; 
        public IList<SeriesOptionsDescriptor> SeriesOptions; 
        public ChartOrientation Orientation; 
        public bool ShouldForceIsFilled; 
        public bool ForcedIsFilled; 
        ///  <summary>
        ///  If true, the fill style in ForcedFillStyle should be used for all fill styles, overriding the markers' own styles.
        ///  </summary>
        public bool ShouldForceFillStyle; 
        public FillStyle ForcedFillStyle;

        protected GenericOptions( bool useColour ) : base( useColour ) 
        { 
            SeriesOptions = new List<SeriesOptionsDescriptor>(); 
            if ( UsesAxisLabelFontDescriptor ) 
            { 
                AxisLabelFontDescriptor = ChartRenderer.DefaultAxisLabelFont; 
            } 
            if ( UsesAxisTitleFontDescriptor ) 
            { 
                AxisTitleFontDescriptor = ChartRenderer.DefaultAxisTitleFont; 
            } 
            if ( UsesLegendFontDescriptor ) 
            { 
                LegendFontDescriptor = ChartRenderer.DefaultLegendFont; 
            } 
            if ( UsesTitleFontDescriptor ) 
            { 
                TitleFontDescriptor = ChartRenderer.DefaultTitleFont; 
            } 
        } 
        
        public virtual bool UsesChartTitle 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual bool UsesAutoscale 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual bool UsesBoxAxes 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual bool UsesSeriesLabels 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual bool UsesAxisLabelFontDescriptor 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual string AxisLabelFontLabel 
        { 
            get 
            { 
                return "Axis Label"; 
            } 
        } 
        
        public virtual bool UsesAxisTitleFontDescriptor 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual bool UsesLegendFontDescriptor 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public virtual string LegendFontLabel 
        { 
            get 
            { 
                return "Legend"; 
            } 
        } 
        
        public virtual string OrientationLabel 
        { 
            get 
            { 
                return "Orientation"; 
            } 
        } 
        
        ///  <summary>
        ///  True if the chart can only be drawn in one orientation or if the orientation matches its preferred orientation.
        ///  False if the chart will be drawn in a non-preferred orientation.
        ///  </summary>
        ///  <value></value>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public virtual bool IsNaturalOrientation 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public virtual bool UsesTitleFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public virtual bool UsesOrientation 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the bar chart be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowBarOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the box+whisker chart be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowBoxWhiskerOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the control chart be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowControlOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the scatter plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowErrorBarOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the forest plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowForestOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the histogram plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowHistogramOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the normal plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowNormalOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the pyramid plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowPyramidOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the ROC plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowRocOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the scatter plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowScatterXYOptions 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        ///  <summary>
        ///  Should the extra items for the scatter plot be shown?
        ///  </summary>
        ///  <returns>True if the extra options should be shown, False if not.</returns>
        public virtual bool ShowSurvivalOptions 
        { 
            get 
            { 
                return false; 
            } 
        }

        public override ChartOptions Clone()
        {
            GenericOptions theClone = (GenericOptions)base.Clone();
            if (null != SeriesOptions)
                theClone.SeriesOptions = new List<SeriesOptionsDescriptor>(SeriesOptions);
            return theClone;
        }
        
    } 
    
    public enum ChartOrientation 
    { 
        Horizontal,
        Vertical,
    } 
} 

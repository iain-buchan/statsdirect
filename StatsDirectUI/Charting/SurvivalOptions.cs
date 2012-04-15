using System;
using System.Collections.Generic; 

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class SurvivalOptions : GenericOptions 
    { 
        [Serializable]
        public class SurvivalSeries  
        { 
            public double[] XDat; 
            public double[] YDat; 
            public double[] YDatL; 
            public double[] YDatU; 
            public int[] CDat; 
            //  Titles are carried in SeriesTitles
        } 
        
        
        public IList<SurvivalSeries> Series; 
        public bool ShowCensorshipTics; 
        public bool ShowEventMarkers; 
        public bool UseSeriesColourForConfidenceIntervals; 
        
        public SurvivalOptions( bool UseColour ) : base( UseColour ) 
        { 
            
            Series = new List<SurvivalSeries>(); 
        } 
        
        ///  <summary>
        ///  Set up markers for the series that are known
        ///  </summary>
        ///  <remarks>Precondition: All series have been set</remarks>
        public void SetMarkers() 
        { 
            MarkerTypes = new List<MarkerType>(); 
            
            //  One marker type per series
            for ( int seriesIndex=0; seriesIndex <= SeriesTitles.Length - 1; seriesIndex++ ) 
            { 
                int mkr = SeriesNumberToMarkerNumber( seriesIndex ); 
                MarkerType markerType = ChartRenderer.MarkerTypes[ mkr ].Clone(); 
                MarkerTypes.Add( markerType );

                SeriesOptionsDescriptor sod = new SeriesOptionsDescriptor
                                                  {
                                                      AllowChangeToDashStyle = true,
                                                      AllowChangeToLineThickness = true,
                                                      AllowChangeToMarkerColour = true,
                                                      AllowChangeToMarkerSize = true,
                                                      AllowChangeToMarkerType = true,
                                                      MarkerIndex = seriesIndex,
                                                      SeriesName = SeriesTitles[seriesIndex]
                                                  };
                SeriesOptions.Add( sod ); 
            } 
            
            //  Now add one more for the CIs
            MarkerType ciMarkerType = ChartRenderer.MarkerTypes[ 10 ].Clone(); 
            MarkerTypes.Add( ciMarkerType );

            SeriesOptionsDescriptor cisod = new SeriesOptionsDescriptor
                                                {
                                                    AllowChangeToDashStyle = true,
                                                    AllowChangeToLineThickness = true,
                                                    AllowChangeToMarkerColour = true,
                                                    AllowChangeToMarkerSize = false,
                                                    AllowChangeToMarkerType = false,
                                                    MarkerIndex = SeriesTitles.Length,
                                                    SeriesName = "Confidence intervals"
                                                };
            SeriesOptions.Add( cisod ); 
        } 
        
        
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.Survival; 
            } 
        } 
        
        public override bool UsesChartTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesSeriesLabels 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesAxisLabelFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesAxisTitleFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesYAxisTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool ShowSurvivalOptions 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool ShowLegendIsRelevant 
        { 
            get 
            { 
                return Series.Count > 1; 
            } 
        } 
    } 
} 

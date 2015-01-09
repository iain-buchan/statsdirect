using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Drawing;

using StatsDirect.Data;

namespace StatsDirect.Charting
{
    [ Serializable ]
    public class PyramidOptions : GenericOptions 
    { 
        
        public DataFrame MaleFrame; 
        public DataFrame FemaleFrame; 
        public DataFrame LabelFrame; 
        public double ScaleMaximum; 
        
        public PyramidOptions( bool useColour ) : base( useColour ) 
        { 
        } 
        
        public void SetOptions() 
        { 
            DataFrame f = new DataFrame(); 
            double maxRow = 0; 
            if ( ( MaleFrame != null ) && MaleFrame.VariableCount > 0 ) 
            { 
                f.Variables.Add( MaleFrame.Variables[ 0 ] ); 
            } 
            if ( ( FemaleFrame != null ) && FemaleFrame.VariableCount > 0 ) 
            { 
                f.Variables.Add( FemaleFrame.Variables[ 0 ] ); 
            } 
            //  A pyramid plot has one marker for male and an optional second for female.
            MarkerTypes = new List<MarkerType>(); 
            for ( int seriesIndex=0; seriesIndex <= f.VariableCount - 1; seriesIndex++ ) 
            { 
                Variable v = f.Variables[ seriesIndex ];

                MarkerType marker = new MarkerType {MarkerColor = Color.Gray, LineColor = Color.Gray, IsMarkerFilled = false};


                if ( Regex.Match( v.Title, @"\b(male|males|men)\b", RegexOptions.IgnoreCase ).Success ) 
                { 
                    marker.MarkerColor = Color.Blue;
                    marker.LineColor = Color.Blue;
                } 
                else if ( Regex.Match( v.Title, @"\b(female|females|women)\b", RegexOptions.IgnoreCase ).Success ) 
                { 
                    marker.MarkerColor = Color.Magenta;
                    marker.LineColor = Color.Magenta;
                } 
                MarkerTypes.Add( marker );

                SeriesOptionsDescriptor sod = new SeriesOptionsDescriptor
                                                  {
                                                      SeriesName = v.Title,
                                                      AllowChangeToDashStyle = false,
                                                      AllowChangeToLineThickness = false,
                                                      AllowChangeToMarkerSize = false,
                                                      AllowChangeToMarkerType = false,
                                                      AllowChangeToFill = true,
                                                      MarkerIndex = seriesIndex
                                                  };
                SeriesOptions.Add( sod ); 
                maxRow = Math.Max( maxRow, v.AsDoubleVariable.Max ); 
            } 
            
            //  Work out a reasonable axis value
            ScaleMaximum = maxRow; 
            double qmin = 0; 
            int div; 
            double zmin; 
            double zint; 
            int JmpDiv; 
            AxisScaler.Q_Axis( ref qmin, 0, ref maxRow, out div, out zmin, out zint, out JmpDiv, Templates.ScaleType.Linear ); 
            ScaleMaximum = zmin + div * zint; 
        } 
        
        public override ChartOptionType OptionType 
        { 
            get 
            { 
                return ChartOptionType.Pyramid; 
            } 
        } 
        
        public override bool UsesChartTitle 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool UsesShowLegend 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public override bool UsesAxisLineThickness 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        public override bool UsesAxisLabelFontDescriptor 
        { 
            get 
            { 
                return true; 
            } 
        } 
        
        public override bool ShowPyramidOptions 
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
                return false; 
            } 
        } 
    } 
} 

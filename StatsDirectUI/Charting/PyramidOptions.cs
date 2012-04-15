using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Drawing;

using StatsDirect.Data;

namespace StatsDirect.Charting
{
    // TRANSMISSINGCOMMENT: Class PyramidOptions
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
        
        // TRANSMISSINGCOMMENT: Method SetOptions
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

                MarkerType marker = new MarkerType {Color = Color.Gray, IsFilled = false};


                if ( Regex.Match( v.Title, @"\b(male|males|men)\b", RegexOptions.IgnoreCase ).Success ) 
                { 
                    marker.Color = Color.Blue; 
                } 
                else if ( Regex.Match( v.Title, @"\b(female|females|women)\b", RegexOptions.IgnoreCase ).Success ) 
                { 
                    marker.Color = Color.Magenta; 
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
            double zmin = 0; 
            double zint = 0; 
            int JmpDiv; 
            AxisScaler.Q_Axis( ref qmin, ref maxRow, out div, ref zmin, ref zint, out JmpDiv, Templates.ScaleType.Linear ); 
            ScaleMaximum = zmin + div * zint; 
        } 
        
        
        // TRANSMISSINGCOMMENT: Property OptionType
        public override OptionTypes OptionType 
        { 
            get 
            { 
                return OptionTypes.Pyramid; 
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
        
        // TRANSMISSINGCOMMENT: Property UsesShowLegend
        public override bool UsesShowLegend 
        { 
            get 
            { 
                return false; 
            } 
        } 
        
        // TRANSMISSINGCOMMENT: Property UsesAxisLineThickness
        public override bool UsesAxisLineThickness 
        { 
            get 
            { 
                return false; 
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
        
        // TRANSMISSINGCOMMENT: Property ShowPyramidOptions
        public override bool ShowPyramidOptions 
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
    } 
    
    
} 

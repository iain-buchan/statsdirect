using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    /// <summary>
    /// An abstract interface for charts to call back into their hosts to obtain information.
    /// </summary>
    public interface IChartHost
    {
        /// <summary>
        /// Presents the specified options to the user in some appropriate way; modifies the options in-place with the user's selections.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns>true if the options are to be used, false if the user cancelled the option selection.</returns>
        bool DisplayOptions(OptionDescriptor descriptor);

        string RoundU(double amount);

        /// <summary>
        /// Format a probability, using the default number of decimal places
        /// </summary>
        string pval(double p);

        string pval_half(double p);

        string zvalp1(double xz);
        string zvalp2(double xz);

        bool GetBoolean(string prompt, string Title, bool initialValue, out bool cancelled);
        bool GetBoolean(string prompt, string Title, bool InitialValue, int HelpIndex, out bool cancelled);
        double GetConfidenceInterval(out bool cancelled);
        double GetDouble(string prompt, string Title, double InitialValue, out bool cancelled);
        int GetInteger(string prompt, string Title, int initialValue, out bool cancelled);

        /// <summary>
        /// Prompt the user for a string; return the user-entered string, or Nothing if the user cancels.
        /// </summary>
        /// <returns>The user-entered string, or Nothing if the user cancels</returns>
        /// <remarks></remarks>
        string GetString(string prompt, string title, string initialValue);

        bool MetaPlotCI { get; }

        int MetaPlotMethod { get; }

        bool CheckScale(ScaleParameters scaleParameters);
    }

    public enum ScaleType
    {
        NotSet = -1,
        Linear = 0,
        LogNatural = 1,
        Log10 = 2,
        Date = 3,
        Category = 4
    }

    public enum LabelDirection
    {
        Across = 0,
        Up = 1,
        Down = 2,
        SlopeUp = 3,
        SlopeDown = 4
    }

    [Serializable]
    public class AxisScaleParameters
    {
        public bool ShouldCheck { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public ICollection<ScaleType> AllowedScaleTypes { get; set; }
        public ScaleType ScaleType { get; set; }

        // Scale
        public bool HasAxisScale { get; set; }
        public double QMin { get; set; }
        public double QMax { get; set; }
        public int Div { get; set; }
        public double ZMin { get; set; }
        public double ZInt { get; set; }
        public int MinorTicsPerMajorTic { get; set; }
        public string Mask { get; set; }
        public LabelDirection LabelDirection { get; set; }

        // Grid lines
        public bool HasGridLines { get; set; }
        public System.Drawing.Drawing2D.DashStyle GridLineDashStyle { get; set; }

        // Marker line
        public bool HasMarkerLine { get; set; }
        public double MarkerLineValue { get; set; }

        public AxisScaleParameters Clone()
        {
            return (AxisScaleParameters)MemberwiseClone();
        }
    }

    [Serializable]
    public class ScaleParameters
    {
        public AxisScaleParameters X;
        public AxisScaleParameters Y;

        public ScaleParameters()
        {
            X = new AxisScaleParameters();
            Y = new AxisScaleParameters();
        }

        public ScaleParameters Clone()
        {
            return new ScaleParameters {X = X.Clone(), Y = Y.Clone()};
        }
    }
}

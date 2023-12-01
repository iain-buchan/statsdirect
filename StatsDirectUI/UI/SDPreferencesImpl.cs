#nullable enable

using StatsDirect.Charting;
using StatsDirect.Templates;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    internal class SDPreferencesImpl : ISdPreferences, IUiPreferences
    {
        // Useful near-constants; these are unlikely to change.
        string ISdPreferences.DECP_CHAR => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        string ISdPreferences.Numeric_Thousands_Separator => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
        int ISdPreferences.MaxRows => 64000;

        // User preferences
        bool ISdPreferences.CanDefaultConfidenceInterval { get; set; } = true;
        bool ISdPreferences.DelayContinuityCorrection { get; set; } = false;
        double ISdPreferences.DefaultConfidenceInterval { get; set; } = 0.95;
        int ISdPreferences.DisplayDecimalPlaces { get; set; } = 6;
        double ISdPreferences.MetaCC { get; set; } = -9;
        bool ISdPreferences.MetaExact { get; set; } = true;
        bool ISdPreferences.MetaPlotCI { get; set; } = true;
        int ISdPreferences.MetaPlotMethod { get; set; } = 1;
        int ISdPreferences.PDecimalPlaces { get; set; } = 4;
        bool ISdPreferences.UseScientificNotationForSmallPValues { get; set; } = false;

        public string Markers { get; set; } = "";
        public FontDescriptor? TitleFont { get; set; }
        public FontDescriptor? LabelFont { get; set; }
        public bool BoxAxes { get; set; } = false;
        public bool BlackAndWhite { get; set; } = false;
        public bool RequestScaleLimits { get; set; } = false;

        // Windows user interface
        WindowDimensions IUiPreferences.CalculatorDimensions { get; set; } = new();
        FontDescriptor IUiPreferences.DefaultWorkbookFont { get; set; } = new FontDescriptor("Calibri", 0, 11);
        WindowDimensions IUiPreferences.MainDimensions { get; set; } = new();
        IList<string> IUiPreferences.RecentFileList { get; set; } = new List<string>();
        bool IUiPreferences.SelectGroupsByIdentifier { get; set; } = false;
        IList<ToolDescriptor> IUiPreferences.Tools { get; set; } = new List<ToolDescriptor>(new ToolDescriptor[]
        {
            new("Calculator", "%STATSDIRECT%\\StatsDirect.exe -calculator"),
            new("Notepad", "notepad.exe"),
            new("Wordpad", "wordpad.exe")
        });
    }

}

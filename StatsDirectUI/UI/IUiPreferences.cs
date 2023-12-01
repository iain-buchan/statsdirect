using System.Collections.Generic;
using StatsDirect.Charting;
using StatsDirect.Configuration;

namespace StatsDirect.UI
{
    public interface IUiPreferences: IPersistentPreferences
    {
        WindowDimensions CalculatorDimensions { get; set; }
        FontDescriptor DefaultWorkbookFont { get; set; }
        WindowDimensions MainDimensions { get; set; }
        IList<string> RecentFileList { get; set; }
        /// <summary>
        /// If false, group selectors are by variable.
        /// If true, group selectors are by indicator.
        /// </summary>
        bool SelectGroupsByIdentifier { get; set; }
        IList<ToolDescriptor> Tools { get; set; }
    }

    public record WindowDimensions
    {
        public int Top;
        public int Left;
        public int Width;
        public int Height;
        public bool IsMaximized;
    }

    public readonly record struct ToolDescriptor
    (
        string Label,
        string Program
    );
}

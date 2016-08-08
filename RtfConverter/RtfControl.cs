using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    public class RtfControl : Chunk
    {
        public override AccumulatedFormat AccumulatedFormat { get; set; }
        public bool IsIrrelevant { get; set; }
        public bool IsNewline { get; set; }
        /// <summary>
        /// For newlines and separators (only), true if this is the starting one, false if it's the ending one.  Undefined for other types.
        /// </summary>
        public bool IsStart { get; set; }
        public bool IsTableCellSeparator { get; set; }
        public string Keyword { get; private set; }
        public bool SuppressFollowingText { get; set; }
        public int? Value { get; private set; }

        public RtfControl(string keyword, string value)
        {
            Keyword = keyword;
            if (null != value)
                Value = int.Parse(value);
        }

        public override string ToString()
        {
            return "RTF: " + Keyword + (Value.HasValue ? " " + Value.Value.ToString() : string.Empty);
        }

        public override void Accept(IChunkVisitor visitor)
        {
            visitor.Visit(this);
        }

        public RtfControl Clone()
        {
            RtfControl clone = (RtfControl)MemberwiseClone();
            clone.AccumulatedFormat = AccumulatedFormat.Clone();
            return clone;
        }
    }
}

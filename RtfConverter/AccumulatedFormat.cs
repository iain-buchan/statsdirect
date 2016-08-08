using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    public class AccumulatedFormat
    {
        public AccumulatedFormat()
        {
            // There is no colour index 0 in RTF, so default it to 1
            ColourIndex = 1;
        }

        public int ColourIndex { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsPartOfTable { get; set; }
        public bool IsSubscript { get; set; }
        public bool IsSuperscript { get; set; }
        public bool IsUnderlined { get; set; }

        public AccumulatedFormat Clone()
        {
            return new AccumulatedFormat
            {
                ColourIndex = ColourIndex,
                IsBold = IsBold,
                IsItalic = IsItalic,
                IsPartOfTable = IsPartOfTable,
                IsSubscript = IsSubscript,
                IsSuperscript = IsSuperscript,
                IsUnderlined = IsUnderlined
            };
        }
    }
}

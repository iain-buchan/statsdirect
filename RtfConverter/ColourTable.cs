using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    class ColourTable
    {
        public Dictionary<int, string> ColourMappings { get; private set; }

        public ColourTable()
        {
            ColourMappings = new Dictionary<int, string>();
        }
    }
}

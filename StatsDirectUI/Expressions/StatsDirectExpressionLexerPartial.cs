using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Expressions
{
    public partial class StatsDirectExpressionLexer
    {
        public enum SeparatorStructure
        {
            CommaDot,
            DotComma,
            SpaceDot
        }

        public SeparatorStructure Separators { get; set; }
    }
}

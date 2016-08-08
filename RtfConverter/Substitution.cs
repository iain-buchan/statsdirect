using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    public class Substitution : Chunk
    {
        public override AccumulatedFormat AccumulatedFormat { get; set; }
        public string Variable { get; private set; }

        public Substitution(string variable)
        {
            Variable = variable;
        }

        public override void Accept(IChunkVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

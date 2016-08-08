using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    public class BlockFinish : Chunk
    {
        public override AccumulatedFormat AccumulatedFormat { get; set; }

        public override void Accept(IChunkVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

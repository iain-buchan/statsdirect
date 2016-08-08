using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    public class BlockStart : Chunk
    {
        public override AccumulatedFormat AccumulatedFormat { get; set; }

        public string Name { get; private set; }

        public BlockStart(string name)
        {
            Name = name;
        }

        public override void Accept(IChunkVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    public class Entity : Chunk
    {
        public override AccumulatedFormat AccumulatedFormat { get; set; }

        public string Hex { get; private set; }

        public Entity(string hex)
        {
            Hex = hex;
        }

        public override void Accept(IChunkVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

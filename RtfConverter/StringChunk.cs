using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    /// <remarks>This doesn't have to be efficient, just simple - so we hold a StringBuilder for each string even though it would be far more efficient not to.</remarks>
    public class StringChunk : Chunk
    {
        public override AccumulatedFormat AccumulatedFormat { get; set; }

        private StringBuilder builder;

        public StringChunk()
        {
            builder = new StringBuilder();
        }

        public void Append(string stuff)
        {
            builder.Append(stuff);
        }

        public override string ToString()
        {
            return builder.ToString();
        }

        public override void Accept(IChunkVisitor visitor)
        {
            visitor.Visit(this);
        }

        public bool IsIrrelevant
        {
            get { return "\r\n".Equals(builder.ToString());  }
        }
    }
}

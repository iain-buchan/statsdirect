using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    public interface IChunkVisitor
    {
        void Visit(BlockFinish victim);
        void Visit(BlockStart victim);
        void Visit(Chunks victim);
        void Visit(Entity victim);
        void Visit(RtfControl victim);
        void Visit(StringChunk victim);
        void Visit(Substitution victim);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    class MarkerSplitter : IChunkVisitor
    {
        public Chunks ProcessedChunks { get; private set; }

        public MarkerSplitter()
        {
            ProcessedChunks = new Chunks();
        }

        public void Process(Chunks victim)
        {
            // Add a line start to the first line
            RtfControl startLine = new RtfControl("par", null) { IsNewline = true, IsStart = true, AccumulatedFormat = new AccumulatedFormat() };
            ProcessedChunks.Add(startLine);

            Visit(victim);

            // Add a line end to the last line
            RtfControl endLine = new RtfControl("par", null) { IsNewline = true, AccumulatedFormat = new AccumulatedFormat() };
            ProcessedChunks.Add(endLine);
        }

        public void Visit(Chunks victim)
        {
            foreach (Chunk chunk in victim)
                chunk.Accept(this);
        }

        public void Visit(RtfControl victim)
        {
            // We always want this one.
            ProcessedChunks.Add(victim);
            if (victim.IsNewline || victim.IsTableCellSeparator)
            {
                // If that was a newline, clone it into the end of the previous line (already pushed) and the start of the new line (cloned)
                // If that was a separator, clone it into the end of the previous cell (already pushed) and the start of the new cell (cloned)
                RtfControl clone = victim.Clone();
                clone.IsStart = true;
                ProcessedChunks.Add(clone);
            }
        }

        public void Visit(Substitution victim)
        {
            ProcessedChunks.Add(victim);
        }

        public void Visit(StringChunk victim)
        {
            ProcessedChunks.Add(victim);
        }

        public void Visit(Entity victim)
        {
            ProcessedChunks.Add(victim);
        }

        public void Visit(BlockStart victim)
        {
            ProcessedChunks.Add(victim);
        }

        public void Visit(BlockFinish victim)
        {
            ProcessedChunks.Add(victim);
        }
    }
}

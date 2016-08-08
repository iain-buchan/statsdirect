namespace RtfConverter
{
    public abstract class Chunk
    {
        public abstract AccumulatedFormat AccumulatedFormat { get; set; }
        public abstract void Accept(IChunkVisitor visitor);
    }
}

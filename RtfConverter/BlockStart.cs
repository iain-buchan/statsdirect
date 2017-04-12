namespace RtfConverter
{
    public class BlockStart : Chunk
    {
        public override AccumulatedFormat AccumulatedFormat { get; set; }

        public string Name { get; }

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

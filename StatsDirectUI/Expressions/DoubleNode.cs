namespace StatsDirect.Expressions
{
    public class DoubleNode : INode
    {
        public double Value { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

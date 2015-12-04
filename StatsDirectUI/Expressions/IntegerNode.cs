namespace StatsDirect.Expressions
{
    public class IntegerNode : INode
    {
        public int Value { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

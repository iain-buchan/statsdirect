namespace StatsDirect.Expressions
{
    public class StringNode : INode
    {
        public string Value { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

namespace StatsDirect.Expressions
{
    public class MonadicNode : INode
    {
        public INode Node { get; set; }
        public MonadicOperator Operator { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

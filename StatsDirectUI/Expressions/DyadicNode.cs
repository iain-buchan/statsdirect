namespace StatsDirect.Expressions
{
    /// <summary>
    /// A representation of a part of an expression with two operands
    /// </summary>
    public class DyadicNode : INode
    {
        public INode Left { get; set; }
        public INode Right { get; set; }
        public DyadicOperator Operator { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

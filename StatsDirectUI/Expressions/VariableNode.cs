namespace StatsDirect.Expressions
{
    public class VariableNode : INode
    {
        public int Index { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

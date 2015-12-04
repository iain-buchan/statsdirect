namespace StatsDirect.Expressions
{
    public class FunctionNode : INode
    {
        public string Name { get; set; }
        public Arguments Arguments { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

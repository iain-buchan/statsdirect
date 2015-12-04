namespace StatsDirect.Expressions
{
    public class ConstantNode : INode
    {
        public ParserConstant Constant { get; set; }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

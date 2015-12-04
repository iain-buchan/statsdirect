namespace StatsDirect.Expressions
{
    public class VariableNode : INode
    {
        public int Index { get; set; }

        DataType INode.DataType(DataType[] passedVariableTypes)
        {
            return passedVariableTypes[Index];
        }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

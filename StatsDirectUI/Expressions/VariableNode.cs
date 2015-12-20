namespace StatsDirect.Expressions
{
    public class VariableNode : INode
    {
        public int Index { get; set; }

        public DataType DataType(DataType[] passedVariableTypes)
        {
            return passedVariableTypes[Index];
        }

        public void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

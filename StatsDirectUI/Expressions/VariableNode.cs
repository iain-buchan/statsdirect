namespace StatsDirect.Expressions
{
    public class VariableNode : INode
    {
        public int Index { get; set; }

        public DataType DataType(DataType[] passedVariableTypes)
        {
            // Index is 1-based, array indices in C# (hence passedVariableTypes) start from 0.
            return passedVariableTypes[Index - 1];
        }

        public void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

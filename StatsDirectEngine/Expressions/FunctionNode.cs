namespace StatsDirect.Expressions
{
    public class FunctionNode : INode
    {
        public string Name { get; set; }
        public Arguments Arguments { get; set; }

        public DataType DataType(DataType[] passedVariableTypes)
        {
            return FunctionRegistry.SoleInstance.FunctionNamed(Name).DataType;
        }

        public void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

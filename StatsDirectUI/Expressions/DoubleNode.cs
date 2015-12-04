using System;

namespace StatsDirect.Expressions
{
    public class DoubleNode : INode
    {
        public double Value { get; set; }

        DataType INode.DataType(DataType[] passedVariableTypes)
        {
            return DataType.Double;
        }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

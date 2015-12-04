using System;

namespace StatsDirect.Expressions
{
    public class IntegerNode : INode
    {
        public int Value { get; set; }

        DataType INode.DataType(DataType[] passedVariableTypes)
        {
            return DataType.Integer;
        }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

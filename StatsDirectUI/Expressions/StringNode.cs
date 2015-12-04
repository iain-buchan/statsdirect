using System;

namespace StatsDirect.Expressions
{
    public class StringNode : INode
    {
        public string Value { get; set; }

        DataType INode.DataType(DataType[] passedVariableTypes)
        {
                return DataType.String;
        }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

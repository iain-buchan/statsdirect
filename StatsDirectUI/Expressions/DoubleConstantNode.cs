using System;

namespace StatsDirect.Expressions
{
    public class DoubleConstantNode : INode
    {
        public ParserConstant Constant { get; set; }

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

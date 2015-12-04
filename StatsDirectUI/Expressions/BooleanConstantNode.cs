using System;

namespace StatsDirect.Expressions
{
    public class BooleanConstantNode : INode
    {
        public ParserConstant Constant { get; set; }

        DataType INode.DataType(DataType[] passedVariableTypes)
        {
            return DataType.Boolean;
        }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

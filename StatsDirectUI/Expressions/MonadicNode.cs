using System;

namespace StatsDirect.Expressions
{
    public class MonadicNode : INode
    {
        public INode Node { get; set; }
        public MonadicOperator Operator { get; set; }

        DataType INode.DataType(DataType[] passedVariableTypes)
        {
            DataType inputType = Node.DataType(passedVariableTypes);
            MonadicOperatorDefinition definition = MonadicOperatorRegistry.SoleInstance.DefinitionFor(Operator);
            foreach (InOutDataTypeDefinition candidate in definition.InOutDataTypeDefinitions)
                if (candidate.InputTypes[0] == inputType)
                    return candidate.ReturnType;
            // If we get here, our input type is illegal
            throw new Exception(Operator.ToString() + " doesn't expect a parameter of type " + inputType.ToString());
        }

        void INode.Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}

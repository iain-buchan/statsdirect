namespace StatsDirect.Expressions
{
    public interface IExpressionVisitor
    {
        void Visit(ConstantNode node);
        void Visit(DoubleNode node);
        void Visit(DyadicNode node);
        void Visit(FunctionNode node);
        void Visit(IntegerNode node);
        void Visit(MonadicNode node);
        void Visit(StringNode node);
        void Visit(VariableNode node);
    }
}

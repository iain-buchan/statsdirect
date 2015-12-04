using System;
using System.Collections.Generic;
using System.Text;

namespace StatsDirect.Expressions
{
    public class CSharpRenderer : IExpressionVisitor
    {
        private StringBuilder activeBuilder;
        private Dictionary<DyadicOperator, string> infixDyadicOperations;
        private Dictionary<DyadicOperator, string> prefixDyadicOperations;
        private Dictionary<ParserConstant, string> parserConstants;

        public CSharpRenderer()
        {
            infixDyadicOperations = new Dictionary<DyadicOperator, string>
            {
                { DyadicOperator.Add, " + " },
                { DyadicOperator.And, " && " },
                { DyadicOperator.Divide, " / (double)" }, // Forces floating-point divide even when both strings parse to integers; otherwise 12 / 5 gives 2, not 2.4.
                { DyadicOperator.Modulo, " % " },
                { DyadicOperator.Multiply, " * " },
                { DyadicOperator.Or, " || " },
                { DyadicOperator.Subtract, " - " },
                { DyadicOperator.Equal, " == " },
                { DyadicOperator.GreaterThan, " > " },
                { DyadicOperator.GreaterThanOrEqual, " >= " },
                { DyadicOperator.LessThan, " < " },
                { DyadicOperator.LessThanOrEqual, " <= " },
                { DyadicOperator.NotEqual, " != " }
            };
            prefixDyadicOperations = new Dictionary<DyadicOperator, string>
            {
                { DyadicOperator.IntegerDivide, "SDMath.IDiv" },
                { DyadicOperator.Pow, "Math.Pow" }
            };
            parserConstants = new Dictionary<ParserConstant, string>
            {
                { ParserConstant.E, "Math.E" },
                { ParserConstant.False, "false" },
                { ParserConstant.Pi, "Math.PI" },
                { ParserConstant.True, "true" }
            };
        }

        public string Render(INode node)
        {
            activeBuilder = new StringBuilder();
            node.Accept(this);
            return activeBuilder.ToString();
        }

        private string RenderInNewContext(INode node)
        {
            StringBuilder savedSb = activeBuilder;
            activeBuilder = new StringBuilder();
            string rendered = Render(node);
            activeBuilder = savedSb;
            return rendered;
        }

        void IExpressionVisitor.Visit(BooleanConstantNode node)
        {
            string cSharpValue;
            if (parserConstants.TryGetValue(node.Constant, out cSharpValue))
            {
                activeBuilder.Append(cSharpValue);
                return;
            }
            throw new Exception("Unknown constant");
        }

        void IExpressionVisitor.Visit(DoubleNode node)
        {
            activeBuilder.Append(node.Value);
        }

        void IExpressionVisitor.Visit(DoubleConstantNode node)
        {
            string cSharpValue;
            if (parserConstants.TryGetValue(node.Constant, out cSharpValue))
            {
                activeBuilder.Append(cSharpValue);
                return;
            }
            throw new Exception("Unknown constant");
        }

        void IExpressionVisitor.Visit(DyadicNode node)
        {
            string cSharpOp;
            if (infixDyadicOperations.TryGetValue(node.Operator, out cSharpOp))
            {
                // Left op Right
                activeBuilder.Append('(');
                node.Left.Accept(this);
                activeBuilder.Append(')');
                activeBuilder.Append(cSharpOp);
                activeBuilder.Append('(');
                node.Right.Accept(this);
                activeBuilder.Append(')');
                return;
            }
            if (prefixDyadicOperations.TryGetValue(node.Operator, out cSharpOp))
            {
                // op(Left, Right)
                activeBuilder.Append(cSharpOp);
                activeBuilder.Append('(');
                node.Left.Accept(this);
                activeBuilder.Append(", ");
                node.Right.Accept(this);
                activeBuilder.Append(')');
                return;
            }
            throw new Exception("Unknown dyadic operation");
        }

        void IExpressionVisitor.Visit(FunctionNode node)
        {
            // Check for positional parameters after named ones.  We don't allow these, as we don't know what the position is.
            bool foundNamedParameter = false;
            foreach (Argument argument in node.Arguments)
            {
                if (null != argument)
                {
                    if (null == argument.ExplicitParameterName)
                    {
                        // Positional parameter
                        if (foundNamedParameter)
                            throw new Exception("Once you start using named parameters, all parameters afterwards must also be named.");
                    }
                    else
                    {
                        foundNamedParameter = true;
                    }
                }
            }

            FunctionDefinition functionDefinition = FunctionRegistry.SoleInstance.FunctionNamed(node.Name);
            if (null == functionDefinition)
                throw new Exception("No function named '" + node.Name + "' is known.");

            // Fill in parameter values using an array.  First set defaults, then overwrite with any positional parameters, then overwrite with any named parameters.
            List<ArgumentDefinition> defs = functionDefinition.ArgumentDefinitions;
            string[] parameterValues = new string[defs.Count];
            for (int i = 0; i < parameterValues.Length; i++)
                parameterValues[i] = defs[i].Default;
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                Argument argument = node.Arguments[i];
                if (null != argument)
                {
                    if (null == argument.ExplicitParameterName)
                    {
                        // Positional
                        if (i >= parameterValues.Length)
                            throw new Exception("Too many arguments to " + functionDefinition);
                        parameterValues[i] = RenderInNewContext(argument.Node);
                    }
                    else
                    {
                        // Named
                        bool found = false;
                        for (int pos = 0; pos < defs.Count; pos++)
                        {
                            if (argument.ExplicitParameterName.Equals(defs[pos].Name))
                            {
                                found = true;
                                parameterValues[pos] = RenderInNewContext(argument.Node);
                                break;
                            }
                        }
                        if (!found)
                            throw new Exception(functionDefinition + "has no named argument called '" + argument.ExplicitParameterName + "'");
                    }
                }
            }

            // By the time we get here, all parameters should have been filled in.  If there are any remaining nulls, they don't have a default or a value from the caller, so fail.
            for (int i = 0; i < parameterValues.Length; i++)
            {
                if (null == parameterValues[i])
                    throw new Exception("You must supply a value for " + defs[i].Name + " in " + functionDefinition);
            }

            activeBuilder.Append(functionDefinition.ClrName);
            activeBuilder.Append("(");
            activeBuilder.Append(string.Join(", ", parameterValues));
            activeBuilder.Append(")");
        }

        void IExpressionVisitor.Visit(IntegerNode node)
        {
            activeBuilder.Append(node.Value);
        }

        void IExpressionVisitor.Visit(MonadicNode node)
        {
            MonadicOperatorDefinition definition = MonadicOperatorRegistry.SoleInstance.DefinitionFor(node.Operator);
            activeBuilder.Append(definition.ClrName);
            activeBuilder.Append('(');
            node.Node.Accept(this);
            activeBuilder.Append(')');
        }

        void IExpressionVisitor.Visit(StringNode node)
        {
            activeBuilder.Append('"');
            // C# strings embed \ as \\ and " as \"; ensure this is respected or we'll get parse errors or (worse) compilation of arbitrary code.
            activeBuilder.Append(node.Value.Replace("\"", "\\\"").Replace("\\", "\\\\"));
            activeBuilder.Append('"');
        }

        void IExpressionVisitor.Visit(VariableNode node)
        {
            // Variables are passed in as a double array x[].
            activeBuilder.Append("x[");
            // Variables in the expression are 1-based; variables in C# are 0-based.
            activeBuilder.Append(node.Index - 1);
            activeBuilder.Append(']');
        }
    }
}

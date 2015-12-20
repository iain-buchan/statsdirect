using System;
using System.Collections.Generic;
using System.Text;

namespace StatsDirect.Expressions
{
    public class CSharpRenderer : IExpressionVisitor
    {
        private StringBuilder activeBuilder;
        private Dictionary<ParserConstant, string> parserConstants;
        private DataType[] passedVariableTypes;

        public CSharpRenderer()
        {
            parserConstants = new Dictionary<ParserConstant, string>
            {
                { ParserConstant.E, "Math.E" },
                { ParserConstant.False, "false" },
                { ParserConstant.Pi, "Math.PI" },
                { ParserConstant.True, "true" }
            };
        }

        public string Render(INode node, DataType[] passedVariableTypes)
        {
            this.passedVariableTypes = passedVariableTypes;
            return Render(node);
        }

        private string Render(INode node)
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

        public void Visit(BooleanConstantNode node)
        {
            string cSharpValue;
            if (parserConstants.TryGetValue(node.Constant, out cSharpValue))
            {
                activeBuilder.Append(cSharpValue);
                return;
            }
            throw new Exception("Unknown constant");
        }

        public void Visit(DoubleNode node)
        {
            activeBuilder.Append(node.Value);
            activeBuilder.Append("D");
        }

        public void Visit(DoubleConstantNode node)
        {
            string cSharpValue;
            if (parserConstants.TryGetValue(node.Constant, out cSharpValue))
            {
                activeBuilder.Append(cSharpValue);
                return;
            }
            throw new Exception("Unknown constant");
        }

        public void Visit(DyadicNode node)
        {
            DyadicOperatorDefinition definition = DyadicOperatorRegistry.SoleInstance.DefinitionFor(node.Operator);
            InOutDataTypeDefinition typesAfterPromotion = node.InOut(passedVariableTypes);
            if (definition.ClrIsPrefix)
            {
                // op(Left, Right)
                activeBuilder.Append(definition.ClrName);
                activeBuilder.Append('(');
                RenderWithPossibleTypePromotion(node.Left, typesAfterPromotion.InputTypes[0]);
                activeBuilder.Append(", ");
                RenderWithPossibleTypePromotion(node.Right, typesAfterPromotion.InputTypes[1]);
                activeBuilder.Append(')');
                return;
            }
            else
            {
                // Left op Right
                activeBuilder.Append('(');
                RenderWithPossibleTypePromotion(node.Left, typesAfterPromotion.InputTypes[0]);
                activeBuilder.Append(')');
                activeBuilder.Append(definition.ClrName);
                activeBuilder.Append('(');
                RenderWithPossibleTypePromotion(node.Right, typesAfterPromotion.InputTypes[1]);
                activeBuilder.Append(')');
                return;
            }
            throw new Exception("Unknown dyadic operation");
        }

        private void RenderWithPossibleTypePromotion(INode node, DataType typeAfterPromotion)
        {
            string prePromote;
            string postPromote;
            if (GetTypePromotionStrings(node.DataType(passedVariableTypes), typeAfterPromotion, out prePromote, out postPromote))
            {
                // Promotion required; wrap the inner node as necessary.
                activeBuilder.Append(prePromote);
                node.Accept(this);
                activeBuilder.Append(postPromote);
            }
            else
            {
                // No promotion required
                node.Accept(this);
            }
        }

        private bool GetTypePromotionStrings(DataType from, DataType to, out string prePromote, out string postPromote)
        {
            // Integers can be promoted to doubles
            if (from == DataType.Integer && to == DataType.Double)
            {
                prePromote = "(double)(";
                postPromote = ")";
                return true;
            }
            // Everything else is either default or incompatible (which should have been caught earlier); either way, we don't write in anything special.
            prePromote = string.Empty;
            postPromote = string.Empty;
            return false;
        }

        public void Visit(FunctionNode node)
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

        public void Visit(IntegerNode node)
        {
            activeBuilder.Append(node.Value);
        }

        public void Visit(MonadicNode node)
        {
            MonadicOperatorDefinition definition = MonadicOperatorRegistry.SoleInstance.DefinitionFor(node.Operator);
            activeBuilder.Append(definition.ClrName);
            activeBuilder.Append('(');
            node.Node.Accept(this);
            activeBuilder.Append(')');
        }

        public void Visit(StringNode node)
        {
            activeBuilder.Append('"');
            // C# strings embed \ as \\ and " as \"; ensure this is respected or we'll get parse errors or (worse) compilation of arbitrary code.
            activeBuilder.Append(node.Value.Replace("\"", "\\\"").Replace("\\", "\\\\"));
            activeBuilder.Append('"');
        }

        public void Visit(VariableNode node)
        {
            // Variables are passed in as a double array x[].
            activeBuilder.Append("x[");
            // Variables in the expression are 1-based; variables in C# are 0-based.
            activeBuilder.Append(node.Index - 1);
            activeBuilder.Append(']');
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StatsDirect.Expressions
{
    partial class StatsDirectExpressionParser
    {
        private string RenderVariable(string variableName)
        {
            // Variables are V, Vn, X, or Xn.  Anything else is an error.
            Regex r = new Regex("^[VvXx]([1-9][0-9]*)?$");
            if (!r.Match(variableName).Success)
                throw new Exception("'" + variableName + "' is not a valid variable reference.  Variable references must be of the form X, X1, X2, X27 etc. If you used " + variableName + " as a named parameter in a function, make sure you have specified '" + variableName + " := value', not '" + variableName + " = value'");

            return "x[" + (variableName.Length == 1 ? "0" : (int.Parse(variableName.Substring(1)) - 1).ToString()) + "]";
        }

        private string RenderFunction(string name, Arguments arguments)
        {
            // Check for positional parameters after named ones.  We don't allow these, as we don't know what the position is.
            bool foundNamedParameter = false;
            foreach (Argument argument in arguments)
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

            FunctionDefinition functionDefinition = FunctionRegistry.SoleInstance.FunctionNamed(name);
            if (null == functionDefinition)
                throw new Exception("No function named '" + name + "' is known.");

            // Fill in parameter values using an array.  First set defaults, then overwrite with any positional parameters, then overwrite with any named parameters.
            List<ArgumentDefinition> defs = functionDefinition.ArgumentDefinitions;
            string[] parameterValues = new string[defs.Count];
            for (int i = 0; i < parameterValues.Length; i++)
                parameterValues[i] = defs[i].ParameterDefault;
            for (int i = 0; i < arguments.Count; i++)
            {
                Argument argument = arguments[i];
                if (null != argument)
                {
                    if (null == argument.ExplicitParameterName)
                    {
                        // Positional
                        if (i >= parameterValues.Length)
                            throw new Exception("Too many arguments to " + functionDefinition);
                        parameterValues[i] = argument.BuiltExpression;
                    }
                    else
                    {
                        // Named
                        bool found = false;
                        for (int pos = 0; pos < defs.Count; pos++)
                        {
                            if (argument.ExplicitParameterName.Equals(defs[pos].ParameterName))
                            {
                                found = true;
                                parameterValues[pos] = argument.BuiltExpression;
                                break;
                            }
                        }
                        if (!found)
                            throw new Exception(functionDefinition + "has no named argument called '" +
                                                argument.ExplicitParameterName + "'");
                    }
                }
            }

            // By the time we get here, all parameters should have been filled in.  If there are any remaining nulls, they don't have a default or a value from the caller, so fail.
            for (int i = 0; i < parameterValues.Length; i++)
            {
                if (null == parameterValues[i])
                    throw new Exception("You must supply a value for " + defs[i].ParameterName + " in " + functionDefinition);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(functionDefinition.ClrName);
            sb.Append("(");
            sb.Append(string.Join(", ", parameterValues));
            sb.Append(")");
            return sb.ToString();
        }
    }
}

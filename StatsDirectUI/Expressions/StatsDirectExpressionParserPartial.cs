using System;
using System.Text.RegularExpressions;

namespace StatsDirect.Expressions
{
    partial class StatsDirectExpressionParser
    {
        private VariableNode ParseVariable(string variableName)
        {
            // Variables are V, Vn, X, or Xn.  Anything else is an error.
            Regex r = new Regex("^[VvXx]([1-9][0-9]*)?$");
            if (!r.Match(variableName).Success)
                throw new Exception("'" + variableName + "' is not a valid variable reference.  Variable references must be of the form X, X1, X2, X27 etc. If you used " + variableName + " as a named parameter in a function, make sure you have specified '" + variableName + " := value', not '" + variableName + " = value'");

            // Variables in the expression are 1-based.
            return new VariableNode { Index = variableName.Length == 1 ? 1 : int.Parse(variableName.Substring(1)) };
        }

        private StringNode ParseString (string rawText)
        {
            // At present, there are no metacharacters within the string; it's just a case of topping and tailing the quoted string.
            return new StringNode { Value = rawText.Substring(1, rawText.Length - 2) };
        }
    }
}

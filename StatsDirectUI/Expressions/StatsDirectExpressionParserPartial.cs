using System;
using System.Text.RegularExpressions;

namespace StatsDirect.Expressions
{
    partial class StatsDirectExpressionParser
    {
        private static VariableNode ParseVariable(string variableName)
        {
            // Variables are V, Vn, X, or Xn.  Anything else is an error.
            Regex r = new("^[VvXx]([1-9][0-9]*)?$");
            if (!r.Match(variableName).Success)
                throw new Exception("'" + variableName + "' is not a valid variable reference. Variable references must be of the form X, X1, X2, X27 etc. If you used " + variableName + " as a named parameter in a function, make sure you have specified '" + variableName + " := value', not '" + variableName + " = value'");

            // Variables in the expression are 1-based.
            return new VariableNode { Index = variableName.Length == 1 ? 1 : int.Parse(variableName.Substring(1)) };
        }

        /// <summary>
        /// The negative of a node. A minus sign on a number is folded into the number, so that an expression that was valid
        /// before unary minus existed (when the grammar only knew "MINUS INTEGER" and "MINUS FLOAT") produces the same tree
        /// and the same C# as it did then; on anything else it is a Negate node, rendered as SDMath.Negate(operand).
        /// </summary>
        private static INode Negate(INode node)
        {
            if (node is IntegerNode integerNode)
                return new IntegerNode { Value = 0 - integerNode.Value };
            if (node is DoubleNode doubleNode)
                return new DoubleNode { Value = 0.0 - doubleNode.Value };
            return new MonadicNode { Operator = MonadicOperator.Negate, Node = node };
        }

        /// <summary>
        /// A whole number: an IntegerNode when it fits an Int32, otherwise a DoubleNode, so that 3000000000 is a number and not an
        /// overflow error. Text that is not all digits (a thousands separator, which int.Parse has never accepted here) goes
        /// through int.Parse as before and stays an error, rather than quietly becoming a different number in an argument list.
        /// </summary>
        private static INode ParseInteger(string text)
        {
            if (int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value))
                return new IntegerNode { Value = value };
            bool allDigits = text.Length > 0;
            foreach (char c in text)
                if (c < '0' || c > '9')
                    allDigits = false;
            if (allDigits)
                return new DoubleNode { Value = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture) };
            return new IntegerNode { Value = int.Parse(text) };
        }

        private static StringNode ParseString (string rawText)
        {
            // At present, there are no metacharacters within the string; it's just a case of topping and tailing the quoted string.
            return new StringNode { Value = rawText.Substring(1, rawText.Length - 2) };
        }
    }
}

using System;
using Antlr.Runtime;

namespace StatsDirect.Expressions
{
    public class Converter
    {
        public static string ConvertToCSharp(string expr)
        {
            ANTLRStringStream input = new ANTLRStringStream(expr);
            StatsDirectExpressionLexer lexer = new StatsDirectExpressionLexer(input);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            StatsDirectExpressionParser parser = new StatsDirectExpressionParser(tokens);
            string retval = parser.start();
            if (parser.Failed)
            {
                throw new Exception("Couldn't parse your expression: " + parser.NumberOfSyntaxErrors + " error(s)");
            }
            if (null == retval)
                throw new Exception("Syntax error");
            return retval;
        }
    }
}
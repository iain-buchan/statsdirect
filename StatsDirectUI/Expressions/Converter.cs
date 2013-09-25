using System;
using Antlr4.Runtime;

namespace StatsDirect.Expressions
{
    public class Converter
    {
        public static string ConvertToCSharp(string expr)
        {
            AntlrInputStream input = new AntlrInputStream(expr);
            StatsDirectExpressionLexer lexer = new StatsDirectExpressionLexer(input);
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            StatsDirectExpressionParser parser = new StatsDirectExpressionParser(tokenStream);
            StatsDirect.Expressions.StatsDirectExpressionParser.RContext retval = parser.r();
            if (parser.NumberOfSyntaxErrors > 0)
            {
                throw new Exception("Couldn't parse your expression: " + parser.NumberOfSyntaxErrors + " error(s)");
            }
            // The parser seems to dislike recognising EOF (for some reason - TODO: find out why) so instead test that we're at EOF at the end of the parse
            if (!"<EOF>".Equals(parser.CurrentToken.Text))
                throw new Exception("Couldn't parse your expression: syntax error near \"" + parser.CurrentToken.Text + "\"");
            if (null == retval || null == retval.builtExpression)
                throw new Exception("Syntax error");
            return retval.builtExpression;
        }
    }
}
using System;
using Antlr4.Runtime;
using System.Text;
using System.Globalization;

namespace StatsDirect.Expressions
{
    public class Converter
    {
        public static string ConvertToCSharp(string expr, DataType[] passedVariableTypes)
        {
            // Spaces in the input stream get confused with spaces in thousand separators, so smash spaces if the thousands separator is spaces.
            if (" ".Equals(CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator))
                expr = expr.Replace(" ", "");

            AntlrInputStream input = new AntlrInputStream(expr);
            StatsDirectExpressionLexer lexer = new StatsDirectExpressionLexer(input);
            lexer.Separators = GetSeparatorStructure();
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            StatsDirectExpressionParser parser = new StatsDirectExpressionParser(tokenStream);
            StringBuilder errorBuilder = new StringBuilder();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new AccumulateErrors(errorBuilder));
            StatsDirect.Expressions.StatsDirectExpressionParser.RContext retval = parser.r();
            if (parser.NumberOfSyntaxErrors > 0)
            {
                throw new Exception("Couldn't parse your expression: " + errorBuilder.ToString());
            }
            // The parser seems to dislike recognising EOF (for some reason - TODO: find out why) so instead test that we're at EOF at the end of the parse
            if (!"<EOF>".Equals(parser.CurrentToken.Text))
                throw new Exception("Couldn't parse your expression: syntax error near \"" + parser.CurrentToken.Text + "\"");
            if (null == retval || null == retval.node)
                throw new Exception("Syntax error");
            return new CSharpRenderer().Render(retval.node, passedVariableTypes);
        }

        private static StatsDirectExpressionLexer.SeparatorStructure GetSeparatorStructure()
        {
            CultureInfo c = CultureInfo.CurrentCulture;
            if (".".Equals(c.NumberFormat.NumberDecimalSeparator))
            {
                if (",".Equals(c.NumberFormat.NumberGroupSeparator))
                    return StatsDirectExpressionLexer.SeparatorStructure.CommaDot;
                else if (" ".Equals(c.NumberFormat.NumberGroupSeparator))
                    return StatsDirectExpressionLexer.SeparatorStructure.SpaceDot;
                else
                {
                    // TODO: Other formats
                    return StatsDirectExpressionLexer.SeparatorStructure.CommaDot;
                }
            }
            else if (",".Equals(c.NumberFormat.NumberDecimalSeparator))
            {
                return StatsDirectExpressionLexer.SeparatorStructure.DotComma;
            }
            else
            {
                // TODO: How to handle other locales?
                return StatsDirectExpressionLexer.SeparatorStructure.CommaDot;
            }
        }

        private class AccumulateErrors : IAntlrErrorListener<IToken>
        {
            private StringBuilder sb;

            public AccumulateErrors(StringBuilder sb)
            {
                this.sb = sb;
            }

            public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            {
                sb.AppendLine("Line " + line + ", character " + charPositionInLine + ": " + msg);
            }
        }
    }
}
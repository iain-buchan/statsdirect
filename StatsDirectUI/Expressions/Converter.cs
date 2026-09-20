using System;
using Antlr4.Runtime;
using System.Text;
using System.Globalization;

namespace StatsDirect.Expressions
{
    public class Converter
    {
        public static bool IsValid(string expr)
        {
            // Spaces in the input stream get confused with spaces in thousand separators, so smash spaces if the thousands separator is spaces.
            // TODO: This also smashes spaces in strings, which we don't want!
            if (" ".Equals(CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator))
                expr = expr.Replace(" ", string.Empty);

            AntlrInputStream input = new(expr);
            StatsDirectExpressionLexer lexer = new(input)
            {
                Separators = GetSeparatorStructure()
            };
            CommonTokenStream tokenStream = new(lexer);
            StatsDirectExpressionParser parser = new(tokenStream);
            StringBuilder errorBuilder = new();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new AccumulateErrors(errorBuilder));
            StatsDirectExpressionParser.RContext retval;
            try
            {
                retval = parser.r();
            }
            catch (Exception)
            {
                // the parser's own actions throw for such things as a name that is not a variable ("abc"): that is an invalid
                // expression, not a reason for the validation itself to fail
                return false;
            }
            if (parser.NumberOfSyntaxErrors > 0)
                return false;

            // The parser seems to dislike recognising EOF (for some reason - TODO: find out why) so instead test that we're at EOF at the end of the parse
            if (!"<EOF>".Equals(parser.CurrentToken.Text))
                return false;
            if (null == retval || null == retval.node)
                return false;
            return true;
        }

        public static string ConvertToCSharp(string expr, DataType[] passedVariableTypes, bool inputsAreObjects, out DataType resultType)
        {
            // Spaces in the input stream get confused with spaces in thousand separators, so smash spaces if the thousands separator is spaces.
            // TODO: This also smashes spaces in strings, which we don't want!
            if (" ".Equals(CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator))
                expr = expr.Replace(" ", string.Empty);

            AntlrInputStream input = new(expr);
            StatsDirectExpressionLexer lexer = new(input)
            {
                Separators = GetSeparatorStructure()
            };
            CommonTokenStream tokenStream = new(lexer);
            StatsDirectExpressionParser parser = new(tokenStream);
            StringBuilder errorBuilder = new();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new AccumulateErrors(errorBuilder));
            StatsDirectExpressionParser.RContext retval = parser.r();
            if (parser.NumberOfSyntaxErrors > 0)
                throw new Exception("Couldn't parse your expression: " + errorBuilder);

            // The parser seems to dislike recognising EOF (for some reason - TODO: find out why) so instead test that we're at EOF at the end of the parse
            if (!"<EOF>".Equals(parser.CurrentToken.Text))
                throw new Exception("Couldn't parse your expression: syntax error near \"" + parser.CurrentToken.Text + "\"");
            if (null == retval || null == retval.node)
                throw new Exception("Syntax error");
            return new CSharpRenderer().Render(retval.node, passedVariableTypes, inputsAreObjects, out resultType);
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
            private readonly StringBuilder sb;

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
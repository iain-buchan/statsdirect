using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtfConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (string path in Directory.GetFiles(@"C:\Sandbox\StatsDirect\StatsDirect3\StatsDirectUI\Assets\Template", "*.rtf", SearchOption.AllDirectories))
            {
                using (TextReader tr = File.OpenText(path))
                {

                }
            }
        }
        /*
        public static List<List<string>> Read(TextReader csvReader)
        {
            AntlrInputStream input = new AntlrInputStream(csvReader);
            RtfLexer lexer = new RtfLexer(input);
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            RtfParser parser = new RtfParser(tokenStream);
            StringBuilder errorBuilder = new StringBuilder();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new AccumulateErrors(errorBuilder));
            RtfParser.FileContext fileContext = parser.document();
            if (parser.NumberOfSyntaxErrors > 0)
                throw new Exception("Invalid CSV file: " + errorBuilder.ToString());

            // The parser seems to dislike recognising EOF (for some reason - TODO: find out why) so instead test that we're at EOF at the end of the parse
            if (!"<EOF>".Equals(parser.CurrentToken.Text))
                throw new Exception("Couldn't parse your expression: syntax error near \"" + parser.CurrentToken.Text + "\"");
            if (null == fileContext || null == fileContext.retval)
                throw new Exception("Syntax error");
            return fileContext.retval;
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
        */
    }
}

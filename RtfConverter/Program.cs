using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
                    // Parse the file into our intermediate format
                    Chunks rawChunks = Read(tr);

                    // Transform the format
                    FormatDigester digester = new FormatDigester();
                    digester.Process(rawChunks);

                    // Split up line beginnings and ends, and tabs, so that we can put separate attributes on them later
                    MarkerSplitter splitter = new MarkerSplitter();
                    splitter.Process(rawChunks);
                    Chunks withSplits = splitter.ProcessedChunks;

                    MarkTableParts(withSplits);
                    withSplits = AddTableRowCellBoundaries(withSplits);
                    ExpandFormattingToCellBoundaries(withSplits);
                    ColourTable colourTable = ExtractColourTable(withSplits);

                    // Render the result
                    CreoleRenderer renderer = new CreoleRenderer();
                    renderer.Process(withSplits, colourTable);
                    string outputPath = Path.ChangeExtension(path, ".creole");
                    using (Stream s = File.OpenWrite(outputPath))
                    {
                        using (TextWriter tw = new StreamWriter(s))
                        {
                            tw.Write(renderer.ToString());
                        }
                    }
                }
                // Console.ReadLine();
            }
        }

        private static ColourTable ExtractColourTable(Chunks linearChunks)
        {
            // Beware: this algorithm relies on the colour table always being emitted with blue last.
            ColourTable colourTable = new ColourTable();
            int tableIndex = 1;
            int lastRed = 0;
            int lastGreen = 0;
            int lastBlue = 0;
            foreach (Chunk chunk in linearChunks)
            {
                if (chunk is RtfControl)
                {
                    RtfControl c = (RtfControl)chunk;
                    switch (c.Keyword)
                    {
                        case "red":
                            lastRed = c.Value.Value;
                            break;
                        case "green":
                            lastGreen = c.Value.Value;
                            break;
                        case "blue":
                            lastBlue = c.Value.Value;
                            colourTable.ColourMappings[tableIndex++] = InterpretColour(lastRed, lastGreen, lastBlue);
                            break;
                        default:
                            // Not interested
                            break;
                    }
                }
            }
            return colourTable;
        }

        private static string InterpretColour(int red, int green, int blue)
        {
            if (IsDark(red) && IsDark(green) && IsDark(blue))
                return null;
            if (IsBright(red) && IsBright(green) && IsBright(blue))
                return null;
            if (IsDark(red) && IsDark(green) && IsBright(blue))
                return "ci";
            if (IsDark(red) && IsMid(green) && IsDark(blue))
                return "pval";
            if (IsBright(red) && IsDark(green) && IsDark(blue))
                return "warn";
            if (IsDark(red) && IsDark(green) && IsMid(blue))
                return "grandtotal";
            if (IsMid(red) && IsDark(green) && IsDark(blue))
                return "subtotal";
            if (IsMid(red) && IsMid(green) && IsDark(blue))
                return "warnabit";
            if (IsDark(red) && IsMid(green) && IsMid(blue))
                return "ci";
            if (IsMid(red) && IsMid(green) && IsMid(blue))
                return "!grey";
            return "!unknown";
        }

        private static bool IsDark(int level)
        {
            return level < 64;
        }

        private static bool IsMid(int level)
        {
            return level >= 64 && level < 192;
        }

        private static bool IsBright(int level)
        {
            return level >= 192;
        }

        private static void ExpandFormattingToCellBoundaries(Chunks linearChunks)
        {
            for (int probe = 0; probe < linearChunks.Count; probe++)
            {
                Chunk chunk = linearChunks[probe];
                // Find table row starts or ends that we don't already know about, and add cell boundaries just after and before them respectively
                if (chunk.AccumulatedFormat.IsPartOfTable && chunk is RtfControl && ((RtfControl)chunk).IsTableCellSeparator)
                {
                    if (((RtfControl)chunk).IsStart)
                    {
                        int probeForward = probe + 1;
                        while (probeForward < linearChunks.Count && linearChunks[probeForward] is RtfControl && ((RtfControl)linearChunks[probeForward]).IsIrrelevant)
                            probeForward++;
                        chunk.AccumulatedFormat.IsUnderlined = linearChunks[probeForward].AccumulatedFormat.IsUnderlined;
                    }
                    else
                    {
                        int probeBack = probe - 1;
                        while (probeBack > 0 && linearChunks[probeBack] is RtfControl && ((RtfControl)linearChunks[probeBack]).IsIrrelevant)
                            --probeBack;
                        chunk.AccumulatedFormat.IsUnderlined = linearChunks[probeBack].AccumulatedFormat.IsUnderlined;
                    }
                }
            }
        }

        private static Chunks AddTableRowCellBoundaries(Chunks linearChunks)
        {
            Chunks withBoundaries = new Chunks();
            for (int probe = 0; probe < linearChunks.Count; probe++)
            {
                // Find table row starts or ends that we don't already know about, and add cell boundaries just after and before them respectively
                if (linearChunks[probe].AccumulatedFormat.IsPartOfTable && linearChunks[probe] is RtfControl && ((RtfControl)linearChunks[probe]).IsNewline && !((RtfControl)linearChunks[probe]).IsStart)
                    withBoundaries.Add(new RtfControl("tab", null) { IsTableCellSeparator = true, AccumulatedFormat = linearChunks[probe - 1].AccumulatedFormat.Clone() });
                withBoundaries.Add(linearChunks[probe]);
                if (linearChunks[probe].AccumulatedFormat.IsPartOfTable && linearChunks[probe] is RtfControl && ((RtfControl)linearChunks[probe]).IsNewline && ((RtfControl)linearChunks[probe]).IsStart)
                    withBoundaries.Add(new RtfControl("tab", null) { IsStart = true, IsTableCellSeparator = true, AccumulatedFormat = linearChunks[probe].AccumulatedFormat.Clone() });
            }
            return withBoundaries;
        }

        private static void MarkTableParts(Chunks linearChunks)
        {
            for (int probe = 0; probe < linearChunks.Count; probe++)
            {
                // Find a cell boundary that we don't already know about
                if (linearChunks[probe] is RtfControl && ((RtfControl)linearChunks[probe]).IsTableCellSeparator && !linearChunks[probe].AccumulatedFormat.IsPartOfTable)
                {
                    // Mark all chunks back to the previous line start and forward to the next line end
                    for (int markBack = probe; markBack > 0 && !(linearChunks[markBack] is RtfControl && ((RtfControl)linearChunks[markBack]).IsNewline && !((RtfControl)linearChunks[markBack]).IsStart); --markBack)
                        linearChunks[markBack].AccumulatedFormat.IsPartOfTable = true;
                    for (int markForward = probe; markForward < linearChunks.Count && !(linearChunks[markForward] is RtfControl && ((RtfControl)linearChunks[markForward]).IsNewline && ((RtfControl)linearChunks[markForward]).IsStart); markForward++)
                        linearChunks[markForward].AccumulatedFormat.IsPartOfTable = true;
                }
            }
        }

        public static Chunks Read(TextReader csvReader)
        {
            AntlrInputStream input = new AntlrInputStream(csvReader);
            RtfLexer lexer = new RtfLexer(input);
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            RtfParser parser = new RtfParser(tokenStream);
            StringBuilder errorBuilder = new StringBuilder();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new AccumulateErrors(errorBuilder));
            RtfParser.DocumentContext fileContext = parser.document();
            if (parser.NumberOfSyntaxErrors > 0)
                throw new Exception("Invalid RTF file: " + errorBuilder.ToString());

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
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtfConverter
{
    class FormatDigester : IChunkVisitor
    {
        private Stack<AccumulatedFormat> formatStack;

        public FormatDigester()
        {
            formatStack = new Stack<AccumulatedFormat>();
            // Empty format as the base from which to work
            formatStack.Push(new AccumulatedFormat());
        }

        public void Process(Chunks victim)
        {
            Visit(victim);
        }

        public void Visit(Chunks victim)
        {
            formatStack.Push(formatStack.Peek().Clone());
            foreach (Chunk chunk in victim)
                chunk.Accept(this);
            formatStack.Pop();
        }

        public void Visit(RtfControl victim)
        {
            switch (victim.Keyword)
            {
                case "b":
                    formatStack.Peek().IsBold = (!victim.Value.HasValue) || 0 != victim.Value.Value;
                    victim.IsIrrelevant = true;
                    break;
                case "cf":
                    formatStack.Peek().ColourIndex = victim.Value.HasValue ? victim.Value.Value : 1;
                    victim.IsIrrelevant = true;
                    break;
                case "fs":
                    if ((!victim.Value.HasValue) || 20 != victim.Value.Value)
                        throw new Exception("Unexpected font size - I thought it was always 20");
                    victim.IsIrrelevant = true;
                    break;
                case "i":
                    formatStack.Peek().IsItalic = (!victim.Value.HasValue) || 0 != victim.Value.Value;
                    victim.IsIrrelevant = true;
                    break;
                case "par":
                    {
                        victim.IsNewline = true;
                        AccumulatedFormat f = formatStack.Peek();
                        f.IsUnderlined = false;
                        f.IsBold = false;
                        f.IsItalic = false;
                        f.IsSubscript = false;
                        f.IsSuperscript = false;
                        f.ColourIndex = 1;
                    }
                    break;
                case "pard":
                case "plain":
                    {
                        AccumulatedFormat f = formatStack.Peek();
                        f.IsUnderlined = false;
                        f.IsBold = false;
                        f.IsItalic = false;
                        f.IsSubscript = false;
                        f.IsSuperscript = false;
                        f.ColourIndex = 1;
                        victim.IsIrrelevant = true;
                    }
                    break;
                case "pntxta":
                    // Text suffix for paragraph numbering.  Not only do we need to ignore it, we also need to ignore its following text block - perhaps a . - or we'll get weirdness in the output.
                    victim.IsIrrelevant = true;
                    victim.SuppressFollowingText = true;
                    break;
                case "sub":
                    formatStack.Peek().IsSubscript = true;
                    victim.IsIrrelevant = true;
                    break;
                case "super":
                    formatStack.Peek().IsSuperscript = true;
                    victim.IsIrrelevant = true;
                    break;
                case "tab":
                    victim.IsTableCellSeparator = true;
                    break;
                case "ul":
                    formatStack.Peek().IsUnderlined = (!victim.Value.HasValue) || 0 != victim.Value.Value;
                    victim.IsIrrelevant = true;
                    break;
                case "ulnone":
                    formatStack.Peek().IsUnderlined = false;
                    victim.IsIrrelevant = true;
                    break;
                case "*":
                case "ansi":
                case "ansicpg":
                case "deff":
                case "deflang":
                case "deftab":
                case "f":
                case "fchars":
                case "fi": // First-line indent
                case "fonttbl":
                case "fprq": // Font pitch in font table
                case "horzdoc":
                case "lchars":
                case "li":
                case "pn":
                case "pndec":
                case "pnf":
                case "pnindent":
                case "pnlvlbody":
                case "pnstart":
                case "pntext": // Paragraph numbering text - by ignoring this, we're saying that we want the calculated text rather than calculating it ourselves.
                case "ri": // Right indent
                case "rtf":
                case "tx":
                case "uc":
                case "viewkind":
                    // Irrelevant in output
                    victim.IsIrrelevant = true;
                    break;
                case "blue":
                case "colortbl":
                case "fcharset":
                case "fmodern":
                case "froman":
                case "fswiss":
                case "green":
                case "red":
                    // Irrelevant in output and have crud after them that needs binning
                    victim.IsIrrelevant = true;
                    victim.SuppressFollowingText = true;
                    break;
                default:
                    // Do nothing
                    break;
            }
            victim.AccumulatedFormat = formatStack.Peek().Clone();
        }

        public void Visit(Substitution victim)
        {
            victim.AccumulatedFormat = formatStack.Peek().Clone();
        }

        public void Visit(StringChunk victim)
        {
            victim.AccumulatedFormat = formatStack.Peek().Clone();
        }

        public void Visit(Entity victim)
        {
            victim.AccumulatedFormat = formatStack.Peek().Clone();
        }

        public void Visit(BlockStart victim)
        {
            victim.AccumulatedFormat = formatStack.Peek().Clone();
        }

        public void Visit(BlockFinish victim)
        {
            victim.AccumulatedFormat = formatStack.Peek().Clone();
        }
    }
}

using System;
using System.Text;

namespace RtfConverter
{
    class CreoleRenderer : IChunkVisitor
    {
        private readonly StringBuilder sb;
        private AccumulatedFormat previousFormat;
        private ColourTable colourTable;
        private bool suppressNextText;

        public CreoleRenderer()
        {
            sb = new StringBuilder();
            previousFormat = new AccumulatedFormat();
        }

        public void Process(Chunks victim, ColourTable colourTable)
        {
            this.colourTable = colourTable;
            sb.AppendLine("<report>");
            Visit(victim);
            sb.AppendLine("</report>");
        }

        public void Visit(Chunks victim)
        {
            foreach (Chunk chunk in victim)
                chunk.Accept(this);
        }

        public void Visit(RtfControl victim)
        {
            if (!victim.IsIrrelevant)
            {
                MaybeEmitFormatting(victim.AccumulatedFormat, victim.IsTableCellSeparator, victim.IsStart);
                if (victim.IsNewline)
                {
                    if (victim.AccumulatedFormat.IsPartOfTable)
                    {
                        if (victim.IsStart)
                            sb.Append("<tr>");
                        else
                            sb.AppendLine("</tr>");
                    }
                    else
                    {
                        if (victim.IsStart)
                            sb.Append("<line>");
                        else
                            sb.AppendLine("</line>");
                    }
                }
                else if (victim.IsTableCellSeparator)
                {
                    if (victim.AccumulatedFormat.IsUnderlined)
                        sb.Append(victim.IsStart ? "<th>" : "</th>");
                    else
                        sb.Append(victim.IsStart ? "<td>" : "</td>");
                }
                else
                {
                    throw new Exception("Unknown RTF");
                }
            }
            suppressNextText = victim.SuppressFollowingText;
        }

        public void Visit(Substitution victim)
        {
            MaybeEmitFormatting(victim.AccumulatedFormat, false, false);
            sb.Append("<in>");
            sb.Append(victim.Variable);
            sb.Append("</in>");
        }

        public void Visit(StringChunk victim)
        {
            if (!victim.IsIrrelevant && !suppressNextText)
            {
                MaybeEmitFormatting(victim.AccumulatedFormat, false, false);
                sb.Append(victim.ToString().Replace("\r", "").Replace("\n", ""));
            }
        }

        public void Visit(Entity victim)
        {
            MaybeEmitFormatting(victim.AccumulatedFormat, false, false);
            sb.Append("&#x");
            sb.Append(victim.Hex);
            sb.Append(';');
        }

        public void Visit(BlockStart victim)
        {
            MaybeEmitFormatting(victim.AccumulatedFormat, false, false);
            sb.Append("<block name=\"");
            sb.Append(victim.Name);
            sb.Append("\">");
        }

        public void Visit(BlockFinish victim)
        {
            MaybeEmitFormatting(victim.AccumulatedFormat, false, false);
            sb.Append("</block>");
        }

        private void MaybeEmitFormatting(AccumulatedFormat toBe, bool isTableCellSeparator, bool isStart)
        {
            // Always end then start - also, always end in one order and start in reverse order to try to ensure nesting.
            // Ends
            if (previousFormat.IsSubscript && !toBe.IsSubscript)
                sb.Append("</sub>");
            if (previousFormat.IsSuperscript && !toBe.IsSuperscript)
                sb.Append("</sup>");
            // Colours typically represent kinds of thing - open late, close early.
            if (previousFormat.ColourIndex != toBe.ColourIndex && previousFormat.ColourIndex != 0)
            {
                string mapping = colourTable.ColourMappings[previousFormat.ColourIndex];
                if (null != mapping)
                {
                    if (mapping.StartsWith("!"))
                    {
                        // Breakpoint!
                    }
                    sb.Append("</");
                    sb.Append(mapping);
                    sb.Append('>');
                }
            }
            if (previousFormat.IsBold && !toBe.IsBold && previousFormat.IsUnderlined && !toBe.IsUnderlined)
                sb.Append("</title>");
            else
            {
                if (previousFormat.IsBold && !toBe.IsBold)
                    sb.Append("</b>");
                if (previousFormat.IsUnderlined && !toBe.IsUnderlined)
                    sb.Append("</u>");
            }
            if (previousFormat.IsItalic && !toBe.IsItalic)
                sb.Append("</i>");
            if (previousFormat.IsPartOfTable && !toBe.IsPartOfTable)
                sb.AppendLine("</table>");

            // Starts - don't render these for start cell separators in order to avoid <tr><u><th>.
            if (!(isTableCellSeparator && isStart))
            {
                if (toBe.IsPartOfTable && !previousFormat.IsPartOfTable)
                    sb.AppendLine("<table>");
                if (toBe.IsItalic && !previousFormat.IsItalic)
                    sb.Append("<i>");
                if (toBe.IsUnderlined && !previousFormat.IsUnderlined && toBe.IsBold && !previousFormat.IsBold)
                    sb.Append("<title>");
                else
                {
                    if (toBe.IsUnderlined && !previousFormat.IsUnderlined)
                        sb.Append("<u>");
                    if (toBe.IsBold && !previousFormat.IsBold)
                        sb.Append("<b>");
                }
                // Colours typically represent kinds of thing - open late, close early.
                if (previousFormat.ColourIndex != toBe.ColourIndex && toBe.ColourIndex != 0)
                {
                    string mapping = colourTable.ColourMappings[toBe.ColourIndex];
                    if (null != mapping)
                    {
                        if (mapping.StartsWith("!"))
                        {
                            // Breakpoint!
                        }
                        sb.Append('<');
                        sb.Append(mapping);
                        sb.Append('>');
                    }
                }
                if (toBe.IsSuperscript && !previousFormat.IsSuperscript)
                    sb.Append("<sup>");
                if (toBe.IsSubscript && !previousFormat.IsSubscript)
                    sb.Append("<sub>");
            }

            previousFormat = toBe;
        }

        public override string ToString()
        {
            return sb.ToString();
        }
    }
}

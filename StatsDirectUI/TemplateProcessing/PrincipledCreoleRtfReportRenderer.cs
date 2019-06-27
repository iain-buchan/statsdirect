using StatsDirect.Creole;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace StatsDirect.TemplateProcessing
{
    public class PrincipledCreoleRtfReportRenderer : ReportRenderer
    {
        const string RTF_REPORT_START = @"/split/{\rtf1\ansi\ansicpg1252\deff0\deflang2057{\fonttbl{\f0\fswiss Calibri;}{\f1\fswiss\fcharset0 Calibri;}{\f2\fswiss Courier New;}}{\colortbl ;\red0\green0\blue0;\red254\green254\blue254;\red0\green127\blue127;\red0\green0\blue255;\red0\green127\blue0;\red255\green0\blue0;\red127\green0\blue0;\red0\green0\blue127;\red127\green127\blue0;}\viewkind4\uc1\pard\li135\cf1\f0\fs20 ";
        const string RTF_REPORT_END = @"\par }";
        const string FirstCellOfTableMarker = "!!FIRSTCELLOFTABLE!!";

        public override string Render(ITemplateHost host, string template, ParameterBag substitutions)
        {
            ICreole<string> creole = CreoleReader.Parse<string>(template, out string _);
            return RTF_REPORT_START + creole.Accept(new InnerRtfReportRenderer(host, substitutions)) + RTF_REPORT_END;
        }

        private class RtfFormatHolder
        {
            public string Prefix { get; }
            public string Suffix { get; }

            public RtfFormatHolder(string prefix)
                : this(prefix, string.Empty)
            {
            }

            public RtfFormatHolder(string prefix, string suffix)
            {
                Prefix = prefix;
                Suffix = suffix;
            }
        }

        private class InnerRtfReportRenderer : ICreoleVisitor<string>
        {
            private static readonly Dictionary<string, RtfFormatHolder> rtfFormatting = new Dictionary<string, RtfFormatHolder>
            {
                // Colour table entries: 1=black, 2=white, 3=dark cyan, 4=blue (CI), 5=green (pval), 6=red (warn), 7=dark red (subtotal), 8=dark blue (model/grandtotal).
                { "b", new RtfFormatHolder(@"\b") },
                { "ci", new RtfFormatHolder(@"\cf4") },
                { "grandtotal", new RtfFormatHolder(@"\cf8") },
                { "i", new RtfFormatHolder(@"\i") },
                { "model", new RtfFormatHolder(@"\cf8") },
                { "pre", new RtfFormatHolder(@"\f2") },
                { "pval", new RtfFormatHolder(@"\cf5") },
                { "score", new RtfFormatHolder(@"\cf3") },
                { "sub", new RtfFormatHolder(@"\sub") },
                { "subtitle", new RtfFormatHolder(@"\ul", @"\par") },
                { "subtotal", new RtfFormatHolder(@"\cf7") },
                { "sup", new RtfFormatHolder(@"\sup") },
                { "title", new RtfFormatHolder(@"\ul\b", @"\par") },
                { "u", new RtfFormatHolder(@"\ul") },
                { "warn", new RtfFormatHolder(@"\cf6") }
            };

            private readonly Stack<ParameterBag> substitutionStack = new Stack<ParameterBag>();
            private readonly ITemplateHost host;

            /// <summary>
            /// State so that we can inject a little extra marker at the end of the first table cell in each table - used so that the RTF insertion can format the table later.
            /// </summary>
            private bool isFirstCellOfTable;

            public InnerRtfReportRenderer(ITemplateHost host, ParameterBag substitutions)
            {
                this.host = host;
                substitutionStack.Push(substitutions);
            }

            string ICreoleVisitor<string>.Visit(CreoleAttribute<string> victim)
            {
                // Should never see; ignore.
                return string.Empty;
            }

            string ICreoleVisitor<string>.Visit(CreoleBlock<string> victim)
            {
                if (null == victim.Contents)
                    return string.Empty;
                StringBuilder sb = new StringBuilder();
                if (substitutionStack.Peek().TryGetValue("*" + victim.Name, out FilledParameter innerList) && null != innerList && innerList.HasData)
                {
                    foreach (ParameterBag inner in innerList.AsParameterBagList)
                    {
                        substitutionStack.Push(inner);
                        sb.Append(victim.Contents.Accept(this));
                        substitutionStack.Pop();
                    }
                }
                return sb.ToString();
            }

            string ICreoleVisitor<string>.Visit(CreoleFormatting<string> victim)
            {
                return "{" + ToRtfPrefix(victim.Format) + " " + MaybeAccept(victim.Contents) + "}" + ToRtfSuffix(victim.Format);
            }

            private string ToRtfPrefix(string format)
            {
                return rtfFormatting[format].Prefix;
            }

            private string ToRtfSuffix(string format)
            {
                return rtfFormatting[format].Suffix;
            }

            string ICreoleVisitor<string>.Visit(CreoleInclude<string> victim)
            {
                // TODO: We should perhaps cache parsed templates in case they are used many times - this is expensive on repeated calls.
                string template = GetContent(victim.Source);
                ICreole<string> creole = CreoleReader.Parse<string>(template, out string _);
                return creole.Accept(this);
            }

            string ICreoleVisitor<string>.Visit(CreoleList<string> victim)
            {
                StringBuilder sb = new StringBuilder();
                foreach (ICreole<string> v in victim)
                    sb.Append(v.Accept(this));
                return sb.ToString();
            }

            string ICreoleVisitor<string>.Visit(CreoleSubstitution<string> victim)
            {
                object value = FindValue(victim.Path);
                if (null == value)
                    return string.Empty;
                if (value is string stringValue)
                    return stringValue;
                if (value is int intValue)
                    return intValue.ToString();
                if (value is IRenderable renderable)
                    return new RtfRenderer(host).Render(renderable);
                if (value is double doubleValue)
                    switch (victim.Format)
                    {
                        case "pval":
                            return host.pval(doubleValue);
                        case "pval_half":
                            return host.pval_half(doubleValue);
                        case "roundu":
                            return host.RoundU(doubleValue);
                        case "roundx":
                            return host.RoundU(doubleValue);
                        case "zvalp1":
                            return host.pval(zvalp1(doubleValue));
                        case "zvalp2":
                            return host.pval(zvalp2(doubleValue));
                        case "default":
                            return doubleValue.ToString();
                        default:
                            // TODO: Warn.
                            return value.ToString();
                    }
                // Nothing we know how to render specially, so just call ToString() on it and hope.
                return value.ToString();
            }

            private object FindValue(string path)
            {
                foreach (ParameterBag candidate in substitutionStack)
                    if (candidate.TryGetValue(path, out FilledParameter value))
                        return value.Data;
                // If we get here, no such value exists.
                return null;
            }

            string ICreoleVisitor<string>.Visit(CreoleTable<string> victim)
            {
                isFirstCellOfTable = true;
                return "{" + MaybeAccept(victim.Contents) + @"}\par ";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableRow<string> victim)
            {
                return @"\trowd\trgaph135\trleft0\trautofit1"
                    + MaybeToCellsDefinition(victim.Contents) + " "
                    + MaybeAccept(victim.Contents)
                    + @"\row ";
            }

            private string MaybeToCellsDefinition(ICreole<string> contents)
            {
                return null == contents
                    ? string.Empty
                    : contents.Accept(new CellsDefinitionRenderer(substitutionStack.Peek()));
            }

            string ICreoleVisitor<string>.Visit(CreoleTableDetail<string> victim)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(@"\pard\intbl ");
                sb.Append(MaybeAccept(victim.Contents));
                if (isFirstCellOfTable)
                {
                    sb.Append(FirstCellOfTableMarker);
                    isFirstCellOfTable = false;
                }
                sb.Append(@"\cell ");
                for (int spanner = 1; spanner < victim.Colspan; spanner++)
                    sb.Append(@"\pard\intbl\cell ");
                return sb.ToString();
            }

            string ICreoleVisitor<string>.Visit(CreoleTableHeader<string> victim)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(@"\pard\intbl {\ul ");
                sb.Append(MaybeAccept(victim.Contents));
                sb.Append(@"}");
                if (isFirstCellOfTable)
                {
                    sb.Append(FirstCellOfTableMarker);
                    isFirstCellOfTable = false;
                }
                sb.Append(@"\cell ");
                for (int spanner = 1; spanner < victim.Colspan; spanner++)
                    sb.Append(@"\pard\intbl {\ul}\cell ");
                return sb.ToString();
            }

            string ICreoleVisitor<string>.Visit(CreoleText<string> victim)
            {
                return victim.Text;
            }

            string ICreoleVisitor<string>.Visit(CreoleLineBreak<string> victim)
            {
                return @"\par ";
            }

            string ICreoleVisitor<string>.Visit(CreoleParagraph<string> victim)
            {
                return @"\par "
                    + MaybeAccept(victim.Contents)
                    + @"\par ";
            }

            string ICreoleVisitor<string>.Visit(CreoleEntity<string> victim)
            {
                return @"\'" + ((int)victim.Value).ToString("X");
            }

            private string MaybeAccept(ICreole<string> victimOrNull)
            {
                return null == victimOrNull ? string.Empty : victimOrNull.Accept(this);
            }

            double zvalp1(double xz)
            {
                double p = 1 - Numerics.PDF.alnorm(xz);
                if (p > 1 - p)
                    p = 1 - p;
                return p;
            }

            double zvalp2(double xz)
            {
                double p = 1 - Numerics.PDF.alnorm(xz);
                if (p > 1 - p)
                    p = 1 - p;
                return p * 2;
            }
        }

        private class CellsDefinitionRenderer : ICreoleVisitor<string>
        {
            private readonly Stack<ParameterBag> substitutionStack = new Stack<ParameterBag>();

            public CellsDefinitionRenderer(ParameterBag substitutions)
            {
                substitutionStack.Push(substitutions);
            }

            string ICreoleVisitor<string>.Visit(CreoleList<string> victim)
            {
                StringBuilder sb = new StringBuilder();
                foreach (ICreole<string> v in victim)
                    sb.Append(v.Accept(this));
                return sb.ToString();
            }

            string ICreoleVisitor<string>.Visit(CreoleAttribute<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleBlock<string> victim)
            {
                if (null == victim.Contents)
                    return string.Empty;
                StringBuilder sb = new StringBuilder();
                if (substitutionStack.Peek().TryGetValue("*" + victim.Name, out FilledParameter innerList) && null != innerList && innerList.HasData)
                {
                    foreach (ParameterBag inner in innerList.AsParameterBagList)
                    {
                        substitutionStack.Push(inner);
                        sb.Append(MaybeAccept(victim.Contents));
                        substitutionStack.Pop();
                    }
                }
                return sb.ToString();
            }
            string ICreoleVisitor<string>.Visit(CreoleEntity<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleFormatting<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleInclude<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleLineBreak<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleParagraph<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleSubstitution<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleTable<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleTableRow<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleTableDetail<string> victim) => CellDefinition(victim.Colspan);
            string ICreoleVisitor<string>.Visit(CreoleTableHeader<string> victim) => CellDefinition(victim.Colspan);
            string ICreoleVisitor<string>.Visit(CreoleText<string> victim) => string.Empty;

            private string MaybeAccept(ICreole<string> victimOrNull)
            {
                return null == victimOrNull ? string.Empty : victimOrNull.Accept(this);
            }

            private string CellDefinition(int colspan)
            {
                if (colspan == 1)
                    return @"\cellx0";
                StringBuilder sb = new StringBuilder();
                sb.Append(@"\clmgf\cellx0");
                for (int spanner = 1; spanner < colspan; spanner++)
                    sb.Append(@"\clmrg\cellx0");
                return sb.ToString();
            }
        }
    }
}

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

        public override string Render(ITemplateHost host, string template, ParameterBag substitutions)
        {
            ICreole<string> creole = CreoleReader.Parse<string>(template, out string _);
            return RTF_REPORT_START + creole.Accept(new InnerRtfReportRenderer(host, substitutions)) + RTF_REPORT_END;
        }

        private class InnerRtfReportRenderer : ICreoleVisitor<string>
        {
            private static readonly Dictionary<string, string> rtfFormatting = new Dictionary<string, string>
            {
                // Colour table entries: 1=black, 2=white, 3=dark cyan, 4=blue (CI), 5=green (pval), 6=red (warn), 7=dark red (subtotal), 8=dark blue (model/grandtotal).
                { "b", @"\b" },
                { "ci", @"\cf4" },
                { "grandtotal", @"\cf8" },
                { "i", @"\i" },
                { "model", @"\cf8" },
                { "pre", @"\f2" },
                { "pval", @"\cf5" },
                { "score", @"\cf3" },
                { "sub", @"\sub" },
                { "subtitle", @"\ul" },
                { "subtotal", @"\cf7" },
                { "sup", @"\sup" },
                { "title", @"\ul\b" },
                { "u", @"\ul" },
                { "warn", @"\cf6" }
            };

            private readonly Stack<ParameterBag> substitutionStack = new Stack<ParameterBag>();
            private readonly ITemplateHost host;

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
                if (substitutionStack.Peek().TryGetValue("*" + victim.Name, out FilledParameter innerList) && innerList.HasData)
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
                return "{" + ToRtfFormatting(victim.Format) + " " + victim.Contents.Accept(this) + "}";
            }

            private string ToRtfFormatting(string format)
            {
                return rtfFormatting[format];
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
                if (value is String stringValue)
                    return stringValue;
                if (value is IRenderable renderable)
                    return new RtfRenderer(host).Render(renderable);
                switch (victim.Format)
                {
                    case "pval":
                        return host.pval((double)value);
                    case "roundx":
                        return host.RoundU((double)value);
                    case "roundu":
                        return host.RoundU((double)value);
                    case "default":
                        return value.ToString();
                    default:
                        // TODO: Warn.
                        return value.ToString();
                }
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
                return "{" + victim.Contents.Accept(this) + @"}\par ";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableRow<string> victim)
            {
                return @"\trowd\trgaph135\trleft0\trautofit1"
                    + ToCellsDefinition(victim.Contents) + " "
                    + victim.Contents.Accept(this)
                    + @"\row ";
            }

            private string ToCellsDefinition(ICreole<string> contents)
            {
                return contents.Accept(new CellsDefinitionRenderer());
            }

            string ICreoleVisitor<string>.Visit(CreoleTableDetail<string> victim)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(@"\pard\intbl ");
                sb.Append(victim.Contents.Accept(this));
                sb.Append(@"\cell ");
                for (int spanner = 1; spanner < victim.Colspan; spanner++)
                    sb.Append(@"\pard\intbl\cell ");
                return sb.ToString();
            }

            string ICreoleVisitor<string>.Visit(CreoleTableHeader<string> victim)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(@"\pard\intbl {\ul ");
                sb.Append(victim.Contents.Accept(this));
                sb.Append(@"}\cell ");
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
                return (null == victim.Contents ? string.Empty : victim.Contents.Accept(this)) + @"\par\par ";
            }
        }

        private class CellsDefinitionRenderer : ICreoleVisitor<string>
        {
            string ICreoleVisitor<string>.Visit(CreoleList<string> victim)
            {
                StringBuilder sb = new StringBuilder();
                foreach (ICreole<string> v in victim)
                    sb.Append(v.Accept(this));
                return sb.ToString();
            }

            string ICreoleVisitor<string>.Visit(CreoleAttribute<string> victim) => string.Empty;
            string ICreoleVisitor<string>.Visit(CreoleBlock<string> victim) => string.Empty;
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

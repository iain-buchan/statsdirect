using StatsDirect.Creole;
using StatsDirect.Templates;
using System.Collections.Generic;
using System.Text;

namespace StatsDirect.TemplateProcessing
{
    public class CreoleHtmlReportRenderer : ReportRenderer
    {
        public override string Render(ITemplateHost host, string template, ParameterBag substitutions)
        {
            ICreole<string> creole = CreoleReader.Parse<string>(template, out string _);
            return creole.Accept(new InnerHtmlReportRenderer(host, substitutions));
        }

        private class InnerHtmlReportRenderer : ICreoleVisitor<string>
        {
            private static readonly Dictionary<string, IWrapper> rtfFormatting = new Dictionary<string, IWrapper>
            {
                { "b", new TagRenderer("b") },
                { "ci", new SpanRenderer("ci") },
                { "grandtotal", new SpanRenderer("grandtotal") },
                { "i", new TagRenderer("i") },
                { "model", new SpanRenderer("model") },
                { "pre", new TagRenderer("pre") },
                { "pval", new SpanRenderer("pval") },
                { "score", new SpanRenderer("score") },
                { "sub", new TagRenderer("sub") },
                { "subtitle", new TagRenderer("h2") },
                { "subtotal", new SpanRenderer("subtotal") },
                { "sup", new TagRenderer("sup") },
                { "title", new TagRenderer("h1") },
                { "u", new TagRenderer("u") },
                { "warn", new SpanRenderer("warn") }
            };

            private readonly Stack<ParameterBag> substitutionStack = new Stack<ParameterBag>();
            private readonly ITemplateHost host;

            public InnerHtmlReportRenderer(ITemplateHost host, ParameterBag substitutions)
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
                IWrapper wrapper = rtfFormatting[victim.Format];
                return wrapper.Open
                    + victim.Contents.Accept(this)
                    + wrapper.Close;
            }

            string ICreoleVisitor<string>.Visit(CreoleInclude<string> victim)
            {
                // TODO: We should perhaps cache parsed templates in case they are used many times - this is expensive on repeated calls.
                string template = GetContent(victim.Source);
                ICreole<string> creole = CreoleReader.Parse<string>(template, out string _);
                return creole.Accept(this);
            }

            string ICreoleVisitor<string>.Visit(CreoleLine<string> victim)
            {
                return (null == victim.Contents ? string.Empty : victim.Contents.Accept(this)) + @"<br />";
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
                if (value is IRenderable renderable)
                    return new HtmlRenderer(host).Render(renderable);
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
                return "<table>" + victim.Contents.Accept(this) + "</table>";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableRow<string> victim)
            {
                return @"<tr>"
                    + victim.Contents.Accept(this)
                    + @"</tr>";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableDetail<string> victim)
            {
                return @"<td>"
                    + victim.Contents.Accept(this)
                    + @"</td>";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableDetailFirst<string> victim)
            {
                return @"<td>"
                    + victim.Contents.Accept(this)
                    + @"</td>";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableDetailSpan<string> victim)
            {
                return @"<td></td>";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableHeader<string> victim)
            {
                return @"<th>"
                    + victim.Contents.Accept(this)
                    + @"</th>";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableHeaderFirst<string> victim)
            {
                return @"<th>"
                    + victim.Contents.Accept(this)
                    + @"</th>";
            }

            string ICreoleVisitor<string>.Visit(CreoleTableHeaderSpan<string> victim)
            {
                return @"<th></th>";
            }

            string ICreoleVisitor<string>.Visit(CreoleText<string> victim)
            {
                return victim.Text;
            }

            private interface IWrapper
            {
                string Open { get; }
                string Close { get; }
            }

            private class TagRenderer: IWrapper
            {
                private string Tag { get; }

                public TagRenderer(string tag)
                {
                    Tag = tag;
                }

                string IWrapper.Open => "<" + Tag + ">";

                string IWrapper.Close => "</" + Tag + ">";
            }

            private class SpanRenderer: IWrapper
            {
                private string CssClass { get; }

                public SpanRenderer(string cssClass)
                {
                    CssClass = cssClass;
                }

                string IWrapper.Open => "<span class=\"" + CssClass + "\">";

                string IWrapper.Close => "</span>";
            }
        }
    }
}

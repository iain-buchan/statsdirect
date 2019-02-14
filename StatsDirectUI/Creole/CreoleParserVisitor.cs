using System;
using System.Linq;
using Antlr4.Runtime.Tree;

namespace StatsDirect.Creole
{
    class CreoleParserVisitor<TResult> : ICreoleParserVisitor<ICreole<TResult>>
    {
        ICreole<TResult> IParseTreeVisitor<ICreole<TResult>>.Visit(IParseTree tree)
        {
            throw new NotImplementedException();
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitAttribute(CreoleParser.AttributeContext context)
        {
            string rawValue = context.value.Text;
            return new CreoleAttribute<TResult> { Name = context.name.Text, Value = rawValue.Substring(1, rawValue.Length - 2) };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitAttributes(CreoleParser.AttributesContext context)
        {
            throw new NotImplementedException();
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitBlock(CreoleParser.BlockContext context)
        {
            return new CreoleBlock<TResult>
            {
                Contents = context.content().Accept(this),
                Name = ((CreoleAttribute<TResult>)context.attribute().Accept(this)).Value
            };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitChardata(CreoleParser.ChardataContext context)
        {
            throw new NotImplementedException();
        }

        ICreole<TResult> IParseTreeVisitor<ICreole<TResult>>.VisitChildren(IRuleNode node)
        {
            throw new NotImplementedException();
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitCompoundSubstitution(CreoleParser.CompoundSubstitutionContext context)
        {
            return new CreoleSubstitution<TResult>
            {
                Path = context.path.Text,
                Format = context.format.Text.Substring(1) // Token includes the separating colon
            };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitContent(CreoleParser.ContentContext context)
        {
            if (0 == context.ChildCount)
                return null;
            if (1 == context.ChildCount)
                return context.children[0].Accept(this);
            return new CreoleList<TResult>(context.children.Select(child => child.Accept(this)));
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitDocument(CreoleParser.DocumentContext context)
        {
            CreoleParser.ElementContext[] elements = context.element();
            if (0 == elements.Length)
                return null;
            if (1 == elements.Length)
                return elements[0].Accept(this);
            return new CreoleList<TResult>(elements.Select(element => element.Accept(this)));
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitElement(CreoleParser.ElementContext context)
        {
            throw new NotImplementedException();
        }

        ICreole<TResult> IParseTreeVisitor<ICreole<TResult>>.VisitErrorNode(IErrorNode node)
        {
            throw new NotImplementedException();
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitFormatting(CreoleParser.FormattingContext context)
        {
            return new CreoleFormatting<TResult>()
            {
                Contents = context.content().Accept(this),
                Format = context.tag.Text
            };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitInclude(CreoleParser.IncludeContext context)
        {
            return new CreoleInclude<TResult>
            {
                Source = ((CreoleAttribute<TResult>)context.attribute().Accept(this)).Value
            };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitInlineContent(CreoleParser.InlineContentContext context)
        {
            return context.children[0].Accept(this);
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitLineBreak(CreoleParser.LineBreakContext context)
        {
            return new CreoleLineBreak<TResult>();
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitParagraph(CreoleParser.ParagraphContext context)
        {
            return new CreoleParagraph<TResult>() { Contents = context.content().Accept(this) };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitReport(CreoleParser.ReportContext context)
        {
            return context.content().Accept(this);
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitSignificantText(CreoleParser.SignificantTextContext context)
        {
            return new CreoleText<TResult> { Text = context.GetText() };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitSimpleSubstitution(CreoleParser.SimpleSubstitutionContext context)
        {
            return new CreoleSubstitution<TResult>
            {
                Path = context.path.Text,
                Format = "default"
            };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitSubstitution(CreoleParser.SubstitutionContext context)
        {
            throw new NotImplementedException();
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitTable(CreoleParser.TableContext context)
        {
            return new CreoleTable<TResult> { Contents = context.content().Accept(this) };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitTableDetail(CreoleParser.TableDetailContext context)
        {
            return new CreoleTableDetail<TResult>
            {
                Contents = context.content().Accept(this),
                Colspan = (null == context.colspan) ? 1 : int.Parse(((CreoleAttribute<TResult>)context.attribute().Accept(this)).Value)
            };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitTableHeader(CreoleParser.TableHeaderContext context)
        {
            return new CreoleTableHeader<TResult>()
            {
                Contents = context.content().Accept(this),
                Colspan = (null == context.colspan) ? 1 : int.Parse(((CreoleAttribute<TResult>)context.attribute().Accept(this)).Value)
            };
        }

        ICreole<TResult> ICreoleParserVisitor<ICreole<TResult>>.VisitTableRow(CreoleParser.TableRowContext context)
        {
            return new CreoleTableRow<TResult>() { Contents = context.content().Accept(this) };
        }

        ICreole<TResult> IParseTreeVisitor<ICreole<TResult>>.VisitTerminal(ITerminalNode node)
        {
            throw new NotImplementedException();
        }
    }
}

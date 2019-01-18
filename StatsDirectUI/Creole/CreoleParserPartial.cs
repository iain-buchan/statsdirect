using Antlr4.Runtime;
using System.Collections.Generic;

namespace StatsDirect.Creole
{
    partial class CreoleParser
    {
        private static ISet<string> validTags = new HashSet<string>
        {
            "b",
            "block",
            "ci",
            "grandtotal",
            "i",
            "in",
            "include",
            "inp",
            "inu",
            "inx",
            "line",
            "model",
            "pre",
            "pval",
            "report",
            "score",
            "sub",
            "subtitle",
            "subtotal",
            "sup",
            "td",
            "tdfirst",
            "tdspan",
            "th",
            "thfirst",
            "thspan",
            "table",
            "title",
            "tr",
            "u",
            "warn"
        };

        private bool TagsMatch(IToken a, IToken b)
        {
            return a.Text.Equals(b.Text);
        }

        private bool IsValidTag(IToken tag)
        {
            return validTags.Contains(tag.Text);
        }
    }
}

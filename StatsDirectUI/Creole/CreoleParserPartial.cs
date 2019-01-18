using Antlr4.Runtime;

namespace StatsDirect.Creole
{
    partial class CreoleParser
    {
        private bool TagsMatch(IToken a, IToken b)
        {
            return a.Text.Equals(b.Text);
        }
    }
}

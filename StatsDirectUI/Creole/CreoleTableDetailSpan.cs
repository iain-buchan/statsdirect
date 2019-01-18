namespace StatsDirect.Creole
{
    public class CreoleTableDetailSpan<TResult> : ICreole<TResult>
    {
        TResult ICreole<TResult>.Accept(ICreoleVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}

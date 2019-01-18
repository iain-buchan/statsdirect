namespace StatsDirect.Creole
{
    public class CreoleTableHeader<TResult> : CreoleContainer<TResult>, ICreole<TResult>
    {
        TResult ICreole<TResult>.Accept(ICreoleVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}

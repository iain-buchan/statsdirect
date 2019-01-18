namespace StatsDirect.Creole
{
    public class CreoleBlock<TResult> : CreoleContainer<TResult>, ICreole<TResult>
    {
        public string Name { get; set; }

        TResult ICreole<TResult>.Accept(ICreoleVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}

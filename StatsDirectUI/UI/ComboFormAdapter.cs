namespace StatsDirect.UI
{
    internal sealed class ComboFormAdapter
    {
        public StatsDirectForm? StatsDirectForm { get; }

        public ComboFormAdapter(StatsDirectForm? statsDirectForm)
        {
            StatsDirectForm = statsDirectForm;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not ComboFormAdapter rhs)
                return false;
            // Check for null forms on either side.  If both are null, we're OK...
            if (null == StatsDirectForm && null == rhs.StatsDirectForm)
                return true;
            // ... otherwise if either is null, the other isn't...
            if (null == StatsDirectForm || null == rhs.StatsDirectForm)
                return false;
            // ... otherwise both are non-null.
            return rhs.StatsDirectForm.Equals(StatsDirectForm);
        }

        public override int GetHashCode() =>
            null == StatsDirectForm
                ? 0
                : StatsDirectForm.GetHashCode();

        public override string ToString() =>
            null == StatsDirectForm
                ? "New report"
                : StatsDirectForm.Text;
    }
}
namespace StatsDirect.UI
{
    internal sealed class ComboFormAdapter
    {
        private readonly StatsDirectForm statsDirectForm;

        public ComboFormAdapter(StatsDirectForm statsDirectForm)
        {
            this.statsDirectForm = statsDirectForm;
        }

        public StatsDirectForm StatsDirectForm
        {
            get { return statsDirectForm; }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ComboFormAdapter))
                return false;
            ComboFormAdapter rhs = (ComboFormAdapter)obj;
            // Check for null forms on either side.  If both are null, we're OK...
            if (null == statsDirectForm && null == rhs.statsDirectForm)
                return true;
            // ... otherwise if either is null, the other isn't...
            if (null == statsDirectForm || null == rhs.statsDirectForm)
                return false;
            // ... otherwise both are non-null.
            return rhs.statsDirectForm.Equals(statsDirectForm);
        }

        public override int GetHashCode()
        {
            return null == statsDirectForm ? 0 : statsDirectForm.GetHashCode();
        }

        public override string ToString()
        {
            return null == statsDirectForm ? "New report" : statsDirectForm.Text;
        }
    }
}